using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarritoCaotico.Tests
{
    /// <summary>El audio funciona aunque falten clips en la biblioteca.</summary>
    public class CarritoAudioTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

        [UnityTest]
        public IEnumerator AudioDirector_CreatesSourcesAndSurvivesAFullRound()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
            GameSession.SetSeed(1);
            GameSession.Begin(1);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;

            var director = Object.FindAnyObjectByType<AudioDirector>();
            Assert.IsNotNull(director, "La escena tiene AudioDirector.");
            Assert.AreEqual(5, director.GetComponentsInChildren<AudioSource>().Length, "Música, ambiente, efectos, interfaz y ruedas.");

            // Pausa y conducción: ningún evento debe fallar aunque falten sonidos.
            var manager = Object.FindAnyObjectByType<GameManager>();
            manager.SetPaused(true);
            yield return null;
            Assert.IsTrue(AudioListener.pause, "La pausa silencia el juego.");
            manager.SetPaused(false);
            Assert.IsFalse(AudioListener.pause);

            var body = Object.FindAnyObjectByType<CartController>().Body;
            for (int i = 0; i < 10; i++)
            {
                body.linearVelocity = body.transform.forward * 4f;
                yield return null;
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
