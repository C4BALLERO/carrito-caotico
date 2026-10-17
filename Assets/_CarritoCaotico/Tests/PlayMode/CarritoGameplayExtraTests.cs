using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarritoCaotico.Tests
{
    /// <summary>Límites del brazo, peso de la carga, pausa del reloj, siguiente nivel y canasta vacía.</summary>
    public class CarritoGameplayExtraTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

        GameManager manager;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameInput.Scheme = ControlScheme.Normal;
            GameSession.ReturnToMenu();
        }

        IEnumerator StartLevel(int level)
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
            GameSession.SetSeed(1);
            GameSession.Begin(level, StoreId.Clasico);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            manager = Object.FindAnyObjectByType<GameManager>();
            float t = 0f;
            while (manager.State != GameState.EnJuego && t < 8f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(GameState.EnJuego, manager.State);
        }

        [UnityTest]
        public IEnumerator Hand_StaysInsideItsArea()
        {
            yield return StartLevel(1);
            GrabberArm grabber = manager.Grabber;
            grabber.SetHandLocalTarget(new Vector3(50f, 9f, 50f), true);
            Assert.AreEqual(2.3f, grabber.HandLocalPosition.x, 0.001f, "Límite lateral.");
            Assert.AreEqual(2.0f, grabber.HandLocalPosition.z, 0.001f, "Límite hacia delante.");
            Assert.AreEqual(1.05f, grabber.HandLocalPosition.y, 0.001f, "La altura de la mano es fija.");
            grabber.SetHandLocalTarget(new Vector3(-50f, 0f, -50f), true);
            Assert.AreEqual(-2.3f, grabber.HandLocalPosition.x, 0.001f);
            Assert.AreEqual(-0.4f, grabber.HandLocalPosition.z, 0.001f, "Límite hacia atrás.");
        }

        [UnityTest]
        public IEnumerator Cargo_MakesTheCartHeavier()
        {
            yield return StartLevel(1);
            CartController cart = manager.Cart;
            float empty = cart.Body.mass;
            cart.SetCargoWeight(4f);
            Assert.AreEqual(cart.BaseMass + 4f * 1.5f, cart.Body.mass, 0.001f);
            Assert.Greater(cart.Body.mass, empty);
            cart.SetCargoWeight(-3f);
            Assert.AreEqual(cart.BaseMass, cart.Body.mass, 0.001f, "Un peso negativo cuenta como cero.");
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTheTimer()
        {
            yield return StartLevel(1);
            manager.SetPaused(true);
            float remaining = manager.Timer.Remaining;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(remaining, manager.Timer.Remaining, 0.0001f, "En pausa el reloj no avanza.");
            Assert.IsFalse(manager.Grabber.InputEnabled, "Ni los jugadores se mueven.");
            manager.SetPaused(false);
            yield return new WaitForSeconds(0.3f);
            Assert.Less(manager.Timer.Remaining, remaining);
        }

        [UnityTest]
        public IEnumerator EmptyCart_IsRejectedAtCheckout()
        {
            yield return StartLevel(1);
            Assert.AreEqual(CheckoutAttempt.EmptyCart, manager.TryCheckout());
            Assert.AreEqual(GameState.EnJuego, manager.State, "Con la canasta vacía la partida sigue.");
        }

        [UnityTest]
        public IEnumerator Restart_KeepsTheSameControlScheme()
        {
            // Desde el nivel 4 el esquema puede cambiar, pero reintentar no lo cambia.
            yield return StartLevel(LevelProgression.ControlSwapStartLevel + 2);
            ControlScheme scheme = GameInput.Scheme;
            int level = manager.CurrentLevel;
            manager.Restart();
            float t = 0f;
            while ((LoadingScreen.IsLoading || Object.FindAnyObjectByType<GameManager>() == manager) && t < 8f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            manager = Object.FindAnyObjectByType<GameManager>();
            t = 0f;
            while (manager.State != GameState.EnJuego && t < 8f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(level, manager.CurrentLevel);
            Assert.AreEqual(scheme, GameInput.Scheme);
        }
    }
}
