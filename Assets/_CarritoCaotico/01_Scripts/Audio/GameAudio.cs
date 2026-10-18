using System;
using System.Collections.Generic;
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
        Explosion,
        Connected,
        Disconnected
    }

    /// <summary>Qué suena: todo, solo efectos o nada. Se guarda entre partidas.</summary>
    public enum AudioMode
    {
        Todo = 0,
        SoloEfectos = 1,
        Silencio = 2
    }

    /// <summary>
    /// Sonidos que completan al <see cref="AudioDirector"/> (que lleva la música, los ambientes y
    /// los efectos de la partida): el paso del mouse por los botones, la explosión del carrito y la
    /// conexión LAN. También guarda el modo de sonido del menú, que respetan los dos.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const string ModeKey = "CarritoCaotico.Sonido";

        [SerializeField] CartHealth cartHealth;

        [Header("Clips (Kenney, CC0)")]
        [SerializeField] AudioClip hover;
        [SerializeField] AudioClip connected;
        [SerializeField] AudioClip disconnected;
        [SerializeField, Range(0f, 1f)] float volume = 0.8f;

        static GameAudio instance;
        static bool modeLoaded;
        static AudioMode mode;

        AudioSource source;
        readonly List<string> history = new List<string>();

        public static GameAudio Instance => instance;
        /// <summary>Último efecto pedido (pruebas y depuración).</summary>
        public string LastPlayed { get; private set; } = "";
        public int PlayedCount { get; private set; }
        /// <summary>Últimos efectos pedidos, del más antiguo al más reciente.</summary>
        public IReadOnlyList<string> History => history;

        public void ClearHistory() => history.Clear();

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
                ModeChanged?.Invoke(value);
            }
        }

        public static string ModeName(AudioMode value) =>
            value == AudioMode.Todo ? "Sonido: todo"
            : value == AudioMode.SoloEfectos ? "Sonido: efectos"
            : "Sonido: no";

        public static bool MusicOn => Mode == AudioMode.Todo;
        public static bool EffectsOn => Mode != AudioMode.Silencio;

        /// <summary>Cambia al siguiente modo: todo → solo efectos → silencio.</summary>
        public static AudioMode CycleMode()
        {
            Mode = (AudioMode)(((int)Mode + 1) % 3);
            return Mode;
        }

        void Awake()
        {
            instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            // La interfaz suena aunque la partida esté en pausa.
            source.ignoreListenerPause = true;
        }

        void OnEnable()
        {
            if (cartHealth != null) cartHealth.Exploded += OnCartExploded;
            LanSession.AnyStatusChanged += OnLanStatus;
        }

        void OnDisable()
        {
            if (cartHealth != null) cartHealth.Exploded -= OnCartExploded;
            LanSession.AnyStatusChanged -= OnLanStatus;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Sonidos de interfaz. Pulsar y navegar los toca el AudioDirector; aquí, el paso del mouse.</summary>
        public static void PlayUI(UISound sound)
        {
            if (instance == null) return;
            if (sound == UISound.Hover) instance.PlayClip(instance.hover, "Hover", 0.45f);
            else instance.Record(sound.ToString());
        }

        public static void Play(GameSound sound)
        {
            if (instance == null) return;
            switch (sound)
            {
                case GameSound.Explosion: instance.PlayClip(ProceduralSounds.Explosion(), "Explosion", 1f); break;
                case GameSound.Connected: instance.PlayClip(instance.connected, "Connected", 0.9f); break;
                case GameSound.Disconnected: instance.PlayClip(instance.disconnected, "Disconnected", 0.9f); break;
            }
        }

        void Record(string name)
        {
            LastPlayed = name;
            PlayedCount++;
            history.Add(name);
            if (history.Count > 32) history.RemoveAt(0);
        }

        void PlayClip(AudioClip clip, string name, float scale)
        {
            Record(name);
            if (clip == null || !EffectsOn || source == null) return;
            source.PlayOneShot(clip, scale * volume);
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
