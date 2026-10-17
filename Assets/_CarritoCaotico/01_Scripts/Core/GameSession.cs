using System;
using UnityEngine;

namespace CarritoCaotico
{
    public enum GameMode
    {
        Normal,
        Tutorial
    }

    /// <summary>
    /// Estado que sobrevive a la recarga de escena: nivel, supermercado y modo actuales, si se
    /// entra directamente a jugar (sin menú) y el progreso desbloqueado, guardado en PlayerPrefs.
    /// </summary>
    public static class GameSession
    {
        const string MaxLevelKey = "CarritoCaotico.NivelMaximo";
        const string TutorialDoneKey = "CarritoCaotico.TutorialHecho";

        /// <summary>Nivel que se juega o se jugará al recargar la escena.</summary>
        public static int CurrentLevel { get; private set; } = 1;

        /// <summary>Supermercado elegido en el menú.</summary>
        public static StoreId CurrentStore { get; private set; } = StoreId.Clasico;

        public static GameMode Mode { get; private set; } = GameMode.Normal;

        /// <summary>true: al cargar la escena se empieza sin pasar por el menú.</summary>
        public static bool StartDirectly { get; private set; }

        /// <summary>Semilla de la sesión: el esquema de controles de un nivel no cambia al reintentarlo.</summary>
        public static int SessionSeed { get; private set; } = Environment.TickCount;

        public static int MaxUnlockedLevel => Mathf.Max(1, PlayerPrefs.GetInt(MaxLevelKey, 1));

        public static bool TutorialDone => PlayerPrefs.GetInt(TutorialDoneKey, 0) == 1;

        /// <summary>Prepara la sesión para jugar un nivel en el supermercado actual.</summary>
        public static void Begin(int level) => Begin(level, CurrentStore);

        /// <summary>Prepara la sesión para jugar un nivel en un supermercado concreto.</summary>
        public static void Begin(int level, StoreId store)
        {
            CurrentLevel = Mathf.Max(1, level);
            CurrentStore = store;
            Mode = GameMode.Normal;
            StartDirectly = true;
        }

        /// <summary>Prepara el tutorial (siempre en el supermercado clásico).</summary>
        public static void BeginTutorial()
        {
            CurrentLevel = 1;
            CurrentStore = StoreId.Clasico;
            Mode = GameMode.Tutorial;
            StartDirectly = true;
        }

        /// <summary>La próxima carga de escena mostrará el menú principal.</summary>
        public static void ReturnToMenu()
        {
            StartDirectly = false;
            Mode = GameMode.Normal;
        }

        public static void SelectStore(StoreId store) => CurrentStore = store;

        public static void Unlock(int level)
        {
            if (level <= MaxUnlockedLevel) return;
            PlayerPrefs.SetInt(MaxLevelKey, level);
            PlayerPrefs.Save();
        }

        public static void MarkTutorialDone()
        {
            PlayerPrefs.SetInt(TutorialDoneKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Fija la semilla (pruebas): hace reproducible el esquema de controles.</summary>
        public static void SetSeed(int seed) => SessionSeed = seed;
    }
}
