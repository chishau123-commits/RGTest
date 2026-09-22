using System;
using UnityEngine;

namespace GeometryRhythm
{
    public enum StoryRuntimeNodeKind
    {
        Dialogue,
        Choice,
        Background,
        Stage,
        End
    }

    [Serializable]
    public sealed class StoryRuntimeProjectData
    {
        public int version;
        public string storyId;
        public string title;
        public string assetRoot;
        public string sceneMusicResource;
        public StoryRuntimeCharacterData[] characters;
        public StoryRuntimeBackgroundData[] backgrounds;
        public StoryRuntimeNodeData[] nodes;
    }

    [Serializable]
    public sealed class StoryRuntimeCharacterData
    {
        public string id;
        public string displayName;
        public string colorHex;
        public StoryRuntimeExpressionData[] expressions;
    }

    [Serializable]
    public sealed class StoryRuntimeExpressionData
    {
        public string id;
        public string label;
        public string assetPath;
        public float anchorX;
        public float anchorY;
    }

    [Serializable]
    public sealed class StoryRuntimeBackgroundData
    {
        public string id;
        public string label;
        public string assetPath;
    }

    [Serializable]
    public sealed class StoryRuntimeChoiceData
    {
        public string id;
        public string text;
        public string variable;
        public string value;
    }

    [Serializable]
    public sealed class StoryRuntimeTextVariantData
    {
        public string variable;
        public string value;
        public string text;
    }

    [Serializable]
    public sealed class StoryRuntimeNodeData
    {
        public string id;
        public StoryRuntimeNodeKind kind;
        public string title;
        public string speakerId;
        public string expressionId;
        public string text;
        public StoryRuntimeTextVariantData[] textVariants;
        public StoryRuntimeChoiceData[] choices;
        public string backgroundId;
        public string transition;
        public string[] castIds;
        public string levelId;
        public string chartAssetPath;
        public bool autoplay;
    }

    public static class StoryRuntimeLoader
    {
        public static StoryRuntimeProjectData LoadMainStory()
        {
            var asset = Resources.Load<TextAsset>("Story/main-story");
            if (asset == null)
            {
                Debug.LogWarning("Story resource was not found: Resources/Story/main-story.json");
                return null;
            }
            var project = JsonUtility.FromJson<StoryRuntimeProjectData>(asset.text);
            if (project == null || project.nodes == null || project.nodes.Length == 0)
            {
                Debug.LogWarning("Story resource is empty or invalid: Resources/Story/main-story.json");
                return null;
            }
            return project;
        }

        public static Texture2D LoadTexture(string assetPath)
        {
            string resourcePath = ResourcePath(assetPath);
            if (string.IsNullOrEmpty(resourcePath)) return null;
            return Resources.Load<Texture2D>(resourcePath);
        }

        public static StoryRuntimeCharacterData FindCharacter(StoryRuntimeProjectData project, string id)
        {
            if (project == null || project.characters == null || string.IsNullOrEmpty(id)) return null;
            foreach (var character in project.characters)
                if (character != null && character.id == id) return character;
            return null;
        }

        public static StoryRuntimeExpressionData FindExpression(StoryRuntimeCharacterData character, string id)
        {
            if (character == null || character.expressions == null || string.IsNullOrEmpty(id)) return null;
            foreach (var expression in character.expressions)
                if (expression != null && expression.id == id) return expression;
            return null;
        }

        public static StoryRuntimeBackgroundData FindBackground(StoryRuntimeProjectData project, string id)
        {
            if (project == null || project.backgrounds == null || string.IsNullOrEmpty(id)) return null;
            foreach (var background in project.backgrounds)
                if (background != null && background.id == id) return background;
            return null;
        }

        public static string ResourcePath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return "";
            string normalized = assetPath.Replace('\\', '/');
            int marker = normalized.IndexOf("/Resources/", StringComparison.Ordinal);
            if (marker < 0) return "";
            string path = normalized.Substring(marker + "/Resources/".Length);
            int extension = path.LastIndexOf('.');
            return extension >= 0 ? path.Substring(0, extension) : path;
        }
    }
}
