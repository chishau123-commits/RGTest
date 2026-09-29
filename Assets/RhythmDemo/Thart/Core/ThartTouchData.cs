using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeometryRhythm.Thart
{
    /// <summary>
    /// 触控录制数据 - 平板端录制的原始触控信息。
    ///
    /// 录制始终发生在平板屏幕里的一个 16:9「录入框」内：百分比坐标是相对录入框的，
    /// 所以同一份手型在任何比例的平板上都会得到同一个 16:9 铺面形状。
    /// frameWidth / frameHeight 记录这个录入框的像素尺寸（老数据为 0，此时
    /// 百分比按整屏理解）。游戏铺面没有固定轨道，这里不做任何分轨处理。
    /// </summary>
    [Serializable]
    public sealed class ThartTouchRecording
    {
        // 3 = 触控百分比相对 16:9 录入框；2 = 相对整屏
        public int schemaVersion = 3;
        public string title = "";
        public int session = -1;
        public double durationSeconds;
        public int screenWidth;
        public int screenHeight;
        /// <summary>录入框像素宽度（0 = 老数据，按整屏归一化）</summary>
        public int frameWidth;
        /// <summary>录入框像素高度（0 = 老数据，按整屏归一化）</summary>
        public int frameHeight;
        public TouchSample[] samples;

        /// <summary>录入框像素尺寸；老数据回退到整屏尺寸</summary>
        public void FrameSize(out float width, out float height)
        {
            width = frameWidth > 0 ? frameWidth : (screenWidth > 0 ? screenWidth : 1920f);
            height = frameHeight > 0 ? frameHeight : (screenHeight > 0 ? screenHeight : 1080f);
        }

        /// <summary>百分比坐标是否已经相对 16:9 录入框</summary>
        public bool HasCaptureFrame { get { return frameWidth > 0 && frameHeight > 0; } }
    }

    /// <summary>
    /// 单个触控采样点
    /// </summary>
    [Serializable]
    public sealed class TouchSample
    {
        public double time; // 相对于录制开始的时间（秒）
        public TouchPoint[] touches; // 当前所有触控点
    }

    /// <summary>
    /// 单个触控点。x/y 为录入框内的像素（左上角为原点）；
    /// xPercent/yPercent 为录入框内归一化百分比（左上角为原点，0~100）。
    /// </summary>
    [Serializable]
    public sealed class TouchPoint
    {
        public int fingerId;
        public float x; // 录入框内坐标 X（像素）
        public float y; // 录入框内坐标 Y（像素，从顶部往下）
        public float xPercent; // 录入框归一化 X：0 = 最左，100 = 最右
        public float yPercent; // 录入框归一化 Y：0 = 最上，100 = 最下
        public float pressure; // 压力值 0-1
        public TouchPhase phase;
    }

    /// <summary>
    /// 触控相位
    /// </summary>
    public enum TouchPhase
    {
        Began,
        Moved,
        Stationary,
        Ended,
        Canceled
    }

    /// <summary>
    /// 识别出的音符事件（从触控数据转换而来）
    /// 位置保持全屏百分比，不再被压成固定轨道的索引。
    /// </summary>
    [Serializable]
    public sealed class ThartNoteEvent
    {
        public double time; // 时间（秒）
        public float xPercent; // X 位置百分比 0-100（全屏）
        public float yPercent; // Y 位置百分比 0-100（全屏）
        public string noteType; // "tap" 或 "drag"
        public int pathIndex; // 由位置聚类得到的列索引（转换阶段填写）
        public double duration; // 长按/拖动持续时间
        public float pressure; // 平均压力
    }

    /// <summary>
    /// 触控转音符的配置参数。默认按触控位置自动分列，不使用固定轨道。
    /// 坐标域固定为 16:9：录入框、游玩界面、铺面形状三者一致，换屏幕比例也不会变形。
    /// </summary>
    [Serializable]
    public sealed class TouchToNoteConfig
    {
        /// <summary>录入框与游玩界面的固定宽高比（16:9）</summary>
        public const float FieldAspect = 16f / 9f;

        /// <summary>固定列数；&lt;= 0 表示根据触控位置自动分列（推荐）</summary>
        public int fixedColumnCount = 0;

        /// <summary>是否根据触控位置自动分列（关掉才会用固定列数均分录入框）</summary>
        public bool autoColumns = true;

        /// <summary>自动分列时允许的最大列数（超过就合并距离最近的两列）</summary>
        public int maxColumns = 16;

        /// <summary>自动分列的合并阈值：占录入框宽度的百分比，按二维距离判断（X 与 Y 都算）</summary>
        public float columnMergePercent = 4.5f;

        /// <summary>录入框纵向映射到的世界高度（世界单位）；屏幕上方对应更大的 Y</summary>
        public float fieldHeight = 6f;

        /// <summary>录入框横向映射到的世界宽度：恒为纵向的 16/9，绝不单独拉伸某一轴</summary>
        public float FieldWidth { get { return Mathf.Max(0.1f, fieldHeight) * FieldAspect; } }

        public float tapMaxDuration = 0.15f; // 判定为 Tap 的最大持续时间（秒）
        public float dragMinDuration = 0.2f; // 判定为 Drag 的最小持续时间（秒）

        /// <summary>
        /// 最小有效压力，&lt;= 0 表示不做压力过滤（默认）。
        /// 多数安卓设备把 pressure 报成恒定的极小值（例如 0.001），
        /// 按阈值过滤会把所有音符都删掉，所以默认关闭，由设备压力是否可用自动决定。
        /// </summary>
        public float minPressure = 0f;
        public float mergeWindow = 0.05f; // 合并窗口（秒），同一列内此时间内的多点视为同一音符
        public float pathSplitMargin = 0.05f; // 固定列模式的边距（占宽度比例）

        /// <summary>
        /// 同一条手指轨迹内允许的单帧位移（占录入框宽度的百分比）。
        /// 超过它说明这不是「同一根手指在移动」，而是抬指帧被采样漏掉之后
        /// 另一次按压被报成了同一根手指：必须拆成两个音符，否则两次不同位置的
        /// 按压会被平均成一个位于中间的错误音符。0 = 关闭该判定。
        /// </summary>
        public float maxJumpPercent = 20f;
    }

    /// <summary>
    /// 一次转换得到的自由布局：路径定义 + 首个小节的位置摆放。
    /// 列的位置来自触控数据本身，所以铺面没有固定轨道。
    /// </summary>
    public sealed class TouchNoteLayout
    {
        public string[] pathIds = new string[0];
        public PathData[] paths = new PathData[0];
        public PathPlacement[] placements = new PathPlacement[0];
        /// <summary>每一列中心的世界 X（来自录入框里的真实触控位置）</summary>
        public float[] columnCenters = new float[0];
        /// <summary>每一列中心的世界 Y —— 四角录入必须落在四角，纵向位置不能丢</summary>
        public float[] columnCentersY = new float[0];

        public int Count { get { return pathIds.Length; } }
    }
}
