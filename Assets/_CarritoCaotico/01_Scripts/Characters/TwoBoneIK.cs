using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Cinemática inversa de dos huesos (muslo y pierna, brazo y antebrazo). Trabaja con
    /// posiciones del mundo y rotaciones mínimas, así que no depende de cómo estén orientados los
    /// ejes de cada hueso: sirve igual para los FBX de Tripo, el esqueleto Mixamo y los compradores.
    /// </summary>
    public static class TwoBoneIK
    {
        /// <summary>
        /// Coloca el extremo de la cadena (<paramref name="end"/>) sobre <paramref name="target"/>.
        /// <paramref name="bendHint"/> indica hacia dónde se dobla la articulación central (la
        /// rodilla hacia delante, el codo hacia atrás). Con <paramref name="weight"/> &lt; 1 mezcla con
        /// la pose de partida.
        /// </summary>
        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 bendHint, float weight = 1f)
        {
            if (upper == null || lower == null || end == null || weight <= 0f) return;

            Vector3 a = upper.position;
            Vector3 b = lower.position;
            Vector3 c = end.position;
            float upperLength = (b - a).magnitude;
            float lowerLength = (c - b).magnitude;
            if (upperLength < 1e-5f || lowerLength < 1e-5f) return;

            Quaternion upperStart = upper.localRotation;
            Quaternion lowerStart = lower.localRotation;

            Vector3 toTarget = target - a;
            float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - lowerLength) + 1e-4f, upperLength + lowerLength - 1e-4f);
            Vector3 direction = toTarget.sqrMagnitude > 1e-10f ? toTarget.normalized : (c - a).normalized;

            // Plano de flexión: el indicado por el usuario o, si es paralelo, el de la pose actual.
            Vector3 bend = Vector3.ProjectOnPlane(bendHint, direction);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(b - a, direction);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(Vector3.forward, direction);
            bend.Normalize();

            // Ley de los cosenos: ángulo en la articulación superior.
            float cos = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * upperLength * distance);
            cos = Mathf.Clamp(cos, -1f, 1f);
            float sin = Mathf.Sqrt(1f - cos * cos);
            Vector3 middle = a + direction * (upperLength * cos) + bend * (upperLength * sin);
            Vector3 endTarget = a + direction * distance;

            upper.rotation = Quaternion.FromToRotation(b - a, middle - a) * upper.rotation;
            Vector3 lowerPosition = lower.position;
            lower.rotation = Quaternion.FromToRotation(end.position - lowerPosition, endTarget - lowerPosition) * lower.rotation;

            if (weight < 1f)
            {
                upper.localRotation = Quaternion.Slerp(upperStart, upper.localRotation, weight);
                lower.localRotation = Quaternion.Slerp(lowerStart, lower.localRotation, weight);
            }
        }

        /// <summary>Gira un hueso para que su hijo apunte hacia <paramref name="target"/> (con límite de ángulo).</summary>
        public static void Aim(Transform bone, Transform child, Vector3 target, float maxDegrees, float weight = 1f)
        {
            if (bone == null || child == null || weight <= 0f) return;
            Vector3 current = child.position - bone.position;
            Vector3 desired = target - bone.position;
            if (current.sqrMagnitude < 1e-8f || desired.sqrMagnitude < 1e-8f) return;
            Quaternion full = Quaternion.FromToRotation(current, desired);
            full.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            angle = Mathf.Clamp(angle, -maxDegrees, maxDegrees) * weight;
            bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
        }
    }
}
