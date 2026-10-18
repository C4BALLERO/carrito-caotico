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
    /// En un solo PC la lista va plegada durante la partida: el Agarrador la despliega
    /// manteniendo su tecla y, mientras la mira, el brazo se queda quieto.
    /// </summary>
    public class CarritoListaOcultaTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

        [TearDown]
        public void TearDown()
        {
            GameInput.Scheme = ControlScheme.Normal;
        }

        static IEnumerator WaitForState(GameManager manager, GameState state, float timeout)
        {
            for (float elapsed = 0f; manager.State != state && elapsed < timeout; elapsed += Time.unscaledDeltaTime)
                yield return null;
            Assert.AreEqual(state, manager.State);
        }

        static IEnumerator Hold(Keyboard keyboard, float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return new WaitForSeconds(seconds);
        }

        [UnityTest]
        public IEnumerator List_FoldsInPlay_AndGrabberHoldsKeyToRead()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
            var previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("TecladoPruebaLista");
            keyboard.MakeCurrent();

            try
            {
                GameSession.SetSeed(1);
                GameSession.Begin(1);
#if UNITY_EDITOR
                yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
                yield return null;
                var manager = Object.FindAnyObjectByType<GameManager>();
                var ui = Object.FindAnyObjectByType<GameUI>();
                var grabber = Object.FindAnyObjectByType<GrabberArm>();

                // Preparación: la lista se ve entera para memorizarla.
                Assert.AreEqual(GameState.Preparacion, manager.State);
                Assert.IsFalse(ui.IsListHidden, "En la preparación la lista se ve entera.");

                // En partida se pliega.
                yield return WaitForState(manager, GameState.EnJuego, 8f);
                yield return null;
                Assert.IsTrue(ui.IsListHidden, "En partida la lista va plegada.");

                // Shift der. la despliega y congela el brazo, aunque se pulse una flecha.
                Vector3 hand = grabber.HandLocalPosition;
                yield return Hold(keyboard, 0.5f, Key.RightShift, Key.RightArrow);
                Assert.IsFalse(ui.IsListHidden, "Manteniendo Shift der. la lista se despliega.");
                Assert.IsTrue(grabber.IsReadingList);
                Assert.Less(Vector3.Distance(hand, grabber.HandLocalPosition), 0.05f, "Mientras mira la lista el brazo no se mueve.");

                // Al soltar se pliega y el brazo vuelve a moverse.
                yield return Hold(keyboard, 0.5f, Key.RightArrow);
                Assert.IsTrue(ui.IsListHidden, "Al soltar la tecla la lista se pliega.");
                Assert.IsFalse(grabber.IsReadingList);
                Assert.Greater(Vector3.Distance(hand, grabber.HandLocalPosition), 0.2f, "Sin mirar la lista el brazo se mueve.");

                // Con Intercambiado la tecla es Shift izq. (Shift der. es del Empujador).
                GameInput.Scheme = ControlScheme.Intercambiado;
                yield return Hold(keyboard, 0.2f, Key.RightShift);
                Assert.IsTrue(ui.IsListHidden, "Con Intercambiado, Shift der. no abre la lista.");
                yield return Hold(keyboard, 0.2f, Key.LeftShift);
                Assert.IsFalse(ui.IsListHidden, "Con Intercambiado la lista se abre con Shift izq.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
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
