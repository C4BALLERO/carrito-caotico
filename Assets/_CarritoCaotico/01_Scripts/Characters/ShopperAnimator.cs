using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>Quien decide qué hace un comprador (su IA) informa a la animación con esto.</summary>
    public interface IShopperBrain
    {
        ShopperAction Action { get; }
        /// <summary>Punto que alcanza la mano (producto) cuando <see cref="Action"/> es Reaching.</summary>
        Vector3 ReachPoint { get; }
        /// <summary>0..1 dentro del gesto de alcanzar: ir al producto, dejarlo en el carrito y volver.</summary>
        float ReachProgress { get; }
    }

    /// <summary>
    /// Animación procedural de un comprador caricaturesco a partir de su movimiento real:
    /// pisadas plantadas (sin patinar), caderas que rebotan al paso, inclinación al acelerar,
    /// frenar y girar con un poco de rebote al detenerse, brazos que empujan su carrito (o se
    /// balancean), mano que alcanza productos agachándose si están bajos, cabeza que mira
    /// productos, a los jugadores y el camino, parpadeo, cejas y boca expresivas y gestos al
    /// curiosear. Cada comprador tiene su ritmo para que no se muevan todos a la vez.
    /// No mueve el objeto: solo coloca los huesos después de que la IA o la física lo muevan.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class ShopperAnimator : MonoBehaviour
    {
        enum Gesture { None, Nod, ScratchHead, Shrug, LookAround }

        [SerializeField] ShopperRig rig;
        [Tooltip("0 = tranquilo, 1 = muy saltarín.")]
        [SerializeField, Range(0f, 1f)] float bounciness = 0.5f;
        [SerializeField] int seed;
        [Tooltip("Más lejos que esto de la cámara se actualiza en fotogramas alternos.")]
        [SerializeField] float farDistance = 28f;

        struct Rest
        {
            public Transform Bone;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        Rest[] rest;
        Quaternion leftFootRest;
        Quaternion rightFootRest;
        Vector3 leftEyeScale;
        Vector3 rightEyeScale;
        Vector3 leftPupilRest;
        Vector3 rightPupilRest;
        Vector3 leftBrowRest;
        Vector3 rightBrowRest;
        Vector3 mouthScale;
        float legLength;

        IShopperBrain brain;
        FootPlanter planter;
        Renderer[] renderers;
        Transform playerCart;

        Vector3 lastPosition;
        float lastYaw;
        Vector3 velocity;
        float speed;
        float lastSpeed;
        float acceleration;
        float yawRate;
        float spring;
        float springVelocity;
        float lookYaw;
        float lookPitch;
        float reachWeight;
        float crouch;
        float torsoTurn;
        float surprise;
        float blink;
        float nextBlink;
        float nextGesture;
        float gestureTime;
        Gesture gesture;
        float clock;
        float pendingDelta;
        bool visible = true;
        int visibilityCheck;
        System.Random random;

        public ShopperRig Rig => rig;
        public float Speed => speed;
        public int Steps => planter != null ? planter.Steps : 0;
        public Vector3 FootPosition(int index) => planter != null ? planter.FootPosition(index) : transform.position;
        /// <summary>Peso actual del gesto de alcanzar (0..1).</summary>
        public float ReachWeight => reachWeight;
        /// <summary>Cuánto se agacha ahora mismo, en metros.</summary>
        public float Crouch => crouch;
        public bool IsAnimating { get; private set; }
        /// <summary>Anima aunque no se vea (pruebas, cinemáticas).</summary>
        public bool AlwaysAnimate { get; set; }

        /// <summary>Configuración usada por el generador de modelos.</summary>
        public void Configure(ShopperRig shopperRig, float bounce, int randomSeed)
        {
            rig = shopperRig;
            bounciness = bounce;
            seed = randomSeed;
        }

        void Awake()
        {
            if (rig == null) rig = GetComponentInChildren<ShopperRig>();
            brain = GetComponent<IShopperBrain>();
            random = new System.Random(seed == 0 ? GetHashCode() : seed);
            clock = (float)random.NextDouble() * 10f;
            nextBlink = 1f + (float)random.NextDouble() * 3f;
            nextGesture = 2f + (float)random.NextDouble() * 3f;
            renderers = GetComponentsInChildren<Renderer>(true);
            if (rig == null || !rig.IsComplete)
            {
                Debug.LogWarning($"[Carrito] {name}: el esqueleto del comprador está incompleto; no se anima.", this);
                enabled = false;
                return;
            }

            Transform[] skeleton = rig.Skeleton;
            rest = new Rest[skeleton.Length];
            for (int i = 0; i < skeleton.Length; i++)
                rest[i] = new Rest { Bone = skeleton[i], LocalPosition = skeleton[i].localPosition, LocalRotation = skeleton[i].localRotation };
            leftFootRest = Quaternion.Inverse(transform.rotation) * rig.leftFoot.rotation;
            rightFootRest = Quaternion.Inverse(transform.rotation) * rig.rightFoot.rotation;
            if (rig.leftEye != null) leftEyeScale = rig.leftEye.localScale;
            if (rig.rightEye != null) rightEyeScale = rig.rightEye.localScale;
            if (rig.leftPupil != null) leftPupilRest = rig.leftPupil.localPosition;
            if (rig.rightPupil != null) rightPupilRest = rig.rightPupil.localPosition;
            if (rig.leftBrow != null) leftBrowRest = rig.leftBrow.localPosition;
            if (rig.rightBrow != null) rightBrowRest = rig.rightBrow.localPosition;
            if (rig.mouth != null) mouthScale = rig.mouth.localScale;
            legLength = Vector3.Distance(rig.leftUpperLeg.position, rig.leftLowerLeg.position) + Vector3.Distance(rig.leftLowerLeg.position, rig.leftFoot.position);

            planter = new FootPlanter(rig.hipWidth * rig.transform.lossyScale.x, rig.stepHeight * rig.transform.lossyScale.y, rig.strideScale);
            lastPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
        }

        void OnEnable()
        {
            if (planter != null) planter.Snap(transform, transform.position.y);
            lastPosition = transform.position;
        }

        /// <summary>Cliente LAN: la IA no corre aquí, el estado llega por la red.</summary>
        public void SetBrain(IShopperBrain shopperBrain) => brain = shopperBrain;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || rest == null) return;
            clock += dt;

            Vector3 position = transform.position;
            Vector3 frameVelocity = (position - lastPosition) / dt;
            lastPosition = position;
            frameVelocity.y = 0f;
            if (frameVelocity.sqrMagnitude > 400f) frameVelocity = Vector3.zero; // aparición o teletransporte
            velocity = Vector3.Lerp(velocity, frameVelocity, 1f - Mathf.Exp(-10f * dt));
            speed = velocity.magnitude;
            acceleration = Mathf.Lerp(acceleration, (speed - lastSpeed) / dt, 1f - Mathf.Exp(-8f * dt));
            lastSpeed = speed;
            float yaw = transform.eulerAngles.y;
            yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, yaw) / dt, 1f - Mathf.Exp(-8f * dt));
            lastYaw = yaw;

            // Los pies se calculan siempre (es barato y evita saltos al volver a verse).
            planter.Update(transform, velocity, position.y, dt);

            if (++visibilityCheck >= 10)
            {
                visibilityCheck = 0;
                visible = false;
                foreach (Renderer r in renderers)
                    if (r != null && r.isVisible) { visible = true; break; }
            }
            IsAnimating = visible || AlwaysAnimate;
            if (!IsAnimating) return;

            // Lejos de la cámara: huesos en fotogramas alternos.
            pendingDelta += dt;
            Camera cam = Camera.main;
            if (!AlwaysAnimate && cam != null && (cam.transform.position - position).sqrMagnitude > farDistance * farDistance && Time.frameCount % 2 == 0) return;
            float step = pendingDelta;
            pendingDelta = 0f;

            RestorePose();
            Animate(step);
        }

        void RestorePose()
        {
            for (int i = 0; i < rest.Length; i++)
            {
                rest[i].Bone.localPosition = rest[i].LocalPosition;
                rest[i].Bone.localRotation = rest[i].LocalRotation;
            }
        }

        void Animate(float dt)
        {
            ShopperAction action = brain != null ? brain.Action : ShopperAction.None;
            float run = Mathf.Clamp01(speed / 1.6f);
            float energy = Mathf.Lerp(0.6f, 1.4f, bounciness);
            Vector3 up = transform.up;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // ---------- Reacciones ----------
            surprise = Mathf.MoveTowards(surprise, action == ShopperAction.Startled ? 1f : 0f, dt * 4f);
            // Rebote amortiguado: al arrancar se echa atrás y al frenar se va hacia delante (anticipación y continuidad).
            springVelocity += (-acceleration * 0.06f * energy - spring * 60f - springVelocity * 9f) * dt;
            spring = Mathf.Clamp(spring + springVelocity * dt, -0.6f, 0.6f);

            // ---------- Alcanzar productos ----------
            bool reaching = action == ShopperAction.Reaching && brain != null;
            float reachProgress = reaching ? brain.ReachProgress : 0f;
            reachWeight = Mathf.MoveTowards(reachWeight, reaching ? 1f : 0f, dt * 3f);
            Vector3 reachPoint = brain != null ? brain.ReachPoint : DefaultLookPoint(forward);
            float lowness = Mathf.Clamp01((rig.hips.position.y - reachPoint.y) / Mathf.Max(0.1f, legLength));
            // Se agacha mientras la mano va al producto y se levanta al llevarlo a la canasta.
            float hold = reaching ? Plateau(reachProgress) : 0f;
            crouch = Mathf.MoveTowards(crouch, reachWeight * hold * lowness * legLength * 0.55f, dt * 1.4f);
            Vector3 toTarget = Vector3.ProjectOnPlane(reachPoint - transform.position, up);
            float targetSide = Vector3.SignedAngle(forward, toTarget, up);
            bool browsing = action == ShopperAction.Browsing || reaching;
            torsoTurn = Mathf.MoveTowards(torsoTurn, browsing ? Mathf.Clamp(targetSide, -60f, 60f) : 0f, dt * 120f);

            // ---------- Caderas ----------
            float bob = planter.SwingAmount * 0.035f * energy * run;
            float sway = Mathf.Sin(clock * 1.3f) * 0.012f * (1f - run); // cambio de peso en reposo
            rig.hips.position += up * (bob - crouch) + right * sway;
            float pitch = run * 5f + Mathf.Clamp(acceleration * 3f, -7f, 7f) + spring * 25f + crouch * 40f;
            float roll = Mathf.Clamp(-yawRate * 0.04f, -9f, 9f) + sway * 120f;
            rig.hips.rotation = Quaternion.AngleAxis(roll, forward) * Quaternion.AngleAxis(pitch, right) * rig.hips.rotation;

            // ---------- Piernas (IK hacia los pies plantados) ----------
            float scaleY = rig.transform.lossyScale.y;
            Leg(rig.leftUpperLeg, rig.leftLowerLeg, rig.leftFoot, planter.FootPosition(0), leftFootRest, -1f, scaleY);
            Leg(rig.rightUpperLeg, rig.rightLowerLeg, rig.rightFoot, planter.FootPosition(1), rightFootRest, 1f, scaleY);

            // ---------- Torso ----------
            float legPhase = Vector3.Dot(planter.FootPosition(1) - planter.FootPosition(0), forward); // >0: pie derecho delante
            float breath = Mathf.Sin(clock * 1.8f) * 2f;
            float counterTwist = -legPhase * 18f * energy;
            rig.spine.rotation = Quaternion.AngleAxis(counterTwist * 0.4f + torsoTurn * 0.35f, up) * rig.spine.rotation;
            // Se inclina hacia lo que alcanza (más cuanto más bajo o lejos está).
            if (reachWeight > 0f)
            {
                float far = Mathf.Clamp01((Vector3.Distance(rig.chest.position, reachPoint) - 0.45f * rig.transform.lossyScale.y) / 0.5f);
                float lean = (lowness * 30f + far * 15f) * reachWeight * (reaching ? Plateau(reachProgress) : 0f);
                Vector3 toward = Vector3.ProjectOnPlane(reachPoint - rig.spine.position, up);
                Vector3 axis = toward.sqrMagnitude > 1e-4f ? Vector3.Cross(up, toward.normalized) : right;
                rig.spine.rotation = Quaternion.AngleAxis(lean, axis) * rig.spine.rotation;
            }
            rig.chest.rotation = Quaternion.AngleAxis(breath, right) * Quaternion.AngleAxis(counterTwist * 0.6f + torsoTurn * 0.4f, up) * rig.chest.rotation;

            // ---------- Gestos al curiosear ----------
            UpdateGesture(dt, action, speed);
            float gestureWeight = gesture == Gesture.None ? 0f : Phase(gestureTime / 1.4f, 0f, 1f);
            if (gesture == Gesture.Shrug)
            {
                float lift = gestureWeight * 0.06f;
                rig.leftShoulder.position += up * lift;
                rig.rightShoulder.position += up * lift;
            }

            // ---------- Brazos ----------
            bool reachRight = targetSide >= 0f;
            Arm(rig.leftUpperArm, rig.leftLowerArm, rig.leftHand, rig.leftGrip, -1f, legPhase, energy,
                !reachRight ? reachWeight : 0f, reachProgress, reachPoint, gesture, gestureWeight, false);
            Arm(rig.rightUpperArm, rig.rightLowerArm, rig.rightHand, rig.rightGrip, 1f, -legPhase, energy,
                reachRight ? reachWeight : 0f, reachProgress, reachPoint, gesture, gestureWeight, true);

            // ---------- Cabeza ----------
            Vector3 look = LookTarget(action, reachPoint);
            Vector3 lookLocal = transform.InverseTransformDirection(look - rig.head.position);
            float desiredYaw = Mathf.Clamp(Mathf.Atan2(lookLocal.x, Mathf.Max(0.01f, lookLocal.z)) * Mathf.Rad2Deg, -75f, 75f);
            float desiredPitch = Mathf.Clamp(-Mathf.Atan2(lookLocal.y, new Vector2(lookLocal.x, lookLocal.z).magnitude) * Mathf.Rad2Deg, -30f, 35f);
            if (lookLocal.z < -0.2f) desiredYaw = Mathf.Sign(lookLocal.x) * 75f; // detrás: lo justo hasta el hombro
            if (gesture == Gesture.Nod) desiredPitch += Mathf.Sin(gestureTime * 9f) * 12f * gestureWeight;
            if (gesture == Gesture.LookAround) desiredYaw += Mathf.Sin(gestureTime * 3f) * 50f * gestureWeight;
            float headSpeed = 1f - Mathf.Exp(-6f * dt);
            lookYaw = Mathf.Lerp(lookYaw, desiredYaw - torsoTurn * 0.75f, headSpeed);
            lookPitch = Mathf.Lerp(lookPitch, desiredPitch, headSpeed);
            rig.neck.rotation = Quaternion.AngleAxis(lookYaw * 0.4f, up) * Quaternion.AngleAxis(lookPitch * 0.4f, right) * rig.neck.rotation;
            float headTilt = Mathf.Sin(clock * 0.7f) * 3f + (gesture == Gesture.ScratchHead ? 10f * gestureWeight : 0f);
            rig.head.rotation = Quaternion.AngleAxis(headTilt, rig.head.forward) * Quaternion.AngleAxis(lookYaw * 0.6f, up) * Quaternion.AngleAxis(lookPitch * 0.6f, right) * rig.head.rotation;

            Face(dt, action);
        }

        Vector3 DefaultLookPoint(Vector3 forward) => transform.position + forward * 2f + Vector3.up;

        void Leg(Transform upper, Transform lower, Transform foot, Vector3 planted, Quaternion footRest, float side, float scaleY)
        {
            Vector3 target = planted + Vector3.up * (rig.footHeight * scaleY);
            Vector3 hint = transform.forward + transform.right * (side * 0.15f);
            TwoBoneIK.Solve(upper, lower, foot, target, hint);
            // Pie plano en el suelo, girado con el cuerpo.
            foot.rotation = transform.rotation * footRest;
        }

        void Arm(Transform upper, Transform lower, Transform hand, Transform grip, float side, float legOffset, float energy,
            float reach, float progress, Vector3 reachPoint, Gesture currentGesture, float gestureWeight, bool gestureHand)
        {
            Vector3 up = transform.up;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 elbowHint = -forward * 0.6f - up * 0.4f + right * (side * 0.5f);

            // Brazo libre: se balancea al contrario que la pierna de su lado.
            Vector3 swingTarget = upper.position - up * 0.48f * rig.transform.lossyScale.y
                                  + forward * (legOffset * 0.8f * energy * Mathf.Clamp01(speed / 1.2f))
                                  + right * (side * 0.08f);
            Vector3 target = grip != null ? grip.position : swingTarget;

            if (gestureHand && currentGesture == Gesture.ScratchHead && gestureWeight > 0f)
            {
                Vector3 scratch = rig.head.position + right * (side * 0.16f) + up * 0.08f;
                target = Vector3.Lerp(target, scratch, gestureWeight);
            }
            else if (currentGesture == Gesture.Shrug && gestureWeight > 0f)
            {
                Vector3 palms = upper.position - up * 0.18f + forward * 0.22f + right * (side * 0.3f);
                target = Vector3.Lerp(target, palms, gestureWeight);
            }

            if (reach > 0f)
            {
                // Ir al producto, llevarlo a la canasta y volver al asa.
                Vector3 drop = rig.dropPoint != null ? rig.dropPoint.position : target;
                Vector3 path;
                if (progress < 0.55f) path = Vector3.Lerp(target, reachPoint, Smooth(progress / 0.55f));
                else if (progress < 0.82f) path = Vector3.Lerp(reachPoint, drop, Smooth((progress - 0.55f) / 0.27f));
                else path = Vector3.Lerp(drop, target, Smooth((progress - 0.82f) / 0.18f));
                target = Vector3.Lerp(target, path, reach);
            }

            TwoBoneIK.Solve(upper, lower, hand, target, elbowHint);
        }

        Vector3 LookTarget(ShopperAction action, Vector3 reachPoint)
        {
            if (action == ShopperAction.Reaching || action == ShopperAction.Browsing) return reachPoint;

            // Mira de reojo el carrito de los jugadores si pasa cerca y por delante.
            if (playerCart == null && Time.frameCount % 30 == 0)
            {
                CartController cart = FindAnyObjectByType<CartController>();
                if (cart != null) playerCart = cart.transform;
            }
            if (playerCart != null)
            {
                Vector3 toCart = playerCart.position - transform.position;
                if (toCart.sqrMagnitude < 36f && Vector3.Dot(toCart.normalized, transform.forward) > -0.2f)
                    return playerCart.position + Vector3.up * 1.2f;
            }
            Vector3 ahead = transform.position + transform.forward * 4f + Vector3.up * 1.4f * rig.transform.lossyScale.y;
            return ahead + transform.right * (Mathf.Sin(clock * 0.35f + seed) * 1.5f);
        }

        void UpdateGesture(float dt, ShopperAction action, float currentSpeed)
        {
            if (gesture != Gesture.None)
            {
                gestureTime += dt;
                if (gestureTime >= 1.4f || currentSpeed > 0.3f || action == ShopperAction.Reaching) gesture = Gesture.None;
                return;
            }
            nextGesture -= dt;
            if (nextGesture > 0f || currentSpeed > 0.15f || action == ShopperAction.Reaching) return;
            nextGesture = 2.5f + (float)random.NextDouble() * 3.5f;
            gestureTime = 0f;
            gesture = (Gesture)(1 + random.Next(4));
        }

        void Face(float dt, ShopperAction action)
        {
            nextBlink -= dt;
            if (nextBlink <= 0f)
            {
                blink = 1f;
                nextBlink = 2f + (float)random.NextDouble() * 3.5f;
            }
            blink = Mathf.MoveTowards(blink, 0f, dt * 7f);
            float open = 1f - Mathf.Sin(blink * Mathf.PI) * 0.92f + surprise * 0.25f;
            if (rig.leftEye != null) rig.leftEye.localScale = new Vector3(leftEyeScale.x, leftEyeScale.y * open, leftEyeScale.z);
            if (rig.rightEye != null) rig.rightEye.localScale = new Vector3(rightEyeScale.x, rightEyeScale.y * open, rightEyeScale.z);

            // Las pupilas siguen la mirada.
            var gaze = new Vector3(lookYaw / 75f * 0.25f, -lookPitch / 35f * 0.2f, 0f);
            if (rig.leftPupil != null) rig.leftPupil.localPosition = leftPupilRest + gaze;
            if (rig.rightPupil != null) rig.rightPupil.localPosition = rightPupilRest + gaze;

            // Cejas arriba al curiosear o sorprenderse; boca en «o» con la sorpresa.
            float browLift = (action == ShopperAction.Browsing ? 0.25f : 0f) + surprise * 0.6f + (gesture == Gesture.Shrug ? 0.4f : 0f);
            if (rig.leftBrow != null) rig.leftBrow.localPosition = leftBrowRest + Vector3.up * (browLift * 0.06f);
            if (rig.rightBrow != null) rig.rightBrow.localPosition = rightBrowRest + Vector3.up * (browLift * 0.06f);
            if (rig.mouth != null)
            {
                float talk = gesture == Gesture.Nod ? Mathf.Abs(Mathf.Sin(gestureTime * 14f)) * 0.5f : 0f;
                rig.mouth.localScale = new Vector3(mouthScale.x * (1f - surprise * 0.45f), mouthScale.y * (1f + surprise * 1.6f + talk), mouthScale.z);
            }
        }

        /// <summary>Sube al ir hacia el producto (0..0.35), se mantiene y baja al volver (0.55..0.82).</summary>
        static float Plateau(float progress)
        {
            if (progress < 0.35f) return Smooth(progress / 0.35f);
            if (progress < 0.55f) return 1f;
            return 1f - Smooth((progress - 0.55f) / 0.27f);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Sube de 0 a 1 al empezar y baja al terminar (envolvente de un gesto).</summary>
        static float Phase(float t, float start, float end)
        {
            t = Mathf.Clamp01((t - start) / Mathf.Max(0.0001f, end - start));
            return Mathf.Sin(t * Mathf.PI) > 0f ? Mathf.Min(1f, Mathf.Sin(t * Mathf.PI) * 2.5f) : 0f;
        }
    }
}
