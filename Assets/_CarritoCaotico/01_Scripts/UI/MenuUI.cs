using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>
    /// Menú principal (tienda, nivel desbloqueado, personajes, mando, jugar, tutorial, LAN, salir),
    /// menú de pausa y botón de pausa. Se maneja con mouse o teclado (flechas + Enter) gracias al
    /// EventSystem. El panel LAN crea una partida o se une a la de otro PC por su IP.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        const string LanRoleKey = "CarritoCaotico.RolLan";
        const string LanAddressKey = "CarritoCaotico.IpLan";

        [SerializeField] GameManager gameManager;

        [Header("Menú principal")]
        [SerializeField] GameObject menuPanel;
        [SerializeField] Button playButton;
        [SerializeField] Button tutorialButton;
        [SerializeField] Button previousStoreButton;
        [SerializeField] Button nextStoreButton;
        [SerializeField] Text storeText;
        [SerializeField] Text storeDetailText;
        [SerializeField] Button previousLevelButton;
        [SerializeField] Button nextLevelButton;
        [SerializeField] Text levelText;
        [SerializeField] Text levelDetailText;
        [SerializeField] Button previousPusherButton;
        [SerializeField] Button nextPusherButton;
        [SerializeField] Text pusherText;
        [SerializeField] Button previousGrabberButton;
        [SerializeField] Button nextGrabberButton;
        [SerializeField] Text grabberText;
        [SerializeField] Button previousPadButton;
        [SerializeField] Button nextPadButton;
        [SerializeField] Text padText;
        [SerializeField] Text padDetailText;
        [SerializeField] Button quitButton;
        [Tooltip("Texto de roles bajo los botones: en LAN explica quién es quién.")]
        [SerializeField] Text rolesText;

        [Header("LAN")]
        [SerializeField] Button lanButton;
        [SerializeField] Text lanButtonText;
        [SerializeField] GameObject lanPanel;
        [Tooltip("Selector de rol, crear partida y unirse: solo sin conexión.")]
        [SerializeField] GameObject lanSetupGroup;
        [SerializeField] Button lanPreviousRoleButton;
        [SerializeField] Button lanNextRoleButton;
        [SerializeField] Text lanRoleText;
        [SerializeField] Text lanLocalAddressText;
        [SerializeField] Button lanHostButton;
        [SerializeField] InputField lanAddressField;
        [SerializeField] Button lanJoinButton;
        [SerializeField] Text lanStatusText;
        [SerializeField] Button lanCancelButton;
        [SerializeField] Text lanCancelText;
        [SerializeField] Button lanBackButton;

        [Header("Pausa")]
        [SerializeField] GameObject pausePanel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button pauseRestartButton;
        [SerializeField] Button pauseMenuButton;

        int selectedLevel = 1;
        int storeIndex;
        string defaultRolesText;
        LanSession.SessionStatus shownLanStatus;

        public int SelectedLevel => selectedLevel;
        public StoreId SelectedStore => StoreCatalog.All[storeIndex];
        public bool IsLanPanelOpen => lanPanel != null && lanPanel.activeSelf;

        /// <summary>Rol del anfitrión al crear una partida LAN (se guarda entre partidas).</summary>
        public static NetRole LanHostRole
        {
            get => PlayerPrefs.GetInt(LanRoleKey, (int)NetRole.Empujador) == (int)NetRole.Agarrador ? NetRole.Agarrador : NetRole.Empujador;
            set
            {
                PlayerPrefs.SetInt(LanRoleKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        void Awake()
        {
            // El GameManager puede cambiar de estado (y refrescar la LAN) antes de nuestro Start.
            if (rolesText != null) defaultRolesText = rolesText.text;
        }

        void OnEnable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged += OnStateChanged;
                gameManager.PauseChanged += OnPauseChanged;
            }
            Listen(playButton, OnPlay);
            Listen(tutorialButton, () => { if (gameManager != null) gameManager.StartTutorial(); });
            Listen(previousStoreButton, () => ChangeStore(-1));
            Listen(nextStoreButton, () => ChangeStore(1));
            Listen(previousLevelButton, () => ChangeLevel(-1));
            Listen(nextLevelButton, () => ChangeLevel(1));
            Listen(previousPusherButton, () => CharacterChoice.Pusher = CharacterChoice.Cycle(CharacterChoice.Pusher, -1));
            Listen(nextPusherButton, () => CharacterChoice.Pusher = CharacterChoice.Cycle(CharacterChoice.Pusher, 1));
            Listen(previousGrabberButton, () => CharacterChoice.Grabber = CharacterChoice.Cycle(CharacterChoice.Grabber, -1));
            Listen(nextGrabberButton, () => CharacterChoice.Grabber = CharacterChoice.Cycle(CharacterChoice.Grabber, 1));
            CharacterChoice.Changed += RefreshCharacters;
            Listen(previousPadButton, () => ChangePad(-1));
            Listen(nextPadButton, () => ChangePad(1));
            GameInput.EnsureDeviceHook();
            GameInput.DevicesChanged += RefreshPad;
            Listen(quitButton, () => { if (gameManager != null) gameManager.QuitGame(); });
            Listen(resumeButton, () => { if (gameManager != null) gameManager.SetPaused(false); });
            Listen(pauseRestartButton, () => { if (gameManager != null) gameManager.Restart(); });
            Listen(pauseMenuButton, () => { if (gameManager != null) gameManager.GoToMenu(); });

            Listen(lanButton, OpenLan);
            Listen(lanPreviousRoleButton, ToggleLanRole);
            Listen(lanNextRoleButton, ToggleLanRole);
            Listen(lanHostButton, HostLan);
            Listen(lanJoinButton, JoinLan);
            Listen(lanCancelButton, () => LanSession.Shutdown("Saliste de la partida LAN."));
            Listen(lanBackButton, CloseLan);
            LanSession.AnyStatusChanged += RefreshLan;
        }

        void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= OnStateChanged;
                gameManager.PauseChanged -= OnPauseChanged;
            }
            GameInput.DevicesChanged -= RefreshPad;
            CharacterChoice.Changed -= RefreshCharacters;
            LanSession.AnyStatusChanged -= RefreshLan;
            foreach (Button b in new[] { playButton, tutorialButton, previousStoreButton, nextStoreButton, previousLevelButton, nextLevelButton,
                         previousPusherButton, nextPusherButton, previousGrabberButton, nextGrabberButton, previousPadButton, nextPadButton,
                         quitButton, resumeButton, pauseRestartButton, pauseMenuButton,
                         lanButton, lanPreviousRoleButton, lanNextRoleButton, lanHostButton, lanJoinButton, lanCancelButton, lanBackButton })
                if (b != null) b.onClick.RemoveAllListeners();
        }

        void Start()
        {
            selectedLevel = GameSession.MaxUnlockedLevel;
            StoreId current = gameManager != null ? gameManager.Store : StoreId.Clasico;
            storeIndex = System.Array.IndexOf(StoreCatalog.All, current);
            if (storeIndex < 0) storeIndex = 0;
            if (lanAddressField != null) lanAddressField.text = PlayerPrefs.GetString(LanAddressKey, "");
            if (lanPanel != null) lanPanel.SetActive(false);
            shownLanStatus = LanSession.Instance != null ? LanSession.Instance.Status : LanSession.SessionStatus.Inactiva;
            RefreshStore();
            RefreshLevel();
            RefreshCharacters();
            RefreshPad();
            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameManager != null) OnStateChanged(gameManager.State);
            RefreshLan();
        }

        void Update()
        {
            if (IsLanPanelOpen && lanBackButton != null && lanBackButton.gameObject.activeSelf && GameInput.BackPressed()) CloseLan();
        }

        static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        void OnStateChanged(GameState state)
        {
            // En LAN el nivel puede tardar en empezar: no se enseña el menú mientras tanto.
            bool menu = state == GameState.Menu && (gameManager == null || !gameManager.IsAwaitingNetwork);
            if (menuPanel != null) menuPanel.SetActive(menu);
            if (!menu && lanPanel != null) lanPanel.SetActive(false);
            if (menu) Select(GameSession.TutorialDone || LanSession.Active ? playButton : tutorialButton);
            if (menu) RefreshLan();
        }

        void OnPauseChanged(bool paused)
        {
            if (pausePanel != null) pausePanel.SetActive(paused);
            if (paused) Select(resumeButton);
        }

        void OnPlay()
        {
            if (gameManager != null) gameManager.StartGame(selectedLevel, SelectedStore);
        }

        void ChangeStore(int delta)
        {
            int count = StoreCatalog.All.Length;
            storeIndex = (storeIndex + delta + count) % count;
            GameSession.SelectStore(SelectedStore);
            RefreshStore();
        }

        void ChangeLevel(int delta)
        {
            selectedLevel = Mathf.Clamp(selectedLevel + delta, 1, GameSession.MaxUnlockedLevel);
            RefreshLevel();
        }

        void ChangePad(int delta)
        {
            const int count = 3;
            GameInput.PadMode = (PadAssignment)(((int)GameInput.PadMode + delta + count) % count);
            RefreshPad();
        }

        void RefreshPad()
        {
            if (padText != null)
            {
                padText.text = GameInput.PadMode == PadAssignment.Agarrador ? "Mando: Agarrador"
                    : GameInput.PadMode == PadAssignment.Empujador ? "Mando: Empujador"
                    : "Mando: automático";
            }
            if (padDetailText != null)
            {
                string connected = GameInput.GamepadCount == 0
                    ? "Sin mando conectado"
                    : GameInput.GamepadCount == 1 ? $"Conectado: {GameInput.GamepadName}" : $"{GameInput.GamepadCount} mandos conectados";
                padDetailText.text = $"{connected}  ·  {GameInput.DevicesSummary()}";
            }
        }

        void RefreshCharacters()
        {
            if (pusherText != null) pusherText.text = CharacterChoice.DisplayName(CharacterChoice.Pusher);
            if (grabberText != null) grabberText.text = CharacterChoice.DisplayName(CharacterChoice.Grabber);
        }

        void RefreshStore()
        {
            if (storeText != null) storeText.text = StoreCatalog.DisplayName(SelectedStore);
            if (storeDetailText != null) storeDetailText.text = StoreCatalog.Description(SelectedStore);
        }

        void RefreshLevel()
        {
            int max = GameSession.MaxUnlockedLevel;
            if (levelText != null) levelText.text = $"Nivel {selectedLevel}";
            if (previousLevelButton != null) previousLevelButton.interactable = selectedLevel > 1;
            if (nextLevelButton != null) nextLevelButton.interactable = selectedLevel < max;

            if (levelDetailText == null) return;
            LevelConfig config = LevelProgression.Get(selectedLevel, GameSession.SessionSeed);
            string rivals = config.RivalCount == 0 ? "sin rivales" : config.RivalCount == 1 ? "1 rival" : $"{config.RivalCount} rivales";
            string controls = selectedLevel >= LevelProgression.ControlSwapStartLevel ? "los controles pueden cambiar" : "controles normales";
            levelDetailText.text = $"Lista de {config.MinUnits} a {config.MaxUnits} unidades  ·  {GameTimer.Format(config.Duration)}  ·  {rivals}  ·  {controls}";
        }

        // ---------- LAN ----------

        void OpenLan()
        {
            if (lanPanel == null) return;
            lanPanel.SetActive(true);
            RefreshLan();
            LanSession session = LanSession.Instance;
            Select(session == null ? lanHostButton : session.Status == LanSession.SessionStatus.Conectada && session.IsHost ? lanBackButton : lanCancelButton);
        }

        void CloseLan()
        {
            LanSession session = LanSession.Instance;
            // Volver mientras se espera cancela la partida: no se puede aceptar a nadie fuera del menú.
            if (session != null && session.Status != LanSession.SessionStatus.Conectada) LanSession.Shutdown("");
            if (lanPanel != null) lanPanel.SetActive(false);
            Select(lanButton);
        }

        void ToggleLanRole()
        {
            LanHostRole = NetRoles.Other(LanHostRole);
            RefreshLan();
        }

        void HostLan()
        {
            LanSession.Host(LanHostRole);
            RefreshLan();
            Select(lanCancelButton);
        }

        void JoinLan()
        {
            string address = lanAddressField != null ? lanAddressField.text.Trim() : "";
            if (address.Length == 0)
            {
                LanSession.LastNotice = "Escribe la IP del PC que creó la partida.";
                RefreshLan();
                return;
            }
            PlayerPrefs.SetString(LanAddressKey, address);
            PlayerPrefs.Save();
            LanSession.Join(address);
            RefreshLan();
            Select(lanCancelButton);
        }

        void RefreshLan()
        {
            LanSession session = LanSession.Instance;
            LanSession.SessionStatus status = session != null ? session.Status : LanSession.SessionStatus.Inactiva;
            bool connected = status == LanSession.SessionStatus.Conectada;
            bool client = connected && !session.IsHost;
            bool justConnected = connected && shownLanStatus != LanSession.SessionStatus.Conectada;
            shownLanStatus = status;

            if (lanSetupGroup != null) lanSetupGroup.SetActive(status == LanSession.SessionStatus.Inactiva);
            if (lanRoleText != null) lanRoleText.text = $"Tú: {NetRoles.Name(LanHostRole)}";
            if (lanLocalAddressText != null)
            {
                var addresses = LanSession.LocalAddresses();
                lanLocalAddressText.text = addresses.Count > 0
                    ? $"Tu IP: {string.Join("  ·  ", addresses)}  (puerto {LanProtocol.DefaultPort})"
                    : "No se encontró ninguna red: conecta este PC al wifi o al router.";
            }
            if (lanCancelButton != null) lanCancelButton.gameObject.SetActive(status != LanSession.SessionStatus.Inactiva);
            if (lanCancelText != null) lanCancelText.text = connected ? "Desconectar" : "Cancelar";
            // El cliente conectado no tiene menú propio: espera al anfitrión.
            if (lanBackButton != null)
            {
                lanBackButton.gameObject.SetActive(!client);
                // Sin «Cancelar», «Volver» va centrado.
                if (lanBackButton.transform is RectTransform back && lanCancelButton != null && lanCancelButton.transform is RectTransform cancel)
                {
                    bool both = status != LanSession.SessionStatus.Inactiva;
                    float gap = 20f;
                    float total = cancel.sizeDelta.x + gap + back.sizeDelta.x;
                    back.anchoredPosition = new Vector2(both ? total * 0.5f - back.sizeDelta.x * 0.5f : 0f, back.anchoredPosition.y);
                    cancel.anchoredPosition = new Vector2(-total * 0.5f + cancel.sizeDelta.x * 0.5f, cancel.anchoredPosition.y);
                }
            }
            if (lanStatusText != null) lanStatusText.text = LanStatus(session, status);

            if (lanButtonText != null) lanButtonText.text = connected ? "LAN: conectado" : "Jugar en LAN";
            if (tutorialButton != null) tutorialButton.interactable = !connected;
            if (rolesText != null)
            {
                rolesText.text = connected
                    ? $"<b>Partida LAN con {session.PeerAddress}.</b>  Tú: <b>{NetRoles.Name(session.BaseRole)}</b>  ·  el otro PC: <b>{NetRoles.Name(NetRoles.Other(session.BaseRole))}</b>\n" +
                      (session.IsHost ? "Elige supermercado y nivel y pulsa <b>Jugar</b>: el otro PC empieza contigo." : "El anfitrión elige supermercado y nivel.")
                    : defaultRolesText ?? rolesText.text;
            }

            bool inMenu = gameManager == null || gameManager.State == GameState.Menu;
            if (lanPanel != null && inMenu)
            {
                // El cliente se queda en el panel; el anfitrión vuelve al menú para elegir nivel.
                if (client && !lanPanel.activeSelf) lanPanel.SetActive(true);
                else if (justConnected && session.IsHost && lanPanel.activeSelf)
                {
                    lanPanel.SetActive(false);
                    Select(playButton);
                }
            }
        }

        static string LanStatus(LanSession session, LanSession.SessionStatus status)
        {
            switch (status)
            {
                case LanSession.SessionStatus.Esperando:
                    return "<b>Esperando al otro PC…</b>\nEn el otro PC: Jugar en LAN → escribe tu IP → Unirse.";
                case LanSession.SessionStatus.Conectando:
                    return $"<b>Conectando con {session.PeerAddress}…</b>";
                case LanSession.SessionStatus.Conectada:
                    return session.IsHost
                        ? $"<b>Conectado con {session.PeerAddress}.</b>\nTú: {NetRoles.Name(session.BaseRole)}. Vuelve al menú, elige nivel y pulsa Jugar."
                        : $"<b>Conectado con el anfitrión ({session.PeerAddress}).</b>  Tú: {NetRoles.Name(session.BaseRole)}.\nEsperando a que elija supermercado y nivel…";
                default:
                    return string.IsNullOrEmpty(LanSession.LastNotice)
                        ? "Los dos PC deben estar en la misma red (wifi o cable). Uno crea la partida y el otro se une con su IP."
                        : LanSession.LastNotice;
            }
        }

        static void Select(Button button)
        {
            if (button == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }
}
