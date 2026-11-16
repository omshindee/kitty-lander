using UnityEngine;

/// <summary>Gentle floating animation for pickups (moves only the visual child).</summary>
public class Bobber : MonoBehaviour
{
    [SerializeField] private float amplitude = 0.12f;
    [SerializeField] private float speed = 2.2f;

    private Transform visual;
    private Vector3 basePos;
    private float offset;

    private void Start()
    {
        visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
        basePos = visual.localPosition;
        offset = (transform.position.x * 0.37f + transform.position.y * 0.11f) % 6.28f;
    }

    private void Update()
    {
        if (visual == transform) return;
        visual.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * speed + offset) * amplitude;
    }
}
