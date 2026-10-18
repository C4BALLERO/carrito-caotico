using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarritoCaotico
{
    public enum CameraView
    {
        TerceraPersona = 0,
        PrimeraPersona = 1
    }

    /// <summary>
    /// Cámara en tercera persona detrás del carrito. El Empujador la rota con el mouse
    /// (o el stick derecho). Sigue la orientación del carrito de forma suavizada y
    /// se acerca si un estante o una pared se interpone.
    /// En LAN, el PC del Empujador puede cambiar a primera persona (ojos del Empujador, detrás del
    /// asa). El Agarrador siempre juega en tercera persona.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        const string ViewKey = "CarritoCaotico.VistaEmpujador";

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

        [Header("Primera persona (Empujador en LAN)")]
        [Tooltip("Posición de los ojos en el espacio local del carrito.")]
        [SerializeField] Vector3 eyeOffset = new Vector3(-0.3f, 1.74f, -1.36f);
        [SerializeField] float firstPersonPitch = 12f;
        [SerializeField] Vector2 firstPersonPitchLimits = new Vector2(-25f, 55f);
        [Tooltip("Cuánto puede girar la cabeza a cada lado, en grados.")]
        [SerializeField] float firstPersonYawLimit = 100f;
        [SerializeField] float firstPersonNearClip = 0.05f;
        [Tooltip("Cuerpo del Empujador: en primera persona solo proyecta sombra.")]
        [SerializeField] Renderer[] hideInFirstPerson = Array.Empty<Renderer>();

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
        float headYaw;
        float headPitch;
        Camera cam;
        float thirdPersonNearClip = -1f;

        public Transform Target => target;
        /// <summary>Giro de la cámara respecto al carrito, en grados (-180..180).</summary>
        public float YawOffset => yawOffset;
        public CameraView View { get; private set; } = CameraView.TerceraPersona;
        /// <summary>Solo el Empujador en LAN puede usar la primera persona.</summary>
        public bool FirstPersonAllowed { get; private set; }
        public Vector3 EyeOffset => eyeOffset;

        public event Action<CameraView> ViewChanged;

        /// <summary>Vista elegida por el Empujador la última vez (se guarda entre partidas).</summary>
        public static CameraView PreferredView
        {
            get => PlayerPrefs.GetInt(ViewKey, 0) == 1 ? CameraView.PrimeraPersona : CameraView.TerceraPersona;
            set
            {
                PlayerPrefs.SetInt(ViewKey, value == CameraView.PrimeraPersona ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null) thirdPersonNearClip = cam.nearClipPlane;
        }

        void Start()
        {
            pitch = defaultPitch;
            Snap();
        }

        /// <summary>Permite o prohíbe la primera persona. Al permitirla se recupera la vista preferida.</summary>
        public void SetFirstPersonAllowed(bool allowed)
        {
            FirstPersonAllowed = allowed;
            SetView(allowed ? PreferredView : CameraView.TerceraPersona, false);
        }

        /// <summary>Cambia de vista. La primera persona solo se aplica si está permitida.</summary>
        public void SetView(CameraView view) => SetView(view, true);

        void SetView(CameraView view, bool remember)
        {
            if (view == CameraView.PrimeraPersona && !FirstPersonAllowed) view = CameraView.TerceraPersona;
            if (remember && FirstPersonAllowed) PreferredView = view;
            if (View == view) return;

            View = view;
            headYaw = 0f;
            headPitch = firstPersonPitch;
            bool firstPerson = view == CameraView.PrimeraPersona;
            foreach (Renderer r in PusherRenderers())
                if (r != null) r.shadowCastingMode = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            if (cam != null && thirdPersonNearClip > 0f) cam.nearClipPlane = firstPerson ? firstPersonNearClip : thirdPersonNearClip;
            if (firstPerson) ApplyFirstPerson();
            else Snap();
            ViewChanged?.Invoke(view);
        }

        Renderer[] PusherRenderers()
        {
            if ((hideInFirstPerson == null || hideInFirstPerson.Length == 0) && target != null)
            {
                // Escenas antiguas: el cuerpo del Empujador se reconoce por el nombre.
                var found = new System.Collections.Generic.List<Renderer>();
                foreach (Renderer r in target.GetComponentsInChildren<Renderer>(true))
                    if (r.name.StartsWith("Empujador_")) found.Add(r);
                hideInFirstPerson = found.ToArray();
            }
            return hideInFirstPerson ?? Array.Empty<Renderer>();
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

            if (lookEnabled && FirstPersonAllowed && GameInput.ViewTogglePressed())
                SetView(View == CameraView.PrimeraPersona ? CameraView.TerceraPersona : CameraView.PrimeraPersona);

            Vector2 look = Vector2.zero;
            if (lookEnabled)
            {
                look = GameInput.MouseLookDelta() * mouseSensitivity + GameInput.PadLook() * (gamepadSensitivity * dt);
            }

            if (look.sqrMagnitude > 0.0001f) idleTime = 0f;
            else idleTime += dt;

            if (View == CameraView.PrimeraPersona)
            {
                headYaw = Mathf.Clamp(headYaw + look.x, -firstPersonYawLimit, firstPersonYawLimit);
                headPitch = Mathf.Clamp(headPitch - look.y, firstPersonPitchLimits.x, firstPersonPitchLimits.y);
                if (recenterSpeed > 0f && idleTime > recenterDelay)
                    headYaw = Mathf.MoveTowards(headYaw, 0f, recenterSpeed * dt);
                ApplyFirstPerson();
                return;
            }

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

        /// <summary>Ojos del Empujador: la cámara va pegada al carrito, sin suavizado ni anticolisión.</summary>
        void ApplyFirstPerson()
        {
            if (target == null) return;
            Vector3 eye = target.TransformPoint(eyeOffset);
            Quaternion rotation = Quaternion.Euler(headPitch, target.eulerAngles.y + headYaw, 0f);
            transform.SetPositionAndRotation(eye, rotation);
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
                // Solo los objetos estáticos (estantes, paredes) acercan la cámara: se ignoran
                // el carrito, los productos, las cajas y los compradores rivales.
                if (rb != null) continue;
                if (hit.distance < best) best = hit.distance;
            }
            return Mathf.Clamp(best, minDistance, maxDistance);
        }
    }
}
