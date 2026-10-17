using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarritoCaotico
{
    public enum GameState
    {
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
    /// Controlador central de estados de la partida. Coordina el inicio, la evaluación en caja,
    /// el fin por tiempo y el reinicio. La lógica de gameplay vive en cada sistema.
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

        [Header("Flujo")]
        [Tooltip("Segundos de preparación para que el Agarrador lea la lista.")]
        [SerializeField, Min(0f)] float preparationTime = 4f;
        [SerializeField] bool lockCursorWhilePlaying = true;

        readonly Dictionary<ProductType, int> deliveredSnapshot = new Dictionary<ProductType, int>();
        float preparationRemaining;

        public GameState State { get; private set; } = GameState.Preparacion;
        public float PreparationRemaining => preparationRemaining;
        public ShoppingReceipt LastReceipt { get; private set; }
        public bool IsGameOver => State == GameState.CompraEntregada || State == GameState.TiempoAgotado;

        public event Action<GameState> StateChanged;
        public event Action<ShoppingReceipt> GameEnded;

        void OnEnable()
        {
            if (timer != null) timer.Expired += OnTimerExpired;
        }

        void OnDisable()
        {
            if (timer != null) timer.Expired -= OnTimerExpired;
            SetCursorLocked(false);
        }

        void Start()
        {
            if (shoppingList != null) shoppingList.Generate();
            if (timer != null) timer.ResetTimer();

            preparationRemaining = preparationTime;
            SetPlayersEnabled(false);
            if (cameraController != null) cameraController.SetLookEnabled(true);
            ChangeState(GameState.Preparacion);

            if (preparationTime <= 0f) BeginPlay();
        }

        void Update()
        {
            if (State == GameState.Preparacion)
            {
                preparationRemaining = Mathf.Max(0f, preparationRemaining - Time.deltaTime);
                if (preparationRemaining <= 0f) BeginPlay();
            }
            else if (IsGameOver && GameInput.RestartPressed())
            {
                Restart();
                return;
            }

            if (State == GameState.EnJuego && GameInput.ToggleCursorPressed())
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
        }

        void BeginPlay()
        {
            if (State != GameState.Preparacion) return;
            SetPlayersEnabled(true);
            SetCursorLocked(lockCursorWhilePlaying);
            if (timer != null) timer.StartTimer();
            ChangeState(GameState.EnJuego);
        }

        /// <summary>
        /// Llamado por la caja. Congela el contenido de la canasta y evalúa una sola vez.
        /// </summary>
        public CheckoutAttempt TryCheckout()
        {
            if (State != GameState.EnJuego) return CheckoutAttempt.NotPlaying;
            if (cartContents == null || cartContents.Count == 0) return CheckoutAttempt.EmptyCart;

            if (timer != null) timer.StopTimer();
            SetPlayersEnabled(false);

            cartContents.FillCounts(deliveredSnapshot);
            float elapsed = timer != null ? timer.Elapsed : 0f;
            float remaining = timer != null ? timer.Remaining : 0f;
            LastReceipt = ShoppingRules.Evaluate(shoppingList.Required, deliveredSnapshot, elapsed, remaining, true);

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
            SetCursorLocked(false);
            ChangeState(finalState);
            GameEnded?.Invoke(LastReceipt);
        }

        /// <summary>Recarga la escena actual para empezar una partida nueva.</summary>
        public void Restart()
        {
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
