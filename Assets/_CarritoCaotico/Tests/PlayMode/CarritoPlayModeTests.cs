using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarritoCaotico.Tests
{
    /// <summary>
    /// Pruebas de integración sobre la escena generada por Carrito > Construir escena de prueba.
    /// Si la escena no existe, las pruebas se marcan como ignoradas.
    /// </summary>
    public class CarritoPlayModeTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/Scenes/CarritoCaotico_Prueba.unity";

        GameManager manager;
        GameTimer timer;
        ShoppingList shoppingList;
        CartContents contents;
        CartController cart;
        GrabberArm grabber;
        Checkout checkout;

        IEnumerator LoadGameScene()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            CacheSystems();
            Assert.IsNotNull(manager, "No se encontró GameManager en la escena.");
        }

        void CacheSystems()
        {
            manager = Object.FindAnyObjectByType<GameManager>();
            timer = Object.FindAnyObjectByType<GameTimer>();
            shoppingList = Object.FindAnyObjectByType<ShoppingList>();
            contents = Object.FindAnyObjectByType<CartContents>();
            cart = Object.FindAnyObjectByType<CartController>();
            grabber = Object.FindAnyObjectByType<GrabberArm>();
            checkout = Object.FindAnyObjectByType<Checkout>();
        }

        IEnumerator WaitForState(GameState state, float timeout)
        {
            float elapsed = 0f;
            while (manager.State != state && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(state, manager.State);
        }

        Product FindFreeProduct(ProductType type)
        {
            foreach (Product product in Object.FindObjectsByType<Product>())
            {
                if (product.Type == type && !product.IsHeld && !contents.Contains(product)) return product;
            }
            return null;
        }

        /// <summary>Lleva un producto a la mano y lo agarra.</summary>
        IEnumerator GrabProduct(Product product)
        {
            product.Body.linearVelocity = Vector3.zero;
            product.transform.position = grabber.HandPosition;
            product.Body.position = grabber.HandPosition;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(grabber.TryGrabNearest(), "La mano no pudo agarrar el producto cercano.");
            Assert.AreSame(product, grabber.HeldProduct);
        }

        [UnityTest]
        public IEnumerator GrabDropAndThrow_UpdateBasketWeightAndList()
        {
            yield return LoadGameScene();
            yield return WaitForState(GameState.EnJuego, 8f);

            ProductType type = shoppingList.Entries[0];
            Product product = FindFreeProduct(type);
            Assert.IsNotNull(product, $"No hay {type} libre en la escena.");

            // Agarrar: el producto queda cinemático, sin colisiones y fuera de la canasta.
            yield return GrabProduct(product);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(product.IsHeld);
            Assert.IsTrue(product.Body.isKinematic);
            Assert.IsFalse(contents.Contains(product), "Un producto en la mano no debe contar como contenido.");
            Assert.AreEqual(0, shoppingList.GetCollected(type));

            // Soltar sobre la canasta: entra, suma peso y marca la lista.
            grabber.DropHeld();
            Assert.IsFalse(product.IsHeld);
            Assert.IsNull(product.transform.parent);
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(contents.Contains(product), "El producto soltado debería estar en la canasta.");
            Assert.AreEqual(product.Weight, contents.TotalWeight, 0.001f);
            Assert.Greater(cart.Body.mass, cart.BaseMass, "La masa efectiva debe aumentar con la carga.");
            Assert.AreEqual(1, shoppingList.GetCollected(type));

            // Volver a agarrarlo desde la canasta lo retira de la lista.
            Assert.IsTrue(grabber.TryGrabNearest());
            Assert.AreSame(product, grabber.HeldProduct);
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(contents.Contains(product));
            Assert.AreEqual(0f, contents.TotalWeight, 0.001f);
            Assert.AreEqual(0, shoppingList.GetCollected(type));

            // Lanzar desde fuera de la canasta: sale con velocidad y no sigue unido al carrito.
            grabber.SetHandLocalTarget(new Vector3(0f, 1.05f, 1.8f), true);
            yield return null;
            grabber.ThrowHeld();
            Assert.IsFalse(product.IsHeld);
            Assert.IsNull(product.transform.parent);
            Assert.IsFalse(product.Body.isKinematic);
            Assert.Greater(product.Body.linearVelocity.magnitude, 3f);
            yield return new WaitForSeconds(1.5f);
            Assert.IsFalse(contents.Contains(product), "El producto lanzado no debería volver a la canasta.");
        }

        [UnityTest]
        public IEnumerator Checkout_EvaluatesOnce_StopsTimer_AndRestarts()
        {
            yield return LoadGameScene();
            yield return WaitForState(GameState.EnJuego, 8f);

            // Con la canasta vacía la caja no termina la partida.
            Assert.AreEqual(CheckoutAttempt.EmptyCart, manager.TryCheckout());
            Assert.AreEqual(GameState.EnJuego, manager.State);

            Product product = FindFreeProduct(shoppingList.Entries[0]);
            yield return GrabProduct(product);
            grabber.DropHeld();
            yield return new WaitForSeconds(1.5f);
            Assert.GreaterOrEqual(contents.Count, 1);

            // Lleva la zona de entrega hasta el carrito para no mover los productos de la canasta.
            checkout.transform.position = cart.transform.position + Vector3.up;
            Physics.SyncTransforms();
            yield return WaitForState(GameState.CompraEntregada, 2f);

            ShoppingReceipt receipt = manager.LastReceipt;
            Assert.IsNotNull(receipt);
            Assert.IsTrue(receipt.Delivered);
            Assert.GreaterOrEqual(receipt.Correct, 1);
            Assert.IsTrue(checkout.Consumed);
            Assert.IsFalse(cart.ControlsEnabled);
            Assert.IsFalse(grabber.InputEnabled);
            Assert.IsNotNull(GameObject.Find("Recibo"), "El recibo debería estar visible.");

            float remaining = timer.Remaining;
            Assert.IsFalse(timer.IsRunning);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(remaining, timer.Remaining, 0.0001f, "El tiempo no debe seguir corriendo.");

            // Un segundo intento no vuelve a evaluar.
            Assert.AreEqual(CheckoutAttempt.NotPlaying, manager.TryCheckout());
            Assert.AreSame(receipt, manager.LastReceipt);

            // Reinicio: se recarga la escena con una partida nueva.
            GameManager oldManager = manager;
            manager.Restart();
            float waited = 0f;
            while ((manager == null || manager == oldManager) && waited < 5f)
            {
                waited += Time.deltaTime;
                yield return null;
                manager = Object.FindAnyObjectByType<GameManager>();
            }
            Assert.IsNotNull(manager);
            Assert.AreNotSame(oldManager, manager);
            CacheSystems();
            Assert.AreEqual(GameState.Preparacion, manager.State);
            Assert.AreEqual(timer.Duration, timer.Remaining, 0.01f);
            Assert.IsNull(GameObject.Find("Recibo"));
        }

        [UnityTest]
        public IEnumerator TimeUp_StopsAtZero_AndClosesStore()
        {
            yield return LoadGameScene();
            yield return WaitForState(GameState.EnJuego, 8f);

            timer.SetRemaining(0.3f);
            yield return WaitForState(GameState.TiempoAgotado, 2f);

            Assert.AreEqual(0f, timer.Remaining);
            Assert.IsFalse(timer.IsRunning);
            Assert.IsNotNull(manager.LastReceipt);
            Assert.IsTrue(manager.LastReceipt.TimedOut);
            Assert.AreEqual(0, manager.LastReceipt.Score);
            Assert.AreEqual(CheckoutAttempt.NotPlaying, manager.TryCheckout());
            Assert.IsNotNull(GameObject.Find("Recibo"));

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, timer.Remaining, "El tiempo nunca debe ser negativo ni reiniciarse solo.");
        }

        [UnityTest]
        public IEnumerator Cart_AcceleratesWithKeyboard_AndRespectsSpeedLimit()
        {
            InputSettings.EditorInputBehaviorInPlayMode previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSettings.BackgroundBehavior previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("TecladoPrueba");
            keyboard.MakeCurrent();

            try
            {
                yield return LoadGameScene();
                yield return WaitForState(GameState.EnJuego, 8f);

                Vector3 start = cart.transform.position;
                Vector3 forward = cart.transform.forward;

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                float maxObserved = 0f;
                float elapsed = 0f;
                while (elapsed < 2f)
                {
                    elapsed += Time.deltaTime;
                    Vector3 v = cart.Body.linearVelocity;
                    maxObserved = Mathf.Max(maxObserved, new Vector2(v.x, v.z).magnitude);
                    yield return null;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());

                float advanced = Vector3.Dot(cart.transform.position - start, forward);
                Assert.Greater(advanced, 1f, "El carrito debería avanzar con W.");
                Assert.LessOrEqual(maxObserved, cart.MaxSpeed * 1.25f + 0.1f, "Se superó el límite de velocidad.");

                // Sin acelerador, el carrito pierde velocidad.
                float speedBefore = cart.Body.linearVelocity.magnitude;
                yield return new WaitForSeconds(1f);
                Assert.Less(cart.Body.linearVelocity.magnitude, speedBefore);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
                InputSystem.settings.backgroundBehavior = previousBackground;
            }
        }
    }
}
