using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarritoCaotico.Tests
{
    /// <summary>Portada al arrancar y carrito sobre la barra de la pantalla de carga.</summary>
    public class CarritoPortadaTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

        IEnumerator LoadMenu()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
            GameSession.SetSeed(1);
            GameSession.ReturnToMenu();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator Splash_ShowsCartOnBar_BlocksMenuAndThenFades()
        {
            yield return LoadMenu();
            SplashScreen splash = SplashScreen.Show();
            Assert.IsNotNull(splash, "Falta la ilustración de la portada en Resources/Portada.");
            yield return null;

            Assert.IsTrue(SplashScreen.IsShowing);
            Assert.IsNull(FindDeep(splash.transform, "Titulo"), "El logo ya viene en la ilustración.");
            Transform cart = FindDeep(splash.transform, "Carrito_Carga");
            Assert.IsNotNull(cart, "El carrito rueda sobre la barra.");
            Assert.IsFalse(EventSystem.current.sendNavigationEvents, "Mientras se ve la portada el menú no recibe teclado.");

            float startX = ((RectTransform)cart).anchorMin.x;
            yield return new WaitForSecondsRealtime(3f);
            Assert.Greater(((RectTransform)cart).anchorMin.x, startX + 0.2f, "El carrito avanza con la barra.");

            // Se ve al menos 7 segundos.
            yield return new WaitForSecondsRealtime(3.8f);
            Assert.IsTrue(SplashScreen.IsShowing, "A los 6,8 s la portada sigue visible.");

            yield return new WaitForSecondsRealtime(1.4f);
            Assert.IsFalse(SplashScreen.IsShowing, "La portada termina sola.");
            Assert.IsNull(GameObject.Find("Portada"), "La portada desaparece.");
            Assert.IsTrue(EventSystem.current.sendNavigationEvents, "El menú vuelve a recibir teclado.");
            Assert.IsNotNull(GameObject.Find("Menu_Principal"), "Debajo está el menú.");
        }

        [UnityTest]
        public IEnumerator LoadingScreen_HasCartOnItsBar()
        {
            yield return LoadMenu();
            LoadingScreen.LoadStore(StoreId.Clasico, "Prueba de carga");
            yield return null;

            GameObject screen = GameObject.Find("Pantalla_Carga");
            Assert.IsNotNull(screen);
            Assert.IsNotNull(FindDeep(screen.transform, "Carrito_Carga"), "La pantalla de carga lleva el carrito.");
            for (float t = 0f; LoadingScreen.IsLoading && t < 10f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.IsFalse(LoadingScreen.IsLoading);
        }
    }
}
