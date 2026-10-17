using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
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
        public const string ScenesFolder = RootFolder + "/Scenes";
        public const string BackupFolder = ScenesFolder + "/Backups";
        public const string MaterialsFolder = RootFolder + "/Materials";
        public const string PrefabsFolder = RootFolder + "/Prefabs";
        public const string ScenePath = ScenesFolder + "/CarritoCaotico_Prueba.unity";

        // ---------- Distribución del nivel (metros) ----------
        const float FloorHalfWidth = 13f;
        const float FloorHalfLength = 21f;
        const float WallHeight = 3f;
        const float ShelfDepth = 1.2f;
        const float ShelfHalfLength = 9f;
        const float ShelfBoardTop = 0.9f;
        const float AisleSpacing = 5.7f;          // distancia entre centros de pasillo
        const float AisleHalfClearWidth = 2.25f;  // medio ancho libre del pasillo
        const float ProductInset = 0.3f;          // distancia del producto al borde del estante
        const float StartZ = -16f;
        const float CheckoutZ = 16f;
        static readonly float[] ProductZ = { -6f, -2f, 2f, 6f };

        static readonly Color32 Teal = new Color32(15, 118, 110, 255);
        static readonly Color32 Yellow = new Color32(255, 201, 60, 255);
        static readonly Color32 Coral = new Color32(233, 79, 55, 255);
        static readonly Color32 Ink = new Color32(31, 41, 51, 255);
        static readonly Color32 Paper = new Color32(255, 253, 247, 255);

        struct ProductSpec
        {
            public ProductType Type;
            public PrimitiveType Shape;
            public Vector3 Scale;
            public Color32 Color;
            public float HalfHeight;
        }

        static readonly ProductSpec[] ProductSpecs =
        {
            new ProductSpec { Type = ProductType.Leche, Shape = PrimitiveType.Cylinder, Scale = new Vector3(0.24f, 0.18f, 0.24f), Color = new Color32(250, 250, 250, 255), HalfHeight = 0.18f },
            new ProductSpec { Type = ProductType.Sandia, Shape = PrimitiveType.Sphere, Scale = new Vector3(0.48f, 0.48f, 0.48f), Color = new Color32(46, 160, 67, 255), HalfHeight = 0.24f },
            new ProductSpec { Type = ProductType.Cereal, Shape = PrimitiveType.Cube, Scale = new Vector3(0.3f, 0.42f, 0.14f), Color = new Color32(250, 204, 21, 255), HalfHeight = 0.21f },
            new ProductSpec { Type = ProductType.Huevos, Shape = PrimitiveType.Cube, Scale = new Vector3(0.28f, 0.12f, 0.2f), Color = new Color32(232, 214, 176, 255), HalfHeight = 0.06f },
            new ProductSpec { Type = ProductType.PapelHigienico, Shape = PrimitiveType.Cylinder, Scale = new Vector3(0.24f, 0.14f, 0.24f), Color = new Color32(244, 143, 177, 255), HalfHeight = 0.14f },
            new ProductSpec { Type = ProductType.Lata, Shape = PrimitiveType.Cylinder, Scale = new Vector3(0.15f, 0.09f, 0.15f), Color = new Color32(220, 38, 38, 255), HalfHeight = 0.09f },
        };

        /// <summary>Pasillos: centro X, tipo en la cara izquierda, tipo en la cara derecha y rótulo.</summary>
        static readonly (float x, ProductType left, ProductType right, string label)[] Aisles =
        {
            (-AisleSpacing, ProductType.Leche, ProductType.Huevos, "Pasillo 1 · Lácteos"),
            (0f, ProductType.Cereal, ProductType.Lata, "Pasillo 2 · Desayuno"),
            (AisleSpacing, ProductType.Sandia, ProductType.PapelHigienico, "Pasillo 3 · Fruta y hogar"),
        };

        // ---------- Estado de la construcción ----------
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        static readonly Dictionary<ProductType, GameObject> ProductPrefabs = new Dictionary<ProductType, GameObject>();
        static readonly Dictionary<string, int> Stats = new Dictionary<string, int>();
        static Shader litShader;
        static Font uiFont;
        static Sprite roundedSprite;
        static PhysicsMaterial cartPhysicsMaterial;
        static PhysicsMaterial productPhysicsMaterial;

        // ======================================================================
        // Menús
        // ======================================================================

        [MenuItem("Carrito/Construir escena de prueba", priority = 0)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ScenePath))
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Carrito Caótico",
                    $"Ya existe la escena:\n{ScenePath}\n\nSi la regeneras, se guardará antes una copia en {BackupFolder}.",
                    "Regenerar (con copia)", "Cancelar", "Abrir la existente");
                if (choice == 1) return;
                if (choice == 2)
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                    return;
                }
            }

            bool ok = Build();
            EditorUtility.DisplayDialog("Carrito Caótico",
                ok ? "Escena generada y validada.\nPulsa Play para jugar." : "Escena generada con avisos. Revisa la consola.",
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

        /// <summary>Construye y guarda la escena. Devuelve true si la validación final es correcta.</summary>
        public static bool Build()
        {
            Stats.Clear();
            Materials.Clear();
            ProductPrefabs.Clear();

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
            Checkout checkout = BuildCheckout(level);
            PlaceProducts();

            CartParts cart = BuildCart();
            CameraController cameraController = BuildCamera(cart);

            GameObject systems = new GameObject("Sistemas");
            var manager = systems.AddComponent<GameManager>();
            var timer = systems.AddComponent<GameTimer>();
            var shoppingList = systems.AddComponent<ShoppingList>();
            Count("Sistemas (GameManager, GameTimer, ShoppingList)");

            GameUI ui = BuildUI(out UiRefs uiRefs);

            // ---------- Conexión de referencias ----------
            SetRefs(cart.Contents, ("cart", cart.Controller));
            SetRefs(shoppingList, ("cartContents", cart.Contents));
            SetRefs(checkout, ("gameManager", manager));
            SetRefs(cameraController, ("target", cart.Controller.transform), ("targetBody", cart.Body));
            SetRefs(manager,
                ("timer", timer), ("shoppingList", shoppingList), ("cartContents", cart.Contents),
                ("cart", cart.Controller), ("grabber", cart.Arm), ("cameraController", cameraController));
            SetRefs(ui,
                ("gameManager", manager), ("timer", timer), ("shoppingList", shoppingList),
                ("cartContents", cart.Contents), ("grabber", cart.Arm), ("checkout", checkout),
                ("timerText", uiRefs.TimerText), ("timerBackground", uiRefs.TimerBackground),
                ("cargoText", uiRefs.CargoText), ("listText", uiRefs.ListText),
                ("listProgressText", uiRefs.ListProgressText), ("handText", uiRefs.HandText),
                ("messagePanel", uiRefs.MessagePanel), ("messageText", uiRefs.MessageText),
                ("receiptPanel", uiRefs.ReceiptPanel), ("receiptTitleText", uiRefs.ReceiptTitle),
                ("receiptBodyText", uiRefs.ReceiptBody), ("receiptResultText", uiRefs.ReceiptResult),
                ("restartButton", uiRefs.RestartButton));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"[Carrito] No se pudo guardar la escena en {ScenePath}");
                return false;
            }
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            bool valid = Validate(scene, out string report);
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
            if (!File.Exists(ScenePath)) return;
            EnsureFolder(BackupFolder);
            string backupPath = $"{BackupFolder}/CarritoCaotico_Prueba_{DateTime.Now:yyyyMMdd_HHmmss}.unity";
            if (AssetDatabase.CopyAsset(ScenePath, backupPath))
                Debug.Log($"[Carrito] Copia de seguridad de la escena anterior: {backupPath}");
            else
                throw new IOException($"No se pudo copiar {ScenePath} a {backupPath}. Se cancela para no perder cambios.");
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
            roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

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
            GetMaterial("Suelo", new Color32(176, 170, 160, 255));
            GetMaterial("Pared", new Color32(205, 232, 225, 255));
            GetMaterial("Estante", new Color32(96, 112, 130, 255));
            GetMaterial("EstanteFondo", new Color32(150, 170, 186, 255));
            GetMaterial("Metal", new Color32(150, 160, 170, 255));
            GetMaterial("Rueda", new Color32(40, 44, 52, 255));
            GetMaterial("Asa", Coral);
            GetMaterial("Empujador", new Color32(59, 130, 246, 255));
            GetMaterial("Agarrador", new Color32(168, 85, 247, 255));
            GetMaterial("Piel", new Color32(244, 200, 160, 255));
            GetMaterial("Brazo", new Color32(255, 140, 66, 255));
            GetMaterial("Mano", new Color32(255, 61, 127, 255));
            GetMaterial("SombraMano", new Color32(30, 30, 40, 255));
            GetMaterial("ZonaInicio", Yellow);
            GetMaterial("ZonaEntrega", new Color32(52, 211, 153, 255));
            GetMaterial("Caja", Teal);
            GetMaterial("CajaCinta", new Color32(30, 30, 35, 255));
            GetMaterial("Registradora", new Color32(55, 65, 81, 255));
            GetMaterial("Carton", new Color32(196, 145, 92, 255));
            GetMaterial("PisoMojado", Yellow);
            GetMaterial("Cartel", Paper);
            foreach (ProductSpec spec in ProductSpecs)
                GetMaterial("Producto_" + spec.Type, spec.Color);
        }

        static Material GetMaterial(string name, Color color)
        {
            if (Materials.TryGetValue(name, out Material cached)) return cached;

            string path = $"{MaterialsFolder}/M_{name}.mat";
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
            light.intensity = 0.9f;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
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

            Prim(PrimitiveType.Cube, "Zona_Inicio", level, new Vector3(0f, 0.005f, StartZ), new Vector3(3f, 0.01f, 3f), "ZonaInicio", false);
        }

        static void BuildShelves(Transform level)
        {
            Transform shelves = Empty("Estantes", level, Vector3.zero).transform;
            // Estantes entre y a los lados de los tres pasillos.
            float[] shelfCenters =
            {
                -AisleSpacing * 1.5f, -AisleSpacing * 0.5f, AisleSpacing * 0.5f, AisleSpacing * 1.5f
            };
            for (int i = 0; i < shelfCenters.Length; i++)
                BuildShelf(shelves, $"Estante_{i + 1}", shelfCenters[i]);

            Transform signs = Empty("Carteles", level, Vector3.zero).transform;
            foreach (var aisle in Aisles)
            {
                AddSign(signs, aisle.label, new Vector3(aisle.x, 2.5f, -ShelfHalfLength - 0.4f), 0f, 4.4f);
                AddSign(signs, aisle.label, new Vector3(aisle.x, 2.5f, ShelfHalfLength + 0.4f), 180f, 4.4f);
            }
        }

        static void BuildShelf(Transform parent, string name, float x)
        {
            Transform shelf = Empty(name, parent, new Vector3(x, 0f, 0f)).transform;
            float length = ShelfHalfLength * 2f;

            Prim(PrimitiveType.Cube, "Base", shelf, new Vector3(0f, 0.15f, 0f), new Vector3(ShelfDepth, 0.3f, length), "Estante");
            Prim(PrimitiveType.Cube, "Balda", shelf, new Vector3(0f, ShelfBoardTop - 0.05f, 0f), new Vector3(ShelfDepth, 0.1f, length), "Estante");
            Prim(PrimitiveType.Cube, "Balda_Superior", shelf, new Vector3(0f, 1.65f, 0f), new Vector3(ShelfDepth, 0.08f, length), "Estante");
            Prim(PrimitiveType.Cube, "Fondo", shelf, new Vector3(0f, 1.0f, 0f), new Vector3(0.08f, 1.9f, length), "EstanteFondo");

            // Bordes para que los productos no rueden fuera de la balda.
            float edge = ShelfDepth * 0.5f - 0.02f;
            Prim(PrimitiveType.Cube, "Borde_Izq", shelf, new Vector3(-edge, ShelfBoardTop + 0.04f, 0f), new Vector3(0.04f, 0.08f, length), "Metal");
            Prim(PrimitiveType.Cube, "Borde_Der", shelf, new Vector3(edge, ShelfBoardTop + 0.04f, 0f), new Vector3(0.04f, 0.08f, length), "Metal");

            foreach (float z in new[] { -ShelfHalfLength, 0f, ShelfHalfLength })
                Prim(PrimitiveType.Cube, "Poste", shelf, new Vector3(0f, 0.95f, z), new Vector3(ShelfDepth, 1.9f, 0.08f), "Estante");

            Count("Estantes");
        }

        static void BuildObstacles(Transform level)
        {
            Transform obstacles = Empty("Obstaculos", level, Vector3.zero).transform;

            // Pirámide de cajas de promoción: se puede derribar.
            Transform pyramid = Empty("Promocion_Cajas", obstacles, new Vector3(AisleSpacing, 0f, -12.5f)).transform;
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

            // Cartel de piso mojado.
            GameObject wetSign = Prim(PrimitiveType.Cube, "Piso_Mojado", obstacles, new Vector3(-AisleSpacing, 0.35f, 12f),
                new Vector3(0.5f, 0.7f, 0.3f), "PisoMojado");
            var wetBody = wetSign.AddComponent<Rigidbody>();
            wetBody.mass = 2f;
            Count("Obstáculos dinámicos");
        }

        static Checkout BuildCheckout(Transform level)
        {
            Transform area = Empty("Caja_Registradora", level, new Vector3(0f, 0f, CheckoutZ)).transform;

            Prim(PrimitiveType.Cube, "Mostrador", area, new Vector3(2.6f, 0.5f, 0f), new Vector3(1.2f, 1f, 3f), "Caja");
            Prim(PrimitiveType.Cube, "Cinta", area, new Vector3(2.6f, 1.02f, 0.2f), new Vector3(1f, 0.04f, 2.4f), "CajaCinta", false);
            Prim(PrimitiveType.Cube, "Registradora", area, new Vector3(2.6f, 1.25f, -1.1f), new Vector3(0.5f, 0.45f, 0.45f), "Registradora");
            Prim(PrimitiveType.Cube, "Pantalla", area, new Vector3(2.35f, 1.6f, -1.1f), new Vector3(0.05f, 0.3f, 0.4f), "ZonaEntrega", false);
            AddSign(area, "CAJA  ·  Entrega aquí", new Vector3(0f, 2.6f, 2.2f), 0f, 4f);

            Prim(PrimitiveType.Cube, "Marca_Entrega", area, new Vector3(0f, 0.005f, 0f), new Vector3(3.6f, 0.01f, 3f), "ZonaEntrega", false);

            GameObject zone = Empty("Zona_Entrega", area, new Vector3(0f, 1f, 0f));
            var trigger = zone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(3.6f, 2f, 3f);
            var checkout = zone.AddComponent<Checkout>();
            Count("Caja registradora y zona de entrega");
            return checkout;
        }

        // ---------- Productos ----------

        static void CreateProductPrefabs()
        {
            foreach (ProductSpec spec in ProductSpecs)
            {
                GameObject template = GameObject.CreatePrimitive(spec.Shape);
                template.name = "Producto_" + spec.Type;
                template.transform.localScale = spec.Scale;
                template.GetComponent<Renderer>().sharedMaterial = Materials["Producto_" + spec.Type];

                Collider collider = template.GetComponent<Collider>();
                if (spec.Shape == PrimitiveType.Cylinder)
                {
                    // El cilindro primitivo trae una cápsula: una caja es más estable en las baldas.
                    Object.DestroyImmediate(collider);
                    collider = template.AddComponent<BoxCollider>();
                }
                collider.sharedMaterial = productPhysicsMaterial;

                var body = template.AddComponent<Rigidbody>();
                body.mass = ProductCatalog.DefaultWeight(spec.Type);
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearDamping = 0.05f;
                body.angularDamping = spec.Shape == PrimitiveType.Sphere ? 2f : 0.5f;

                var product = template.AddComponent<Product>();
                product.Configure(spec.Type, ProductCatalog.DisplayName(spec.Type), ProductCatalog.DefaultWeight(spec.Type));
                SetRefs(product, ("body", body));

                string path = $"{PrefabsFolder}/Producto_{spec.Type}.prefab";
                bool existed = File.Exists(path);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, path);
                Object.DestroyImmediate(template);
                ProductPrefabs[spec.Type] = prefab;
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
                    PlaceProduct(root, aisle.left, new Vector3(leftX, 0f, z));
                    PlaceProduct(root, aisle.right, new Vector3(rightX, 0f, z));
                }
            }
        }

        static void PlaceProduct(Transform parent, ProductType type, Vector3 position)
        {
            ProductSpec spec = Array.Find(ProductSpecs, s => s.Type == type);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(ProductPrefabs[type], parent.gameObject.scene);
            instance.transform.SetParent(parent, true);
            instance.transform.position = new Vector3(position.x, ShelfBoardTop + spec.HalfHeight + 0.02f, position.z);
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
        }

        static CartParts BuildCart()
        {
            var root = new GameObject("Carrito");
            root.transform.position = new Vector3(0f, 0.02f, StartZ);
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
            Prim(PrimitiveType.Capsule, "Empujador_Cuerpo", t, new Vector3(-0.3f, 0.7f, -1.4f), new Vector3(0.42f, 0.7f, 0.42f), "Empujador", false);
            Prim(PrimitiveType.Sphere, "Empujador_Cabeza", t, new Vector3(-0.3f, 1.55f, -1.4f), Vector3.one * 0.3f, "Piel", false);

            // Agarrador sentado en el asiento del carrito.
            Prim(PrimitiveType.Capsule, "Agarrador_Cuerpo", t, new Vector3(0.18f, 0.9f, -0.6f), new Vector3(0.36f, 0.3f, 0.36f), "Agarrador", false);
            Prim(PrimitiveType.Sphere, "Agarrador_Cabeza", t, new Vector3(0.18f, 1.32f, -0.6f), Vector3.one * 0.28f, "Piel", false);

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

            var grabber = root.AddComponent<GrabberArm>();
            SetRefs(grabber,
                ("cartRoot", t), ("cartBody", body), ("shoulder", shoulder.transform), ("hand", hand.transform),
                ("grabPoint", grabPoint.transform), ("armVisual", arm.transform),
                ("handRenderer", handVisual.GetComponent<Renderer>()), ("handShadow", shadow.transform),
                ("basketDropPoint", dropPoint.transform));

            Count("Carrito (CartController, GrabberArm, CartContents)");
            return new CartParts { Controller = controller, Body = body, Contents = contents, Arm = grabber };
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
            camera.backgroundColor = new Color32(191, 227, 236, 255);
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
        // Interfaz
        // ======================================================================

        struct UiRefs
        {
            public Text TimerText;
            public Image TimerBackground;
            public Text CargoText;
            public Text ListText;
            public Text ListProgressText;
            public Text HandText;
            public GameObject MessagePanel;
            public Text MessageText;
            public GameObject ReceiptPanel;
            public Text ReceiptTitle;
            public Text ReceiptBody;
            public Text ReceiptResult;
            public Button RestartButton;
        }

        static GameUI BuildUI(out UiRefs refs)
        {
            refs = new UiRefs();

            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform c = canvasGo.transform;

            // Temporizador (arriba, centro).
            Image timerPanel = Panel(c, "Temporizador", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(260f, 100f), Teal);
            refs.TimerBackground = timerPanel;
            refs.TimerText = Label(timerPanel.transform, "Tiempo", "2:00", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(refs.TimerText.rectTransform, 0f);

            // Panel del Empujador (arriba, izquierda).
            Image pusher = Panel(c, "Panel_Empujador", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(560f, 230f), new Color(1f, 1f, 1f, 0.94f));
            Image pusherHeader = Panel(pusher.transform, "Cabecera", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(560f, 54f), Teal);
            Text pusherTitle = Label(pusherHeader.transform, "Titulo", "EMPUJADOR  ·  Jugador 1", 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            Stretch(pusherTitle.rectTransform, 18f);
            Text pusherControls = Label(pusher.transform, "Controles",
                "<b>W</b> acelerar   <b>S</b> frenar / atrás\n<b>A / D</b> girar   <b>Shift izq.</b> derrapar\n<b>Mouse</b> rotar cámara   <b>Esc</b> liberar cursor",
                22, Ink, TextAnchor.UpperLeft, FontStyle.Normal);
            Place(pusherControls.rectTransform, new Vector2(18f, -66f), new Vector2(524f, 96f));
            refs.CargoText = Label(pusher.transform, "Carga", "Carga: 0.0 kg", 24, Coral, TextAnchor.UpperLeft, FontStyle.Bold);
            Place(refs.CargoText.rectTransform, new Vector2(18f, -168f), new Vector2(524f, 48f));

            // Lista del Agarrador (derecha). Solo el Agarrador debe mirarla.
            Image grabberPanel = Panel(c, "Panel_Agarrador", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(520f, 640f), new Color(1f, 0.99f, 0.96f, 0.96f));
            Image listHeader = Panel(grabberPanel.transform, "Cabecera", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(520f, 70f), Yellow);
            Text listTitle = Label(listHeader.transform, "Titulo", "Lista del Agarrador", 34, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Stretch(listTitle.rectTransform, 20f);
            Text secret = Label(grabberPanel.transform, "Aviso", "Solo el Agarrador (Jugador 2) mira esta lista.", 19, new Color32(71, 85, 105, 255), TextAnchor.UpperLeft, FontStyle.Italic);
            Place(secret.rectTransform, new Vector2(20f, -80f), new Vector2(480f, 28f));
            refs.ListText = Label(grabberPanel.transform, "Lista", "", 26, Ink, TextAnchor.UpperLeft, FontStyle.Normal);
            Place(refs.ListText.rectTransform, new Vector2(20f, -116f), new Vector2(480f, 270f));
            refs.ListProgressText = Label(grabberPanel.transform, "Progreso", "", 24, Teal, TextAnchor.UpperLeft, FontStyle.Bold);
            Place(refs.ListProgressText.rectTransform, new Vector2(20f, -392f), new Vector2(480f, 40f));
            refs.HandText = Label(grabberPanel.transform, "Mano", "Mano: vacía", 22, Ink, TextAnchor.UpperLeft, FontStyle.Normal);
            Place(refs.HandText.rectTransform, new Vector2(20f, -438f), new Vector2(480f, 60f));
            Image grabberFooter = Panel(grabberPanel.transform, "Controles", new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(520f, 130f), new Color32(243, 232, 255, 255));
            grabberFooter.rectTransform.pivot = new Vector2(0f, 0f);
            Text grabberControls = Label(grabberFooter.transform, "Texto",
                "<b>AGARRADOR · Jugador 2</b>\n<b>Flechas</b> mover brazo\n<b>Enter</b> agarrar / soltar   <b>Ctrl der.</b> lanzar",
                22, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            Stretch(grabberControls.rectTransform, 20f);

            // Mensaje central.
            Image message = Panel(c, "Mensaje", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1000f, 90f), new Color(0.12f, 0.16f, 0.2f, 0.88f));
            message.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            refs.MessagePanel = message.gameObject;
            refs.MessageText = Label(message.transform, "Texto", "", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(refs.MessageText.rectTransform, 16f);
            message.gameObject.SetActive(false);

            // Recibo final.
            Image overlay = Panel(c, "Recibo", new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f));
            overlay.sprite = null;
            overlay.rectTransform.offsetMin = Vector2.zero;
            overlay.rectTransform.offsetMax = Vector2.zero;
            refs.ReceiptPanel = overlay.gameObject;
            Image paper = Panel(overlay.transform, "Papel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 900f), Paper);
            paper.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Image paperHeader = Panel(paper.transform, "Cabecera", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(760f, 90f), Teal);
            refs.ReceiptTitle = Label(paperHeader.transform, "Titulo", "Recibo", 42, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(refs.ReceiptTitle.rectTransform, 16f);
            refs.ReceiptBody = Label(paper.transform, "Detalle", "", 26, Ink, TextAnchor.UpperLeft, FontStyle.Normal);
            Place(refs.ReceiptBody.rectTransform, new Vector2(40f, -110f), new Vector2(680f, 560f));
            refs.ReceiptResult = Label(paper.transform, "Resultado", "", 36, Coral, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(refs.ReceiptResult.rectTransform, new Vector2(40f, -680f), new Vector2(680f, 60f));

            Image buttonImage = Panel(paper.transform, "Boton_Reiniciar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(360f, 80f), Coral);
            buttonImage.rectTransform.pivot = new Vector2(0.5f, 0f);
            var button = buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.9f, 0.85f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.65f);
            button.colors = colors;
            Text buttonText = Label(buttonImage.transform, "Texto", "Reiniciar  (R)", 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(buttonText.rectTransform, 0f);
            refs.RestartButton = button;
            overlay.gameObject.SetActive(false);

            // EventSystem con el módulo del New Input System.
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            Count("Interfaz (Canvas, EventSystem)");
            return canvasGo.AddComponent<GameUI>();
        }

        static Image Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = color;
            if (roundedSprite != null)
            {
                image.sprite = roundedSprite;
                image.type = Image.Type.Sliced;
            }
            Count("Elementos de UI");
            return image;
        }

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

        static void Place(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;
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
            int index = scenes.FindIndex(s => s.path == ScenePath);
            if (index == 0 && scenes[0].enabled) return;
            if (index >= 0) scenes.RemoveAt(index);
            // La escena del juego va primero; las demás escenas del usuario se conservan.
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
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
            ExpectSingle<EventSystem>(scene, problems);

            foreach (CartContents contents in FindInScene<CartContents>(scene))
                if (!contents.GetComponent<Collider>().isTrigger) problems.Add("El collider de la canasta no es trigger.");
            foreach (Checkout checkout in FindInScene<Checkout>(scene))
                if (!checkout.GetComponent<Collider>().isTrigger) problems.Add("El collider de la zona de entrega no es trigger.");

            ShoppingList list = FindInScene<ShoppingList>(scene).Count > 0 ? FindInScene<ShoppingList>(scene)[0] : null;
            int maxPerType = list != null ? list.MaxPerType : 2;
            foreach (ProductType type in ProductCatalog.AllTypes)
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
            sb.AppendLine($"[Carrito] Escena generada: {ScenePath}");
            sb.AppendLine($"[Carrito] Shader de materiales: {litShader.name}");
            var keys = new List<string>(Stats.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys) sb.AppendLine($"   {key}: {Stats[key]}");
            return sb.ToString();
        }
    }
}
