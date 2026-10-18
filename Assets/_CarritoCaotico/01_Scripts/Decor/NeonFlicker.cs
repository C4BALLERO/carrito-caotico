using System;
using UnityEngine;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>
    /// Parpadeo de un neón o una luminaria estropeada: casi siempre encendido y, de vez en cuando,
    /// una ráfaga corta de apagones. Atenúa la emisión de sus mallas, sus luces y sus textos.
    /// Solo decorativo.
    /// </summary>
    public class NeonFlicker : MonoBehaviour
    {
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Renderer[] renderers = Array.Empty<Renderer>();
        [SerializeField] Light[] lights = Array.Empty<Light>();
        [SerializeField] Graphic[] graphics = Array.Empty<Graphic>();
        [Tooltip("Emisión encendida de las mallas (color × intensidad).")]
        [SerializeField, ColorUsage(false, true)] Color emission = Color.white;
        [Tooltip("Segundos entre ráfagas de parpadeo (mínimo y máximo).")]
        [SerializeField] Vector2 burstInterval = new Vector2(2f, 7f);
        [Tooltip("Brillo durante los apagones (0 = apagado del todo).")]
        [SerializeField, Range(0f, 1f)] float dimLevel = 0.15f;

        MaterialPropertyBlock block;
        float[] lightIntensity;
        Color[] graphicColor;
        float nextBurst;
        float burstEnd;
        float seed;
        float appliedLevel = -1f;

        /// <summary>Configuración usada por el generador de escena.</summary>
        public void Configure(Renderer[] meshes, Light[] glow, Graphic[] texts, Color emissionColor)
        {
            renderers = meshes ?? Array.Empty<Renderer>();
            lights = glow ?? Array.Empty<Light>();
            graphics = texts ?? Array.Empty<Graphic>();
            emission = emissionColor;
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            lightIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) lightIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
            graphicColor = new Color[graphics.Length];
            for (int i = 0; i < graphics.Length; i++) graphicColor[i] = graphics[i] != null ? graphics[i].color : Color.white;
            seed = UnityEngine.Random.value * 100f;
            nextBurst = Time.time + UnityEngine.Random.Range(burstInterval.x * 0.5f, burstInterval.y);
        }

        void Update()
        {
            if (Time.time >= nextBurst)
            {
                burstEnd = Time.time + UnityEngine.Random.Range(0.15f, 0.6f);
                nextBurst = burstEnd + UnityEngine.Random.Range(burstInterval.x, burstInterval.y);
            }
            bool off = Time.time < burstEnd && Mathf.PerlinNoise(Time.time * 25f, seed) < 0.55f;
            Apply(off ? dimLevel : 1f);
        }

        void Apply(float level)
        {
            if (Mathf.Approximately(level, appliedLevel)) return;
            appliedLevel = level;

            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionId, emission * level);
                r.SetPropertyBlock(block);
            }
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) lights[i].intensity = lightIntensity[i] * level;
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] == null) continue;
                Color c = graphicColor[i];
                c.a *= Mathf.Lerp(0.3f, 1f, level);
                graphics[i].color = c;
            }
        }
    }
}
