using UnityEngine;

/// <summary>Fuel pickup. Cat food gives a small refill, the milk bottle a big one.</summary>
public class CatFood : MonoBehaviour
{
    [SerializeField] private float fuelAmount = 8f;

    public float FuelAmount => fuelAmount;

    private bool collected;

    public void DestroySelf()
    {
        if (collected) return;
        collected = true;
        Destroy(gameObject);
    }
}
