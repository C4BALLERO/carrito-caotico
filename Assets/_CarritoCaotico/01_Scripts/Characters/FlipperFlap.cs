using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Aletas del pingüino: suben y bajan en oposición al ritmo del bamboleo, alrededor del eje
    /// frontal del cuerpo (no depende de cómo estén orientados los huesos del modelo).
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class FlipperFlap : MonoBehaviour
    {
        [SerializeField] CharacterWaddle waddle;
        [SerializeField] Transform leftFlipper;
        [SerializeField] Transform rightFlipper;
        [SerializeField, Range(0f, 60f)] float flapDegrees = 22f;

        Quaternion leftRest;
        Quaternion rightRest;

        void Awake()
        {
            leftRest = leftFlipper.localRotation;
            rightRest = rightFlipper.localRotation;
        }

        void LateUpdate()
        {
            float flap = Mathf.Sin(waddle.Phase * 2f) * flapDegrees * waddle.Intensity;
            Vector3 axis = waddle.Model.forward;
            leftFlipper.localRotation = leftRest;
            leftFlipper.rotation = Quaternion.AngleAxis(-flap, axis) * leftFlipper.rotation;
            rightFlipper.localRotation = rightRest;
            rightFlipper.rotation = Quaternion.AngleAxis(flap, axis) * rightFlipper.rotation;
        }
    }
}
