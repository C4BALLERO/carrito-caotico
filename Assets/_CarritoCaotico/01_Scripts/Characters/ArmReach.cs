using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Gira un brazo para que apunte siempre a un objetivo: el zorro Agarrador señala su mano
    /// extensible mientras la mueve. Parte de la pose original del brazo en cada fotograma.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ArmReach : MonoBehaviour
    {
        [SerializeField] Transform upperArm;
        [Tooltip("Hueso de la mano de ese brazo: define hacia dónde apunta.")]
        [SerializeField] Transform hand;
        [SerializeField] Transform target;

        Quaternion rest;

        void Awake()
        {
            rest = upperArm.localRotation;
        }

        void LateUpdate()
        {
            upperArm.localRotation = rest;
            Vector3 current = hand.position - upperArm.position;
            Vector3 desired = target.position - upperArm.position;
            if (current.sqrMagnitude > 1e-6f && desired.sqrMagnitude > 1e-6f)
                upperArm.rotation = Quaternion.FromToRotation(current, desired) * upperArm.rotation;
        }
    }
}
