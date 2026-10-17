using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>
    /// Menú principal (jugar, elegir nivel desbloqueado, salir), menú de pausa y botón de pausa
    /// del HUD. Se maneja con mouse o teclado (flechas + Enter) gracias al EventSystem.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        [SerializeField] GameManager gameManager;

        [Header("Menú principal")]
        [SerializeField] GameObject menuPanel;
        [SerializeField] Button playButton;
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
        [SerializeField] Button hudPauseButton;

        int selectedLevel = 1;

        public int SelectedLevel => selectedLevel;

        void OnEnable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged += OnStateChanged;
                gameManager.PauseChanged += OnPauseChanged;
            }
            Listen(playButton, OnPlay);
            Listen(previousLevelButton, () => ChangeLevel(-1));
            Listen(nextLevelButton, () => ChangeLevel(1));
            Listen(quitButton, () => { if (gameManager != null) gameManager.QuitGame(); });
            Listen(resumeButton, () => { if (gameManager != null) gameManager.SetPaused(false); });
            Listen(pauseRestartButton, () => { if (gameManager != null) gameManager.Restart(); });
            Listen(pauseMenuButton, () => { if (gameManager != null) gameManager.GoToMenu(); });
            Listen(hudPauseButton, () => { if (gameManager != null) gameManager.SetPaused(true); });
        }

        void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= OnStateChanged;
                gameManager.PauseChanged -= OnPauseChanged;
            }
            foreach (Button b in new[] { playButton, previousLevelButton, nextLevelButton, quitButton, resumeButton, pauseRestartButton, pauseMenuButton, hudPauseButton })
                if (b != null) b.onClick.RemoveAllListeners();
        }

        void Start()
        {
            selectedLevel = GameSession.MaxUnlockedLevel;
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
            if (hudPauseButton != null) hudPauseButton.gameObject.SetActive(state == GameState.Preparacion || state == GameState.EnJuego);
            if (menu) Select(playButton);
        }

        void OnPauseChanged(bool paused)
        {
            if (pausePanel != null) pausePanel.SetActive(paused);
            if (paused) Select(resumeButton);
        }

        void OnPlay()
        {
            if (gameManager != null) gameManager.StartGame(selectedLevel);
        }

        void ChangeLevel(int delta)
        {
            selectedLevel = Mathf.Clamp(selectedLevel + delta, 1, GameSession.MaxUnlockedLevel);
            RefreshLevel();
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
            string controls = selectedLevel >= LevelProgression.ControlSwapStartLevel
                ? "Los controles pueden cambiar"
                : "Controles normales";
            levelDetailText.text = $"Lista de {config.MinUnits} a {config.MaxUnits} unidades  ·  {GameTimer.Format(config.Duration)}  ·  {rivals}\n{controls}  ·  Desbloqueado hasta el nivel {max}";
        }

        static void Select(Button button)
        {
            if (button == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }
}
