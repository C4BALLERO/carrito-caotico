using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Personajes de los compradores rivales: packs CC0 de KayKit (Kay Lousberg). Todos comparten
    /// el mismo esqueleto, así que usan un único FBX de animaciones (NPC_Animaciones.fbx) y un
    /// único Animator Controller. Los FBX se recortaron (sin armas ni animaciones propias); ver
    /// 06_Models/NPC/CREDITOS.md. El frente de cada modelo mira hacia +Z.
    /// </summary>
    internal static class NpcModels
    {
        public const string Folder = CharacterModels.ModelsFolder + "/NPC";
        public const string AnimationsPath = Folder + "/NPC_Animaciones.fbx";
        public const string ControllerPath = Folder + "/NPC_Comprador.controller";
        /// <summary>Todos los personajes tienen el mismo esqueleto: una escala común deja los hombros a 0,8 m.</summary>
        public const float Scale = 0.72f;
        public const string LeftUpperArm = "upperarm.l";
        public const string LeftHand = "hand.l";
        public const string RightUpperArm = "upperarm.r";
        public const string RightHand = "hand.r";

        internal sealed class Definition
        {
            public string Pack;
            public string Name;
            public string Texture;
            public string ModelPath => $"{Folder}/{Pack}/{Name}.fbx";
            public string TexturePath => $"{Folder}/{Pack}/{Texture}.png";
            public string MaterialName => "NPC_" + Texture;
        }

        static Definition Adventurer(string name, string texture) => new Definition { Pack = "Aventureros", Name = name, Texture = texture };
        static Definition Skeleton(string name) => new Definition { Pack = "Esqueletos", Name = name, Texture = "skeleton_texture" };

        static readonly Definition[] Adventurers =
        {
            Adventurer("Knight", "knight_texture"),
            Adventurer("Barbarian", "barbarian_texture"),
            Adventurer("Mage", "mage_texture"),
            Adventurer("Rogue", "rogue_texture"),
            Adventurer("RogueHooded", "rogue_texture"),
        };

        static readonly Definition[] Skeletons =
        {
            Skeleton("Skeleton_Minion"),
            Skeleton("Skeleton_Warrior"),
            Skeleton("Skeleton_Rogue"),
            Skeleton("Skeleton_Mage"),
        };

        /// <summary>Aventureros en el supermercado clásico y esqueletos en el embrujado.</summary>
        public static Definition[] ForStore(StoreId store) => store == StoreId.Halloween ? Skeletons : Adventurers;

        public static IEnumerable<Definition> All => Adventurers.Concat(Skeletons);

        /// <summary>Asigna la textura del pack al material.</summary>
        public static void ApplyTexture(Material material, Definition definition)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(definition.TexturePath);
            if (texture == null) throw new InvalidOperationException($"No se encontró la textura {definition.TexturePath}.");
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(material);
        }

        /// <summary>Material brillante para los ojos de los esqueletos.</summary>
        public static void MakeGlow(Material material, Color color)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 1.5f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
        }

        static void ConfigureModel(string path, bool animations)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException($"No se encontró el modelo {path}.");

            bool changed = false;
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
            if (importer.animationType != ModelImporterAnimationType.Generic) { importer.animationType = ModelImporterAnimationType.Generic; changed = true; }
            if (importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar) { importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar; changed = true; }
            if (importer.importAnimation != animations) { importer.importAnimation = animations; changed = true; }
            if (importer.importBlendShapes) { importer.importBlendShapes = false; changed = true; }
            if (importer.importCameras) { importer.importCameras = false; changed = true; }
            if (importer.importLights) { importer.importLights = false; changed = true; }

            if (animations)
            {
                // Las tomas se llaman «Rig|Idle»: se quedan con el nombre corto y las de ciclo se repiten.
                ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    string name = clip.takeName.Substring(clip.takeName.LastIndexOf('|') + 1);
                    bool loop = name == "Idle" || name == "Walking_A";
                    if (clip.name != name || clip.loopTime != loop)
                    {
                        clip.name = name;
                        clip.loopTime = loop;
                        changed = true;
                    }
                }
                if (importer.clipAnimations.Length == 0) changed = true;
                importer.clipAnimations = clips;
            }
            if (changed) importer.SaveAndReimport();
        }

        static AnimationClip Clip(string name)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(AnimationsPath).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == name);
            if (clip == null) throw new InvalidOperationException($"Falta la animación '{name}' en {AnimationsPath}.");
            return clip;
        }

        /// <summary>
        /// Animator Controller compartido: Quieto (Idle), Caminar (Walking_A, a ritmo del parámetro
        /// Ritmo) y Agarrar (PickUp). KayKitShopperAnimator salta entre estados con CrossFade.
        /// </summary>
        public static AnimatorController GetController()
        {
            ConfigureModel(AnimationsPath, true);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            foreach (AnimatorControllerParameter parameter in controller.parameters)
                controller.RemoveParameter(parameter);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = KayKitShopperAnimator.PaceParameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1f
            });

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in machine.states)
                machine.RemoveState(child.state);

            AnimatorState idle = machine.AddState(KayKitShopperAnimator.IdleState, new Vector3(250f, 0f, 0f));
            idle.motion = Clip("Idle");
            AnimatorState walk = machine.AddState(KayKitShopperAnimator.WalkState, new Vector3(250f, 80f, 0f));
            walk.motion = Clip("Walking_A");
            walk.speedParameterActive = true;
            walk.speedParameter = KayKitShopperAnimator.PaceParameter;
            AnimatorState grab = machine.AddState(KayKitShopperAnimator.GrabState, new Vector3(250f, 160f, 0f));
            grab.motion = Clip("PickUp");
            machine.defaultState = idle;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        internal struct Character
        {
            public GameObject Root;
            public Animator Animator;
            public Transform LeftUpperArm, LeftHand, RightUpperArm, RightHand;
        }

        /// <summary>
        /// Coloca el personaje como hijo de <paramref name="parent"/> con los pies en
        /// <paramref name="localPosition"/>. Los ojos (malla «_Eyes») usan <paramref name="eyes"/>.
        /// </summary>
        public static Character Build(Definition definition, string name, Transform parent, Vector3 localPosition,
            Material material, Material eyes, RuntimeAnimatorController controller)
        {
            ConfigureModel(definition.ModelPath, false);
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
            model.transform.localScale = Vector3.one * Scale;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = renderer.name.EndsWith("_Eyes") && eyes != null ? eyes : material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                // La animación mueve las mallas lejos de su pose de reposo: los límites se recalculan.
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }

            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            return new Character
            {
                Root = root,
                Animator = animator,
                LeftUpperArm = FindBone(model.transform, LeftUpperArm),
                LeftHand = FindBone(model.transform, LeftHand),
                RightUpperArm = FindBone(model.transform, RightUpperArm),
                RightHand = FindBone(model.transform, RightHand),
            };
        }

        static Transform FindBone(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            throw new InvalidOperationException($"No se encontró el hueso '{name}' en {root.name}.");
        }
    }
}
