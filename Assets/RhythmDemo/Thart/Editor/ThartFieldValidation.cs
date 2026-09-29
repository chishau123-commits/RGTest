using System;
using UnityEditor;
using UnityEngine;
using GeometryRhythm;
using GeometryRhythm.Thart;
using GeometryRhythm.Thart.Editor;
using GeometryRhythm.Thart.TouchRecorder;
using ThartTouchPhase = GeometryRhythm.Thart.TouchPhase;

namespace GeometryRhythm.Thart.EditorTools
{
    /// <summary>
    /// Thart 坐标域回归：录入框 / 铺面 / 游玩界面都必须是 16:9，
    /// 而且「手指按在四个角」必须真的变成「铺面里四个角各一列」。
    ///
    /// 这里全部用合成录制数据验证，不依赖平板、不依赖 UI：
    /// 位置错了就是数学错了，一眼能看出来。
    /// </summary>
    public static class ThartFieldValidation
    {
        public static void RunBatch()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            int checks = 0;
            var config = new TouchToNoteConfig();

            Check(Mathf.Abs(TouchToNoteConfig.FieldAspect - 16f / 9f) < .00001f,
                "canvas aspect is 16:9", ref checks);
            Check(Mathf.Abs(config.FieldWidth - config.fieldHeight * 16f / 9f) < .00001f,
                "field width is derived from the height at 16:9", ref checks);

            // 百分比 → 世界：左上角是 (-x, +y)（屏幕左边是负 X），右下角是 (+x, -y)
            Vector2 topLeft = ThartChartBuilder.PercentToWorld(0f, 0f, config);
            Vector2 bottomRight = ThartChartBuilder.PercentToWorld(100f, 100f, config);
            Check(Mathf.Abs(topLeft.x + config.FieldWidth * .5f) < .001f &&
                  Mathf.Abs(topLeft.y - config.fieldHeight * .5f) < .001f,
                "0%,0% is the top-left corner of the 16:9 field: " + topLeft, ref checks);
            Check(Mathf.Abs(bottomRight.x - config.FieldWidth * .5f) < .001f &&
                  Mathf.Abs(bottomRight.y + config.fieldHeight * .5f) < .001f,
                "100%,100% is the bottom-right corner: " + bottomRight, ref checks);
            Check(Mathf.Abs(config.FieldWidth / config.fieldHeight - 16f / 9f) < .0001f,
                "world field keeps the 16:9 ratio (no axis is stretched)", ref checks);

            // 四角录入 → 四列，列的位置就是四个角
            var corners = BuildCornerRecording();
            TouchNoteLayout layout;
            var events = ThartChartBuilder.ConvertTouchToNotes(corners, config, out layout);
            Check(layout != null && layout.Count == 4,
                "four corners become four paths: " + (layout == null ? -1 : layout.Count), ref checks);
            Check(events.Count == 5, "corner taps become five note events: " + events.Count, ref checks);

            if (layout != null && layout.Count == 4)
            {
                int topLeftCluster = NearestColumn(layout, ThartChartBuilder.PercentToWorld(5f, 5f, config));
                int topRightCluster = NearestColumn(layout, ThartChartBuilder.PercentToWorld(95f, 5f, config));
                int bottomLeftCluster = NearestColumn(layout, ThartChartBuilder.PercentToWorld(5f, 95f, config));
                int bottomRightCluster = NearestColumn(layout, ThartChartBuilder.PercentToWorld(95f, 95f, config));

                Check(topLeftCluster != topRightCluster && topLeftCluster != bottomLeftCluster &&
                      topLeftCluster != bottomRightCluster && topRightCluster != bottomLeftCluster &&
                      topRightCluster != bottomRightCluster && bottomLeftCluster != bottomRightCluster,
                    "the four corners are four distinct paths", ref checks);

                Check(layout.placements[topLeftCluster].x < 0f && layout.placements[topLeftCluster].y > 0f,
                    "top-left column is left and above: " + layout.placements[topLeftCluster].x + "," +
                    layout.placements[topLeftCluster].y, ref checks);
                Check(layout.placements[topRightCluster].x > 0f && layout.placements[topRightCluster].y > 0f,
                    "top-right column is right and above", ref checks);
                Check(layout.placements[bottomLeftCluster].x < 0f && layout.placements[bottomLeftCluster].y < 0f,
                    "bottom-left column is left and below", ref checks);
                Check(layout.placements[bottomRightCluster].x > 0f && layout.placements[bottomRightCluster].y < 0f,
                    "bottom-right column is right and below", ref checks);

                // 四角的世界跨度必须就是整个 16:9 场地的四角（不是被归一化到某条线上的）
                Check(Mathf.Abs(layout.placements[topLeftCluster].y - config.fieldHeight * .45f) < .2f &&
                      Mathf.Abs(layout.placements[bottomLeftCluster].y + config.fieldHeight * .45f) < .2f,
                    "corner columns keep their vertical position instead of collapsing to the middle",
                    ref checks);

                // 同一位置重复按：仍然挂在同一条列上，不会被拆成两列
                Check(events.Count > 0 && events[events.Count - 1].pathIndex == topLeftCluster,
                    "a repeated press at one corner reuses that corner's path", ref checks);
            }

