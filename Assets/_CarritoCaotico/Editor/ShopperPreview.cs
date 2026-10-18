using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Foto de todos los compradores en fila (de frente y de perfil) para revisarlos de un vistazo.
    /// Menú: Carrito > Vista previa de compradores. Escribe Docs/compradores_*.png.
    /// </summary>
    public static class ShopperPreview
    {
        [MenuItem("Carrito/Vista previa de compradores", priority = 31)]
        public static void RenderFromMenu()
        {
            Render("Clasico", "Assets/_CarritoCaotico/02_Prefabs/Compradores", "_Halloween", false);
            Render("Halloween", "Assets/_CarritoCaotico/02_Prefabs/Compradores", "_Halloween", true);
        }

        public static void RenderFromCommandLine()
        {
            RenderFromMenu();
            EditorApplication.Exit(0);
        }

        static void Render(string label, string folder, string halloweenSuffix, bool halloween)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("Luz");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.6f);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(4f, 1f, 2f);
            floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.85f, 0.86f, 0.88f) };

            int index = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                bool isHalloween = path.Contains(halloweenSuffix);
                if (isHalloween != halloween) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var shopper = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                shopper.transform.position = new Vector3(-7f + index * 2f, 0f, 0f);
                // De frente a la cámara, alternando tres cuartos para ver la silueta.
                shopper.transform.rotation = Quaternion.Euler(0f, index % 2 == 0 ? 200f : 235f, 0f);
                index++;
            }

            var cameraGo = new GameObject("Camara");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.75f, 0.86f, 0.92f);
            camera.fieldOfView = 34f;
            cameraGo.transform.position = new Vector3(0f, 1.6f, -10.5f);
            cameraGo.transform.LookAt(new Vector3(0f, 0.95f, 0f));

            var target = new RenderTexture(2400, 900, 24) { antiAliasing = 8 };
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            Directory.CreateDirectory("Docs");
            File.WriteAllBytes($"Docs/compradores_{label.ToLowerInvariant()}.png", image.EncodeToPNG());
            Object.DestroyImmediate(image);
            target.Release();
            Debug.Log($"[Carrito] Vista previa: Docs/compradores_{label.ToLowerInvariant()}.png ({index} compradores)");
        }
    }
}
