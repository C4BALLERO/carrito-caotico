using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CarritoCaotico.Tests
{
    /// <summary>Vida del carrito: choques, barra del HUD, explosión y tutorial sin daño.</summary>
    public class CarritoHealthTests
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

        static IEnumerator WaitUntil(System.Func<bool> condition, float timeout, string message)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(condition(), message);
        }

        IEnumerator Load(bool tutorial)
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
            GameSession.SetSeed(1);
            if (tutorial) GameSession.BeginTutorial();
            else GameSession.Begin(1, StoreId.Clasico);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            manager = Object.FindAnyObjectByType<GameManager>();
            yield return WaitUntil(() => manager.State == GameState.EnJuego, 8f, "La partida debería empezar.");
        }

        [UnityTest]
        public IEnumerator CrashIntoShelf_HurtsTheCart()
        {
            yield return Load(false);
            CartHealth health = manager.Health;
            Assert.IsNotNull(health, "El carrito tiene vida.");
            Assert.IsTrue(health.DamageEnabled);
            Assert.AreEqual(health.MaxHealth, health.Health);

            // Busca un estante o una pared delante y lanza el carrito contra él.
            Rigidbody body = manager.Cart.Body;
            Transform cart = manager.Cart.transform;
            Vector3 origin = cart.position + Vector3.up * 0.5f;
            Assert.IsTrue(Physics.Raycast(origin, cart.forward, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore), "Debería haber algo delante.");
            int guard = 0;
            while (hit.rigidbody != null && guard++ < 10)
                Assert.IsTrue(Physics.Raycast(hit.point + cart.forward * 0.2f, cart.forward, out hit, 60f, ~0, QueryTriggerInteraction.Ignore));
            Vector3 start = hit.point - cart.forward * 2.5f;
            body.position = new Vector3(start.x, body.position.y, start.z);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            body.linearVelocity = cart.forward * 8f;

            yield return WaitUntil(() => health.Health < health.MaxHealth, 2f, "Un choque fuerte quita vida.");
            Assert.Greater(health.Health, health.MaxHealth - 31f, "Pero nunca más del máximo por golpe.");
            CollectionAssert.Contains(GameAudio.Instance.History, "Hit", "El choque suena.");
        }

        [UnityTest]
        public IEnumerator Products_DoNotHurtTheCart()
        {
            yield return Load(false);
            CartHealth health = manager.Health;
            Product product = Object.FindObjectsByType<Product>()[0];
            Transform cart = manager.Cart.transform;
            product.Body.position = cart.position + cart.right * 1.5f + Vector3.up * 0.4f;
            Physics.SyncTransforms();
            product.Body.linearVelocity = -cart.right * 15f;
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(health.MaxHealth, health.Health, "Un producto lanzado no rompe el carrito.");
        }

        [UnityTest]
        public IEnumerator HealthBar_ShowsTheDamage()
        {
            yield return Load(false);
            var ui = Object.FindAnyObjectByType<GameUI>();
            GameObject panel = GameObject.Find("Vida_Carrito");
            Assert.IsNotNull(panel, "La barra de vida se ve durante la partida.");
            manager.Health.ApplyDamage(30f);
            yield return WaitUntil(() => Mathf.Abs(ui.ShownHealth - 0.7f) < 0.01f, 2f, "La barra baja hasta la vida real.");
            Text value = panel.transform.Find("Relleno/Valor").GetComponent<Text>();
            Assert.AreEqual("70%", value.text, "El número acompaña al color.");

            manager.Health.ApplyDamage(45f);
            yield return null;
            Assert.AreEqual("25%", value.text);
            Assert.IsTrue(GameObject.Find("Mensaje") != null, "Con poca vida aparece un aviso.");
        }

        [UnityTest]
        public IEnumerator Explosion_EndsTheGameAndBreaksTheCart()
        {
            yield return Load(false);
            manager.Health.ApplyDamage(500f);
            Assert.IsTrue(manager.Health.IsDestroyed);
            Assert.AreEqual(GameState.CarritoDestruido, manager.State, "Si el carrito explota se acaba la partida.");
            Assert.IsTrue(manager.IsGameOver);
            Assert.IsFalse(manager.LastReceipt.Delivered);
            Assert.IsFalse(manager.CanAdvance);
            Assert.IsNotNull(GameObject.Find("Explosion"), "Se ve la explosión.");
            CollectionAssert.Contains(GameAudio.Instance.History, "Explosion");
            Assert.IsFalse(manager.Grabber.enabled, "El brazo deja de funcionar.");

            int pieces = 0;
            foreach (Rigidbody rb in Object.FindObjectsByType<Rigidbody>())
                if (rb.name.StartsWith("Pieza_")) pieces++;
            Assert.Greater(pieces, 5, "Las piezas del carrito salen volando.");

            yield return WaitUntil(() => GameObject.Find("Recibo") != null, 2f, "Aparece el recibo.");
            Text title = GameObject.Find("Recibo").transform.Find("Papel/Relleno/Cabecera/Titulo").GetComponent<Text>();
            Assert.AreEqual("¡El carrito explotó!", title.text);
        }

        [UnityTest]
        public IEnumerator Tutorial_CartCannotBreak()
        {
            yield return Load(true);
            Assert.IsTrue(manager.IsTutorial);
            Assert.IsFalse(manager.Health.DamageEnabled, "En el tutorial el carrito no se rompe.");
            yield return null;
            GameObject panel = GameObject.Find("Vida_Carrito");
            Assert.IsTrue(panel == null || !panel.activeInHierarchy, "Ni se muestra la barra de vida.");
        }
    }
}
