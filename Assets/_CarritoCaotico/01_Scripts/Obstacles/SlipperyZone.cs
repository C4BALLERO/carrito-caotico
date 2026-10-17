using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Charco resbaladizo: mientras el carrito está dentro, pierde casi todo el agarre lateral
    /// y derrapa aunque no se pulse el derrape.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SlipperyZone : MonoBehaviour
    {
        [Tooltip("Agarre lateral dentro del charco (1 = normal, 0 = hielo).")]
        [SerializeField, Range(0f, 1f)] float grip = 0.15f;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerStay(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null) return;
            CartController cart = rb.GetComponent<CartController>();
            if (cart != null) cart.ApplySurfaceGrip(grip);
        }
    }
}
