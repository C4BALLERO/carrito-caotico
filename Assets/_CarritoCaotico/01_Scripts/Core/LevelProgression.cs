using System;

namespace CarritoCaotico
{
    /// <summary>Esquemas de control. A partir de cierto nivel pueden cambiar al azar.</summary>
    public enum ControlScheme
    {
        /// <summary>Empujador: WASD + Shift izq. · Agarrador: flechas + Enter + Ctrl der.</summary>
        Normal = 0,
        /// <summary>Los jugadores intercambian teclas: Empujador con flechas, Agarrador con WASD.</summary>
        Intercambiado = 1,
        /// <summary>Teclas normales, pero todas las direcciones van al revés.</summary>
        Invertido = 2
    }

    /// <summary>Parámetros de dificultad de un nivel.</summary>
    public sealed class LevelConfig
    {
        public int Level;
        public float Duration;
        public int MinUnits;
        public int MaxUnits;
        public int MaxPerType;
        public int RivalCount;
        public float RivalSpeed;
        public float RivalGrabSeconds;
        public int RivalCapacity;
        /// <summary>Probabilidad de que un rival vaya a por un producto de la lista del jugador.</summary>
        public float RivalListBias;
        public ControlScheme Scheme;
    }

    /// <summary>
    /// Progresión procedural: cada nivel pide más unidades, da algo menos de tiempo y añade
    /// compradores rivales más rápidos. Desde <see cref="ControlSwapStartLevel"/> los controles
    /// pueden cambiar al azar. La dificultad deja de subir en <see cref="MaxDifficultyLevel"/>.
    /// </summary>
    public static class LevelProgression
    {
        public const int ControlSwapStartLevel = 4;
        public const int MaxDifficultyLevel = 12;
        public const int MaxRivals = 6;
        public const int MaxListUnits = 8;

        public static LevelConfig Get(int level, int seed)
        {
            int l = Math.Max(1, level);
            int d = Math.Min(l, MaxDifficultyLevel);

            var config = new LevelConfig
            {
                Level = l,
                MinUnits = Math.Min(7, 3 + (d - 1) / 2),
                MaxPerType = d >= 5 ? 3 : 2,
                Duration = d <= 2 ? 120f : Math.Max(85f, 120f - 5f * (d - 2)),
                RivalCount = Math.Min(MaxRivals, d / 2),
                RivalSpeed = Math.Min(4.5f, 2.2f + 0.25f * Math.Max(0, d - 2)),
                RivalGrabSeconds = Math.Max(0.5f, 1.4f - 0.08f * d),
                RivalCapacity = 2 + d / 3,
                RivalListBias = Math.Min(0.85f, 0.35f + 0.06f * d),
                Scheme = PickScheme(l, seed)
            };
            // Como mucho 8 unidades: la lista del Agarrador tiene 8 filas.
            config.MaxUnits = Math.Min(MaxListUnits, config.MinUnits + 2);
            return config;
        }

        static ControlScheme PickScheme(int level, int seed)
        {
            if (level < ControlSwapStartLevel) return ControlScheme.Normal;
            var rng = new Random(unchecked(seed * 397) ^ (level * 7919));
            double roll = rng.NextDouble();
            if (roll < 0.4) return ControlScheme.Normal;
            if (roll < 0.7) return ControlScheme.Intercambiado;
            return ControlScheme.Invertido;
        }

        public static string SchemeName(ControlScheme scheme)
        {
            switch (scheme)
            {
                case ControlScheme.Intercambiado: return "Controles intercambiados";
                case ControlScheme.Invertido: return "Controles al revés";
                default: return "Controles normales";
            }
        }
    }
}
