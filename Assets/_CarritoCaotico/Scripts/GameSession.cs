using System;
using UnityEngine;

namespace CarritoCaotico
{
    /// <summary>
    /// Estado que sobrevive a la recarga de escena: nivel actual, si se entra directamente
    /// a jugar (sin menú) y el progreso desbloqueado, guardado en PlayerPrefs.
    /// </summary>
    public static class GameSession
    {
        const string MaxLevelKey = "CarritoCaotico.NivelMaximo";

        /// <summary>Nivel que se juega o se jugará al recargar la escena.</summary>
        public static int CurrentLevel { get; private set; } = 1;

        /// <summary>true: al cargar la escena se empieza el nivel sin pasar por el menú.</summary>
        public static bool StartDirectly { get; private set; }

        /// <summary>Semilla de la sesión: el esquema de controles de un nivel no cambia al reintentarlo.</summary>
        public static int SessionSeed { get; private set; } = Environment.TickCount;

        public static int MaxUnlockedLevel => Mathf.Max(1, PlayerPrefs.GetInt(MaxLevelKey, 1));

        /// <summary>Prepara la sesión para jugar un nivel concreto.</summary>
        public static void Begin(int level)
        {
            CurrentLevel = Mathf.Max(1, level);
            StartDirectly = true;
        }

        /// <summary>La próxima carga de escena mostrará el menú principal.</summary>
        public static void ReturnToMenu()
        {
            StartDirectly = false;
        }

        public static void Unlock(int level)
        {
            if (level <= MaxUnlockedLevel) return;
            PlayerPrefs.SetInt(MaxLevelKey, level);
            PlayerPrefs.Save();
        }

        /// <summary>Fija la semilla (pruebas): hace reproducible el esquema de controles.</summary>
        public static void SetSeed(int seed) => SessionSeed = seed;
    }
}
