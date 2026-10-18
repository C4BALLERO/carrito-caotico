using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarritoCaotico.Tests
{
    /// <summary>Cuando el carrito explota, la explosión se ve antes de que aparezca el recibo.</summary>
    public class CarritoExplosionTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

        [UnityTest]
        public IEnumerator Explosion_IsVisibleBeforeTheReceipt()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
            GameSession.SetSeed(1);
            GameSession.Begin(1);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            var manager = Object.FindAnyObjectByType<GameManager>();
            for (float elapsed = 0f; manager.State != GameState.EnJuego && elapsed < 8f; elapsed += Time.unscaledDeltaTime)
                yield return null;
            Assert.AreEqual(GameState.EnJuego, manager.State);

            Object.FindAnyObjectByType<CartHealth>().Explode();
            yield return null;

            Assert.AreEqual(GameState.CarritoDestruido, manager.State, "La partida termina al explotar.");
            Assert.IsNotNull(Object.FindAnyObjectByType<ExplosionEffect>(), "Se ve el efecto de explosión.");
            Assert.IsNull(GameObject.Find("Recibo"), "El recibo no tapa la explosión nada más estallar.");

            yield return new WaitForSecondsRealtime(1f);
            Assert.IsNull(GameObject.Find("Recibo"), "Al segundo todavía se ve la explosión.");

            yield return new WaitForSecondsRealtime(1.6f);
            Assert.IsNotNull(GameObject.Find("Recibo"), "Después de la explosión aparece el recibo.");
        }
    }
}
