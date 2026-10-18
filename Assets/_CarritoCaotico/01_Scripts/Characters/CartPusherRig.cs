using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Empujador del carrito: camina de verdad detrás del asa. Los pies se quedan plantados en el
    /// suelo y dan pasos al ritmo de la velocidad del carrito (ya no patinan), las manos sujetan
    /// el asa con IK y la cabeza mira hacia la curva. Se aplica después del bamboleo
    /// (<see cref="CharacterWaddle"/>) y no toca la física del carrito.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public class CartPusherRig : MonoBehaviour
    {
        [SerializeField] Rigidbody cartBody;
        [Header("Piernas")]
        [SerializeField] Transform leftUpperLeg;
        [SerializeField] Transform leftLowerLeg;
        [SerializeField] Transform leftFoot;
        [SerializeField] Transform rightUpperLeg;
        [SerializeField] Transform rightLowerLeg;
        [SerializeField] Transform rightFoot;
        [Header("Brazos (manos en el asa)")]
        [SerializeField] Transform leftUpperArm;
        [SerializeField] Transform leftLowerArm;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightUpperArm;
        [SerializeField] Transform rightLowerArm;
        [SerializeField] Transform rightHand;
        [SerializeField] Transform leftGrip;
        [SerializeField] Transform rightGrip;
        [Tooltip("Columna: se inclina hacia delante si los brazos no llegan al asa.")]
        [SerializeField] Transform spine;
        [Header("Cabeza")]
        [SerializeField] Transform head;
        [SerializeField, Range(0f, 60f)] float maxHeadYaw = 35f;

        Transform[] bones;
        Quaternion[] restRotations;
        Quaternion leftFootRest;
        Quaternion rightFootRest;
        FootPlanter planter;
        float ankleHeight;
        float headYaw;
        float groundOffset;
        float armLength;
        float lean;

        public int Steps => planter != null ? planter.Steps : 0;
        public Vector3 FootPosition(int index) => planter != null ? planter.FootPosition(index) : transform.position;
        public Transform LeftFoot => leftFoot;
        public Transform RightFoot => rightFoot;
        public Transform LeftHand => leftHand;
        public float ArmLength => armLength;
        public float Lean => lean;
        public Transform LeftUpperArm => leftUpperArm;
        public Transform RightHand => rightHand;
        public Transform LeftGrip => leftGrip;
        public Transform RightGrip => rightGrip;

        void Awake()
        {
            bones = new[] { leftUpperLeg, leftLowerLeg, leftFoot, rightUpperLeg, rightLowerLeg, rightFoot,
                leftUpperArm, leftLowerArm, leftHand, rightUpperArm, rightLowerArm, rightHand, head, spine };
            armLength = Vector3.Distance(leftUpperArm.position, leftLowerArm.position) + Vector3.Distance(leftLowerArm.position, leftHand.position);
            restRotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null) restRotations[i] = bones[i].localRotation;

            leftFootRest = Quaternion.Inverse(transform.rotation) * leftFoot.rotation;
            rightFootRest = Quaternion.Inverse(transform.rotation) * rightFoot.rotation;
            // Medidas de la pose de partida: anchura de caderas y altura del tobillo.
            float ground = Ground();
            groundOffset = transform.position.y - ground;
            ankleHeight = Mathf.Max(0.01f, (leftFoot.position.y + rightFoot.position.y) * 0.5f - ground);
            float hipWidth = Vector3.Distance(leftUpperLeg.position, rightUpperLeg.position) * 0.5f;
            float legLength = Vector3.Distance(leftUpperLeg.position, leftLowerLeg.position) + Vector3.Distance(leftLowerLeg.position, leftFoot.position);
            planter = new FootPlanter(hipWidth, legLength * 0.22f, Mathf.Clamp(legLength / 0.6f, 0.5f, 1.2f));
        }

        float Ground() => cartBody != null ? cartBody.position.y - 0.02f : transform.position.y - groundOffset;

        void OnEnable()
        {
            if (planter != null) planter.Snap(transform, Ground());
        }

        void LateUpdate()
        {
            if (planter == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null) bones[i].localRotation = restRotations[i];

            Vector3 velocity = cartBody != null ? cartBody.linearVelocity : Vector3.zero;
            float ground = cartBody != null ? cartBody.position.y - 0.02f : transform.position.y;
            planter.Update(transform, velocity, ground, dt);

            Vector3 forward = transform.forward;
            Leg(leftUpperLeg, leftLowerLeg, leftFoot, planter.FootPosition(0), leftFootRest, -1f);
            Leg(rightUpperLeg, rightLowerLeg, rightFoot, planter.FootPosition(1), rightFootRest, 1f);

            // Si el asa queda lejos de los hombros, el tronco se inclina hacia delante para alcanzarla.
            if (spine != null && leftGrip != null && rightGrip != null)
            {
                float reach = Mathf.Max(Vector3.Distance(leftUpperArm.position, leftGrip.position), Vector3.Distance(rightUpperArm.position, rightGrip.position));
                float wanted = Mathf.Clamp((reach - armLength * 0.92f) / Mathf.Max(0.01f, armLength) * 70f, 0f, 30f);
                lean = Mathf.Lerp(lean, wanted, 1f - Mathf.Exp(-6f * dt));
                spine.rotation = Quaternion.AngleAxis(lean, transform.right) * spine.rotation;
            }

            // Manos en el asa: los codos hacia fuera y abajo.
            if (leftGrip != null) TwoBoneIK.Solve(leftUpperArm, leftLowerArm, leftHand, leftGrip.position, -forward * 0.3f - transform.up * 0.6f - transform.right * 0.6f);
            if (rightGrip != null) TwoBoneIK.Solve(rightUpperArm, rightLowerArm, rightHand, rightGrip.position, -forward * 0.3f - transform.up * 0.6f + transform.right * 0.6f);

            // La cabeza mira hacia donde gira el carrito.
            float turn = cartBody != null ? cartBody.angularVelocity.y * Mathf.Rad2Deg : 0f;
            headYaw = Mathf.Lerp(headYaw, Mathf.Clamp(turn * 0.35f, -maxHeadYaw, maxHeadYaw), 1f - Mathf.Exp(-5f * dt));
            if (head != null) head.rotation = Quaternion.AngleAxis(headYaw, transform.up) * head.rotation;
        }

        void Leg(Transform upper, Transform lower, Transform foot, Vector3 planted, Quaternion footRest, float side)
        {
            Vector3 target = planted + Vector3.up * ankleHeight;
            TwoBoneIK.Solve(upper, lower, foot, target, transform.forward + transform.right * (side * 0.1f));
            foot.rotation = transform.rotation * footRest;
        }

        /// <summary>Configuración usada por el generador de escena.</summary>
        public void Configure(Rigidbody body, Transform[] legs, Transform[] arms, Transform leftHandle, Transform rightHandle, Transform headBone, Transform spineBone)
        {
            spine = spineBone;
            cartBody = body;
            leftUpperLeg = legs[0]; leftLowerLeg = legs[1]; leftFoot = legs[2];
            rightUpperLeg = legs[3]; rightLowerLeg = legs[4]; rightFoot = legs[5];
            leftUpperArm = arms[0]; leftLowerArm = arms[1]; leftHand = arms[2];
            rightUpperArm = arms[3]; rightLowerArm = arms[4]; rightHand = arms[5];
            leftGrip = leftHandle;
            rightGrip = rightHandle;
            head = headBone;
        }
    }
}
