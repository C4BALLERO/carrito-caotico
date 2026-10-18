using UnityEditor;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Las imágenes de Resources/Portada (portada y carrito de la barra de carga) se importan
    /// siempre como sprites de interfaz, con transparencia y sin mipmaps.
    /// </summary>
    internal sealed class PortadaImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Portada/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
        }
    }
}
