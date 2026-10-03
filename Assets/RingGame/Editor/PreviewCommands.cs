using System;
using RingGame.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RingGame.Editor
{
    [InitializeOnLoad]
    public static class PreviewCommands
    {
        const string PreviewFlag = "RingGame.StartPreview";

        static PreviewCommands()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PreviewFlag, false)) return;
                SessionState.SetBool(PreviewFlag, false);
                EditorApplication.delayCall += () =>
                {
                    var game = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
                    ShowGame();
                    if (game != null) game.StartPreview();
                };
            };
        }

        [MenuItem("Ring Game/Open Gameplay Preview")]
        public static void Open()
        {
            if (EditorApplication.isPlaying)
            {
                UnityEngine.Object.FindObjectOfType<GameBootstrap>()?.StartPreview();
                ShowGame(); return;
            }
            BuildCommands.EnsureProject();
            EditorSceneManager.OpenScene(BuildCommands.ScenePath);
            SessionState.SetBool(PreviewFlag, true);
            ShowGame();
            EditorApplication.isPlaying = true;
        }

        static void ShowGame()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type != null) { var view = EditorWindow.GetWindow(type); view.Show(); view.maximized = true; view.Focus(); }
        }
    }
}
