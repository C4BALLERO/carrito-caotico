using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Gira la cabeza (sin pasar de un ángulo) para mirar un objetivo, con suavizado: el Agarrador
    /// sigue con la vista su mano extensible. Parte cada fotograma de la pose original del hueso.
    /// </summary>
    [DefaultExecutionOrder(15)]
    public class HeadLook : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Transform target;
        [Tooltip("Personaje: define el frente y la vertical de la mirada.")]
        [SerializeField] Transform body;
        [SerializeField, Range(0f, 90f)] float maxYaw = 55f;
        [SerializeField, Range(0f, 60f)] float maxPitch = 30f;
        [SerializeField, Min(0.1f)] float speed = 7f;

        Quaternion rest;
        float yaw;
        float pitch;

        public float Yaw => yaw;
        public float Pitch => pitch;

        void Awake()
        {
            if (body == null) body = transform;
            if (head != null) rest = head.localRotation;
        }

        void LateUpdate()
        {
            if (head == null || target == null) return;
            head.localRotation = rest;
            Vector3 local = body.InverseTransformDirection(target.position - head.position);
            float desiredYaw = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(0.05f, local.z)) * Mathf.Rad2Deg, -maxYaw, maxYaw);
            float desiredPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -maxPitch, maxPitch);
            float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
            yaw = Mathf.Lerp(yaw, desiredYaw, t);
            pitch = Mathf.Lerp(pitch, desiredPitch, t);
            head.rotation = Quaternion.AngleAxis(yaw, body.up) * Quaternion.AngleAxis(pitch, body.right) * head.rotation;
        }

        /// <summary>Configuración usada por el generador de escena.</summary>
        public void Configure(Transform headBone, Transform lookTarget, Transform character)
        {
            head = headBone;
            target = lookTarget;
            body = character;
        }
    }
}
