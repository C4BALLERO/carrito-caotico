using System;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Vida del carrito. Los choques fuertes contra estantes, paredes, rivales u obstáculos le quitan
    /// vida según la velocidad del impacto; los productos y el suelo no hacen daño. Con la vida a
    /// cero el carrito explota: sus piezas salen volando y la partida termina.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CartHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] float maxHealth = 100f;
        [Tooltip("Velocidad de impacto (m/s) por debajo de la cual el choque no hace daño.")]
        [SerializeField, Min(0f)] float safeImpactSpeed = 2.5f;
        [Tooltip("Daño por cada m/s por encima de la velocidad segura.")]
        [SerializeField, Min(0f)] float damagePerSpeed = 9f;
        [SerializeField, Min(1f)] float maxDamagePerHit = 30f;
        [Tooltip("Segundos entre dos golpes que cuentan (un choque genera varios contactos).")]
        [SerializeField, Min(0f)] float hitCooldown = 0.35f;
        [SerializeField] float explosionForce = 9f;
        [SerializeField] float explosionRadius = 5f;

        float health;
        float nextHitTime;
        Rigidbody body;

        public float MaxHealth => maxHealth;
        public float Health => health;
        public float Fraction => maxHealth > 0f ? health / maxHealth : 0f;
        public bool IsDestroyed { get; private set; }
        /// <summary>Solo se recibe daño durante la partida (no en el tutorial ni en el cliente LAN).</summary>
        public bool DamageEnabled { get; private set; }

        /// <summary>Se dispara con el daño recibido (o con la bajada de vida que llega del anfitrión).</summary>
        public event Action<float> Damaged;
        public event Action Exploded;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            health = maxHealth;
        }

        public void SetDamageEnabled(bool enabled) => DamageEnabled = enabled && !IsDestroyed;

        /// <summary>Daño de un choque a cierta velocidad (lógica pura, también para pruebas).</summary>
        public static float DamageForImpact(float impactSpeed, float safeSpeed, float perSpeed, float maxPerHit)
        {
            if (impactSpeed <= safeSpeed) return 0f;
            return Mathf.Min(maxPerHit, (impactSpeed - safeSpeed) * perSpeed);
        }

        public float DamageForImpact(float impactSpeed) => DamageForImpact(impactSpeed, safeImpactSpeed, damagePerSpeed, maxDamagePerHit);

        void OnCollisionEnter(Collision collision)
        {
            if (!DamageEnabled || IsDestroyed || Time.time < nextHitTime) return;
            Rigidbody other = collision.rigidbody;
            // Los productos (dentro o fuera de la canasta) no rompen el carrito.
            if (other != null && other.GetComponent<Product>() != null) return;
            if (collision.contactCount == 0) return;

            Vector3 normal = collision.GetContact(0).normal;
            // El suelo no cuenta (normal casi vertical).
            if (Mathf.Abs(normal.y) > 0.6f) return;

            float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal));
            float damage = DamageForImpact(impact);
            if (damage <= 0f) return;
            nextHitTime = Time.time + hitCooldown;
            ApplyDamage(damage);
        }

        /// <summary>Quita vida. Al llegar a cero, el carrito explota.</summary>
        public void ApplyDamage(float amount)
        {
            if (IsDestroyed || amount <= 0f) return;
            health = Mathf.Max(0f, health - amount);
            Damaged?.Invoke(amount);
            if (health <= 0f) Explode();
        }

        /// <summary>Cliente LAN: la vida la decide el anfitrión.</summary>
        public void SetRemoteHealth(float value)
        {
            if (IsDestroyed) return;
            float clamped = Mathf.Clamp(value, 0f, maxHealth);
            float lost = health - clamped;
            health = clamped;
            if (lost > 0.5f) Damaged?.Invoke(lost);
        }

        public void Explode()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;
            DamageEnabled = false;
            health = 0f;

            // Suelta lo que sostiene la mano y para el brazo antes de romper el carrito.
            if (TryGetComponent(out GrabberArm grabber))
            {
                if (grabber.HeldProduct != null) grabber.DropHeld();
                grabber.SetInputEnabled(false);
                grabber.enabled = false;
            }

            Vector3 center = transform.position + Vector3.up * 0.7f;
            ExplosionEffect.Spawn(center, 1.2f);
            BreakApart(center);

            // Empuja lo que haya cerca (productos, piezas). En el cliente LAN todo es cinemático.
            foreach (Collider hit in Physics.OverlapSphere(center, explosionRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                Rigidbody rb = hit.attachedRigidbody;
                if (rb != null && !rb.isKinematic && rb != body) rb.AddExplosionForce(explosionForce, center, explosionRadius, 0.6f, ForceMode.VelocityChange);
            }
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            Exploded?.Invoke();
        }

        /// <summary>Las piezas visibles del carrito se sueltan y salen despedidas.</summary>
        void BreakApart(Vector3 center)
        {
            var random = new System.Random(UnityEngine.Random.Range(1, int.MaxValue));
            foreach (Renderer piece in GetComponentsInChildren<Renderer>())
            {
                Transform t = piece.transform;
                // Los productos (por ejemplo, uno sostenido) no son piezas del carrito.
                if (t == transform || piece.GetComponentInParent<Product>() != null) continue;
                Vector3 scale = t.lossyScale;
                t.SetParent(null, true);
                piece.gameObject.name = "Pieza_" + piece.gameObject.name;
                if (piece.GetComponent<Collider>() == null)
                {
                    var box = piece.gameObject.AddComponent<BoxCollider>();
                    box.size = Vector3.one;
                }
                Rigidbody rb = piece.gameObject.GetComponent<Rigidbody>();
                if (rb == null) rb = piece.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.mass = Mathf.Clamp(scale.x * scale.y * scale.z * 40f, 0.2f, 6f);
                Vector3 away = (t.position - center).normalized + Vector3.up * 0.8f;
                rb.linearVelocity = away.normalized * (4f + (float)random.NextDouble() * 5f);
                rb.angularVelocity = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * 12f;
                Destroy(piece.gameObject, 8f);
            }
        }
    }
}
