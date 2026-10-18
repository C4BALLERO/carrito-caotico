using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarritoCaotico.Tests
{
    /// <summary>Pruebas de los personajes del carrito: pingüino Empujador y zorro Agarrador.</summary>
    public class CarritoCharacterTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

        IEnumerator Load()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena de prueba no está generada.");
            GameSession.SetSeed(1);
            GameSession.Begin(1);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
        }

        static SkinnedMeshRenderer CheckModel(GameObject character, string label)
        {
            Assert.IsNotNull(character, $"Falta el {label}.");
            var skin = character.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(skin, $"El {label} conserva sus huesos.");
            Assert.IsNotNull(skin.sharedMaterial.mainTexture, $"El material del {label} tiene su textura.");
            Assert.IsTrue(character.transform.IsChildOf(Object.FindAnyObjectByType<CartController>().transform));
            return skin;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator Penguin_HasModelAndWaddlesWhenCartMoves()
        {
            yield return Load();

            GameObject penguin = GameObject.Find("Empujador_Pinguino");
            SkinnedMeshRenderer skin = CheckModel(penguin, "pingüino");
            var waddle = penguin.GetComponent<CharacterWaddle>();
            Assert.IsNotNull(waddle);
            Assert.IsNotNull(penguin.GetComponent<FlipperFlap>(), "El pingüino agita las aletas.");

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

        [UnityTest]
        public IEnumerator Fox_PointsAtGrabberHand()
        {
            yield return Load();

            GameObject fox = GameObject.Find("Agarrador_Zorro");
            CheckModel(fox, "zorro");
            Assert.IsNotNull(fox.GetComponent<CharacterWaddle>());
            Assert.IsNotNull(fox.GetComponent<ArmReach>(), "El zorro señala su mano extensible.");

            yield return null;
            Transform upperArm = FindDeep(fox.transform, "Right_UpperArm");
            Transform hand = FindDeep(fox.transform, "Right_Hand");
            Transform target = FindDeep(Object.FindAnyObjectByType<CartController>().transform, "Mano");
            Assert.IsNotNull(target, "Falta la mano extensible del carrito.");

            float angle = Vector3.Angle(hand.position - upperArm.position, target.position - upperArm.position);
            Assert.Less(angle, 5f, "El brazo derecho apunta a la mano extensible.");
        }
    }
}
