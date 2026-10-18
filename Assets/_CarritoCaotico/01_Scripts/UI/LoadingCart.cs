using UnityEngine;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>
    /// Carrito que rueda sobre una barra de carga: avanza con el progreso y da pequeños botes.
    /// Lo usan la portada y la pantalla de carga entre escenas.
    /// </summary>
    public class LoadingCart : MonoBehaviour
    {
        public const string SpritePath = "Portada/Carrito_Veloz";

        RectTransform rect;
        float baseY;

        /// <summary>
        /// Crea el carrito como hijo de <paramref name="track"/>, con las ruedas sobre su borde
        /// superior y hundido <paramref name="sink"/> píxeles en la barra. Devuelve null si falta la imagen.
        /// </summary>
        public static LoadingCart Create(RectTransform track, float height, float sink)
        {
            Sprite sprite = Resources.Load<Sprite>(SpritePath);
            if (sprite == null) return null;

            var go = new GameObject("Carrito_Carga", typeof(RectTransform));
            go.transform.SetParent(track, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var cart = go.AddComponent<LoadingCart>();
            cart.rect = (RectTransform)go.transform;
            float aspect = sprite.rect.width / sprite.rect.height;
            cart.rect.sizeDelta = new Vector2(height * aspect, height);
            // La imagen tiene margen transparente debajo de las ruedas: se apoya algo por debajo.
            cart.rect.pivot = new Vector2(0.62f, 0.08f);
            cart.baseY = -sink;
            cart.rect.anchoredPosition = new Vector2(0f, cart.baseY);
            cart.SetProgress(0f);
            return cart;
        }

        public void SetProgress(float value)
        {
            float x = Mathf.Clamp01(value);
            rect.anchorMin = new Vector2(x, 1f);
            rect.anchorMax = new Vector2(x, 1f);
        }

        void Update()
        {
            // Botes y balanceo suaves, con tiempo real (la pantalla de carga ignora la pausa).
            float t = Time.unscaledTime;
            rect.anchoredPosition = new Vector2(0f, baseY + Mathf.Abs(Mathf.Sin(t * 11f)) * 4f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 7f) * 2.5f);
        }
    }
}
