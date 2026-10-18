using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Sonidos sintetizados por código para los que no hay archivo: la explosión del carrito.
    /// Se genera una vez y se reutiliza.
    /// </summary>
    public static class ProceduralSounds
    {
        const int Rate = 44100;
        static AudioClip explosion;

        /// <summary>Explosión: estallido de ruido que se va apagando, con un retumbo grave debajo.</summary>
        public static AudioClip Explosion()
        {
            if (explosion != null) return explosion;
            const float seconds = 1.6f;
            int length = (int)(Rate * seconds);
            var data = new float[length];
            var random = new System.Random(11);
            float low = 0f;
            float phase = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)Rate;
                float white = (float)random.NextDouble() * 2f - 1f;
                // Filtro paso bajo cuyo corte baja con el tiempo: de crujido a retumbo.
                float cutoff = Mathf.Lerp(0.5f, 0.03f, Mathf.Clamp01(t / 0.9f));
                low += (white - low) * cutoff;
                float attack = Mathf.Clamp01(t / 0.008f);
                float envelope = attack * Mathf.Exp(-t * 3.2f);
                phase += 2f * Mathf.PI * Mathf.Lerp(70f, 32f, t / seconds) / Rate;
                float rumble = Mathf.Sin(phase) * Mathf.Exp(-t * 2.2f) * 0.6f;
                data[i] = Mathf.Clamp((low * 1.8f * envelope) + rumble * attack, -1f, 1f);
            }
            explosion = AudioClip.Create("Explosion_Carrito", length, 1, Rate, false);
            explosion.SetData(data, 0);
            return explosion;
        }
    }
}
