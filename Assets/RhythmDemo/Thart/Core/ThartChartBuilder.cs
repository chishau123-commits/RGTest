using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeometryRhythm.Thart
{
    /// <summary>
    /// 将触控录制数据转换为铺面音符数据的工具类。
    ///
    /// 关键约定：游戏铺面没有固定轨道，所以这里不会把触控点压成「4 条等宽轨道」。
    /// 触控点的全屏百分比位置会被保留，并按位置聚类成若干「列」，每一列在铺面里
    /// 就是一条位置自由的路径（PathPlacement.x/y 来自真实的触控位置）。
    /// </summary>
    public static class ThartChartBuilder
    {
        /// <summary>
        /// 常用网格密度预设（每拍的等分数）：1 拍、1/2 拍、1/3 拍、1/4 拍、1/6 拍、1/8 拍
        /// </summary>
        public static readonly int[] GridPresets = { 1, 2, 3, 4, 6, 8 };

        /// <summary>
        /// 网格密度显示文本
        /// </summary>
        public static string GridLabel(int divisor)
        {
            if (divisor <= 0) return "关闭";
            if (divisor == 1) return "1 拍";
            return "1/" + divisor + " 拍";
        }

        /// <summary>
        /// 把 tick 吸附到最近的网格线上
        /// </summary>
        public static int SnapTick(int tick, int ticksPerBeat, int gridDivisor)
        {
            if (gridDivisor <= 0 || ticksPerBeat <= 0) return tick;

            // 用 double 计算，允许 n 不为 ticksPerBeat 的约数（例如 1/7 拍）
            double gridTicks = ticksPerBeat / (double)gridDivisor;
            if (gridTicks <= 0) return tick;

            return (int)Math.Round(Math.Round(tick / gridTicks) * gridTicks);
        }

        #region 触控 → 音符事件

        /// <summary>
        /// 将触控录制数据转换为音符事件列表（不带布局信息）
        /// </summary>
        public static List<ThartNoteEvent> ConvertTouchToNotes(
            ThartTouchRecording recording,
            TouchToNoteConfig config)
        {
            TouchNoteLayout ignored;
            return ConvertTouchToNotes(recording, config, out ignored);
        }

        /// <summary>
        /// 将触控录制数据转换为音符事件列表，并给出与位置匹配的自由布局
        /// </summary>
        public static List<ThartNoteEvent> ConvertTouchToNotes(
            ThartTouchRecording recording,
            TouchToNoteConfig config,
            out TouchNoteLayout layout)
        {
            if (config == null) config = new TouchToNoteConfig();

            var events = new List<ThartNoteEvent>();
            if (recording == null || recording.samples == null || recording.samples.Length == 0)
            {
                layout = BuildFixedLayout(config, 1);
                return events;
            }

            float screenW, screenH;
            recording.FrameSize(out screenW, out screenH);

            // 只有录制数据里的压力确实有变化时，压力过滤才有意义。
            // 安卓平板普遍把 pressure 固定报成 0.001，若照阈值过滤会把所有轨迹删光，
            // 表现为「录完却一个音符都没有」。
            bool pressureUsable = HasUsablePressure(recording);

            // 1. 追踪每根手指的完整轨迹
            var fingerTracks = new Dictionary<int, FingerTrack>();
            var completedTracks = new List<FingerTrack>();

            foreach (var sample in recording.samples)
            {
                if (sample.touches == null) continue;

                foreach (var touch in sample.touches)
                {
                    if (touch.phase == TouchPhase.Began)
                    {
                        // 同一个 fingerId 又收到 Began，说明上一次抬指的 Ended 采样丢了
                        // （60Hz 采样很容易漏掉快速点按的抬指帧）。旧轨迹必须在这里收尾，
                        // 否则会被直接覆盖，表现就是「点了却没音符」。
                        if (fingerTracks.TryGetValue(touch.fingerId, out var stale))
                        {
                            completedTracks.Add(stale);
                            fingerTracks.Remove(touch.fingerId);
                        }

                        var track = new FingerTrack
                        {
                            fingerId = touch.fingerId,
                            startTime = sample.time,
                            maxPressure = touch.pressure,
                            points = new List<TrackPoint>()
                        };
                        track.points.Add(MakeTrackPoint(touch, sample.time, screenW, screenH));
                        fingerTracks[touch.fingerId] = track;
                    }
                    else if (fingerTracks.TryGetValue(touch.fingerId, out var track))
                    {
                        var point = MakeTrackPoint(touch, sample.time, screenW, screenH);

                        // 抬指帧被采样漏掉时，下一次按压会以同一 fingerId 的 Moved/Ended
                        // 出现，位置却是另一个角。人不可能在一帧内瞬移大半个录入框，
                        // 所以这种跳变按「新手指出现在这里」处理，否则两次按压会被
                        // 平均成一个中间位置的音符（四个角就变成了三列）。
                        if (IsJump(track, point, config))
                        {
                            completedTracks.Add(track);
                            fingerTracks.Remove(touch.fingerId);

                            var reborn = new FingerTrack
                            {
                                fingerId = touch.fingerId,
                                startTime = sample.time,
                                endTime = sample.time,
                                maxPressure = touch.pressure,
                                points = new List<TrackPoint>()
                            };
                            reborn.points.Add(point);
                            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                                completedTracks.Add(reborn);
                            else
                                fingerTracks[touch.fingerId] = reborn;
                            continue;
                        }

                        track.points.Add(point);
                        track.endTime = sample.time;
                        if (touch.pressure > track.maxPressure)
                            track.maxPressure = touch.pressure;

                        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        {
                            completedTracks.Add(track);
                            fingerTracks.Remove(touch.fingerId);
                        }
                    }
                    else
                    {
                        // 没收到 Began（丢包/录制开始时就已按下）：补一条轨迹
                        var late = new FingerTrack
                        {
                            fingerId = touch.fingerId,
                            startTime = sample.time,
                            endTime = sample.time,
                            maxPressure = touch.pressure,
                            points = new List<TrackPoint>()
                        };
                        late.points.Add(MakeTrackPoint(touch, sample.time, screenW, screenH));
                        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                            completedTracks.Add(late);
                        else
                            fingerTracks[touch.fingerId] = late;
                    }
                }
            }

            foreach (var kvp in fingerTracks)
                completedTracks.Add(kvp.Value);

            // 2. 每条轨迹 → 一个音符事件（位置保持全屏百分比）
            foreach (var track in completedTracks)
            {
                if (track.points.Count == 0) continue;
                if (pressureUsable && config.minPressure > 0f && track.maxPressure < config.minPressure) continue;

                double duration = track.endTime - track.startTime;
                if (track.endTime <= 0 && duration <= 0) duration = 0;

                float avgPressure = 0, avgX = 0, avgY = 0;
                foreach (var p in track.points)
                {
                    avgPressure += p.pressure;
                    // 拖动音符用两端更稳：这里用起点与终点平均
                    avgX += p.xPercent;
                    avgY += p.yPercent;
                }
                int n = track.points.Count;
                avgPressure /= n;
                avgX /= n;
                avgY /= n;

                string noteType = duration <= config.tapMaxDuration ? "tap" : "drag";

                events.Add(new ThartNoteEvent
                {
                    time = track.startTime,
                    xPercent = Mathf.Clamp(avgX, 0f, 100f),
                    yPercent = Mathf.Clamp(avgY, 0f, 100f),
                    noteType = noteType,
                    pathIndex = -1,
                    duration = duration,
                    pressure = avgPressure
                });
            }

            // 3. 按时间排序
            events.Sort((a, b) => a.time.CompareTo(b.time));

            // 4. 合并时间相近、位置也相近的点（不做轨道假设）
            if (config.mergeWindow > 0)
                events = MergeCloseNotes(events, config);

            // 5. 按真实位置聚类成列，并生成自由布局
            layout = config.autoColumns
                ? BuildFreeLayout(events, config)
                : BuildFixedLayout(config, Mathf.Clamp(config.fixedColumnCount, 1, 32));

            AssignColumns(events, layout, config);

            return events;
        }

        /// <summary>
        /// 判断录制数据里的压力是否有实际区分度。
        /// 安卓设备常把 pressure 固定成 0.001 这类常数，此时压力不可用，必须跳过压力过滤。
        /// </summary>
        private static bool HasUsablePressure(ThartTouchRecording recording)
        {
            if (recording == null || recording.samples == null) return false;

            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (var sample in recording.samples)
            {
                if (sample == null || sample.touches == null) continue;
                foreach (var t in sample.touches)
                {
                    if (t.pressure < min) min = t.pressure;
                    if (t.pressure > max) max = t.pressure;
                }
            }

            if (min > max) return false;      // 一个触控点都没有
            return max - min >= 0.05f;        // 有明显变化才认为压力可用
        }

        /// <summary>
        /// 轨迹里出现「瞬移」：距上一个点超过 maxJumpPercent（占录入框宽度）就认为
        /// 这不是同一根手指在移动，而是漏掉抬指帧之后的另一次按压。
        /// 60Hz 采样下，物理上不可能出现这么大的单帧位移（20% 宽度 ≈ 40 m/s）。
        /// </summary>
        private static bool IsJump(FingerTrack track, TrackPoint point, TouchToNoteConfig config)
        {
            if (config == null || config.maxJumpPercent <= 0f) return false;
            if (track == null || track.points == null || track.points.Count == 0) return false;

            var last = track.points[track.points.Count - 1];
            Vector2 a = PercentToWorld(last.xPercent, last.yPercent, config);
            Vector2 b = PercentToWorld(point.xPercent, point.yPercent, config);
            return Vector2.Distance(a, b) > config.maxJumpPercent / 100f * config.FieldWidth;
        }

        private static TrackPoint MakeTrackPoint(TouchPoint touch, double time, float frameW, float frameH)
        {
            float xp = touch.xPercent;
            float yp = touch.yPercent;
            // 旧版数据没有百分比字段，用录入框内的像素现算
            if (xp <= 0f && yp <= 0f && (touch.x > 0f || touch.y > 0f))
            {
                xp = Mathf.Clamp01(touch.x / frameW) * 100f;
                yp = Mathf.Clamp01(touch.y / frameH) * 100f;
            }
            return new TrackPoint
            {
                time = time,
                xPercent = Mathf.Clamp(xp, 0f, 100f),
                yPercent = Mathf.Clamp(yp, 0f, 100f),
                pressure = touch.pressure
            };
        }

        #endregion

        #region 布局

        /// <summary>
        /// 根据触控位置自动分列：按 X 与 Y 的二维距离聚簇。
        ///
        /// 只按 X 聚簇是不够的 —— 在左上一个点、左下一个点会被并成同一列，
        /// 纵向位置被平均掉，于是「按了四个角，制铺器里只有两条竖线」。
        /// 这里把每根手指落点当成平面上的位置来聚簇，四角就是四列。
        /// </summary>
        private static TouchNoteLayout BuildFreeLayout(List<ThartNoteEvent> events, TouchToNoteConfig config)
        {
            if (events.Count == 0) return BuildFixedLayout(config, 1);

            // 合并阈值：录入框宽度的百分之几，换算成世界单位后就是二维半径
            float mergeDistance = Mathf.Max(0.05f, config.columnMergePercent / 100f * config.FieldWidth);

            // 顺序处理（音符号是按时间排好的）：每个音符挂到最近的簇上，
            // 同一个位置反复按不会因为按下顺序不同而被拆成好几条路径。
            var clusters = new List<ColumnCluster>();
            foreach (var e in events)
            {
                Vector2 world = PercentToWorld(e.xPercent, e.yPercent, config);

                int best = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < clusters.Count; i++)
                {
                    float d = clusters[i].DistanceTo(world);
                    if (d < bestDist) { bestDist = d; best = i; }
                }

                if (best >= 0 && bestDist <= mergeDistance) clusters[best].Add(world);
                else clusters.Add(new ColumnCluster(world));
            }

            // 列数超上限：反复合并距离最近的两列
            int maxColumns = Mathf.Clamp(config.maxColumns, 1, 32);
            while (clusters.Count > maxColumns)
            {
                int closeA = 0, closeB = 1;
                float closeGap = float.MaxValue;
                for (int i = 0; i < clusters.Count; i++)
                {
                    for (int j = i + 1; j < clusters.Count; j++)
                    {
                        float gap = Vector2.Distance(clusters[i].Center, clusters[j].Center);
                        if (gap < closeGap) { closeGap = gap; closeA = i; closeB = j; }
                    }
                }
                clusters[closeA].Absorb(clusters[closeB]);
                clusters.RemoveAt(closeB);
            }

            // 从左到右编 P1..Pn，铺面里的列顺序与平面上的左右顺序一致
            clusters.Sort((a, b) => a.Center.x.CompareTo(b.Center.x));

            var ids = new string[clusters.Count];
            var paths = new PathData[clusters.Count];
            var placements = new PathPlacement[clusters.Count];
            var centers = new float[clusters.Count];
            var centersY = new float[clusters.Count];

            for (int i = 0; i < clusters.Count; i++)
            {
                string id = "p" + i;
                Vector2 center = clusters[i].Center;

                ids[i] = id;
                centers[i] = center.x;
                centersY[i] = center.y;

                paths[i] = new PathData { id = id, roll = 0 };

                placements[i] = new PathPlacement
                {
                    pathId = id,
                    x = center.x,
                    y = center.y,
                    bend = 0f,
                    lift = 0f
                };
            }

            return new TouchNoteLayout
            {
                pathIds = ids,
                paths = paths,
                placements = placements,
                columnCenters = centers,
                columnCentersY = centersY
            };
        }

        /// <summary>固定列模式：把录入框宽度等分，纵向一律居中（保留给需要整齐排布的场景）</summary>
        private static TouchNoteLayout BuildFixedLayout(TouchToNoteConfig config, int count)
        {
            count = Mathf.Clamp(count, 1, 32);
            var ids = new string[count];
            var paths = new PathData[count];
            var placements = new PathPlacement[count];
            var centers = new float[count];
            var centersY = new float[count];

            for (int i = 0; i < count; i++)
            {
                string id = "p" + i;
                ids[i] = id;
                paths[i] = new PathData { id = id, roll = 0 };

                float centerPercent = count == 1 ? 50f : (i / (float)(count - 1)) * 100f;
                Vector2 world = PercentToWorld(centerPercent, 50f, config);
                centers[i] = world.x;
                centersY[i] = world.y;
                placements[i] = new PathPlacement
                {
                    pathId = id,
                    x = world.x,
                    y = world.y,
                    bend = 0f,
                    lift = 0f
                };
            }

            return new TouchNoteLayout
            {
                pathIds = ids,
                paths = paths,
                placements = placements,
                columnCenters = centers,
                columnCentersY = centersY
            };
        }

        /// <summary>录入框百分比 → 世界坐标（屏幕上方 = 更大的 Y），横向永远是纵向的 16/9</summary>
        public static Vector2 PercentToWorld(float xPercent, float yPercent, TouchToNoteConfig config)
        {
            float h = config != null && config.fieldHeight > 0.1f ? config.fieldHeight : 6f;
            float w = config != null ? config.FieldWidth : 6f * TouchToNoteConfig.FieldAspect;
            float x = (Mathf.Clamp01(xPercent / 100f) - 0.5f) * w;
            float y = (0.5f - Mathf.Clamp01(yPercent / 100f)) * h;
            return new Vector2(x, y);
        }

        private static void AssignColumns(List<ThartNoteEvent> events, TouchNoteLayout layout, TouchToNoteConfig config)
        {
            if (layout == null || layout.Count == 0)
            {
                foreach (var e in events) e.pathIndex = 0;
                return;
            }

            if (!config.autoColumns)
            {
                int count = layout.Count;
                foreach (var e in events)
                {
                    float normalized = Mathf.Clamp(e.xPercent - config.pathSplitMargin * 100f, 0f,
                        100f - config.pathSplitMargin * 200f);
                    int idx = Mathf.FloorToInt(normalized / (100f / count));
                    e.pathIndex = Mathf.Clamp(idx, 0, count - 1);
                }
                return;
            }

            // 二维最近列：纵向位置也参与匹配，否则上下两个角会抢同一条列
            foreach (var e in events)
            {
                Vector2 world = PercentToWorld(e.xPercent, e.yPercent, config);

                int best = 0;
                float bestDist = float.MaxValue;
                for (int i = 0; i < layout.columnCenters.Length; i++)
                {
                    float dy = i < layout.columnCentersY.Length ? layout.columnCentersY[i] - world.y : 0f;
                    float dx = layout.columnCenters[i] - world.x;
                    float d = dx * dx + dy * dy;
                    if (d < bestDist) { bestDist = d; best = i; }
                }
                e.pathIndex = best;
            }
        }

        /// <summary>合并时间相近且平面位置都相近的点（不依赖轨道编号）</summary>
        private static List<ThartNoteEvent> MergeCloseNotes(List<ThartNoteEvent> notes, TouchToNoteConfig config)
        {
            if (notes.Count <= 1) return notes;

            // 位置窗口用世界单位量：同时看 X 与 Y，两个角上的同时按下不会被并成一个音符
            float window = Mathf.Max(0.05f, config.columnMergePercent / 100f * config.FieldWidth);

            var merged = new List<ThartNoteEvent>();
            foreach (var note in notes)
            {
                bool mergedIn = false;
                Vector2 world = PercentToWorld(note.xPercent, note.yPercent, config);

                for (int i = merged.Count - 1; i >= 0 && i >= merged.Count - 8; i--)
                {
                    var last = merged[i];
                    if (note.time - (last.time + last.duration) > config.mergeWindow) continue;
                    Vector2 lastWorld = PercentToWorld(last.xPercent, last.yPercent, config);
                    if (Vector2.Distance(world, lastWorld) > window) continue;

                    // 合并：保持较早的时间，持续时间覆盖到较晚的结束点
                    double newEnd = Math.Max(last.time + last.duration, note.time + note.duration);
                    last.duration = newEnd - last.time;
                    last.noteType = last.duration <= config.tapMaxDuration ? "tap" : "drag";
                    last.xPercent = (last.xPercent + note.xPercent) * 0.5f;
                    last.yPercent = (last.yPercent + note.yPercent) * 0.5f;
                    last.pressure = (last.pressure + note.pressure) * 0.5f;
                    mergedIn = true;
                    break;
                }

                if (!mergedIn) merged.Add(note);
            }

            return merged;
        }

        #endregion

        #region 音符事件 → ChartData 音符

        /// <summary>
        /// 将音符事件转换为 ChartData 格式的 NoteData
        /// </summary>
        /// <param name="events">音符事件（pathIndex 已按布局填好）</param>
        /// <param name="tempoMap">节拍映射</param>
        /// <param name="ticksPerBeat">每拍 tick 数</param>
        /// <param name="pathIds">布局里的路径 ID 列表</param>
        /// <param name="gridDivisor">录制后自动吸附的网格密度（每拍等分数，0 = 不吸附）</param>
        /// <param name="dedupeSameTick">同一列同一 tick 上只保留一个音符</param>
        public static NoteData[] ConvertToChartNotes(
            List<ThartNoteEvent> events,
            TempoMap tempoMap,
            int ticksPerBeat,
            string[] pathIds,
            int gridDivisor = 0,
            bool dedupeSameTick = true)
        {
            var notes = new List<NoteData>();
            var occupied = new HashSet<string>();
            int noteCounter = 0;

            if (events == null || tempoMap == null || pathIds == null || pathIds.Length == 0)
                return notes.ToArray();

            foreach (var ev in events)
            {
                double beat = tempoMap.BeatAtSeconds(ev.time);
                int tick = Mathf.RoundToInt((float)(beat * ticksPerBeat));

                // 录制结束后自动吸附到网格
                if (gridDivisor > 0)
                    tick = SnapTick(tick, ticksPerBeat, gridDivisor);

                if (tick < 0) tick = 0;

                int pathIndex = Mathf.Clamp(ev.pathIndex < 0 ? 0 : ev.pathIndex, 0, pathIds.Length - 1);

                // 吸附后可能出现同列同时刻的重复音符，去重
                if (dedupeSameTick)
                {
                    string key = pathIds[pathIndex] + ":" + tick;
                    if (!occupied.Add(key)) continue;
                }

                notes.Add(new NoteData
                {
                    id = "n" + noteCounter.ToString("D6"),
                    tick = tick,
                    pathId = pathIds[pathIndex],
                    action = ev.noteType,
                    protectedNote = false
                });
                noteCounter++;
            }

            return notes.ToArray();
        }

        #endregion

        #region 内部辅助

        private class FingerTrack
        {
            public int fingerId;
            public double startTime;
            public double endTime;
            public float maxPressure;
            public List<TrackPoint> points;
        }

        private struct TrackPoint
        {
            public double time;
            public float xPercent, yPercent;
            public float pressure;
        }

        /// <summary>
        /// 一条路径的候选位置：平面上的一簇触控点。
        /// 均值存的是世界坐标，所以 X 与 Y 的地位相同，纵向不会被丢掉。
        /// </summary>
        private class ColumnCluster
        {
            private float xSum, ySum;
            private int count;

            public ColumnCluster(Vector2 world)
            {
                xSum = world.x; ySum = world.y; count = 1;
            }

            public void Add(Vector2 world) { xSum += world.x; ySum += world.y; count++; }
            public void Absorb(ColumnCluster other) { xSum += other.xSum; ySum += other.ySum; count += other.count; }

            public Vector2 Center
            {
                get { return count > 0 ? new Vector2(xSum / count, ySum / count) : Vector2.zero; }
            }

            public float DistanceTo(Vector2 world) { return Vector2.Distance(Center, world); }
        }

        #endregion
    }
}
