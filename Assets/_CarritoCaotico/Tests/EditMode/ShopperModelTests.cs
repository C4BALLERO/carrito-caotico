using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CarritoCaotico.Tests
{
    /// <summary>Compradores generados: esqueleto completo, variedad, piezas sin colisión y referencias conectadas.</summary>
    public class ShopperModelTests
    {
        const string Folder = "Assets/_CarritoCaotico/02_Prefabs/Compradores";

        static List<GameObject> Prefabs()
        {
            if (!Directory.Exists(Folder)) Assert.Ignore("Los compradores no están generados.");
            var result = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
                result.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            return result;
        }

        [Test]
        public void EveryShopper_HasACompleteSkeletonAndItsSystems()
        {
            List<GameObject> prefabs = Prefabs();
            Assert.AreEqual(16, prefabs.Count, "Ocho compradores por supermercado.");
            foreach (GameObject prefab in prefabs)
            {
                var rig = prefab.GetComponentInChildren<ShopperRig>();
                Assert.IsNotNull(rig, $"{prefab.name}: sin esqueleto.");
                Assert.IsTrue(rig.IsComplete, $"{prefab.name}: faltan huesos.");
                Transform[] bones = rig.Skeleton;
                for (int i = 0; i < bones.Length; i++)
                    Assert.AreEqual(ShopperRig.BoneNames[i], bones[i].name, $"{prefab.name}: hueso {i} mal conectado.");

                // Jerarquía coherente: cada hueso cuelga del que le corresponde.
                Assert.AreEqual(rig.hips, rig.spine.parent);
                Assert.AreEqual(rig.chest, rig.neck.parent);
                Assert.AreEqual(rig.neck, rig.head.parent);
                Assert.AreEqual(rig.leftUpperArm, rig.leftLowerArm.parent);
                Assert.AreEqual(rig.rightLowerLeg, rig.rightFoot.parent);
                Assert.Greater(rig.head.position.y, rig.chest.position.y);
                Assert.Greater(rig.chest.position.y, rig.hips.position.y);
                Assert.Greater(rig.hips.position.y, rig.leftFoot.position.y);

                Assert.IsNotNull(rig.leftEye, $"{prefab.name}: sin ojos.");
                Assert.IsNotNull(rig.mouth, $"{prefab.name}: sin boca.");
                Assert.IsTrue(rig.HasGrips, $"{prefab.name}: sin agarres en el carrito.");
                Assert.IsNotNull(prefab.GetComponent<ShopperAnimator>(), $"{prefab.name}: sin animación.");
                Assert.IsNotNull(prefab.GetComponent<RivalShopper>(), $"{prefab.name}: sin IA.");

                // Solo la cápsula del cuerpo choca; las piezas visuales no.
                Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
                Assert.AreEqual(1, colliders.Length, $"{prefab.name}: las piezas no deberían tener colisión.");
            }
        }

        [Test]
        public void Shoppers_AreVaried()
        {
            var heights = new List<float>();
            var looks = new HashSet<string>();
            foreach (GameObject prefab in Prefabs())
            {
                if (prefab.name.Contains("Halloween")) continue;
                var rig = prefab.GetComponentInChildren<ShopperRig>();
                heights.Add(rig.transform.localScale.y);
                var renderer = rig.chest.GetComponentInChildren<Renderer>();
                looks.Add(renderer.sharedMaterial.name);
            }
            heights.Sort();
            Assert.Greater(heights[heights.Count - 1] - heights[0], 0.25f, "Hay compradores bajos y altos.");
            Assert.GreaterOrEqual(looks.Count, 7, "Cada comprador viste distinto.");
        }

        [Test]
        public void HalloweenShoppers_WearCostumes()
        {
            int costumes = 0;
            foreach (GameObject prefab in Prefabs())
            {
                if (!prefab.name.Contains("Halloween")) continue;
                foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Sombrero" || t.name == "Capa" || t.name.StartsWith("Cuerno")) { costumes++; break; }
            }
            Assert.AreEqual(8, costumes, "Todos los compradores de Halloween van disfrazados.");
        }
    }
}
