using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CarritoCaotico.Tests
{
    /// <summary>
    /// Menú principal en lista: navegación vertical que salta las flechas, pincelada que sigue a la
    /// opción elegida, descripción de cada opción, izquierda/derecha que cambian el valor y la
    /// cámara que recorre el supermercado de fondo. Con la variable de entorno
    /// CARRITO_CAPTURA_MENU guarda una captura del menú en esa carpeta para revisarlo a ojo.
    /// </summary>
    public class CarritoMenuTests
    {
        static readonly string[] Scenes =
        {
            "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity",
            "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Halloween.unity"
        };

        PadAssignment previousPad;

        [SetUp]
        public void SetUp() => previousPad = GameInput.PadMode;

        [TearDown]
        public void TearDown()
        {
            GameInput.PadMode = previousPad;
            GameSession.ReturnToMenu();
            Time.timeScale = 1f;
        }

        static IEnumerator Load(string path)
        {
            if (!File.Exists(path)) Assert.Ignore($"La escena {path} no está generada.");
            GameSession.ReturnToMenu();
            GameSession.SetSeed(1);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            yield return null;
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenu_ListNavigatesAndTheHighlightFollows()
        {
            yield return Load(Scenes[0]);
            var manager = Object.FindAnyObjectByType<GameManager>();
            Assert.AreEqual(GameState.Menu, manager.State);
            var list = Object.FindAnyObjectByType<MenuList>();
            Assert.IsNotNull(list, "El menú es una lista.");
            Assert.AreEqual(10, list.RowCount);

            // Arriba y abajo pasan de fila sin detenerse en las flechas, y la lista da la vuelta.
            Assert.AreEqual(list.RowAt(4).main, list.RowAt(3).main.FindSelectableOnDown(), "De Supermercado se baja a Nivel.");
            Assert.AreEqual(list.RowAt(2).main, list.RowAt(3).main.FindSelectableOnUp());
            Assert.AreEqual(list.RowAt(0).main, list.RowAt(list.RowCount - 1).main.FindSelectableOnDown(), "Desde Salir se vuelve a Jugar.");
            Assert.AreEqual(Navigation.Mode.None, list.RowAt(3).previous.navigation.mode);

            // Al elegir una opción, la pincelada se desliza hasta ella y aparece su descripción.
            MenuList.Row pad = list.RowAt(7);
            EventSystem.current.SetSelectedGameObject(pad.main.gameObject);
            yield return Wait(0.6f);
            Assert.AreEqual(7, list.SelectedIndex);
            Assert.AreEqual(pad.rect.anchoredPosition.y, list.Highlight.anchoredPosition.y, 1f, "La pincelada está detrás de la opción elegida.");
            Assert.IsTrue(pad.detail.activeInHierarchy, "Se ve la descripción del mando.");
            Assert.IsFalse(list.RowAt(3).detail.activeInHierarchy, "Solo una descripción a la vez.");
            Assert.AreNotEqual(Color.white, pad.label.color, "La opción elegida cambia de color sobre la pincelada.");
            Assert.AreEqual(Color.white, list.RowAt(0).label.color);

            // Aceptar (o la flecha derecha) cambia el valor.
            string before = pad.value.text;
            ((Button)pad.main).onClick.Invoke();
            yield return null;
            Assert.AreNotEqual(before, pad.value.text, "Aceptar sobre una opción con valor pasa al siguiente.");
            pad.previous.onClick.Invoke();
            yield return null;
            Assert.AreEqual(before, pad.value.text, "La flecha izquierda vuelve al valor anterior.");
        }

        [UnityTest]
        public IEnumerator MainMenu_CameraTravelsThroughTheStore()
        {
            yield return Load(Scenes[0]);
            var backdrop = Object.FindAnyObjectByType<MenuBackdrop>();
            Assert.IsNotNull(backdrop, "La cámara tiene el fondo vivo del menú.");
            Assert.IsTrue(backdrop.IsActive);
            Transform camera = backdrop.transform;
            yield return null;
            Vector3 start = camera.position;
            yield return Wait(1f);
            Assert.Greater(Vector3.Distance(start, camera.position), 0.1f, "La cámara se mueve mientras se elige.");
            Assert.Greater(camera.position.y, 3.5f, "Va por encima de las estanterías.");

            // Al empezar la partida deja de mover la cámara.
            Object.FindAnyObjectByType<GameManager>().StartGame(1);
            yield return Wait(0.3f);
            Assert.IsFalse(backdrop.IsActive);
        }

        /// <summary>Captura del menú de cada supermercado (solo si se pide con CARRITO_CAPTURA_MENU).</summary>
        [UnityTest]
        public IEnumerator MainMenu_Capture()
        {
            string folder = Environment.GetEnvironmentVariable("CARRITO_CAPTURA_MENU");
            if (string.IsNullOrEmpty(folder)) Assert.Ignore("Sin CARRITO_CAPTURA_MENU: no se guarda captura.");
            Directory.CreateDirectory(folder);
            foreach (string scene in Scenes)
            {
                yield return Load(scene);
                yield return Wait(1.5f);
                var list = Object.FindAnyObjectByType<MenuList>();
                EventSystem.current.SetSelectedGameObject(list.RowAt(3).main.gameObject);
                yield return Wait(0.6f);
                Save(Path.Combine(folder, $"menu_{Path.GetFileNameWithoutExtension(scene)}.png"));
            }
        }

        /// <summary>El lienzo se dibuja encima de la cámara en una textura (el modo Overlay no sale en Camera.Render).</summary>
        static void Save(string path)
        {
            Camera camera = Camera.main;
            Canvas canvas = Object.FindAnyObjectByType<MenuUI>().GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                foreach (Canvas c in Object.FindObjectsByType<Canvas>())
                    if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) canvas = c;
            }
            var texture = new RenderTexture(1920, 1080, 24);
            RenderMode mode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.1f;
            camera.targetTexture = texture;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            canvas.renderMode = mode;
            Object.Destroy(image);
            texture.Release();
            Object.Destroy(texture);
            Debug.Log($"[Carrito] Captura del menú: {path}");
        }
    }
}
