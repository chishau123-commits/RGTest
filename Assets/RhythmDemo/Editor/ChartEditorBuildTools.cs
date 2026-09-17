using System.IO;
using GeometryRhythm.ChartEditor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GeometryRhythm.Editor
{
    public static class ChartEditorBuildTools
    {
        const string ScenePath = "Assets/RhythmDemo/Scenes/GeometryChartStudio.unity";
        const string BuildDirectory = "Builds/GeometryChartStudio";

        [MenuItem("Geometry Rhythm/Chart Studio/Create or Open Desktop Editor")]
        public static void CreateOrOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Geometry Rhythm/Chart Studio/Build Windows Editor")]
        public static void BuildWindows()
        {
            CreateScene();
            Directory.CreateDirectory(BuildDirectory);
            var oldMode = PlayerSettings.fullScreenMode;
            int oldWidth = PlayerSettings.defaultScreenWidth, oldHeight = PlayerSettings.defaultScreenHeight;
            bool oldNative = PlayerSettings.defaultIsNativeResolution;
            bool oldResizable = PlayerSettings.resizableWindow;
            bool oldBackground = PlayerSettings.runInBackground;
            try
            {
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultIsNativeResolution = false;
                PlayerSettings.defaultScreenWidth = 1440;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.runInBackground = true;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = Path.Combine(BuildDirectory, "GeometryChartStudio.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new BuildFailedException("Chart Studio build failed: " + report.summary.result);
                Debug.Log("CHART_STUDIO_BUILD_PASS " + Path.GetFullPath(BuildDirectory));
            }
            finally
            {
                PlayerSettings.fullScreenMode = oldMode;
                PlayerSettings.defaultScreenWidth = oldWidth;
                PlayerSettings.defaultScreenHeight = oldHeight;
                PlayerSettings.defaultIsNativeResolution = oldNative;
                PlayerSettings.resizableWindow = oldResizable;
                PlayerSettings.runInBackground = oldBackground;
            }
        }

        // Batch-mode entry point used by CI/local verification.
        public static void CreateSceneAndBuild() => BuildWindows();

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "GeometryChartStudio";
            var root = new GameObject("Geometry Chart Studio", typeof(RuntimeChartEditorController));
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
