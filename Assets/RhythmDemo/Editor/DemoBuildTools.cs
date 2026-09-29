using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GeometryRhythm.Editor
{
    /// <summary>Scene creation/build entry points. Batch validation uses a separate project copy
    /// so an already-open editor and any unsaved user scene remain untouched.</summary>
    public static class DemoBuildTools
    {
        public const string ScenePath="Assets/RhythmDemo/Scenes/GeometryRhythmDemo.unity";
        [MenuItem("Geometry Rhythm/Open Demo Scene")]
        public static void OpenDemo()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if(!File.Exists(ScenePath)) CreateScene();
            else EditorSceneManager.OpenScene(ScenePath);
        }
        static void CreateScene()
        {
            Directory.CreateDirectory("Assets/RhythmDemo/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Geometry Rhythm / JSON Demo");
            root.AddComponent<RhythmDemoController>();
            // Preserve the fog shader variants in the build; the world is generated at runtime.
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=new Color(.965f,.945f,.91f);RenderSettings.fogStartDistance=15;RenderSettings.fogEndDistance=125;
            EditorSceneManager.SaveScene(scene,ScenePath);
            // A referenced Standard material keeps the procedural world's shader in player builds.
            Directory.CreateDirectory("Assets/RhythmDemo/Resources/Materials");
            const string matPath="Assets/RhythmDemo/Resources/Materials/StandardReference.mat";
            if(!File.Exists(matPath)) AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")),matPath);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        [MenuItem("Geometry Rhythm/Validate Chart and Rules")]
        public static void Validate() => DemoValidation.Run();

        public static void ValidateAndBuild()
        {
            try
            {
                // Preserve scene-authored settings such as the temporary game title.
                if(!File.Exists(ScenePath)) CreateScene();
                else EditorSceneManager.OpenScene(ScenePath);
                DemoValidation.Run();
                string directory=Argument("-demoBuildDirectory") ?? "Builds/GeometryRhythm";
                Directory.CreateDirectory(directory);
                PlayerSettings.companyName="Geometry Rhythm";PlayerSettings.productName="Geometry Rhythm Demo";
                PlayerSettings.defaultScreenWidth=1920;PlayerSettings.defaultScreenHeight=1080;
                PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
                PlayerSettings.runInBackground=true;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                    scenes=new[]{ScenePath},locationPathName=Path.Combine(directory,"GeometryRhythmDemo.exe"),
                    // Use a non-development player for normal play and performance checks.
                    target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed: "+report.summary.result);
                Debug.Log("GEOMETRY_BUILD_SUCCESS "+report.summary.totalSize+" bytes");
            }
            catch(Exception ex) {Debug.LogException(ex);EditorApplication.Exit(1);return;}
            EditorApplication.Exit(0);
        }
        static string Argument(string name)
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}
    }
}
