using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>
    /// Tutorial guiado paso a paso. Cada paso explica una acción con sus teclas y avanza solo
    /// cuando los jugadores la hacen (o con Tab para saltarla). Termina al entregar la compra.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        struct Step
        {
            public string Who;
            public Color WhoColor;
            public string Title;
            public string Body;
            public string[] Keys;
            public Func<bool> Done;
        }

        [Header("Sistemas")]
        [SerializeField] GameManager gameManager;
        [SerializeField] CartController cart;
        [SerializeField] GrabberArm grabber;
        [SerializeField] CartContents cartContents;
        [SerializeField] ShoppingList shoppingList;
        [SerializeField] CameraController cameraController;

        [Header("Interfaz")]
        [SerializeField] GameObject panel;
        [SerializeField] Image whoChip;
        [SerializeField] Text whoText;
        [SerializeField] Text titleText;
        [SerializeField] Text bodyText;
        [SerializeField] Text progressText;
        [SerializeField] Transform keysRow;
        [Tooltip("Plantilla de tecla (inactiva) que se copia para cada tecla del paso.")]
        [SerializeField] GameObject keyTemplate;
        [SerializeField] Image doneBadge;
        [SerializeField] GameObject completePanel;
        [SerializeField] Button playButton;
        [SerializeField] Button menuButton;

        static readonly Color PusherBlue = new Color32(37, 99, 235, 255);
        static readonly Color GrabberPurple = new Color32(124, 58, 237, 255);
        static readonly Color BothGreen = new Color32(4, 120, 87, 255);

        readonly List<Step> steps = new List<Step>();
        readonly List<GameObject> keyInstances = new List<GameObject>();
        int index = -1;
        bool started;
        bool finished;
        float celebrateUntil = -1f;
        float stepTime;

        // Datos para comprobar los pasos.
        Vector3 startPosition;
        float startYaw;
        float driftSeconds;
        Vector3 startHand;
        bool thrown;
        float listReadSeconds;

        public int StepIndex => index;
        public int StepCount => steps.Count;
        public bool IsRunning => started && !finished;
        public bool IsFinished => finished;

        void OnEnable()
        {
            if (grabber != null) grabber.ProductThrown += OnThrown;
            if (gameManager != null) gameManager.GameEnded += OnGameEnded;
            if (playButton != null) playButton.onClick.AddListener(OnPlay);
            if (menuButton != null) menuButton.onClick.AddListener(OnMenu);
        }

        void OnDisable()
        {
            if (grabber != null) grabber.ProductThrown -= OnThrown;
            if (gameManager != null) gameManager.GameEnded -= OnGameEnded;
            if (playButton != null) playButton.onClick.RemoveListener(OnPlay);
            if (menuButton != null) menuButton.onClick.RemoveListener(OnMenu);
        }

        void Start()
        {
            if (panel != null) panel.SetActive(false);
            if (completePanel != null) completePanel.SetActive(false);
            if (keyTemplate != null) keyTemplate.SetActive(false);
        }

        void Update()
        {
            if (gameManager == null) return;
            if (!started)
            {
                if (gameManager.IsTutorial && gameManager.State == GameState.EnJuego) Begin();
                return;
            }
            if (finished || gameManager.IsPaused) return;

            if (celebrateUntil > 0f)
            {
                if (Time.time < celebrateUntil) return;
                celebrateUntil = -1f;
                ShowStep(index + 1);
                return;
            }

            stepTime += Time.deltaTime;
            if (cart != null && cart.IsDrifting) driftSeconds += Time.deltaTime;
            if (GameInput.GrabberListHeld()) listReadSeconds += Time.deltaTime;

            if (index >= 0 && index < steps.Count && (steps[index].Done() || GameInput.TutorialSkipPressed()))
                Celebrate();
        }

        void Begin()
        {
            started = true;
            BuildSteps();
            ShowStep(0);
        }

        void BuildSteps()
        {
            steps.Clear();
            string[] p = GameInput.PusherKeyLabels(ControlScheme.Normal);
            string[] g = GameInput.GrabberKeyLabels(ControlScheme.Normal);
            // Con mando, el giro es un único stick; con teclado, dos teclas.
            string[] steerKeys = GameInput.PusherUsesGamepad
                ? new[] { p[2] }
                : p[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            steps.Add(new Step
            {
                Who = "EMPUJADOR · Jugador 1", WhoColor = PusherBlue,
                Title = "Empuja el carrito",
                Body = "Mantén pulsado para acelerar. Suelta para frenar poco a poco.",
                Keys = new[] { p[0] },
                Done = () => cart != null && Vector3.Distance(cart.transform.position, startPosition) > 3f
            });
            steps.Add(new Step
            {
                Who = "EMPUJADOR · Jugador 1", WhoColor = PusherBlue,
                Title = "Gira",
                Body = "Gira a izquierda y derecha. Funciona mejor si el carrito está en movimiento.",
                Keys = steerKeys,
                Done = () => cart != null && Mathf.Abs(Mathf.DeltaAngle(startYaw, cart.transform.eulerAngles.y)) > 70f
            });
            steps.Add(new Step
            {
                Who = "EMPUJADOR · Jugador 1", WhoColor = PusherBlue,
                Title = "Derrapa",
                Body = "Acelera, gira y mantén pulsado para derrapar en las curvas.",
                Keys = new[] { p[0], p[3] },
                Done = () => driftSeconds > 0.6f
            });
            steps.Add(new Step
            {
                Who = "EMPUJADOR · Jugador 1", WhoColor = PusherBlue,
                Title = "Mira alrededor",
                Body = GameInput.PusherUsesGamepad
                    ? "Mueve el stick derecho para girar la cámara. Frena si hace falta."
                    : "Mueve el mouse para girar la cámara. Frena con la otra tecla si hace falta.",
                Keys = new[] { GameInput.CameraLabel, p[1] },
                Done = () => cameraController != null && Mathf.Abs(cameraController.YawOffset) > 25f
            });
            steps.Add(new Step
            {
                Who = "AGARRADOR · Jugador 2", WhoColor = GrabberPurple,
                Title = "Mueve el brazo",
                Body = "La mano rosa se mueve alrededor del carrito. Se vuelve amarilla cuando hay algo al alcance.",
                Keys = new[] { g[0] },
                Done = () => grabber != null && Vector3.Distance(grabber.HandLocalPosition, startHand) > 1f
            });
            steps.Add(new Step
            {
                Who = "AGARRADOR · Jugador 2", WhoColor = GrabberPurple,
                Title = "Agarra un producto",
                Body = "Acerquen el carrito a una balda, pon la mano sobre un producto y agárralo.",
                Keys = new[] { g[1] },
                Done = () => grabber != null && grabber.HeldProduct != null
            });
            steps.Add(new Step
            {
                Who = "AGARRADOR · Jugador 2", WhoColor = GrabberPurple,
                Title = "Suéltalo en la canasta",
                Body = "Lleva la mano sobre el carrito y suelta: el producto cae dentro de la canasta.",
                Keys = new[] { g[0], g[1] },
                Done = () => cartContents != null && cartContents.Count > 0 && grabber.HeldProduct == null
            });
            steps.Add(new Step
            {
                Who = "AGARRADOR · Jugador 2", WhoColor = GrabberPurple,
                Title = "Lanza un producto",
                Body = "Agarra algo y lánzalo en la dirección del brazo. ¡Cuidado con lo que rompes!",
                Keys = new[] { g[1], g[2] },
                Done = () => thrown
            });
            steps.Add(new Step
            {
                Who = "AGARRADOR · Jugador 2", WhoColor = GrabberPurple,
                Title = "Mira la lista",
                Body = "La lista va plegada para que el Empujador no la vea. Mantén la tecla para leerla: mientras la miras, el brazo se queda quieto.",
                Keys = new[] { GameInput.GrabberListLabelFor(ControlScheme.Normal) },
                Done = () => listReadSeconds >= 0.6f
            });
            steps.Add(new Step
            {
                Who = "LOS DOS", WhoColor = BothGreen,
                Title = "Completen la lista",
                Body = "Solo el Agarrador mira la lista: debe decirle al Empujador adónde ir. Pista: Pasillo 3 · Frutas.",
                Keys = Array.Empty<string>(),
                Done = () => shoppingList != null && shoppingList.IsComplete
            });
            steps.Add(new Step
            {
                Who = "LOS DOS", WhoColor = BothGreen,
                Title = "Paguen en la caja",
                Body = "Lleven el carrito a la zona verde, al fondo de la tienda. En partida pueden pausar con Esc o P.",
                Keys = Array.Empty<string>(),
                Done = () => false // termina con el evento de fin de partida
            });
        }

        void ShowStep(int newIndex)
        {
            index = newIndex;
            if (index >= steps.Count) return;
            stepTime = 0f;

            // Referencias para comprobar el paso desde el estado actual.
            if (cart != null)
            {
                startPosition = cart.transform.position;
                startYaw = cart.transform.eulerAngles.y;
            }
            if (grabber != null) startHand = grabber.HandLocalPosition;
            driftSeconds = 0f;
            thrown = false;
            listReadSeconds = 0f;

            Step step = steps[index];
            if (panel != null) panel.SetActive(true);
            if (doneBadge != null) doneBadge.gameObject.SetActive(false);
            if (whoChip != null) whoChip.color = step.WhoColor;
            if (whoText != null) whoText.text = step.Who;
            if (titleText != null) titleText.text = step.Title;
            if (bodyText != null) bodyText.text = step.Body;
            if (progressText != null) progressText.text = $"Paso {index + 1} de {steps.Count}  ·  Tab / Select: saltar paso";
            ShowKeys(step.Keys);
        }

        void ShowKeys(string[] keys)
        {
            foreach (GameObject key in keyInstances) Destroy(key);
            keyInstances.Clear();
            if (keyTemplate == null || keysRow == null) return;

            foreach (string label in keys)
            {
                GameObject key = Instantiate(keyTemplate, keysRow);
                key.SetActive(true);
                Text text = key.GetComponentInChildren<Text>(true);
                if (text != null) text.text = label;
                var layout = key.GetComponent<LayoutElement>();
                if (layout != null) layout.preferredWidth = Mathf.Max(60f, 30f + label.Length * 16f);
                keyInstances.Add(key);
            }
        }

        void Celebrate()
        {
            if (doneBadge != null) doneBadge.gameObject.SetActive(true);
            if (titleText != null) titleText.text = "¡Bien hecho!";
            celebrateUntil = Time.time + 0.9f;
        }

        void OnThrown(Product product) => thrown = true;

        void OnGameEnded(ShoppingReceipt receipt)
        {
            if (!started) return;
            finished = true;
            if (panel != null) panel.SetActive(false);
            if (completePanel != null) completePanel.SetActive(true);
            if (playButton != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        void OnPlay()
        {
            GameSession.Begin(1, StoreId.Clasico);
            LoadingScreen.LoadStore(StoreId.Clasico, "Súper Caótico · Nivel 1");
        }

        void OnMenu()
        {
            if (gameManager != null) gameManager.GoToMenu();
        }

        /// <summary>Pruebas: fuerza el paso actual como completado.</summary>
        public void CompleteCurrentStepForTests() => Celebrate();
    }
}
