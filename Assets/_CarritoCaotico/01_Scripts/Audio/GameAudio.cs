using System;
using UnityEngine;

namespace CarritoCaotico
{
    public enum UISound
    {
        Hover,
        Click,
        Back,
        Toggle
    }

    public enum GameSound
    {
        Grab,
        Drop,
        Throw,
        IntoBasket,
        RivalTook,
        EmptyCart,
        Tick,
        Countdown,
        Go,
        Pause,
        Connected,
        Disconnected,
        Hit,
        Explosion
    }

    /// <summary>Qué suena: todo, solo efectos o nada. Se guarda entre partidas.</summary>
    public enum AudioMode
    {
        Todo = 0,
        SoloEfectos = 1,
        Silencio = 2
    }

    /// <summary>
    /// Música y efectos de la escena. La música es la del supermercado (sigue sonando donde iba
    /// al reintentar o pasar de nivel), baja en pausa y en el recibo, y el final suena con un
    /// jingle de victoria o derrota. Los efectos salen de los eventos del juego, así funcionan
    /// igual en un PC y en los dos PC de una partida LAN.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const string ModeKey = "CarritoCaotico.Sonido";

        [Header("Sistemas")]
        [SerializeField] GameManager gameManager;
        [SerializeField] GrabberArm grabber;
        [SerializeField] ShoppingList shoppingList;
        [SerializeField] GameTimer timer;
        [SerializeField] RivalManager rivals;
        [SerializeField] Checkout checkout;
        [SerializeField] CartHealth cartHealth;

        [Header("Música")]
        [SerializeField] AudioClip music;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.45f;
        [Tooltip("Volumen relativo de la música en pausa y en el recibo.")]
        [SerializeField, Range(0f, 1f)] float duckedVolume = 0.35f;
        [SerializeField] AudioClip winJingle;
        [SerializeField] AudioClip loseJingle;

        [Header("Interfaz")]
        [SerializeField] AudioClip hover;
        [SerializeField] AudioClip click;
        [SerializeField] AudioClip back;
        [SerializeField] AudioClip toggle;

        [Header("Partida")]
        [SerializeField] AudioClip grab;
        [SerializeField] AudioClip drop;
        [SerializeField] AudioClip throwClip;
        [SerializeField] AudioClip intoBasket;
        [SerializeField] AudioClip rivalTook;
        [SerializeField] AudioClip emptyCart;
        [SerializeField] AudioClip tick;
        [SerializeField] AudioClip countdown;
        [SerializeField] AudioClip go;
        [SerializeField] AudioClip pause;
        [SerializeField] AudioClip connected;
        [SerializeField] AudioClip disconnected;
        [SerializeField, Range(0f, 1f)] float effectsVolume = 0.8f;

        static GameAudio instance;
        static AudioClip resumeClip;
        static float resumeTime;
        static bool modeLoaded;
        static AudioMode mode;

        AudioSource musicSource;
        AudioSource effectsSource;
        float musicTarget = 1f;
        int lastCartTotal;
        int lastPreparationSecond = -1;
        bool pendingDrop;
        bool pendingThrow;

        public static GameAudio Instance => instance;
        public AudioSource MusicSource => musicSource;
        public AudioClip Music => music;
        readonly System.Collections.Generic.List<string> history = new System.Collections.Generic.List<string>();

        /// <summary>Último efecto pedido (pruebas y depuración).</summary>
        public string LastPlayed { get; private set; } = "";
        public int PlayedCount { get; private set; }
        /// <summary>Últimos efectos pedidos, del más antiguo al más reciente (pruebas y depuración).</summary>
        public System.Collections.Generic.IReadOnlyList<string> History => history;

        public void ClearHistory() => history.Clear();

        void Record(string name)
        {
            LastPlayed = name;
            PlayedCount++;
            history.Add(name);
            if (history.Count > 32) history.RemoveAt(0);
        }

        /// <summary>Se dispara al cambiar el modo de sonido.</summary>
        public static event Action<AudioMode> ModeChanged;

        public static AudioMode Mode
        {
            get
            {
                if (!modeLoaded)
                {
                    mode = (AudioMode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 2);
                    modeLoaded = true;
                }
                return mode;
            }
            set
            {
                mode = value;
                modeLoaded = true;
                PlayerPrefs.SetInt(ModeKey, (int)value);
                PlayerPrefs.Save();
                if (instance != null) instance.ApplyMode();
                ModeChanged?.Invoke(value);
            }
        }

