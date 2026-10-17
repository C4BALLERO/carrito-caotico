using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Personajes con modelo 3D importado. Los ajustes de importación se fijan desde código para
    /// que el resultado no dependa del Inspector de cada equipo. El frente de cada modelo mira
    /// hacia +Z y su base queda en y = 0.
    /// </summary>
    internal static class CharacterModels
    {
        public const string ModelsFolder = CarritoSceneBuilder.RootFolder + "/06_Models";
        public const string PenguinModelPath = ModelsFolder + "/Pinguino/Pinguino.fbx";
        public const string PenguinTexturePath = ModelsFolder + "/Pinguino/Pinguino_BaseColor.jpg";

        /// <summary>Altura del pingüino en la escena (metros).</summary>
        public const float PenguinHeight = 1.25f;

        /// <summary>Dirección de reposo de las aletas (espacio del personaje): abajo, algo abiertas y hacia el asa.</summary>
        static readonly Vector3 FlipperRestDirection = new Vector3(0.45f, -0.75f, 0.5f);

        internal struct Character
        {
            public GameObject Root;
            public Transform Model;
            public Transform LeftFlipper;
            public Transform RightFlipper;
        }

        /// <summary>
        /// Ajustes del pingüino: rig Generic para conservar los huesos (las aletas se mueven por
        /// código), sin materiales ni animaciones del FBX y con la textura limitada a 1024 px.
        /// </summary>
        public static void ConfigurePenguinImport()
        {
            var model = AssetImporter.GetAtPath(PenguinModelPath) as ModelImporter;
            if (model == null) throw new InvalidOperationException($"No se encontró el modelo {PenguinModelPath}.");

            bool changed = false;
            if (model.materialImportMode != ModelImporterMaterialImportMode.None) { model.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
            if (model.animationType != ModelImporterAnimationType.Generic) { model.animationType = ModelImporterAnimationType.Generic; changed = true; }
            if (model.avatarSetup != ModelImporterAvatarSetup.NoAvatar) { model.avatarSetup = ModelImporterAvatarSetup.NoAvatar; changed = true; }
            if (model.importAnimation) { model.importAnimation = false; changed = true; }
            if (model.importBlendShapes) { model.importBlendShapes = false; changed = true; }
            if (model.importCameras) { model.importCameras = false; changed = true; }
            if (model.importLights) { model.importLights = false; changed = true; }
            if (model.importVisibility) { model.importVisibility = false; changed = true; }
            if (changed) model.SaveAndReimport();

            var texture = AssetImporter.GetAtPath(PenguinTexturePath) as TextureImporter;
            if (texture == null) throw new InvalidOperationException($"No se encontró la textura {PenguinTexturePath}.");
            if (texture.maxTextureSize != 1024)
            {
                texture.maxTextureSize = 1024;
                texture.SaveAndReimport();
            }
        }

        /// <summary>Material del pingüino con su textura de color.</summary>
        public static void ApplyPenguinTexture(Material material)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PenguinTexturePath);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// Coloca el pingüino como hijo de <paramref name="parent"/>, escalado a
        /// <see cref="PenguinHeight"/>, con las aletas bajadas desde la pose T del modelo.
        /// </summary>
        public static Character BuildPenguin(string name, Transform parent, Vector3 localPosition, Material material)
        {
            ConfigurePenguinImport();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PenguinModelPath);
            if (asset == null) throw new InvalidOperationException($"No se pudo cargar {PenguinModelPath}.");

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.identity;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Modelo";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var animator = model.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);

            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null) throw new InvalidOperationException("El pingüino no tiene SkinnedMeshRenderer: revisa el rig del FBX.");
            skin.sharedMaterial = material;

            Transform left = FindDeep(model.transform, "Left_UpperArm");
            Transform right = FindDeep(model.transform, "Right_UpperArm");
            LowerFlipper(left, FindDeep(model.transform, "Left_Hand"), root.transform, -1f);
            LowerFlipper(right, FindDeep(model.transform, "Right_Hand"), root.transform, 1f);

            // Escala a la altura deseada y apoya la base en el suelo. Se mide la malla ya posada:
            // los límites de un SkinnedMeshRenderer no se actualizan en el editor.
            Bounds bounds = PosedBounds(skin);
            model.transform.localScale = Vector3.one * (PenguinHeight / Mathf.Max(0.01f, bounds.size.y));
            bounds = PosedBounds(skin);
            model.transform.position += new Vector3(
                root.transform.position.x - bounds.center.x,
                root.transform.position.y - bounds.min.y,
                root.transform.position.z - bounds.center.z);

            // Límites para el culling, con margen para el bamboleo.
            bounds = PosedBounds(skin);
            Vector3 localCenter = skin.transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = skin.transform.InverseTransformVector(bounds.size);
            skin.localBounds = new Bounds(localCenter, new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z)) * 1.3f);

            return new Character { Root = root, Model = model.transform, LeftFlipper = left, RightFlipper = right };
        }

        /// <summary>Límites en el mundo de la malla deformada por los huesos en su pose actual.</summary>
        static Bounds PosedBounds(SkinnedMeshRenderer skin)
        {
            var baked = new Mesh();
            skin.BakeMesh(baked, true);
            Vector3[] vertices = baked.vertices;
            Object.DestroyImmediate(baked);
            Transform t = skin.transform;
            var bounds = new Bounds(t.position + t.rotation * vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Length; i++)
                bounds.Encapsulate(t.position + t.rotation * vertices[i]);
            return bounds;
        }

        /// <summary>Gira el hueso del brazo para que la aleta apunte a la dirección de reposo.</summary>
        static void LowerFlipper(Transform upperArm, Transform hand, Transform character, float side)
        {
            if (upperArm == null || hand == null) return;
            Vector3 current = hand.position - upperArm.position;
            Vector3 local = FlipperRestDirection;
            local.x *= side;
            Vector3 desired = character.TransformDirection(local.normalized);
            upperArm.rotation = Quaternion.FromToRotation(current, desired) * upperArm.rotation;
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
    }
}
