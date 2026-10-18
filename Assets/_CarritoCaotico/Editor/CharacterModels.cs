using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Personajes con modelo 3D importado (FBX de Tripo con huesos). Los ajustes de importación
    /// se fijan desde código para que el resultado no dependa del Inspector de cada equipo. El
    /// frente de cada modelo mira hacia +Z.
    /// </summary>
    internal static class CharacterModels
    {
        public const string ModelsFolder = CarritoSceneBuilder.RootFolder + "/06_Models";

        /// <summary>Datos de un personaje: archivos, nombres de sus huesos y si conserva su pose.</summary>
        internal sealed class Definition
        {
            public CharacterId Id;
            public string Folder;
            /// <summary>true si el modelo ya viene posado (no en pose T): no se le bajan los brazos.</summary>
            public bool KeepPose;
            public string LeftUpperArm = "Left_UpperArm";
            public string LeftHand = "Left_Hand";
            public string RightUpperArm = "Right_UpperArm";
            public string RightHand = "Right_Hand";

            public string ModelPath => $"{ModelsFolder}/{Folder}/{Folder}.fbx";
            public string TexturePath => $"{ModelsFolder}/{Folder}/{Folder}_BaseColor.jpg";
            public string DisplayName => CharacterChoice.DisplayName(Id);
        }

        /// <summary>Todos los personajes, en el orden de <see cref="CharacterId"/>.</summary>
        public static readonly Definition[] All =
        {
            new Definition { Id = CharacterId.Pinguino, Folder = "Pinguino" },
            new Definition { Id = CharacterId.Zorro, Folder = "Zorro", KeepPose = true },
            new Definition
            {
                Id = CharacterId.Cerdo, Folder = "Cerdo",
                LeftUpperArm = "mixamorig:LeftArm", LeftHand = "mixamorig:LeftHand",
                RightUpperArm = "mixamorig:RightArm", RightHand = "mixamorig:RightHand",
            },
        };

        internal struct Character
        {
            public GameObject Root;
            public Transform Model;
            public Transform LeftArm;
            public Transform RightArm;
            public Transform RightHand;
            /// <summary>Límites de la malla posada, en el espacio local del padre.</summary>
            public Bounds LocalBounds;
        }

        /// <summary>
        /// Ajustes de importación: rig Generic sin avatar para conservar los huesos (los brazos se
        /// mueven por código), sin materiales ni animaciones del FBX y textura limitada a 1024 px.
        /// </summary>
        public static void ConfigureImport(Definition definition)
        {
            var model = AssetImporter.GetAtPath(definition.ModelPath) as ModelImporter;
            if (model == null) throw new InvalidOperationException($"No se encontró el modelo {definition.ModelPath}.");

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

            var texture = AssetImporter.GetAtPath(definition.TexturePath) as TextureImporter;
            if (texture == null) throw new InvalidOperationException($"No se encontró la textura {definition.TexturePath}.");
            if (texture.maxTextureSize != 1024)
            {
                texture.maxTextureSize = 1024;
                texture.SaveAndReimport();
            }
        }

        /// <summary>Asigna al material la textura de color del personaje.</summary>
        public static void ApplyTexture(Material material, Definition definition)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(definition.TexturePath);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// Coloca el personaje como hijo de <paramref name="parent"/> con la altura indicada, la
        /// base en <paramref name="localPosition"/> y centrado en X y Z. Si el modelo viene en
        /// pose T, gira los brazos hacia <paramref name="armRestDirection"/> (brazo derecho,
        /// espacio del personaje; el izquierdo es su reflejo).
        /// </summary>
        public static Character Build(Definition definition, string name, Transform parent, Vector3 localPosition,
            float height, Vector3 armRestDirection, Material material)
        {
            ConfigureImport(definition);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);
            if (asset == null) throw new InvalidOperationException($"No se pudo cargar {definition.ModelPath}.");

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
            if (skin == null) throw new InvalidOperationException($"El {definition.DisplayName} no tiene SkinnedMeshRenderer: revisa el rig del FBX.");
            skin.sharedMaterial = material;

            Transform left = FindBone(model.transform, definition.LeftUpperArm);
            Transform right = FindBone(model.transform, definition.RightUpperArm);
            Transform rightHand = FindBone(model.transform, definition.RightHand);
            if (!definition.KeepPose)
            {
                LowerArm(left, FindBone(model.transform, definition.LeftHand), root.transform, armRestDirection, -1f);
                LowerArm(right, rightHand, root.transform, armRestDirection, 1f);
            }

            // Escala a la altura deseada y apoya la base. Se mide la malla ya posada: los límites
            // de un SkinnedMeshRenderer no se actualizan en el editor.
            Bounds bounds = PosedBounds(skin);
            model.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, bounds.size.y));
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

            return new Character
            {
                Root = root, Model = model.transform, LeftArm = left, RightArm = right, RightHand = rightHand,
                LocalBounds = new Bounds(parent.InverseTransformPoint(bounds.center), bounds.size),
            };
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

        /// <summary>Gira el hueso del brazo para que apunte a la dirección de reposo.</summary>
        static void LowerArm(Transform upperArm, Transform hand, Transform character, Vector3 restDirection, float side)
        {
            Vector3 current = hand.position - upperArm.position;
            Vector3 local = restDirection;
            local.x *= side;
            Vector3 desired = character.TransformDirection(local.normalized);
            upperArm.rotation = Quaternion.FromToRotation(current, desired) * upperArm.rotation;
        }

        static Transform FindBone(Transform root, string name)
        {
            Transform bone = FindDeep(root, name);
            if (bone == null) throw new InvalidOperationException($"No se encontró el hueso '{name}' en {root.name}.");
            return bone;
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