            // 同时按下：同一位置合并成一个音符，两个角保持两个音符
            var simultaneous = BuildSimultaneousRecording();
            TouchNoteLayout simultaneousLayout;
            var simultaneousEvents = ThartChartBuilder.ConvertTouchToNotes(simultaneous, config, out simultaneousLayout);
            Check(simultaneousEvents.Count == 2,
                "same-spot chord notes merge, opposite corners do not: " + simultaneousEvents.Count, ref checks);

            // 抬指帧丢了：同一 fingerId 的下一次按压落在另一个角，绝不能平均成一个中间音符
            TouchNoteLayout missedLiftLayout;
            var missedLiftEvents = ThartChartBuilder.ConvertTouchToNotes(
                BuildMissedLiftRecording(), config, out missedLiftLayout);
            Check(missedLiftEvents.Count == 3,
                "a missed lift-off does not average two presses into one: " + missedLiftEvents.Count, ref checks);
            Check(missedLiftLayout != null && missedLiftLayout.Count == 3,
                "the three presses stay three paths: " + (missedLiftLayout == null ? -1 : missedLiftLayout.Count),
                ref checks);
            if (missedLiftEvents.Count == 3 && missedLiftLayout != null)
            {
                // 不能有任何一个音符落在场地中间（两个角被平均的典型结果）
                bool anyMiddle = false;
                foreach (var e in missedLiftEvents)
                    if (Mathf.Abs(e.xPercent - 50f) < 20f && Mathf.Abs(e.yPercent - 50f) < 20f) anyMiddle = true;
                Check(!anyMiddle, "no note lands where two corners were averaged", ref checks);
            }

            // 时间轴拖动：锁住窗口后，同一个鼠标位置必须给出同一个时间
            const double duration = 200.0;
            const float zoom = 8f;
            const float contentW = 1000f;
            double viewStart, viewSpan;
            ThartEditorController.TimelineWindow(duration, zoom, 60.0, out viewStart, out viewSpan);
            Check(Math.Abs(viewSpan - duration / zoom) < .001, "zoom selects the visible span", ref checks);

            double firstTime = ThartEditorController.TimelineTimeAtLocalX(contentW * .9f, contentW, viewStart, viewSpan);
            double lockedTime = firstTime, unlockedTime = firstTime;
            double lockedStart = viewStart, unlockedStart = viewStart;
            for (int i = 0; i < 20; i++)
            {
                // 修复后：窗口锁在 MouseDown 那一刻
                lockedTime = ThartEditorController.TimelineTimeAtLocalX(contentW * .9f, contentW, lockedStart, viewSpan);
                // 修复前：窗口每帧以播放头为中心重算，鼠标不动也会一直往右跑
                ThartEditorController.TimelineWindow(duration, zoom, unlockedTime, out unlockedStart, out viewSpan);
                unlockedTime = ThartEditorController.TimelineTimeAtLocalX(contentW * .9f, contentW, unlockedStart, viewSpan);
            }
            double lockedDrift = Math.Abs(lockedTime - firstTime);
            double unlockedDrift = Math.Abs(unlockedTime - firstTime);
            Check(lockedDrift < .0001, "a locked scrub holds the pointer's time: drift=" + lockedDrift, ref checks);
            Check(unlockedDrift > 20.0,
                "the old playhead-centred window is what ran away: drift=" + unlockedDrift, ref checks);

            // 游玩视口：永远 16:9，且居中
            CheckFrame(new Vector2(1920, 1080), 1920, 1080, 0, 0, ref checks);
            CheckFrame(new Vector2(2400, 1080), 1920, 1080, 240, 0, ref checks);
            CheckFrame(new Vector2(1280, 1024), 1280, 720, 0, 152, ref checks);
            CheckFrame(new Vector2(1600, 900), 1600, 900, 0, 0, ref checks);

            // 平板 16:9 录入框：不管平板是什么比例，框本身必须是精确 16:9，
            // 并且完整落在「顶部应用栏 .. 底部状态条」之间。
            CheckTabletFrame(2560, 1600, 1.8f, ref checks);   // 16:10 平板
            CheckTabletFrame(2400, 1080, 1.35f, ref checks);  // 20:9 手机
            CheckTabletFrame(1920, 1080, 1.35f, ref checks);  // 16:9
            CheckTabletFrame(1024, 768, 1.0f, ref checks);    // 4:3

