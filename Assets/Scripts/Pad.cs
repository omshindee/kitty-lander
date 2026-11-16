using UnityEngine;
using System.Collections;

public class Pad : MonoBehaviour
{
    public static Pad Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Hide();
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);

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

            float scale = Mathf.Lerp(0f, 1f, time / duration);

            transform.localScale = Vector3.one * scale;

            yield return null;
        }

        transform.localScale = Vector3.one;
    }
}