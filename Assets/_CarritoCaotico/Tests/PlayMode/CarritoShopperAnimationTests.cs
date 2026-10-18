using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarritoCaotico.Tests
{
    /// <summary>
    /// Animación de los compradores y del Empujador medida en el mundo: pies que no patinan ni
    /// flotan, pasos al girar y al frenar, mano que alcanza el producto agachándose, manos en el
    /// asa, rivales que guardan distancia y cogen el producto con la mano.
    /// </summary>
    public class CarritoShopperAnimationTests
    {
        const string ScenePath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";
        const string PrefabFolder = "Assets/_CarritoCaotico/02_Prefabs/Compradores";

        GameObject shopper;
        GameObject floor;

        sealed class FakeBrain : IShopperBrain
        {
            public ShopperAction Action { get; set; }
            public Vector3 ReachPoint { get; set; }
            public float ReachProgress { get; set; }
        }

        [TearDown]
        public void TearDown()
        {
            if (shopper != null) Object.Destroy(shopper);
            if (floor != null) Object.Destroy(floor);
            GameSession.ReturnToMenu();
            Time.timeScale = 1f;
        }

        static GameObject LoadPrefab(string name)
        {
#if UNITY_EDITOR
            string path = $"{PrefabFolder}/Comprador_{name}.prefab";
            if (!File.Exists(path)) Assert.Ignore($"No existe {path}: genera las escenas.");
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            Assert.Ignore("Solo en el Editor.");
            return null;
#endif
        }

        IEnumerator Spawn(string name)
        {
            yield return SceneManager.LoadSceneAsync(0, LoadSceneMode.Single); // escena limpia del juego
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.position = new Vector3(100f, 0f, 100f);
            floor.transform.localScale = Vector3.one * 5f;
            shopper = Object.Instantiate(LoadPrefab(name), new Vector3(100f, 0f, 100f), Quaternion.identity);
            if (shopper.TryGetComponent(out ShopperAnimator procedural)) procedural.AlwaysAnimate = true;
            if (shopper.TryGetComponent(out ClipShopperAnimator clips)) clips.AlwaysAnimate = true;
            // La prueba mueve el transform a mano: sin interpolar, el Rigidbody no lo devuelve atrás.
            if (shopper.TryGetComponent(out Rigidbody body)) body.interpolation = RigidbodyInterpolation.None;
            yield return null;
            yield return null;
        }

        /// <summary>Recorre una distancia a velocidad constante y mide cuánto se mueve cada pie mientras está apoyado.</summary>
        IEnumerator Walk(Vector3 velocity, float seconds, List<float> plantedSlides, List<float> heights)
        {
            var animator = shopper.GetComponent<ShopperAnimator>();
            Transform leftFoot = animator.Rig.leftFoot;
            Transform rightFoot = animator.Rig.rightFoot;
            Vector3 lastLeft = animator.FootPosition(0);
            Vector3 lastRight = animator.FootPosition(1);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                shopper.transform.position += velocity * Time.deltaTime;
                yield return null;
                Vector3 left = animator.FootPosition(0);
                Vector3 right = animator.FootPosition(1);
                // Solo cuenta si el pie estaba apoyado en los dos fotogramas (el aterrizaje no es deslizar).
                if (Mathf.Approximately(left.y, 0f) && Mathf.Approximately(lastLeft.y, 0f)) plantedSlides.Add(Vector3.Distance(left, lastLeft));
                if (Mathf.Approximately(right.y, 0f) && Mathf.Approximately(lastRight.y, 0f)) plantedSlides.Add(Vector3.Distance(right, lastRight));
                lastLeft = left;
                lastRight = right;
                heights.Add(Mathf.Min(leftFoot.position.y, rightFoot.position.y));
            }
        }

        [UnityTest]
        public IEnumerator Walking_FeetStayPlantedAndOnTheGround()
        {
            yield return Spawn("Chica_Coleta");
            var animator = shopper.GetComponent<ShopperAnimator>();
            var slides = new List<float>();
            var heights = new List<float>();
            yield return Walk(Vector3.forward * 1.3f, 3f, slides, heights);

            Assert.Greater(animator.Steps, 5, "Debería dar pasos al caminar.");
            float worstSlide = 0f;
            foreach (float s in slides) worstSlide = Mathf.Max(worstSlide, s);
            Assert.Less(worstSlide, 0.005f, "Un pie apoyado no se desliza por el suelo.");

            // Los tobillos (pie IK) a la altura correcta: ni hundidos ni flotando.
            float scale = animator.Rig.transform.lossyScale.y;
            float ankle = animator.Rig.footHeight * scale;
            float lowest = float.MaxValue;
            foreach (float h in heights) lowest = Mathf.Min(lowest, h);
            Assert.AreEqual(ankle, lowest, 0.02f, "Al menos un pie toca el suelo en cada momento.");
            Assert.Greater(animator.Rig.head.position.y, 1.1f * scale, "La cabeza sigue arriba.");
        }

        [UnityTest]
        public IEnumerator TurningInPlace_StepsAroundAndStopsSettled()
        {
            yield return Spawn("Senor_Panza");
            var animator = shopper.GetComponent<ShopperAnimator>();
            int before = animator.Steps;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                shopper.transform.rotation *= Quaternion.Euler(0f, 120f * Time.deltaTime, 0f);
                yield return null;
            }
            Assert.Greater(animator.Steps, before + 1, "Al girar parado da pasos para acompañar el giro.");

            // Al detenerse, los pies acaban bajo las caderas.
            yield return new WaitForSeconds(1.2f);
            Vector3 center = shopper.transform.position;
            for (int i = 0; i < 2; i++)
            {
                Vector3 foot = animator.FootPosition(i);
                foot.y = 0f;
                Assert.Less(Vector3.Distance(foot, new Vector3(center.x, 0f, center.z)), 0.35f, "El pie se recoloca al detenerse.");
            }
        }

        [UnityTest]
        public IEnumerator Idle_HandsHoldTheCartHandle()
        {
            yield return Spawn("Abuelo_Bigotes");
            yield return new WaitForSeconds(0.3f);
            ShopperRig rig = shopper.GetComponent<ShopperAnimator>().Rig;
            Assert.Less(Vector3.Distance(rig.leftHand.position, rig.leftGrip.position), 0.05f, "Mano izquierda en el asa.");
            Assert.Less(Vector3.Distance(rig.rightHand.position, rig.rightGrip.position), 0.05f, "Mano derecha en el asa.");
        }

        [UnityTest]
        public IEnumerator Reaching_LowProduct_CrouchesAndTouchesIt()
        {
            yield return Spawn("Tipo_Tupe");
            var animator = shopper.GetComponent<ShopperAnimator>();
            var brain = new FakeBrain { Action = ShopperAction.Reaching, ReachPoint = shopper.transform.position + new Vector3(0.55f, 0.45f, 0.25f) };
            animator.SetBrain(brain);

            float closest = float.MaxValue;
            float deepest = 0f;
            for (float t = 0f; t < 2.4f; t += Time.deltaTime)
            {
                // Medio gesto: la mano va al producto y se queda allí.
                brain.ReachProgress = Mathf.Min(0.55f, t / 2f * 0.55f + 0.2f);
                yield return null;
                closest = Mathf.Min(closest, Vector3.Distance(animator.Rig.rightHand.position, brain.ReachPoint));
                deepest = Mathf.Max(deepest, animator.Crouch);
            }
            Assert.Less(closest, 0.15f, "La mano llega al producto.");
            Assert.Greater(deepest, 0.05f, "Se agacha para un producto bajo.");
        }

        static Transform Bone(GameObject root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>
        /// KayKit caminando a la velocidad base de los rivales (2,2 m/s): el clip va al ritmo medido
        /// y el pie apoyado se bloquea con IK, así que el pie más bajo apenas se mueve por el suelo
        /// comparado con lo que avanza el cuerpo (sin ritmo ni bloqueo era el 100 %).
        /// </summary>
        [UnityTest]
        public IEnumerator KayKit_WalkClipKeepsTheFootPlanted()
        {
            yield return Spawn("KayKit_Knight");
            var clips = shopper.GetComponent<ClipShopperAnimator>();
            Transform[] contacts = { Bone(shopper, "foot.l"), Bone(shopper, "foot.r") };
            const float speed = 2.2f;
            float slid = 0f, walked = 0f;
            Transform lastFoot = null;
            Vector3 last = Vector3.zero;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                shopper.transform.position += Vector3.forward * speed * Time.deltaTime;
                yield return null;
                if (t < 0.6f) continue; // deja que la animación entre en el ciclo de caminar
                Transform foot = contacts[0];
                foreach (Transform c in contacts) if (c.position.y < foot.position.y) foot = c;
                Vector3 now = Vector3.ProjectOnPlane(foot.position, Vector3.up);
                if (foot == lastFoot)
                {
                    slid += Vector3.Distance(now, last);
                    walked += speed * Time.deltaTime;
                }
                lastFoot = foot;
                last = now;
            }
            float ratio = slid / Mathf.Max(0.001f, walked);
            Debug.Log($"[Carrito] KayKit: el pie apoyado se desliza {ratio:P0} de lo que avanza el cuerpo (clip a {clips.WalkClipSpeed:0.00} m/s).");
            Assert.Less(ratio, 0.15f, "El pie apoyado casi no patina.");
        }

        [UnityTest]
        public IEnumerator KayKit_HoldsTheHandleAndReachesTheProduct()
        {
            yield return Spawn("KayKit_Skeleton_Minion");
            var clips = shopper.GetComponent<ClipShopperAnimator>();
            yield return new WaitForSeconds(0.4f);
            float leftGap = Vector3.Distance(clips.LeftHand.position, clips.LeftGrip.position);
            float rightGap = Vector3.Distance(clips.RightHand.position, clips.RightGrip.position);
            Debug.Log($"[Carrito] KayKit: manos a {leftGap:0.000} y {rightGap:0.000} m del asa.");
            Assert.Less(Mathf.Max(leftGap, rightGap), 0.06f, "Las manos sujetan el asa.");

            var brain = new FakeBrain { Action = ShopperAction.Reaching, ReachPoint = shopper.transform.position + new Vector3(0.45f, 0.7f, 0.3f) };
            clips.SetBrain(brain);
            float closest = float.MaxValue;
            for (float t = 0f; t < 2.4f; t += Time.deltaTime)
            {
                brain.ReachProgress = Mathf.Min(0.55f, t / 2f * 0.55f + 0.2f);
                yield return null;
                closest = Mathf.Min(closest, Vector3.Distance(clips.RightHand.position, brain.ReachPoint));
            }
            Debug.Log($"[Carrito] KayKit: la mano llega a {closest:0.000} m del producto.");
            Assert.Less(closest, 0.12f, "La mano llega al producto.");
            Assert.AreEqual(0f, clips.GripWeight, 0.01f, "Suelta el asa para coger el producto.");
        }

        [UnityTest]
        public IEnumerator Pusher_WalksBehindTheCartWithoutSliding()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena no está generada.");
            GameSession.SetSeed(1);
            GameSession.Begin(1, StoreId.Clasico);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            var manager = Object.FindAnyObjectByType<GameManager>();
            for (float t = 0f; manager.State != GameState.EnJuego && t < 8f; t += Time.deltaTime) yield return null;

            CartPusherRig pusher = null;
            foreach (CartPusherRig rig in Object.FindObjectsByType<CartPusherRig>())
                if (rig.isActiveAndEnabled) pusher = rig;
            Assert.IsNotNull(pusher, "El Empujador activo tiene CartPusherRig.");

            Rigidbody body = manager.Cart.Body;
            int before = pusher.Steps;
            float worstSlide = 0f;
            Vector3 lastLeft = pusher.FootPosition(0);
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                body.linearVelocity = body.transform.forward * 2.2f;
                yield return null;
                Vector3 left = pusher.FootPosition(0);
                if (Mathf.Abs(left.y - (body.position.y - 0.02f)) < 0.001f && Mathf.Abs(lastLeft.y - left.y) < 0.001f)
                    worstSlide = Mathf.Max(worstSlide, Vector3.Distance(left, lastLeft));
                lastLeft = left;
            }
            Assert.Greater(pusher.Steps, before + 3, "Da pasos mientras empuja.");
            Assert.Less(worstSlide, 0.01f, "El pie apoyado no patina.");
            // La mano llega al asa o, si el brazo es corto (el Cerdo), se estira del todo hacia ella.
            float shoulderToGrip = Vector3.Distance(pusher.LeftUpperArm.position, pusher.LeftGrip.position);
            float leftGap = Vector3.Distance(pusher.LeftHand.position, pusher.LeftGrip.position);
            float expected = Mathf.Max(0f, shoulderToGrip - pusher.ArmLength);
            Assert.Less(leftGap, expected + 0.06f, $"{pusher.name}: la mano va hacia el asa (brazo {pusher.ArmLength:0.00} m, asa a {shoulderToGrip:0.00} m).");
        }

        [UnityTest]
        public IEnumerator Rivals_KeepTheirDistanceAndGrabWithTheHand()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("La escena no está generada.");
            GameSession.SetSeed(3);
            GameSession.Begin(8, StoreId.Clasico);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            var manager = Object.FindAnyObjectByType<GameManager>();
            for (float t = 0f; manager.State != GameState.EnJuego && t < 8f; t += Time.deltaTime) yield return null;
            RivalManager rivals = manager.Rivals;
            Assert.Greater(manager.CurrentConfig.RivalCount, 1);

            float closest = float.MaxValue;
            bool heldInHand = false;
            var variants = new HashSet<int>();
            for (float t = 0f; t < 30f && (!heldInHand || rivals.ActiveCount < 2); t += Time.deltaTime)
            {
                yield return null;
                IReadOnlyList<RivalShopper> list = rivals.Rivals;
                for (int i = 0; i < list.Count; i++)
                {
                    variants.Add(list[i].Variant);
                    foreach (Product p in Object.FindObjectsByType<Product>())
                        if (p.IsTakenByRival && p.transform.parent != null && p.transform.parent.name.StartsWith("Mano_")) heldInHand = true;
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        if (list[i].CurrentSpeed < 0.1f && list[j].CurrentSpeed < 0.1f) continue;
                        closest = Mathf.Min(closest, Vector3.Distance(list[i].transform.position, list[j].transform.position));
                    }
                }
            }
            Assert.IsTrue(heldInHand, "Un rival coge el producto con la mano antes de dejarlo en su carrito.");
            Assert.Greater(variants.Count, 1, "Los rivales no son todos el mismo modelo.");
            if (closest < float.MaxValue) Assert.Greater(closest, 0.45f, "Dos rivales en marcha no se meten uno dentro de otro.");
        }
    }
}
