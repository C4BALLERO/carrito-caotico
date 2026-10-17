using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Modelos de producto construidos con varias primitivas. Cada producto tiene una raíz
    /// con Rigidbody, Product y un único collider simple (estable en la física); las piezas
    /// visuales son hijas sin collider. El frente de cada modelo mira hacia +Z.
    /// </summary>
    internal static class ProductModels
    {
        internal delegate Material MaterialProvider(string name, Color color);

        /// <summary>Escala de presentación: productos algo más grandes que la realidad para leerse bien desde la cámara.</summary>
        public const float DisplayScale = 1.3f;

        internal struct Model
        {
            public GameObject Root;
            /// <summary>Distancia del origen a la base del producto, para apoyarlo en la balda.</summary>
            public float BottomOffset;
            /// <summary>Color principal: se usa en los carteles y en el stock decorativo.</summary>
            public Color MainColor;
        }

        // Colores principales por tipo.
        static readonly Color32 White = new Color32(248, 248, 245, 255);
        static readonly Color32 Blue = new Color32(37, 99, 235, 255);
        static readonly Color32 Red = new Color32(220, 38, 38, 255);
        static readonly Color32 Silver = new Color32(200, 205, 212, 255);
        static readonly Color32 Brown = new Color32(120, 72, 36, 255);
        static readonly Color32 LeafGreen = new Color32(74, 160, 60, 255);

        public static Color MainColor(ProductType type)
        {
            switch (type)
            {
                case ProductType.Leche: return White;
                case ProductType.Sandia: return new Color32(34, 120, 50, 255);
                case ProductType.Cereal: return new Color32(250, 204, 21, 255);
                case ProductType.Huevos: return new Color32(232, 214, 176, 255);
                case ProductType.PapelHigienico: return new Color32(244, 143, 177, 255);
                case ProductType.Lata: return Red;
                case ProductType.Pan: return new Color32(196, 134, 72, 255);
                case ProductType.Manzana: return new Color32(190, 24, 48, 255);
                case ProductType.Platano: return new Color32(250, 215, 60, 255);
                case ProductType.Queso: return new Color32(255, 190, 50, 255);
                case ProductType.Agua: return new Color32(125, 200, 240, 255);
                case ProductType.Detergente: return new Color32(130, 80, 200, 255);
                case ProductType.Calabaza: return new Color32(234, 88, 12, 255);
                case ProductType.Caramelos: return new Color32(217, 70, 239, 255);
                case ProductType.Vela: return new Color32(254, 243, 199, 255);
                case ProductType.Calavera: return new Color32(248, 250, 252, 255);
                case ProductType.Pocion: return new Color32(132, 204, 22, 255);
                case ProductType.SombreroBruja: return new Color32(49, 27, 84, 255);
                case ProductType.Murcielago: return new Color32(63, 56, 74, 255);
                case ProductType.ManzanaAcaramelada: return new Color32(185, 28, 28, 255);
                default: return Color.gray;
            }
        }

        public static Model Build(ProductType type, MaterialProvider materials, PhysicsMaterial physics)
        {
            var root = new GameObject("Producto_" + type);
            var builder = new PartBuilder(root.transform, type, materials);
            float bottom;
            Collider collider;
            float angularDamping = 0.5f;

            switch (type)
            {
                case ProductType.Leche:
                    // Cartón con techo a dos aguas, franja azul y tapón.
                    builder.Part(PrimitiveType.Cube, "Carton", Vector3.zero, new Vector3(0.2f, 0.32f, 0.2f), Vector3.zero, "Carton", White);
                    builder.Part(PrimitiveType.Cube, "Techo", new Vector3(0f, 0.16f, 0f), new Vector3(0.141f, 0.141f, 0.2f), new Vector3(0f, 0f, 45f), "Carton", White);
                    builder.Part(PrimitiveType.Cube, "Franja", new Vector3(0f, -0.06f, 0f), new Vector3(0.205f, 0.09f, 0.205f), Vector3.zero, "Franja", Blue);
                    builder.Part(PrimitiveType.Cube, "Etiqueta", new Vector3(0f, 0.06f, 0.101f), new Vector3(0.12f, 0.08f, 0.004f), Vector3.zero, "Etiqueta", new Color32(147, 197, 253, 255));
                    builder.Part(PrimitiveType.Cylinder, "Tapon", new Vector3(0f, 0.262f, 0.05f), new Vector3(0.05f, 0.012f, 0.05f), Vector3.zero, "Tapon", Red);
                    collider = Box(root, new Vector3(0f, 0.05f, 0f), new Vector3(0.2f, 0.42f, 0.2f));
                    bottom = 0.16f;
                    break;

                case ProductType.Sandia:
                    // Elipsoide con rayas claras y rabito.
                    builder.Part(PrimitiveType.Sphere, "Cascara", Vector3.zero, new Vector3(0.52f, 0.42f, 0.42f), Vector3.zero, "Cascara", MainColor(type));
                    for (int i = 0; i < 4; i++)
                        builder.Part(PrimitiveType.Sphere, "Raya", Vector3.zero, new Vector3(0.525f, 0.425f, 0.07f), new Vector3(i * 45f, 0f, 0f), "Raya", new Color32(132, 196, 96, 255));
                    builder.Part(PrimitiveType.Cylinder, "Rabito", new Vector3(0.265f, 0f, 0f), new Vector3(0.03f, 0.02f, 0.03f), new Vector3(0f, 0f, 90f), "Rabito", Brown);
                    var capsule = root.AddComponent<CapsuleCollider>();
                    capsule.direction = 0;
                    capsule.radius = 0.21f;
                    capsule.height = 0.52f;
                    collider = capsule;
                    bottom = 0.21f;
                    angularDamping = 2f;
                    break;

                case ProductType.Cereal:
                    // Caja con panel frontal y tazón dibujado.
                    builder.Part(PrimitiveType.Cube, "Caja", Vector3.zero, new Vector3(0.3f, 0.42f, 0.12f), Vector3.zero, "Caja", MainColor(type));
                    builder.Part(PrimitiveType.Cube, "Panel", new Vector3(0f, 0.09f, 0.061f), new Vector3(0.24f, 0.15f, 0.004f), Vector3.zero, "Panel", Red);
                    builder.Part(PrimitiveType.Cube, "Panel_Trasero", new Vector3(0f, 0.09f, -0.061f), new Vector3(0.24f, 0.15f, 0.004f), Vector3.zero, "Panel", Red);
                    builder.Part(PrimitiveType.Cylinder, "Tazon", new Vector3(0f, -0.08f, 0.062f), new Vector3(0.15f, 0.002f, 0.15f), new Vector3(90f, 0f, 0f), "Tazon", White);
                    builder.Part(PrimitiveType.Cylinder, "Copos", new Vector3(0f, -0.07f, 0.064f), new Vector3(0.1f, 0.002f, 0.07f), new Vector3(90f, 0f, 0f), "Copos", new Color32(234, 140, 40, 255));
                    collider = Box(root, Vector3.zero, new Vector3(0.3f, 0.42f, 0.12f));
                    bottom = 0.21f;
                    break;

                case ProductType.Huevos:
                    // Huevera abierta con seis huevos.
                    builder.Part(PrimitiveType.Cube, "Huevera", Vector3.zero, new Vector3(0.32f, 0.08f, 0.2f), Vector3.zero, "Huevera", new Color32(196, 178, 146, 255));
                    builder.Part(PrimitiveType.Cube, "Etiqueta", new Vector3(0f, 0f, 0.101f), new Vector3(0.2f, 0.04f, 0.004f), Vector3.zero, "Etiqueta", Blue);
                    for (int x = -1; x <= 1; x++)
                    for (int z = 0; z < 2; z++)
                        builder.Part(PrimitiveType.Sphere, "Huevo", new Vector3(x * 0.1f, 0.05f, z == 0 ? -0.05f : 0.05f), new Vector3(0.075f, 0.095f, 0.075f), Vector3.zero, "Huevo", new Color32(250, 240, 220, 255));
                    collider = Box(root, new Vector3(0f, 0.03f, 0f), new Vector3(0.32f, 0.14f, 0.2f));
                    bottom = 0.04f;
                    break;

                case ProductType.PapelHigienico:
                    // Paquete de cuatro rollos con envoltorio rosado.
                    for (int x = 0; x < 2; x++)
                    for (int z = 0; z < 2; z++)
                    {
                        var p = new Vector3(x == 0 ? -0.065f : 0.065f, 0f, z == 0 ? -0.065f : 0.065f);
                        builder.Part(PrimitiveType.Cylinder, "Rollo", p, new Vector3(0.125f, 0.13f, 0.125f), Vector3.zero, "Rollo", White);
                        builder.Part(PrimitiveType.Cylinder, "Tubo", p + new Vector3(0f, 0.131f, 0f), new Vector3(0.04f, 0.002f, 0.04f), Vector3.zero, "Tubo", new Color32(150, 120, 90, 255));
                    }
                    builder.Part(PrimitiveType.Cube, "Envoltorio", Vector3.zero, new Vector3(0.262f, 0.12f, 0.262f), Vector3.zero, "Envoltorio", MainColor(type));
                    collider = Box(root, Vector3.zero, new Vector3(0.26f, 0.26f, 0.26f));
                    bottom = 0.13f;
                    break;

                case ProductType.Lata:
                    // Lata con etiqueta y bordes metálicos.
                    builder.Part(PrimitiveType.Cylinder, "Cuerpo", Vector3.zero, new Vector3(0.14f, 0.09f, 0.14f), Vector3.zero, "Cuerpo", Red);
                    builder.Part(PrimitiveType.Cylinder, "Etiqueta", Vector3.zero, new Vector3(0.143f, 0.035f, 0.143f), Vector3.zero, "Etiqueta", White);
                    builder.Part(PrimitiveType.Cylinder, "Borde_Superior", new Vector3(0f, 0.09f, 0f), new Vector3(0.13f, 0.006f, 0.13f), Vector3.zero, "Metal", Silver);
                    builder.Part(PrimitiveType.Cylinder, "Borde_Inferior", new Vector3(0f, -0.09f, 0f), new Vector3(0.13f, 0.006f, 0.13f), Vector3.zero, "Metal", Silver);
                    collider = Box(root, Vector3.zero, new Vector3(0.14f, 0.18f, 0.14f));
                    bottom = 0.09f;
                    break;

                case ProductType.Pan:
                    // Barra de pan con corteza tostada y cortes.
                    builder.Part(PrimitiveType.Capsule, "Miga", Vector3.zero, new Vector3(0.17f, 0.19f, 0.15f), new Vector3(0f, 0f, 90f), "Miga", MainColor(type));
                    builder.Part(PrimitiveType.Capsule, "Corteza", new Vector3(0f, 0.025f, 0f), new Vector3(0.13f, 0.175f, 0.12f), new Vector3(0f, 0f, 90f), "Corteza", new Color32(150, 92, 44, 255));
                    for (int i = -1; i <= 1; i++)
                        builder.Part(PrimitiveType.Cube, "Corte", new Vector3(i * 0.1f, 0.078f, 0f), new Vector3(0.025f, 0.012f, 0.09f), new Vector3(0f, 30f, 0f), "Corte", new Color32(240, 205, 150, 255));
                    var loaf = root.AddComponent<CapsuleCollider>();
                    loaf.direction = 0;
                    loaf.radius = 0.085f;
                    loaf.height = 0.38f;
                    collider = loaf;
                    bottom = 0.085f;
                    break;

                case ProductType.Manzana:
                    // Manzana con rabito y hoja.
                    builder.Part(PrimitiveType.Sphere, "Fruta", Vector3.zero, new Vector3(0.2f, 0.18f, 0.2f), Vector3.zero, "Fruta", MainColor(type));
                    builder.Part(PrimitiveType.Cylinder, "Rabito", new Vector3(0f, 0.1f, 0f), new Vector3(0.015f, 0.03f, 0.015f), new Vector3(0f, 0f, 10f), "Rabito", Brown);
                    builder.Part(PrimitiveType.Cube, "Hoja", new Vector3(0.035f, 0.115f, 0f), new Vector3(0.06f, 0.005f, 0.03f), new Vector3(0f, 20f, 20f), "Hoja", LeafGreen);
                    var sphere = root.AddComponent<SphereCollider>();
                    sphere.radius = 0.095f;
                    collider = sphere;
                    bottom = 0.09f;
                    angularDamping = 3f;
                    break;

                case ProductType.Platano:
                    // Racimo de tres plátanos curvos unidos por el tallo.
                    for (int i = 0; i < 3; i++)
                    {
                        float z = (i - 1) * 0.055f;
                        float y = i == 1 ? 0.02f : 0f;
                        builder.Part(PrimitiveType.Capsule, "Platano_Izq", new Vector3(-0.07f, y + 0.012f, z), new Vector3(0.055f, 0.085f, 0.055f), new Vector3(0f, 0f, 75f), "Platano", MainColor(type));
                        builder.Part(PrimitiveType.Capsule, "Platano_Der", new Vector3(0.07f, y + 0.012f, z), new Vector3(0.055f, 0.085f, 0.055f), new Vector3(0f, 0f, 105f), "Platano", MainColor(type));
                        builder.Part(PrimitiveType.Sphere, "Punta", new Vector3(0.15f, y + 0.035f, z), Vector3.one * 0.025f, Vector3.zero, "Punta", new Color32(60, 40, 20, 255));
                    }
                    builder.Part(PrimitiveType.Cube, "Tallo", new Vector3(-0.155f, 0.04f, 0f), new Vector3(0.04f, 0.04f, 0.14f), Vector3.zero, "Tallo", new Color32(110, 120, 40, 255));
                    collider = Box(root, new Vector3(0f, 0.015f, 0f), new Vector3(0.34f, 0.1f, 0.18f));
                    bottom = 0.035f;
                    break;

                case ProductType.Queso:
                    // Rueda de queso con cera roja y etiqueta.
                    builder.Part(PrimitiveType.Cylinder, "Rueda", Vector3.zero, new Vector3(0.28f, 0.06f, 0.28f), Vector3.zero, "Rueda", MainColor(type));
                    builder.Part(PrimitiveType.Cylinder, "Cera", Vector3.zero, new Vector3(0.286f, 0.042f, 0.286f), Vector3.zero, "Cera", new Color32(185, 28, 28, 255));
                    builder.Part(PrimitiveType.Cylinder, "Etiqueta", new Vector3(0f, 0.061f, 0f), new Vector3(0.12f, 0.002f, 0.12f), Vector3.zero, "Etiqueta", White);
                    collider = Box(root, Vector3.zero, new Vector3(0.27f, 0.12f, 0.27f));
                    bottom = 0.06f;
                    break;

                case ProductType.Agua:
                    // Botella con hombro, cuello, tapón y etiqueta.
                    var water = MainColor(type);
                    builder.Part(PrimitiveType.Cylinder, "Botella", new Vector3(0f, -0.03f, 0f), new Vector3(0.12f, 0.12f, 0.12f), Vector3.zero, "Botella", water);
                    builder.Part(PrimitiveType.Sphere, "Hombro", new Vector3(0f, 0.09f, 0f), new Vector3(0.12f, 0.09f, 0.12f), Vector3.zero, "Botella", water);
                    builder.Part(PrimitiveType.Cylinder, "Cuello", new Vector3(0f, 0.15f, 0f), new Vector3(0.045f, 0.03f, 0.045f), Vector3.zero, "Botella", water);
                    builder.Part(PrimitiveType.Cylinder, "Tapon", new Vector3(0f, 0.185f, 0f), new Vector3(0.05f, 0.012f, 0.05f), Vector3.zero, "Tapon", new Color32(30, 64, 175, 255));
                    builder.Part(PrimitiveType.Cylinder, "Etiqueta", new Vector3(0f, -0.03f, 0f), new Vector3(0.123f, 0.04f, 0.123f), Vector3.zero, "Etiqueta", White);
                    collider = Box(root, new Vector3(0f, 0.025f, 0f), new Vector3(0.12f, 0.35f, 0.12f));
                    bottom = 0.15f;
                    break;

                case ProductType.Detergente:
                    // Garrafa con asa, tapón y etiqueta frontal.
                    var jug = MainColor(type);
                    builder.Part(PrimitiveType.Cube, "Garrafa", Vector3.zero, new Vector3(0.22f, 0.3f, 0.14f), Vector3.zero, "Garrafa", jug);
                    builder.Part(PrimitiveType.Cube, "Asa", new Vector3(-0.06f, 0.19f, 0f), new Vector3(0.07f, 0.08f, 0.04f), Vector3.zero, "Garrafa", jug);
                    builder.Part(PrimitiveType.Cylinder, "Tapon", new Vector3(0.06f, 0.17f, 0f), new Vector3(0.06f, 0.025f, 0.06f), Vector3.zero, "Tapon", new Color32(249, 115, 22, 255));
                    builder.Part(PrimitiveType.Cube, "Etiqueta", new Vector3(0f, -0.02f, 0.071f), new Vector3(0.16f, 0.15f, 0.004f), Vector3.zero, "Etiqueta", White);
                    builder.Part(PrimitiveType.Cube, "Logo", new Vector3(0f, -0.02f, 0.073f), new Vector3(0.08f, 0.06f, 0.004f), Vector3.zero, "Logo", new Color32(249, 115, 22, 255));
                    builder.Part(PrimitiveType.Cube, "Etiqueta_Trasera", new Vector3(0f, -0.02f, -0.071f), new Vector3(0.16f, 0.15f, 0.004f), Vector3.zero, "Etiqueta", White);
                    collider = Box(root, new Vector3(0f, 0.03f, 0f), new Vector3(0.22f, 0.36f, 0.14f));
                    bottom = 0.15f;
                    break;

                // ---------- Supermercado de Halloween ----------

                case ProductType.Calabaza:
                    // Calabaza con gajos y tallo.
                    builder.Part(PrimitiveType.Sphere, "Cuerpo", Vector3.zero, new Vector3(0.48f, 0.38f, 0.48f), Vector3.zero, "Cuerpo", MainColor(type));
                    for (int i = 0; i < 6; i++)
                        builder.Part(PrimitiveType.Sphere, "Gajo", Vector3.zero, new Vector3(0.16f, 0.39f, 0.49f), new Vector3(0f, i * 30f, 0f), "Gajo", new Color32(194, 65, 12, 255));
                    builder.Part(PrimitiveType.Cylinder, "Tallo", new Vector3(0.01f, 0.21f, 0f), new Vector3(0.06f, 0.05f, 0.06f), new Vector3(0f, 0f, 12f), "Tallo", new Color32(77, 124, 15, 255));
                    var pumpkin = root.AddComponent<SphereCollider>();
                    pumpkin.radius = 0.21f;
                    collider = pumpkin;
                    bottom = 0.19f;
                    angularDamping = 3f;
                    break;

                case ProductType.Caramelos:
                    // Bolsa de caramelos con ventana y caramelos de colores.
                    builder.Part(PrimitiveType.Cube, "Bolsa", Vector3.zero, new Vector3(0.24f, 0.28f, 0.1f), Vector3.zero, "Bolsa", MainColor(type));
                    builder.Part(PrimitiveType.Cube, "Cierre", new Vector3(0f, 0.15f, 0f), new Vector3(0.2f, 0.03f, 0.06f), Vector3.zero, "Cierre", new Color32(250, 204, 21, 255));
                    builder.Part(PrimitiveType.Cube, "Ventana", new Vector3(0f, -0.02f, 0.051f), new Vector3(0.17f, 0.15f, 0.004f), Vector3.zero, "Ventana", new Color32(253, 244, 255, 255));
                    Color32[] candy = { new Color32(239, 68, 68, 255), new Color32(34, 197, 94, 255), new Color32(234, 179, 8, 255) };
                    for (int i = 0; i < 3; i++)
                        builder.Part(PrimitiveType.Cylinder, "Caramelo", new Vector3((i - 1) * 0.05f, -0.03f + (i % 2) * 0.04f, 0.055f), new Vector3(0.04f, 0.003f, 0.04f), new Vector3(90f, 0f, 0f), "Caramelo" + i, candy[i]);
                    collider = Box(root, new Vector3(0f, 0.01f, 0f), new Vector3(0.24f, 0.31f, 0.1f));
                    bottom = 0.14f;
                    break;

                case ProductType.Vela:
                    // Vela con cera derretida, mecha y llama.
                    builder.Part(PrimitiveType.Cylinder, "Cera", Vector3.zero, new Vector3(0.09f, 0.12f, 0.09f), Vector3.zero, "Cera", MainColor(type));
                    builder.Part(PrimitiveType.Cylinder, "Goteo", new Vector3(0f, 0.1f, 0f), new Vector3(0.096f, 0.02f, 0.096f), Vector3.zero, "Goteo", new Color32(253, 230, 138, 255));
                    builder.Part(PrimitiveType.Cylinder, "Mecha", new Vector3(0f, 0.135f, 0f), new Vector3(0.008f, 0.015f, 0.008f), Vector3.zero, "Mecha", new Color32(30, 30, 30, 255));
                    builder.Part(PrimitiveType.Sphere, "Llama", new Vector3(0f, 0.17f, 0f), new Vector3(0.035f, 0.06f, 0.035f), Vector3.zero, "Llama", new Color32(251, 146, 60, 255));
                    collider = Box(root, new Vector3(0f, 0.02f, 0f), new Vector3(0.09f, 0.28f, 0.09f));
                    bottom = 0.12f;
                    break;

                case ProductType.Calavera:
                    // Calavera de azúcar con mandíbula, ojos y flor.
                    builder.Part(PrimitiveType.Sphere, "Craneo", new Vector3(0f, 0.02f, 0f), new Vector3(0.2f, 0.18f, 0.19f), Vector3.zero, "Craneo", MainColor(type));
                    builder.Part(PrimitiveType.Cube, "Mandibula", new Vector3(0f, -0.07f, 0.03f), new Vector3(0.12f, 0.06f, 0.11f), Vector3.zero, "Craneo", MainColor(type));
                    builder.Part(PrimitiveType.Sphere, "Ojo_Izq", new Vector3(-0.045f, 0.02f, 0.085f), Vector3.one * 0.055f, Vector3.zero, "Ojo", new Color32(30, 27, 75, 255));
                    builder.Part(PrimitiveType.Sphere, "Ojo_Der", new Vector3(0.045f, 0.02f, 0.085f), Vector3.one * 0.055f, Vector3.zero, "Ojo", new Color32(30, 27, 75, 255));
                    builder.Part(PrimitiveType.Sphere, "Flor", new Vector3(0f, 0.09f, 0.06f), Vector3.one * 0.04f, Vector3.zero, "Flor", new Color32(236, 72, 153, 255));
                    var skull = root.AddComponent<SphereCollider>();
                    skull.radius = 0.1f;
                    collider = skull;
                    bottom = 0.1f;
                    angularDamping = 3f;
                    break;

                case ProductType.Pocion:
                    // Matraz redondo con cuello, corcho y etiqueta.
                    builder.Part(PrimitiveType.Sphere, "Matraz", Vector3.zero, Vector3.one * 0.18f, Vector3.zero, "Matraz", MainColor(type));
                    builder.Part(PrimitiveType.Cylinder, "Cuello", new Vector3(0f, 0.12f, 0f), new Vector3(0.06f, 0.05f, 0.06f), Vector3.zero, "Matraz", MainColor(type));
                    builder.Part(PrimitiveType.Cylinder, "Corcho", new Vector3(0f, 0.18f, 0f), new Vector3(0.065f, 0.02f, 0.065f), Vector3.zero, "Corcho", new Color32(146, 100, 60, 255));
                    builder.Part(PrimitiveType.Cube, "Etiqueta", new Vector3(0f, 0f, 0.088f), new Vector3(0.08f, 0.06f, 0.004f), Vector3.zero, "Etiqueta", new Color32(254, 243, 199, 255));
                    collider = Box(root, new Vector3(0f, 0.04f, 0f), new Vector3(0.18f, 0.29f, 0.18f));
                    bottom = 0.09f;
                    break;

                case ProductType.SombreroBruja:
                    // Ala ancha y copa cónica hecha con cilindros, banda morada.
                    var hat = MainColor(type);
                    builder.Part(PrimitiveType.Cylinder, "Ala", Vector3.zero, new Vector3(0.34f, 0.008f, 0.34f), Vector3.zero, "Sombrero", hat);
                    float[] widths = { 0.18f, 0.14f, 0.1f, 0.065f, 0.035f };
                    for (int i = 0; i < widths.Length; i++)
                        builder.Part(PrimitiveType.Cylinder, "Copa", new Vector3(0.006f * i, 0.04f + i * 0.06f, 0f), new Vector3(widths[i], 0.032f, widths[i]), new Vector3(0f, 0f, -2f * i), "Sombrero", hat);
                    builder.Part(PrimitiveType.Cylinder, "Banda", new Vector3(0f, 0.035f, 0f), new Vector3(0.185f, 0.016f, 0.185f), Vector3.zero, "Banda", new Color32(147, 51, 234, 255));
                    collider = Box(root, new Vector3(0f, 0.15f, 0f), new Vector3(0.32f, 0.32f, 0.32f));
                    bottom = 0.01f;
                    break;

                case ProductType.Murcielago:
                    // Murciélago de peluche con alas, orejas y ojos.
                    var bat = MainColor(type);
                    builder.Part(PrimitiveType.Sphere, "Cuerpo", Vector3.zero, new Vector3(0.15f, 0.17f, 0.13f), Vector3.zero, "Peluche", bat);
                    builder.Part(PrimitiveType.Cube, "Ala_Izq", new Vector3(-0.15f, 0.02f, 0f), new Vector3(0.16f, 0.1f, 0.02f), new Vector3(0f, 0f, 20f), "Peluche", bat);
                    builder.Part(PrimitiveType.Cube, "Ala_Der", new Vector3(0.15f, 0.02f, 0f), new Vector3(0.16f, 0.1f, 0.02f), new Vector3(0f, 0f, -20f), "Peluche", bat);
                    builder.Part(PrimitiveType.Cube, "Oreja_Izq", new Vector3(-0.04f, 0.09f, 0f), new Vector3(0.03f, 0.05f, 0.02f), new Vector3(0f, 0f, 15f), "Peluche", bat);
                    builder.Part(PrimitiveType.Cube, "Oreja_Der", new Vector3(0.04f, 0.09f, 0f), new Vector3(0.03f, 0.05f, 0.02f), new Vector3(0f, 0f, -15f), "Peluche", bat);
                    builder.Part(PrimitiveType.Sphere, "Ojo_Izq", new Vector3(-0.03f, 0.03f, 0.058f), Vector3.one * 0.03f, Vector3.zero, "Ojos", new Color32(250, 204, 21, 255));
                    builder.Part(PrimitiveType.Sphere, "Ojo_Der", new Vector3(0.03f, 0.03f, 0.058f), Vector3.one * 0.03f, Vector3.zero, "Ojos", new Color32(250, 204, 21, 255));
                    collider = Box(root, new Vector3(0f, 0.01f, 0f), new Vector3(0.42f, 0.2f, 0.13f));
                    bottom = 0.085f;
                    break;

                case ProductType.ManzanaAcaramelada:
                    // Manzana roja brillante con palo y charco de caramelo en la base.
                    builder.Part(PrimitiveType.Sphere, "Manzana", Vector3.zero, new Vector3(0.18f, 0.17f, 0.18f), Vector3.zero, "Manzana", MainColor(type));
                    builder.Part(PrimitiveType.Cylinder, "Palo", new Vector3(0f, 0.14f, 0f), new Vector3(0.02f, 0.07f, 0.02f), Vector3.zero, "Palo", new Color32(214, 181, 140, 255));
                    builder.Part(PrimitiveType.Cylinder, "Caramelo", new Vector3(0f, -0.075f, 0f), new Vector3(0.2f, 0.012f, 0.2f), Vector3.zero, "Caramelo", new Color32(180, 83, 9, 255));
                    var apple = root.AddComponent<SphereCollider>();
                    apple.radius = 0.09f;
                    collider = apple;
                    bottom = 0.085f;
                    angularDamping = 3f;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Producto sin modelo.");
            }

            collider.sharedMaterial = physics;
            root.transform.localScale = Vector3.one * DisplayScale;

            var body = root.AddComponent<Rigidbody>();
            body.mass = ProductCatalog.DefaultWeight(type);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0.05f;
            body.angularDamping = angularDamping;

            var product = root.AddComponent<Product>();
            product.Configure(type, ProductCatalog.DisplayName(type), ProductCatalog.DefaultWeight(type));

            return new Model { Root = root, BottomOffset = bottom * DisplayScale, MainColor = MainColor(type) };
        }

        static BoxCollider Box(GameObject root, Vector3 center, Vector3 size)
        {
            var box = root.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            return box;
        }

        /// <summary>Crea piezas visuales sin collider bajo la raíz del producto.</summary>
        readonly struct PartBuilder
        {
            readonly Transform root;
            readonly ProductType type;
            readonly MaterialProvider materials;

            public PartBuilder(Transform root, ProductType type, MaterialProvider materials)
            {
                this.root = root;
                this.type = type;
                this.materials = materials;
            }

            public void Part(PrimitiveType shape, string name, Vector3 position, Vector3 scale, Vector3 euler,
                string materialName, Color color)
            {
                GameObject go = GameObject.CreatePrimitive(shape);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(root, false);
                go.transform.localPosition = position;
                go.transform.localRotation = Quaternion.Euler(euler);
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = materials($"Prod_{type}_{materialName}", color);
            }
        }
    }
}
