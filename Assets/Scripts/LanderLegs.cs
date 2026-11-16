using UnityEngine;

public class LanderLegs : MonoBehaviour
{
    public bool isGrounded { get; private set; }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.TryGetComponent(out Pad pad))
        {
            isGrounded = true;
        }
    }
    private void OnCollisionExit2D(Collision2D collision)
    {
        if(collision.gameObject.TryGetComponent(out Pad pad))
        {
            isGrounded = false;
        }
    }
}
