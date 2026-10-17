using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarritoCaotico.Tests
{
    /// <summary>Pruebas del pingüino Empujador: modelo, textura y bamboleo al moverse.</summary>
    public class CarritoCharacterTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

        [UnityTest]
        public IEnumerator Penguin_HasModelAndWaddlesWhenCartMoves()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
            GameSession.SetSeed(1);
            GameSession.Begin(1);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;

            var waddle = Object.FindAnyObjectByType<PenguinWaddle>();
            Assert.IsNotNull(waddle, "El Empujador debería ser el pingüino.");
            var skin = waddle.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(skin, "El pingüino conserva sus huesos.");
            Assert.IsNotNull(skin.sharedMaterial.mainTexture, "El material del pingüino tiene su textura.");
            Assert.IsTrue(waddle.transform.IsChildOf(Object.FindAnyObjectByType<CartController>().transform));

            Transform model = skin.transform.parent;
            Quaternion rest = model.localRotation;
            Assert.Less(waddle.Intensity, 0.01f, "Parado no se bambolea.");

            var body = Object.FindAnyObjectByType<CartController>().GetComponent<Rigidbody>();
            float maxAngle = 0f;
            // El bamboleo sube en unos 0,25 s; se espera tiempo de juego, no fotogramas.
            for (float elapsed = 0f; elapsed < 0.8f; elapsed += Time.deltaTime)
            {
                body.linearVelocity = body.transform.forward * 3f;
                yield return null;
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(rest, model.localRotation));
            }

            Assert.Greater(waddle.Intensity, 0.5f, "En marcha el bamboleo sube.");
            Assert.Greater(maxAngle, 2f, "El modelo se balancea de lado a lado.");
        }
    }
}
