using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class Lander : MonoBehaviour
{
    public static Lander Instance { get; private set; }

    public enum State
    {
        Ready,    // waiting for the first key press, physics paused
        Flying,
        Landed,
        Crashed,
    }

    private Rigidbody2D rb;
    private SpriteRenderer sr;

    [Header("Flight")]
    [SerializeField] private float upSpeed = 14f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float rotationForce = 0.2f;

    [Header("Fuel (units = seconds of main-thruster burn)")]
    [SerializeField] private float maxFuel = 30f;
    [SerializeField] private float mainBurnPerSecond = 1f;
    [SerializeField] private float sideBurnPerSecond = 0.5f;

    [Header("Landing")]
    [SerializeField] private float maxLandingSpeed = 6f;

    [SerializeField] private LanderLegs leftLeg;
    [SerializeField] private LanderLegs rightLeg;

    [SerializeField] private Sprite idle;
    [SerializeField] private Sprite left;
    [SerializeField] private Sprite right;
    [SerializeField] private Sprite happy;

    public event EventHandler OnCrash;
    public event EventHandler OnLanded;
    public event EventHandler OnLaunch;
    public event EventHandler OnUp;
    public event EventHandler OnLeft;
    public event EventHandler OnRight;
    public event EventHandler OnBeforeForce;
    public event EventHandler<float> OnRefuel;

    public State CurrentState { get; private set; } = State.Ready;
    public float Fuel { get; private set; }
    public float MaxFuel => maxFuel;
    public float FuelNormalized => maxFuel > 0f ? Mathf.Clamp01(Fuel / maxFuel) : 0f;
    public float MaxLandingSpeed => maxLandingSpeed;
    public float Speed => rb != null ? rb.linearVelocity.magnitude : 0f;
    public bool IsThrusting { get; private set; }
    public string CrashReason { get; private set; } = "";
    public float LandingSpeed => landingSpeed;

    private float landingSpeed;

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        Fuel = maxFuel;
        // Hold the lander in place until the player presses a key.
        rb.simulated = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Called by the GameManager to apply per-level settings.</summary>
    public void Configure(float fuel, float gravityScale)
    {
        maxFuel = fuel;
        Fuel = fuel;
        rb.gravityScale = gravityScale;
    }

    public void Refuel(float amount)
    {
        Fuel = Mathf.Min(maxFuel, Fuel + amount);
        OnRefuel?.Invoke(this, amount);
    }

    private static bool UpPressed()
    {
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && (k.wKey.isPressed || k.upArrowKey.isPressed || k.spaceKey.isPressed))
            || (g != null && (g.buttonSouth.isPressed || g.leftStick.y.ReadValue() > 0.5f || g.rightTrigger.isPressed));
    }

    private static bool LeftPressed()
    {
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && (k.aKey.isPressed || k.leftArrowKey.isPressed))
            || (g != null && (g.leftStick.x.ReadValue() < -0.5f || g.dpad.left.isPressed));
    }

    private static bool RightPressed()
    {
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && (k.dKey.isPressed || k.rightArrowKey.isPressed))
            || (g != null && (g.leftStick.x.ReadValue() > 0.5f || g.dpad.right.isPressed));
    }

    private void Update()
    {
        if (CurrentState == State.Ready && Time.timeScale > 0f && (UpPressed() || LeftPressed() || RightPressed()))
        {
            Launch();
        }
    }

    /// <summary>Starts the flight (normally triggered by the first key press).</summary>
    public void Launch()
    {
        if (CurrentState != State.Ready) return;
        CurrentState = State.Flying;
        rb.simulated = true;
        OnLaunch?.Invoke(this, EventArgs.Empty);
    }

    private void FixedUpdate()
    {
        IsThrusting = false;
        if (CurrentState != State.Flying) return;

        sr.sprite = idle;

        OnBeforeForce?.Invoke(this, EventArgs.Empty);

        bool up = UpPressed();
        bool leftKey = LeftPressed();
        bool rightKey = RightPressed();

        if (Fuel > 0f)
        {
            if (up) Fuel -= mainBurnPerSecond * Time.fixedDeltaTime;
            if (leftKey) Fuel -= sideBurnPerSecond * Time.fixedDeltaTime;
            if (rightKey) Fuel -= sideBurnPerSecond * Time.fixedDeltaTime;
            Fuel = Mathf.Max(0f, Fuel);

            IsThrusting = up || leftKey || rightKey;

            if (up)
            {
                rb.AddForce(upSpeed * transform.up);
                OnUp?.Invoke(this, EventArgs.Empty);

                float straightenTorque = 100f;
                float currentAngle = Mathf.DeltaAngle(rb.rotation, 0f);
                rb.AddTorque(currentAngle * straightenTorque * Time.fixedDeltaTime);
            }
            if (leftKey)
            {
                sr.sprite = left;
                OnLeft?.Invoke(this, EventArgs.Empty);
                rb.AddForce(turnSpeed * (-transform.right));
                rb.AddTorque(rotationForce);
            }
            if (rightKey)
            {
                sr.sprite = right;
                OnRight?.Invoke(this, EventArgs.Empty);
                rb.AddForce(turnSpeed * transform.right);
                rb.AddTorque(-rotationForce);
            }
        }

        // Keep the lander (almost) upright, like the original design.
        rb.rotation = Mathf.Clamp(rb.rotation, -2f, 2f);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (CurrentState != State.Flying) return;

        if (collision.gameObject.TryGetComponent(out Pad pad))
        {
            landingSpeed = collision.relativeVelocity.magnitude;
            CancelInvoke(nameof(EvaluateLandingStatus));
            Invoke(nameof(EvaluateLandingStatus), 0.10f);
        }
        else
        {
            TriggerCrash(collision.gameObject.GetComponentInParent<Hazard>() != null
                ? "Ouch! That looked sharp."
                : "Bonk! You hit the ground.");
        }
    }

    private void EvaluateLandingStatus()
    {
        if (CurrentState != State.Flying) return;

        if (leftLeg.isGrounded && rightLeg.isGrounded)
        {
            if (landingSpeed <= maxLandingSpeed)
            {
                CurrentState = State.Landed;
                sr.sprite = happy;
                IsThrusting = false;
                OnBeforeForce?.Invoke(this, EventArgs.Empty); // switch thrusters off
                OnLanded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                TriggerCrash("Too fast! Land more gently.");
            }
        }
        else
        {
            TriggerCrash("Land on both legs!");
        }
    }

    /// <summary>Crash the lander from outside (e.g. flying out of the level).</summary>
    public void ForceCrash(string reason)
    {
        if (CurrentState == State.Flying || CurrentState == State.Ready) TriggerCrash(reason);
    }

    private void TriggerCrash(string reason)
    {
        CrashReason = reason;
        CurrentState = State.Crashed;
        IsThrusting = false;
        OnCrash?.Invoke(this, EventArgs.Empty);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (CurrentState != State.Flying) return;

        if (other.gameObject.TryGetComponent(out CatFood food))
        {
            Refuel(food.FuelAmount);
            food.DestroySelf();
        }
        if (other.gameObject.TryGetComponent(out Fish fish))
        {
            fish.DestroySelf();
        }
    }
}
