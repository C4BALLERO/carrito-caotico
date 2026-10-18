using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarritoCaotico.Tests
{
    /// <summary>La mano del Agarrador no atraviesa obstáculos fijos ni agarra a través de ellos.</summary>
    public class CarritoGrabberObstacleTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

        [UnityTest]
        public IEnumerator Hand_StopsAtWallAndCannotGrabBehindIt()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
            GameSession.SetSeed(1);
            GameSession.Begin(1);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;

            var cart = Object.FindAnyObjectByType<CartController>();
            var grabber = Object.FindAnyObjectByType<GrabberArm>();
            Transform t = cart.transform;
            cart.Body.isKinematic = true;

            // Sin obstáculos, la mano llega al lateral.
            var sideTarget = new Vector3(1.6f, 1.05f, 0.8f);
            grabber.SetHandLocalTarget(sideTarget, true);
            yield return null;
            Assert.AreEqual(sideTarget.x, t.InverseTransformPoint(grabber.HandPosition).x, 0.05f, "Sin obstáculos la mano llega al objetivo.");

            // Pared fija (sin Rigidbody) entre el carrito y un producto al alcance del radio de agarre.
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetPositionAndRotation(t.TransformPoint(new Vector3(1.0f, 1f, 0.8f)), t.rotation);
            wall.transform.localScale = new Vector3(0.08f, 2f, 3f);

            Product product = null;
            foreach (Product candidate in Object.FindObjectsByType<Product>())
            {
                if (!candidate.IsHeld) { product = candidate; break; }
            }
            Assert.IsNotNull(product);
            product.Body.isKinematic = true;
            product.Body.position = t.TransformPoint(new Vector3(1.3f, 1.0f, 0.8f));
            product.transform.position = product.Body.position;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            grabber.SetHandLocalTarget(sideTarget, true);
            yield return null;
            float stoppedX = t.InverseTransformPoint(grabber.HandPosition).x;
            Assert.Less(stoppedX, 0.96f, "La mano se detiene delante de la pared.");
            Assert.Greater(stoppedX, 0.7f, "La mano llega hasta la pared.");
            Assert.IsNull(grabber.Candidate, "El producto de detrás de la pared no se ofrece para agarrar.");
            Assert.IsFalse(grabber.TryGrabNearest(), "No se agarra a través de la pared.");

            // Sin la pared, el mismo producto sí se alcanza.
            Object.Destroy(wall);
            yield return new WaitForFixedUpdate();
            grabber.SetHandLocalTarget(new Vector3(1.3f, 1.05f, 0.8f), true);
            yield return null;
            Assert.IsTrue(grabber.TryGrabNearest(), "Sin obstáculo el producto se puede agarrar.");
            Assert.AreSame(product, grabber.HeldProduct);
        }
    }
}
