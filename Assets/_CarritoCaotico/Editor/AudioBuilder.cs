using UnityEditor;
using UnityEngine;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Crea el objeto "Audio" de la escena con <see cref="GameAudio"/>: el paso del mouse por los
    /// botones, la explosión del carrito y la conexión LAN. Usa los sonidos CC0 de
    /// 05_Audio/Kenney (ver CREDITOS.md). La música y el resto de efectos son del AudioDirector.
    /// </summary>
    internal static class AudioBuilder
    {
        public const string KenneyFolder = CarritoSceneBuilder.RootFolder + "/05_Audio/Kenney";

        /// <summary>Campo de GameAudio → archivo dentro de 05_Audio/Kenney.</summary>
        public static readonly (string field, string file)[] Clips =
        {
            ("hover", "tap-a.ogg"),
            ("connected", "maximize_006.ogg"),
            ("disconnected", "minimize_006.ogg")
        };

        public static GameAudio Build(CartHealth health)
        {
            var go = new GameObject("Audio");
            var audio = go.AddComponent<GameAudio>();
            var serialized = new SerializedObject(audio);

            void Set(string field, Object value)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null) Debug.LogError($"[Carrito] GameAudio no tiene el campo '{field}'.");
                else property.objectReferenceValue = value;
            }

            Set("cartHealth", health);
            foreach (var (field, file) in Clips) Set(field, LoadClip(file));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return audio;
        }

        static AudioClip LoadClip(string file)
        {
            string path = $"{KenneyFolder}/{file}";
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.DecompressOnLoad || !importer.forceToMono)
                {
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = 0.7f;
                    importer.defaultSampleSettings = settings;
                    importer.forceToMono = true;
                    importer.SaveAndReimport();
                }
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogError($"[Carrito] Falta el audio {path}. Revisa 05_Audio/Kenney/CREDITOS.md.");
            return clip;
        }
    }
}