            Rect frame = GameViewport.Frame(new Vector2(2400, 1080));
            Rect content = GameViewport.Content(frame, new Rect(0, 0, 2400, 1080));
            Check(Mathf.Abs(content.width - 1920f) < .001f && Mathf.Abs(content.height - 1080f) < .001f,
                "UI content fills the 16:9 playfield when the safe area allows it: " + content, ref checks);
            Rect notched = GameViewport.Content(frame, new Rect(200, 0, 2000, 1080));
            Check(notched.xMin >= 240f - .001f &&
                  notched.xMin >= 200f - .001f && notched.xMax <= 2160f + .001f,
                "UI content never leaves the playfield: " + notched, ref checks);

            Debug.Log("THART_FIELD_VALIDATION_SUCCESS " + checks + " checks");
        }

        static void CheckFrame(Vector2 screen, float expectW, float expectH, float expectX, float expectY, ref int checks)
        {
            Rect frame = GameViewport.Frame(screen);
            Check(Mathf.Abs(frame.width - expectW) < .001f && Mathf.Abs(frame.height - expectH) < .001f &&
                  Mathf.Abs(frame.x - expectX) < .001f && Mathf.Abs(frame.y - expectY) < .001f,
                "viewport " + screen + " -> " + frame, ref checks);
            Check(Mathf.Abs(frame.width / frame.height - 16f / 9f) < .00001f,
                "viewport is exactly 16:9 at " + screen, ref checks);
        }

        /// <summary>平板录入框：精确 16:9、左右居中、完全在可用区域（应用栏与状态条之间）内</summary>
        static void CheckTabletFrame(int screenWidth, int screenHeight, float uiScale, ref int checks)
        {
            Rect frame = ThartTouchRecorder.CaptureFrameFor(screenWidth, screenHeight, uiScale);
            float bottomBar = 56f * uiScale;
            float topBar = 84f * uiScale;

            Check(Mathf.Abs(frame.width / frame.height - 16f / 9f) < .00001f,
                "tablet capture frame is exactly 16:9 at " + screenWidth + "x" + screenHeight + ": " + frame, ref checks);
            Check(frame.xMin >= -.001f && frame.xMax <= screenWidth + .001f &&
                  frame.yMin >= bottomBar - .001f && frame.yMax <= screenHeight - topBar + .001f,
                "tablet capture frame stays between the top bar and the status bar: " + frame, ref checks);
            Check(Mathf.Abs((frame.xMin + frame.xMax) * .5f - screenWidth * .5f) <= .5f,
                "tablet capture frame is horizontally centred: " + frame, ref checks);
            Check(frame.width >= 16f && frame.height >= 9f,
                "tablet capture frame is usable: " + frame, ref checks);

            // 触控点 → 录入框内百分比：框的四角必须正好是 0%/100%。
            // （曾经把「屏幕高 - y」的左上角原点和左下角原点的 frame.y 混用，
            //  录下来的 Y 会整体偏移「屏幕高 - 框高 - 2×框下边距」那么多。）
            // 边界点用 0.001px 往内挪一点：Rect.Contains 把上/右边界当开区间。
            float localX, localY, xPercent, yPercent;
            Check(ThartTouchRecorder.FramePoint(frame, new Vector2(frame.x + .001f, frame.yMax - .001f),
                    out localX, out localY, out xPercent, out yPercent) &&
                  Mathf.Abs(xPercent) < .001f && Mathf.Abs(yPercent) < .001f,
                "frame top-left maps to 0%,0%: " + xPercent + "," + yPercent, ref checks);
            Check(ThartTouchRecorder.FramePoint(frame, new Vector2(frame.xMax - .001f, frame.y + .001f),
                    out localX, out localY, out xPercent, out yPercent) &&
                  Mathf.Abs(xPercent - 100f) < .01f && Mathf.Abs(yPercent - 100f) < .01f,
                "frame bottom-right maps to 100%,100%: " + xPercent + "," + yPercent, ref checks);
            Check(ThartTouchRecorder.FramePoint(frame, new Vector2(frame.x + .001f, frame.y + .001f),
                    out localX, out localY, out xPercent, out yPercent) &&
                  Mathf.Abs(xPercent) < .001f && Mathf.Abs(yPercent - 100f) < .001f,
                "frame bottom-left maps to 0%,100%: " + xPercent + "," + yPercent, ref checks);
            Check(!ThartTouchRecorder.FramePoint(frame, new Vector2(frame.center.x, frame.yMax + 2f),
                    out localX, out localY, out xPercent, out yPercent),
                "a press in the top bar is rejected", ref checks);

            // 一条与设备无关的通用检查：任意框、任意内缩比例下都必须是线性映射
            var probeFrame = new Rect(4f, 136f, 3040f, 1710f);   // 2032 高屏幕上的真实框
            float probeTop = 2032f - probeFrame.yMax;            // 框顶在左上角原点下的位置
            float expected = 6.02f;
            ThartTouchRecorder.FramePoint(probeFrame, new Vector2(probeFrame.x + 0.06f * probeFrame.width,
                probeFrame.yMax - 0.06f * probeFrame.height), out localX, out localY, out xPercent, out yPercent);
            Check(Mathf.Abs(xPercent - 6f) < .01f && Mathf.Abs(yPercent - expected) < .05f,
                "a 6% inset touch reads 6%/" + expected + "%, got " + xPercent + "%/" + yPercent + "%", ref checks);
            Check(probeTop > 0f, "probe frame is not vertically centred (the case that used to shift)", ref checks);
        }

