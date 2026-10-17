using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarritoCaotico
{
    public enum GameState
    {
        Menu,
        Preparacion,
        EnJuego,
        CompraEntregada,
        TiempoAgotado
    }

    public enum CheckoutAttempt
    {
        Accepted,
        EmptyCart,
        NotPlaying
    }

    /// <summary>
    /// Controlador central de estados de la partida: menú, niveles, pausa, evaluación en caja,
    /// fin por tiempo y reinicio. La lógica de gameplay vive en cada sistema.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Sistemas")]
        [SerializeField] GameTimer timer;
        [SerializeField] ShoppingList shoppingList;
        [SerializeField] CartContents cartContents;
        [SerializeField] CartController cart;
        [SerializeField] GrabberArm grabber;
        [SerializeField] CameraController cameraController;
        [SerializeField] RivalManager rivals;

        [Header("Supermercado")]
        [Tooltip("Supermercado que representa esta escena.")]
        [SerializeField] StoreId store = StoreId.Clasico;
        [Tooltip("Lista fija del tutorial (tipos que hay cerca de la salida).")]
        [SerializeField] ProductType[] tutorialProducts = { ProductType.Sandia, ProductType.Manzana };

        [Header("Flujo")]
        [Tooltip("Segundos de preparación para que el Agarrador lea la lista.")]
        [SerializeField, Min(0f)] float preparationTime = 4f;
        [SerializeField] bool lockCursorWhilePlaying = true;
        [Tooltip("Mostrar el menú principal al abrir la escena (si no se viene de un nivel).")]
        [SerializeField] bool showMenuOnStart = true;

        readonly Dictionary<ProductType, int> deliveredSnapshot = new Dictionary<ProductType, int>();
        float preparationRemaining;

        public GameState State { get; private set; } = GameState.Menu;
        public float PreparationRemaining => preparationRemaining;
        public ShoppingReceipt LastReceipt { get; private set; }
        public LevelConfig CurrentConfig { get; private set; }
        public int CurrentLevel => CurrentConfig != null ? CurrentConfig.Level : GameSession.CurrentLevel;
        public bool IsPaused { get; private set; }
        public bool IsGameOver => State == GameState.CompraEntregada || State == GameState.TiempoAgotado;
        public bool CanAdvance => !IsTutorial && State == GameState.CompraEntregada && LastReceipt != null && LastReceipt.IsComplete;
        public bool IsTutorial { get; private set; }
        public StoreId Store => store;

        public event Action<GameState> StateChanged;
        public event Action<ShoppingReceipt> GameEnded;
        public event Action<bool> PauseChanged;
        public event Action<LevelConfig> LevelConfigured;

        void Awake()
        {
            Time.timeScale = 1f;
        }

        void OnEnable()
        {
            if (timer != null) timer.Expired += OnTimerExpired;
        }

        void OnDisable()
        {
            if (timer != null) timer.Expired -= OnTimerExpired;
            SetCursorLocked(false);
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        void Start()
        {
            SetPlayersEnabled(false);
            if (cameraController != null) cameraController.SetLookEnabled(true);

            if (showMenuOnStart && !GameSession.StartDirectly)
            {
                GameInput.Scheme = ControlScheme.Normal;
                GameSession.SelectStore(store);
                ChangeState(GameState.Menu);
                return;
            }
            if (GameSession.Mode == GameMode.Tutorial) BeginTutorial();
            else BeginLevel(GameSession.CurrentLevel);
        }

        void Update()
        {
            if ((State == GameState.Preparacion || State == GameState.EnJuego) && GameInput.PausePressed())
            {
                SetPaused(!IsPaused);
                return;
            }
            if (IsPaused) return;

            if (State == GameState.Preparacion)
            {
                preparationRemaining = Mathf.Max(0f, preparationRemaining - Time.deltaTime);
                if (preparationRemaining <= 0f) BeginPlay();
            }
            else if (IsGameOver)
            {
                if (CanAdvance && GameInput.NextLevelPressed()) NextLevel();
                else if (GameInput.RestartPressed()) Restart();
                else if (GameInput.MenuPressed()) GoToMenu();
            }
        }

        // ---------- Niveles ----------

        /// <summary>Empieza un nivel desde el menú en el supermercado de esta escena.</summary>
        public void StartGame(int level) => StartGame(level, store);

        /// <summary>
        /// Empieza un nivel desde el menú. Si el supermercado es otro, carga su escena con la
        /// pantalla de carga; si es este, empieza sin recargar.
        /// </summary>
        public void StartGame(int level, StoreId targetStore)
        {
            if (State != GameState.Menu) return;
            GameSession.Begin(level, targetStore);
            if (targetStore != store)
            {
                LoadingScreen.LoadStore(targetStore, $"{StoreCatalog.DisplayName(targetStore)} · Nivel {GameSession.CurrentLevel}");
                return;
            }
            BeginLevel(GameSession.CurrentLevel);
        }

        /// <summary>Tutorial guiado: siempre en el supermercado clásico.</summary>
        public void StartTutorial()
        {
            if (State != GameState.Menu) return;
            GameSession.BeginTutorial();
            if (store != StoreId.Clasico)
            {
                LoadingScreen.LoadStore(StoreId.Clasico, "Tutorial");
                return;
            }
            BeginTutorial();
        }

        void BeginTutorial()
        {
            IsTutorial = true;
            CurrentConfig = LevelProgression.Get(1, GameSession.SessionSeed);
            CurrentConfig.RivalCount = 0;
            GameInput.Scheme = ControlScheme.Normal;
            if (timer != null) timer.SetDuration(CurrentConfig.Duration);
            if (shoppingList != null)
            {
                var items = new (ProductType, int)[tutorialProducts.Length];
                for (int i = 0; i < tutorialProducts.Length; i++) items[i] = (tutorialProducts[i], 1);
                shoppingList.SetFixed(items);
            }
            LevelConfigured?.Invoke(CurrentConfig);
            // Sin cuenta atrás ni reloj: el tutorial avanza a su ritmo.
            SetPlayersEnabled(true);
            SetCursorLocked(lockCursorWhilePlaying);
            ChangeState(GameState.EnJuego);
        }

        void BeginLevel(int level)
        {
            CurrentConfig = LevelProgression.Get(level, GameSession.SessionSeed);
            GameInput.Scheme = CurrentConfig.Scheme;

            if (timer != null) timer.SetDuration(CurrentConfig.Duration);
            if (shoppingList != null)
            {
                shoppingList.Configure(CurrentConfig.MinUnits, CurrentConfig.MaxUnits, CurrentConfig.MaxPerType);
                shoppingList.Generate();
            }

            preparationRemaining = preparationTime;
            LevelConfigured?.Invoke(CurrentConfig);
            ChangeState(GameState.Preparacion);
            if (preparationTime <= 0f) BeginPlay();
        }

        void BeginPlay()
        {
            if (State != GameState.Preparacion) return;
            SetPlayersEnabled(true);
            SetCursorLocked(lockCursorWhilePlaying);
            if (timer != null) timer.StartTimer();
            if (rivals != null && CurrentConfig != null) rivals.Begin(CurrentConfig);
            ChangeState(GameState.EnJuego);
        }

        /// <summary>Recarga la escena y repite el nivel actual (o el tutorial).</summary>
        public void Restart()
        {
            if (IsTutorial) GameSession.BeginTutorial();
            else GameSession.Begin(CurrentLevel, store);
            ReloadScene(IsTutorial ? "Tutorial" : $"Reintentar · Nivel {CurrentLevel}");
        }

        /// <summary>Pasa al siguiente nivel (solo tras una compra completa).</summary>
        public void NextLevel()
        {
            if (!CanAdvance) return;
            GameSession.Begin(CurrentLevel + 1, store);
            ReloadScene($"{StoreCatalog.DisplayName(store)} · Nivel {CurrentLevel + 1}");
        }

        public void GoToMenu()
        {
            GameSession.ReturnToMenu();
            ReloadScene("Menú principal");
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- Pausa ----------

        public void SetPaused(bool paused)
        {
            if (paused && State != GameState.Preparacion && State != GameState.EnJuego) return;
            if (IsPaused == paused) return;

            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            bool playing = State == GameState.EnJuego;
            SetPlayersEnabled(!paused && playing);
            if (cameraController != null) cameraController.SetLookEnabled(!paused);
            SetCursorLocked(!paused && playing && lockCursorWhilePlaying);
            PauseChanged?.Invoke(paused);
        }

        // ---------- Caja y fin de partida ----------

        /// <summary>
        /// Llamado por la caja. Congela el contenido de la canasta y evalúa una sola vez.
        /// </summary>
        public CheckoutAttempt TryCheckout()
        {
            if (State != GameState.EnJuego || IsPaused) return CheckoutAttempt.NotPlaying;
            if (cartContents == null || cartContents.Count == 0) return CheckoutAttempt.EmptyCart;

            if (timer != null) timer.StopTimer();
            SetPlayersEnabled(false);
            if (IsTutorial) GameSession.MarkTutorialDone();

            cartContents.FillCounts(deliveredSnapshot);
            float elapsed = timer != null ? timer.Elapsed : 0f;
            float remaining = timer != null ? timer.Remaining : 0f;
            LastReceipt = ShoppingRules.Evaluate(shoppingList.Required, deliveredSnapshot, elapsed, remaining, true);
            if (LastReceipt.IsComplete && !IsTutorial) GameSession.Unlock(CurrentLevel + 1);

            EndGame(GameState.CompraEntregada);
            return CheckoutAttempt.Accepted;
        }

        void OnTimerExpired()
        {
            if (State != GameState.EnJuego) return;

            SetPlayersEnabled(false);
            float duration = timer != null ? timer.Duration : 0f;
            LastReceipt = ShoppingRules.Evaluate(shoppingList.Required, null, duration, 0f, false);
            EndGame(GameState.TiempoAgotado);
        }

        void EndGame(GameState finalState)
        {
            if (rivals != null) rivals.Stop();
            SetCursorLocked(false);
            ChangeState(finalState);
            GameEnded?.Invoke(LastReceipt);
        }

        void ReloadScene(string heading)
        {
            Time.timeScale = 1f;
            SetCursorLocked(false);
            LoadingScreen.Load(SceneManager.GetActiveScene().path, store, heading);
        }

        void ChangeState(GameState newState)
        {
            State = newState;
            StateChanged?.Invoke(newState);
        }

        void SetPlayersEnabled(bool enabled)
        {
            if (cart != null) cart.SetControlsEnabled(enabled);
            if (grabber != null) grabber.SetInputEnabled(enabled);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
