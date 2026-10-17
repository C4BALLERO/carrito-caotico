using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CarritoCaotico
{
    /// <summary>
    /// Pantalla de carga persistente: ilustración del supermercado de destino, título, consejo y
    /// barra de progreso mientras la escena se carga en segundo plano. Las imágenes se toman de
    /// Resources/Carga: primero las generadas con IA ("*_ia_*") y, si no hay, las capturas.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        const float MinimumSeconds = 1.2f;
        const float FadeSeconds = 0.3f;

        static LoadingScreen instance;

        Canvas canvas;
        CanvasGroup group;
        Image picture;
        Text title;
        Text tip;
        RectTransform barFill;
        Font font;

        public static bool IsLoading { get; private set; }

        /// <summary>Carga la escena del supermercado indicado mostrando la pantalla de carga.</summary>
        public static void LoadStore(StoreId store, string heading)
        {
            Load(StoreCatalog.ScenePath(store), store, heading);
        }

        /// <summary>Carga una escena por su ruta (Assets/.../Escena.unity).</summary>
        public static void Load(string scenePath, StoreId store, string heading)
        {
            if (IsLoading) return;
            EnsureInstance();
            instance.StartCoroutine(instance.LoadRoutine(scenePath, store, heading));
        }

        static AsyncOperation StartLoad(string scenePath)
        {
            if (SceneUtility.GetBuildIndexByScenePath(scenePath) >= 0)
                return SceneManager.LoadSceneAsync(scenePath);
#if UNITY_EDITOR
            // Fuera de Build Settings (pruebas): carga por ruta desde el Editor.
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                scenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            return null;
#endif
        }

        static void EnsureInstance()
        {
            if (instance != null) return;
            var go = new GameObject("Pantalla_Carga");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LoadingScreen>();
            instance.Build();
        }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>();

            Image back = NewImage(transform, "Fondo", new Color32(15, 23, 42, 255));
            Stretch(back.rectTransform, Vector2.zero, Vector2.one);

            picture = NewImage(transform, "Ilustracion", Color.white);
            picture.preserveAspect = false;
            Stretch(picture.rectTransform, Vector2.zero, Vector2.one);

            // Banda inferior oscura para que el texto sea legible sobre cualquier imagen.
            Image band = NewImage(transform, "Banda", new Color(0.06f, 0.09f, 0.16f, 0.82f));
            Stretch(band.rectTransform, Vector2.zero, new Vector2(1f, 0.26f));

            title = NewText(band.transform, "Titulo", 54, FontStyle.Bold, TextAnchor.UpperLeft);
            Stretch(title.rectTransform, new Vector2(0f, 0.5f), Vector2.one, new Vector2(80f, 0f), new Vector2(-80f, -26f));
            tip = NewText(band.transform, "Consejo", 28, FontStyle.Normal, TextAnchor.UpperLeft);
            Stretch(tip.rectTransform, new Vector2(0f, 0.18f), new Vector2(1f, 0.52f), new Vector2(80f, 0f), new Vector2(-80f, 0f));

            Image track = NewImage(band.transform, "Barra", new Color(1f, 1f, 1f, 0.25f));
            Stretch(track.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.14f), new Vector2(80f, 0f), new Vector2(-80f, 0f));
            Image fill = NewImage(track.transform, "Relleno", new Color32(245, 158, 11, 255));
            barFill = fill.rectTransform;
            Stretch(barFill, Vector2.zero, new Vector2(0f, 1f));

            group.alpha = 0f;
            canvas.enabled = false;
        }

        IEnumerator LoadRoutine(string scenePath, StoreId store, string heading)
        {
            IsLoading = true;
            Time.timeScale = 1f;
            ShowContent(store, heading);
            canvas.enabled = true;
            group.alpha = 1f;
            group.blocksRaycasts = true;
            SetProgress(0f);

            float start = Time.unscaledTime;
            AsyncOperation operation = StartLoad(scenePath);
            if (operation == null)
            {
                Debug.LogError($"[Carrito] No se pudo cargar la escena '{scenePath}'. ¿Está en Build Settings?");
                Hide();
                yield break;
            }
            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f || Time.unscaledTime - start < MinimumSeconds)
            {
                float loadPart = Mathf.Clamp01(operation.progress / 0.9f);
                float timePart = Mathf.Clamp01((Time.unscaledTime - start) / MinimumSeconds);
                SetProgress(Mathf.Min(loadPart, timePart));
                yield return null;
            }
            SetProgress(1f);
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;

            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - t / FadeSeconds;
                yield return null;
            }
            Hide();
        }

        void Hide()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            canvas.enabled = false;
            IsLoading = false;
        }

        void ShowContent(StoreId store, string heading)
        {
            Sprite sprite = PickImage(store);
            picture.sprite = sprite;
            picture.color = sprite != null ? Color.white : new Color32(4, 120, 87, 255);
            title.text = string.IsNullOrEmpty(heading) ? StoreCatalog.DisplayName(store) : heading;
            string[] tips = StoreCatalog.Tips(store);
            tip.text = tips.Length > 0 ? "Consejo: " + tips[Random.Range(0, tips.Length)] : "";
        }

        static Sprite PickImage(StoreId store)
        {
            string prefix = StoreCatalog.LoadingPrefix(store);
            Sprite[] all = Resources.LoadAll<Sprite>("Carga");
            var ai = new List<Sprite>();
            var captures = new List<Sprite>();
            foreach (Sprite sprite in all)
            {
                if (!sprite.name.StartsWith(prefix)) continue;
                if (sprite.name.Contains("_ia_")) ai.Add(sprite);
                else captures.Add(sprite);
            }
            List<Sprite> pool = ai.Count > 0 ? ai : captures;
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }

        void SetProgress(float value)
        {
            if (barFill != null) barFill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        Text NewText(Transform parent, string name, int size, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            Stretch(rect, min, max, Vector2.zero, Vector2.zero);
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
