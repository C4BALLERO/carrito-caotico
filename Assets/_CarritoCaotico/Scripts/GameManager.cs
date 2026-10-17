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
        public bool CanAdvance => State == GameState.CompraEntregada && LastReceipt != null && LastReceipt.IsComplete;

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
                ChangeState(GameState.Menu);
                return;
            }
            BeginLevel(GameSession.CurrentLevel);
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

        /// <summary>Empieza un nivel desde el menú, sin recargar la escena.</summary>
        public void StartGame(int level)
        {
            if (State != GameState.Menu) return;
            GameSession.Begin(level);
            BeginLevel(GameSession.CurrentLevel);
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

        /// <summary>Recarga la escena y repite el nivel actual.</summary>
        public void Restart()
        {
            GameSession.Begin(CurrentLevel);
            ReloadScene();
        }

        /// <summary>Pasa al siguiente nivel (solo tras una compra completa).</summary>
        public void NextLevel()
        {
            if (!CanAdvance) return;
            GameSession.Begin(CurrentLevel + 1);
            ReloadScene();
        }

        public void GoToMenu()
        {
            GameSession.ReturnToMenu();
            ReloadScene();
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

            cartContents.FillCounts(deliveredSnapshot);
            float elapsed = timer != null ? timer.Elapsed : 0f;
            float remaining = timer != null ? timer.Remaining : 0f;
            LastReceipt = ShoppingRules.Evaluate(shoppingList.Required, deliveredSnapshot, elapsed, remaining, true);
            if (LastReceipt.IsComplete) GameSession.Unlock(CurrentLevel + 1);

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

        void ReloadScene()
        {
            Time.timeScale = 1f;
            SetCursorLocked(false);
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }
#if UNITY_EDITOR
            // La escena no está en Build Settings: recarga por ruta dentro del Editor.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.name);
#endif
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
