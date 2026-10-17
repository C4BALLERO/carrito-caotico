using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Cámara en tercera persona detrás del carrito. El Empujador la rota con el mouse
    /// (o el stick derecho). Sigue la orientación del carrito de forma suavizada y
    /// se acerca si un estante o una pared se interpone.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Objetivo")]
        [SerializeField] Transform target;
        [Tooltip("Rigidbody del carrito: sus colliders se ignoran en la anticolisión.")]
        [SerializeField] Rigidbody targetBody;

        [Header("Encuadre")]
        [SerializeField, Min(1f)] float distance = 6.5f;
        [Tooltip("Altura del punto de mira sobre el carrito.")]
        [SerializeField] float height = 2.0f;
        [SerializeField] float defaultPitch = 32f;
        [SerializeField] float minPitch = 5f;
        [SerializeField] float maxPitch = 60f;

        [Header("Sensibilidad")]
        [Tooltip("Grados por píxel de movimiento del mouse.")]
        [SerializeField] float mouseSensitivity = 0.12f;
        [Tooltip("Grados por segundo con el stick derecho.")]
        [SerializeField] float gamepadSensitivity = 140f;

        [Header("Suavizado")]
        [SerializeField, Min(0.01f)] float followSmoothTime = 0.1f;
        [SerializeField, Min(0.01f)] float rotationSmoothTime = 0.3f;
        [Tooltip("Segundos sin mover la cámara antes de recentrarla detrás del carrito.")]
        [SerializeField] float recenterDelay = 2.5f;
        [Tooltip("Velocidad de recentrado en grados por segundo (0 = desactivado).")]
        [SerializeField] float recenterSpeed = 45f;

        [Header("Colisión")]
        [SerializeField] LayerMask collisionMask = ~0;
        [SerializeField] float collisionRadius = 0.3f;
        [SerializeField] float minDistance = 1.5f;
        [SerializeField] float minHeightAboveGround = 0.5f;

        readonly RaycastHit[] hits = new RaycastHit[16];
        float yawOffset;
        float pitch;
        float currentYaw;
        float yawVelocity;
        float currentDistance;
        float idleTime;
        Vector3 pivot;
        Vector3 pivotVelocity;
        bool lookEnabled = true;

        public Transform Target => target;

        void Start()
        {
            pitch = defaultPitch;
            Snap();
        }

        /// <summary>Coloca la cámara en su posición ideal sin suavizado.</summary>
        public void Snap()
        {
            if (target == null) return;
            currentYaw = target.eulerAngles.y + yawOffset;
            pivot = target.position + Vector3.up * height;
            currentDistance = distance;
            ApplyTransform();
        }

        public void SetLookEnabled(bool enabled) => lookEnabled = enabled;

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            Vector2 look = Vector2.zero;
            if (lookEnabled)
            {
                look = GameInput.MouseLookDelta() * mouseSensitivity + GameInput.PadLook() * (gamepadSensitivity * dt);
            }

            if (look.sqrMagnitude > 0.0001f) idleTime = 0f;
            else idleTime += dt;

            yawOffset = Mathf.Repeat(yawOffset + look.x + 180f, 360f) - 180f;
            pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);

            if (recenterSpeed > 0f && idleTime > recenterDelay)
                yawOffset = Mathf.MoveTowardsAngle(yawOffset, 0f, recenterSpeed * dt);

            // Seguimiento suavizado de orientación y posición: evita saltos al girar el carrito.
            float desiredYaw = target.eulerAngles.y + yawOffset;
            currentYaw = Mathf.SmoothDampAngle(currentYaw, desiredYaw, ref yawVelocity, rotationSmoothTime);
            pivot = Vector3.SmoothDamp(pivot, target.position + Vector3.up * height, ref pivotVelocity, followSmoothTime);

            ApplyTransform();
        }

        void ApplyTransform()
        {
            Quaternion rotation = Quaternion.Euler(pitch, currentYaw, 0f);
            Vector3 back = rotation * Vector3.back;

            float allowed = ComputeAllowedDistance(pivot, back, distance);
            // Acercarse de inmediato al chocar, alejarse de forma progresiva.
            currentDistance = allowed < currentDistance
                ? allowed
                : Mathf.MoveTowards(currentDistance, allowed, 6f * Time.deltaTime);

            Vector3 position = pivot + back * currentDistance;
            position.y = Mathf.Max(position.y, minHeightAboveGround);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(pivot - position, Vector3.up));
        }

        float ComputeAllowedDistance(Vector3 origin, Vector3 direction, float maxDistance)
        {
            int count = Physics.SphereCastNonAlloc(origin, collisionRadius, direction, hits, maxDistance,
                collisionMask, QueryTriggerInteraction.Ignore);

            float best = maxDistance;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance <= 0f) continue;
                Rigidbody rb = hit.collider.attachedRigidbody;
                // Ignora el carrito y los objetos dinámicos (productos, cajas).
                if (rb != null && (rb == targetBody || !rb.isKinematic)) continue;
                if (hit.distance < best) best = hit.distance;
            }
            return Mathf.Clamp(best, minDistance, maxDistance);
        }
    }
}
