using UnityEngine;

namespace GeometryRhythm.Thart.TouchRecorder
{
    /// <summary>
    /// Thart 触控录制器场景启动器 - 挂载在平板场景中
    /// </summary>
    public sealed class ThartTouchRecorderBootstrap : MonoBehaviour
    {
        void Awake()
        {
            // 添加触控录制器
            gameObject.AddComponent<ThartTouchRecorder>();
        }
    }
}
