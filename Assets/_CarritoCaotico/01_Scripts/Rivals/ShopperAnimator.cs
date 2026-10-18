using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Animación de un comprador rival (personajes KayKit): camina o espera según lo rápido que se
    /// mueve, agarra productos con la animación de recoger y, mientras empuja, apunta los brazos
    /// al asa de su carrito. Mide la velocidad por el movimiento del frame, así que también anima
    /// las réplicas de la red.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ShopperAnimator : MonoBehaviour
    {
        public const string IdleState = "Quieto";
        public const string WalkState = "Caminar";
        public const string GrabState = "Agarrar";
        public const string PaceParameter = "Ritmo";

        [SerializeField] Animator animator;
        [Tooltip("Objeto que se desplaza (la raíz del rival).")]
        [SerializeField] Transform mover;
        [SerializeField] Transform leftUpperArm;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightUpperArm;
        [SerializeField] Transform rightHand;
        [Tooltip("Puntos del asa donde van las manos.")]
        [SerializeField] Transform leftGrip;
        [SerializeField] Transform rightGrip;
        [Tooltip("Velocidad (m/s) a la que la animación de caminar va a ritmo normal.")]
        [SerializeField, Min(0.1f)] float walkClipSpeed = 1.1f;
        [SerializeField, Min(0f)] float walkThreshold = 0.12f;

        static readonly int Idle = Animator.StringToHash(IdleState);
        static readonly int Walk = Animator.StringToHash(WalkState);
        static readonly int Grab = Animator.StringToHash(GrabState);
        static readonly int Pace = Animator.StringToHash(PaceParameter);

        Vector3 lastPosition;
        float speed;
        float grabUntil;
        float gripWeight = 1f;
        int currentState;

        void OnEnable()
        {
            if (mover == null) mover = transform;
            lastPosition = mover.position;
            currentState = 0;
        }

        /// <summary>Reproduce la animación de recoger durante <paramref name="seconds"/> segundos.</summary>
        public void PlayGrab(float seconds)
        {
            grabUntil = Time.time + seconds;
            Play(Grab, 0.1f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 position = mover.position;
            if (dt > 0f)
            {
                Vector3 delta = position - lastPosition;
                delta.y = 0f;
                speed = Mathf.Lerp(speed, delta.magnitude / dt, 1f - Mathf.Exp(-10f * dt));
            }
            lastPosition = position;

            bool grabbing = Time.time < grabUntil;
            if (!grabbing) Play(speed > walkThreshold ? Walk : Idle, 0.2f);
            if (animator != null) animator.SetFloat(Pace, Mathf.Clamp(speed / walkClipSpeed, 0.5f, 2f));
            gripWeight = Mathf.MoveTowards(gripWeight, grabbing ? 0f : 1f, dt * 5f);
        }

        void Play(int state, float fade)
        {
            if (animator == null || currentState == state) return;
            currentState = state;
            animator.CrossFadeInFixedTime(state, fade);
        }

        void LateUpdate()
        {
            if (gripWeight <= 0f) return;
            Aim(leftUpperArm, leftHand, leftGrip);
            Aim(rightUpperArm, rightHand, rightGrip);
        }

        /// <summary>Gira el brazo (sobre la pose animada) para que la mano apunte al asa.</summary>
        void Aim(Transform upperArm, Transform hand, Transform grip)
        {
            if (upperArm == null || hand == null || grip == null) return;
            Vector3 current = hand.position - upperArm.position;
            Vector3 desired = grip.position - upperArm.position;
            if (current.sqrMagnitude < 1e-6f || desired.sqrMagnitude < 1e-6f) return;
            Quaternion aimed = Quaternion.FromToRotation(current, desired) * upperArm.rotation;
            upperArm.rotation = Quaternion.Slerp(upperArm.rotation, aimed, gripWeight);
        }
    }
}
