using UnityEngine;

/// <summary>
/// Smoothly follows the lander while keeping the view inside the level bounds.
/// If the level is smaller than the view, the camera stays centred on it.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private float smoothTime = 0.25f;
    [SerializeField] private Vector2 lookOffset = new Vector2(0f, 0.5f);

    private Camera cam;
    private Vector3 velocity;
    private Transform target;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (Lander.Instance != null) target = Lander.Instance.transform;
        if (target != null) transform.position = Clamp(Desired());
    }

    private Vector3 Desired()
    {
        Vector3 p = target.position + (Vector3)lookOffset;
        p.z = transform.position.z;
        return p;
    }

    private Vector3 Clamp(Vector3 p)
    {
        if (GameManager.Instance == null) return p;
        Rect b = GameManager.Instance.LevelBounds;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        p.x = b.width <= halfW * 2f ? b.center.x : Mathf.Clamp(p.x, b.xMin + halfW, b.xMax - halfW);
        p.y = b.height <= halfH * 2f ? b.center.y : Mathf.Clamp(p.y, b.yMin + halfH, b.yMax - halfH);
        return p;
    }

    private void LateUpdate()
    {
        if (target == null || !target.gameObject.activeInHierarchy) return;
        transform.position = Vector3.SmoothDamp(transform.position, Clamp(Desired()), ref velocity, smoothTime);
    }
}
