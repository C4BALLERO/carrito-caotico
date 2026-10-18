using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>
    /// Menú en lista vertical con una barra luminosa detrás de la opción elegida (jerarquía de los
    /// menús de juegos de acción: título grande arriba, opciones de texto centradas, la elegida
    /// resaltada y su descripción abajo). Arriba y abajo cambian de opción; en las opciones con
    /// valor (supermercado, nivel…), izquierda y derecha cambian el valor. El mouse elige al pasar.
    /// </summary>
    public class MenuList : MonoBehaviour
    {
        [Serializable]
        public class Row
        {
            public RectTransform rect;
            public Selectable main;
            public Text label;
            [Tooltip("Valor de la opción (vacío en las acciones).")]
            [Optional] public Text value;
            [Optional] public Button previous;
            [Optional] public Button next;
            [Tooltip("Descripción que se muestra abajo mientras está elegida.")]
            [Optional] public GameObject detail;
        }

        [SerializeField] Row[] rows = Array.Empty<Row>();
        [SerializeField] RectTransform highlight;
        [Tooltip("Descripción por defecto (si la opción elegida no tiene).")]
        [SerializeField] GameObject defaultDetail;
        [SerializeField] Color normalText = Color.white;
        [SerializeField] Color selectedText = new Color32(15, 23, 42, 255);
        [SerializeField] Color valueText = new Color32(251, 191, 36, 255);
        [SerializeField] Color disabledText = new Color(1f, 1f, 1f, 0.35f);

        int selected = -1;
        float highlightY;
        bool highlightPlaced;

        public int SelectedIndex => selected;
        public int RowCount => rows.Length;
        public RectTransform Highlight => highlight;
        public Row RowAt(int index) => rows[index];

        void Awake()
        {
            // Aceptar sobre una opción con valor pasa al valor siguiente.
            foreach (Row row in rows)
            {
                if (!(row.main is Button button) || row.next == null) continue;
                Button next = row.next;
                button.onClick.AddListener(() => { if (next.IsInteractable()) next.onClick.Invoke(); });
            }
        }

        void OnEnable()
        {
            highlightPlaced = false;
            selected = -1;
        }

        void Update()
        {
            int current = FindSelected();
            if (current >= 0) selected = current;

            if (selected >= 0 && selected < rows.Length)
            {
                Row row = rows[selected];
                // Izquierda y derecha cambian el valor de la opción elegida.
                int horizontal = GameInput.MenuHorizontalPressed();
                if (horizontal < 0 && row.previous != null && row.previous.IsInteractable()) row.previous.onClick.Invoke();
                else if (horizontal > 0 && row.next != null && row.next.IsInteractable()) row.next.onClick.Invoke();
            }

            Refresh();
        }

        int FindSelected()
        {
            EventSystem events = EventSystem.current;
            GameObject current = events != null ? events.currentSelectedGameObject : null;
            if (current == null) return -1;
            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                if (row.main != null && row.main.gameObject == current) return i;
                if (row.previous != null && row.previous.gameObject == current) return i;
                if (row.next != null && row.next.gameObject == current) return i;
            }
            return -1;
        }

        void Refresh()
        {
            bool anyDetail = false;
            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                bool isSelected = i == selected;
                bool interactable = row.main == null || row.main.IsInteractable();
                Color text = !interactable ? disabledText : isSelected ? selectedText : normalText;
                if (row.label != null && row.label.color != text) row.label.color = text;
                if (row.value != null)
                {
                    Color value = isSelected ? selectedText : valueText;
                    if (row.value.color != value) row.value.color = value;
                }
                SetArrowColor(row.previous, isSelected ? selectedText : normalText, disabledText);
                SetArrowColor(row.next, isSelected ? selectedText : normalText, disabledText);
                if (row.detail != null)
                {
                    if (row.detail.activeSelf != isSelected) row.detail.SetActive(isSelected);
                    anyDetail |= isSelected;
                }
            }
            if (defaultDetail != null && defaultDetail.activeSelf == anyDetail) defaultDetail.SetActive(!anyDetail);

            if (highlight == null) return;
            bool show = selected >= 0 && selected < rows.Length && rows[selected].rect != null;
            if (highlight.gameObject.activeSelf != show) highlight.gameObject.SetActive(show);
            if (!show) return;
            float target = rows[selected].rect.anchoredPosition.y;
            // La barra se desliza hasta la opción elegida.
            highlightY = highlightPlaced ? Mathf.Lerp(highlightY, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime)) : target;
            highlightPlaced = true;
            highlight.anchoredPosition = new Vector2(highlight.anchoredPosition.x, highlightY);
        }

        static void SetArrowColor(Button arrow, Color color, Color disabled)
        {
            if (arrow == null) return;
            if (!arrow.IsInteractable()) color = disabled;
            Text text = arrow.GetComponentInChildren<Text>();
            if (text != null && text.color != color) text.color = color;
        }
    }
}
