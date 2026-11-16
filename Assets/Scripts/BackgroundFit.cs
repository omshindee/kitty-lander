using UnityEngine;

/// <summary>
/// Keeps a background sprite (child of the camera) scaled to cover the whole view,
/// with a slight parallax drift so it doesn't feel glued to the screen.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundFit : MonoBehaviour
{
    [SerializeField] private float parallax = 0.04f;
    [SerializeField] private float overscan = 1.12f;

    private SpriteRenderer sr;
    private Camera cam;

    private void OnEnable()
    {
        sr = GetComponent<SpriteRenderer>();
        cam = GetComponentInParent<Camera>();
    }

    private void LateUpdate()
    {
        if (cam == null || sr == null || sr.sprite == null) return;
        float h = cam.orthographicSize * 2f;
        float w = h * cam.aspect;
        Vector2 size = sr.sprite.bounds.size;
        float s = Mathf.Max(w / size.x, h / size.y) * overscan;
        transform.localScale = new Vector3(s, s, 1f);

        Vector3 camPos = cam.transform.position;
        float maxShift = (s * size.x - w) * 0.5f;
        float maxShiftY = (s * size.y - h) * 0.5f;
        transform.localPosition = new Vector3(
            Mathf.Clamp(-camPos.x * parallax, -maxShift, maxShift),
            Mathf.Clamp(-camPos.y * parallax, -maxShiftY, maxShiftY),
            20f);
    }
}
