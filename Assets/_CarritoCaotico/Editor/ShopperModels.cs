using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Compradores caricaturescos generados por código, con estilo de dibujos animados clásicos:
    /// cabeza grande, ojos saltones con pupilas, cejas y boca que se mueven, guantes blancos y
    /// zapatones. Cada uno es un modelo segmentado: piezas rígidas colgadas de una jerarquía de
    /// huesos real (<see cref="ShopperRig"/>), así que no hay pesos de vértices que se deformen.
    /// Hay ocho compradores por supermercado (en Halloween, disfrazados) y un reponedor.
    /// </summary>
    internal static class ShopperModels
    {
        public enum Hair { None, Curly, Ponytail, Pompadour, BaldSides, Bun, Afro, Cap }

        public sealed class Style
        {
            public string Name;
            public float Height = 1.7f;
            /// <summary>Anchura del cuerpo (1 normal, 1.3 corpulento, 0.85 delgado).</summary>
            public float Girth = 1f;
            /// <summary>Barriga (1 normal).</summary>
            public float Belly = 1f;
            public Color Skin;
            public Color Shirt;
            public Color Pants;
            public Color Shoes = new Color32(60, 45, 40, 255);
            public Color HairColor = new Color32(70, 45, 30, 255);
            public Hair HairStyle;
            public bool Dress;
            public bool Glasses;
            public bool Mustache;
            public bool LongNose;
            public bool Apron;
            public float Bounce = 0.5f;
            // Disfraces de Halloween.
            public bool WitchHat;
            public bool Cape;
            public bool Horns;
            public Color CostumeColor = new Color32(76, 29, 149, 255);
        }

        static readonly Color32 Glove = new Color32(250, 250, 248, 255);
        static readonly Color32 EyeWhite = new Color32(255, 255, 255, 255);
        static readonly Color32 Pupil = new Color32(20, 20, 28, 255);
        static readonly Color32 Mouth = new Color32(150, 40, 50, 255);

        /// <summary>Compradores del súper clásico.</summary>
        public static Style[] Classic() => new[]
        {
            new Style { Name = "Senora_Rulos", Height = 1.5f, Girth = 1.15f, Belly = 1.15f, Skin = new Color32(244, 204, 170, 255), Shirt = new Color32(236, 112, 160, 255), Pants = new Color32(236, 112, 160, 255), Dress = true, HairStyle = Hair.Curly, HairColor = new Color32(186, 170, 210, 255), Glasses = true, Bounce = 0.7f },
            new Style { Name = "Abuelo_Bigotes", Height = 1.82f, Girth = 0.85f, Skin = new Color32(232, 190, 160, 255), Shirt = new Color32(214, 196, 150, 255), Pants = new Color32(96, 86, 120, 255), HairStyle = Hair.BaldSides, HairColor = new Color32(230, 230, 230, 255), Mustache = true, LongNose = true, Bounce = 0.25f },
            new Style { Name = "Chica_Coleta", Height = 1.62f, Girth = 0.9f, Skin = new Color32(198, 140, 100, 255), Shirt = new Color32(20, 160, 150, 255), Pants = new Color32(52, 84, 160, 255), HairStyle = Hair.Ponytail, HairColor = new Color32(230, 110, 40, 255), Bounce = 0.85f },
            new Style { Name = "Tipo_Tupe", Height = 1.88f, Girth = 1f, Skin = new Color32(240, 196, 160, 255), Shirt = new Color32(220, 60, 60, 255), Pants = new Color32(40, 50, 90, 255), HairStyle = Hair.Pompadour, HairColor = new Color32(30, 30, 35, 255), Glasses = true, Bounce = 0.55f },
            new Style { Name = "Nino_Gorra", Height = 1.32f, Girth = 0.9f, Skin = new Color32(250, 214, 180, 255), Shirt = new Color32(250, 204, 21, 255), Pants = new Color32(60, 110, 200, 255), HairStyle = Hair.Cap, HairColor = new Color32(220, 38, 38, 255), Bounce = 1f },
            new Style { Name = "Senor_Panza", Height = 1.72f, Girth = 1.3f, Belly = 1.45f, Skin = new Color32(214, 160, 120, 255), Shirt = new Color32(34, 160, 90, 255), Pants = new Color32(120, 84, 56, 255), HairStyle = Hair.BaldSides, HairColor = new Color32(60, 40, 30, 255), Mustache = true, Bounce = 0.4f },
            new Style { Name = "Senora_Mono", Height = 1.78f, Girth = 0.95f, Skin = new Color32(150, 100, 70, 255), Shirt = new Color32(168, 140, 220, 255), Pants = new Color32(90, 70, 130, 255), Dress = true, HairStyle = Hair.Bun, HairColor = new Color32(40, 28, 24, 255), LongNose = true, Bounce = 0.45f },
            new Style { Name = "Chico_Afro", Height = 1.7f, Girth = 1f, Skin = new Color32(110, 72, 50, 255), Shirt = new Color32(249, 140, 40, 255), Pants = new Color32(70, 110, 170, 255), HairStyle = Hair.Afro, HairColor = new Color32(30, 22, 18, 255), Bounce = 0.75f },
        };

        /// <summary>Los mismos compradores, disfrazados para el súper de Halloween.</summary>
        public static Style[] Halloween()
        {
            Style[] styles = Classic();
            for (int i = 0; i < styles.Length; i++)
            {
                Style s = styles[i];
                s.Name += "_Halloween";
                switch (i % 4)
                {
                    case 0: s.WitchHat = true; s.CostumeColor = new Color32(76, 29, 149, 255); s.Shirt = new Color32(88, 40, 140, 255); break;
                    case 1: s.Cape = true; s.CostumeColor = new Color32(20, 20, 30, 255); s.Shirt = new Color32(150, 20, 30, 255); break;
                    case 2: s.Horns = true; s.Shirt = new Color32(200, 40, 30, 255); break;
                    default: s.Cape = true; s.WitchHat = true; s.CostumeColor = new Color32(234, 88, 12, 255); break;
                }
            }
            return styles;
        }

        public static Style Stocker() => new Style
        {
            Name = "Reponedor", Height = 1.76f, Girth = 1.05f, Skin = new Color32(226, 180, 140, 255),
            Shirt = new Color32(240, 240, 235, 255), Pants = new Color32(50, 60, 80, 255), HairStyle = Hair.Cap,
            HairColor = new Color32(22, 163, 74, 255), Apron = true, Mustache = true, Bounce = 0.35f
        };

        // ---------- Prefabs de rivales ----------

        /// <summary>Crea (o actualiza) los prefabs de los compradores rivales del supermercado.</summary>
        public static RivalShopper[] CreateRivalPrefabs(StoreId store, string prefabsFolder, Func<string, Color, Material> material)
        {
            string folder = $"{prefabsFolder}/Compradores";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(prefabsFolder, "Compradores");
            Style[] styles = store == StoreId.Halloween ? Halloween() : Classic();
            var result = new RivalShopper[styles.Length];
            for (int i = 0; i < styles.Length; i++)
            {
                GameObject root = BuildRival(styles[i], material, 101 + i * 37);
                string path = $"{folder}/Comprador_{styles[i].Name}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                result[i] = prefab.GetComponent<RivalShopper>();
            }

            // Personajes KayKit con clips: se mezclan con los caricaturescos como más variantes.
            NpcModels.Definition[] npcs = NpcModels.ForStore(store);
            RuntimeAnimatorController controller = NpcModels.GetController();
            var all = new List<RivalShopper>(result);
            foreach (NpcModels.Definition npc in npcs)
            {
                GameObject root = BuildClipRival(npc, store, material, controller);
                string path = $"{folder}/Comprador_KayKit_{npc.Name}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                all.Add(prefab.GetComponent<RivalShopper>());
            }
            return all.ToArray();
        }

        /// <summary>Altura del asa para los KayKit: sus hombros quedan a 0,8 m.</summary>
        const float ClipRivalHandleHeight = 0.85f;
        /// <summary>Los pies quedan algo detrás del centro para que las manos lleguen al asa.</summary>
        const float ClipRivalBodyZ = -0.08f;

        /// <summary>
        /// Comprador KayKit: malla con piel, Animator con los clips compartidos y
        /// <see cref="ClipShopperAnimator"/>, que los ajusta a la velocidad medida del clip.
        /// </summary>
        static GameObject BuildClipRival(NpcModels.Definition definition, StoreId store, Func<string, Color, Material> material, RuntimeAnimatorController controller)
        {
            var root = new GameObject("Comprador_KayKit_" + definition.Name);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.65f, 0f);
            capsule.height = 1.3f;
            capsule.radius = 0.3f;

            Transform cart = BuildShopperCart(root.transform, material, out Transform leftGrip, out Transform rightGrip, out Transform basket, ClipRivalHandleHeight);
            Material skin = material(definition.MaterialName, Color.white);
            NpcModels.ApplyTexture(skin, definition);
            Material eyes = null;
            if (store == StoreId.Halloween)
            {
                Color glow = new Color32(120, 255, 220, 255);
                eyes = material("NPC_Ojos", glow);
                NpcModels.MakeGlow(eyes, glow);
            }
            NpcModels.Character character = NpcModels.Build(definition, "Cuerpo", root.transform, new Vector3(0f, 0f, ClipRivalBodyZ), skin, eyes, controller);
            var (walkSpeed, grabContact) = NpcModels.MeasureClips(character);
            Debug.Log($"[Carrito] {definition.Name}: el clip de caminar pisa a {walkSpeed:0.00} m/s; recoge en {grabContact:0.00} del clip.");

            var rival = root.AddComponent<RivalShopper>();
            var serialized = new SerializedObject(rival);
            serialized.FindProperty("basketPoint").objectReferenceValue = basket;
            serialized.FindProperty("leftHand").objectReferenceValue = character.LeftHand;
            serialized.FindProperty("rightHand").objectReferenceValue = character.RightHand;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<ClipShopperAnimator>().Configure(character.Animator, character.Arms, character.Legs, character.Head, leftGrip, rightGrip, walkSpeed, grabContact);
            cart.name = "Carrito_Rival";
            return root;
        }

        static GameObject BuildRival(Style style, Func<string, Color, Material> material, int seed)
        {
            var root = new GameObject("Comprador_" + style.Name);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, style.Height * 0.5f, 0f);
            capsule.height = style.Height;
            capsule.radius = 0.3f * Mathf.Max(1f, style.Girth);

            ShopperRig rig = BuildBody(root.transform, style, material);
            Transform cart = BuildShopperCart(root.transform, material, out Transform leftGrip, out Transform rightGrip, out Transform basket);
            rig.leftGrip = leftGrip;
            rig.rightGrip = rightGrip;
            rig.dropPoint = basket;

            var rival = root.AddComponent<RivalShopper>();
            var serialized = new SerializedObject(rival);
            serialized.FindProperty("basketPoint").objectReferenceValue = basket;
            serialized.FindProperty("rig").objectReferenceValue = rig;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<ShopperAnimator>().Configure(rig, style.Bounce, seed);
            cart.name = "Carrito_Rival";
            return root;
        }

        /// <summary>Reponedor: camina por la entrada con una caja en brazos.</summary>
        public static GameObject BuildStocker(Transform parent, Vector3 a, Vector3 b, Func<string, Color, Material> material)
        {
            Style style = Stocker();
            var root = new GameObject("Reponedor");
            root.transform.SetParent(parent, false);
            root.transform.position = a;
            root.transform.rotation = Quaternion.LookRotation((b - a).normalized);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, style.Height * 0.5f, 0f);
            capsule.height = style.Height;
            capsule.radius = 0.35f;

            ShopperRig rig = BuildBody(root.transform, style, material);
            // La caja va delante del pecho; las manos la sujetan por los lados.
            GameObject box = Part(PrimitiveType.Cube, "Caja", root.transform, new Vector3(0f, 1.0f, 0.36f), new Vector3(0.5f, 0.38f, 0.34f), material("Carton", new Color32(190, 140, 90, 255)));
            rig.leftGrip = Empty("Agarre_Izq", box.transform.parent, new Vector3(-0.27f, 1.0f, 0.36f));
            rig.rightGrip = Empty("Agarre_Der", box.transform.parent, new Vector3(0.27f, 1.0f, 0.36f));
            rig.dropPoint = box.transform;
            root.AddComponent<MovingObstacle>().Configure(a, b, 1.2f, 0f);
            root.AddComponent<ShopperAnimator>().Configure(rig, style.Bounce, 7);
            return root;
        }

        // ---------- Cuerpo ----------

        /// <summary>Construye el esqueleto y las piezas del cuerpo. Medidas para 1,70 m; luego se escala.</summary>
        static ShopperRig BuildBody(Transform root, Style s, Func<string, Color, Material> material)
        {
            Material skin = material("Piel_" + Hex(s.Skin), s.Skin);
            Material shirt = material("Ropa_" + Hex(s.Shirt), s.Shirt);
            Material pants = material("Ropa_" + Hex(s.Pants), s.Pants);
            Material shoes = material("Zapato_" + Hex(s.Shoes), s.Shoes);
            Material hair = material("Pelo_" + Hex(s.HairColor), s.HairColor);
            Material glove = material("Guante", Glove);
            Material eye = material("Ojo", EyeWhite);
            Material pupil = material("Pupila", Pupil);
            Material mouth = material("Boca", Mouth);
            Material costume = material("Disfraz_" + Hex(s.CostumeColor), s.CostumeColor);

            var model = new GameObject("Modelo");
            model.transform.SetParent(root, false);
            model.transform.localScale = Vector3.one * (s.Height / 1.7f);
            var rig = model.AddComponent<ShopperRig>();
            float g = s.Girth;

            // Huesos (rotación identidad: los miembros cuelgan hacia -Y y el frente es +Z).
            rig.hips = Empty("Caderas", model.transform, new Vector3(0f, 0.79f, 0f));
            rig.spine = Empty("Columna", rig.hips, new Vector3(0f, 0.1f, 0f));
            rig.chest = Empty("Pecho", rig.spine, new Vector3(0f, 0.2f, 0f));
            rig.neck = Empty("Cuello", rig.chest, new Vector3(0f, 0.25f, 0f));
            rig.head = Empty("Cabeza", rig.neck, new Vector3(0f, 0.09f, 0f));
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float side = sideIndex == 0 ? -1f : 1f;
                string suffix = sideIndex == 0 ? "_Izq" : "_Der";
                Transform shoulder = Empty("Hombro" + suffix, rig.chest, new Vector3(side * 0.19f * g, 0.18f, 0f));
                Transform upperArm = Empty("Brazo" + suffix, shoulder, new Vector3(side * 0.04f, 0f, 0f));
                Transform lowerArm = Empty("Antebrazo" + suffix, upperArm, new Vector3(0f, -0.28f, 0f));
                Transform hand = Empty("Mano" + suffix, lowerArm, new Vector3(0f, -0.25f, 0f));
                Transform upperLeg = Empty("Muslo" + suffix, rig.hips, new Vector3(side * 0.11f * g, -0.02f, 0f));
                Transform lowerLeg = Empty("Pierna" + suffix, upperLeg, new Vector3(0f, -0.36f, 0f));
                Transform foot = Empty("Pie" + suffix, lowerLeg, new Vector3(0f, -0.33f, 0f));

                // Brazos: manga, antebrazo y guante blanco con pulgar.
                Part(PrimitiveType.Capsule, "Manga", upperArm, new Vector3(0f, -0.13f, 0f), new Vector3(0.12f, 0.16f, 0.12f), shirt);
                Part(PrimitiveType.Capsule, "Antebrazo", lowerArm, new Vector3(0f, -0.12f, 0f), new Vector3(0.09f, 0.14f, 0.09f), s.Apron ? shirt : skin);
                Part(PrimitiveType.Sphere, "Guante", hand, new Vector3(0f, -0.06f, 0.01f), new Vector3(0.16f, 0.14f, 0.13f), glove);
                Part(PrimitiveType.Sphere, "Pulgar", hand, new Vector3(-side * 0.065f, -0.035f, 0.045f), Vector3.one * 0.06f, glove);
                Part(PrimitiveType.Cylinder, "Puno", hand, new Vector3(0f, 0.01f, 0f), new Vector3(0.14f, 0.025f, 0.14f), glove);

                // Piernas: muslo, pierna y zapatón.
                Part(PrimitiveType.Capsule, "Muslo", upperLeg, new Vector3(0f, -0.18f, 0f), new Vector3(0.16f * g, 0.2f, 0.16f * g), s.Dress ? skin : pants);
                Part(PrimitiveType.Capsule, "Pierna", lowerLeg, new Vector3(0f, -0.16f, 0f), new Vector3(0.12f, 0.18f, 0.12f), s.Dress ? skin : pants);
                Part(PrimitiveType.Sphere, "Zapato", foot, new Vector3(0f, -0.04f, 0.07f), new Vector3(0.15f, 0.09f, 0.28f), shoes);

                if (sideIndex == 0)
                {
                    rig.leftShoulder = shoulder; rig.leftUpperArm = upperArm; rig.leftLowerArm = lowerArm; rig.leftHand = hand;
                    rig.leftUpperLeg = upperLeg; rig.leftLowerLeg = lowerLeg; rig.leftFoot = foot;
                }
                else
                {
                    rig.rightShoulder = shoulder; rig.rightUpperArm = upperArm; rig.rightLowerArm = lowerArm; rig.rightHand = hand;
                    rig.rightUpperLeg = upperLeg; rig.rightLowerLeg = lowerLeg; rig.rightFoot = foot;
                }
            }

            // Tronco.
            Part(PrimitiveType.Sphere, "Cadera", rig.hips, new Vector3(0f, 0f, 0f), new Vector3(0.36f * g, 0.24f, 0.26f * g), s.Dress ? shirt : pants);
            if (s.Dress) Part(PrimitiveType.Cylinder, "Falda", rig.hips, new Vector3(0f, -0.12f, 0f), new Vector3(0.48f * g, 0.13f, 0.42f * g), shirt);
            Part(PrimitiveType.Sphere, "Barriga", rig.spine, new Vector3(0f, 0.05f, 0.02f * s.Belly), new Vector3(0.4f * g * s.Belly, 0.36f, 0.32f * g * s.Belly), shirt);
            Part(PrimitiveType.Capsule, "Torso", rig.chest, new Vector3(0f, 0.08f, 0f), new Vector3(0.42f * g, 0.2f, 0.28f * g), shirt);
            if (s.Apron)
            {
                Material apron = material("Delantal", new Color32(22, 163, 74, 255));
                Part(PrimitiveType.Cube, "Delantal", rig.spine, new Vector3(0f, 0.02f, 0.17f * g * s.Belly), new Vector3(0.36f * g, 0.52f, 0.03f), apron);
            }
            if (s.Cape) Part(PrimitiveType.Cube, "Capa", rig.chest, new Vector3(0f, -0.08f, -0.16f * g), new Vector3(0.5f * g, 0.62f, 0.03f), costume);
            Part(PrimitiveType.Cylinder, "Cuello", rig.neck, new Vector3(0f, -0.04f, 0f), new Vector3(0.11f, 0.05f, 0.11f), skin);

            BuildHead(rig, s, skin, hair, eye, pupil, mouth, costume, material);

            rig.footHeight = 0.08f;
            rig.hipWidth = 0.11f * g;
            rig.stepHeight = 0.1f;
            rig.strideScale = Mathf.Clamp(s.Height / 1.7f, 0.75f, 1.15f);
            return rig;
        }

        static void BuildHead(ShopperRig rig, Style s, Material skin, Material hair, Material eye, Material pupil, Material mouth, Material costume,
            Func<string, Color, Material> material)
        {
            // Cabeza de dibujo animado: todo lo de la cara cuelga de un cráneo un 20 % más grande.
            Transform head = Empty("Craneo", rig.head, Vector3.zero);
            head.localScale = Vector3.one * 1.2f;
            const float r = 0.17f;
            Part(PrimitiveType.Sphere, "Cara", head, new Vector3(0f, r, 0.01f), new Vector3(0.34f, 0.35f, 0.32f), skin);
            Part(PrimitiveType.Sphere, "Oreja_Izq", head, new Vector3(-0.165f, 0.17f, 0f), new Vector3(0.06f, 0.09f, 0.05f), skin);
            Part(PrimitiveType.Sphere, "Oreja_Der", head, new Vector3(0.165f, 0.17f, 0f), new Vector3(0.06f, 0.09f, 0.05f), skin);
            if (s.LongNose) Part(PrimitiveType.Capsule, "Nariz", head, new Vector3(0f, 0.15f, 0.18f), new Vector3(0.06f, 0.06f, 0.06f), skin).transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            else Part(PrimitiveType.Sphere, "Nariz", head, new Vector3(0f, 0.15f, 0.17f), Vector3.one * 0.075f, skin);

            // Ojos saltones: blanco + pupila; las cejas y la boca se animan.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                GameObject white = Part(PrimitiveType.Sphere, i == 0 ? "Ojo_Izq" : "Ojo_Der", head, new Vector3(side * 0.06f, 0.215f, 0.135f), new Vector3(0.085f, 0.105f, 0.06f), eye);
                GameObject dot = Part(PrimitiveType.Sphere, "Pupila", white.transform, new Vector3(0f, -0.05f, 0.42f), new Vector3(0.45f, 0.42f, 0.4f), pupil);
                GameObject brow = Part(PrimitiveType.Cube, i == 0 ? "Ceja_Izq" : "Ceja_Der", head, new Vector3(side * 0.065f, 0.285f, 0.14f), new Vector3(0.075f, 0.016f, 0.02f), material("Pelo_" + Hex(s.HairColor), s.HairColor));
                brow.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 8f);
                if (i == 0) { rig.leftEye = white.transform; rig.leftPupil = dot.transform; rig.leftBrow = brow.transform; }
                else { rig.rightEye = white.transform; rig.rightPupil = dot.transform; rig.rightBrow = brow.transform; }
            }
            rig.mouth = Part(PrimitiveType.Sphere, "Boca", head, new Vector3(0f, 0.085f, 0.145f), new Vector3(0.1f, 0.03f, 0.04f), mouth).transform;
            if (s.Mustache) Part(PrimitiveType.Capsule, "Bigote", head, new Vector3(0f, 0.115f, 0.16f), new Vector3(0.05f, 0.08f, 0.04f), hair).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            if (s.Glasses)
            {
                Material frame = material("Gafas", new Color32(25, 25, 30, 255));
                Part(PrimitiveType.Cube, "Gafas_Puente", head, new Vector3(0f, 0.25f, 0.165f), new Vector3(0.24f, 0.014f, 0.014f), frame);
                Part(PrimitiveType.Cube, "Gafas_Izq", head, new Vector3(-0.06f, 0.18f, 0.168f), new Vector3(0.1f, 0.012f, 0.012f), frame);
                Part(PrimitiveType.Cube, "Gafas_Der", head, new Vector3(0.06f, 0.18f, 0.168f), new Vector3(0.1f, 0.012f, 0.012f), frame);
            }

            switch (s.HairStyle)
            {
                case Hair.Curly:
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i / 9f * Mathf.PI * 2f;
                        Part(PrimitiveType.Sphere, "Rizo", head, new Vector3(Mathf.Cos(a) * 0.13f, 0.3f + Mathf.Sin(a * 2f) * 0.02f, Mathf.Sin(a) * 0.12f - 0.02f), Vector3.one * 0.12f, hair);
                    }
                    Part(PrimitiveType.Sphere, "Rizo_Arriba", head, new Vector3(0f, 0.34f, -0.02f), Vector3.one * 0.16f, hair);
                    break;
                case Hair.Ponytail:
                    Part(PrimitiveType.Sphere, "Pelo", head, new Vector3(0f, 0.22f, -0.025f), new Vector3(0.36f, 0.33f, 0.33f), hair);
                    Part(PrimitiveType.Capsule, "Coleta", head, new Vector3(0f, 0.2f, -0.2f), new Vector3(0.09f, 0.13f, 0.09f), hair).transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                    break;
                case Hair.Pompadour:
                    Part(PrimitiveType.Sphere, "Pelo", head, new Vector3(0f, 0.25f, -0.03f), new Vector3(0.355f, 0.3f, 0.32f), hair);
                    Part(PrimitiveType.Capsule, "Tupe", head, new Vector3(0f, 0.36f, 0.08f), new Vector3(0.18f, 0.13f, 0.16f), hair).transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
                    break;
                case Hair.BaldSides:
                    Part(PrimitiveType.Sphere, "Pelo_Izq", head, new Vector3(-0.15f, 0.2f, -0.04f), new Vector3(0.07f, 0.1f, 0.14f), hair);
                    Part(PrimitiveType.Sphere, "Pelo_Der", head, new Vector3(0.15f, 0.2f, -0.04f), new Vector3(0.07f, 0.1f, 0.14f), hair);
                    break;
                case Hair.Bun:
                    Part(PrimitiveType.Sphere, "Pelo", head, new Vector3(0f, 0.22f, -0.02f), new Vector3(0.355f, 0.33f, 0.33f), hair);
                    Part(PrimitiveType.Sphere, "Mono", head, new Vector3(0f, 0.42f, -0.06f), Vector3.one * 0.15f, hair);
                    break;
                case Hair.Afro:
                    Part(PrimitiveType.Sphere, "Afro", head, new Vector3(0f, 0.27f, -0.03f), new Vector3(0.5f, 0.44f, 0.46f), hair);
                    break;
                case Hair.Cap:
                    Part(PrimitiveType.Sphere, "Gorra", head, new Vector3(0f, 0.26f, 0f), new Vector3(0.36f, 0.2f, 0.34f), hair);
                    Part(PrimitiveType.Cube, "Visera", head, new Vector3(0f, 0.29f, 0.17f), new Vector3(0.22f, 0.02f, 0.14f), hair);
                    break;
            }

            if (s.WitchHat)
            {
                Part(PrimitiveType.Cylinder, "Ala_Sombrero", head, new Vector3(0f, 0.34f, 0f), new Vector3(0.5f, 0.012f, 0.5f), costume);
                Transform hat = Part(PrimitiveType.Capsule, "Sombrero", head, new Vector3(0f, 0.5f, -0.02f), new Vector3(0.22f, 0.2f, 0.22f), costume).transform;
                Part(PrimitiveType.Capsule, "Punta", hat, new Vector3(0f, 0.9f, -0.15f), new Vector3(0.45f, 0.7f, 0.45f), costume).transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            }
            if (s.Horns)
            {
                Material horn = material("Cuernos", new Color32(185, 28, 28, 255));
                Part(PrimitiveType.Capsule, "Cuerno_Izq", head, new Vector3(-0.09f, 0.36f, 0.03f), new Vector3(0.05f, 0.07f, 0.05f), horn).transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
                Part(PrimitiveType.Capsule, "Cuerno_Der", head, new Vector3(0.09f, 0.36f, 0.03f), new Vector3(0.05f, 0.07f, 0.05f), horn).transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
            }
        }

        /// <summary>Carrito pequeño que empuja el comprador, con dos agarres en el asa.</summary>
        static Transform BuildShopperCart(Transform root, Func<string, Color, Material> material, out Transform leftGrip, out Transform rightGrip, out Transform basket,
            float handleHeight = 0.95f)
        {
            Material metal = material("Metal", new Color32(150, 160, 170, 255));
            Material wheel = material("Rueda", new Color32(40, 44, 52, 255));
            Material handle = material("Asa", new Color32(255, 107, 90, 255));
            Transform cart = Empty("Carrito", root, new Vector3(0f, 0f, 0.7f));
            Part(PrimitiveType.Cube, "Fondo", cart, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.05f, 0.62f), metal);
            Part(PrimitiveType.Cube, "Pared_Frontal", cart, new Vector3(0f, 0.65f, 0.3f), new Vector3(0.6f, 0.4f, 0.03f), metal);
            Part(PrimitiveType.Cube, "Pared_Izq", cart, new Vector3(-0.29f, 0.65f, 0f), new Vector3(0.03f, 0.4f, 0.62f), metal);
            Part(PrimitiveType.Cube, "Pared_Der", cart, new Vector3(0.29f, 0.65f, 0f), new Vector3(0.03f, 0.4f, 0.62f), metal);
            Part(PrimitiveType.Cylinder, "Asa", cart, new Vector3(0f, handleHeight, -0.36f), new Vector3(0.04f, 0.32f, 0.04f), handle).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // Soportes desde dentro de las paredes hasta el asa.
            float supportY = (0.7f + handleHeight) * 0.5f;
            float supportHeight = handleHeight - 0.69f;
            Part(PrimitiveType.Cube, "Soporte_Izq", cart, new Vector3(-0.28f, supportY, -0.33f), new Vector3(0.03f, supportHeight, 0.06f), metal);
            Part(PrimitiveType.Cube, "Soporte_Der", cart, new Vector3(0.28f, supportY, -0.33f), new Vector3(0.03f, supportHeight, 0.06f), metal);
            foreach (Vector3 p in new[] { new Vector3(-0.25f, 0.08f, 0.25f), new Vector3(0.25f, 0.08f, 0.25f), new Vector3(-0.25f, 0.08f, -0.25f), new Vector3(0.25f, 0.08f, -0.25f) })
                Part(PrimitiveType.Cylinder, "Rueda", cart, p, new Vector3(0.14f, 0.03f, 0.14f), wheel).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(PrimitiveType.Cube, "Pata", cart, new Vector3(0f, 0.26f, 0f), new Vector3(0.04f, 0.36f, 0.04f), metal);
            leftGrip = Empty("Agarre_Izq", cart, new Vector3(-0.17f, handleHeight, -0.36f));
            rightGrip = Empty("Agarre_Der", cart, new Vector3(0.17f, handleHeight, -0.36f));
            basket = Empty("Punto_Canasta", cart, new Vector3(0f, 0.6f, 0f));
            return cart;
        }

        // ---------- Utilidades ----------

        static Transform Empty(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        /// <summary>Pieza visual sin colisión: el comprador choca con su cápsula.</summary>
        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            // Las piezas pequeñas no proyectan sombra (mejor rendimiento, misma silueta).
            if (scale.x < 0.06f && scale.y < 0.06f) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
    }
}
