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
    /// <summary>Pruebas del menú, la pausa, los compradores rivales y el cambio de controles.</summary>
    public class CarritoFeatureTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

        GameManager manager;

        IEnumerator Load(int level, bool direct)
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
            GameSession.SetSeed(1);
            if (direct) GameSession.Begin(level);
            else GameSession.ReturnToMenu();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            manager = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(manager);
        }

        IEnumerator WaitForState(GameState state, float timeout)
        {
            float elapsed = 0f;
            while (manager.State != state && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(state, manager.State);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameInput.Scheme = ControlScheme.Normal;
        }

        [UnityTest]
        public IEnumerator Menu_ShowsOnStart_AndPlayStartsLevel()
        {
            yield return Load(1, direct: false);
            yield return null;

            Assert.AreEqual(GameState.Menu, manager.State);
            Assert.IsNotNull(GameObject.Find("Menu_Principal"), "El menú principal debería estar visible.");
            Assert.IsNull(GameObject.Find("Juego"), "Los paneles de juego se ocultan en el menú.");
            Assert.IsFalse(Object.FindAnyObjectByType<GameTimer>().IsRunning);

            manager.StartGame(1);
            yield return null;
            Assert.AreEqual(GameState.Preparacion, manager.State);
            Assert.IsNull(GameObject.Find("Menu_Principal"));
            Assert.IsNotNull(GameObject.Find("Juego"));
            yield return WaitForState(GameState.EnJuego, 8f);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTimeTimerAndCheckout()
        {
            yield return Load(1, direct: true);
            yield return WaitForState(GameState.EnJuego, 8f);
            var timer = Object.FindAnyObjectByType<GameTimer>();
            var cart = Object.FindAnyObjectByType<CartController>();

            manager.SetPaused(true);
            Assert.IsTrue(manager.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(cart.ControlsEnabled);
            Assert.IsNotNull(GameObject.Find("Pausa"), "El menú de pausa debería estar visible.");

            float remaining = timer.Remaining;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(remaining, timer.Remaining, 0.0001f, "El tiempo no debe correr en pausa.");
            Assert.AreEqual(CheckoutAttempt.NotPlaying, manager.TryCheckout());

            manager.SetPaused(false);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(cart.ControlsEnabled);
            Assert.IsNull(GameObject.Find("Pausa"));
            yield return new WaitForSeconds(0.3f);
            Assert.Less(timer.Remaining, remaining);
        }

        [UnityTest]
        public IEnumerator Rivals_TakeProducts_ButListStaysCompletable()
        {
            yield return Load(6, direct: true);
            yield return WaitForState(GameState.EnJuego, 8f);
            Assert.Greater(manager.CurrentConfig.RivalCount, 0);

            var rivals = Object.FindAnyObjectByType<RivalManager>();
            var list = Object.FindAnyObjectByType<ShoppingList>();
            float elapsed = 0f;
            while (rivals.TakenCount < 1 && elapsed < 40f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.GreaterOrEqual(rivals.TakenCount, 1, "Un rival debería llevarse al menos un producto.");
            Assert.Greater(rivals.ActiveCount, 0);

            int taken = 0;
            foreach (Product product in Object.FindObjectsByType<Product>())
            {
                if (!product.IsTakenByRival) continue;
                taken++;
                Assert.IsFalse(product.IsOnShelf);
            }
            Assert.AreEqual(rivals.TakenCount, taken);

            // Regla justa: siempre quedan en las baldas las unidades que faltan de la lista.
            foreach (ProductType type in list.Entries)
            {
                int onShelf = 0;
                foreach (Product product in Object.FindObjectsByType<Product>())
                    if (product.Type == type && product.IsOnShelf) onShelf++;
                Assert.GreaterOrEqual(onShelf, list.GetMissing(type), $"Faltan {type} en las baldas para completar la lista.");
            }
        }

        [UnityTest]
        public IEnumerator SwappedControls_PusherUsesArrows()
        {
            InputSettings.EditorInputBehaviorInPlayMode previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSettings.BackgroundBehavior previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("TecladoPruebaControles");
            keyboard.MakeCurrent();

            try
            {
                yield return Load(1, direct: true);
                yield return WaitForState(GameState.EnJuego, 8f);
                var cart = Object.FindAnyObjectByType<CartController>();
                GameInput.Scheme = ControlScheme.Intercambiado;

                // Con W el carrito ya no acelera (W es del Agarrador).
                Vector3 start = cart.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(0.8f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.Less(Vector3.Distance(start, cart.transform.position), 0.3f, "W no debe mover el carrito con controles intercambiados.");

                // Con la flecha arriba sí.
                start = cart.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
                yield return new WaitForSeconds(1.5f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.Greater(Vector3.Dot(cart.transform.position - start, cart.transform.forward), 1f, "La flecha arriba debe mover el carrito.");
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
