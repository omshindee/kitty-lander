using UnityEngine;
using System;

public class Fish : MonoBehaviour
{
    public static event EventHandler OnFishCollected;

    private bool collected;

    public void DestroySelf()
    {
        // The lander has several colliders, so guard against double collection.
        if (collected) return;
        collected = true;
        Destroy(gameObject);
        OnFishCollected?.Invoke(this, EventArgs.Empty);
    }
}
