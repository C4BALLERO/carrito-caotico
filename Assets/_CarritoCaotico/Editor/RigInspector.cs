using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Informe de los esqueletos de los modelos 3D: jerarquía de huesos, pesos de vértices,
    /// influencias por vértice, huesos sin peso, huesos estándar que faltan y animaciones del FBX.
    /// Menú: Carrito > Inspeccionar esqueletos. Escribe Docs/esqueletos.md.
    /// </summary>
    public static class RigInspector
    {
        public const string ReportPath = "Docs/esqueletos.md";

        /// <summary>Partes del cuerpo que se buscan por nombre (cualquiera de los alias).</summary>
        static readonly (string part, string[] aliases)[] StandardBones =
        {
            ("Cadera", new[] { "hips", "pelvis", "hip" }),
            ("Columna", new[] { "spine" }),
            ("Pecho", new[] { "chest", "spine2", "spine1", "upperchest" }),
            ("Cuello", new[] { "neck" }),
            ("Cabeza", new[] { "head" }),
            ("Hombro izq.", new[] { "leftshoulder", "left_shoulder", "l_shoulder", "shoulder.l", "clavicle_l" }),
            ("Brazo izq.", new[] { "leftarm", "left_upperarm", "l_upperarm", "upperarm.l", "leftupperarm" }),
            ("Antebrazo izq.", new[] { "leftforearm", "left_forearm", "left_lowerarm", "l_forearm", "forearm.l" }),
            ("Mano izq.", new[] { "lefthand", "left_hand", "l_hand", "hand.l" }),
            ("Muslo izq.", new[] { "leftupleg", "left_upperleg", "left_thigh", "l_thigh", "thigh.l", "leftupperleg" }),
            ("Pierna izq.", new[] { "leftleg", "left_lowerleg", "left_calf", "l_calf", "shin.l", "leftlowerleg" }),
            ("Pie izq.", new[] { "leftfoot", "left_foot", "l_foot", "foot.l" }),
            ("Muslo der.", new[] { "rightupleg", "right_upperleg", "right_thigh", "r_thigh", "thigh.r", "rightupperleg" }),
            ("Pierna der.", new[] { "rightleg", "right_lowerleg", "right_calf", "r_calf", "shin.r", "rightlowerleg" }),
            ("Pie der.", new[] { "rightfoot", "right_foot", "r_foot", "foot.r" }),
        };

        [MenuItem("Carrito/Inspeccionar esqueletos", priority = 30)]
        public static void InspectFromMenu()
        {
            string report = BuildReport();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report, new UTF8Encoding(false));
            Debug.Log($"[Carrito] Informe de esqueletos en {ReportPath}");
        }

        public static void InspectFromCommandLine()
        {
            InspectFromMenu();
            EditorApplication.Exit(0);
        }

        public static string BuildReport()
        {
            var text = new StringBuilder();
            text.AppendLine("# Esqueletos de los modelos 3D");
            text.AppendLine();
            text.AppendLine($"Generado con *Carrito > Inspeccionar esqueletos* (Unity {Application.unityVersion}).");
            text.AppendLine();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { CarritoSceneBuilder.RootFolder + "/06_Models" }))
                Describe(AssetDatabase.GUIDToAssetPath(guid), text);
            return text.ToString();
        }

        static void Describe(string path, StringBuilder text)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            text.AppendLine($"## {Path.GetFileNameWithoutExtension(path)}");
            text.AppendLine();
            text.AppendLine($"- Archivo: `{path}`");
            if (importer != null)
            {
                text.AppendLine($"- Rig: {importer.animationType}, avatar: {importer.avatarSetup}");
                int takes = importer.importedTakeInfos != null ? importer.importedTakeInfos.Length : 0;
                text.AppendLine($"- Animaciones dentro del FBX: {takes}" + (takes > 0 ? " (" + string.Join(", ", importer.importedTakeInfos.Select(t => t.name)) + ")" : ""));
            }
            if (asset == null)
            {
                text.AppendLine("- No se pudo cargar.");
                return;
            }

            SkinnedMeshRenderer[] skins = asset.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            text.AppendLine($"- Mallas con piel: {skins.Length}");
            foreach (SkinnedMeshRenderer skin in skins)
            {
                Mesh mesh = skin.sharedMesh;
                if (mesh == null) continue;
                Transform[] bones = skin.bones;
                var weightPerBone = new float[bones.Length];
                var influences = new int[5];
                BoneWeight[] weights = mesh.boneWeights;
                foreach (BoneWeight w in weights)
                {
                    int count = 0;
                    if (w.weight0 > 0f) { count++; weightPerBone[w.boneIndex0] += w.weight0; }
                    if (w.weight1 > 0f) { count++; weightPerBone[w.boneIndex1] += w.weight1; }
                    if (w.weight2 > 0f) { count++; weightPerBone[w.boneIndex2] += w.weight2; }
                    if (w.weight3 > 0f) { count++; weightPerBone[w.boneIndex3] += w.weight3; }
                    influences[count]++;
                }
                text.AppendLine($"- `{skin.name}`: {mesh.vertexCount} vértices, {mesh.triangles.Length / 3} triángulos, {bones.Length} huesos, raíz `{(skin.rootBone != null ? skin.rootBone.name : "—")}`");
                text.AppendLine($"  - Influencias por vértice: 1 → {influences[1]}, 2 → {influences[2]}, 3 → {influences[3]}, 4 → {influences[4]}, sin peso → {influences[0]}");
                var unweighted = new List<string>();
                for (int i = 0; i < bones.Length; i++)
                    if (bones[i] != null && weightPerBone[i] <= 0.001f) unweighted.Add(bones[i].name);
                text.AppendLine($"  - Huesos sin peso: {(unweighted.Count == 0 ? "ninguno" : string.Join(", ", unweighted))}");

                var names = new HashSet<string>(bones.Where(b => b != null).Select(b => Normalize(b.name)));
                var missing = new List<string>();
                foreach (var (part, aliases) in StandardBones)
                    if (!aliases.Any(alias => names.Any(n => n == Normalize(alias) || n.EndsWith(Normalize(alias))))) missing.Add(part);
                text.AppendLine($"  - Partes estándar que faltan: {(missing.Count == 0 ? "ninguna" : string.Join(", ", missing))}");
            }

            text.AppendLine();
            text.AppendLine("```");
            Transform root = asset.transform;
            var boneSet = new HashSet<Transform>(skins.SelectMany(s => s.bones).Where(b => b != null));
            WriteHierarchy(root, 0, boneSet, text);
            text.AppendLine("```");
            text.AppendLine();
        }

        static void WriteHierarchy(Transform t, int depth, HashSet<Transform> bones, StringBuilder text)
        {
            string marker = bones.Contains(t) ? "" : (t.GetComponent<SkinnedMeshRenderer>() != null ? "  [malla]" : "  [nodo]");
            Vector3 euler = t.localEulerAngles;
            text.AppendLine($"{new string(' ', depth * 2)}{t.name}{marker}  pos {Format(t.localPosition)} rot {Format(euler)}");
            foreach (Transform child in t) WriteHierarchy(child, depth + 1, bones, text);
        }

        static string Format(Vector3 v) => $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###})";

        static string Normalize(string name) => name.ToLowerInvariant().Replace("mixamorig:", "").Replace(" ", "").Replace("-", "_");
    }
}
