using System;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Zona de entrega junto a la caja registradora. Cuando el carrito entra con productos,
    /// pide al GameManager que evalúe la compra. Solo se evalúa una vez por partida.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Checkout : MonoBehaviour
    {
        [SerializeField] GameManager gameManager;
        [Tooltip("Segundos entre avisos de canasta vacía.")]
        [SerializeField] float emptyHintCooldown = 2.5f;

        bool consumed;
        float nextHintTime;

        public bool Consumed => consumed;

        /// <summary>El carrito llegó a la caja sin productos.</summary>
        public event Action EmptyCartRejected;

        /// <summary>Cliente LAN: el anfitrión avisa de que el carrito llegó vacío.</summary>
        public void RaiseRemoteEmptyCart() => EmptyCartRejected?.Invoke();

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerStay(Collider other)
        {
            if (consumed || gameManager == null) return;

            Rigidbody rb = other.attachedRigidbody;
            if (rb == null || rb.GetComponent<CartController>() == null) return;

            switch (gameManager.TryCheckout())
            {
                case CheckoutAttempt.Accepted:
                    consumed = true;
                    break;
                case CheckoutAttempt.EmptyCart:
                    if (Time.time >= nextHintTime)
                    {
                        nextHintTime = Time.time + emptyHintCooldown;
                        EmptyCartRejected?.Invoke();
                    }
                    break;
            }
        }
    }
}
