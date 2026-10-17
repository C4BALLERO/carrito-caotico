using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Bamboleo del pingüino Empujador: se balancea de lado a lado y da saltitos al ritmo de la
    /// velocidad del carrito, se inclina en las curvas y agita las aletas. Parado, respira.
    /// Solo mueve el modelo visual; no toca la física.
    /// </summary>
    public class PenguinWaddle : MonoBehaviour
    {
        [SerializeField] Rigidbody cartBody;
        [Tooltip("Modelo visual que se balancea (hijo de este objeto).")]
        [SerializeField] Transform model;
        [SerializeField] Transform leftFlipper;
        [SerializeField] Transform rightFlipper;

        [Header("Bamboleo")]
        [Tooltip("Pasos por metro recorrido: ritmo del balanceo.")]
        [SerializeField, Min(0f)] float stepsPerMeter = 1.4f;
        [Tooltip("Velocidad (m/s) a la que el bamboleo llega a su máximo.")]
        [SerializeField, Min(0.1f)] float fullWaddleSpeed = 3f;
        [SerializeField, Range(0f, 30f)] float rollDegrees = 10f;
        [SerializeField, Min(0f)] float hopHeight = 0.05f;
        [SerializeField, Range(0f, 20f)] float turnLeanDegrees = 7f;
        [SerializeField, Range(0f, 60f)] float flapDegrees = 22f;

        [Header("Reposo")]
        [SerializeField, Min(0f)] float breathHeight = 0.012f;
        [SerializeField, Min(0f)] float breathSpeed = 2.2f;

        Vector3 restPosition;
        Quaternion restRotation;
        Quaternion leftRest;
        Quaternion rightRest;
        float phase;
        float intensity;
        float lean;

        /// <summary>Entre 0 (quieto) y 1 (bamboleo completo).</summary>
        public float Intensity => intensity;

        void Awake()
        {
            if (model == null) model = transform;
            restPosition = model.localPosition;
            restRotation = model.localRotation;
            if (leftFlipper != null) leftRest = leftFlipper.localRotation;
            if (rightFlipper != null) rightRest = rightFlipper.localRotation;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 velocity = cartBody != null ? cartBody.linearVelocity : Vector3.zero;
            velocity.y = 0f;
            float speed = velocity.magnitude;

            intensity = Mathf.MoveTowards(intensity, Mathf.Clamp01(speed / fullWaddleSpeed), dt * 4f);
            // Cada paso es medio ciclo del seno: un balanceo a la izquierda y otro a la derecha.
            phase = Mathf.Repeat(phase + dt * Mathf.PI * stepsPerMeter * speed, Mathf.PI * 2f);

            float turnRate = cartBody != null ? cartBody.angularVelocity.y : 0f;
            lean = Mathf.Lerp(lean, -Mathf.Clamp(turnRate * 0.5f, -1f, 1f) * turnLeanDegrees, 1f - Mathf.Exp(-8f * dt));

            float step = Mathf.Sin(phase);
            float roll = step * rollDegrees * intensity + lean;
            float hop = Mathf.Abs(step) * hopHeight * intensity;
            float breath = (Mathf.Sin(Time.time * breathSpeed) * 0.5f + 0.5f) * breathHeight * (1f - intensity);

            model.localPosition = restPosition + Vector3.up * (hop + breath);
            model.localRotation = restRotation * Quaternion.Euler(0f, 0f, roll);

            // Aletas: suben y bajan alrededor del eje frontal del cuerpo, en oposición.
            float flap = Mathf.Sin(phase * 2f) * flapDegrees * intensity;
            Vector3 axis = model.forward;
            if (leftFlipper != null)
            {
                leftFlipper.localRotation = leftRest;
                leftFlipper.rotation = Quaternion.AngleAxis(-flap, axis) * leftFlipper.rotation;
            }
            if (rightFlipper != null)
            {
                rightFlipper.localRotation = rightRest;
                rightFlipper.rotation = Quaternion.AngleAxis(flap, axis) * rightFlipper.rotation;
            }
        }
    }
}
