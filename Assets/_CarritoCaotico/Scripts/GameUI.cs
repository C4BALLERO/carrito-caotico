using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>
    /// Interfaz de la partida: temporizador, panel del Empujador (izquierda), lista del
    /// Agarrador (derecha), mensajes centrales y recibo final. Solo escucha eventos de los
    /// sistemas y nunca contiene lógica de juego.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("Sistemas")]
        [SerializeField] GameManager gameManager;
        [SerializeField] GameTimer timer;
        [SerializeField] ShoppingList shoppingList;
        [SerializeField] CartContents cartContents;
        [SerializeField] GrabberArm grabber;
        [SerializeField] Checkout checkout;

        [Header("HUD")]
        [SerializeField] Text timerText;
        [SerializeField] Image timerBackground;
        [SerializeField] Text cargoText;
        [SerializeField] Text listText;
        [SerializeField] Text listProgressText;
        [SerializeField] Text handText;
        [SerializeField] GameObject messagePanel;
        [SerializeField] Text messageText;

        [Header("Recibo")]
        [SerializeField] GameObject receiptPanel;
        [SerializeField] Text receiptTitleText;
        [SerializeField] Text receiptBodyText;
        [SerializeField] Text receiptResultText;
        [SerializeField] Button restartButton;

        [Header("Estilo")]
        [SerializeField] Color timerNormalColor = new Color32(15, 118, 110, 255);
        [SerializeField] Color timerWarningColor = new Color32(233, 79, 55, 255);
        [SerializeField, Min(0)] int warningSeconds = 20;
        [SerializeField] string doneColorHex = "#1F8A4C";
        [SerializeField] string pendingColorHex = "#1F2933";
        [SerializeField] string extraColorHex = "#C2410C";

        readonly StringBuilder builder = new StringBuilder(512);
        float messageHideTime = -1f;
        Product lastCandidate;
        Product lastHeld;
        bool handDirty = true;

        void OnEnable()
        {
            if (timer != null) timer.SecondsChanged += OnSecondsChanged;
            if (shoppingList != null) shoppingList.Changed += RefreshList;
            if (cartContents != null) cartContents.Changed += RefreshCargo;
            if (grabber != null) grabber.HeldProductChanged += OnHeldChanged;
            if (checkout != null) checkout.EmptyCartRejected += OnEmptyCart;
            if (gameManager != null)
            {
                gameManager.StateChanged += OnStateChanged;
                gameManager.GameEnded += ShowReceipt;
            }
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        }

        void OnDisable()
        {
            if (timer != null) timer.SecondsChanged -= OnSecondsChanged;
            if (shoppingList != null) shoppingList.Changed -= RefreshList;
            if (cartContents != null) cartContents.Changed -= RefreshCargo;
            if (grabber != null) grabber.HeldProductChanged -= OnHeldChanged;
            if (checkout != null) checkout.EmptyCartRejected -= OnEmptyCart;
            if (gameManager != null)
            {
                gameManager.StateChanged -= OnStateChanged;
                gameManager.GameEnded -= ShowReceipt;
            }
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
        }

        void Start()
        {
            if (receiptPanel != null) receiptPanel.SetActive(false);
            if (timer != null) OnSecondsChanged(Mathf.CeilToInt(timer.Remaining));
            RefreshCargo();
            RefreshList();
            RefreshHand();
        }

        void Update()
        {
            if (gameManager != null && gameManager.State == GameState.Preparacion)
            {
                int seconds = Mathf.CeilToInt(gameManager.PreparationRemaining);
                SetMessage($"¡Prepárense!  Agarrador: lee la lista.  {seconds}", -1f);
            }
            else if (messageHideTime > 0f && Time.time >= messageHideTime)
            {
                messageHideTime = -1f;
                if (messagePanel != null) messagePanel.SetActive(false);
            }

            // El candidato de la mano cambia según la posición: se actualiza solo si cambia.
            if (grabber != null && (grabber.Candidate != lastCandidate || grabber.HeldProduct != lastHeld))
                handDirty = true;
            if (handDirty) RefreshHand();
        }

        void OnSecondsChanged(int seconds)
        {
            if (timerText != null) timerText.text = GameTimer.Format(seconds);
            if (timerBackground != null)
                timerBackground.color = seconds <= warningSeconds ? timerWarningColor : timerNormalColor;
        }

        void RefreshCargo()
        {
            if (cargoText == null || cartContents == null) return;
            int count = cartContents.Count;
            cargoText.text = $"Carga: {cartContents.TotalWeight:0.0} kg   ·   {count} {(count == 1 ? "producto" : "productos")} en la canasta";
        }

        void RefreshList()
        {
            if (listText == null || shoppingList == null) return;

            builder.Clear();
            var entries = shoppingList.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                ProductType type = entries[i];
                int required = shoppingList.GetRequired(type);
                int collected = shoppingList.GetCollected(type);
                int inCart = shoppingList.GetInCart(type);
                bool done = collected >= required;
                string color = done ? doneColorHex : pendingColorHex;

                builder.Append("<color=").Append(color).Append('>');
                builder.Append(done ? "[x]  " : "[  ]  ");
                builder.Append("<b>").Append(required).Append(" × ").Append(ProductCatalog.DisplayName(type)).Append("</b>");
                builder.Append("   ").Append(collected).Append('/').Append(required);
                if (!done) builder.Append("  (faltan ").Append(required - collected).Append(')');
                builder.Append("</color>");
                if (inCart > required)
                    builder.Append("  <color=").Append(extraColorHex).Append(">+").Append(inCart - required).Append(" de más</color>");
                builder.Append('\n');
            }

            // Productos en la canasta que no están en la lista.
            foreach (ProductType type in ProductCatalog.AllTypes)
            {
                if (shoppingList.GetRequired(type) > 0) continue;
                int inCart = shoppingList.GetInCart(type);
                if (inCart <= 0) continue;
                builder.Append("<color=").Append(extraColorHex).Append(">(!) ")
                    .Append(inCart).Append(" × ").Append(ProductCatalog.DisplayName(type)).Append(": no está en la lista</color>\n");
            }

            listText.text = builder.ToString();

            if (listProgressText != null)
            {
                listProgressText.text = shoppingList.IsComplete
                    ? "¡Lista completa! Vayan a la caja"
                    : $"Conseguido: {shoppingList.TotalCollected} / {shoppingList.TotalRequired} unidades";
            }
        }

        void OnHeldChanged(Product product)
        {
            handDirty = true;
        }

        void RefreshHand()
        {
            handDirty = false;
            if (handText == null || grabber == null) return;

            lastCandidate = grabber.Candidate;
            lastHeld = grabber.HeldProduct;

            if (lastHeld != null) handText.text = $"Mano: sosteniendo <b>{lastHeld.DisplayName}</b>  (Enter suelta · Ctrl der. lanza)";
            else if (lastCandidate != null) handText.text = $"Mano: Enter para agarrar <b>{lastCandidate.DisplayName}</b>";
            else handText.text = "Mano: vacía";
        }

        void OnStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.EnJuego:
                    SetMessage("¡A comprar! Comuníquense y lleguen a la caja", 2.5f);
                    break;
                case GameState.TiempoAgotado:
                    SetMessage("¡La tienda cerró!", -1f);
                    break;
                case GameState.CompraEntregada:
                    if (messagePanel != null) messagePanel.SetActive(false);
                    break;
            }
        }

        void OnEmptyCart()
        {
            SetMessage("La canasta está vacía: ¡consigan productos primero!", 2.5f);
        }

        void SetMessage(string text, float duration)
        {
            if (messageText == null) return;
            if (messageText.text != text) messageText.text = text;
            if (messagePanel != null && !messagePanel.activeSelf) messagePanel.SetActive(true);
            messageHideTime = duration > 0f ? Time.time + duration : -1f;
        }

        void ShowReceipt(ShoppingReceipt receipt)
        {
            if (receiptPanel == null || receipt == null) return;
            receiptPanel.SetActive(true);

            if (receiptTitleText != null)
                receiptTitleText.text = receipt.TimedOut ? "¡La tienda cerró!" : "Recibo · Súper Caótico";

            builder.Clear();
            builder.Append("Tiempo empleado:  <b>").Append(GameTimer.Format(receipt.ElapsedSeconds)).Append("</b>\n");
            builder.Append("Tiempo restante:  <b>").Append(GameTimer.Format(receipt.RemainingSeconds)).Append("</b>\n");
            builder.Append("--------------------------------------------\n");

            for (int i = 0; i < receipt.Lines.Count; i++)
            {
                ReceiptLine line = receipt.Lines[i];
                builder.Append("<b>").Append(ProductCatalog.DisplayName(line.Type)).Append("</b>   ");
                builder.Append("pedido ").Append(line.Required);
                builder.Append(" · entregado ").Append(line.Delivered);
                if (line.Missing > 0) builder.Append(" · <color=#B91C1C>faltan ").Append(line.Missing).Append("</color>");
                if (line.Extra > 0) builder.Append(" · <color=").Append(extraColorHex).Append(">sobran ").Append(line.Extra).Append("</color>");
                builder.Append('\n');
            }

            builder.Append("--------------------------------------------\n");
            builder.Append("Productos correctos:  <b>").Append(receipt.Correct).Append("</b>\n");
            builder.Append("Productos faltantes:  <b>").Append(receipt.Missing).Append("</b>\n");
            builder.Append("Productos sobrantes:  <b>").Append(receipt.Extra).Append("</b>\n");
            builder.Append("\n<size=34><b>Puntuación: ").Append(receipt.Score).Append("</b></size>");

            if (receiptBodyText != null) receiptBodyText.text = builder.ToString();

            if (receiptResultText != null)
            {
                receiptResultText.text = receipt.ResultLabel;
                receiptResultText.color = receipt.IsComplete ? (Color)new Color32(31, 138, 76, 255) : (Color)new Color32(185, 28, 28, 255);
            }
        }

        void OnRestartClicked()
        {
            if (gameManager != null) gameManager.Restart();
        }
    }
}
