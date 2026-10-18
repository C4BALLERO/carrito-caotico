using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Explosión hecha con primitivas: bolas de fuego que crecen y se apagan, humo que sube y un
    /// destello de luz. No usa partículas para no depender de shaders que la compilación podría quitar.
    /// </summary>
    public class ExplosionEffect : MonoBehaviour
    {
        const float Lifetime = 1.6f;

        struct Puff
        {
            public Transform Transform;
            public Renderer Renderer;
            public Vector3 Velocity;
            public float StartSize;
            public float EndSize;
            public Color Color;
            public bool Smoke;
        }

        Puff[] puffs;
        Light flash;
        float age;
        MaterialPropertyBlock block;
        static Material sharedMaterial;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public static ExplosionEffect Spawn(Vector3 position, float scale = 1f)
        {
            var go = new GameObject("Explosion");
            go.transform.position = position;
            var effect = go.AddComponent<ExplosionEffect>();
            effect.Build(scale);
            return effect;
        }

        static Material Material()
        {
            if (sharedMaterial != null) return sharedMaterial;
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            sharedMaterial = new Material(shader) { name = "M_Explosion" };
            sharedMaterial.EnableKeyword("_EMISSION");
            return sharedMaterial;
        }

        void Build(float scale)
        {
            block = new MaterialPropertyBlock();
            var random = new System.Random(UnityEngine.Random.Range(1, int.MaxValue));
            float R() => (float)random.NextDouble();

            puffs = new Puff[26];
            for (int i = 0; i < puffs.Length; i++)
            {
                bool smoke = i >= 16;
                GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(ball.GetComponent<Collider>());
                ball.name = smoke ? "Humo" : "Fuego";
                ball.transform.SetParent(transform, false);
                Renderer renderer = ball.GetComponent<Renderer>();
                renderer.sharedMaterial = Material();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                Vector3 direction = new Vector3(R() * 2f - 1f, R() * 1.2f + 0.2f, R() * 2f - 1f).normalized;
                Color color = smoke
                    ? Color.Lerp(new Color(0.25f, 0.25f, 0.28f), new Color(0.45f, 0.43f, 0.42f), R())
                    : Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.32f, 0.05f), R());
                puffs[i] = new Puff
                {
                    Transform = ball.transform,
                    Renderer = renderer,
                    Velocity = direction * (smoke ? 1.5f + R() * 1.5f : 4f + R() * 5f) * scale,
                    StartSize = (smoke ? 0.4f : 0.3f) * scale,
                    EndSize = (smoke ? 1.6f + R() : 1.0f + R() * 0.8f) * scale,
                    Color = color,
                    Smoke = smoke
                };
                ball.transform.localPosition = direction * 0.2f;
                ball.transform.localScale = Vector3.one * puffs[i].StartSize;
            }

            var lightObject = new GameObject("Destello");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.up * 0.8f;
            flash = lightObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.6f, 0.2f);
            flash.range = 12f * scale;
            flash.intensity = 8f;
            Apply(0f);
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Lifetime);
            Apply(t);
            if (age >= Lifetime) Destroy(gameObject);
        }

        void Apply(float t)
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < puffs.Length; i++)
            {
                Puff puff = puffs[i];
                if (puff.Transform == null) continue;
                // El fuego frena rápido; el humo sigue subiendo.
                puff.Velocity *= puff.Smoke ? 0.97f : 0.9f;
                if (puff.Smoke) puff.Velocity += Vector3.up * (1.2f * dt);
                puff.Transform.localPosition += puff.Velocity * dt;
                float grow = 1f - Mathf.Pow(1f - t, 3f);
                float size = Mathf.Lerp(puff.StartSize, puff.EndSize, grow) * (puff.Smoke ? 1f : 1f - t * 0.7f);
                puff.Transform.localScale = Vector3.one * Mathf.Max(0.01f, size);

                Color color = puff.Smoke ? puff.Color : Color.Lerp(puff.Color, new Color(0.3f, 0.1f, 0.05f), t);
                block.SetColor(ColorId, color);
                block.SetColor(BaseColorId, color);
                block.SetColor(EmissionId, puff.Smoke ? Color.black : color * (2.5f * (1f - t)));
                puff.Renderer.SetPropertyBlock(block);
                puffs[i] = puff;
            }
            if (flash != null) flash.intensity = Mathf.Lerp(8f, 0f, t * 2.5f);
        }
    }
}
