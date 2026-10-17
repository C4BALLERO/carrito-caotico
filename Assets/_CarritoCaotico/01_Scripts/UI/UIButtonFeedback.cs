using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>
    /// Respuesta de un botón: crece y se eleva al pasar el mouse o seleccionarlo con teclado o
    /// mando, se aplasta al pulsarlo y suena. Usa tiempo sin escala para funcionar en pausa.
    /// El botón principal puede "respirar" para llamar la atención.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField] float hoverScale = 1.06f;
        [SerializeField] float pressScale = 0.94f;
        [Tooltip("Píxeles que sube el botón con el hover.")]
        [SerializeField] float hoverLift = 4f;
        [Tooltip("Velocidad de la transición (1/s).")]
        [SerializeField] float speed = 16f;
        [Tooltip("Botón principal: late suavemente cuando no se toca.")]
        [SerializeField] bool idlePulse;
        [SerializeField] UISound clickSound = UISound.Click;

        RectTransform rect;
        Selectable selectable;
        Vector2 basePosition;
        Vector2 lastApplied;
        bool hasBase;
        bool hovered;
        bool selected;
        bool pressed;
        float scale = 1f;
        float lift;

        public bool IsHighlighted => hovered || selected;
        public float CurrentScale => scale;

        void Awake()
        {
            rect = transform as RectTransform;
            selectable = GetComponent<Selectable>();
        }

        void OnEnable()
        {
            hovered = selected = pressed = false;
            scale = 1f;
            lift = 0f;
            if (rect != null) rect.localScale = Vector3.one;
        }

        void OnDisable()
        {
            if (rect == null) return;
            rect.localScale = Vector3.one;
            if (hasBase) rect.anchoredPosition = basePosition;
        }

        bool Interactable => selectable == null || selectable.IsInteractable();

        /// <summary>Escala objetivo según el estado (también la usan las pruebas).</summary>
        public float TargetScale
        {
            get
            {
                if (!Interactable) return 1f;
                if (pressed) return pressScale;
                if (IsHighlighted) return hoverScale;
                if (idlePulse) return 1f + 0.025f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f));
                return 1f;
            }
        }

        void LateUpdate()
        {
            if (rect == null) return;
            // La posición base se toma tras el primer layout y se actualiza si otro script mueve el botón.
            if (!hasBase || (rect.anchoredPosition - lastApplied).sqrMagnitude > 0.01f)
            {
                basePosition = rect.anchoredPosition;
                hasBase = true;
            }

            float t = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
            scale = Mathf.Lerp(scale, TargetScale, t);
            lift = Mathf.Lerp(lift, IsHighlighted && !pressed && Interactable ? hoverLift : 0f, t);
            rect.localScale = new Vector3(scale, scale, 1f);
            if (rect.parent == null || rect.parent.GetComponent<LayoutGroup>() == null)
                rect.anchoredPosition = basePosition + new Vector2(0f, lift);
            lastApplied = rect.anchoredPosition;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!hovered && Interactable) GameAudio.PlayUI(UISound.Hover);
            hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Interactable) pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pressed && Interactable) GameAudio.PlayUI(clickSound);
            pressed = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            // Con teclado o mando la selección hace de hover (con el mouse ya sonó al entrar).
            if (!selected && !hovered && Interactable && !(eventData is PointerEventData)) GameAudio.PlayUI(UISound.Hover);
            selected = true;
        }

        public void OnDeselect(BaseEventData eventData) => selected = false;

        public void OnSubmit(BaseEventData eventData)
        {
            if (!Interactable) return;
            GameAudio.PlayUI(clickSound);
            // Pequeño aplastamiento también al confirmar con Enter o A.
            scale = pressScale;
        }

        /// <summary>Configuración usada por el generador de la interfaz.</summary>
        public void Configure(bool pulse, UISound sound)
        {
            idlePulse = pulse;
            clickSound = sound;
        }
    }
}
