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
            public string LeftLowerArm = "Left_LowerArm";
            public string LeftHand = "Left_Hand";
            public string RightUpperArm = "Right_UpperArm";
            public string RightLowerArm = "Right_LowerArm";
            public string RightHand = "Right_Hand";
            public string LeftUpperLeg = "Left_UpperLeg";
            public string LeftLowerLeg = "Left_LowerLeg";
            public string LeftFoot = "Left_Foot";
            public string RightUpperLeg = "Right_UpperLeg";
            public string RightLowerLeg = "Right_LowerLeg";
            public string RightFoot = "Right_Foot";
            public string Head = "Head";
            public string Spine = "Spine";
            /// <summary>Personaje KayKit: varias mallas, textura PNG y archivos en 06_Models/NPC.</summary>
            public string ModelOverride;
            public string TextureOverride;
            /// <summary>Esqueletos KayKit: las mallas «_Eyes» brillan.</summary>
            public bool GlowEyes;
            /// <summary>Nombre de los objetos en la escena (la carpeta, salvo en los KayKit, que comparten material).</summary>
            public string Name;
            public string ObjectName => Name ?? Folder;

            public string ModelPath => ModelOverride ?? $"{ModelsFolder}/{Folder}/{Folder}.fbx";
            public string TexturePath => TextureOverride ?? $"{ModelsFolder}/{Folder}/{Folder}_BaseColor.jpg";
            public string DisplayName => CharacterChoice.DisplayName(Id);
        }

        /// <summary>
        /// Personaje KayKit (los mismos modelos que los rivales). Comparte material con los rivales
        /// («NPC_&lt;textura&gt;») y los huesos del pack, que son iguales en todos.
        /// </summary>
        static Definition KayKit(CharacterId id, string pack, string model, string texture) => new Definition
        {
            Id = id, Folder = "NPC_" + texture, Name = id.ToString(),
            ModelOverride = $"{ModelsFolder}/NPC/{pack}/{model}.fbx",
            TextureOverride = $"{ModelsFolder}/NPC/{pack}/{texture}.png",
            GlowEyes = pack == "Esqueletos",
            LeftUpperArm = "upperarm.l", LeftLowerArm = "lowerarm.l", LeftHand = "hand.l",
            RightUpperArm = "upperarm.r", RightLowerArm = "lowerarm.r", RightHand = "hand.r",
            LeftUpperLeg = "upperleg.l", LeftLowerLeg = "lowerleg.l", LeftFoot = "foot.l",
            RightUpperLeg = "upperleg.r", RightLowerLeg = "lowerleg.r", RightFoot = "foot.r",
            Head = "head", Spine = "spine",
        };

        /// <summary>Todos los personajes, en el orden de <see cref="CharacterId"/>.</summary>
        public static readonly Definition[] All =
        {
            new Definition { Id = CharacterId.Pinguino, Folder = "Pinguino" },
            new Definition { Id = CharacterId.Zorro, Folder = "Zorro", KeepPose = true },
            new Definition
            {
                Id = CharacterId.Cerdo, Folder = "Cerdo",
                LeftUpperArm = "mixamorig:LeftArm", LeftLowerArm = "mixamorig:LeftForeArm", LeftHand = "mixamorig:LeftHand",
                RightUpperArm = "mixamorig:RightArm", RightLowerArm = "mixamorig:RightForeArm", RightHand = "mixamorig:RightHand",
                LeftUpperLeg = "mixamorig:LeftUpLeg", LeftLowerLeg = "mixamorig:LeftLeg", LeftFoot = "mixamorig:LeftFoot",
                RightUpperLeg = "mixamorig:RightUpLeg", RightLowerLeg = "mixamorig:RightLeg", RightFoot = "mixamorig:RightFoot",
                Head = "mixamorig:Head", Spine = "mixamorig:Spine",
            },
            KayKit(CharacterId.Caballero, "Aventureros", "Knight", "knight_texture"),
            KayKit(CharacterId.Barbaro, "Aventureros", "Barbarian", "barbarian_texture"),
            KayKit(CharacterId.Maga, "Aventureros", "Mage", "mage_texture"),
            KayKit(CharacterId.Picaro, "Aventureros", "Rogue", "rogue_texture"),
            KayKit(CharacterId.PicaroCapucha, "Aventureros", "RogueHooded", "rogue_texture"),
            KayKit(CharacterId.Esqueleto, "Esqueletos", "Skeleton_Minion", "skeleton_texture"),
            KayKit(CharacterId.EsqueletoGuerrero, "Esqueletos", "Skeleton_Warrior", "skeleton_texture"),
            KayKit(CharacterId.EsqueletoPicaro, "Esqueletos", "Skeleton_Rogue", "skeleton_texture"),
            KayKit(CharacterId.EsqueletoMago, "Esqueletos", "Skeleton_Mage", "skeleton_texture"),
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

            EnsureTexture(definition);
        }

        /// <summary>
        /// Textura 2D normal de 1024 px como máximo. Los .meta del pack KayKit solo traen el GUID y
        /// Unity detectaba sus PNG (cuadrados) como Cubemap.
        /// </summary>
        static void EnsureTexture(Definition definition)
        {
            var texture = AssetImporter.GetAtPath(definition.TexturePath) as TextureImporter;
            if (texture == null) throw new InvalidOperationException($"No se encontró la textura {definition.TexturePath}.");
            // Las de KayKit son paletas de colores: con mipmaps, a distancia los colores se mezclan en gris.
            bool palette = definition.TextureOverride != null;
            if (texture.maxTextureSize != 1024 || texture.textureShape != TextureImporterShape.Texture2D || texture.textureType != TextureImporterType.Default
                || (palette && texture.mipmapEnabled))
            {
                texture.maxTextureSize = 1024;
                texture.textureShape = TextureImporterShape.Texture2D;
                texture.textureType = TextureImporterType.Default;
                if (palette) texture.mipmapEnabled = false;
                texture.SaveAndReimport();
            }
        }

        /// <summary>Asigna al material la textura de color del personaje.</summary>
        public static void ApplyTexture(Material material, Definition definition)
        {
            EnsureTexture(definition);
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
            float height, Vector3 armRestDirection, Material material, Material eyes = null)
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

            // Los KayKit tienen varias mallas (cuerpo, cabeza, brazos, piernas, ojos…) con el mismo esqueleto.
            SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skins.Length == 0) throw new InvalidOperationException($"El {definition.DisplayName} no tiene SkinnedMeshRenderer: revisa el rig del FBX.");
            // Cascos, sombreros y capas de los KayKit son mallas sueltas colgadas de los huesos.
            foreach (Renderer part in model.GetComponentsInChildren<Renderer>(true))
                part.sharedMaterial = eyes != null && part.name.EndsWith("_Eyes") ? eyes : material;

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
            Bounds bounds = PosedBounds(skins);
            model.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, bounds.size.y));
            bounds = PosedBounds(skins);
            model.transform.position += new Vector3(
                root.transform.position.x - bounds.center.x,
                root.transform.position.y - bounds.min.y,
                root.transform.position.z - bounds.center.z);

            // Límites para el culling, con margen para el bamboleo.
            bounds = PosedBounds(skins);
            foreach (SkinnedMeshRenderer skin in skins)
            {
                Vector3 localCenter = skin.transform.InverseTransformPoint(bounds.center);
                Vector3 localSize = skin.transform.InverseTransformVector(bounds.size);
                skin.localBounds = new Bounds(localCenter, new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z)) * 1.3f);
            }

            return new Character
            {
                Root = root, Model = model.transform, LeftArm = left, RightArm = right, RightHand = rightHand,
                LocalBounds = new Bounds(parent.InverseTransformPoint(bounds.center), bounds.size),
            };
        }

        /// <summary>Límites en el mundo de las mallas deformadas por los huesos en su pose actual.</summary>
        static Bounds PosedBounds(SkinnedMeshRenderer[] skins)
        {
            Bounds bounds = default;
            bool first = true;
            var baked = new Mesh();
            foreach (SkinnedMeshRenderer skin in skins)
            {
                skin.BakeMesh(baked, true);
                Vector3[] vertices = baked.vertices;
                Transform t = skin.transform;
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 world = t.position + t.rotation * vertex;
                    if (first) { bounds = new Bounds(world, Vector3.zero); first = false; }
                    else bounds.Encapsulate(world);
                }
            }
            Object.DestroyImmediate(baked);
            // Piezas rígidas (casco, sombrero) colgadas de los huesos.
            if (skins.Length > 0)
                foreach (MeshRenderer piece in skins[0].transform.root.GetComponentsInChildren<MeshRenderer>())
                    if (piece.transform.IsChildOf(skins[0].transform.parent)) bounds.Encapsulate(piece.bounds);
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

        /// <summary>Hueso del modelo por su nombre (error si no existe: el esqueleto no es el esperado).</summary>
        public static Transform Bone(Transform model, string name) => FindBone(model, name);

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