        public static string ModeName(AudioMode value) =>
            value == AudioMode.Todo ? "Sonido: todo"
            : value == AudioMode.SoloEfectos ? "Sonido: efectos"
            : "Sonido: no";

        public static bool MusicOn => Mode == AudioMode.Todo;
        public static bool EffectsOn => Mode != AudioMode.Silencio;

        // ---------- Ciclo de vida ----------

        void Awake()
        {
            instance = this;
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.ignoreListenerPause = true;
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            // Los efectos de la interfaz suenan aunque la partida esté en pausa.
            effectsSource.ignoreListenerPause = true;
        }

        void OnEnable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged += OnStateChanged;
                gameManager.GameEnded += OnGameEnded;
                gameManager.PauseChanged += OnPauseChanged;
            }
            if (grabber != null)
            {
                grabber.HeldProductChanged += OnHeldChanged;
                grabber.ProductThrown += OnThrown;
            }
            if (shoppingList != null) shoppingList.Changed += OnListChanged;
            if (timer != null) timer.SecondsChanged += OnSecondsChanged;
            if (rivals != null) rivals.ProductTaken += OnRivalTook;
            if (checkout != null) checkout.EmptyCartRejected += OnEmptyCart;
            if (cartHealth != null)
            {
                cartHealth.Damaged += OnCartDamaged;
                cartHealth.Exploded += OnCartExploded;
            }
            LanSession.AnyStatusChanged += OnLanStatus;
        }

        void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= OnStateChanged;
                gameManager.GameEnded -= OnGameEnded;
                gameManager.PauseChanged -= OnPauseChanged;
            }
            if (grabber != null)
            {
                grabber.HeldProductChanged -= OnHeldChanged;
                grabber.ProductThrown -= OnThrown;
            }
            if (shoppingList != null) shoppingList.Changed -= OnListChanged;
            if (timer != null) timer.SecondsChanged -= OnSecondsChanged;
            if (rivals != null) rivals.ProductTaken -= OnRivalTook;
            if (checkout != null) checkout.EmptyCartRejected -= OnEmptyCart;
            if (cartHealth != null)
            {
                cartHealth.Damaged -= OnCartDamaged;
                cartHealth.Exploded -= OnCartExploded;
            }
            LanSession.AnyStatusChanged -= OnLanStatus;
        }

        void Start()
        {
            lastCartTotal = CartTotal();
            if (music != null)
            {
                musicSource.clip = music;
                musicSource.Play();
                // Al reintentar o pasar de nivel la música sigue donde iba (con streaming, después de Play).
                if (resumeClip == music && resumeTime > 0f) musicSource.time = Mathf.Repeat(resumeTime, music.length);
            }
            ApplyMode();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Update()
        {
            // Se guarda cada frame: la escena nueva puede arrancar antes de que se destruya esta.
            if (instance == this && musicSource.clip != null && musicSource.isPlaying)
            {
                resumeClip = musicSource.clip;
                resumeTime = musicSource.time;
            }

            float target = MusicOn ? musicVolume * musicTarget : 0f;
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, Time.unscaledDeltaTime * 1.5f);

            // Cuenta atrás de la preparación: un pitido por segundo.
            if (gameManager != null && gameManager.State == GameState.Preparacion && !gameManager.IsPaused)
            {
                int second = Mathf.CeilToInt(gameManager.PreparationRemaining);
                if (second != lastPreparationSecond && second > 0)
                {
                    if (lastPreparationSecond >= 0) Play(GameSound.Countdown);
                    lastPreparationSecond = second;
                }
            }
        }

        void LateUpdate()
        {
            // Lanzar también suelta el producto: en ese caso solo suena el lanzamiento.
            if (pendingThrow) Play(GameSound.Throw);
            else if (pendingDrop) Play(GameSound.Drop);
            pendingThrow = pendingDrop = false;
        }

        void ApplyMode()
        {
            if (musicSource != null && !MusicOn) musicSource.volume = 0f;
        }

        // ---------- API ----------

        public static void PlayUI(UISound sound)
        {
            if (instance == null) return;
            switch (sound)
            {
                case UISound.Hover: instance.PlayClip(instance.hover, "Hover", 0.45f); break;
                case UISound.Click: instance.PlayClip(instance.click, "Click", 0.8f); break;
                case UISound.Back: instance.PlayClip(instance.back, "Back", 0.8f); break;
                case UISound.Toggle: instance.PlayClip(instance.toggle, "Toggle", 0.8f); break;
            }
        }

        public static void Play(GameSound sound)
        {
            if (instance == null) return;
            GameAudio a = instance;
            switch (sound)
            {
                case GameSound.Grab: a.PlayClip(a.grab, "Grab", 0.9f); break;
                case GameSound.Drop: a.PlayClip(a.drop, "Drop", 0.7f); break;
                case GameSound.Throw: a.PlayClip(a.throwClip, "Throw", 0.9f); break;
                case GameSound.IntoBasket: a.PlayClip(a.intoBasket, "IntoBasket", 0.9f); break;
                case GameSound.RivalTook: a.PlayClip(a.rivalTook, "RivalTook", 0.8f); break;
                case GameSound.EmptyCart: a.PlayClip(a.emptyCart, "EmptyCart", 0.8f); break;
                case GameSound.Tick: a.PlayClip(a.tick, "Tick", 0.6f); break;
                case GameSound.Countdown: a.PlayClip(a.countdown, "Countdown", 0.8f); break;
                case GameSound.Go: a.PlayClip(a.go, "Go", 0.9f); break;
                case GameSound.Pause: a.PlayClip(a.pause, "Pause", 0.7f); break;
                case GameSound.Connected: a.PlayClip(a.connected, "Connected", 0.9f); break;
                case GameSound.Disconnected: a.PlayClip(a.disconnected, "Disconnected", 0.9f); break;
                case GameSound.Hit: a.PlayClip(ProceduralSounds.Hit(), "Hit", 0.9f); break;
                case GameSound.Explosion: a.PlayClip(ProceduralSounds.Explosion(), "Explosion", 1f); break;
            }
        }

        void PlayClip(AudioClip clip, string name, float volume)
        {
            Record(name);
            if (clip == null || !EffectsOn) return;
            effectsSource.PlayOneShot(clip, volume * effectsVolume);
        }

        /// <summary>Cambia al siguiente modo: todo → solo efectos → silencio.</summary>
        public static AudioMode CycleMode()
        {
            Mode = (AudioMode)(((int)Mode + 1) % 3);
            return Mode;
        }

        // ---------- Eventos del juego ----------

        void OnStateChanged(GameState state)
        {
            if (state == GameState.EnJuego)
            {
                musicTarget = 1f;
                if (gameManager == null || !gameManager.IsTutorial) Play(GameSound.Go);
            }
            else if (state == GameState.Preparacion)
            {
                musicTarget = 1f;
                lastPreparationSecond = -1;
            }
            else if (state == GameState.Menu)
            {
                musicTarget = 1f;
            }
        }

        void OnGameEnded(ShoppingReceipt receipt)
        {
            musicTarget = duckedVolume;
            bool won = receipt != null && receipt.IsComplete;
            Record(won ? "Win" : "Lose");
            AudioClip jingle = won ? winJingle : loseJingle;
            if (jingle != null && EffectsOn) effectsSource.PlayOneShot(jingle, effectsVolume);
        }

        void OnPauseChanged(bool paused)
        {
            musicTarget = paused ? duckedVolume : 1f;
            Play(GameSound.Pause);
        }

        void OnHeldChanged(Product product)
        {
            if (product != null) Play(GameSound.Grab);
            else pendingDrop = true;
        }

        void OnThrown(Product product) => pendingThrow = true;

        void OnListChanged()
        {
            int total = CartTotal();
            if (total > lastCartTotal && gameManager != null && gameManager.State == GameState.EnJuego) Play(GameSound.IntoBasket);
            lastCartTotal = total;
        }

        int CartTotal()
        {
            if (shoppingList == null) return 0;
            int total = 0;
            foreach (ProductType type in ProductCatalog.AllTypes) total += shoppingList.GetInCart(type);
            return total;
        }

        void OnSecondsChanged(int seconds)
        {
            if (timer != null && timer.IsRunning && seconds > 0 && seconds <= 10) Play(GameSound.Tick);
        }

        void OnRivalTook(Product product) => Play(GameSound.RivalTook);

        void OnEmptyCart() => Play(GameSound.EmptyCart);

        void OnCartDamaged(float amount)
        {
            if (cartHealth == null || !cartHealth.IsDestroyed) Play(GameSound.Hit);
        }

        void OnCartExploded() => Play(GameSound.Explosion);

        void OnLanStatus()
        {
            LanSession session = LanSession.Instance;
            if (session != null && session.Status == LanSession.SessionStatus.Conectada) Play(GameSound.Connected);
            else if (session == null && !string.IsNullOrEmpty(LanSession.LastNotice)) Play(GameSound.Disconnected);
        }
    }
}
