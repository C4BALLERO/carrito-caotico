using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Movimiento arcade del carrito mediante fuerzas sobre un Rigidbody.
    /// No es un simulador de vehículos: prioriza control, inercia y diversión.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CartController : MonoBehaviour
    {
        [Header("Aceleración y frenado")]
        [Tooltip("Fuerza de empuje en newtons. Con más carga, la aceleración es menor.")]
        [SerializeField] float accelerationForce = 210f;
        [Tooltip("Velocidad a la que el acelerador llega al 100 % (por segundo).")]
        [SerializeField] float throttleRampUp = 2.5f;
        [Tooltip("Velocidad a la que el acelerador vuelve a cero (por segundo).")]
        [SerializeField] float throttleRampDown = 5f;
        [SerializeField] float maxSpeed = 7f;
        [SerializeField] float maxReverseSpeed = 3f;
        [SerializeField, Range(0.1f, 1f)] float reverseForceMultiplier = 0.6f;
        [Tooltip("Fuerza de frenado en newtons al pulsar la dirección contraria.")]
        [SerializeField] float brakeForce = 380f;
        [Tooltip("Desaceleración sin acelerar (1/s).")]
        [SerializeField] float rollingResistance = 1.4f;

        [Header("Giro")]
        [Tooltip("Velocidad de giro máxima en grados por segundo.")]
        [SerializeField] float turnSpeed = 130f;
        [Tooltip("Rapidez con la que el giro alcanza su objetivo.")]
        [SerializeField] float turnForce = 9f;
        [Tooltip("Fracción de giro disponible estando parado (los carritos pivotan).")]
        [SerializeField, Range(0f, 1f)] float minTurnFactor = 0.4f;
        [Tooltip("Velocidad a partir de la cual se dispone del giro completo.")]
        [SerializeField] float fullTurnSpeed = 2.5f;

        [Header("Derrape")]
        [Tooltip("Agarre lateral: cuánto se anula el deslizamiento de lado (1/s).")]
        [SerializeField] float lateralFriction = 9f;
        [Tooltip("0 = sin derrape, 1 = sin agarre lateral al derrapar.")]
        [SerializeField, Range(0f, 1f)] float driftIntensity = 0.82f;
        [SerializeField] float driftTurnMultiplier = 1.5f;

        [Header("Peso")]
        [SerializeField, Min(1f)] float baseMass = 30f;
        [Tooltip("Cuánto aumenta la masa efectiva por cada kg de carga.")]
        [SerializeField, Min(0f)] float cargoWeightMultiplier = 1.5f;

        Rigidbody body;
        float throttle;
        float throttleInput;
        float steerInput;
        bool driftInput;
        bool controlsEnabled = true;
        float cargoWeight;

        public Rigidbody Body => body;
        public float CargoWeight => cargoWeight;
        public float BaseMass => baseMass;
        public float MaxSpeed => maxSpeed;
        public bool IsDrifting { get; private set; }
        public bool ControlsEnabled => controlsEnabled;
        public float ForwardSpeed => body != null ? Vector3.Dot(body.linearVelocity, transform.forward) : 0f;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.mass = baseMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // El carrito nunca vuelca: solo gira sobre el eje vertical.
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.centerOfMass = new Vector3(0f, 0.25f, 0f);
        }

        void Update()
        {
            if (controlsEnabled)
            {
                throttleInput = GameInput.PusherThrottle();
                steerInput = GameInput.PusherSteer();
                driftInput = GameInput.PusherDrift();
            }
            else
            {
                throttleInput = 0f;
                steerInput = 0f;
                driftInput = false;
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // Aceleración progresiva: sube despacio y baja rápido.
            bool releasing = Mathf.Abs(throttleInput) < Mathf.Abs(throttle) || Mathf.Sign(throttleInput) != Mathf.Sign(throttle);
            float rate = releasing ? throttleRampDown : throttleRampUp;
            throttle = Mathf.MoveTowards(throttle, throttleInput, rate * dt);

            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 velocity = body.linearVelocity;
            Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
            float forwardSpeed = Vector3.Dot(flatVelocity, forward);
            float lateralSpeed = Vector3.Dot(flatVelocity, right);

            ApplyPropulsion(forward, forwardSpeed);

            // Fricción lateral (reducida al derrapar).
            IsDrifting = driftInput && flatVelocity.magnitude > 1f;
            float grip = lateralFriction * (IsDrifting ? 1f - driftIntensity : 1f);
            body.AddForce(-right * (lateralSpeed * grip), ForceMode.Acceleration);

            ApplySteering(forwardSpeed);
            ClampSpeed(velocity, flatVelocity);
        }

        void ApplyPropulsion(Vector3 forward, float forwardSpeed)
        {
            if (!controlsEnabled)
            {
                // Freno automático cuando la partida termina.
                Vector3 flat = body.linearVelocity;
                flat.y = 0f;
                body.AddForce(-flat * 3f, ForceMode.Acceleration);
                return;
            }

            if (throttle > 0.01f)
            {
                if (forwardSpeed < -0.3f)
                    body.AddForce(forward * (brakeForce * throttle), ForceMode.Force);
                else if (forwardSpeed < maxSpeed)
                    body.AddForce(forward * (accelerationForce * throttle), ForceMode.Force);
            }
            else if (throttle < -0.01f)
            {
                if (forwardSpeed > 0.3f)
                    body.AddForce(-forward * (brakeForce * -throttle), ForceMode.Force);
                else if (forwardSpeed > -maxReverseSpeed)
                    body.AddForce(forward * (accelerationForce * reverseForceMultiplier * throttle), ForceMode.Force);
            }
            else
            {
                body.AddForce(-forward * (forwardSpeed * rollingResistance), ForceMode.Acceleration);
            }
        }

        void ApplySteering(float forwardSpeed)
        {
            float speedFactor = Mathf.Lerp(minTurnFactor, 1f, Mathf.Clamp01(Mathf.Abs(forwardSpeed) / fullTurnSpeed));
            // Al ir marcha atrás se invierte el giro, como un carrito real.
            float direction = forwardSpeed < -0.2f ? -1f : 1f;
            float drift = IsDrifting ? driftTurnMultiplier : 1f;
            float targetYawRate = steerInput * turnSpeed * Mathf.Deg2Rad * speedFactor * direction * drift;

            float currentYawRate = body.angularVelocity.y;
            body.AddTorque(Vector3.up * ((targetYawRate - currentYawRate) * turnForce), ForceMode.Acceleration);
        }

        void ClampSpeed(Vector3 velocity, Vector3 flatVelocity)
        {
            float hardLimit = maxSpeed * 1.25f;
            if (flatVelocity.sqrMagnitude > hardLimit * hardLimit)
            {
                Vector3 limited = flatVelocity.normalized * hardLimit;
                body.linearVelocity = new Vector3(limited.x, velocity.y, limited.z);
            }
        }

        /// <summary>Actualiza la masa efectiva según el peso de los productos en la canasta.</summary>
        public void SetCargoWeight(float weightKg)
        {
            cargoWeight = Mathf.Max(0f, weightKg);
            if (body == null) body = GetComponent<Rigidbody>();
            body.mass = baseMass + cargoWeight * cargoWeightMultiplier;
        }

        public void SetControlsEnabled(bool enabled)
        {
            controlsEnabled = enabled;
            if (!enabled) throttle = 0f;
        }
    }
}
