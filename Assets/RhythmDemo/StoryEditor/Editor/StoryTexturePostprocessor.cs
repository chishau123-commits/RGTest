using UnityEditor;
using UnityEngine;

namespace GeometryRhythm.StoryEditor
{
    /// <summary>Keeps story art ready for dialogue/background presentation.</summary>
    public sealed class StoryTexturePostprocessor : AssetPostprocessor
    {
        const string StoryAssetRoot = "Assets/RhythmDemo/StoryData/Assets/";
        const string RuntimeStoryRoot = "Assets/RhythmDemo/Resources/Story/";
        const string TuriArtRoot = "Assets/RhythmDemo/Resources/Characters/Turi/";
        const string ImportVersionKey = "RhythmHacker.StoryTextureImport.V2";

        [InitializeOnLoadMethod]
        static void UpgradeExistingStoryTextures()
        {
            if (EditorPrefs.GetBool(ImportVersionKey, false)) return;
            string[] paths =
            {
                "Assets/RhythmDemo/StoryData/Assets",
                "Assets/RhythmDemo/Resources/Story"
            };
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", paths))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            EditorPrefs.SetBool(ImportVersionKey, true);
        }

        void OnPreprocessTexture()
        {
            bool turiArt = assetPath.StartsWith(TuriArtRoot, System.StringComparison.Ordinal);
            if (!assetPath.StartsWith(StoryAssetRoot, System.StringComparison.Ordinal) &&
                !assetPath.StartsWith(RuntimeStoryRoot, System.StringComparison.Ordinal) && !turiArt) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = turiArt ? FilterMode.Point : FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.isReadable = true;
        }
    }
}
