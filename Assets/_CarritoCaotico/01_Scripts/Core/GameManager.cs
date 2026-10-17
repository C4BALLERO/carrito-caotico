using System;
using System.Collections;
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
    /// En LAN el anfitrión decide todo; el cliente recibe niveles, estados y recibo por
    /// <see cref="LanSync"/> y le pide al anfitrión reintentar, avanzar, ir al menú o pausar.
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

        public GameTimer Timer => timer;
        public ShoppingList List => shoppingList;
        public CartContents Contents => cartContents;
        public CartController Cart => cart;
        public GrabberArm Grabber => grabber;
        public CameraController CameraRig => cameraController;
        public RivalManager Rivals => rivals;

        public bool IsLanHost => LanSession.IsHostActive;
        public bool IsLanClient => LanSession.IsClientActive;
        /// <summary>LAN: el nivel aún no empieza porque falta el otro PC (o la lista del anfitrión).</summary>
        public bool IsAwaitingNetwork { get; private set; }

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
            if (LanSession.Active) LanSync.Attach(this, LanSession.Instance);

            if (showMenuOnStart && !GameSession.StartDirectly)
            {
                GameInput.Scheme = ControlScheme.Normal;
                GameSession.SelectStore(store);
                ChangeState(GameState.Menu);
                return;
            }
            if (IsLanClient)
            {
                // El cliente espera la lista y el esquema del anfitrión.
                IsAwaitingNetwork = true;
                return;
            }
            if (IsLanHost)
            {
                StartCoroutine(BeginWhenPeerReady());
                return;
            }
            if (GameSession.Mode == GameMode.Tutorial) BeginTutorial();
            else BeginLevel(GameSession.CurrentLevel);
        }

        /// <summary>Anfitrión LAN: empieza el nivel cuando el otro PC termina de cargar la escena.</summary>
        IEnumerator BeginWhenPeerReady()
        {
            IsAwaitingNetwork = true;
            while (LanSession.IsHostActive && !LanSession.Instance.PeerReady) yield return null;
            IsAwaitingNetwork = false;
            if (!LanSession.IsHostActive) yield break; // LanSync vuelve al menú al perder la conexión.
            BeginLevel(GameSession.CurrentLevel);
        }

        void Update()
        {
            if ((State == GameState.Preparacion || State == GameState.EnJuego) && GameInput.PausePressed())
            {
                if (IsLanClient) LanSync.SendRequest(LanRequest.TogglePause);
                else SetPaused(!IsPaused);
                return;
            }
            if (IsPaused) return;

            if (State == GameState.Preparacion && !IsLanClient)
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
            if (State != GameState.Menu || IsLanClient) return;
            GameSession.Begin(level, targetStore);
            if (IsLanHost)
            {
                // En LAN los dos PC cargan la escena para empezar a la vez.
                ReloadScene($"{StoreCatalog.DisplayName(targetStore)} · Nivel {GameSession.CurrentLevel}", targetStore);
                return;
            }
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
            // El tutorial es para un solo PC.
            if (State != GameState.Menu || LanSession.Active) return;
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
            if (IsLanClient)
            {
                LanSync.SendRequest(LanRequest.Restart);
                return;
            }
            if (IsTutorial) GameSession.BeginTutorial();
            else GameSession.Begin(CurrentLevel, store);
            ReloadScene(IsTutorial ? "Tutorial" : $"Reintentar · Nivel {CurrentLevel}");
        }

        /// <summary>Pasa al siguiente nivel (solo tras una compra completa).</summary>
        public void NextLevel()
        {
            if (!CanAdvance) return;
            if (IsLanClient)
            {
                LanSync.SendRequest(LanRequest.NextLevel);
                return;
            }
            GameSession.Begin(CurrentLevel + 1, store);
            ReloadScene($"{StoreCatalog.DisplayName(store)} · Nivel {CurrentLevel + 1}");
        }

        public void GoToMenu()
        {
            if (IsLanClient)
            {
                LanSync.SendRequest(LanRequest.Menu);
                return;
            }
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
            if (IsLanClient)
            {
                // La pausa es común: la decide el anfitrión.
                LanSync.SendRequest(paused ? LanRequest.TogglePause : LanRequest.Resume);
                return;
            }
            ApplyPause(paused);
        }

        void ApplyPause(bool paused)
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
            if (State != GameState.EnJuego || IsPaused || IsLanClient) return CheckoutAttempt.NotPlaying;
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

        void ReloadScene(string heading) => ReloadScene(heading, store);

        void ReloadScene(string heading, StoreId targetStore)
        {
            Time.timeScale = 1f;
            SetCursorLocked(false);
            string path = targetStore == store ? SceneManager.GetActiveScene().path : StoreCatalog.ScenePath(targetStore);
            if (IsLanHost) LanSession.Instance.HostLoad(path, targetStore, heading);
            else LoadingScreen.Load(path, targetStore, heading);
        }

        // ---------- LAN: el cliente aplica lo que decide el anfitrión ----------

        /// <summary>Cliente LAN: nivel, esquema y lista del anfitrión.</summary>
        public void ApplyRemoteLevel(LevelConfig config, IReadOnlyList<(ProductType type, int count)> items, float preparation)
        {
            IsAwaitingNetwork = false;
            IsTutorial = false;
            CurrentConfig = config;
            GameInput.Scheme = config.Scheme;
            if (timer != null) timer.SetDuration(config.Duration);
            if (shoppingList != null)
            {
                var fixedItems = new (ProductType, int)[items.Count];
                for (int i = 0; i < items.Count; i++) fixedItems[i] = items[i];
                shoppingList.SetFixed(fixedItems);
            }
            preparationRemaining = preparation;
            LevelConfigured?.Invoke(config);
        }

        /// <summary>Cliente LAN: cambio de estado decidido por el anfitrión.</summary>
        public void ApplyRemoteState(GameState state)
        {
            if (State == state || IsGameOver) return;
            bool playing = state == GameState.EnJuego;
            SetPlayersEnabled(playing && !IsPaused);
            SetCursorLocked(playing && !IsPaused && lockCursorWhilePlaying);
            ChangeState(state);
        }

        public void ApplyRemotePreparation(float remaining) => preparationRemaining = Mathf.Max(0f, remaining);

        public void ApplyRemotePause(bool paused) => ApplyPause(paused);

        /// <summary>Cliente LAN: fin de partida con el recibo calculado como en el anfitrión.</summary>
        public void ApplyRemoteEnd(GameState finalState, ShoppingReceipt receipt)
        {
            if (IsGameOver) return;
            if (IsPaused) ApplyPause(false);
            LastReceipt = receipt;
            SetPlayersEnabled(false);
            if (receipt.IsComplete) GameSession.Unlock(CurrentLevel + 1);
            EndGame(finalState);
        }

        /// <summary>Se perdió la conexión: este PC vuelve a su menú.</summary>
        public void HandleLanLost()
        {
            GameSession.ReturnToMenu();
            Time.timeScale = 1f;
            SetCursorLocked(false);
            LoadingScreen.Load(SceneManager.GetActiveScene().path, store, "Conexión perdida");
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
