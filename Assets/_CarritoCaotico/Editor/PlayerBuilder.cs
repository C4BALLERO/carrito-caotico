using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Compila el juego para Windows (para jugar en LAN, cada PC necesita una copia).
    /// Menú: Carrito > Compilar para Windows (LAN). Línea de comandos:
    /// Unity -batchmode -projectPath . -executeMethod CarritoCaotico.EditorTools.PlayerBuilder.BuildWindowsFromCommandLine -quit
    /// </summary>
    public static class PlayerBuilder
    {
        public const string OutputPath = "Builds/Windows/CarritoCaotico.exe";

        [MenuItem("Carrito/Compilar para Windows (LAN)", priority = 20)]
        public static void BuildWindowsFromMenu()
        {
            string path = BuildWindows();
            if (path != null) EditorUtility.RevealInFinder(path);
        }

        public static void BuildWindowsFromCommandLine()
        {
            EditorApplication.Exit(BuildWindows() != null ? 0 : 1);
        }

        /// <summary>Compila las escenas de los supermercados. Devuelve la ruta del .exe o null si falla.</summary>
        public static string BuildWindows()
        {
            var scenes = new List<string>();
            foreach (StoreId store in StoreCatalog.All)
            {
                string scene = CarritoSceneBuilder.ScenePathFor(store);
                if (File.Exists(scene)) scenes.Add(scene);
            }
            if (scenes.Count == 0)
            {
                Debug.LogError("[Carrito] No hay escenas: usa antes Carrito > Construir escena de prueba.");
                return null;
            }

            // En LAN el anfitrión debe seguir simulando aunque su ventana no tenga el foco.
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Carrito] La compilación falló: {report.summary.result} ({report.summary.totalErrors} errores).");
                return null;
            }
            Debug.Log($"[Carrito] Juego compilado en {Path.GetFullPath(OutputPath)} ({report.summary.totalSize / (1024 * 1024)} MB).");
            return OutputPath;
        }
    }
}
