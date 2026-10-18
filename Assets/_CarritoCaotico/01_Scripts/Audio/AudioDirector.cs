using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>
    /// Sonido del juego: ambientación y música según el supermercado (día o noche) y el estado
    /// de la partida, y efectos para el carrito, el brazo, la lista, la caja, el reloj y el menú.
    /// Solo escucha eventos de los sistemas; si falta un clip en la biblioteca, no suena.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        [SerializeField] SoundLibrary library;
        [SerializeField] GameManager gameManager;
        [SerializeField] GameTimer timer;
        [SerializeField] ShoppingList shoppingList;
        [SerializeField] CartContents cartContents;
        [SerializeField] CartController cart;
        [SerializeField] GrabberArm grabber;
        [SerializeField] Checkout checkout;
        [SerializeField] RivalManager rivals;

        [Header("Volúmenes")]
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] float ambienceVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] float effectsVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] float wheelsVolume = 0.5f;

        [Header("Reloj")]
        [SerializeField, Min(0)] int tickFromSeconds = 10;
        [SerializeField, Min(0)] int urgencySeconds = 30;

        AudioSource music;
        AudioSource ambience;
        AudioSource effects;
        AudioSource interfaceEffects;
        AudioSource wheels;

        int collected;
        int cartCount;
        bool releasePending;
        bool thrownThisFrame;
        bool wasDrifting;
        bool wasSlipping;
        GameObject lastSelected;
        CharacterId lastPusher;
        CharacterId lastGrabber;

        bool IsNight => gameManager != null && gameManager.Store == StoreId.Halloween;
        bool Playing => gameManager != null && gameManager.State == GameState.EnJuego;

        void Awake()
        {
            music = CreateSource("Musica", true, musicVolume);
            ambience = CreateSource("Ambiente", true, ambienceVolume);
            effects = CreateSource("Efectos", false, effectsVolume);
            interfaceEffects = CreateSource("Interfaz", false, effectsVolume);
            interfaceEffects.ignoreListenerPause = true;
            wheels = CreateSource("Ruedas", true, 0f);
        }

        AudioSource CreateSource(string sourceName, bool loop, float volume)
        {
            var child = new GameObject("Audio_" + sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0f;
            return source;
        }

        void OnEnable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged += OnStateChanged;
                gameManager.PauseChanged += OnPauseChanged;
                gameManager.GameEnded += OnGameEnded;
                gameManager.LevelConfigured += OnLevelConfigured;
            }
            if (timer != null) timer.SecondsChanged += OnSecondsChanged;
            if (shoppingList != null) shoppingList.Changed += OnListChanged;
            if (cartContents != null) cartContents.Changed += OnCartChanged;
            if (cart != null) cart.Bumped += OnBumped;
            if (grabber != null)
            {
                grabber.HeldProductChanged += OnHeldChanged;
                grabber.ProductThrown += OnThrown;
            }
            if (checkout != null) checkout.EmptyCartRejected += OnEmptyCart;
            if (rivals != null) rivals.ProductTaken += OnRivalTookProduct;
            CharacterChoice.Changed += OnCharacterChanged;
            GameInput.DevicesChanged += OnDevicesChanged;
        }

        void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= OnStateChanged;
                gameManager.PauseChanged -= OnPauseChanged;
                gameManager.GameEnded -= OnGameEnded;
                gameManager.LevelConfigured -= OnLevelConfigured;
            }
            if (timer != null) timer.SecondsChanged -= OnSecondsChanged;
            if (shoppingList != null) shoppingList.Changed -= OnListChanged;
            if (cartContents != null) cartContents.Changed -= OnCartChanged;
            if (cart != null) cart.Bumped -= OnBumped;
            if (grabber != null)
            {
                grabber.HeldProductChanged -= OnHeldChanged;
                grabber.ProductThrown -= OnThrown;
            }
            if (checkout != null) checkout.EmptyCartRejected -= OnEmptyCart;
            if (rivals != null) rivals.ProductTaken -= OnRivalTookProduct;
            CharacterChoice.Changed -= OnCharacterChanged;
            GameInput.DevicesChanged -= OnDevicesChanged;
            AudioListener.pause = false;
        }

        void Start()
        {
            lastPusher = CharacterChoice.Pusher;
            lastGrabber = CharacterChoice.Grabber;
            if (shoppingList != null) collected = shoppingList.TotalCollected;
            if (cartContents != null) cartCount = cartContents.Count;

            // Todos los botones (menú, pausa, recibo, tutorial) suenan al pulsarlos.
            foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                button.onClick.AddListener(() => PlayInterface(library != null ? library.uiConfirmar : null));

            if (gameManager != null) OnStateChanged(gameManager.State);
        }

        void Update()
        {
            UpdateWheels();
            UpdateMenuNavigation();
        }

        void FixedUpdate()
        {
            if (cart == null || library == null) return;
            // IsSlipping solo es cierto durante el paso de física en que pisa el charco.
            if (cart.IsSlipping && !wasSlipping && Playing)
                Play(IsNight ? library.sfxCharcoBaba : library.sfxCharcoAgua);
            wasSlipping = cart.IsSlipping;
        }

        void LateUpdate()
        {
            // Lanzar también suelta el producto: solo suena "soltar" si no hubo lanzamiento.
            if (releasePending && !thrownThisFrame && library != null) Play(library.sfxSoltar);
            releasePending = false;
            thrownThisFrame = false;
        }

        // ---------- Estados ----------

        void OnStateChanged(GameState state)
        {
            if (library == null) return;
            switch (state)
            {
                case GameState.Menu:
                    SetLoop(music, library.musMenu);
                    SetLoop(ambience, IsNight ? library.ambNoche : library.ambDia);
                    break;
                case GameState.Preparacion:
                    SetLoop(music, null);
                    SetLoop(ambience, IsNight ? library.ambNoche : library.ambDia);
                    Play(library.sfxCuentaAtras);
                    break;
                case GameState.EnJuego:
                    if (shoppingList != null) collected = shoppingList.TotalCollected;
                    if (cartContents != null) cartCount = cartContents.Count;
                    SetLoop(music, IsNight ? library.musNoche : library.musDia);
                    SetLoop(ambience, IsNight ? library.ambNoche : library.ambDia);
                    break;
                case GameState.TiempoAgotado:
                case GameState.CompraEntregada:
                    SetLoop(music, null);
                    break;
            }
        }

        void OnPauseChanged(bool paused)
        {
            AudioListener.pause = paused;
            if (library != null) PlayInterface(library.uiPausa);
        }

        void OnLevelConfigured(LevelConfig config)
        {
            if (library != null && config.Scheme != ControlScheme.Normal) Play(library.sfxControlesCambiados);
        }

        void OnGameEnded(ShoppingReceipt receipt)
        {
            if (library == null || receipt == null) return;
            if (receipt.TimedOut)
            {
                Play(IsNight ? library.sfxTiempoNoche : library.sfxTiempoDia);
                return;
            }
            Play(library.sfxCajaPago);
            StartCoroutine(PlayAfter(receipt.IsComplete ? library.sfxVictoria : library.sfxDerrota, 1.2f));
        }

        IEnumerator PlayAfter(AudioClip clip, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Play(clip);
        }

        // ---------- Reloj ----------

        void OnSecondsChanged(int seconds)
        {
            if (library == null || !Playing || gameManager.IsTutorial) return;
            if (seconds > 0 && seconds <= tickFromSeconds) Play(library.sfxTic);
            if (seconds == urgencySeconds)
            {
                Play(library.sfxAviso30s);
                if (library.musUrgencia != null) SetLoop(music, library.musUrgencia);
            }
        }

        // ---------- Lista, canasta y caja ----------

        void OnListChanged()
        {
            int now = shoppingList.TotalCollected;
            if (now > collected && Playing && library != null) Play(library.sfxListaOk);
            collected = now;
        }

        void OnCartChanged()
        {
            int now = cartContents.Count;
            if (now > cartCount && Playing && library != null) Play(library.sfxCanasta);
            cartCount = now;
        }

        void OnEmptyCart()
        {
            if (library != null) Play(library.sfxCajaVacia);
        }

        void OnRivalTookProduct(Product product)
        {
            if (library != null && Playing) Play(IsNight ? library.sfxRivalNoche : library.sfxRivalDia);
        }

        // ---------- Brazo ----------

        void OnHeldChanged(Product product)
        {
            if (library == null) return;
            if (product != null) Play(library.sfxAgarrar);
            else releasePending = true;
        }

        void OnThrown(Product product)
        {
            thrownThisFrame = true;
            if (library != null) Play(library.sfxLanzar);
        }

        // ---------- Carrito ----------

        void OnBumped(float speed)
        {
            if (library == null || !Playing) return;
            Play(library.sfxChoque, Mathf.Clamp01(speed / 5f));
        }

        void UpdateWheels()
        {
            if (cart == null || library == null || library.sfxRuedasLoop == null)
            {
                if (wheels != null && wheels.isPlaying) wheels.Stop();
                return;
            }

            float speed = Mathf.Abs(cart.ForwardSpeed);
            float t = Playing ? Mathf.Clamp01(speed / Mathf.Max(0.1f, cart.MaxSpeed)) : 0f;
            if (wheels.clip != library.sfxRuedasLoop) wheels.clip = library.sfxRuedasLoop;
            wheels.volume = wheelsVolume * t;
            wheels.pitch = 0.85f + 0.35f * t;
            if (t > 0.02f && !wheels.isPlaying) wheels.Play();
            else if (t <= 0.02f && wheels.isPlaying) wheels.Stop();

            bool drifting = cart.IsDrifting && speed > 1.5f && Playing;
            if (drifting && !wasDrifting) Play(library.sfxDerrape);
            wasDrifting = drifting;
        }

        // ---------- Menú y otros ----------

        void UpdateMenuNavigation()
        {
            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            if (selected != lastSelected && selected != null && lastSelected != null && library != null)
                PlayInterface(library.uiNavegar);
            lastSelected = selected;
        }

        void OnCharacterChanged()
        {
            if (library == null) return;
            if (CharacterChoice.Pusher != lastPusher) PlayInterface(library.Voice(CharacterChoice.Pusher));
            else if (CharacterChoice.Grabber != lastGrabber) PlayInterface(library.Voice(CharacterChoice.Grabber));
            lastPusher = CharacterChoice.Pusher;
            lastGrabber = CharacterChoice.Grabber;
        }

        void OnDevicesChanged()
        {
            if (library != null) PlayInterface(library.sfxMando);
        }

        // ---------- Reproducción ----------

        void Play(AudioClip clip, float volumeScale = 1f)
        {
            if (clip != null && effects != null) effects.PlayOneShot(clip, volumeScale);
        }

        void PlayInterface(AudioClip clip)
        {
            if (clip != null && interfaceEffects != null) interfaceEffects.PlayOneShot(clip);
        }

        static void SetLoop(AudioSource source, AudioClip clip)
        {
            if (source == null) return;
            if (clip == null)
            {
                source.Stop();
                source.clip = null;
                return;
            }
            if (source.clip == clip && source.isPlaying) return;
            source.clip = clip;
            source.Play();
        }
    }
}
