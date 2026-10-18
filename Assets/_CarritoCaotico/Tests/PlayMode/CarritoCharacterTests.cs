using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarritoCaotico.Tests
{
    /// <summary>Pruebas de los personajes del carrito: modelos, bamboleo, brazo que señala y selector.</summary>
    public class CarritoCharacterTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

        CharacterId savedPusher;
        CharacterId savedGrabber;

        [SetUp]
        public void SetUp()
        {
            // La elección se guarda en PlayerPrefs: se fija para la prueba y se restaura después.
            savedPusher = CharacterChoice.Pusher;
            savedGrabber = CharacterChoice.Grabber;
            // Estas pruebas revisan el pingüino y el zorro (los KayKit tienen las suyas).
            CharacterChoice.Pusher = CharacterId.Pinguino;
            CharacterChoice.Grabber = CharacterId.Zorro;
        }

        [TearDown]
        public void TearDown()
        {
            CharacterChoice.Pusher = savedPusher;
            CharacterChoice.Grabber = savedGrabber;
        }

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

        /// <summary>Ángulo entre el brazo derecho del Agarrador activo y la mano extensible.</summary>
        static float GrabberArmAngle(GameObject grabber, string upperArmBone, string handBone)
        {
            Transform upperArm = FindDeep(grabber.transform, upperArmBone);
            Transform hand = FindDeep(grabber.transform, handBone);
            Transform target = FindDeep(Object.FindAnyObjectByType<CartController>().transform, "Mano");
            Assert.IsNotNull(target, "Falta la mano extensible del carrito.");
            return Vector3.Angle(hand.position - upperArm.position, target.position - upperArm.position);
        }

        [UnityTest]
        public IEnumerator Penguin_HasModelAndWaddlesWhenCartMoves()
        {
            yield return Load();

            GameObject penguin = GameObject.Find("Empujador_Pinguino");
            SkinnedMeshRenderer skin = CheckModel(penguin, "pingüino");
            var waddle = penguin.GetComponent<CharacterWaddle>();
            Assert.IsNotNull(waddle);
            // Empujando, las aletas sujetan el asa (CartPusherRig) en lugar de agitarse.
            Assert.IsNotNull(penguin.GetComponent<CartPusherRig>(), "El pingüino camina y sujeta el asa.");

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
            Assert.Less(GrabberArmAngle(fox, "Right_UpperArm", "Right_Hand"), 5f, "El brazo derecho apunta a la mano extensible.");
        }

        [UnityTest]
        public IEnumerator Selector_SwapsCharactersLive()
        {
            yield return Load();
            var crew = Object.FindAnyObjectByType<CartCrew>();
            Assert.IsNotNull(crew, "El carrito tiene su tripulación.");
            Assert.AreEqual("Empujador_Pinguino", crew.ActivePusher.name);
            Assert.AreEqual("Agarrador_Zorro", crew.ActiveGrabber.name);

            // Se cambia en el menú: el carrito cambia al momento y solo hay uno activo por puesto.
            CharacterChoice.Pusher = CharacterId.Zorro;
            CharacterChoice.Grabber = CharacterId.Cerdo;
            yield return null;

            Assert.IsTrue(GameObject.Find("Empujador_Zorro") != null && GameObject.Find("Empujador_Pinguino") == null);
            GameObject pig = GameObject.Find("Agarrador_Cerdo");
            CheckModel(pig, "cerdo");
            Assert.IsNull(GameObject.Find("Agarrador_Zorro"));
            Assert.Less(GrabberArmAngle(pig, "mixamorig:RightArm", "mixamorig:RightHand"), 5f, "El cerdo también señala su mano extensible.");
        }
    }
}
