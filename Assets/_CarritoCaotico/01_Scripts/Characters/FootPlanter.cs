using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Pisadas de un personaje de dos piernas: cada pie se queda plantado en el suelo hasta que se
    /// aleja demasiado de donde debería estar, y entonces da un paso en arco hacia delante. Los
    /// pasos se alternan y su longitud y ritmo salen de la velocidad real, así los pies nunca
    /// patinan. También cubre los giros sin avanzar y los pasos para recolocarse al detenerse.
    /// </summary>
    public sealed class FootPlanter
    {
        struct Foot
        {
            public Vector3 Position;
            public Vector3 From;
            public Vector3 To;
            public bool Swinging;
            public float Progress;
        }

        readonly Foot[] feet = new Foot[2];
        readonly float hipWidth;
        readonly float stepHeight;
        readonly float strideScale;
        float stillTime;
        bool initialized;

        /// <summary>Cuántos pasos ha dado (pruebas y depuración).</summary>
        public int Steps { get; private set; }

        public FootPlanter(float hipWidth, float stepHeight, float strideScale = 1f)
        {
            this.hipWidth = hipWidth;
            this.stepHeight = stepHeight;
            this.strideScale = Mathf.Max(0.2f, strideScale);
        }

        public Vector3 FootPosition(int index) => feet[index].Position;
        public bool IsSwinging(int index) => feet[index].Swinging;
        /// <summary>0 con los dos pies en el suelo; 1 en mitad de un paso.</summary>
        public float SwingAmount => Mathf.Max(Arc(0), Arc(1));

        float Arc(int i) => feet[i].Swinging ? Mathf.Sin(feet[i].Progress * Mathf.PI) : 0f;

        Vector3 Home(Transform root, int index, float groundY)
        {
            float side = index == 0 ? -1f : 1f;
            Vector3 home = root.position + root.right * (side * hipWidth);
            home.y = groundY;
            return home;
        }

        /// <summary>Coloca los dos pies en su sitio sin pasos (al aparecer o al teletransportarse).</summary>
        public void Snap(Transform root, float groundY)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector3 home = Home(root, i, groundY);
                feet[i] = new Foot { Position = home, From = home, To = home };
            }
            initialized = true;
        }

        public void Update(Transform root, Vector3 velocity, float groundY, float dt)
        {
            if (!initialized) Snap(root, groundY);
            velocity.y = 0f;
            float speed = velocity.magnitude;
            float run = Mathf.Clamp01(speed / 2f);
            float stepDuration = Mathf.Lerp(0.4f, 0.26f, run);
            float threshold = Mathf.Lerp(0.09f, 0.34f, run) * strideScale;
            stillTime = speed < 0.05f ? stillTime + dt : 0f;
            // Parado, se recoloca con pasos cortos si un pie quedó mal puesto.
            if (stillTime > 0.25f) threshold = 0.05f * strideScale;

            var desired = new Vector3[2];
            for (int i = 0; i < 2; i++)
            {
                desired[i] = Home(root, i, groundY) + velocity * (stepDuration * 0.6f);
                // Lejos de golpe (aparición, teletransporte): se recoloca sin paso.
                if ((feet[i].Position - desired[i]).sqrMagnitude > 2.25f) Snap(root, groundY);
            }

            for (int i = 0; i < 2; i++)
            {
                if (!feet[i].Swinging) continue;
                Foot foot = feet[i];
                foot.Progress = Mathf.Min(1f, foot.Progress + dt / stepDuration);
                // El destino se ajusta un poco mientras el pie va en el aire (curvas, cambios de ritmo).
                foot.To = Vector3.Lerp(foot.To, desired[i], dt * 6f);
                float eased = foot.Progress * foot.Progress * (3f - 2f * foot.Progress);
                Vector3 position = Vector3.Lerp(foot.From, foot.To, eased);
                position.y = groundY + Mathf.Sin(foot.Progress * Mathf.PI) * stepHeight * Mathf.Lerp(0.5f, 1f, run);
                foot.Position = position;
                if (foot.Progress >= 1f)
                {
                    foot.Swinging = false;
                    foot.Position = new Vector3(foot.To.x, groundY, foot.To.z);
                }
                feet[i] = foot;
            }

            // Solo un pie en el aire a la vez (el otro puede despegar cuando el primero va por la mitad).
            int candidate = -1;
            float worst = threshold;
            for (int i = 0; i < 2; i++)
            {
                if (feet[i].Swinging) continue;
                Foot other = feet[1 - i];
                if (other.Swinging && other.Progress < 0.6f) continue;
                float error = Horizontal(feet[i].Position - desired[i]);
                if (error > worst)
                {
                    worst = error;
                    candidate = i;
                }
            }
            if (candidate >= 0)
            {
                feet[candidate].Swinging = true;
                feet[candidate].Progress = 0f;
                feet[candidate].From = feet[candidate].Position;
                feet[candidate].To = desired[candidate];
                Steps++;
            }
        }

        static float Horizontal(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }
    }
}
