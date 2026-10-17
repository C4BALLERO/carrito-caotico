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
    /// <summary>Un jugador con teclado y el otro con mando.</summary>
    public class CarritoGamepadTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

        PadAssignment previousMode;
        InputSettings.EditorInputBehaviorInPlayMode previousBehavior;
        InputSettings.BackgroundBehavior previousBackground;
        Gamepad pad;
        Keyboard keyboard;

        [SetUp]
        public void SetUp()
        {
            previousMode = GameInput.PadMode;
            previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            // Las pruebas solo funcionan con exactamente un mando: el virtual.
            if (Gamepad.all.Count > 0) Assert.Ignore("Hay un mando real conectado: desconéctalo para esta prueba.");
            pad = InputSystem.AddDevice<Gamepad>("MandoPrueba");
            keyboard = InputSystem.AddDevice<Keyboard>("TecladoPruebaMando");
            keyboard.MakeCurrent();
        }

        [TearDown]
        public void TearDown()
        {
            if (pad != null) InputSystem.RemoveDevice(pad);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            GameInput.PadMode = previousMode;
            GameInput.Scheme = ControlScheme.Normal;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
            InputSystem.settings.backgroundBehavior = previousBackground;
        }

        IEnumerator LoadAndPlay()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
            GameSession.SetSeed(1);
            GameSession.Begin(1, StoreId.Clasico);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            var manager = Object.FindAnyObjectByType<GameManager>();
            float t = 0f;
            while (manager.State != GameState.EnJuego && t < 8f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(GameState.EnJuego, manager.State);
        }

        [UnityTest]
        public IEnumerator GrabberOnGamepad_PusherOnKeyboard()
        {
            GameInput.PadMode = PadAssignment.Agarrador;
            yield return LoadAndPlay();
            Assert.IsTrue(GameInput.GrabberUsesGamepad);
            Assert.IsFalse(GameInput.PusherUsesGamepad);
            Assert.AreEqual("A", GameInput.GrabberKeyLabels(ControlScheme.Normal)[1], "El HUD debería mostrar los botones del mando al Agarrador.");
            Assert.AreEqual("W", GameInput.PusherKeyLabels(ControlScheme.Normal)[0]);

            var cart = Object.FindAnyObjectByType<CartController>();
            var grabber = Object.FindAnyObjectByType<GrabberArm>();

            // El stick del mando mueve la mano y el gatillo no acelera el carrito.
            Vector3 startHand = grabber.HandLocalPosition;
            Vector3 startCart = cart.transform.position;
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(1f, 0f), rightTrigger = 1f });
            yield return new WaitForSeconds(0.8f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            Assert.Greater(grabber.HandLocalPosition.x - startHand.x, 0.8f, "El stick del mando debería mover la mano.");
            Assert.Less(Vector3.Distance(startCart, cart.transform.position), 0.3f, "El gatillo del Agarrador no debe mover el carrito.");

            // El teclado sigue moviendo el carrito.
            startCart = cart.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(1.2f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.Greater(Vector3.Dot(cart.transform.position - startCart, cart.transform.forward), 0.8f, "W debería mover el carrito.");
        }

        [UnityTest]
        public IEnumerator PusherOnGamepad_TriggerDrivesCart()
        {
            GameInput.PadMode = PadAssignment.Empujador;
            yield return LoadAndPlay();
            Assert.IsTrue(GameInput.PusherUsesGamepad);
            Assert.IsFalse(GameInput.GrabberUsesGamepad);
            Assert.AreEqual("RT", GameInput.PusherKeyLabels(ControlScheme.Normal)[0]);
            Assert.AreEqual("Stick der.", GameInput.CameraLabel);

            var cart = Object.FindAnyObjectByType<CartController>();
            Vector3 startCart = cart.transform.position;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f });
            yield return new WaitForSeconds(1.5f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            Assert.Greater(Vector3.Dot(cart.transform.position - startCart, cart.transform.forward), 1f, "El gatillo RT debería acelerar el carrito.");

            // Con controles intercambiados el mando pasa al Agarrador.
            GameInput.Scheme = ControlScheme.Intercambiado;
            Assert.IsTrue(GameInput.GrabberUsesGamepad);
            Assert.IsFalse(GameInput.PusherUsesGamepad);
        }
    }
}
