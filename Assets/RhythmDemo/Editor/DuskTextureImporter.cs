using UnityEditor;
using UnityEngine;

namespace GeometryRhythm.Editor
{
    public sealed class DuskTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/RhythmDemo/Resources/Dusk/",System.StringComparison.Ordinal))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;
            importer.maxTextureSize=4096;importer.isReadable=true;
        }
    }
}
