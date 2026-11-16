using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class Lander : MonoBehaviour
{
    public static Lander Instance { get; private set; }

    private Rigidbody2D rb;
    private SpriteRenderer sr;

    private float upSpeed = 14f;
    private float turnSpeed = 10f;

    private float rotationForce = 0.2f;
    private float fuelAmount = 1000;

    private float landingSpeed;

    [SerializeField] private LanderLegs leftLeg;
    [SerializeField] private LanderLegs rightLeg;

    public event EventHandler OnCrash;
    public event EventHandler OnUp;
    public event EventHandler OnLeft;
    public event EventHandler OnRight;
    public event EventHandler OnBeforeForce;
    
    private StateMachine state;
    private enum StateMachine
    {
        normal,
        gameOver,
    }

    [SerializeField] private Sprite idle;
    [SerializeField] private Sprite left;
    [SerializeField] private Sprite right;
    [SerializeField] private Sprite happy;
    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void FixedUpdate()
    {
        switch (state)
        {
            default:
                sr.sprite = idle;

                OnBeforeForce?.Invoke(this, EventArgs.Empty);

                if(Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed)
                {
                    OnFuelConsume();
                }
                if(fuelAmount >= 0)
                {
                    if (Keyboard.current.wKey.isPressed)
                    {

                        rb.AddForce(upSpeed * transform.up);
                        OnUp?.Invoke(this, EventArgs.Empty);

                        float straightenTorque = 100f;
                        float currentAngle = Mathf.DeltaAngle(rb.rotation, 0f);
                        rb.AddTorque(currentAngle * straightenTorque * Time.deltaTime);

                    }
                    if (Keyboard.current.aKey.isPressed)
                    {

                        sr.sprite = left;
                        OnLeft?.Invoke(this, EventArgs.Empty);
                        rb.AddForce(turnSpeed * (-transform.right));
                        rb.AddTorque(rotationForce);

                    }
                    if (Keyboard.current.dKey.isPressed)
                    {
                        sr.sprite = right;
                        OnRight?.Invoke(this, EventArgs.Empty);
                        rb.AddForce(turnSpeed * transform.right);
                        rb.AddTorque(-rotationForce);

                    }

                    float currentRotation = rb.rotation;

                    currentRotation = Mathf.Clamp(currentRotation, -2f, 2f);

                    rb.rotation = currentRotation;
                }

              
                break;
            case (StateMachine.gameOver):
                break;

        }



    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (state == StateMachine.gameOver) return;

        if (collision.gameObject.TryGetComponent(out Pad pad))
        {
            landingSpeed = collision.relativeVelocity.magnitude;
            Invoke(nameof(EvaluateLandingStatus), 0.10f);
        }
        else
        {
            Debug.Log("Hit an obstacle! Crash!");
            TriggerCrash();
        }
    }

    private void OnFuelConsume()
    {
        fuelAmount -= 5 * Time.deltaTime;
        //Debug.Log(fuelAmount);
    }

    private void EvaluateLandingStatus()
    {
        if (state == StateMachine.gameOver) return;

        float maxLandingSpeed = 6f;

        if (leftLeg.isGrounded && rightLeg.isGrounded)
        {
            if (landingSpeed <= maxLandingSpeed)
            {
                Debug.Log(landingSpeed);
                Debug.Log("Successful Landing!");
                
                state = StateMachine.gameOver;
                sr.sprite = happy;
            }
            else
            {
                Debug.Log("Too fast! Crash!");
                TriggerCrash();
            }
        }
        else
        {
            // One leg missed or the main body slammed down single-sided
            Debug.Log("Body hit the pad before legs stabilized! Crash!");
            TriggerCrash();
        }
    }

    private void TriggerCrash()
    {
        OnCrash?.Invoke(this, EventArgs.Empty);
        state = StateMachine.gameOver;
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.TryGetComponent(out CatFood fishFood))
        {
            fishFood.DestroySelf();
        }
        if (other.gameObject.TryGetComponent(out Fish fish)
)
        {
            fish.DestroySelf();
        }

    }

}
