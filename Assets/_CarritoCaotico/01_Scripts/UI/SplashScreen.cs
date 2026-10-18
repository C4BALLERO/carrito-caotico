using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>
    /// Portada al arrancar el juego: ilustración (con el logo incluido) y barra de carga con el
    /// carrito rodando encima. Aparece una vez por sesión, cuando la primera escena tiene
    /// GameManager (las pruebas empiezan en otra escena), se ve entera al menos 7 s y se funde con
    /// el menú. Mientras se ve, el menú de debajo no recibe teclado ni clics.
    /// </summary>
    public class SplashScreen : MonoBehaviour
    {
        public const string BackgroundPath = "Portada/Carrito_Caotico";
        // La barra se llena en 6,5 s y se queda llena 0,5 s: 7 s de portada antes del fundido.
        const float FillSeconds = 6.5f;
        const float HoldSeconds = 0.5f;
        const float FadeSeconds = 0.5f;
        static readonly Color32 FillColor = new Color32(245, 158, 11, 255);

        // Franja oscura de la ilustración donde va la barra (proporciones de la imagen, desde abajo).
        static readonly Vector2 StripMin = new Vector2(0.0909f, 0.0266f);
        static readonly Vector2 StripMax = new Vector2(0.9085f, 0.0840f);

        static bool shown;

        CanvasGroup group;
        RectTransform fill;
        LoadingCart cart;
        EventSystem blockedEvents;

        public static bool IsShowing { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            shown = false;
            IsShowing = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ShowOnStart()
        {
            if (shown || FindAnyObjectByType<GameManager>() == null) return;
            Show();
        }

        /// <summary>Muestra la portada (también la usan las pruebas). Null si falta la ilustración.</summary>
        public static SplashScreen Show()
        {
            Sprite background = Resources.Load<Sprite>(BackgroundPath);
            if (background == null) return null;
            shown = true;
            var go = new GameObject("Portada");
            DontDestroyOnLoad(go);
            var splash = go.AddComponent<SplashScreen>();
            splash.Build(background);
            splash.StartCoroutine(splash.Run());
            return splash;
        }

        void Build(Sprite background)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000; // por encima de la pantalla de carga
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true;

            // Fondo negro que intercepta los clics, por si la pantalla es más ancha que la imagen.
            Image back = NewImage(transform, "Fondo", Color.black);
            back.raycastTarget = true;
            Stretch(back.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // La ilustración cubre la pantalla sin deformarse; la barra va dentro de ella.
            Image picture = NewImage(transform, "Ilustracion", Color.white);
            picture.sprite = background;
            Stretch(picture.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = background.rect.width / background.rect.height;

            Image track = NewImage(picture.transform, "Barra", new Color(0f, 0f, 0f, 0f));
            Stretch(track.rectTransform, StripMin, StripMax, new Vector2(16f, 9f), new Vector2(-16f, -9f));
            Image bar = NewImage(track.transform, "Relleno", FillColor);
            fill = bar.rectTransform;
            Stretch(fill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            cart = LoadingCart.Create(track.rectTransform, 170f, 24f);

            // Sin navegación de teclado ni mando: el menú de debajo no se activa con Enter.
            blockedEvents = EventSystem.current;
            if (blockedEvents != null) blockedEvents.sendNavigationEvents = false;
            SetProgress(0f);
        }

        IEnumerator Run()
        {
            IsShowing = true;
            for (float t = 0f; t < FillSeconds; t += Time.unscaledDeltaTime)
            {
                SetProgress(Mathf.SmoothStep(0f, 1f, t / FillSeconds));
                yield return null;
            }
            SetProgress(1f);
            yield return new WaitForSecondsRealtime(HoldSeconds);
            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / FadeSeconds;
                yield return null;
            }
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (blockedEvents != null) blockedEvents.sendNavigationEvents = true;
            IsShowing = false;
        }

        void SetProgress(float value)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            if (cart != null) cart.SetProgress(value);
        }

        static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
