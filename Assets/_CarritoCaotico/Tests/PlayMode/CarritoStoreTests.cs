using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarritoCaotico.Tests
{
    /// <summary>Pruebas del tutorial, el supermercado de Halloween, los obstáculos y la pantalla de carga.</summary>
    public class CarritoStoreTests
    {
        const string ClassicPath = "Assets/_CarritoCaotico/Scenes/CarritoCaotico_Prueba.unity";
        const string HalloweenPath = "Assets/_CarritoCaotico/Scenes/CarritoCaotico_Halloween.unity";

        GameManager manager;

        IEnumerator Load(string path)
        {
            if (!File.Exists(path)) Assert.Ignore($"La escena {path} no está generada.");
            GameSession.SetSeed(1);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            manager = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(manager);
        }

        IEnumerator WaitUntil(System.Func<bool> condition, float timeout, string message)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(condition(), message);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameInput.Scheme = ControlScheme.Normal;
            GameSession.ReturnToMenu();
        }

        [UnityTest]
        public IEnumerator Tutorial_AdvancesStepsAndFinishesWithoutReceipt()
        {
            GameSession.BeginTutorial();
            yield return Load(ClassicPath);
            yield return WaitUntil(() => manager.State == GameState.EnJuego, 5f, "El tutorial debería empezar sin cuenta atrás.");
            Assert.IsTrue(manager.IsTutorial);

            var tutorial = Object.FindAnyObjectByType<TutorialController>();
            yield return WaitUntil(() => tutorial.IsRunning, 2f, "El tutorial debería estar en marcha.");
            Assert.AreEqual(0, tutorial.StepIndex);
            Assert.IsNotNull(GameObject.Find("Tutorial"), "El panel del tutorial debería verse.");
            Assert.IsFalse(Object.FindAnyObjectByType<GameTimer>().IsRunning, "En el tutorial no corre el reloj.");

            // Paso 1: avanzar el carrito 3 m lo completa.
            var cart = Object.FindAnyObjectByType<CartController>();
            cart.Body.position += cart.transform.forward * 4f;
            yield return WaitUntil(() => tutorial.StepIndex == 1, 3f, "Mover el carrito debería completar el primer paso.");

            // Los demás pasos se fuerzan hasta el último (pagar en la caja).
            while (tutorial.StepIndex < tutorial.StepCount - 1)
            {
                int before = tutorial.StepIndex;
                tutorial.CompleteCurrentStepForTests();
                yield return WaitUntil(() => tutorial.StepIndex == before + 1, 3f, "El paso debería avanzar.");
            }

            // Entregar la compra termina el tutorial y no muestra el recibo.
            var grabber = Object.FindAnyObjectByType<GrabberArm>();
            var list = Object.FindAnyObjectByType<ShoppingList>();
            Product product = null;
            foreach (Product p in Object.FindObjectsByType<Product>())
                if (p.Type == list.Entries[0] && p.IsOnShelf) { product = p; break; }
            product.transform.position = grabber.HandPosition;
            product.Body.position = grabber.HandPosition;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(grabber.TryGrabNearest());
            grabber.DropHeld();
            yield return new WaitForSeconds(1.2f);

            var checkout = Object.FindAnyObjectByType<Checkout>();
            checkout.transform.position = cart.transform.position + Vector3.up;
            Physics.SyncTransforms();
            yield return WaitUntil(() => tutorial.IsFinished, 3f, "El tutorial debería terminar al pagar.");
            Assert.IsNotNull(GameObject.Find("Tutorial_Completado"));
            Assert.IsNull(GameObject.Find("Recibo"), "En el tutorial no se muestra el recibo.");
            Assert.IsTrue(GameSession.TutorialDone);
        }

        [UnityTest]
        public IEnumerator Halloween_SellsOnlyHalloweenProducts()
        {
            GameSession.Begin(1, StoreId.Halloween);
            yield return Load(HalloweenPath);
            yield return WaitUntil(() => manager.State == GameState.EnJuego, 8f, "La partida de Halloween debería empezar.");
            Assert.AreEqual(StoreId.Halloween, manager.Store);

            var halloween = new HashSet<ProductType>
            {
                ProductType.Calabaza, ProductType.Caramelos, ProductType.Vela, ProductType.Calavera,
                ProductType.Pocion, ProductType.SombreroBruja, ProductType.Murcielago, ProductType.ManzanaAcaramelada
            };
            var list = Object.FindAnyObjectByType<ShoppingList>();
            foreach (ProductType type in list.Entries)
                Assert.IsTrue(halloween.Contains(type), $"{type} no se vende en el supermercado de Halloween.");

            foreach (Product product in Object.FindObjectsByType<Product>())
                Assert.IsTrue(halloween.Contains(product.Type), $"Hay {product.Type} en las baldas de Halloween.");
            Assert.Greater(Object.FindObjectsByType<MovingObstacle>().Length, 0, "Debería haber fantasmas.");
        }

        [UnityTest]
        public IEnumerator Spill_MakesTheCartSlip()
        {
            GameSession.Begin(1, StoreId.Clasico);
            yield return Load(ClassicPath);
            yield return WaitUntil(() => manager.State == GameState.EnJuego, 8f, "La partida debería empezar.");

            var cart = Object.FindAnyObjectByType<CartController>();
            var spill = Object.FindAnyObjectByType<SlipperyZone>();
            Assert.IsNotNull(spill, "Debería haber charcos en la tienda.");

            yield return new WaitForFixedUpdate();
            Assert.IsFalse(cart.IsSlipping);

            Vector3 center = spill.GetComponent<Collider>().bounds.center;
            cart.Body.position = new Vector3(center.x, cart.Body.position.y, center.z);
            Physics.SyncTransforms();
            yield return WaitUntil(() => cart.IsSlipping, 1f, "El carrito debería patinar sobre el charco.");
        }

        [UnityTest]
        public IEnumerator Restart_ShowsLoadingScreen()
        {
            GameSession.Begin(1, StoreId.Clasico);
            yield return Load(ClassicPath);
            yield return WaitUntil(() => manager.State == GameState.EnJuego, 8f, "La partida debería empezar.");

            GameManager oldManager = manager;
            manager.Restart();
            Assert.IsTrue(LoadingScreen.IsLoading, "La pantalla de carga debería aparecer al reiniciar.");
            Assert.IsNotNull(GameObject.Find("Pantalla_Carga"));

            yield return WaitUntil(() => !LoadingScreen.IsLoading, 8f, "La pantalla de carga debería cerrarse.");
            manager = Object.FindAnyObjectByType<GameManager>();
            Assert.AreNotSame(oldManager, manager);
            Assert.AreEqual(GameState.Preparacion, manager.State);
        }
    }
}
