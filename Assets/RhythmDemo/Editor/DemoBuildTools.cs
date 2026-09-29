using System;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
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

        /// <summary>Android debug build for a USB-connected device: landscape only, ARM64 with
        /// IL2CPP, development player so logcat carries full stack traces and the profiler can
        /// attach. Pass -androidRelease for performance/normal play without development overhead.
        /// Install and launch through adb; see the Android section of the docs.</summary>
        public static void BuildAndroid()
        {
            try
            {
                if(!File.Exists(ScenePath)) CreateScene();
                else EditorSceneManager.OpenScene(ScenePath);
                DemoValidation.Run();
                AndroidGameplayValidation.Run();
                ChartImportValidation.Run();
                NoteSpawnValidation.Run();
                RecordedTouchValidation.Run();
                HudLayoutValidation.Run();
                AudioSyncValidation.Run();
                string directory=Argument("-androidBuildDirectory") ?? "Builds/Android";
                Directory.CreateDirectory(directory);
                PlayerSettings.companyName="Geometry Rhythm";PlayerSettings.productName="Geometry Rhythm Demo";
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,"com.geometryrhythm.demo");
                // Landscape on both sides; portrait stays off, matching the shipping mobile layout.
                PlayerSettings.defaultInterfaceOrientation=UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait=false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
                PlayerSettings.allowedAutorotateToLandscapeLeft=true;
                PlayerSettings.allowedAutorotateToLandscapeRight=true;
                PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel24;
                PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
                PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,ScriptingImplementation.IL2CPP);
                bool release=Array.IndexOf(Environment.GetCommandLineArgs(),"-androidRelease")>=0;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                    scenes=new[]{ScenePath},locationPathName=Path.Combine(directory,"GeometryRhythmDemo.apk"),
                    target=BuildTarget.Android,options=release?BuildOptions.None:BuildOptions.Development});
                if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Android build failed: "+report.summary.result);
                Debug.Log("GEOMETRY_ANDROID_BUILD_SUCCESS "+Path.GetFullPath(Path.Combine(directory,"GeometryRhythmDemo.apk"))+
                    " "+report.summary.totalSize+" bytes");
            }
            catch(Exception ex) {Debug.LogException(ex);EditorApplication.Exit(1);return;}
            EditorApplication.Exit(0);
        }
    }

    /// <summary>Declare the actual application category so Android's GameManager can
    /// recognise this player. Unity 2022's legacy isGame setting alone is insufficient.</summary>
    public sealed class AndroidGameManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath=Path.Combine(path,"src/main/AndroidManifest.xml");
            var manifest=new XmlDocument();manifest.Load(manifestPath);
            var application=manifest.SelectSingleNode("/manifest/application") as XmlElement;
            if(application==null) throw new InvalidOperationException("Android manifest is missing its application element");
            application.SetAttribute("appCategory","http://schemas.android.com/apk/res/android","game");
            manifest.Save(manifestPath);
        }
    }
}
