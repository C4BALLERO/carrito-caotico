using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Crea el objeto "Audio" de la escena con <see cref="GameAudio"/>, le asigna la música del
    /// supermercado y los efectos de 05_Audio (ver 05_Audio/CREDITOS.md) y ajusta la importación:
    /// la música se lee en streaming y los efectos se descomprimen al cargar.
    /// </summary>
    internal static class AudioBuilder
    {
        public const string AudioFolder = CarritoSceneBuilder.RootFolder + "/05_Audio";

        /// <summary>Campo de GameAudio → archivo dentro de 05_Audio.</summary>
        static readonly (string field, string file)[] Effects =
        {
            ("hover", "Efectos/tap-a.ogg"),
            ("click", "Efectos/click-a.ogg"),
            ("back", "Efectos/back_002.ogg"),
            ("toggle", "Efectos/switch_002.ogg"),
            ("grab", "Efectos/pluck_001.ogg"),
            ("drop", "Efectos/drop_002.ogg"),
            ("throwClip", "Efectos/maximize_003.ogg"),
            ("intoBasket", "Efectos/confirmation_001.ogg"),
            ("rivalTook", "Efectos/error_004.ogg"),
            ("emptyCart", "Efectos/error_006.ogg"),
            ("tick", "Efectos/tick_001.ogg"),
            ("countdown", "Efectos/tick_004.ogg"),
            ("go", "Efectos/confirmation_004.ogg"),
            ("pause", "Efectos/open_002.ogg"),
            ("connected", "Efectos/maximize_006.ogg"),
            ("disconnected", "Efectos/minimize_006.ogg"),
            ("winJingle", "Jingles/jingles_NES12.ogg"),
            ("loseJingle", "Jingles/jingles_NES11.ogg")
        };

        public static string MusicFile(StoreId store) =>
            store == StoreId.Halloween ? "Musica/creepyloop-v2.ogg" : "Musica/headinthesand.ogg";

        /// <summary>Todos los archivos de audio que necesita una escena (para la validación).</summary>
        public static IEnumerable<string> RequiredFiles(StoreId store)
        {
            yield return MusicFile(store);
            foreach (var (_, file) in Effects) yield return file;
        }

        public static GameAudio Build(StoreId store, GameManager manager, GrabberArm grabber, ShoppingList list,
            GameTimer timer, RivalManager rivals, Checkout checkout, CartHealth health)
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

            Set("gameManager", manager);
            Set("grabber", grabber);
            Set("shoppingList", list);
            Set("timer", timer);
            Set("rivals", rivals);
            Set("checkout", checkout);
            Set("cartHealth", health);
            Set("music", LoadClip(MusicFile(store), true));
            foreach (var (field, file) in Effects) Set(field, LoadClip(file, false));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return audio;
        }

        static AudioClip LoadClip(string file, bool music)
        {
            string path = $"{AudioFolder}/{file}";
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                AudioClipLoadType loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                if (settings.loadType != loadType || importer.forceToMono != !music)
                {
                    settings.loadType = loadType;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = music ? 0.7f : 0.6f;
                    importer.defaultSampleSettings = settings;
                    importer.forceToMono = !music;
                    importer.SaveAndReimport();
                }
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogError($"[Carrito] Falta el audio {path}. Revisa 05_Audio/CREDITOS.md.");
            return clip;
        }
    }
}