        static int NearestColumn(TouchNoteLayout layout, Vector2 world)        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < layout.Count; i++)
            {
                float dx = layout.columnCenters[i] - world.x;
                float dy = layout.columnCentersY[i] - world.y;
                float distance = dx * dx + dy * dy;
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
            return best;
        }

        /// <summary>四个角各按一次，再回到左上角补按一次（检查同一位置复用同一列）</summary>
        static ThartTouchRecording BuildCornerRecording()
        {
            return new ThartTouchRecording
            {
                schemaVersion = 3,
                screenWidth = 2560,
                screenHeight = 1600,
                frameWidth = 2560,
                frameHeight = 1440,
                durationSeconds = 1.0,
                samples = new[]
                {
                    Tap(0, 0.10, 5f, 5f),
                    Tap(1, 0.20, 95f, 5f),
                    Tap(2, 0.30, 5f, 95f),
                    Tap(3, 0.40, 95f, 95f),
                    Tap(4, 0.50, 6f, 6f)
                }
            };
        }

        /// <summary>同一时刻：两个手指按住同一个点，第三个手指按对角</summary>
        static ThartTouchRecording BuildSimultaneousRecording()
        {
            var touches = new[]
            {
                new TouchPoint { fingerId = 0, xPercent = 20f, yPercent = 20f, pressure = 1f, phase = ThartTouchPhase.Began },
                new TouchPoint { fingerId = 1, xPercent = 21f, yPercent = 21f, pressure = 1f, phase = ThartTouchPhase.Began },
                new TouchPoint { fingerId = 2, xPercent = 80f, yPercent = 80f, pressure = 1f, phase = ThartTouchPhase.Began }
            };
            return new ThartTouchRecording
            {
                schemaVersion = 3,
                screenWidth = 2560,
                screenHeight = 1600,
                frameWidth = 2560,
                frameHeight = 1440,
                durationSeconds = 1.0,
                samples = new[]
                {
                    new TouchSample { time = 0.10, touches = touches }
                }
            };
        }

        /// <summary>
        /// 抬指帧丢失的真实录法：第一根手指按下左上角，没等到 Ended 采样，
        /// 第二、三次按压被 Unity 报成同一根手指的 Moved（位置在右上 / 右下）。
        /// 修好之前这三次会被平均成「中间偏上」和「右下」两个音符。
        /// </summary>
        static ThartTouchRecording BuildMissedLiftRecording()
        {
            return new ThartTouchRecording
            {
                schemaVersion = 3,
                screenWidth = 3048,
                screenHeight = 2032,
                frameWidth = 3040,
                frameHeight = 1710,
                durationSeconds = 2.0,
                samples = new[]
                {
                    new TouchSample
                    {
                        time = 0.20,
                        touches = new[]
                        {
                            new TouchPoint { fingerId = 0, xPercent = 6f, yPercent = 9f, pressure = 1f, phase = ThartTouchPhase.Began }
                        }
                    },
                    new TouchSample
                    {
                        time = 0.65,
                        touches = new[]
                        {
                            new TouchPoint { fingerId = 0, xPercent = 94f, yPercent = 9f, pressure = 1f, phase = ThartTouchPhase.Moved }
                        }
                    },
                    new TouchSample
                    {
                        time = 1.10,
                        touches = new[]
                        {
                            new TouchPoint { fingerId = 0, xPercent = 94f, yPercent = 97f, pressure = 1f, phase = ThartTouchPhase.Moved }
                        }
                    }
                }
            };
        }

        static TouchSample Tap(int fingerId, double time, float xPercent, float yPercent)        {
            return new TouchSample
            {
                time = time,
                touches = new[]
                {
                    new TouchPoint
                    {
                        fingerId = fingerId,
                        xPercent = xPercent,
                        yPercent = yPercent,
                        pressure = 1f,
                        phase = ThartTouchPhase.Began
                    }
                }
            };
        }

        static void Check(bool condition, string message, ref int checks)
        {
            checks++;
            if (!condition) throw new Exception("Thart field: " + message);
        }
    }
}
