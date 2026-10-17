using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>
    /// Menú principal (tienda, nivel desbloqueado, jugar, tutorial, salir), menú de pausa y botón
    /// de pausa. Se maneja con mouse o teclado (flechas + Enter) gracias al EventSystem.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
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
        [SerializeField] Button quitButton;

        [Header("Pausa")]
        [SerializeField] GameObject pausePanel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button pauseRestartButton;
        [SerializeField] Button pauseMenuButton;

        int selectedLevel = 1;
        int storeIndex;

        public int SelectedLevel => selectedLevel;
        public StoreId SelectedStore => StoreCatalog.All[storeIndex];

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
            Listen(quitButton, () => { if (gameManager != null) gameManager.QuitGame(); });
            Listen(resumeButton, () => { if (gameManager != null) gameManager.SetPaused(false); });
            Listen(pauseRestartButton, () => { if (gameManager != null) gameManager.Restart(); });
            Listen(pauseMenuButton, () => { if (gameManager != null) gameManager.GoToMenu(); });
        }

        void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= OnStateChanged;
                gameManager.PauseChanged -= OnPauseChanged;
            }
            foreach (Button b in new[] { playButton, tutorialButton, previousStoreButton, nextStoreButton, previousLevelButton, nextLevelButton, quitButton, resumeButton, pauseRestartButton, pauseMenuButton })
                if (b != null) b.onClick.RemoveAllListeners();
        }

        void Start()
        {
            selectedLevel = GameSession.MaxUnlockedLevel;
            StoreId current = gameManager != null ? gameManager.Store : StoreId.Clasico;
            storeIndex = System.Array.IndexOf(StoreCatalog.All, current);
            if (storeIndex < 0) storeIndex = 0;
            RefreshStore();
            RefreshLevel();
            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameManager != null) OnStateChanged(gameManager.State);
        }

        static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        void OnStateChanged(GameState state)
        {
            bool menu = state == GameState.Menu;
            if (menuPanel != null) menuPanel.SetActive(menu);
            if (menu) Select(GameSession.TutorialDone ? playButton : tutorialButton);
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

        static void Select(Button button)
        {
            if (button == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }
}
