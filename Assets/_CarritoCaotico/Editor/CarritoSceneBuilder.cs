using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Genera la escena jugable de Carrito Caótico: supermercado, carrito, brazo, productos,
    /// caja, cámara, interfaz y referencias conectadas. Menú: Carrito > Construir escena de prueba.
    /// Si la escena ya existe, se guarda una copia de seguridad antes de regenerarla.
    /// </summary>
    public static class CarritoSceneBuilder
    {
        public const string RootFolder = "Assets/_CarritoCaotico";
        public const string ScenesFolder = RootFolder + "/00_Scenes";
        public const string BackupFolder = ScenesFolder + "/Backups";
        public const string MaterialsFolder = RootFolder + "/03_Materials";
        public const string PrefabsFolder = RootFolder + "/02_Prefabs";
        public const string ScenePath = ScenesFolder + "/CarritoCaotico_Prueba.unity";

        public const string LoadingFolder = RootFolder + "/Resources/Carga";

        // ---------- Medidas comunes (metros) ----------
        const float ShelfDepth = 1.2f;
        const float ShelfBoardTop = 0.9f;
        const float AisleSpacing = 5.7f;          // distancia entre centros de pasillo
        const float AisleHalfClearWidth = 2.25f;  // medio ancho libre del pasillo
        const float ProductInset = 0.29f;         // distancia del producto al borde del estante

        static readonly Color32 Teal = new Color32(15, 118, 110, 255);
        static readonly Color32 Yellow = new Color32(255, 201, 60, 255);
        static readonly Color32 Coral = new Color32(233, 79, 55, 255);
        static readonly Color32 Ink = new Color32(31, 41, 51, 255);
        static readonly Color32 Paper = new Color32(255, 253, 247, 255);

        /// <summary>Distribución, aspecto y contenido de un supermercado.</summary>
        sealed class StoreDefinition
        {
            public StoreId Id;
            public string MaterialPrefix;
            public float FloorHalfWidth;
            public float FloorHalfLength;
            public float WallHeight;
            public float ShelfOuterZ;
            public float ShelfInnerZ;
            public float StartX;
            public float StartZ;
            public float CheckoutZ;
            public float[] ProductZ;
            public float[] CorridorsZ;
            public (float x, ProductType left, ProductType right, string label)[] Aisles;

            public Color Floor, Wall, Shelf, ShelfBack, Sign, StartZone, Delivery, Counter, CameraBack, Ambient, SunColor;
            public float SunIntensity;
            public bool Fog;
            public float FogDensity;
        }

        static readonly StoreDefinition ClassicStore = new StoreDefinition
        {
            Id = StoreId.Clasico,
            MaterialPrefix = "",
            FloorHalfWidth = 21f, FloorHalfLength = 25f, WallHeight = 3.5f,
            ShelfOuterZ = 13f, ShelfInnerZ = 1.5f,
            StartX = -AisleSpacing * 0.5f, StartZ = -20f, CheckoutZ = 20f,
            ProductZ = new[] { -11f, -7.25f, -3.5f, 3.5f, 7.25f, 11f },
            CorridorsZ = new[] { -16.5f, 0f, 16f },
            Aisles = new[]
            {
                (-AisleSpacing * 2.5f, ProductType.Leche, ProductType.Queso, "1 · Lácteos"),
                (-AisleSpacing * 1.5f, ProductType.Cereal, ProductType.Huevos, "2 · Desayuno"),
                (-AisleSpacing * 0.5f, ProductType.Sandia, ProductType.Manzana, "3 · Frutas"),
                (AisleSpacing * 0.5f, ProductType.Platano, ProductType.Pan, "4 · Plátanos y pan"),
                (AisleSpacing * 1.5f, ProductType.Agua, ProductType.Lata, "5 · Bebidas y latas"),
                (AisleSpacing * 2.5f, ProductType.PapelHigienico, ProductType.Detergente, "6 · Hogar"),
            },
            Floor = new Color32(176, 170, 160, 255), Wall = new Color32(205, 232, 225, 255),
            Shelf = new Color32(96, 112, 130, 255), ShelfBack = new Color32(150, 170, 186, 255),
            Sign = Paper, StartZone = Yellow, Delivery = new Color32(52, 211, 153, 255), Counter = Teal,
            CameraBack = new Color32(191, 227, 236, 255), Ambient = new Color(0.42f, 0.44f, 0.48f),
            SunColor = new Color(1f, 0.97f, 0.92f), SunIntensity = 0.9f, Fog = false
        };

        static readonly StoreDefinition HalloweenStore = new StoreDefinition
        {
            Id = StoreId.Halloween,
            MaterialPrefix = "H_",
            FloorHalfWidth = 15.5f, FloorHalfLength = 23f, WallHeight = 3.5f,
            ShelfOuterZ = 12f, ShelfInnerZ = 1.5f,
            StartX = -AisleSpacing * 0.5f, StartZ = -19f, CheckoutZ = 18.5f,
            ProductZ = new[] { -10f, -6.75f, -3.5f, 3.5f, 6.75f, 10f },
            CorridorsZ = new[] { -15.5f, 0f, 15f },
            Aisles = new[]
            {
                (-AisleSpacing * 1.5f, ProductType.Calabaza, ProductType.Caramelos, "1 · Calabazas y dulces"),
                (-AisleSpacing * 0.5f, ProductType.Vela, ProductType.Calavera, "2 · Velas y calaveras"),
                (AisleSpacing * 0.5f, ProductType.Pocion, ProductType.SombreroBruja, "3 · Brujería"),
                (AisleSpacing * 1.5f, ProductType.Murcielago, ProductType.ManzanaAcaramelada, "4 · Murciélagos y manzanas"),
            },
            Floor = new Color32(62, 52, 76, 255), Wall = new Color32(46, 30, 66, 255),
            Shelf = new Color32(40, 34, 48, 255), ShelfBack = new Color32(154, 52, 18, 255),
            Sign = new Color32(251, 146, 60, 255), StartZone = new Color32(234, 88, 12, 255),
            Delivery = new Color32(132, 204, 22, 255), Counter = new Color32(88, 28, 135, 255),
            CameraBack = new Color32(26, 14, 38, 255), Ambient = new Color(0.3f, 0.24f, 0.38f),
            SunColor = new Color(0.65f, 0.6f, 1f), SunIntensity = 0.45f, Fog = true, FogDensity = 0.022f
        };

        static readonly StoreDefinition[] Stores = { ClassicStore, HalloweenStore };

        /// <summary>Materiales que cambian de color según el supermercado (se guardan con prefijo).</summary>
        static readonly HashSet<string> ThemedMaterials = new HashSet<string>
        {
            "Suelo", "Pared", "Estante", "EstanteFondo", "Cartel", "ZonaInicio", "ZonaEntrega", "Caja", "Charco"
        };

        static StoreDefinition current = ClassicStore;
        static float FloorHalfWidth => current.FloorHalfWidth;
        static float FloorHalfLength => current.FloorHalfLength;
        static float WallHeight => current.WallHeight;
        static float ShelfOuterZ => current.ShelfOuterZ;
        static float ShelfInnerZ => current.ShelfInnerZ;
        static float StartX => current.StartX;
        static float StartZ => current.StartZ;
        static float CheckoutZ => current.CheckoutZ;
        static float[] ProductZ => current.ProductZ;
        static (float x, ProductType left, ProductType right, string label)[] Aisles => current.Aisles;
        static string CurrentScenePath => ScenePathFor(current.Id);

        public static string ScenePathFor(StoreId store) => $"{ScenesFolder}/{StoreCatalog.SceneName(store)}.unity";

        // ---------- Estado de la construcción ----------
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        static readonly Dictionary<ProductType, GameObject> ProductPrefabs = new Dictionary<ProductType, GameObject>();
        static readonly Dictionary<ProductType, float> ProductBottoms = new Dictionary<ProductType, float>();
        static readonly Dictionary<string, int> Stats = new Dictionary<string, int>();
        static Shader litShader;
        static Font uiFont;
        static PhysicsMaterial cartPhysicsMaterial;
        static PhysicsMaterial productPhysicsMaterial;

        // ======================================================================
        // Menús
        // ======================================================================

        [MenuItem("Carrito/Construir escena de prueba", priority = 0)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ScenePath) || File.Exists(ScenePathFor(StoreId.Halloween)))
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Carrito Caótico",
                    $"Se generan las escenas de los dos supermercados (clásico y Halloween).\n\nSi ya existen, se guardará antes una copia en {BackupFolder}.",
                    "Regenerar (con copia)", "Cancelar", "Abrir la existente");
                if (choice == 1) return;
                if (choice == 2)
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                    return;
                }
            }

            bool ok = Build();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog("Carrito Caótico",
                ok ? "Escenas generadas y validadas.\nPulsa Play para jugar." : "Escenas generadas con avisos. Revisa la consola.",
                "Aceptar");
        }

        [MenuItem("Carrito/Abrir escena de prueba", priority = 1)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Carrito Caótico", "La escena todavía no existe. Usa Carrito > Construir escena de prueba.", "Aceptar");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Carrito/Abrir supermercado de Halloween", priority = 1)]
        public static void OpenHalloweenScene()
        {
            string path = ScenePathFor(StoreId.Halloween);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Carrito Caótico", "La escena todavía no existe. Usa Carrito > Construir escena de prueba.", "Aceptar");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        [MenuItem("Carrito/Validar escena abierta", priority = 2)]
        public static void ValidateOpenScene()
        {
            bool ok = Validate(SceneManager.GetActiveScene(), out string report);
            if (ok) Debug.Log(report);
            else Debug.LogError(report);
        }

        /// <summary>
        /// Punto de entrada para línea de comandos:
        /// Unity -batchmode -projectPath . -executeMethod CarritoCaotico.EditorTools.CarritoSceneBuilder.BuildFromCommandLine -quit
        /// </summary>
        public static void BuildFromCommandLine()
        {
            bool ok;
            try
            {
                ok = Build();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ok = false;
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        // ======================================================================
        // Construcción
        // ======================================================================

        /// <summary>Construye las escenas de todos los supermercados. true si todas validan.</summary>
        public static bool Build()
        {
            bool ok = true;
            foreach (StoreDefinition store in Stores)
                ok &= BuildStore(store);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            return ok;
        }

        /// <summary>Construye y guarda la escena de un supermercado. Devuelve true si la validación es correcta.</summary>
        static bool BuildStore(StoreDefinition store)
        {
            current = store;
            Stats.Clear();
            Materials.Clear();
            ProductPrefabs.Clear();
            ProductBottoms.Clear();

            EnsureFolders();
            BackupExistingScene();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PrepareSharedResources();
            CreateMaterials();
            CreateProductPrefabs();

            BuildLighting();
            Transform level = new GameObject("Supermercado").transform;
            BuildFloorAndWalls(level);
            BuildShelves(level);
            BuildObstacles(level);
            if (current.Id == StoreId.Halloween) BuildHalloweenDecor(level);
            Checkout checkout = BuildCheckout(level);
            PlaceProducts();

            CartParts cart = BuildCart();
            CameraController cameraController = BuildCamera(cart);
            SetArray(cameraController, "hideInFirstPerson", cart.PusherRenderers);

            GameObject systems = new GameObject("Sistemas");
            var manager = systems.AddComponent<GameManager>();
            var timer = systems.AddComponent<GameTimer>();
            var shoppingList = systems.AddComponent<ShoppingList>();
            var rivalManager = systems.AddComponent<RivalManager>();
            var audioDirector = systems.AddComponent<AudioDirector>();
            Count("Sistemas (GameManager, GameTimer, ShoppingList, RivalManager, AudioDirector)");
            RivalShopper rivalPrefab = CreateRivalPrefab();

            var (ui, menu, tutorial) = HudBuilder.Build(uiFont, current.Id);
            Stats.TryGetValue("Elementos de UI", out int signElements);
            Stats["Elementos de UI"] = signElements + HudBuilder.ElementCount;
            Count("Interfaz (Canvas, EventSystem)");

            // ---------- Conexión de referencias ----------
            SetRefs(cart.Contents, ("cart", cart.Controller));
            SetRefs(shoppingList, ("cartContents", cart.Contents));
            SetRefs(checkout, ("gameManager", manager));
            SetRefs(cameraController, ("target", cart.Controller.transform), ("targetBody", cart.Body));
            CartHealth cartHealth = cart.Controller.GetComponent<CartHealth>();
            SetRefs(manager,
                ("timer", timer), ("shoppingList", shoppingList), ("cartContents", cart.Contents),
                ("cart", cart.Controller), ("grabber", cart.Arm), ("cameraController", cameraController),
                ("rivals", rivalManager), ("cartHealth", cartHealth));
            SetRefs(ui,
                ("gameManager", manager), ("timer", timer), ("shoppingList", shoppingList),
                ("checkout", checkout), ("rivals", rivalManager));
            SetRefs(menu, ("gameManager", manager));
            SetRefs(tutorial,
                ("gameManager", manager), ("cart", cart.Controller), ("grabber", cart.Arm),
                ("cartContents", cart.Contents), ("shoppingList", shoppingList), ("cameraController", cameraController));
            SetRefs(audioDirector,
                ("library", SoundAssets.BuildLibrary()), ("gameManager", manager), ("timer", timer),
                ("shoppingList", shoppingList), ("cartContents", cart.Contents), ("cart", cart.Controller),
                ("grabber", cart.Arm), ("checkout", checkout), ("rivals", rivalManager));
            ConfigureStore(manager, shoppingList);
            ConfigureRivals(rivalManager, rivalPrefab, shoppingList, cart.Contents);
            AudioBuilder.Build(cartHealth);
            Count("Audio (GameAudio con música y efectos)");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, CurrentScenePath))
            {
                Debug.LogError($"[Carrito] No se pudo guardar la escena en {CurrentScenePath}");
                return false;
            }

            bool valid = Validate(scene, out string report);
            CaptureLoadingImages();
            Debug.Log(BuildSummary() + "\n" + report);
            if (!valid) Debug.LogError("[Carrito] La validación encontró problemas. Revisa el informe anterior.");
            return valid;
        }

        static void EnsureFolders()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(ScenesFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void BackupExistingScene()
        {
            if (!File.Exists(CurrentScenePath)) return;
            EnsureFolder(BackupFolder);
            string backupPath = $"{BackupFolder}/{StoreCatalog.SceneName(current.Id)}_{DateTime.Now:yyyyMMdd_HHmmss}.unity";
            if (AssetDatabase.CopyAsset(CurrentScenePath, backupPath))
                Debug.Log($"[Carrito] Copia de seguridad de la escena anterior: {backupPath}");
            else
                throw new IOException($"No se pudo copiar {CurrentScenePath} a {backupPath}. Se cancela para no perder cambios.");
        }

        static void PrepareSharedResources()
        {
            litShader = null;
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null) litShader = Shader.Find("HDRP/Lit");
            }
            if (litShader == null) litShader = Shader.Find("Standard");
            if (litShader == null) throw new InvalidOperationException("No se encontró un shader Lit/Standard.");

            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            cartPhysicsMaterial = GetOrCreatePhysicsMaterial("PM_Carrito", 0f, 0f, PhysicsMaterialCombine.Minimum, 0.1f);
            productPhysicsMaterial = GetOrCreatePhysicsMaterial("PM_Producto", 0.7f, 0.6f, PhysicsMaterialCombine.Average, 0.15f);
        }

        static PhysicsMaterial GetOrCreatePhysicsMaterial(string name, float dynamicFriction, float staticFriction,
            PhysicsMaterialCombine frictionCombine, float bounciness)
        {
            string path = $"{MaterialsFolder}/{name}.asset";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial(name);
                AssetDatabase.CreateAsset(material, path);
                Count("Materiales físicos creados");
            }
            else Count("Materiales físicos reutilizados");

            material.dynamicFriction = dynamicFriction;
            material.staticFriction = staticFriction;
            material.frictionCombine = frictionCombine;
            material.bounciness = bounciness;
            material.bounceCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void CreateMaterials()
        {
            GetMaterial("Suelo", current.Floor);
            GetMaterial("Pared", current.Wall);
            GetMaterial("Estante", current.Shelf);
            GetMaterial("EstanteFondo", current.ShelfBack);
            GetMaterial("Metal", new Color32(150, 160, 170, 255));
            GetMaterial("Rueda", new Color32(40, 44, 52, 255));
            GetMaterial("Asa", Coral);
            foreach (CharacterModels.Definition character in CharacterModels.All)
                CharacterModels.ApplyTexture(GetMaterial(character.Folder, Color.white), character);
            foreach (NpcModels.Definition npc in NpcModels.ForStore(current.Id))
                NpcModels.ApplyTexture(GetMaterial(npc.MaterialName, Color.white), npc);
            if (current.Id == StoreId.Halloween) NpcModels.MakeGlow(GetMaterial("NPC_Ojos", new Color32(120, 255, 220, 255)), new Color32(120, 255, 220, 255));
            GetMaterial("Piel", new Color32(244, 200, 160, 255));
            GetMaterial("Brazo", new Color32(255, 140, 66, 255));
            GetMaterial("Mano", new Color32(255, 61, 127, 255));
            GetMaterial("SombraMano", new Color32(30, 30, 40, 255));
            GetMaterial("ZonaInicio", current.StartZone);
            GetMaterial("ZonaEntrega", current.Delivery);
            GetMaterial("Caja", current.Counter);
            GetMaterial("CajaCinta", new Color32(30, 30, 35, 255));
            GetMaterial("Registradora", new Color32(55, 65, 81, 255));
            GetMaterial("Carton", new Color32(196, 145, 92, 255));
            GetMaterial("PisoMojado", Yellow);
            GetMaterial("Cartel", current.Sign);
            GetMaterial("Rival", new Color32(235, 235, 235, 255));
            // Obstáculos.
            GetMaterial("Charco", current.Id == StoreId.Halloween ? new Color32(132, 204, 22, 255) : new Color32(96, 165, 250, 255));
            GetMaterial("Palet", new Color32(161, 120, 78, 255));
            GetMaterial("Reponedor", new Color32(22, 163, 74, 255));
            GetMaterial("CarritoViejo", new Color32(120, 113, 108, 255));
            // Decoración de Halloween.
            GetMaterial("Calabaza", new Color32(234, 88, 12, 255));
            GetMaterial("CalabazaCara", new Color32(254, 240, 138, 255));
            GetMaterial("Lapida", new Color32(120, 113, 128, 255));
            GetMaterial("Murcielago", new Color32(24, 20, 30, 255));
            GetMaterial("Telarana", new Color32(226, 232, 240, 255));
            GetMaterial("Caldero", new Color32(30, 27, 36, 255));
            GetMaterial("Baba", new Color32(132, 204, 22, 255));
            GetMaterial("Fantasma", new Color32(241, 245, 249, 255));
            GetMaterial("FantasmaOjos", new Color32(15, 23, 42, 255));
            foreach (ProductType type in ProductCatalog.AllTypes)
                GetMaterial("Stock_" + type, Color.Lerp(ProductModels.MainColor(type), new Color(0.45f, 0.45f, 0.45f), 0.35f));
        }

        static Material GetMaterial(string name, Color color)
        {
            if (Materials.TryGetValue(name, out Material cached)) return cached;

            string prefix = ThemedMaterials.Contains(name) ? current.MaterialPrefix : "";
            string path = $"{MaterialsFolder}/M_{prefix}{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(litShader) { name = "M_" + name };
                AssetDatabase.CreateAsset(material, path);
                Count("Materiales creados");
            }
            else
            {
                if (material.shader != litShader) material.shader = litShader;
                Count("Materiales reutilizados");
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.15f);
            EditorUtility.SetDirty(material);
            Materials[name] = material;
            return material;
        }

        // ---------- Utilidades de primitivas ----------

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale,
            string materialName, bool keepCollider = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Materials[materialName];
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            Count("Primitivas");
            return go;
        }

        static GameObject Empty(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        static void AddSign(Transform parent, string text, Vector3 localPosition, float yRotation, float width)
        {
            GameObject board = Prim(PrimitiveType.Cube, "Cartel_" + text, parent, localPosition, new Vector3(width, 0.55f, 0.06f), "Cartel", false);
            board.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);

            // Canvas en espacio de mundo: a diferencia de TextMesh, respeta la profundidad
            // y no se dibuja por encima de otros objetos.
            var canvasGo = new GameObject("Texto_" + text, typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localRotation = board.transform.localRotation;
            canvasGo.transform.localPosition = localPosition + board.transform.localRotation * new Vector3(0f, 0f, -0.035f);
            canvasGo.transform.localScale = Vector3.one * 0.01f;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(width * 100f, 55f);

            Text label = Label(canvasGo.transform, "Texto", text, 32, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform, 8f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = 34;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            Count("Carteles");
        }

        // ---------- Entorno ----------

        static void BuildLighting()
        {
            var sun = new GameObject("Luz direccional");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = current.SunIntensity;
            light.color = current.SunColor;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = current.Ambient;
            RenderSettings.skybox = null;
            RenderSettings.fog = current.Fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = current.FogDensity;
            RenderSettings.fogColor = current.CameraBack;
            Count("Luces");
        }

        static void BuildFloorAndWalls(Transform level)
        {
            Prim(PrimitiveType.Cube, "Suelo", level, new Vector3(0f, -0.5f, 0f),
                new Vector3(FloorHalfWidth * 2f, 1f, FloorHalfLength * 2f), "Suelo");

            Transform walls = Empty("Paredes", level, Vector3.zero).transform;
            float t = 0.4f;
            Prim(PrimitiveType.Cube, "Pared_Norte", walls, new Vector3(0f, WallHeight * 0.5f, FloorHalfLength + t * 0.5f), new Vector3(FloorHalfWidth * 2f + t * 2f, WallHeight, t), "Pared");
            Prim(PrimitiveType.Cube, "Pared_Sur", walls, new Vector3(0f, WallHeight * 0.5f, -FloorHalfLength - t * 0.5f), new Vector3(FloorHalfWidth * 2f + t * 2f, WallHeight, t), "Pared");
            Prim(PrimitiveType.Cube, "Pared_Este", walls, new Vector3(FloorHalfWidth + t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, FloorHalfLength * 2f), "Pared");
            Prim(PrimitiveType.Cube, "Pared_Oeste", walls, new Vector3(-FloorHalfWidth - t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, FloorHalfLength * 2f), "Pared");

            Prim(PrimitiveType.Cube, "Zona_Inicio", level, new Vector3(StartX, 0.005f, StartZ), new Vector3(3f, 0.01f, 3f), "ZonaInicio", false);
        }

        static void BuildShelves(Transform level)
        {
            Transform shelves = Empty("Estantes", level, Vector3.zero).transform;
            // Siete estantes: uno entre cada par de pasillos y uno en cada extremo.
            for (int i = 0; i <= Aisles.Length; i++)
            {
                float x = (i - Aisles.Length * 0.5f) * AisleSpacing;
                // Tipo de producto de cada cara, para el stock decorativo de la balda inferior.
                ProductType? leftFace = i > 0 ? Aisles[i - 1].right : (ProductType?)null;
                ProductType? rightFace = i < Aisles.Length ? Aisles[i].left : (ProductType?)null;
                BuildShelf(shelves, $"Estante_{i + 1}_Sur", x, -ShelfOuterZ, -ShelfInnerZ, leftFace, rightFace);
                BuildShelf(shelves, $"Estante_{i + 1}_Norte", x, ShelfInnerZ, ShelfOuterZ, leftFace, rightFace);
            }

            Transform signs = Empty("Carteles", level, Vector3.zero).transform;
            foreach (var aisle in Aisles)
            {
                string label = "Pasillo " + aisle.label;
                AddSign(signs, label, new Vector3(aisle.x, 3.3f, -ShelfOuterZ - 0.4f), 0f, 4.4f);
                AddSign(signs, label, new Vector3(aisle.x, 3.3f, ShelfOuterZ + 0.4f), 180f, 4.4f);
            }
        }

        /// <summary>Tramo de estante doble cara entre zStart y zEnd.</summary>
        static void BuildShelf(Transform parent, string name, float x, float zStart, float zEnd,
            ProductType? leftFace, ProductType? rightFace)
        {
            float length = zEnd - zStart;
            Transform shelf = Empty(name, parent, new Vector3(x, 0f, (zStart + zEnd) * 0.5f)).transform;

            Prim(PrimitiveType.Cube, "Base", shelf, new Vector3(0f, 0.15f, 0f), new Vector3(ShelfDepth, 0.3f, length), "Estante");
            Prim(PrimitiveType.Cube, "Balda", shelf, new Vector3(0f, ShelfBoardTop - 0.05f, 0f), new Vector3(ShelfDepth, 0.1f, length), "Estante");
            // Balda superior estrecha: no tapa los productos vistos desde la cámara elevada.
            Prim(PrimitiveType.Cube, "Balda_Superior", shelf, new Vector3(0f, 1.65f, 0f), new Vector3(0.36f, 0.08f, length), "Estante");
            Prim(PrimitiveType.Cube, "Fondo", shelf, new Vector3(0f, 1.0f, 0f), new Vector3(0.08f, 1.9f, length), "EstanteFondo");

            // Bordes para que los productos no rueden fuera de la balda.
            float edge = ShelfDepth * 0.5f - 0.02f;
            Prim(PrimitiveType.Cube, "Borde_Izq", shelf, new Vector3(-edge, ShelfBoardTop + 0.04f, 0f), new Vector3(0.04f, 0.08f, length), "Metal");
            Prim(PrimitiveType.Cube, "Borde_Der", shelf, new Vector3(edge, ShelfBoardTop + 0.04f, 0f), new Vector3(0.04f, 0.08f, length), "Metal");

            float half = length * 0.5f;
            foreach (float z in new[] { -half, 0f, half })
                Prim(PrimitiveType.Cube, "Poste", shelf, new Vector3(0f, 0.95f, z), new Vector3(ShelfDepth, 1.9f, 0.08f), "Estante");

            // Stock decorativo (sin collider) en la balda inferior, del color de los productos
            // de cada cara, para reconocer el pasillo de lejos.
            Transform stock = Empty("Stock", shelf, Vector3.zero).transform;
            float side = ShelfDepth * 0.5f - 0.3f;
            for (float z = -half + 0.7f; z < half - 0.5f; z += 1.6f)
            {
                if (leftFace.HasValue)
                    Prim(PrimitiveType.Cube, "Paquete", stock, new Vector3(-side, 0.45f, z + 0.4f), new Vector3(0.4f, 0.3f, 0.7f), "Stock_" + leftFace.Value, false);
                if (rightFace.HasValue)
                    Prim(PrimitiveType.Cube, "Paquete", stock, new Vector3(side, 0.45f, z + 0.4f), new Vector3(0.4f, 0.3f, 0.7f), "Stock_" + rightFace.Value, false);
            }
            foreach (Transform child in stock)
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);

            Count("Tramos de estante");
        }

        /// <summary>
        /// Obstáculos: charcos resbaladizos, pilas que se derriban, carritos abandonados, palés y
        /// un obstáculo que se mueve (reponedor o fantasma). Ninguno bloquea del todo un pasillo.
        /// </summary>
        static void BuildObstacles(Transform level)
        {
            Transform obstacles = Empty("Obstaculos", level, Vector3.zero).transform;
            bool halloween = current.Id == StoreId.Halloween;
            float[] corridors = current.CorridorsZ;

            // Charcos (baba verde en Halloween).
            var spills = halloween
                ? new[] { new Vector3(Aisles[0].x, 0f, -6f), new Vector3(Aisles[2].x, 0f, 7f), new Vector3(AisleSpacing, 0f, corridors[2]) }
                : new[] { new Vector3(Aisles[1].x, 0f, -6.5f), new Vector3(Aisles[4].x, 0f, 6.5f), new Vector3(0f, 0f, corridors[2]) };
            foreach (Vector3 p in spills) Spill(obstacles, p);

            // Pila que se puede derribar: cajas en el clásico, calabazas decorativas en Halloween.
            Vector3 pilePosition = new Vector3(Aisles[Aisles.Length - 2].x, 0f, StartZ + 2.5f);
            if (halloween) PumpkinPile(obstacles, pilePosition);
            else BoxPyramid(obstacles, pilePosition);
            if (!halloween) BoxPyramid(obstacles, new Vector3(Aisles[0].x, 0f, corridors[2] + 1.6f));

            // Carritos abandonados en el pasillo transversal central (se pueden empujar).
            AbandonedCart(obstacles, new Vector3(-AisleSpacing, 0f, 0.4f), 25f);
            AbandonedCart(obstacles, new Vector3(halloween ? AisleSpacing : AisleSpacing * 2f, 0f, -0.4f), -40f);

            // Palés junto a las paredes laterales.
            Palet(obstacles, new Vector3(-FloorHalfWidth + 1.4f, 0f, 7f));
            Palet(obstacles, new Vector3(FloorHalfWidth - 1.4f, 0f, -6f));

            if (halloween)
            {
                // Fantasmas que flotan por los pasillos transversales.
                Ghost(obstacles, new Vector3(-FloorHalfWidth + 3f, 0.9f, 0.9f), new Vector3(FloorHalfWidth - 3f, 0.9f, 0.9f), 1.2f);
                Ghost(obstacles, new Vector3(FloorHalfWidth - 3f, 0.9f, corridors[2] - 1.4f), new Vector3(-FloorHalfWidth + 3f, 0.9f, corridors[2] - 1.4f), 1.5f);
                // Lápidas fijas en los pasillos exteriores.
                foreach (Vector3 p in new[] { new Vector3(-13.6f, 0f, -9f), new Vector3(13.6f, 0f, 4f), new Vector3(-13.6f, 0f, 8f), new Vector3(13.6f, 0f, -11f) })
                    Tombstone(obstacles, p);
            }
            else
            {
                // Reponedor que cruza la zona de entrada.
                Stocker(obstacles, new Vector3(-FloorHalfWidth + 3f, 0f, corridors[0] + 2f), new Vector3(FloorHalfWidth - 3f, 0f, corridors[0] + 2f));
                // Cartel de piso mojado junto al primer charco.
                GameObject wetSign = Prim(PrimitiveType.Cube, "Piso_Mojado", obstacles, spills[0] + new Vector3(1.3f, 0.35f, -1.2f),
                    new Vector3(0.5f, 0.7f, 0.3f), "PisoMojado");
                var wetBody = wetSign.AddComponent<Rigidbody>();
                wetBody.mass = 2f;
                Count("Obstáculos dinámicos");
            }
        }

        static void Spill(Transform parent, Vector3 position)
        {
            Transform spill = Empty("Charco", parent, position).transform;
            Prim(PrimitiveType.Cylinder, "Mancha", spill, new Vector3(0f, 0.006f, 0f), new Vector3(2.6f, 0.004f, 1.8f), "Charco", false);
            Prim(PrimitiveType.Cylinder, "Mancha_2", spill, new Vector3(0.9f, 0.007f, 0.5f), new Vector3(1.2f, 0.004f, 0.9f), "Charco", false);
            var trigger = spill.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0.2f, 0.5f, 0.1f);
            trigger.size = new Vector3(3f, 1f, 2.2f);
            spill.gameObject.AddComponent<SlipperyZone>();
            Count("Obstáculos: charcos");
        }

        static void BoxPyramid(Transform parent, Vector3 position)
        {
            Transform pyramid = Empty("Promocion_Cajas", parent, position).transform;
            int[] rows = { 3, 2, 1 };
            float size = 0.5f;
            for (int row = 0; row < rows.Length; row++)
            {
                for (int i = 0; i < rows[row]; i++)
                {
                    float offset = (i - (rows[row] - 1) * 0.5f) * (size + 0.02f);
                    GameObject box = Prim(PrimitiveType.Cube, "Caja_Carton", pyramid,
                        new Vector3(offset, size * 0.5f + row * size, 0f), Vector3.one * size, "Carton");
                    var rb = box.AddComponent<Rigidbody>();
                    rb.mass = 1.5f;
                    Count("Obstáculos dinámicos");
                }
            }
        }

        static void PumpkinPile(Transform parent, Vector3 position)
        {
            Transform pile = Empty("Pila_Calabazas", parent, position).transform;
            Vector3[] spots = { new Vector3(-0.5f, 0.3f, 0f), new Vector3(0.1f, 0.3f, 0.2f), new Vector3(0.6f, 0.3f, -0.1f), new Vector3(-0.15f, 0.8f, 0.05f), new Vector3(0.4f, 0.8f, 0.05f) };
            foreach (Vector3 spot in spots)
            {
                GameObject pumpkin = Prim(PrimitiveType.Sphere, "Calabaza_Decorativa", pile, spot, new Vector3(0.6f, 0.48f, 0.6f), "Calabaza");
                Prim(PrimitiveType.Cylinder, "Tallo", pumpkin.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 0.15f, 0.12f), "Reponedor", false);
                var rb = pumpkin.AddComponent<Rigidbody>();
                rb.mass = 3f;
                rb.angularDamping = 2f;
                Count("Obstáculos dinámicos");
            }
        }

        static void AbandonedCart(Transform parent, Vector3 position, float yaw)
        {
            GameObject cart = Empty("Carrito_Abandonado", parent, position);
            cart.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform t = cart.transform;
            var box = cart.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(0.9f, 1f, 1.4f);
            box.sharedMaterial = cartPhysicsMaterial;
            Prim(PrimitiveType.Cube, "Fondo", t, new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.05f, 1.4f), "CarritoViejo", false);
            Prim(PrimitiveType.Cube, "Pared_Frontal", t, new Vector3(0f, 0.7f, 0.68f), new Vector3(0.9f, 0.55f, 0.04f), "CarritoViejo", false);
            Prim(PrimitiveType.Cube, "Pared_Trasera", t, new Vector3(0f, 0.7f, -0.68f), new Vector3(0.9f, 0.55f, 0.04f), "CarritoViejo", false);
            Prim(PrimitiveType.Cube, "Pared_Izq", t, new Vector3(-0.43f, 0.7f, 0f), new Vector3(0.04f, 0.55f, 1.4f), "CarritoViejo", false);
            Prim(PrimitiveType.Cube, "Pared_Der", t, new Vector3(0.43f, 0.7f, 0f), new Vector3(0.04f, 0.55f, 1.4f), "CarritoViejo", false);
            foreach (Vector3 w in new[] { new Vector3(-0.35f, 0.08f, 0.55f), new Vector3(0.35f, 0.08f, 0.55f), new Vector3(-0.35f, 0.08f, -0.55f), new Vector3(0.35f, 0.08f, -0.55f) })
            {
                GameObject wheel = Prim(PrimitiveType.Cylinder, "Rueda", t, w, new Vector3(0.16f, 0.03f, 0.16f), "Rueda", false);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            var rb = cart.AddComponent<Rigidbody>();
            rb.mass = 20f;
            rb.linearDamping = 1.5f;
            rb.angularDamping = 3f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            Count("Obstáculos dinámicos");
        }

        static void Palet(Transform parent, Vector3 position)
        {
            Transform palet = Empty("Palet", parent, position).transform;
            Prim(PrimitiveType.Cube, "Base", palet, new Vector3(0f, 0.08f, 0f), new Vector3(1.2f, 0.16f, 1.2f), "Palet");
            Prim(PrimitiveType.Cube, "Carga", palet, new Vector3(0f, 0.6f, 0f), new Vector3(1.1f, 0.9f, 1.1f), "Carton");
            Count("Obstáculos fijos");
        }

        static void Stocker(Transform parent, Vector3 a, Vector3 b)
        {
            GameObject stocker = Empty("Reponedor", parent, a);
            var body = stocker.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var capsule = stocker.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            Prim(PrimitiveType.Capsule, "Cuerpo", stocker.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.55f, 0.75f, 0.55f), "Reponedor", false);
            Prim(PrimitiveType.Sphere, "Cabeza", stocker.transform, new Vector3(0f, 1.65f, 0f), Vector3.one * 0.32f, "Piel", false);
            Prim(PrimitiveType.Cube, "Caja", stocker.transform, new Vector3(0f, 1.05f, 0.4f), new Vector3(0.6f, 0.45f, 0.4f), "Carton", false);
            stocker.AddComponent<MovingObstacle>().Configure(a, b, 1.4f, 0f);
            Count("Obstáculos móviles");
        }

        static void Ghost(Transform parent, Vector3 a, Vector3 b, float speed)
        {
            GameObject ghost = Empty("Fantasma", parent, a);
            var body = ghost.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var capsule = ghost.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.3f, 0f);
            capsule.height = 1.6f;
            capsule.radius = 0.45f;
            Transform t = ghost.transform;
            Prim(PrimitiveType.Capsule, "Sabana", t, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.8f, 0.9f), "Fantasma", false);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                Prim(PrimitiveType.Sphere, "Borde", t, new Vector3(Mathf.Cos(angle) * 0.32f, -0.42f, Mathf.Sin(angle) * 0.32f), Vector3.one * 0.32f, "Fantasma", false);
            }
            Prim(PrimitiveType.Sphere, "Ojo_Izq", t, new Vector3(-0.15f, 0.65f, 0.4f), new Vector3(0.12f, 0.18f, 0.08f), "FantasmaOjos", false);
            Prim(PrimitiveType.Sphere, "Ojo_Der", t, new Vector3(0.15f, 0.65f, 0.4f), new Vector3(0.12f, 0.18f, 0.08f), "FantasmaOjos", false);
            Prim(PrimitiveType.Sphere, "Boca", t, new Vector3(0f, 0.38f, 0.42f), new Vector3(0.14f, 0.16f, 0.06f), "FantasmaOjos", false);
            ghost.AddComponent<MovingObstacle>().Configure(a, b, speed, 0.18f);
            Count("Obstáculos móviles");
        }

        static void Tombstone(Transform parent, Vector3 position)
        {
            Transform stone = Empty("Lapida", parent, position).transform;
            Prim(PrimitiveType.Cube, "Piedra", stone, new Vector3(0f, 0.5f, 0f), new Vector3(0.8f, 1f, 0.25f), "Lapida");
            Prim(PrimitiveType.Cylinder, "Remate", stone, new Vector3(0f, 1f, 0f), new Vector3(0.8f, 0.125f, 0.8f), "Lapida", false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Cruz_V", stone, new Vector3(0f, 0.6f, -0.13f), new Vector3(0.08f, 0.4f, 0.02f), "FantasmaOjos", false);
            Prim(PrimitiveType.Cube, "Cruz_H", stone, new Vector3(0f, 0.68f, -0.13f), new Vector3(0.26f, 0.08f, 0.02f), "FantasmaOjos", false);
            Count("Obstáculos fijos");
        }

        // ---------- Decoración de Halloween ----------

        static void BuildHalloweenDecor(Transform level)
        {
            Transform decor = Empty("Decoracion_Halloween", level, Vector3.zero).transform;

            // Calabazas iluminadas sobre los extremos de los estantes que dan a la entrada.
            for (int i = 0; i <= Aisles.Length; i++)
            {
                float x = (i - Aisles.Length * 0.5f) * AisleSpacing;
                JackOLantern(decor, new Vector3(x, 1.95f, -ShelfOuterZ + 0.3f), i % 2 == 0);
            }

            // Murciélagos colgando sobre los pasillos.
            for (int i = 0; i < Aisles.Length; i++)
            {
                for (int k = 0; k < 3; k++)
                    Bat(decor, new Vector3(Aisles[i].x + (k - 1) * 0.9f, 3.1f - k % 2 * 0.25f, -8f + k * 7f + i));
            }

            // Telarañas en las esquinas.
            foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
                Cobweb(decor, new Vector3(corner.x * (FloorHalfWidth - 0.4f), WallHeight - 0.5f, corner.y * (FloorHalfLength - 0.4f)), corner);
        }

        static void JackOLantern(Transform parent, Vector3 position, bool withLight)
        {
            Transform pumpkin = Empty("Calabaza_Farol", parent, position).transform;
            Prim(PrimitiveType.Sphere, "Calabaza", pumpkin, Vector3.zero, new Vector3(0.6f, 0.48f, 0.6f), "Calabaza", false);
            Prim(PrimitiveType.Cube, "Ojo_Izq", pumpkin, new Vector3(-0.12f, 0.06f, -0.29f), new Vector3(0.1f, 0.1f, 0.04f), "CalabazaCara", false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Prim(PrimitiveType.Cube, "Ojo_Der", pumpkin, new Vector3(0.12f, 0.06f, -0.29f), new Vector3(0.1f, 0.1f, 0.04f), "CalabazaCara", false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Prim(PrimitiveType.Cube, "Boca", pumpkin, new Vector3(0f, -0.09f, -0.29f), new Vector3(0.28f, 0.06f, 0.04f), "CalabazaCara", false);
            Prim(PrimitiveType.Cylinder, "Tallo", pumpkin, new Vector3(0f, 0.27f, 0f), new Vector3(0.07f, 0.06f, 0.07f), "Reponedor", false);
            if (withLight)
            {
                var light = new GameObject("Luz").AddComponent<Light>();
                light.transform.SetParent(pumpkin, false);
                light.transform.localPosition = new Vector3(0f, 0.2f, -0.8f);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.55f, 0.15f);
                light.range = 9f;
                light.intensity = 1.6f;
                Count("Luces");
            }
            Count("Decoración");
        }

        static void Bat(Transform parent, Vector3 position)
        {
            Transform bat = Empty("Murcielago", parent, position).transform;
            Prim(PrimitiveType.Sphere, "Cuerpo", bat, Vector3.zero, new Vector3(0.2f, 0.24f, 0.18f), "Murcielago", false);
            Prim(PrimitiveType.Cube, "Ala_Izq", bat, new Vector3(-0.24f, 0.04f, 0f), new Vector3(0.32f, 0.14f, 0.02f), "Murcielago", false).transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            Prim(PrimitiveType.Cube, "Ala_Der", bat, new Vector3(0.24f, 0.04f, 0f), new Vector3(0.32f, 0.14f, 0.02f), "Murcielago", false).transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
            Prim(PrimitiveType.Cylinder, "Hilo", bat, new Vector3(0f, 0.4f, 0f), new Vector3(0.01f, 0.3f, 0.01f), "Telarana", false);
            Count("Decoración");
        }

        static void Cobweb(Transform parent, Vector3 position, Vector2 corner)
        {
            Transform web = Empty("Telarana", parent, position).transform;
            web.localRotation = Quaternion.Euler(0f, Mathf.Atan2(corner.x, corner.y) * Mathf.Rad2Deg, 0f);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, "Hebra", web, Vector3.zero, new Vector3(1.6f, 0.02f, 0.02f), "Telarana", false).transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
            for (int r = 1; r <= 2; r++)
                Prim(PrimitiveType.Cylinder, "Anillo", web, Vector3.zero, new Vector3(r * 0.6f, 0.005f, r * 0.6f), "Telarana", false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Count("Decoración");
        }

        static Checkout BuildCheckout(Transform level)
        {
            Transform area = Empty("Caja_Registradora", level, new Vector3(0f, 0f, CheckoutZ)).transform;

            // Mostradores a ambos lados de la zona de entrega.
            foreach (float x in new[] { -6.5f, -3.2f, 3.2f, 6.5f })
            {
                Transform counter = Empty("Mostrador", area, new Vector3(x, 0f, 0f)).transform;
                Prim(PrimitiveType.Cube, "Mueble", counter, new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 1f, 3f), "Caja");
                Prim(PrimitiveType.Cube, "Cinta", counter, new Vector3(0f, 1.02f, 0.2f), new Vector3(1f, 0.04f, 2.4f), "CajaCinta", false);
                Prim(PrimitiveType.Cube, "Registradora", counter, new Vector3(0f, 1.25f, -1.1f), new Vector3(0.5f, 0.45f, 0.45f), "Registradora");
                Prim(PrimitiveType.Cube, "Pantalla", counter, new Vector3(-0.25f, 1.6f, -1.1f), new Vector3(0.05f, 0.3f, 0.4f), "ZonaEntrega", false);
            }
            AddSign(area, current.Id == StoreId.Halloween ? "CAJA  ·  Paguen al caldero" : "CAJA  ·  Entrega aquí", new Vector3(0f, 2.8f, 2.2f), 0f, 4.4f);
            if (current.Id == StoreId.Halloween)
            {
                Transform cauldron = Empty("Caldero", area, new Vector3(-4.8f, 0f, 1.9f)).transform;
                Prim(PrimitiveType.Cylinder, "Olla", cauldron, new Vector3(0f, 0.45f, 0f), new Vector3(1.1f, 0.45f, 1.1f), "Caldero");
                Prim(PrimitiveType.Cylinder, "Pocion", cauldron, new Vector3(0f, 0.91f, 0f), new Vector3(0.95f, 0.02f, 0.95f), "Baba", false);
                var glow = new GameObject("Luz_Caldero").AddComponent<Light>();
                glow.transform.SetParent(cauldron, false);
                glow.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                glow.type = LightType.Point;
                glow.color = new Color(0.55f, 1f, 0.2f);
                glow.range = 8f;
                glow.intensity = 2f;
                Count("Luces");
            }

            Prim(PrimitiveType.Cube, "Marca_Entrega", area, new Vector3(0f, 0.005f, 0f), new Vector3(4.6f, 0.01f, 3f), "ZonaEntrega", false);

            GameObject zone = Empty("Zona_Entrega", area, new Vector3(0f, 1f, 0f));
            var trigger = zone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(4.6f, 2f, 3f);
            var checkout = zone.AddComponent<Checkout>();
            Count("Caja registradora y zona de entrega");
            return checkout;
        }

        // ---------- Productos ----------

        static void CreateProductPrefabs()
        {
            foreach (ProductType type in ProductCatalog.AllTypes)
            {
                ProductModels.Model model = ProductModels.Build(type, GetMaterial, productPhysicsMaterial);
                GameObject template = model.Root;
                SetRefs(template.GetComponent<Product>(), ("body", template.GetComponent<Rigidbody>()));

                string path = $"{PrefabsFolder}/Producto_{type}.prefab";
                bool existed = File.Exists(path);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, path);
                Object.DestroyImmediate(template);
                ProductPrefabs[type] = prefab;
                ProductBottoms[type] = model.BottomOffset;
                Count(existed ? "Prefabs actualizados" : "Prefabs creados");
            }
        }

        static void PlaceProducts()
        {
            Transform root = new GameObject("Productos").transform;
            foreach (var aisle in Aisles)
            {
                float leftX = aisle.x - AisleHalfClearWidth - ProductInset;
                float rightX = aisle.x + AisleHalfClearWidth + ProductInset;
                foreach (float z in ProductZ)
                {
                    // El frente del modelo (+Z) mira hacia el centro del pasillo.
                    PlaceProduct(root, aisle.left, new Vector3(leftX, 0f, z), 90f);
                    PlaceProduct(root, aisle.right, new Vector3(rightX, 0f, z), -90f);
                }
            }
        }

        static void PlaceProduct(Transform parent, ProductType type, Vector3 position, float yaw)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(ProductPrefabs[type], parent.gameObject.scene);
            instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(
                new Vector3(position.x, ShelfBoardTop + ProductBottoms[type] + 0.02f, position.z),
                Quaternion.Euler(0f, yaw, 0f));
            Count("Productos colocados");
            Count("Productos: " + ProductCatalog.DisplayName(type));
        }

        // ---------- Carrito ----------

        struct CartParts
        {
            public CartController Controller;
            public Rigidbody Body;
            public CartContents Contents;
            public GrabberArm Arm;
            /// <summary>Mallas de los Empujadores: la primera persona (LAN) las deja solo con sombra.</summary>
            public Object[] PusherRenderers;
        }

        static CartParts BuildCart()
        {
            var root = new GameObject("Carrito");
            root.transform.position = new Vector3(StartX, 0.02f, StartZ);
            var body = root.AddComponent<Rigidbody>();
            body.mass = 30f;
            body.linearDamping = 0.3f;
            body.angularDamping = 2f;
            var controller = root.AddComponent<CartController>();
            Transform t = root.transform;

            // Collider principal del chasis (sin fricción para deslizar por fuerzas).
            GameObject chassis = Empty("Colision_Chasis", t, new Vector3(0f, 0.2f, 0f));
            var chassisCollider = chassis.AddComponent<BoxCollider>();
            chassisCollider.size = new Vector3(1f, 0.4f, 1.7f);
            chassisCollider.sharedMaterial = cartPhysicsMaterial;

            // Visuales del chasis.
            Prim(PrimitiveType.Cube, "Fondo_Canasta", t, new Vector3(0f, 0.37f, 0f), new Vector3(1f, 0.06f, 1.7f), "Metal", false);
            foreach (Vector3 p in new[] { new Vector3(-0.42f, 0.1f, 0.7f), new Vector3(0.42f, 0.1f, 0.7f), new Vector3(-0.42f, 0.1f, -0.7f), new Vector3(0.42f, 0.1f, -0.7f) })
            {
                GameObject wheel = Prim(PrimitiveType.Cylinder, "Rueda", t, p, new Vector3(0.2f, 0.04f, 0.2f), "Rueda", false);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Prim(PrimitiveType.Cube, "Pata", t, new Vector3(p.x, 0.24f, p.z), new Vector3(0.05f, 0.24f, 0.05f), "Metal", false);
            }

            // Paredes de la canasta (con colisión).
            Transform walls = Empty("Canasta", t, Vector3.zero).transform;
            CartWall(walls, "Pared_Frontal", new Vector3(0f, 0.7f, 0.82f), new Vector3(1f, 0.6f, 0.06f));
            CartWall(walls, "Pared_Trasera", new Vector3(0f, 0.7f, -0.82f), new Vector3(1f, 0.6f, 0.06f));
            CartWall(walls, "Pared_Izquierda", new Vector3(-0.47f, 0.7f, 0f), new Vector3(0.06f, 0.6f, 1.7f));
            CartWall(walls, "Pared_Derecha", new Vector3(0.47f, 0.7f, 0f), new Vector3(0.06f, 0.6f, 1.7f));
            CartWall(walls, "Separador_Asiento", new Vector3(0f, 0.7f, -0.35f), new Vector3(0.88f, 0.6f, 0.04f));

            // Asa y empujador.
            GameObject handle = Prim(PrimitiveType.Cylinder, "Asa", t, new Vector3(0f, 1.15f, -0.98f), new Vector3(0.06f, 0.55f, 0.06f), "Asa", false);
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cube, "Soporte_Asa_Izq", t, new Vector3(-0.45f, 1.08f, -0.9f), new Vector3(0.04f, 0.16f, 0.2f), "Metal", false);
            Prim(PrimitiveType.Cube, "Soporte_Asa_Der", t, new Vector3(0.45f, 1.08f, -0.9f), new Vector3(0.04f, 0.16f, 0.2f), "Metal", false);

            // Trigger de la canasta.
            GameObject basket = Empty("Canasta_Trigger", t, new Vector3(0f, 0.72f, 0.23f));
            var basketTrigger = basket.AddComponent<BoxCollider>();
            basketTrigger.isTrigger = true;
            basketTrigger.size = new Vector3(0.84f, 0.62f, 1.06f);
            var contents = basket.AddComponent<CartContents>();
            GameObject dropPoint = Empty("Punto_Soltar_Canasta", t, new Vector3(0f, 1.15f, 0.23f));

            // Brazo extensible.
            GameObject shoulder = Empty("Hombro", t, new Vector3(0.28f, 1.12f, -0.5f));
            GameObject arm = Prim(PrimitiveType.Cylinder, "Brazo", t, Vector3.zero, new Vector3(0.07f, 0.5f, 0.07f), "Brazo", false);
            GameObject hand = Empty("Mano", t, new Vector3(0f, 1.05f, 0.2f));
            GameObject handVisual = Prim(PrimitiveType.Sphere, "Mano_Visual", hand.transform, Vector3.zero, Vector3.one * 0.26f, "Mano", false);
            GameObject grabPoint = Empty("Punto_Agarre", hand.transform, new Vector3(0f, -0.12f, 0f));
            GameObject shadow = Prim(PrimitiveType.Cylinder, "Sombra_Mano", null, Vector3.zero, new Vector3(0.3f, 0.005f, 0.3f), "SombraMano", false);
            shadow.transform.SetParent(root.transform.parent, true);

            root.AddComponent<CartHealth>();
            Object[] pusherRenderers = BuildCrew(t, body, hand.transform);

            var grabber = root.AddComponent<GrabberArm>();
            SetRefs(grabber,
                ("cartRoot", t), ("cartBody", body), ("shoulder", shoulder.transform), ("hand", hand.transform),
                ("grabPoint", grabPoint.transform), ("armVisual", arm.transform),
                ("handRenderer", handVisual.GetComponent<Renderer>()), ("handShadow", shadow.transform),
                ("basketDropPoint", dropPoint.transform));

            Count("Carrito (CartController, GrabberArm, CartContents)");
            return new CartParts { Controller = controller, Body = body, Contents = contents, Arm = grabber, PusherRenderers = pusherRenderers };
        }

        // ---------- Personajes del carrito ----------

        const float PusherHeight = 1.25f;
        const float GrabberHeight = 1f;
        /// <summary>El frente del Empujador (pico, hocico) queda detrás de la pared trasera de la canasta.</summary>
        const float PusherFrontZ = -0.98f;
        /// <summary>La espalda y la cola del Agarrador quedan dentro de la pared trasera de la canasta.</summary>
        const float GrabberBackZ = -0.8f;
        /// <summary>Brazos del Empujador: abajo, algo abiertos y hacia el asa.</summary>
        static readonly Vector3 PusherArms = new Vector3(0.45f, -0.75f, 0.5f);
        /// <summary>Brazos del Agarrador: relajados a los lados (el derecho lo mueve ArmReach).</summary>
        static readonly Vector3 GrabberArms = new Vector3(0.35f, -0.9f, 0.15f);

        /// <summary>
        /// Coloca todos los personajes en los dos puestos del carrito. CartCrew activa en juego el
        /// elegido por cada jugador; la escena se guarda con los personajes por defecto. Devuelve
        /// las mallas de los Empujadores para ocultarlas en primera persona.
        /// </summary>
        static Object[] BuildCrew(Transform cart, Rigidbody body, Transform grabberHand)
        {
            var pushers = new Object[CharacterModels.All.Length];
            var pusherRenderers = new Object[CharacterModels.All.Length];
            var grabbers = new Object[CharacterModels.All.Length];
            foreach (CharacterModels.Definition definition in CharacterModels.All)
            {
                Material material = Materials[definition.Folder];
                int index = (int)definition.Id;

                // Empujador: de pie detrás del asa. Se bambolea y, si no trae pose propia, agita los brazos.
                CharacterModels.Character pusher = CharacterModels.Build(definition, "Empujador_" + definition.Folder, cart,
                    new Vector3(0f, 0f, -1.6f), PusherHeight, PusherArms, material);
                pusher.Root.transform.localPosition += Vector3.forward * (PusherFrontZ - pusher.LocalBounds.max.z);
                var waddle = pusher.Root.AddComponent<CharacterWaddle>();
                SetRefs(waddle, ("cartBody", body), ("model", pusher.Model));
                if (!definition.KeepPose)
                {
                    SetRefs(pusher.Root.AddComponent<FlipperFlap>(),
                        ("waddle", waddle), ("leftFlipper", pusher.LeftArm), ("rightFlipper", pusher.RightArm));
                }
                pusher.Root.SetActive(definition.Id == CharacterChoice.DefaultPusher);
                pushers[index] = pusher.Root;
                pusherRenderers[index] = pusher.Model.GetComponentInChildren<SkinnedMeshRenderer>(true);

                // Agarrador: de pie en el asiento (se ve de pecho para arriba); señala su mano extensible.
                CharacterModels.Character grabber = CharacterModels.Build(definition, "Agarrador_" + definition.Folder, cart,
                    new Vector3(-0.02f, 0.4f, -0.5f), GrabberHeight, GrabberArms, material);
                grabber.Root.transform.localPosition += Vector3.forward * (GrabberBackZ - grabber.LocalBounds.min.z);
                var motion = grabber.Root.AddComponent<CharacterWaddle>();
                motion.ConfigureMotion(3f, 0.02f, 6f);
                SetRefs(motion, ("cartBody", body), ("model", grabber.Model));
                SetRefs(grabber.Root.AddComponent<ArmReach>(),
                    ("upperArm", grabber.RightArm), ("hand", grabber.RightHand), ("target", grabberHand));
                grabber.Root.SetActive(definition.Id == CharacterChoice.DefaultGrabber);
                grabbers[index] = grabber.Root;
            }

            var crew = cart.gameObject.AddComponent<CartCrew>();
            SetArray(crew, "pushers", pushers);
            SetArray(crew, "grabbers", grabbers);
            Count("Personajes del carrito (CartCrew)");
            return pusherRenderers;
        }

        static void SetArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Compradores rivales ----------

        /// <summary>Los pies del rival quedan a esta distancia detrás del centro de su cuerpo; así las manos llegan al asa.</summary>
        const float RivalBodyZ = -0.08f;
        /// <summary>Asa del carrito rival a la altura de las manos (los hombros quedan a 0,8 m).</summary>
        const float RivalHandleY = 0.85f;

        /// <summary>
        /// Prefab del comprador rival de este supermercado: un personaje KayKit animado por
        /// supermercado (aventureros o esqueletos; RivalShopper activa uno según su número)
        /// empujando un carrito pequeño con paredes tintables.
        /// </summary>
        static RivalShopper CreateRivalPrefab()
        {
            var root = new GameObject("Comprador_Rival");
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.32f;
            Transform t = root.transform;

            Transform cartRoot = Empty("Carrito_Rival", t, new Vector3(0f, 0f, 0.65f)).transform;
            Prim(PrimitiveType.Cube, "Fondo", cartRoot, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.05f, 0.62f), "Metal", false);
            GameObject front = Prim(PrimitiveType.Cube, "Pared_Frontal", cartRoot, new Vector3(0f, 0.65f, 0.3f), new Vector3(0.6f, 0.4f, 0.03f), "Rival", false);
            GameObject left = Prim(PrimitiveType.Cube, "Pared_Izq", cartRoot, new Vector3(-0.29f, 0.65f, 0f), new Vector3(0.03f, 0.4f, 0.62f), "Rival", false);
            GameObject right = Prim(PrimitiveType.Cube, "Pared_Der", cartRoot, new Vector3(0.29f, 0.65f, 0f), new Vector3(0.03f, 0.4f, 0.62f), "Rival", false);
            GameObject handle = Prim(PrimitiveType.Cylinder, "Asa", cartRoot, new Vector3(0f, RivalHandleY, -0.36f), new Vector3(0.04f, 0.32f, 0.04f), "Asa", false);
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Transform leftGrip = Empty("Agarre_Izq", cartRoot, new Vector3(-0.17f, RivalHandleY, -0.36f)).transform;
            Transform rightGrip = Empty("Agarre_Der", cartRoot, new Vector3(0.17f, RivalHandleY, -0.36f)).transform;

            // Un cuerpo por personaje del supermercado, todos con el mismo Animator Controller.
            AnimatorController controller = NpcModels.GetController();
            Materials.TryGetValue("NPC_Ojos", out Material eyes);
            NpcModels.Definition[] characters = NpcModels.ForStore(current.Id);
            var bodies = new Object[characters.Length];
            for (int i = 0; i < characters.Length; i++)
            {
                NpcModels.Definition definition = characters[i];
                NpcModels.Character shopper = NpcModels.Build(definition, "Cuerpo_" + definition.Name, t,
                    new Vector3(0f, 0f, RivalBodyZ), Materials[definition.MaterialName], eyes, controller);
                SetRefs(shopper.Root.AddComponent<ShopperAnimator>(),
                    ("animator", shopper.Animator), ("mover", t),
                    ("leftUpperArm", shopper.LeftUpperArm), ("leftHand", shopper.LeftHand),
                    ("rightUpperArm", shopper.RightUpperArm), ("rightHand", shopper.RightHand),
                    ("leftGrip", leftGrip), ("rightGrip", rightGrip));
                shopper.Root.SetActive(i == 0);
                bodies[i] = shopper.Root;
            }

            foreach (Vector3 p in new[] { new Vector3(-0.25f, 0.08f, 0.25f), new Vector3(0.25f, 0.08f, 0.25f), new Vector3(-0.25f, 0.08f, -0.25f), new Vector3(0.25f, 0.08f, -0.25f) })
            {
                GameObject wheel = Prim(PrimitiveType.Cylinder, "Rueda", cartRoot, p, new Vector3(0.14f, 0.03f, 0.14f), "Rueda", false);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            Prim(PrimitiveType.Cube, "Pata", cartRoot, new Vector3(0f, 0.26f, 0f), new Vector3(0.04f, 0.36f, 0.04f), "Metal", false);
            GameObject basket = Empty("Punto_Canasta", cartRoot, new Vector3(0f, 0.6f, 0f));

            var rival = root.AddComponent<RivalShopper>();
            SetRefs(rival, ("basketPoint", basket.transform));
            SetArray(rival, "tintRenderers", new Object[] { front.GetComponent<Renderer>(), left.GetComponent<Renderer>(), right.GetComponent<Renderer>() });
            SetArray(rival, "bodies", bodies);

            // El clásico conserva el nombre de siempre para no romper las escenas ya generadas.
            string path = current.Id == StoreId.Halloween ? $"{PrefabsFolder}/Comprador_Rival_Halloween.prefab" : $"{PrefabsFolder}/Comprador_Rival.prefab";
            bool existed = File.Exists(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Count(existed ? "Prefabs actualizados" : "Prefabs creados");
            return prefab.GetComponent<RivalShopper>();
        }

        /// <summary>Supermercado de la escena y tipos de producto que vende.</summary>
        static void ConfigureStore(GameManager manager, ShoppingList list)
        {
            var managerObject = new SerializedObject(manager);
            managerObject.FindProperty("store").intValue = (int)current.Id;
            managerObject.ApplyModifiedPropertiesWithoutUndo();

            var types = new List<ProductType>();
            foreach (var aisle in Aisles)
            {
                types.Add(aisle.left);
                types.Add(aisle.right);
            }
            var listObject = new SerializedObject(list);
            SerializedProperty available = listObject.FindProperty("availableTypes");
            available.arraySize = types.Count;
            for (int i = 0; i < types.Count; i++)
                available.GetArrayElementAtIndex(i).intValue = (int)types[i];
            listObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Red de pasillos de los rivales: centros de pasillo y tres pasillos transversales.</summary>
        static void ConfigureRivals(RivalManager manager, RivalShopper prefab, ShoppingList list, CartContents contents)
        {
            SetRefs(manager, ("rivalPrefab", prefab), ("shoppingList", list), ("cartContents", contents));
            var serialized = new SerializedObject(manager);

            SerializedProperty aisles = serialized.FindProperty("aisleCentersX");
            aisles.arraySize = Aisles.Length;
            for (int i = 0; i < Aisles.Length; i++)
                aisles.GetArrayElementAtIndex(i).floatValue = Aisles[i].x;

            float[] corridors = current.CorridorsZ;
            SerializedProperty corridorProperty = serialized.FindProperty("corridorsZ");
            corridorProperty.arraySize = corridors.Length;
            for (int i = 0; i < corridors.Length; i++)
                corridorProperty.GetArrayElementAtIndex(i).floatValue = corridors[i];

            serialized.FindProperty("shelfZoneAbsZ").vector2Value = new Vector2(ShelfInnerZ, ShelfOuterZ);
            serialized.FindProperty("entryPoint").vector3Value = new Vector3(-FloorHalfWidth + 1f, 0f, corridors[0]);
            serialized.FindProperty("exitPoint").vector3Value = new Vector3(FloorHalfWidth - 1f, 0f, corridors[0]);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CartWall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject wall = Prim(PrimitiveType.Cube, name, parent, position, size, "Metal");
            wall.GetComponent<Collider>().sharedMaterial = cartPhysicsMaterial;
        }

        static CameraController BuildCamera(CartParts cart)
        {
            var go = new GameObject("Camara_Principal");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = current.CameraBack;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            go.AddComponent<AudioListener>();
            go.transform.position = cart.Controller.transform.position + new Vector3(0f, 4f, -6f);
            go.transform.LookAt(cart.Controller.transform.position + Vector3.up);
            Count("Cámara");
            return go.AddComponent<CameraController>();
        }

        // ======================================================================
        // Texto de los carteles (la interfaz de juego la construye HudBuilder)
        // ======================================================================

        static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor alignment, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            var label = go.AddComponent<Text>();
            label.font = uiFont;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.lineSpacing = 1.1f;
            label.raycastTarget = false;
            Count("Elementos de UI");
            return label;
        }

        static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding, 0f);
            rect.offsetMax = new Vector2(-padding, 0f);
        }

        // ======================================================================
        // Referencias, Build Settings y validación
        // ======================================================================

        static void SetRefs(Object target, params (string field, Object value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach (var (field, value) in references)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"[Carrito] {target.GetType().Name} no tiene el campo serializado '{field}'.");
                    continue;
                }
                property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            // Las escenas del juego van primero (clásica = 0); las demás del usuario se conservan.
            for (int i = Stores.Length - 1; i >= 0; i--)
            {
                string path = ScenePathFor(Stores[i].Id);
                if (!File.Exists(path)) continue;
                scenes.RemoveAll(s => s.path == path);
                scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------- Imágenes de las pantallas de carga ----------

        /// <summary>
        /// Renderiza vistas de la escena como imágenes de carga provisionales
        /// (Resources/Carga/&lt;tienda&gt;_captura_N.png). Las ilustraciones generadas con IA se
        /// guardan como &lt;tienda&gt;_ia_N.png, tienen prioridad y nunca se sobrescriben.
        /// </summary>
        static void CaptureLoadingImages()
        {
            EnsureFolder(RootFolder + "/Resources");
            EnsureFolder(LoadingFolder);
            string prefix = StoreCatalog.LoadingPrefix(current.Id);
            float aisle = Aisles[Mathf.Min(1, Aisles.Length - 1)].x;
            var views = new[]
            {
                (new Vector3(StartX, 4.2f, StartZ - 2.5f), new Vector3(StartX, 1.2f, StartZ + 10f)),
                (new Vector3(FloorHalfWidth * 0.55f, 15f, -FloorHalfLength * 0.6f), new Vector3(0f, 0f, 2f)),
                (new Vector3(aisle + 1.2f, 1.9f, -ShelfOuterZ + 1f), new Vector3(aisle - 0.3f, 1.1f, 0f)),
            };

            var go = new GameObject("Camara_Captura");
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = current.CameraBack;
            camera.fieldOfView = 55f;
            camera.farClipPlane = 200f;
            var target = new RenderTexture(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                for (int i = 0; i < views.Length; i++)
                {
                    go.transform.position = views[i].Item1;
                    go.transform.LookAt(views[i].Item2);
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    pixels.Apply();
                    RenderTexture.active = null;

                    string path = $"{LoadingFolder}/{prefix}_captura_{i + 1}.png";
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    ConfigureLoadingTexture(path);
                    Count("Imágenes de carga (capturas)");
                }
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        [MenuItem("Carrito/Configurar imágenes de carga", priority = 3)]
        public static void ConfigureAllLoadingTextures()
        {
            if (!AssetDatabase.IsValidFolder(LoadingFolder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { LoadingFolder }))
                ConfigureLoadingTexture(AssetDatabase.GUIDToAssetPath(guid));
        }

        static void ConfigureLoadingTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled && importer.maxTextureSize == 2048) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        /// <summary>Busca referencias vacías y comprueba la configuración básica de la escena.</summary>
        public static bool Validate(Scene scene, out string report)
        {
            var problems = new List<string>();
            var productCounts = new Dictionary<ProductType, int>();
            int checkedComponents = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null)
                    {
                        problems.Add($"Script perdido en '{root.name}'.");
                        continue;
                    }
                    if (behaviour.GetType().Namespace != typeof(GameManager).Namespace) continue;
                    checkedComponents++;

                    var serialized = new SerializedObject(behaviour);
                    SerializedProperty property = serialized.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null
                            && property.name != "m_Script")
                        {
                            problems.Add($"{behaviour.GetType().Name} en '{behaviour.name}': referencia vacía '{property.name}'.");
                        }
                    }

                    if (behaviour is Product product)
                    {
                        productCounts.TryGetValue(product.Type, out int count);
                        productCounts[product.Type] = count + 1;
                    }
                }
            }

            ExpectSingle<GameManager>(scene, problems);
            ExpectSingle<CartController>(scene, problems);
            ExpectSingle<GrabberArm>(scene, problems);
            ExpectSingle<CartContents>(scene, problems);
            ExpectSingle<Checkout>(scene, problems);
            ExpectSingle<GameUI>(scene, problems);
            ExpectSingle<MenuUI>(scene, problems);
            ExpectSingle<TutorialController>(scene, problems);
            ExpectSingle<RivalManager>(scene, problems);
            ExpectSingle<EventSystem>(scene, problems);

            foreach (CartContents contents in FindInScene<CartContents>(scene))
                if (!contents.GetComponent<Collider>().isTrigger) problems.Add("El collider de la canasta no es trigger.");
            foreach (Checkout checkout in FindInScene<Checkout>(scene))
                if (!checkout.GetComponent<Collider>().isTrigger) problems.Add("El collider de la zona de entrega no es trigger.");

            ShoppingList list = FindInScene<ShoppingList>(scene).Count > 0 ? FindInScene<ShoppingList>(scene)[0] : null;
            // La progresión puede pedir hasta 3 unidades de un mismo tipo.
            const int maxPerType = 3;
            IReadOnlyList<ProductType> sold = list != null ? list.AvailableTypes : ProductCatalog.AllTypes;
            foreach (ProductType type in sold)
            {
                productCounts.TryGetValue(type, out int count);
                if (count < maxPerType)
                    problems.Add($"Solo hay {count} unidades de {ProductCatalog.DisplayName(type)}; la lista puede pedir {maxPerType}.");
            }

            if (Camera.main == null && FindInScene<Camera>(scene).Count == 0) problems.Add("No hay cámara en la escena.");

            var sb = new StringBuilder();
            sb.AppendLine($"[Carrito] Validación de '{scene.path}': {checkedComponents} componentes revisados.");
            if (problems.Count == 0) sb.AppendLine("[Carrito] Validación correcta: sin referencias vacías ni problemas de configuración.");
            else foreach (string problem in problems) sb.AppendLine(" - " + problem);
            report = sb.ToString();
            return problems.Count == 0;
        }

        static List<T> FindInScene<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }

        static void ExpectSingle<T>(Scene scene, List<string> problems) where T : Component
        {
            int count = FindInScene<T>(scene).Count;
            if (count != 1) problems.Add($"Se esperaba 1 {typeof(T).Name} y hay {count}.");
        }

        static void Count(string key)
        {
            Stats.TryGetValue(key, out int value);
            Stats[key] = value + 1;
        }

        static string BuildSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Carrito] Escena generada: {CurrentScenePath}");
            sb.AppendLine($"[Carrito] Shader de materiales: {litShader.name}");
            var keys = new List<string>(Stats.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys) sb.AppendLine($"   {key}: {Stats[key]}");
            return sb.ToString();
        }
    }
}
