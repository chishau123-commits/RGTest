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
        static string BuildDirectory => System.Environment.GetEnvironmentVariable("CHART_STUDIO_BUILD_DIRECTORY")
            ?? "Builds/GeometryChartStudio";

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
            EnsureRuntimeGltfShaders();
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
                CopyBlenderBgaBridge();
                CopyVideoBgaTools();
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

        static void EnsureRuntimeGltfShaders()
        {
            const string packageFolder = "Packages/com.unity.cloud.gltfast/Runtime/Shader/Built-In";
            const string resourceFolder = "Assets/RhythmDemo/Resources/BlenderBgaShaders";
            string sourceRoot = Path.GetFullPath(packageFolder);
            string targetRoot = Path.GetFullPath(resourceFolder);
            if (!Directory.Exists(sourceRoot)) throw new BuildFailedException("glTFast built-in shaders are missing");
            foreach (string source in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(source).ToLowerInvariant();
                if (extension != ".shader" && extension != ".cginc") continue;
                string relative = source.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(targetRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void CopyBlenderBgaBridge()
        {
            string sourcePackage = Path.GetFullPath("BGA/exports/Firefly_the_Summer_Opening_v03");
            string packageTarget = Path.GetFullPath(Path.Combine(BuildDirectory, "BGA", "Current"));
            string toolsTarget = Path.GetFullPath(Path.Combine(BuildDirectory, "BGA", "Tools"));
            string sourceTarget = Path.GetFullPath(Path.Combine(BuildDirectory, "BGA", "Source"));
            Directory.CreateDirectory(packageTarget); Directory.CreateDirectory(toolsTarget); Directory.CreateDirectory(sourceTarget);
            foreach (string name in new[] { "manifest.json", "timeline.json", "scene.glb", "background.png" })
            {
                string source = Path.Combine(sourcePackage, name);
                if (!File.Exists(source)) throw new BuildFailedException("Missing Blender BGA package file: " + source);
                File.Copy(source, Path.Combine(packageTarget, name), true);
            }
            File.Copy(Path.GetFullPath("BGA/tools/export_unity_bga.py"), Path.Combine(toolsTarget, "export_unity_bga.py"), true);
            File.Copy(Path.GetFullPath("BGA/blender/Firefly_the_Summer_Opening_v03.blend"),
                Path.Combine(sourceTarget, "Firefly_the_Summer_Opening_v03.blend"), true);
        }

        static void CopyVideoBgaTools()
        {
            string target = Path.Combine(BuildDirectory, "BGA", "Tools");
            Directory.CreateDirectory(target);
            File.Copy("BGA/tools/bin/ffmpeg.exe", Path.Combine(target, "ffmpeg.exe"), true);
            if (File.Exists("BGA/tools/bin/FFMPEG-NOTICE.txt"))
                File.Copy("BGA/tools/bin/FFMPEG-NOTICE.txt", Path.Combine(target, "FFMPEG-NOTICE.txt"), true);
            // The opening chain is the default package the editor loads, so it ships
            // first; the full 45 s package is kept alongside it for reference.
            CopyVideoPackage("BGA/Video20", new[] { "Firefly20.chart.json" }, "Open-Firefly20.cmd");
            CopyVideoPackage("BGA/Video45", new[] { "Firefly45.chart.json" }, "Open-Firefly45.cmd");
            string videoSpaceStarter = "BGA/ChartPackages/Summer20_H3_VideoSpace_20s.grchart";
            string pathOnlyStarter = "BGA/ChartPackages/Summer20_H3_PathOnly_20s.grchart";
            CopyPortableChartPackage(File.Exists(pathOnlyStarter) ? pathOnlyStarter : File.Exists(videoSpaceStarter) ? videoSpaceStarter :
                "BGA/ChartPackages/Summer20_H3_20s.grchart", "Open-Summer20-H3.cmd");
        }

        static void CopyPortableChartPackage(string source, string launcher)
        {
            if (!File.Exists(source)) return;
            string target = Path.Combine(BuildDirectory, "Charts");
            Directory.CreateDirectory(target);
            string fileName = Path.GetFileName(source);
            File.Copy(source, Path.Combine(target, fileName), true);
            File.WriteAllText(Path.Combine(BuildDirectory, launcher),
                "@echo off\r\nstart \"\" \"%~dp0GeometryChartStudio.exe\" -chart \"%~dp0Charts\\" + fileName + "\"\r\n");
        }

        static void CopyVideoPackage(string source, string[] charts, string launcher)
        {
            if (!File.Exists(Path.Combine(source, "manifest.json"))) return;
            string packageTarget = Path.Combine(BuildDirectory, source.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(packageTarget);
            foreach (string name in new[] { "manifest.json", "video.mp4", "audio.wav" })
                File.Copy(Path.Combine(source, name), Path.Combine(packageTarget, name), true);
            foreach (string chart in charts)
                File.Copy(Path.Combine(source, chart), Path.Combine(packageTarget, chart), true);
            string launcherSource = Path.Combine("BGA/tools", launcher);
            if (File.Exists(launcherSource))
                File.Copy(launcherSource, Path.Combine(BuildDirectory, launcher), true);
        }

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
