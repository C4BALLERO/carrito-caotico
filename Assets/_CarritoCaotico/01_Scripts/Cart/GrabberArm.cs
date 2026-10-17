using System;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Brazo extensible del Agarrador. La mano se mueve en el espacio local del carrito,
    /// dentro de un área limitada, y puede agarrar, soltar o lanzar un único producto.
    /// </summary>
    public class GrabberArm : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Raíz del carrito: la mano se mueve en su espacio local.")]
        [SerializeField] Transform cartRoot;
        [SerializeField] Rigidbody cartBody;
        [SerializeField] Transform shoulder;
        [Tooltip("Transform de la mano (sin escala). Debe ser hijo de la raíz del carrito.")]
        [SerializeField] Transform hand;
        [SerializeField] Transform grabPoint;
        [Tooltip("Cilindro visual que une el hombro con la mano.")]
        [SerializeField] Transform armVisual;
        [SerializeField] Renderer handRenderer;
        [Tooltip("Marcador en el suelo bajo la mano para leer la profundidad.")]
        [SerializeField] Transform handShadow;
        [Tooltip("Punto sobre la canasta donde caen los productos al soltarlos cerca del carrito.")]
        [SerializeField] Transform basketDropPoint;

        [Header("Movimiento de la mano")]
        [SerializeField] float handSpeed = 3.2f;
        [SerializeField, Min(0.01f)] float handSmoothTime = 0.07f;
        [Tooltip("Límites laterales (x) en el espacio local del carrito.")]
        [SerializeField] Vector2 lateralLimits = new Vector2(-2.3f, 2.3f);
        [Tooltip("Límites adelante / atrás (z) en el espacio local del carrito.")]
        [SerializeField] Vector2 forwardLimits = new Vector2(-0.4f, 2.0f);
        [SerializeField] float handHeight = 1.05f;
        [SerializeField] Vector3 startLocalPosition = new Vector3(0f, 1.05f, 0.2f);
        [SerializeField] float armThickness = 0.07f;

        [Header("Agarre")]
        [SerializeField] float grabRadius = 0.6f;
        [SerializeField] LayerMask grabMask = ~0;
        [Tooltip("Zona local alrededor del carrito: si la mano está dentro, el producto cae en la canasta.")]
        [SerializeField] Bounds basketDropArea = new Bounds(new Vector3(0f, 0.8f, 0.15f), new Vector3(1.4f, 1.8f, 2.0f));

        [Header("Lanzamiento")]
        [Tooltip("Velocidad añadida al lanzar (m/s), independiente de la masa.")]
        [SerializeField] float throwSpeed = 6.5f;
        [SerializeField, Range(0f, 1.5f)] float throwUpward = 0.5f;
        [SerializeField] float throwSpin = 4f;
        [Tooltip("Altura extra sobre el punto de la canasta al lanzar desde dentro del carrito.")]
        [SerializeField] float throwBasketLift = 0.3f;

        [Header("Colores de la mano")]
        [SerializeField] Color idleColor = new Color(1f, 0.24f, 0.5f);
        [SerializeField] Color canGrabColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] Color holdingColor = new Color(0.2f, 0.85f, 0.45f);

        readonly Collider[] overlap = new Collider[48];
        Vector3 targetLocal;
        Vector3 currentLocal;
        Vector3 handVelocity;
        Product held;
        Product candidate;
        bool inputEnabled = true;
        MaterialPropertyBlock propertyBlock;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public Product HeldProduct => held;
        public Product Candidate => candidate;
        public Vector3 HandPosition => hand != null ? hand.position : transform.position;
        /// <summary>Posición de la mano en el espacio local del carrito.</summary>
        public Vector3 HandLocalPosition => currentLocal;
        public bool InputEnabled => inputEnabled;

        /// <summary>Se dispara cuando la mano agarra o deja de sostener un producto.</summary>
        public event Action<Product> HeldProductChanged;
        /// <summary>Se dispara al lanzar un producto.</summary>
        public event Action<Product> ProductThrown;

        void Awake()
        {
            if (cartRoot == null) cartRoot = transform;
            if (cartBody == null) cartBody = cartRoot.GetComponent<Rigidbody>();
            targetLocal = currentLocal = ClampLocal(startLocalPosition);
            if (hand != null) hand.localPosition = currentLocal;
        }

        void Update()
        {
            if (hand == null) return;
            float dt = Time.deltaTime;

            if (inputEnabled)
            {
                Vector2 move = GameInput.GrabberMove();
                targetLocal.x += move.x * handSpeed * dt;
                targetLocal.z += move.y * handSpeed * dt;
                targetLocal = ClampLocal(targetLocal);
            }

            currentLocal = Vector3.SmoothDamp(currentLocal, targetLocal, ref handVelocity, handSmoothTime);
            hand.localPosition = currentLocal;

            candidate = held == null ? FindNearestProduct() : null;

            if (inputEnabled)
            {
                if (GameInput.GrabberGrabPressed())
                {
                    if (held != null) DropHeld();
                    else TryGrabNearest();
                }
                else if (GameInput.GrabberThrowPressed() && held != null)
                {
                    ThrowHeld();
                }
            }

            UpdateHandColor();
        }

        void LateUpdate()
        {
            UpdateArmVisual();
            UpdateHandShadow();
        }

        Vector3 ClampLocal(Vector3 local)
        {
            local.x = Mathf.Clamp(local.x, lateralLimits.x, lateralLimits.y);
            local.z = Mathf.Clamp(local.z, forwardLimits.x, forwardLimits.y);
            local.y = handHeight;
            return local;
        }

        Product FindNearestProduct()
        {
            Vector3 center = hand.position;
            int count = Physics.OverlapSphereNonAlloc(center, grabRadius, overlap, grabMask, QueryTriggerInteraction.Ignore);

            Product best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider col = overlap[i];
                Product product = col.attachedRigidbody != null
                    ? col.attachedRigidbody.GetComponent<Product>()
                    : col.GetComponentInParent<Product>();
                if (product == null || product.IsHeld) continue;

                float distance = (col.ClosestPoint(center) - center).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = product;
                }
            }
            return best;
        }

        /// <summary>Agarra el producto más cercano a la mano, si lo hay.</summary>
        public bool TryGrabNearest()
        {
            if (held != null || hand == null) return false;
            Product product = FindNearestProduct();
            if (product == null || !product.TryAttach(grabPoint)) return false;

            held = product;
            candidate = null;
            HeldProductChanged?.Invoke(held);
            return true;
        }

        /// <summary>
        /// Suelta el producto. Cerca del carrito lo deja caer dentro de la canasta
        /// para que no atraviese las paredes.
        /// </summary>
        public void DropHeld()
        {
            if (held == null) return;

            Vector3 handLocal = cartRoot.InverseTransformPoint(hand.position);
            bool overBasket = basketDropPoint != null && basketDropArea.Contains(handLocal);
            Vector3 position = overBasket ? basketDropPoint.position : grabPoint.position;
            Vector3 velocity = cartBody != null ? cartBody.GetPointVelocity(position) : Vector3.zero;
            Release(position, velocity);
        }

        /// <summary>Lanza el producto en la dirección del brazo.</summary>
        public void ThrowHeld()
        {
            if (held == null) return;

            Vector3 direction = hand.position - shoulder.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.04f) direction = cartRoot.forward;
            direction = (direction.normalized + Vector3.up * throwUpward).normalized;

            Vector3 position = grabPoint.position;
            // Desde dentro de la canasta, el lanzamiento sale por encima de las paredes.
            if (basketDropPoint != null && basketDropArea.Contains(cartRoot.InverseTransformPoint(hand.position)))
                position = basketDropPoint.position + cartRoot.up * throwBasketLift;

            Vector3 baseVelocity = cartBody != null ? cartBody.GetPointVelocity(position) : Vector3.zero;
            Product product = held;
            Release(position, baseVelocity + direction * throwSpeed);
            product.Body.AddTorque(UnityEngine.Random.insideUnitSphere * throwSpin, ForceMode.VelocityChange);
            ProductThrown?.Invoke(product);
        }

        void Release(Vector3 position, Vector3 velocity)
        {
            Product product = held;
            held = null;
            product.Detach(position, velocity);
            HeldProductChanged?.Invoke(null);
        }

        public void SetInputEnabled(bool enabled) => inputEnabled = enabled;

        /// <summary>Mueve el objetivo de la mano (espacio local del carrito). Útil para pruebas.</summary>
        public void SetHandLocalTarget(Vector3 localPosition, bool snap)
        {
            targetLocal = ClampLocal(localPosition);
            if (!snap || hand == null) return;
            currentLocal = targetLocal;
            handVelocity = Vector3.zero;
            hand.localPosition = currentLocal;
        }

        void UpdateHandColor()
        {
            if (handRenderer == null) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

            Color color = held != null ? holdingColor : candidate != null ? canGrabColor : idleColor;
            handRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            handRenderer.SetPropertyBlock(propertyBlock);
        }

        void UpdateArmVisual()
        {
            if (armVisual == null || shoulder == null || hand == null) return;

            Vector3 from = shoulder.position;
            Vector3 to = hand.position;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.001f) return;

            armVisual.position = from + delta * 0.5f;
            armVisual.rotation = Quaternion.FromToRotation(Vector3.up, delta / length);
            // El cilindro primitivo mide 2 unidades de alto.
            armVisual.localScale = new Vector3(armThickness, length * 0.5f, armThickness);
        }

        void UpdateHandShadow()
        {
            if (handShadow == null || hand == null) return;

            if (Physics.Raycast(hand.position, Vector3.down, out RaycastHit hit, 3f, grabMask, QueryTriggerInteraction.Ignore))
            {
                if (!handShadow.gameObject.activeSelf) handShadow.gameObject.SetActive(true);
                handShadow.SetPositionAndRotation(hit.point + hit.normal * 0.01f, Quaternion.FromToRotation(Vector3.up, hit.normal));
            }
            else if (handShadow.gameObject.activeSelf)
            {
                handShadow.gameObject.SetActive(false);
            }
        }

        void OnDrawGizmosSelected()
        {
            Transform root = cartRoot != null ? cartRoot : transform;
            Gizmos.matrix = root.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Vector3 center = new Vector3((lateralLimits.x + lateralLimits.y) * 0.5f, handHeight, (forwardLimits.x + forwardLimits.y) * 0.5f);
            Vector3 size = new Vector3(lateralLimits.y - lateralLimits.x, 0.05f, forwardLimits.y - forwardLimits.x);
            Gizmos.DrawWireCube(center, size);
            Gizmos.color = new Color(0f, 1f, 0.4f, 0.5f);
            Gizmos.DrawWireCube(basketDropArea.center, basketDropArea.size);
        }
    }
}
