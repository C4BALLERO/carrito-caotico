using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Bamboleo de los personajes del carrito: se balancean de lado a lado y dan saltitos al
    /// ritmo de la velocidad del carrito y se inclinan en las curvas. Parados, respiran.
    /// Solo mueve el modelo visual; no toca la física. Si el cuerpo es cinemático (rivales,
    /// réplicas de la red), la velocidad se mide por cuánto se movió en cada frame.
    /// </summary>
    public class CharacterWaddle : MonoBehaviour
    {
        [SerializeField] Rigidbody cartBody;
        [Tooltip("Modelo visual que se balancea (hijo de este objeto).")]
        [SerializeField] Transform model;

        [Header("Bamboleo")]
        [Tooltip("Pasos por metro recorrido: ritmo del balanceo.")]
        [SerializeField, Min(0f)] float stepsPerMeter = 1.4f;
        [Tooltip("Velocidad (m/s) a la que el bamboleo llega a su máximo.")]
        [SerializeField, Min(0.1f)] float fullWaddleSpeed = 3f;
        [SerializeField, Range(0f, 30f)] float rollDegrees = 10f;
        [SerializeField, Min(0f)] float hopHeight = 0.05f;
        [SerializeField, Range(0f, 20f)] float turnLeanDegrees = 7f;

        [Header("Reposo")]
        [SerializeField, Min(0f)] float breathHeight = 0.012f;
        [SerializeField, Min(0f)] float breathSpeed = 2.2f;

        Vector3 restPosition;
        Quaternion restRotation;
        float lean;
        Vector3 lastPosition;
        float lastYaw;
        Vector3 measuredVelocity;
        float measuredTurnRate;

        /// <summary>Entre 0 (quieto) y 1 (bamboleo completo).</summary>
        public float Intensity { get; private set; }
        /// <summary>Fase del paso en radianes (0..2π): cada paso es medio ciclo.</summary>
        public float Phase { get; private set; }
        public Transform Model => model;

        /// <summary>Configuración usada por el generador de escena.</summary>
        public void ConfigureMotion(float roll, float hop, float turnLean)
        {
            rollDegrees = roll;
            hopHeight = hop;
            turnLeanDegrees = turnLean;
        }

        void Awake()
        {
            if (model == null) model = transform;
            restPosition = model.localPosition;
            restRotation = model.localRotation;
            Transform mover = Mover;
            lastPosition = mover.position;
            lastYaw = mover.eulerAngles.y;
        }

        Transform Mover => cartBody != null ? cartBody.transform : transform;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            MeasureMotion(dt);
            bool simulated = cartBody != null && !cartBody.isKinematic;
            Vector3 velocity = simulated ? cartBody.linearVelocity : measuredVelocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;

            Intensity = Mathf.MoveTowards(Intensity, Mathf.Clamp01(speed / fullWaddleSpeed), dt * 4f);
            Phase = Mathf.Repeat(Phase + dt * Mathf.PI * stepsPerMeter * speed, Mathf.PI * 2f);

            float turnRate = simulated ? cartBody.angularVelocity.y : measuredTurnRate;
            lean = Mathf.Lerp(lean, -Mathf.Clamp(turnRate * 0.5f, -1f, 1f) * turnLeanDegrees, 1f - Mathf.Exp(-8f * dt));

            float step = Mathf.Sin(Phase);
            float roll = step * rollDegrees * Intensity + lean;
            float hop = Mathf.Abs(step) * hopHeight * Intensity;
            float breath = (Mathf.Sin(Time.time * breathSpeed) * 0.5f + 0.5f) * breathHeight * (1f - Intensity);

            model.localPosition = restPosition + Vector3.up * (hop + breath);
            model.localRotation = restRotation * Quaternion.Euler(0f, 0f, roll);
        }

        /// <summary>Velocidad y giro (rad/s) a partir del movimiento del frame, suavizados.</summary>
        void MeasureMotion(float dt)
        {
            Transform mover = Mover;
            Vector3 position = mover.position;
            float yaw = mover.eulerAngles.y;
            if (dt > 0f)
            {
                float smooth = 1f - Mathf.Exp(-10f * dt);
                measuredVelocity = Vector3.Lerp(measuredVelocity, (position - lastPosition) / dt, smooth);
                float turn = Mathf.DeltaAngle(lastYaw, yaw) * Mathf.Deg2Rad / dt;
                measuredTurnRate = Mathf.Lerp(measuredTurnRate, turn, smooth);
            }
            lastPosition = position;
            lastYaw = yaw;
        }
    }
}
