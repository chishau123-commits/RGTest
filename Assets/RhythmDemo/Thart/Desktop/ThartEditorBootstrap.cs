using UnityEngine;

namespace GeometryRhythm.Thart.Editor
{
    /// <summary>
    /// Thart 编辑器场景启动器 - 挂载在场景中启动编辑器
    /// </summary>
    public sealed class ThartEditorBootstrap : MonoBehaviour
    {
        [Header("Audio")]
        public AudioSource audioSource;

        void Awake()
        {
            // 确保有 AudioSource
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            // 添加编辑器控制器
            var controller = gameObject.AddComponent<ThartEditorController>();
            controller.audioSource = audioSource;
        }
    }
}
