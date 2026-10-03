using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RingGame.Editor
{
    /// <summary>Reproducible development builds; never creates or modifies player content.</summary>
    public static class BuildCommands
    {
        public const string ScenePath = "Assets/RingGame/Scenes/Game.unity";
        public const string ApplicationId = "com.chishau.ringgame.prototype";

        [MenuItem("Ring Game/Prepare Project")]
        public static void EnsureProject()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "chishau";
            PlayerSettings.productName = "Ring Game Prototype";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = false;
            SetInputSystemOnly();
            ConfigureAndroidPlayer(35);

            // GameBootstrap creates all gameplay objects at runtime. Keep an existing scene intact.
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var previous = EditorSceneManager.GetSceneManagerSetup();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new BuildFailedException("Unable to save bootstrap scene: " + ScenePath);
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Ring Game/Build Android Debug APK")]
        public static void BuildAndroid()
        {
            EnsureProject();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Install Android Build Support, SDK/NDK tools and OpenJDK via Unity Hub.");

            var api = ReadArgument("-ringAndroidApi", "35");
            if (!int.TryParse(api, out var targetApi) || targetApi < 26)
                throw new BuildFailedException("-ringAndroidApi must be an installed API level >= 26.");
            ConfigureAndroidPlayer(targetApi);
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            Build(BuildTarget.Android, ReadArgument("-ringBuildPath", "Builds/Android/RingGame-debug.apk"));
        }

        private static void ConfigureAndroidPlayer(int targetApi)
        {
            // Prepare serializes these defaults so opening the project is Android-ready.
            // Unity 2022.3 supports Android ARM64 through IL2CPP, not Mono.
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, ApplicationId);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)targetApi;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.renderOutsideSafeArea = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        }

        [MenuItem("Ring Game/Build Windows Gameplay Smoke Player")]
        public static void BuildWindowsSmoke()
        {
            EnsureProject();
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            Build(BuildTarget.StandaloneWindows64,
                ReadArgument("-ringBuildPath", "Builds/WindowsSmoke/RingGame.exe"));
        }

        private static void Build(BuildTarget target, string output)
        {
            var fullPath = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = target,
                locationPathName = fullPath,
                options = BuildOptions.Development | BuildOptions.AllowDebugging |
                          BuildOptions.CompressWithLz4 | BuildOptions.DetailedBuildReport
            });

            var summary = new BuildSummaryRecord
            {
                unityVersion = Application.unityVersion,
                utc = DateTime.UtcNow.ToString("O"),
                target = target.ToString(),
                result = report.summary.result.ToString(),
                output = fullPath,
                totalErrors = report.summary.totalErrors,
                totalWarnings = report.summary.totalWarnings,
                totalBytes = report.summary.totalSize,
                durationSeconds = report.summary.totalTime.TotalSeconds,
                applicationId = target == BuildTarget.Android ? ApplicationId : "",
                development = true
            };
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(fullPath), "build-summary.json"),
                JsonUtility.ToJson(summary, true));
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Build failed; inspect the Editor log and build-summary.json.");
            Debug.Log("RING_BUILD_SUCCEEDED " + fullPath);
        }

        private static void SetInputSystemOnly()
        {
            var settings = Resources.FindObjectsOfTypeAll<PlayerSettings>();
            // PlayerSettings may not be enumerated before any Inspector has opened.
            // The fallback accessor exists in the pinned Unity 2022.3 reference source.
            var serialized = settings.Length > 0 ? new SerializedObject(settings[0]) :
                typeof(PlayerSettings).GetMethod("GetSerializedObject", BindingFlags.NonPublic | BindingFlags.Static)
                    ?.Invoke(null, null) as SerializedObject;
            if (serialized == null)
                throw new BuildFailedException("Cannot access PlayerSettings to configure the Input System.");
            var handler = serialized.FindProperty("activeInputHandler");
            if (handler == null)
                throw new BuildFailedException("Unity 2022.3 activeInputHandler property was not found.");
            handler.intValue = 1; // Input System Package (New).
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string ReadArgument(string name, string fallback)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] != name) continue;
                if (i + 1 == arguments.Length || arguments[i + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new BuildFailedException("Missing value for " + name);
                return arguments[i + 1];
            }
            return fallback;
        }

        [Serializable]
        private sealed class BuildSummaryRecord
        {
            public string unityVersion;
            public string utc;
            public string target;
            public string result;
            public string output;
            public int totalErrors;
            public int totalWarnings;
            public ulong totalBytes;
            public double durationSeconds;
            public string applicationId;
            public bool development;
        }
    }
}
