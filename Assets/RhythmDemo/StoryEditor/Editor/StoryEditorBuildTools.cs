using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GeometryRhythm.StoryEditor
{
    public static class StoryEditorBuildTools
    {
        const string SampleProjectPath = "Assets/RhythmDemo/StoryEditor/Samples/story-example.json";
        const string MainStoryPath = "Assets/RhythmDemo/Resources/Story/main-story.json";

        [MenuItem("节奏黑客/剧情编辑器/生成初始剧情文件", false, 120)]
        public static void CreateStarterProjectFile()
        {
            var project = StoryProjectFactory.CreateStarter();
            string directory = Path.GetDirectoryName(SampleProjectPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(SampleProjectPath, JsonUtility.ToJson(project, true));
            AssetDatabase.ImportAsset(SampleProjectPath, ImportAssetOptions.ForceUpdate);
            var window = EditorWindow.GetWindow<StoryGraphEditorWindow>();
            window.LoadProjectForTools(Path.GetFullPath(SampleProjectPath));
            window.Show();
            Debug.Log("STORY_EDITOR_SAMPLE_CREATED " + SampleProjectPath);
        }

        [MenuItem("节奏黑客/剧情编辑器/打开剧情数据目录", false, 121)]
        public static void ShowStoryDataFolder()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "RhythmDemo", "Resources", "Story");
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        // Batch-mode entry point used to verify data, JSON round-tripping and validation.
        public static void BatchValidate()
        {
            try
            {
                var source = StoryProjectFactory.CreateStarter();
                string json = JsonUtility.ToJson(source, true);
                var loaded = JsonUtility.FromJson<StoryProjectData>(json);
                Require(loaded != null, "round-trip produced null");
                Require(loaded.version == 2, "starter format version");
                Require(loaded.nodes != null && loaded.nodes.Length == 6, "starter step count");
                Require(loaded.characters != null && loaded.characters.Length == 1, "starter character count");
                var diagnostics = StoryProjectValidator.Validate(loaded, true);
                int errors = diagnostics.Count(value => value.severity == StoryDiagnosticSeverity.Error);
                Require(errors == 0, "starter project has " + errors + " validation errors");
                int mainNodes = 0;
                if (File.Exists(MainStoryPath))
                {
                    var mainStory = JsonUtility.FromJson<StoryProjectData>(File.ReadAllText(MainStoryPath));
                    Require(mainStory != null && mainStory.version == 2, "main story format");
                    mainNodes = mainStory.nodes == null ? 0 : mainStory.nodes.Length;
                    var mainDiagnostics = StoryProjectValidator.Validate(mainStory, true);
                    int mainErrors = mainDiagnostics.Count(
                        value => value.severity == StoryDiagnosticSeverity.Error);
                    Require(mainErrors == 0, "main story has " + mainErrors + " validation errors");
                }
                var runtimeStory = StoryRuntimeLoader.LoadMainStory();
                Require(runtimeStory != null && runtimeStory.nodes != null,
                    "runtime story resource could not be loaded");
                Require(runtimeStory.nodes.Length == 17, "runtime story node count");
                var ami = StoryRuntimeLoader.FindCharacter(runtimeStory, "amiya");
                var closure = StoryRuntimeLoader.FindCharacter(runtimeStory, "closure");
                var amiExpression = StoryRuntimeLoader.FindExpression(ami, "default");
                var closureExpression = StoryRuntimeLoader.FindExpression(closure, "default");
                Require(StoryRuntimeLoader.LoadTexture(amiExpression == null ? "" : amiExpression.assetPath) != null,
                    "Amiya portrait resource");
                Require(StoryRuntimeLoader.LoadTexture(closureExpression == null ? "" : closureExpression.assetPath) != null,
                    "Closure portrait resource");
                var runtimeBackground = StoryRuntimeLoader.FindBackground(runtimeStory, "sealed-recording-room");
                Require(StoryRuntimeLoader.LoadTexture(runtimeBackground == null ? "" : runtimeBackground.assetPath) != null,
                    "story background resource");
                var uiRoot = new GameObject("Story UI validation");
                var frontend = uiRoot.AddComponent<RhythmFrontend>();
                var storyView = new RhythmFrontendView(uiRoot.transform, frontend, "RHYTHM HACKER");
                storyView.ShowStory(runtimeStory, runtimeStory.nodes[0], null,
                    "sealed-recording-room", new Dictionary<string, string>(),
                    new List<string> { "amiya", "closure" }, false);
                Require(storyView.ButtonCount >= 1, "story UI did not build controls");
                UnityEngine.Object.DestroyImmediate(uiRoot);
                Debug.Log("STORY_EDITOR_VALIDATION_PASS nodes=" + loaded.nodes.Length +
                    " mainNodes=" + mainNodes + " warnings=" + diagnostics.Count +
                    " jsonBytes=" + json.Length);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Story editor validation failed: " + message);
        }
    }
}
