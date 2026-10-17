using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>Referencias de una fila de la Lista del Agarrador.</summary>
    [Serializable]
    public class ShoppingListRow
    {
        public GameObject root;
        public Image checkBox;
        public Image checkMark;
        public Text nameText;
        public Image countPill;
        public Text countText;
    }

    /// <summary>
    /// Interfaz de la partida. Durante el juego solo se ve la Lista del Agarrador (con el reloj y
    /// el nivel integrados) y avisos breves. Los controles se explican en el tutorial, en la
    /// tarjeta de preparación y en el menú de pausa. El estado nunca se comunica solo con color.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        enum MessageKind { Info, Warning, Danger }

        [Header("Sistemas")]
        [SerializeField] GameManager gameManager;
        [SerializeField] GameTimer timer;
        [SerializeField] ShoppingList shoppingList;
        [SerializeField] Checkout checkout;
        [SerializeField] RivalManager rivals;

        [Header("Contenedores")]
        [Tooltip("Paneles de juego: se ocultan en el menú principal.")]
        [SerializeField] GameObject hudRoot;

        [Header("Lista del Agarrador")]
        [SerializeField] Text listSubtitleText;
        [SerializeField] Text timerText;
        [SerializeField] Image timerBackground;
        [SerializeField] ShoppingListRow[] listRows = Array.Empty<ShoppingListRow>();
        [SerializeField] Text listExtraText;
        [SerializeField] Text listProgressText;
        [SerializeField] RectTransform listProgressFill;

        [Header("Avisos, preparación y pausa")]
        [SerializeField] GameObject messagePanel;
        [SerializeField] Text messageText;
        [SerializeField] Image messageBackground;
        [SerializeField] GameObject preparationPanel;
        [SerializeField] Text preparationTitleText;
        [SerializeField] Text preparationCountText;
        [SerializeField] Text preparationHintText;
        [SerializeField] Text preparationControlsText;
        [SerializeField] Text pauseControlsText;

        [Header("Recibo")]
        [SerializeField] GameObject receiptPanel;
        [SerializeField] Text receiptTitleText;
        [SerializeField] Text receiptTimeText;
        [SerializeField] Text receiptNamesText;
        [SerializeField] Text receiptRequiredText;
        [SerializeField] Text receiptDeliveredText;
        [SerializeField] Text receiptStatusText;
        [SerializeField] Text receiptCorrectText;
        [SerializeField] Text receiptMissingText;
        [SerializeField] Text receiptExtraText;
        [SerializeField] Text receiptScoreText;
        [SerializeField] Text receiptResultText;
        [SerializeField] Image receiptResultBadge;
        [SerializeField] Button restartButton;
        [SerializeField] Button nextLevelButton;
        [SerializeField] Button menuButton;

        [Header("Paleta (contraste mínimo 4.5:1 para texto)")]
        [SerializeField] Color ink = new Color32(15, 23, 42, 255);
        [SerializeField] Color neutral = new Color32(226, 232, 240, 255);
        [SerializeField] Color success = new Color32(4, 120, 87, 255);
        [SerializeField] Color warning = new Color32(245, 158, 11, 255);
        [SerializeField] Color warningText = new Color32(180, 83, 9, 255);
        [SerializeField] Color danger = new Color32(220, 38, 38, 255);

        [Header("Temporizador: umbrales")]
        [SerializeField, Min(0)] int warningSeconds = 30;
        [SerializeField, Min(0)] int dangerSeconds = 15;
        [SerializeField, Min(0)] int pulseSeconds = 10;

        readonly StringBuilder builder = new StringBuilder(512);
        float messageHideTime = -1f;
        int shownSeconds = -1;

        bool IsTutorial => gameManager != null && gameManager.IsTutorial;

        void OnEnable()
        {
            GameInput.EnsureDeviceHook();
            GameInput.DevicesChanged += OnDevicesChanged;
            if (timer != null) timer.SecondsChanged += OnSecondsChanged;
            if (shoppingList != null) shoppingList.Changed += RefreshList;
            if (checkout != null) checkout.EmptyCartRejected += OnEmptyCart;
            if (gameManager != null)
            {
                gameManager.StateChanged += OnStateChanged;
                gameManager.GameEnded += ShowReceipt;
                gameManager.LevelConfigured += OnLevelConfigured;
            }
            if (rivals != null)
            {
                rivals.Changed += RefreshSubtitle;
                rivals.ProductTaken += OnRivalTookProduct;
            }
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
            if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
            if (menuButton != null) menuButton.onClick.AddListener(OnMenuClicked);
        }

        void OnDisable()
        {
            GameInput.DevicesChanged -= OnDevicesChanged;
            if (timer != null) timer.SecondsChanged -= OnSecondsChanged;
            if (shoppingList != null) shoppingList.Changed -= RefreshList;
            if (checkout != null) checkout.EmptyCartRejected -= OnEmptyCart;
            if (gameManager != null)
            {
                gameManager.StateChanged -= OnStateChanged;
                gameManager.GameEnded -= ShowReceipt;
                gameManager.LevelConfigured -= OnLevelConfigured;
            }
            if (rivals != null)
            {
                rivals.Changed -= RefreshSubtitle;
                rivals.ProductTaken -= OnRivalTookProduct;
            }
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
            if (nextLevelButton != null) nextLevelButton.onClick.RemoveListener(OnNextLevelClicked);
            if (menuButton != null) menuButton.onClick.RemoveListener(OnMenuClicked);
        }

        void Start()
        {
            if (receiptPanel != null) receiptPanel.SetActive(false);
            if (messagePanel != null) messagePanel.SetActive(false);
            if (timer != null) OnSecondsChanged(Mathf.CeilToInt(timer.Remaining));
            RefreshList();
            ApplyScheme(GameInput.Scheme);
            if (gameManager != null)
            {
                if (gameManager.CurrentConfig != null) OnLevelConfigured(gameManager.CurrentConfig);
                OnStateChanged(gameManager.State);
            }
        }

        void Update()
        {
            if (gameManager != null && gameManager.State == GameState.Preparacion && preparationCountText != null)
            {
                string count = Mathf.CeilToInt(gameManager.PreparationRemaining).ToString();
                if (preparationCountText.text != count) preparationCountText.text = count;
            }

            if (messageHideTime > 0f && Time.time >= messageHideTime)
            {
                messageHideTime = -1f;
                if (messagePanel != null) messagePanel.SetActive(false);
            }

            UpdateTimerPulse();
        }

        // ---------- Temporizador (dentro de la cabecera de la lista) ----------

        void OnSecondsChanged(int seconds)
        {
            shownSeconds = seconds;
            if (timerText == null) return;

            if (IsTutorial)
            {
                timerText.text = "Tutorial";
                timerText.color = ink;
                if (timerBackground != null) timerBackground.color = Color.white;
                return;
            }

            timerText.text = GameTimer.Format(seconds);
            if (timerBackground == null) return;
            // Color + tono del texto: el nivel de urgencia también se lee en los dígitos.
            if (seconds <= dangerSeconds)
            {
                timerBackground.color = danger;
                timerText.color = Color.white;
            }
            else if (seconds <= warningSeconds)
            {
                timerBackground.color = warning;
                timerText.color = ink;
            }
            else
            {
                timerBackground.color = Color.white;
                timerText.color = ink;
            }
        }

        void UpdateTimerPulse()
        {
            if (timer == null || timerBackground == null) return;
            Transform pill = timerBackground.transform;
            // Pulso suave solo en los últimos segundos: el movimiento transmite urgencia.
            if (timer.IsRunning && shownSeconds > 0 && shownSeconds <= pulseSeconds)
            {
                float pulse = 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f));
                pill.localScale = new Vector3(pulse, pulse, 1f);
            }
            else if (pill.localScale != Vector3.one)
            {
                pill.localScale = Vector3.one;
            }
        }

        // ---------- Lista del Agarrador ----------

        void RefreshList()
        {
            if (shoppingList == null) return;
            var entries = shoppingList.Entries;

            for (int i = 0; i < listRows.Length; i++)
            {
                ShoppingListRow row = listRows[i];
                if (row == null || row.root == null) continue;

                bool visible = i < entries.Count;
                if (row.root.activeSelf != visible) row.root.SetActive(visible);
                if (!visible) continue;

                ProductType type = entries[i];
                int required = shoppingList.GetRequired(type);
                int collected = shoppingList.GetCollected(type);
                bool done = collected >= required;

                if (row.checkBox != null) row.checkBox.color = done ? success : neutral;
                if (row.checkMark != null) row.checkMark.enabled = done;
                if (row.nameText != null)
                {
                    row.nameText.text = $"{required} × {ProductCatalog.DisplayName(type)}";
                    row.nameText.color = done ? success : ink;
                }
                if (row.countPill != null) row.countPill.color = done ? success : neutral;
                if (row.countText != null)
                {
                    row.countText.text = done ? "¡Listo!" : $"{collected}/{required}";
                    row.countText.color = done ? Color.white : ink;
                }
            }

            RefreshExtras();

            int total = shoppingList.TotalRequired;
            int got = shoppingList.TotalCollected;
            if (listProgressText != null)
            {
                listProgressText.text = shoppingList.IsComplete
                    ? "¡Lista completa! Vayan a la caja"
                    : $"Conseguido: {got} de {total} unidades";
                listProgressText.color = shoppingList.IsComplete ? success : ink;
            }
            if (listProgressFill != null)
            {
                Vector2 max = listProgressFill.anchorMax;
                max.x = total > 0 ? Mathf.Clamp01((float)got / total) : 0f;
                listProgressFill.anchorMax = max;
            }
        }

        /// <summary>Avisa de unidades sobrantes y de productos que no están en la lista.</summary>
        void RefreshExtras()
        {
            if (listExtraText == null) return;

            builder.Clear();
            foreach (ProductType type in ProductCatalog.AllTypes)
            {
                int inCart = shoppingList.GetInCart(type);
                int required = shoppingList.GetRequired(type);
                if (inCart <= required) continue;

                if (builder.Length > 0) builder.Append("   ");
                if (required == 0) builder.Append("(!) ").Append(ProductCatalog.DisplayName(type)).Append(" no está en la lista");
                else builder.Append("(!) ").Append(inCart - required).Append(' ').Append(ProductCatalog.DisplayName(type)).Append(" de más");
            }

            listExtraText.text = builder.ToString();
            listExtraText.gameObject.SetActive(builder.Length > 0);
        }

        void RefreshSubtitle()
        {
            if (listSubtitleText == null || gameManager == null) return;
            if (IsTutorial)
            {
                listSubtitleText.text = "Tutorial · solo el Agarrador mira esta lista";
                return;
            }

            int level = gameManager.CurrentLevel;
            int configured = gameManager.CurrentConfig != null ? gameManager.CurrentConfig.RivalCount : 0;
            int count = rivals != null ? Mathf.Max(configured, rivals.ActiveCount) : configured;
            int taken = rivals != null ? rivals.TakenCount : 0;
            string rivalsLine = count == 0 ? "sin rivales" : $"{count} {(count == 1 ? "rival" : "rivales")} · {taken} robados";
            listSubtitleText.text = $"Nivel {level} · {StoreCatalog.DisplayName(gameManager.Store)} · {rivalsLine}";
        }

        // ---------- Estados y avisos ----------

        void OnStateChanged(GameState state)
        {
            if (preparationPanel != null) preparationPanel.SetActive(state == GameState.Preparacion);
            if (hudRoot != null) hudRoot.SetActive(state != GameState.Menu);
            if (state == GameState.Menu && receiptPanel != null) receiptPanel.SetActive(false);

            switch (state)
            {
                case GameState.EnJuego:
                    if (!IsTutorial) ShowMessage("¡A comprar! Comuníquense y lleguen a la caja", 2.5f, MessageKind.Info);
                    if (timer != null) OnSecondsChanged(Mathf.CeilToInt(timer.Remaining));
                    RefreshSubtitle();
                    break;
                case GameState.TiempoAgotado:
                case GameState.CompraEntregada:
                    if (messagePanel != null) messagePanel.SetActive(false);
                    messageHideTime = -1f;
                    break;
            }
        }

        void OnLevelConfigured(LevelConfig config)
        {
            ApplyScheme(config.Scheme);
            RefreshSubtitle();

            if (preparationTitleText != null) preparationTitleText.text = $"Nivel {config.Level} · ¡Prepárense!";
            if (preparationHintText != null)
            {
                string rivalsLine = config.RivalCount == 0
                    ? "sin compradores rivales"
                    : config.RivalCount == 1 ? "1 comprador rival" : $"{config.RivalCount} compradores rivales";
                string scheme = config.Scheme == ControlScheme.Normal
                    ? "Agarrador: lee la lista y guía al Empujador."
                    : $"<b>¡{LevelProgression.SchemeName(config.Scheme)}!</b>";
                preparationHintText.text = $"{GameTimer.Format(config.Duration)} · {rivalsLine}\n{scheme}";
            }
        }

        void OnRivalTookProduct(Product product)
        {
            if (shoppingList == null || shoppingList.GetRequired(product.Type) == 0) return;
            ShowMessage($"¡Un rival se llevó {product.DisplayName}!", 2f, MessageKind.Warning);
        }

        void OnEmptyCart()
        {
            ShowMessage("La canasta está vacía: consigan productos primero", 2.5f, MessageKind.Warning);
        }

        void ShowMessage(string text, float duration, MessageKind kind)
        {
            if (messageText == null) return;
            messageText.text = text;
            Color background = kind == MessageKind.Warning ? warning : kind == MessageKind.Danger ? danger : ink;
            if (messageBackground != null) messageBackground.color = background;
            messageText.color = kind == MessageKind.Warning ? ink : Color.white;
            if (messagePanel != null && !messagePanel.activeSelf) messagePanel.SetActive(true);
            messageHideTime = duration > 0f ? Time.time + duration : -1f;
        }

        /// <summary>Texto con los controles del esquema activo (preparación y pausa).</summary>
        void ApplyScheme(ControlScheme scheme)
        {
            string text = ControlsText(scheme);
            if (preparationControlsText != null) preparationControlsText.text = text;
            if (pauseControlsText != null) pauseControlsText.text = text;
        }

        public static string ControlsText(ControlScheme scheme)
        {
            string[] p = GameInput.PusherKeyLabels(scheme);
            string[] g = GameInput.GrabberKeyLabels(scheme);
            string note = scheme == ControlScheme.Invertido ? "  <i>(direcciones al revés)</i>" : "";
            string pusherDevice = GameInput.PusherUsesGamepad ? " (mando)" : " (teclado)";
            string grabberDevice = GameInput.GrabberUsesGamepad ? " (mando)" : " (teclado)";
            return $"<b>Empujador{pusherDevice}:</b> {p[0]} acelerar · {p[1]} frenar · {p[2]} girar · {p[3]} derrapar · {GameInput.CameraLabel} cámara{note}\n" +
                   $"<b>Agarrador{grabberDevice}:</b> {g[0]} mover brazo · {g[1]} agarrar / soltar · {g[2]} lanzar{note}";
        }

        void OnDevicesChanged()
        {
            ApplyScheme(GameInput.Scheme);
            ShowMessage(GameInput.DevicesSummary(), 2.5f, MessageKind.Info);
        }

        // ---------- Recibo ----------

        void ShowReceipt(ShoppingReceipt receipt)
        {
            // En el tutorial el cierre lo muestra el TutorialController.
            if (receiptPanel == null || receipt == null || IsTutorial) return;
            receiptPanel.SetActive(true);
            if (preparationPanel != null) preparationPanel.SetActive(false);

            if (receiptTitleText != null)
                receiptTitleText.text = receipt.TimedOut ? "¡La tienda cerró!" : $"Recibo · {StoreCatalog.DisplayName(gameManager.Store)}";
            if (receiptTimeText != null)
                receiptTimeText.text = $"Tiempo empleado  <b>{GameTimer.Format(receipt.ElapsedSeconds)}</b>     ·     Tiempo restante  <b>{GameTimer.Format(receipt.RemainingSeconds)}</b>";

            FillReceiptTable(receipt);

            SetText(receiptCorrectText, receipt.Correct.ToString());
            SetText(receiptMissingText, receipt.Missing.ToString());
            SetText(receiptExtraText, receipt.Extra.ToString());
            SetText(receiptScoreText, receipt.Score.ToString());

            if (nextLevelButton != null)
                nextLevelButton.gameObject.SetActive(gameManager != null && gameManager.CanAdvance);
            // Selección inicial para navegar el recibo con el mando.
            Button first = nextLevelButton != null && nextLevelButton.gameObject.activeSelf ? nextLevelButton : restartButton;
            if (first != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(first.gameObject);
            if (receiptResultText != null) receiptResultText.text = receipt.ResultLabel;
            if (receiptResultBadge != null)
            {
                bool perfect = receipt.IsComplete && receipt.Extra == 0;
                Color badge = perfect ? success : receipt.IsComplete ? warning : danger;
                receiptResultBadge.color = badge;
                if (receiptResultText != null) receiptResultText.color = badge == warning ? ink : Color.white;
            }
        }

        void FillReceiptTable(ShoppingReceipt receipt)
        {
            var names = new StringBuilder();
            var required = new StringBuilder();
            var delivered = new StringBuilder();
            var status = new StringBuilder();
            string successHex = ColorUtility.ToHtmlStringRGB(success);
            string dangerHex = ColorUtility.ToHtmlStringRGB(danger);
            string warningHex = ColorUtility.ToHtmlStringRGB(warningText);

            for (int i = 0; i < receipt.Lines.Count; i++)
            {
                ReceiptLine line = receipt.Lines[i];
                if (i > 0)
                {
                    names.Append('\n');
                    required.Append('\n');
                    delivered.Append('\n');
                    status.Append('\n');
                }
                names.Append(ProductCatalog.DisplayName(line.Type));
                required.Append(line.Required);
                delivered.Append(line.Delivered);

                if (line.Missing > 0) status.Append("<color=#").Append(dangerHex).Append(">Faltan ").Append(line.Missing).Append("</color>");
                else if (line.Extra > 0) status.Append("<color=#").Append(warningHex).Append(">Sobran ").Append(line.Extra).Append("</color>");
                else status.Append("<color=#").Append(successHex).Append(">Completo</color>");
            }

            if (receipt.Lines.Count == 0) names.Append("Sin productos");

            SetText(receiptNamesText, names.ToString());
            SetText(receiptRequiredText, required.ToString());
            SetText(receiptDeliveredText, delivered.ToString());
            SetText(receiptStatusText, status.ToString());
        }

        static void SetText(Text target, string value)
        {
            if (target != null) target.text = value;
        }

        void OnRestartClicked()
        {
            if (gameManager != null) gameManager.Restart();
        }

        void OnNextLevelClicked()
        {
            if (gameManager != null) gameManager.NextLevel();
        }

        void OnMenuClicked()
        {
            if (gameManager != null) gameManager.GoToMenu();
        }
    }
}
