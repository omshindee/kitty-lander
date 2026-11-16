using UnityEngine;
using System;
using System.Collections;

public class Pad : MonoBehaviour
{
    public static Pad Instance { get; private set; }

    public static event EventHandler OnPadShown;

    public bool IsShown { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (!IsShown) Hide();
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        if (IsShown) return;
        IsShown = true;
        gameObject.SetActive(true);
        OnPadShown?.Invoke(this, EventArgs.Empty);
        StartCoroutine(SpawnAnimation());
    }

    private IEnumerator SpawnAnimation()
    {
        transform.localScale = Vector3.zero;

        float time = 0f;
        float duration = 1f;

        while (time < duration)
        {
            time += Time.deltaTime;
            // little overshoot "pop"
            float t = Mathf.Clamp01(time / duration);
            float scale = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }

        transform.localScale = Vector3.one;
    }
}
