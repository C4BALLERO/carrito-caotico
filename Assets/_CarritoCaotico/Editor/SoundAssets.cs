using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Biblioteca de sonidos generada: busca en 05_Audio (y sus subcarpetas) un clip con el nombre
    /// de cada campo de <see cref="SoundLibrary"/> y lo asigna. Basta con dejar los archivos con el
    /// nombre correcto y regenerar la escena.
    /// </summary>
    internal static class SoundAssets
    {
        public const string AudioFolder = CarritoSceneBuilder.RootFolder + "/05_Audio";
        public const string LibraryPath = AudioFolder + "/SoundLibrary.asset";

        /// <summary>Crea o actualiza la biblioteca e informa en la consola de los sonidos que faltan.</summary>
        public static SoundLibrary BuildLibrary()
        {
            if (!AssetDatabase.IsValidFolder(AudioFolder))
                AssetDatabase.CreateFolder(CarritoSceneBuilder.RootFolder, Path.GetFileName(AudioFolder));

            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var clips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ConfigureImport(path);
                clips[Path.GetFileNameWithoutExtension(path)] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }

            int found = 0;
            var missing = new List<string>();
            FieldInfo[] fields = typeof(SoundLibrary).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType != typeof(AudioClip)) continue;
                string fileName = SoundLibrary.FileNameFor(field.Name);
                clips.TryGetValue(fileName, out AudioClip clip);
                field.SetValue(library, clip);
                if (clip != null) found++;
                else missing.Add(fileName);
            }
            EditorUtility.SetDirty(library);

            Debug.Log($"[Carrito] Sonidos asignados: {found} de {found + missing.Count}." +
                (missing.Count > 0 ? $" Faltan en {AudioFolder}: {string.Join(", ", missing)}" : ""));
            return library;
        }

        /// <summary>Música y ambientes en streaming (son largos); efectos descomprimidos al cargar.</summary>
        static void ConfigureImport(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;
            string name = Path.GetFileNameWithoutExtension(path);
            bool longClip = name.StartsWith("mus_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("amb_", StringComparison.OrdinalIgnoreCase);

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            AudioClipLoadType loadType = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            if (settings.loadType == loadType && settings.compressionFormat == AudioCompressionFormat.Vorbis) return;
            settings.loadType = loadType;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = longClip ? 0.5f : 0.7f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
