#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GeometryRhythm.Thart.Editor;
using GeometryRhythm.Thart.TouchRecorder;

namespace GeometryRhythm.Thart.EditorTools
{
    /// <summary>
    /// Thart 制谱器构建工具
    /// </summary>
    public static class ThartBuildTools
    {
        private const string EditorScenePath = "Assets/RhythmDemo/Thart/Scenes/ThartEditor.unity";
        private const string RecorderScenePath = "Assets/RhythmDemo/Thart/Scenes/ThartTouchRecorder.unity";
        private const string EditorBuildDir = "Builds/ThartEditor";
        private const string RecorderBuildDir = "Builds/ThartTouchRecorder";

        [MenuItem("Geometry Rhythm/Thart/创建编辑器场景")]
        public static void CreateEditorScene()
        {
            // 确保目录存在
            string dir = Path.GetDirectoryName(EditorScenePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // 创建新场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 创建相机
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.orthographic = false;
            cam.fieldOfView = 60;
            camGo.AddComponent<AudioListener>();

            // 创建启动器
            var bootstrapGo = new GameObject("ThartEditor");
            bootstrapGo.AddComponent<ThartEditorBootstrap>();

            // 保存场景
            EditorSceneManager.SaveScene(scene, EditorScenePath);
            Debug.Log("Thart 编辑器场景已创建: " + EditorScenePath);
        }

        [MenuItem("Geometry Rhythm/Thart/创建触控录制器场景")]
        public static void CreateRecorderScene()
        {
            string dir = Path.GetDirectoryName(RecorderScenePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 创建相机
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.orthographic = false;
            cam.fieldOfView = 60;
            camGo.AddComponent<AudioListener>();

            // 创建启动器
            var bootstrapGo = new GameObject("ThartTouchRecorder");
            bootstrapGo.AddComponent<ThartTouchRecorderBootstrap>();

            EditorSceneManager.SaveScene(scene, RecorderScenePath);
            Debug.Log("Thart 触控录制器场景已创建: " + RecorderScenePath);
        }

        [MenuItem("Geometry Rhythm/Thart/构建 Windows 编辑器")]
        public static void BuildWindowsEditor()
        {
            EnsureScene(EditorScenePath, CreateEditorScene);
            EnsureAlwaysIncludedShader("Thart/Unlit");

            string dir = Path.Combine(Directory.GetCurrentDirectory(), EditorBuildDir);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string exePath = Path.Combine(dir, "ThartEditor.exe");

            // 只在本方法内改产品名，构建完立即还原，避免污染其他构建入口。
            string oldCompany = PlayerSettings.companyName;
            string oldProduct = PlayerSettings.productName;
            FullScreenMode oldFullScreenMode = PlayerSettings.fullScreenMode;
            int oldW = PlayerSettings.defaultScreenWidth;
            int oldH = PlayerSettings.defaultScreenHeight;
            bool oldNative = PlayerSettings.defaultIsNativeResolution;
            bool oldResizable = PlayerSettings.resizableWindow;
            try
            {
                PlayerSettings.companyName = "Geometry Rhythm";
                PlayerSettings.productName = "Thart Chart Editor";

                // 电脑端不使用强制全屏：窗口化 + 可缩放
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultIsNativeResolution = false;
                PlayerSettings.defaultScreenWidth = 1600;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.allowFullscreenSwitch = true;
                PlayerSettings.runInBackground = true;

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { EditorScenePath },
                    locationPathName = exePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(options);
                Debug.Log("Thart 编辑器构建完成: " + exePath + " 结果: " + report.summary.result);
            }
            finally
            {
                PlayerSettings.companyName = oldCompany;
                PlayerSettings.productName = oldProduct;
                PlayerSettings.fullScreenMode = oldFullScreenMode;
                PlayerSettings.defaultScreenWidth = oldW;
                PlayerSettings.defaultScreenHeight = oldH;
                PlayerSettings.defaultIsNativeResolution = oldNative;
                PlayerSettings.resizableWindow = oldResizable;
            }
        }

        [MenuItem("Geometry Rhythm/Thart/构建 Android 触控录制器")]
        public static void BuildAndroidRecorder()
        {
            EnsureScene(RecorderScenePath, CreateRecorderScene);

            string dir = Path.Combine(Directory.GetCurrentDirectory(), RecorderBuildDir);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string apkPath = Path.Combine(dir, "ThartTouchRecorder.apk");

            // 触控录制器需要网络权限与横屏；包名与游戏区分，避免覆盖已装的 GeometryRhythmDemo。
            // 产品名只在本方法内生效，构建后还原。
            string oldCompany = PlayerSettings.companyName;
            string oldProduct = PlayerSettings.productName;
            try
            {
                PlayerSettings.companyName = "Geometry Rhythm";
                PlayerSettings.productName = "Thart Touch Recorder";
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.geometryrhythm.tharttouch");
                PlayerSettings.Android.forceInternetPermission = true;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { RecorderScenePath },
                    locationPathName = apkPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(options);
                Debug.Log("Thart 触控录制器构建完成: " + apkPath + " 结果: " + report.summary.result);
            }
            finally
            {
                PlayerSettings.companyName = oldCompany;
                PlayerSettings.productName = oldProduct;
            }
        }

        [MenuItem("Geometry Rhythm/Thart/构建 iOS 触控录制器")]
        public static void BuildiOSRecorder()
        {
            EnsureScene(RecorderScenePath, CreateRecorderScene);

            string dir = Path.Combine(Directory.GetCurrentDirectory(), RecorderBuildDir + "_iOS");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { RecorderScenePath },
                locationPathName = dir,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("Thart 触控录制器 iOS Xcode 工程已生成: " + dir + " 结果: " + report.summary.result);
        }

        [MenuItem("Geometry Rhythm/Thart/全部构建")]
        public static void BuildAll()
        {
            BuildWindowsEditor();
            BuildAndroidRecorder();
        }

        private static void EnsureScene(string path, Action createAction)
        {
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), path)))
            {
                createAction();
            }
        }

        /// <summary>
        /// 把指定 shader 加进 Always Included Shaders，避免构建后 Shader.Find 找不到（3D 预览需要）
        /// </summary>
        private static void EnsureAlwaysIncludedShader(string shaderName)
        {
            try
            {
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogWarning("[Thart] 找不到 shader: " + shaderName);
                    return;
                }

                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                if (assets == null || assets.Length == 0) return;

                var so = new SerializedObject(assets[0]);
                var arr = so.FindProperty("m_AlwaysIncludedShaders");
                if (arr == null) return;

                for (int i = 0; i < arr.arraySize; i++)
                {
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                        return; // 已经在列表里
                }

                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log("[Thart] 已加入 Always Included Shaders: " + shaderName);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Thart] 写入 Always Included Shaders 失败: " + e.Message);
            }
        }
    }
}
#endif
