using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>Lo que está haciendo un comprador, para su animación.</summary>
    public enum ShopperAction : byte
    {
        None = 0,
        /// <summary>Mira la estantería con calma.</summary>
        Browsing = 1,
        /// <summary>Alcanza un producto con la mano y lo deja en su carrito.</summary>
        Reaching = 2,
        /// <summary>Sorpresa breve (le quitaron el producto, choque).</summary>
        Startled = 3
    }

    /// <summary>
    /// Esqueleto de un comprador: referencias a sus huesos (caderas → columna → pecho → cuello →
    /// cabeza; hombros → brazos → antebrazos → manos; muslos → piernas → pies) y a las piezas de la
    /// cara. No mueve nada: lo usa <see cref="ShopperAnimator"/>. Un modelo con piel se puede
    /// conectar asignando aquí sus huesos.
    /// </summary>
    public class ShopperRig : MonoBehaviour
    {
        [Header("Tronco")]
        public Transform hips;
        public Transform spine;
        public Transform chest;
        public Transform neck;
        public Transform head;

        [Header("Brazos")]
        public Transform leftShoulder;
        public Transform leftUpperArm;
        public Transform leftLowerArm;
        public Transform leftHand;
        public Transform rightShoulder;
        public Transform rightUpperArm;
        public Transform rightLowerArm;
        public Transform rightHand;

        [Header("Piernas")]
        public Transform leftUpperLeg;
        public Transform leftLowerLeg;
        public Transform leftFoot;
        public Transform rightUpperLeg;
        public Transform rightLowerLeg;
        public Transform rightFoot;

        [Header("Cara")]
        public Transform leftEye;
        public Transform rightEye;
        public Transform leftPupil;
        public Transform rightPupil;
        public Transform leftBrow;
        public Transform rightBrow;
        public Transform mouth;

        [Header("Agarres (asa del carrito o caja)")]
        [Tooltip("Vacío: los brazos se balancean al caminar.")]
        public Transform leftGrip;
        public Transform rightGrip;
        [Tooltip("Donde deja lo que coge (canasta de su carrito).")]
        public Transform dropPoint;

        [Header("Medidas")]
        [Tooltip("Altura del tobillo sobre el suelo.")]
        public float footHeight = 0.08f;
        [Tooltip("Separación lateral de cada pie respecto al centro.")]
        public float hipWidth = 0.12f;
        public float stepHeight = 0.09f;
        [Tooltip("Longitud de paso relativa (personajes altos dan pasos más largos).")]
        public float strideScale = 1f;

        /// <summary>Nombres de los huesos obligatorios, en el orden de <see cref="Skeleton"/>.</summary>
        public static readonly string[] BoneNames =
        {
            "Caderas", "Columna", "Pecho", "Cuello", "Cabeza",
            "Hombro_Izq", "Brazo_Izq", "Antebrazo_Izq", "Mano_Izq",
            "Hombro_Der", "Brazo_Der", "Antebrazo_Der", "Mano_Der",
            "Muslo_Izq", "Pierna_Izq", "Pie_Izq",
            "Muslo_Der", "Pierna_Der", "Pie_Der"
        };

        public Transform[] Skeleton => new[]
        {
            hips, spine, chest, neck, head,
            leftShoulder, leftUpperArm, leftLowerArm, leftHand,
            rightShoulder, rightUpperArm, rightLowerArm, rightHand,
            leftUpperLeg, leftLowerLeg, leftFoot,
            rightUpperLeg, rightLowerLeg, rightFoot
        };

        /// <summary>true si están todos los huesos obligatorios.</summary>
        public bool IsComplete
        {
            get
            {
                foreach (Transform bone in Skeleton)
                    if (bone == null) return false;
                return true;
            }
        }

        public bool HasGrips => leftGrip != null && rightGrip != null;
    }
}
