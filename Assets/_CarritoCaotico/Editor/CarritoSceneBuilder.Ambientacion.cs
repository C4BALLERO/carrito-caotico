using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Ambientación del supermercado: suelo de baldosas, luminarias sobre las estanterías,
    /// carteles de cabecera, letreros colgantes de pared, etiquetas de precio, flechas hacia la
    /// caja y ventanas. De día es sobria; de noche (Halloween) los letreros son góticos y de neón.
    /// Nada tiene colisionador ni baja de 2 m sobre las zonas por las que pasa el carrito, y no hay
    /// techo: la cámara en tercera persona va por encima de las paredes.
    /// </summary>
    public static partial class CarritoSceneBuilder
    {
        public const string FontsFolder = RootFolder + "/04_Fonts";
        const string TexturesFolder = MaterialsFolder + "/Texturas";

        /// <summary>Tamaño de una baldosa del suelo (metros).</summary>
        const float TileSize = 0.6f;
        /// <summary>Emisión de los neones de noche (sin bloom, por encima de 1 satura a un color vivo).</summary>
        const float NeonEmission = 2.5f;

        static readonly string[] DayPromos =
        {
            "Ofertas de la semana", "2x1 en frutas", "Pan recién horneado", "Lácteos -20%",
            "Todo fresco", "Precios bajos", "Novedades", "Tu súper de confianza",
        };
        static readonly string[] NightPromos =
        {
            "Ofertas de ultratumba", "Calabazas 2x1", "Pociones frescas", "Dulce o truco",
            "Rebajas embrujadas", "Abierto hasta medianoche",
        };
        static readonly string[] DayWallSigns =
        {
            "Súper Caótico", "Frutas y verduras", "Panadería", "Bebidas frías", "Hogar y limpieza", "Atención al cliente",
        };
        static readonly string[] NightWallSigns =
        {
            "Súper Embrujado", "No alimentes a los fantasmas", "Calderos en oferta", "Murciélagos frescos",
            "Cuidado con la baba", "Velas para la luna llena",
        };

        static bool IsNight => current.Id == StoreId.Halloween;
        static Font dayFont;
        static Font nightFont;

        /// <summary>Nombre del material de ambientación de este supermercado (Dia_ o Noche_).</summary>
        static string Decor(string baseName) => (IsNight ? "Noche_" : "Dia_") + baseName;

        static Color Accent(int index)
        {
            Color[] palette = IsNight
                ? new Color[] { new Color32(251, 146, 60, 255), new Color32(192, 132, 252, 255), new Color32(163, 230, 53, 255) }
                : new Color[] { Teal, Coral, Yellow };
            return palette[((index % palette.Length) + palette.Length) % palette.Length];
        }

        static void BuildAmbience(Transform level)
        {
            dayFont = AssetDatabase.LoadAssetAtPath<Font>(FontsFolder + "/ArchivoBlack-Regular.ttf");
            nightFont = AssetDatabase.LoadAssetAtPath<Font>(FontsFolder + "/PirataOne-Regular.ttf");
            if (dayFont == null || nightFont == null) Debug.LogWarning($"[Carrito] Faltan fuentes en {FontsFolder}: se usa la fuente por defecto.");
            CreateAmbienceMaterials();

            Transform root = Empty("Ambientacion", level, Vector3.zero).transform;
            ApplyFloorTiles();
            BuildCeilingLights(root);
            BuildEndcapHeaders(root);
            BuildWallSigns(root);
            BuildPriceTags(root);
            BuildFloorArrows(root);
            BuildWindows(root);
        }

        // ---------- Materiales ----------

        static void CreateAmbienceMaterials()
        {
            DecorMaterial("Tabla", IsNight ? (Color)new Color32(24, 14, 32, 255) : (Color)Paper);
            for (int i = 0; i < 3; i++) DecorMaterial("Borde" + i, Accent(i), IsNight ? NeonEmission : 0f);
            DecorMaterial("Hierro", IsNight ? new Color32(30, 26, 34, 255) : new Color32(120, 128, 138, 255));
            DecorMaterial("Luminaria", IsNight ? new Color32(190, 255, 210, 255) : new Color32(255, 248, 230, 255), IsNight ? 0.9f : 1.3f);
            DecorMaterial("Etiqueta", IsNight ? new Color32(251, 146, 60, 255) : Yellow, IsNight ? 0.6f : 0f);
            DecorMaterial("Flecha", IsNight ? new Color32(163, 230, 53, 255) : Yellow, IsNight ? 1.2f : 0f);
            DecorMaterial("Marco", IsNight ? new Color32(22, 18, 28, 255) : new Color32(245, 245, 240, 255));
            DecorMaterial("Cristal", IsNight ? new Color32(20, 28, 70, 255) : new Color32(170, 215, 240, 255), IsNight ? 0.6f : 0.8f);
            if (IsNight) DecorMaterial("Luna", new Color32(255, 248, 210, 255), 2f);
        }

        static Material DecorMaterial(string baseName, Color color, float emission = 0f)
        {
            Material material = GetMaterial(Decor(baseName), color);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- Suelo ----------

        /// <summary>Baldosas: claras y discretas de día; tablero negro y morado de noche.</summary>
        static void ApplyFloorTiles()
        {
            Texture2D tiles = IsNight
                ? TileTexture("T_Baldosas_Noche", new Color32(28, 22, 34, 255), new Color32(70, 52, 88, 255), new Color32(14, 10, 18, 255))
                : TileTexture("T_Baldosas_Dia", new Color32(234, 228, 216, 255), new Color32(214, 208, 198, 255), new Color32(168, 162, 154, 255));
            Material floor = Materials["Suelo"];
            if (floor.HasProperty("_BaseColor")) floor.SetColor("_BaseColor", Color.white);
            if (floor.HasProperty("_Color")) floor.SetColor("_Color", Color.white);
            floor.mainTexture = tiles;
            floor.mainTextureScale = new Vector2(FloorHalfWidth * 2f / (TileSize * 2f), FloorHalfLength * 2f / (TileSize * 2f));
            EditorUtility.SetDirty(floor);
        }

        /// <summary>Textura de 2×2 baldosas con junta y un ruido suave (determinista).</summary>
        static Texture2D TileTexture(string name, Color a, Color b, Color grout)
        {
            EnsureFolder(TexturesFolder);
            const int size = 256;
            const int tile = size / 2;
            const int joint = 3;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var random = new System.Random(42);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isJoint = x % tile < joint || y % tile < joint;
                    Color color = isJoint ? grout : ((x / tile + y / tile) % 2 == 0 ? a : b);
                    float noise = (float)(random.NextDouble() - 0.5) * 0.03f;
                    texture.SetPixel(x, y, new Color(color.r + noise, color.g + noise, color.b + noise));
                }
            }
            string path = $"{TexturesFolder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.wrapMode != TextureWrapMode.Repeat || importer.maxTextureSize != 256 || importer.anisoLevel != 4)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = 256;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- Luminarias ----------

        /// <summary>
        /// Barras de luz sobre cada tramo de estantería (no sobre los pasillos, para no tapar el
        /// carrito). De noche algunas fallan.
        /// </summary>
        static void BuildCeilingLights(Transform root)
        {
            Transform lights = Empty("Luminarias", root, Vector3.zero).transform;
            var broken = new List<Renderer>();
            float length = ShelfOuterZ - ShelfInnerZ - 2f;
            for (int i = 0; i <= Aisles.Length; i++)
            {
                float x = (i - Aisles.Length * 0.5f) * AisleSpacing;
                for (int half = 0; half < 2; half++)
                {
                    float z = (half == 0 ? -1f : 1f) * (ShelfOuterZ + ShelfInnerZ) * 0.5f;
                    GameObject bar = Prim(PrimitiveType.Cube, "Luminaria", lights, new Vector3(x, 2.9f, z), new Vector3(0.18f, 0.06f, length), Decor("Luminaria"), false);
                    if (IsNight && (i + half) % 3 == 0) broken.Add(bar.GetComponent<Renderer>());
                }
            }
            if (broken.Count > 0)
            {
                Color emission = Materials[Decor("Luminaria")].GetColor("_EmissionColor");
                lights.gameObject.AddComponent<NeonFlicker>().Configure(broken.ToArray(), null, null, emission);
            }
            Count("Luminarias");
        }

        // ---------- Letreros ----------

        /// <summary>Carteles sobre los extremos de las estanterías que dan al pasillo central.</summary>
        static void BuildEndcapHeaders(Transform root)
        {
            Transform headers = Empty("Carteles_Cabecera", root, Vector3.zero).transform;
            string[] texts = IsNight ? NightPromos : DayPromos;
            int n = 0;
            for (int i = 0; i <= Aisles.Length; i++)
            {
                float x = (i - Aisles.Length * 0.5f) * AisleSpacing;
                foreach (float side in new[] { 1f, -1f })
                {
                    var center = new Vector3(x, 2.3f, side * (ShelfInnerZ + 0.1f));
                    // De dos caras: se lee desde el pasillo central y desde dentro del pasillo.
                    BuildSign(headers, $"Cabecera_{n}", texts[n % texts.Length], center, side > 0 ? 0f : 180f,
                        new Vector2(1.15f, 0.42f), n, doubleSided: true, withLight: n % 2 == 0, flicker: n % 4 == 1);
                    foreach (float post in new[] { -0.4f, 0.4f })
                        Prim(PrimitiveType.Cube, "Soporte", headers, new Vector3(x + post, 1.99f, center.z), new Vector3(0.03f, 0.18f, 0.03f), Decor("Hierro"), false);
                    n++;
                }
            }
        }

        /// <summary>
        /// Letreros colgantes de las paredes laterales, en cada pasillo transversal: brazo de hierro,
        /// cadenas y tabla de dos caras. Y un cartel grande sobre la caja.
        /// </summary>
        static void BuildWallSigns(Transform root)
        {
            Transform signs = Empty("Letreros_Pared", root, Vector3.zero).transform;
            string[] texts = IsNight ? NightWallSigns : DayWallSigns;
            int n = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                float wallX = side * FloorHalfWidth;
                foreach (float z in current.CorridorsZ)
                {
                    Prim(PrimitiveType.Cube, "Brazo", signs, new Vector3(wallX - side * 0.65f, 3.05f, z), new Vector3(1.3f, 0.05f, 0.05f), Decor("Hierro"), false);
                    float signX = wallX - side * 0.8f;
                    foreach (float chain in new[] { -0.35f, 0.35f })
                        Prim(PrimitiveType.Cylinder, "Cadena", signs, new Vector3(signX + chain, 2.89f, z), new Vector3(0.02f, 0.14f, 0.02f), Decor("Hierro"), false);
                    BuildSign(signs, $"Pared_{n}", texts[n % texts.Length], new Vector3(signX, 2.47f, z), 0f,
                        new Vector2(1.0f, 0.5f), n + 1, doubleSided: true, withLight: true, flicker: n % 3 == 0);
                    n++;
                }
            }

            BuildSign(signs, "Caja", IsNight ? "Sal... si puedes" : "¡Gracias por su compra!",
                new Vector3(0f, 2.75f, FloorHalfLength - 0.06f), 0f, new Vector2(4.6f, 0.7f), 0, doubleSided: false, withLight: true, flicker: IsNight);
        }

        /// <summary>
        /// Letrero: tabla con borde de color y texto. De día, tabla clara y texto oscuro; de noche,
        /// tabla negra, borde y texto de neón con un halo y una luz de color.
        /// </summary>
        static void BuildSign(Transform parent, string name, string text, Vector3 center, float yRotation, Vector2 size,
            int colorIndex, bool doubleSided, bool withLight, bool flicker)
        {
            Transform sign = Empty("Letrero_" + name, parent, center).transform;
            sign.localRotation = Quaternion.Euler(0f, yRotation, 0f);
            const float depth = 0.05f;
            const float border = 0.04f;
            Color accent = Accent(colorIndex);
            string borderMaterial = Decor("Borde" + (((colorIndex % 3) + 3) % 3));

            Prim(PrimitiveType.Cube, "Tabla", sign, Vector3.zero, new Vector3(size.x, size.y, depth), Decor("Tabla"), false);
            var frame = new List<Renderer>
            {
                Prim(PrimitiveType.Cube, "Borde", sign, new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x + border, border, depth + 0.02f), borderMaterial, false).GetComponent<Renderer>(),
                Prim(PrimitiveType.Cube, "Borde", sign, new Vector3(0f, -size.y * 0.5f, 0f), new Vector3(size.x + border, border, depth + 0.02f), borderMaterial, false).GetComponent<Renderer>(),
                Prim(PrimitiveType.Cube, "Borde", sign, new Vector3(-size.x * 0.5f, 0f, 0f), new Vector3(border, size.y, depth + 0.02f), borderMaterial, false).GetComponent<Renderer>(),
                Prim(PrimitiveType.Cube, "Borde", sign, new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(border, size.y, depth + 0.02f), borderMaterial, false).GetComponent<Renderer>(),
            };

            Font font = IsNight ? nightFont : dayFont;
            // Neón: núcleo casi blanco con halo del color del tubo; de día, texto oscuro.
            Color textColor = IsNight ? Color.Lerp(accent, Color.white, 0.6f) : (Color)Ink;
            Color? glow = IsNight ? new Color(accent.r, accent.g, accent.b, 0.9f) : (Color?)null;
            Vector2 textSize = size - new Vector2(0.12f, 0.08f);
            var texts = new List<Graphic>
            {
                SignText(sign, name, text, new Vector3(0f, 0f, -(depth * 0.5f + 0.012f)), 0f, textSize, font, textColor, glow),
            };
            if (doubleSided)
                texts.Add(SignText(sign, name + "_Detras", text, new Vector3(0f, 0f, depth * 0.5f + 0.012f), 180f, textSize, font, textColor, glow));

            var glowLights = new List<Light>();
            if (IsNight && withLight)
            {
                var lightGo = Empty("Luz", sign, new Vector3(0f, -size.y * 0.5f - 0.3f, doubleSided ? 0f : -0.4f));
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = accent;
                light.range = 3.5f;
                light.intensity = 1.4f;
                light.shadows = LightShadows.None;
                glowLights.Add(light);
            }
            if (IsNight && flicker)
                sign.gameObject.AddComponent<NeonFlicker>().Configure(frame.ToArray(), glowLights.ToArray(), texts.ToArray(), accent * NeonEmission);
            Count("Letreros");
        }

        /// <summary>Texto en un canvas de mundo (respeta la profundidad, a diferencia de TextMesh).</summary>
        static Text SignText(Transform parent, string name, string text, Vector3 localPosition, float yRotation,
            Vector2 size, Font font, Color color, Color? glow)
        {
            var canvasGo = new GameObject("Texto_" + name, typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = localPosition;
            canvasGo.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
            canvasGo.transform.localScale = Vector3.one * 0.01f;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasGo.transform).sizeDelta = size * 100f;

            Text label = Label(canvasGo.transform, "Texto", text, 40, color, TextAnchor.MiddleCenter, FontStyle.Normal);
            if (font != null) label.font = font;
            Stretch(label.rectTransform, 2f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 8;
            label.resizeTextMaxSize = 60;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.lineSpacing = 0.9f;
            if (glow.HasValue)
            {
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = glow.Value;
                outline.effectDistance = new Vector2(1.8f, -1.8f);
            }
            return label;
        }

        // ---------- Etiquetas, flechas y ventanas ----------

        /// <summary>Etiquetas de precio en el reborde de las baldas, en las caras con productos.</summary>
        static void BuildPriceTags(Transform root)
        {
            Transform tags = Empty("Etiquetas", root, Vector3.zero).transform;
            float faceX = ShelfDepth * 0.5f + 0.006f;
            for (int i = 0; i <= Aisles.Length; i++)
            {
                float x = (i - Aisles.Length * 0.5f) * AisleSpacing;
                foreach (var (zStart, zEnd) in new[] { (-ShelfOuterZ, -ShelfInnerZ), (ShelfInnerZ, ShelfOuterZ) })
                {
                    for (float z = zStart + 0.6f; z < zEnd - 0.4f; z += 1.2f)
                    {
                        if (i > 0)
                            Prim(PrimitiveType.Cube, "Etiqueta", tags, new Vector3(x - faceX, 0.93f, z), new Vector3(0.012f, 0.07f, 0.16f), Decor("Etiqueta"), false);
                        if (i < Aisles.Length)
                            Prim(PrimitiveType.Cube, "Etiqueta", tags, new Vector3(x + faceX, 0.93f, z), new Vector3(0.012f, 0.07f, 0.16f), Decor("Etiqueta"), false);
                    }
                }
            }
            foreach (Transform tag in tags)
                GameObjectUtility.SetStaticEditorFlags(tag.gameObject, StaticEditorFlags.BatchingStatic);
            Count("Etiquetas de precio");
        }

        /// <summary>Flechas en el suelo de los pasillos transversales, apuntando hacia la caja.</summary>
        static void BuildFloorArrows(Transform root)
        {
            Transform arrows = Empty("Flechas", root, Vector3.zero).transform;
            foreach (float z in new[] { current.CorridorsZ[0], current.CorridorsZ[1] })
            {
                foreach (var aisle in Aisles)
                {
                    // Chevrón: dos trazos que se unen en la punta (hacia +z, donde está la caja).
                    Transform chevron = Empty("Flecha", arrows, new Vector3(aisle.x, 0.006f, z + 0.3f)).transform;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        GameObject stroke = Prim(PrimitiveType.Cube, "Trazo", chevron, new Vector3(side * 0.25f, 0f, -0.25f), new Vector3(0.12f, 0.01f, 0.75f), Decor("Flecha"), false);
                        stroke.transform.localRotation = Quaternion.Euler(0f, -side * 45f, 0f);
                    }
                }
            }
            Count("Flechas");
        }

        /// <summary>Ventanas: cielo claro de día; noche azul con luna llena en una de ellas.</summary>
        static void BuildWindows(Transform root)
        {
            Transform windows = Empty("Ventanas", root, Vector3.zero).transform;
            float[] corridors = current.CorridorsZ;
            bool moon = IsNight;
            for (int c = 0; c < corridors.Length - 1; c++)
            {
                float z = (corridors[c] + corridors[c + 1]) * 0.5f;
                BuildWindow(windows, new Vector3(-FloorHalfWidth + 0.03f, 2.35f, z), -90f, moon);
                moon = false;
                BuildWindow(windows, new Vector3(FloorHalfWidth - 0.03f, 2.35f, z), 90f, false);
            }
            foreach (float x in new[] { -FloorHalfWidth * 0.5f, FloorHalfWidth * 0.5f })
                BuildWindow(windows, new Vector3(x, 2.35f, -FloorHalfLength + 0.03f), 180f, false);
        }

        /// <summary>Ventana pegada a la pared; su cara -z local mira hacia el interior.</summary>
        static void BuildWindow(Transform parent, Vector3 center, float yRotation, bool withMoon)
        {
            Transform window = Empty("Ventana", parent, center).transform;
            window.localRotation = Quaternion.Euler(0f, yRotation, 0f);
            var size = new Vector2(2.2f, 1.3f);
            const float bar = 0.08f;
            const float front = -0.03f;

            Prim(PrimitiveType.Cube, "Cristal", window, Vector3.zero, new Vector3(size.x, size.y, 0.02f), Decor("Cristal"), false);
            Prim(PrimitiveType.Cube, "Marco", window, new Vector3(0f, size.y * 0.5f, front), new Vector3(size.x + bar, bar, 0.05f), Decor("Marco"), false);
            Prim(PrimitiveType.Cube, "Marco", window, new Vector3(0f, -size.y * 0.5f, front), new Vector3(size.x + bar, bar, 0.05f), Decor("Marco"), false);
            Prim(PrimitiveType.Cube, "Marco", window, new Vector3(-size.x * 0.5f, 0f, front), new Vector3(bar, size.y, 0.05f), Decor("Marco"), false);
            Prim(PrimitiveType.Cube, "Marco", window, new Vector3(size.x * 0.5f, 0f, front), new Vector3(bar, size.y, 0.05f), Decor("Marco"), false);
            Prim(PrimitiveType.Cube, "Parteluz", window, new Vector3(0f, 0f, front), new Vector3(bar * 0.6f, size.y, 0.04f), Decor("Marco"), false);
            Prim(PrimitiveType.Cube, "Travesano", window, new Vector3(0f, 0f, front), new Vector3(size.x, bar * 0.6f, 0.04f), Decor("Marco"), false);

            if (withMoon)
            {
                GameObject moon = Prim(PrimitiveType.Cylinder, "Luna", window, new Vector3(0.5f, 0.28f, -0.015f), new Vector3(0.42f, 0.005f, 0.42f), Decor("Luna"), false);
                moon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            Count("Ventanas");
        }
    }
}
