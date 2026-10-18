using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Animación de un comprador con malla y clips (personajes KayKit). El Animator reproduce los
    /// clips (reposo, caminar y recoger) y este componente decide cuál según la IA
    /// (<see cref="IShopperBrain"/>) y el movimiento real; después corrige encima de la pose:
    /// manos en el asa, la mano que coge el producto y la cabeza que lo mira.
    ///
    /// - Caminar va al ritmo de la velocidad medida: <see cref="walkClipSpeed"/> es la velocidad
    ///   que el clip pisa a ritmo 1 (la mide el generador en los propios pies), así que el pie
    ///   apoyado no patina.
    /// - Además, cada pie que el clip apoya se bloquea en el suelo con IK de pierna mientras dure el
    ///   apoyo: corrige el pequeño deslizamiento que queda en el propio clip.
    /// - Recoger no corre solo: su tiempo sigue al progreso del gesto, de modo que la mano llega
    ///   al producto justo cuando la IA lo coge (<see cref="grabContact"/>).
    /// - Fuera de cámara el Animator no calcula huesos (CullUpdateTransforms) y aquí no hay IK.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class ClipShopperAnimator : MonoBehaviour
    {
        public const string IdleState = "Quieto";
        public const string WalkState = "Caminar";
        public const string GrabState = "Agarrar";
        public const string PaceParameter = "Ritmo";

        [SerializeField] Animator animator;
        [Header("Huesos")]
        [SerializeField] Transform leftUpperArm;
        [SerializeField] Transform leftLowerArm;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightUpperArm;
        [SerializeField] Transform rightLowerArm;
        [SerializeField] Transform rightHand;
        [SerializeField] Transform head;
        [SerializeField] Transform leftUpperLeg;
        [SerializeField] Transform leftLowerLeg;
        [SerializeField] Transform leftFoot;
        [SerializeField] Transform rightUpperLeg;
        [SerializeField] Transform rightLowerLeg;
        [SerializeField] Transform rightFoot;
        [Tooltip("Altura (m) sobre la del reposo a la que el pie del clip deja de contar como apoyado.")]
        [SerializeField, Min(0.005f)] float contactHeight = 0.06f;
        [Tooltip("Ritmo máximo del clip de caminar: los rivales van deprisa y mueven los pies rápido, como en los dibujos.")]
        [SerializeField, Min(1f)] float maxPace = 6f;
        [Tooltip("Puntos del asa donde van las manos.")]
        [SerializeField] Transform leftGrip;
        [SerializeField] Transform rightGrip;
        [Header("Clips")]
        [Tooltip("Velocidad (m/s) que pisa el clip de caminar a ritmo 1, medida en sus pies.")]
        [SerializeField, Min(0.1f)] float walkClipSpeed = 1f;
        [Tooltip("Por debajo de esta velocidad (m/s) se queda en reposo.")]
        [SerializeField, Min(0f)] float walkThreshold = 0.08f;
        [Tooltip("Tiempo normalizado del clip de recoger en el que la mano está más baja.")]
        [SerializeField, Range(0.05f, 0.95f)] float grabContact = 0.45f;
        [SerializeField, Range(0f, 90f)] float maxHeadTurn = 60f;

        static readonly int Idle = Animator.StringToHash(IdleState);
        static readonly int Walk = Animator.StringToHash(WalkState);
        static readonly int Grab = Animator.StringToHash(GrabState);
        static readonly int Pace = Animator.StringToHash(PaceParameter);

        IShopperBrain brain;
        Renderer visibility;
        Vector3 lastPosition;
        float speed;
        float gripWeight = 1f;
        float reachWeight;
        float headWeight;
        int currentState;
        bool rightSide;
        readonly FootLock[] feet = { new FootLock(), new FootLock() };

        /// <summary>Bloqueo de un pie: dónde quedó apoyado y cuánto manda el bloqueo sobre el clip.</summary>
        sealed class FootLock
        {
            public Transform Upper, Lower, Foot;
            public float RestHeight;
            public Vector3 Planted;
            public bool Locked;
            public float Weight;
        }

        public float Speed => speed;
        public float GripWeight => gripWeight;
        public float WalkClipSpeed => walkClipSpeed;
        public Animator Animator => animator;
        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;
        public Transform LeftGrip => leftGrip;
        public Transform RightGrip => rightGrip;
        bool alwaysAnimate;

        /// <summary>Solo para pruebas: anima (clips e IK) aunque ninguna cámara lo vea.</summary>
        public bool AlwaysAnimate
        {
            get => alwaysAnimate;
            set
            {
                alwaysAnimate = value;
                if (animator != null) animator.cullingMode = value ? AnimatorCullingMode.AlwaysAnimate : AnimatorCullingMode.CullUpdateTransforms;
            }
        }

        void Awake()
        {
            feet[0].Upper = leftUpperLeg; feet[0].Lower = leftLowerLeg; feet[0].Foot = leftFoot;
            feet[1].Upper = rightUpperLeg; feet[1].Lower = rightLowerLeg; feet[1].Foot = rightFoot;
            foreach (FootLock lockState in feet)
                if (lockState.Foot != null) lockState.RestHeight = lockState.Foot.position.y - transform.position.y;
            brain = GetComponent<IShopperBrain>();
            visibility = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        void OnEnable()
        {
            lastPosition = transform.position;
            currentState = 0;
        }

        /// <summary>Para pruebas: otra IA que mande sobre el comprador.</summary>
        public void SetBrain(IShopperBrain value) => brain = value;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || animator == null) return;
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            speed = Mathf.Lerp(speed, delta.magnitude / dt, 1f - Mathf.Exp(-12f * dt));

            bool reaching = brain != null && brain.Action == ShopperAction.Reaching;
            if (reaching)
            {
                float progress = Mathf.Clamp01(brain.ReachProgress);
                // La mano llega al producto (progreso 0,55, cuando la IA lo coge) en el punto más bajo del clip.
                float time = progress < 0.55f
                    ? progress / 0.55f * grabContact
                    : grabContact + (progress - 0.55f) / 0.45f * (1f - grabContact);
                if (currentState != Grab)
                {
                    currentState = Grab;
                    rightSide = Vector3.SignedAngle(transform.forward, Vector3.ProjectOnPlane(brain.ReachPoint - transform.position, Vector3.up), Vector3.up) >= 0f;
                    animator.CrossFade(Grab, 0.15f, 0, time);
                }
                else if (!animator.IsInTransition(0))
                {
                    animator.Play(Grab, 0, time);
                }
            }
            else
            {
                Play(speed > walkThreshold ? Walk : Idle, 0.2f);
            }
            animator.SetFloat(Pace, Mathf.Clamp(speed / walkClipSpeed, 0.3f, maxPace));

            gripWeight = Mathf.MoveTowards(gripWeight, reaching ? 0f : 1f, dt * 5f);
            float envelope = reaching ? Plateau(brain.ReachProgress) : 0f;
            reachWeight = Mathf.MoveTowards(reachWeight, envelope, dt * 6f);
            bool looking = brain != null && (reaching || brain.Action == ShopperAction.Browsing);
            headWeight = Mathf.MoveTowards(headWeight, looking ? 1f : 0f, dt * 3f);
        }

        void Play(int state, float fade)
        {
            if (currentState == state) return;
            currentState = state;
            animator.CrossFadeInFixedTime(state, fade);
        }

        void LateUpdate()
        {
            if (animator == null) return;
            if (!AlwaysAnimate && visibility != null && !visibility.isVisible) return;
            Vector3 up = transform.up;
            Vector3 right = transform.right;
            Vector3 back = -transform.forward;
            LockFeet(Time.deltaTime);

            // Manos en el asa, sobre la pose del clip (codos hacia fuera y abajo).
            if (gripWeight > 0f)
            {
                if (leftGrip != null) TwoBoneIK.Solve(leftUpperArm, leftLowerArm, leftHand, leftGrip.position, back * 0.3f - up * 0.6f - right * 0.6f, gripWeight);
                if (rightGrip != null) TwoBoneIK.Solve(rightUpperArm, rightLowerArm, rightHand, rightGrip.position, back * 0.3f - up * 0.6f + right * 0.6f, gripWeight);
            }

            if (brain == null) return;
            Vector3 point = brain.ReachPoint;
            // La mano del lado del producto termina el gesto del clip sobre el producto.
            if (reachWeight > 0f)
            {
                if (rightSide) TwoBoneIK.Solve(rightUpperArm, rightLowerArm, rightHand, point, back * 0.4f - up * 0.6f + right * 0.5f, reachWeight);
                else TwoBoneIK.Solve(leftUpperArm, leftLowerArm, leftHand, point, back * 0.4f - up * 0.6f - right * 0.5f, reachWeight);
            }

            // La cabeza mira el producto (giro limitado), sin depender de los ejes del hueso.
            if (head != null && headWeight > 0f)
            {
                Vector3 toPoint = point - head.position;
                if (toPoint.sqrMagnitude > 1e-4f)
                {
                    Quaternion turn = Quaternion.FromToRotation(transform.forward, toPoint);
                    float angle = Quaternion.Angle(Quaternion.identity, turn);
                    if (angle > maxHeadTurn) turn = Quaternion.Slerp(Quaternion.identity, turn, maxHeadTurn / angle);
                    head.rotation = Quaternion.Slerp(Quaternion.identity, turn, headWeight) * head.rotation;
                }
            }
        }

        /// <summary>
        /// Pie apoyado según el clip (a la altura del reposo): se guarda dónde tocó el suelo y la
        /// pierna se resuelve con IK para que se quede ahí hasta que el clip lo levanta. El pie
        /// conserva la orientación del clip.
        /// </summary>
        void LockFeet(float dt)
        {
            float ground = transform.position.y;
            foreach (FootLock leg in feet)
            {
                if (leg.Upper == null || leg.Lower == null || leg.Foot == null) continue;
                Vector3 animated = leg.Foot.position;
                bool contact = animated.y - ground - leg.RestHeight < contactHeight;
                if (contact && !leg.Locked)
                {
                    leg.Locked = true;
                    leg.Planted = animated;
                }
                // Si el cuerpo se ha alejado mucho del pie (giro brusco, teletransporte), se suelta.
                if (!contact || Vector3.Distance(leg.Planted, animated) > 0.3f) leg.Locked = false;
                leg.Weight = Mathf.MoveTowards(leg.Weight, leg.Locked ? 1f : 0f, dt * (leg.Locked ? 40f : 12f));
                if (!leg.Locked) leg.Planted = Vector3.Lerp(animated, leg.Planted, leg.Weight);
                if (leg.Weight <= 0f) continue;

                Vector3 target = Vector3.Lerp(animated, new Vector3(leg.Planted.x, animated.y, leg.Planted.z), leg.Weight);
                Quaternion footRotation = leg.Foot.rotation;
                TwoBoneIK.Solve(leg.Upper, leg.Lower, leg.Foot, target, transform.forward);
                leg.Foot.rotation = footRotation;
            }
        }

        /// <summary>Peso de la mano sobre el producto: sube al ir, se mantiene al cogerlo y baja al guardarlo.</summary>
        static float Plateau(float progress)
        {
            if (progress < 0.35f) return Smooth(progress / 0.35f);
            if (progress < 0.6f) return 1f;
            return 1f - Smooth((progress - 0.6f) / 0.22f);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Configuración usada por el generador de compradores.</summary>
        public void Configure(Animator clipAnimator, Transform[] arms, Transform[] legs, Transform headBone, Transform leftHandle, Transform rightHandle, float measuredWalkSpeed, float measuredGrabContact)
        {
            leftUpperLeg = legs[0]; leftLowerLeg = legs[1]; leftFoot = legs[2];
            rightUpperLeg = legs[3]; rightLowerLeg = legs[4]; rightFoot = legs[5];
            animator = clipAnimator;
            leftUpperArm = arms[0]; leftLowerArm = arms[1]; leftHand = arms[2];
            rightUpperArm = arms[3]; rightLowerArm = arms[4]; rightHand = arms[5];
            head = headBone;
            leftGrip = leftHandle;
            rightGrip = rightHandle;
            walkClipSpeed = Mathf.Max(0.1f, measuredWalkSpeed);
            grabContact = Mathf.Clamp(measuredGrabContact, 0.05f, 0.95f);
        }
    }
}
