using UnityEngine;

/// <summary>
/// Marks an obstacle as a hazard. Any non-pad collision already crashes the lander;
/// this component only changes the crash message and lets hazards move or spin.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Hazard : MonoBehaviour
{
    [Tooltip("Ping-pong movement: position = start + offset * sin(2*pi*t/period + phase).")]
    [SerializeField] private Vector2 moveOffset = Vector2.zero;
    [SerializeField] private float movePeriod = 4f;
    [SerializeField, Range(0f, 1f)] private float movePhase = 0f;
    [Tooltip("Degrees per second.")]
    [SerializeField] private float spinSpeed = 0f;

    private Rigidbody2D rb;
    private Vector2 startPosition;
    private float time;

    public void Setup(Vector2 offset, float period, float phase, float spin)
    {
        moveOffset = offset;
        movePeriod = period;
        movePhase = phase;
        spinSpeed = spin;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        startPosition = rb.position;
    }

    private void FixedUpdate()
    {
        time += Time.fixedDeltaTime;
        if (moveOffset != Vector2.zero && movePeriod > 0f)
        {
            float s = Mathf.Sin((time / movePeriod + movePhase) * Mathf.PI * 2f);
            rb.MovePosition(startPosition + moveOffset * s);
        }
        if (!Mathf.Approximately(spinSpeed, 0f))
        {
            rb.MoveRotation(rb.rotation + spinSpeed * Time.fixedDeltaTime);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (moveOffset == Vector2.zero) return;
        Vector2 p = Application.isPlaying ? startPosition : (Vector2)transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(p - moveOffset, p + moveOffset);
    }
}
