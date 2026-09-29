using System;
using System.IO;
using UnityEngine;
using GeometryRhythm.Thart.Network;

namespace GeometryRhythm.Thart.Editor
{
    public sealed partial class ThartEditorController
    {
        // ======== 布局常量（预览摄像机也用这套数值算区域） ========
        private const float LayoutToolbarH = 58f;
        private const float LayoutStatusH = 30f;
        private const float LayoutGap = 8f;
        private const float TimelineToolbarH = 32f;
        private const float RulerH = 20f;

        // 时间轴里的音符绘制区左右留白（左边留给列标签）
        private const float TrackLeftPad = 48f;
        private const float TrackRightPad = 10f;

        // 时间轴拖动状态
        private bool scrubbing;
        // 拖动播放头期间锁住的可见窗口：不锁的话「窗口以播放头为中心」会正反馈 runaway
        private bool timelineViewLocked;
        private double lockedViewStart;
        private double lockedViewSpan;

        // 时间轴横向缩放：1 = 整首歌铺满，值越大看得越细。
        // 视图始终以播放头为中心，播放时自动跟着推进。
        private float timelineZoom = 1f;
        private const float MinTimelineZoom = 1f;
        private const float MaxTimelineZoom = 8f;
        private const float TimelineZoomStep = 1.2f;
        // OnGUI 一帧会跑多次（Layout/Repaint），用帧号保证一次滚轮只缩放一次
        private int lastZoomFrame = -1;

        // 面板尺寸拖动状态
        private bool draggingLeftSplitter;
        private bool draggingTimelineSplitter;
        private float leftSplitterGrab;
        private float timelineSplitterGrab;

        #region 布局

        private float ViewW { get { return Screen.width / Mathf.Max(0.01f, uiScale); } }
        private float ViewH { get { return Screen.height / Mathf.Max(0.01f, uiScale); } }

        private Rect GetToolbarRect()
        {
            return new Rect(0, 0, ViewW, LayoutToolbarH);
        }

        private Rect GetStatusRect()
        {
            return new Rect(0, ViewH - LayoutStatusH, ViewW, LayoutStatusH);
        }

        private Rect GetLeftPanelRect()
        {
            float y = LayoutToolbarH + LayoutGap;
            float h = ViewH - LayoutToolbarH - LayoutStatusH - LayoutGap * 2;
            return new Rect(LayoutGap, y, Mathf.Max(200f, leftPanelWidth), h);
        }

        /// <summary>右侧列（主视图 + 时间轴）的左边界：紧贴左面板右侧</summary>
        private float RightColumnX { get { return LayoutGap * 2 + leftPanelWidth; } }

        /// <summary>右侧列宽度</summary>
        private float RightColumnW { get { return Mathf.Max(120f, ViewW - RightColumnX - LayoutGap); } }

        /// <summary>
        /// 时间轴只占右侧列，绝不压在左面板上 —— 之前它横跨整屏，
        /// 后画的时间轴把左面板下半部分整个盖住，导致那部分信息看不见。
        /// </summary>
        private Rect GetTimelineRect()
        {
            float bottom = ViewH - LayoutStatusH - LayoutGap;
            return new Rect(RightColumnX, bottom - timelineHeight, RightColumnW, timelineHeight);
        }

        private Rect GetMainViewLogicalRect()
        {
            float y = LayoutToolbarH + LayoutGap;
            float w = RightColumnW;
            float h = ViewH - LayoutToolbarH - LayoutStatusH - timelineHeight - LayoutGap * 3;
            return new Rect(RightColumnX, y, Mathf.Max(60f, w), Mathf.Max(80f, h));
        }

        private Rect GetMainViewPixelRect()
        {
            Rect r = GetMainViewLogicalRect();
            float s = Mathf.Max(0.01f, uiScale);
            return new Rect(r.x * s, r.y * s, r.width * s, r.height * s);
        }

        /// <summary>
        /// 主视图里的 16:9 画面区域（像素，按 16x9 整数块取整）。
        /// 3D 预览渲染到这块贴图上，其余部分留黑边，比例永远精确 16:9。
        /// </summary>
        private Rect GetMainViewFieldPixelRect()
        {
            Rect px = GetMainViewPixelRect();
            float blocks = Mathf.Max(1f, Mathf.Floor(Mathf.Min(px.width / 16f, px.height / 9f)));
            float w = blocks * 16f;
            float h = blocks * 9f;
            return new Rect(Mathf.Round(px.x + (px.width - w) * 0.5f),
                Mathf.Round(px.y + (px.height - h) * 0.5f), w, h);
        }

        /// <summary>同上的逻辑坐标版本，用于把预览贴图画回界面</summary>
        private Rect GetMainViewFieldLogicalRect()
        {
            Rect p = GetMainViewFieldPixelRect();
            float s = Mathf.Max(0.01f, uiScale);
            return new Rect(p.x / s, p.y / s, p.width / s, p.height / s);
        }

        /// <summary>左面板与主视图之间的拖动分隔条</summary>
        private Rect GetLeftSplitterRect()
        {
            Rect panel = GetLeftPanelRect();
            return new Rect(panel.xMax, panel.y, LayoutGap, panel.height);
        }

        /// <summary>主视图与时间轴之间的拖动分隔条</summary>
        private Rect GetTimelineSplitterRect()
        {
            Rect view = GetMainViewLogicalRect();
            Rect tl = GetTimelineRect();
            float h = Mathf.Max(8f, tl.y - view.yMax);
            return new Rect(view.x, view.yMax, view.width, h);
        }

        #endregion

        #region OnGUI

        void OnGUI()
        {
            // 自适应缩放：以 1080p 为基准
            float baseWidth = 1920f;
            uiScale = Mathf.Clamp(Screen.width / baseWidth, 1f, 1.8f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

            Styles.EnsureStyles();

            HandlePanelResize();

            // 整屏兜底底色：第一件事就把它铺满。
            // 3D 预览已经改成渲染到贴图后再画进主视图，所以这里可以放心整屏铺底，
            // 不会再出现「上下两条黑边」那种没画到的区域。
            Styles.DrawBackdrop(new Rect(0, 0, ViewW, ViewH));

            DrawToolbar();
            DrawLeftPanel();
            DrawMainView();
            DrawSplitters();
            DrawTimeline();
            DrawStatusBar();

            HandleKeyboardShortcuts();

            // 文件浏览器（最上层）
            if (fileDialog != null && fileDialog.IsOpen)
                fileDialog.Draw(new Rect(0, 0, ViewW, ViewH));
        }

        #endregion

        #region 面板缩放

        /// <summary>拖动分隔条自由调整左面板宽度与时间轴高度</summary>
        private void HandlePanelResize()
        {
            Event e = Event.current;
            if (e == null) return;
            // 文件浏览器是模态的，开着的时候不要响应分隔条拖动
            if (fileDialog != null && fileDialog.IsOpen) return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (GetLeftSplitterRect().Contains(e.mousePosition))
                {
                    draggingLeftSplitter = true;
                    // 记下抓取时鼠标相对面板右边缘的偏移，拖动时面板才不会跳
                    leftSplitterGrab = e.mousePosition.x - (LayoutGap + leftPanelWidth);
                    e.Use();
                    return;
                }
                if (GetTimelineSplitterRect().Contains(e.mousePosition))
                {
                    draggingTimelineSplitter = true;
                    timelineSplitterGrab = e.mousePosition.y - (ViewH - LayoutStatusH - LayoutGap - timelineHeight);
                    e.Use();
                    return;
                }
            }

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (draggingLeftSplitter)
                {
                    float maxW = Mathf.Max(260f, ViewW * 0.55f);
                    leftPanelWidth = Mathf.Clamp(e.mousePosition.x - LayoutGap - leftSplitterGrab, 220f, maxW);
                    e.Use();
                    return;
                }
                if (draggingTimelineSplitter)
                {
                    float minH = 130f;
                    float maxH = Mathf.Max(minH + 10f, ViewH - LayoutToolbarH - LayoutStatusH - 170f);
                    float bottom = ViewH - LayoutStatusH - LayoutGap;
                    timelineHeight = Mathf.Clamp(bottom - (e.mousePosition.y - timelineSplitterGrab), minH, maxH);
                    e.Use();
                    return;
                }
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (draggingLeftSplitter || draggingTimelineSplitter)
                {
                    draggingLeftSplitter = false;
                    draggingTimelineSplitter = false;
                    e.Use();
                }
            }
        }

        private void DrawSplitters()
        {
            // 只在分隔条所在位置画一条细线，提示可以拖动
            Rect left = GetLeftSplitterRect();
            Rect tl = GetTimelineSplitterRect();

            bool leftHot = left.Contains(Event.current != null ? Event.current.mousePosition : Vector2.zero);
            bool tlHot = tl.Contains(Event.current != null ? Event.current.mousePosition : Vector2.zero);

            GUI.color = draggingLeftSplitter || leftHot
                ? new Color(0.32f, 0.78f, 1f, 0.6f)
                : new Color(1f, 1f, 1f, 0.07f);
            GUI.DrawTexture(new Rect(left.center.x - 1f, left.y + 24f, 2f, Mathf.Max(30f, left.height - 48f)),
                Texture2D.whiteTexture);

            GUI.color = draggingTimelineSplitter || tlHot
                ? new Color(0.32f, 0.78f, 1f, 0.6f)
                : new Color(1f, 1f, 1f, 0.07f);
            GUI.DrawTexture(new Rect(tl.x + 60f, tl.center.y - 1f, Mathf.Max(40f, tl.width - 120f), 2f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        #endregion

        #region 工具栏

        private void DrawToolbar()
        {
            Rect bar = GetToolbarRect();
            Styles.DrawToolbarBg(bar);
            // 底部一条高光细线，把工具栏和内容区分开，而不是靠一条生硬的黑边
            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            GUI.DrawTexture(new Rect(bar.x, bar.yMax - 1, bar.width, 1), Texture2D.whiteTexture);
            GUI.color = Styles.Line;
            GUI.DrawTexture(new Rect(bar.x, bar.yMax, bar.width, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 统一控件高度并整体垂直居中，避免文字与按钮错位
            const float contentH = 36f;
            float contentY = bar.y + Mathf.Round((bar.height - contentH) * 0.5f);

            GUILayout.BeginArea(new Rect(bar.x + 14, contentY, bar.width - 28, contentH));
            GUILayout.BeginHorizontal();

            // 品牌
            GUILayout.Label("Thart", Styles.TitleLabel, GUILayout.Width(56), GUILayout.Height(contentH));
            var accentRect = GUILayoutUtility.GetRect(3, contentH, GUILayout.Width(3), GUILayout.Height(contentH));
            GUI.color = Styles.Accent;
            GUI.DrawTexture(new Rect(accentRect.x, accentRect.y + 6f, 3f, contentH - 12f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.Space(10);

            // 模式分段控件
            DrawModeSegment("录制", ThartEditorMode.Recording, contentH);
            DrawModeSegment("编辑", ThartEditorMode.Editing, contentH);
            DrawModeSegment("预览", ThartEditorMode.Preview, contentH);

            GUILayout.Space(18);

            // 文件
            if (GUILayout.Button("新建", Styles.GhostButton, GUILayout.Height(contentH), GUILayout.Width(58)))
                NewChart();

            if (GUILayout.Button("打开", Styles.GhostButton, GUILayout.Height(contentH), GUILayout.Width(58)))
                OpenChartPackage();

            if (GUILayout.Button("保存", Styles.GhostButton, GUILayout.Height(contentH), GUILayout.Width(58)))
                SaveChartPackage();

            if (GUILayout.Button("另存为", Styles.GhostButton, GUILayout.Height(contentH), GUILayout.Width(72)))
                SaveChartPackageAs();

            GUILayout.Space(10);
            if (GUILayout.Button("撤销", Styles.GhostButton, GUILayout.Height(contentH), GUILayout.Width(58))) Undo();
            if (GUILayout.Button("重做", Styles.GhostButton, GUILayout.Height(contentH), GUILayout.Width(58))) Redo();

            GUILayout.Space(10);
            if (GUILayout.Button("导入音频", Styles.GhostButton, GUILayout.Height(contentH), GUILayout.Width(86)))
                OpenAudioPicker();

            GUILayout.FlexibleSpace();

            // 播放控制
            if (GUILayout.Button(playing ? "暂停" : "播放", Styles.RoundPlayButton, GUILayout.Height(contentH), GUILayout.Width(72)))
                TogglePlayback();
            if (GUILayout.Button("回到开头", Styles.GhostButton, GUILayout.Height(contentH), GUILayout.Width(82)))
            {
                StopPlayback();
                songTime = 0;
            }

            GUILayout.Space(8);
            var timeRect = GUILayoutUtility.GetRect(148, contentH, GUILayout.Width(148), GUILayout.Height(contentH));
            Styles.DrawRoundSm(timeRect, new Color(1f, 1f, 1f, 0.05f));
            GUI.Label(timeRect, FormatTime(songTime) + " / " + FormatTime(GetDuration()), Styles.TimeReadout);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawModeSegment(string label, ThartEditorMode target, float h)
        {
            bool on = mode == target;
            if (GUILayout.Button(label, on ? Styles.SegOn : Styles.SegOff, GUILayout.Height(h), GUILayout.Width(68)))
                mode = target;
        }

        #endregion

        #region 左侧面板

        private void DrawLeftPanel()
        {
            Rect panel = GetLeftPanelRect();
            Styles.DrawCard(panel);

            GUILayout.BeginArea(new Rect(panel.x + 14, panel.y + 12, panel.width - 28, panel.height - 24));
            // 滚动条样式只能从 GUI.skin 取，传 style 参数换不掉那条默认灰条，
            // 所以只在滚动视图这一段临时换成自定义皮肤，其余控件不受影响。
            var originalSkin = GUI.skin;
            if (Styles.ScrollSkin != null) GUI.skin = Styles.ScrollSkin;
            leftPanelScroll = GUILayout.BeginScrollView(leftPanelScroll, GUILayout.ExpandHeight(true));

            if (mode == ThartEditorMode.Preview) DrawPreviewInfoPanel();
            else if (mode == ThartEditorMode.Recording) DrawRecordingPanel();
            else DrawEditingPanel();

            GUILayout.Space(20);
            GUILayout.EndScrollView();
            GUI.skin = originalSkin;
            GUILayout.EndArea();
        }

        /// <summary>小节标题：左侧一条强调竖线</summary>
        private void SectionHeader(string title)
        {
            GUILayout.Space(12);
            Rect r = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
            GUI.color = Styles.Accent;
            GUI.DrawTexture(new Rect(r.x, r.y + 4, 3, 14), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 10, r.y, r.width - 10, 22), title, Styles.Heading);
            GUILayout.Space(6);
        }

        private void InfoRow(string key, string value, Color? valueColor = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(key, Styles.SmallLabel, GUILayout.Width(86));
            var old = GUI.color;
            GUI.color = valueColor ?? Styles.Text;
            GUILayout.Label(value, Styles.Label);
            GUI.color = old;
            GUILayout.EndHorizontal();
        }

        private void DrawRecordingPanel()
        {
            SectionHeader("连接");

            int clientCount = server != null ? server.ClientCount : 0;
            bool linked = clientCount > 0;
            var chip = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            Styles.DrawRoundSm(chip, linked ? new Color(0.13f, 0.32f, 0.21f) : new Color(1f, 1f, 1f, 0.05f));
            Styles.DrawRoundSm(new Rect(chip.x + 12, chip.y + chip.height / 2f - 5f, 10, 10),
                linked ? Styles.Ok : Styles.DimText);
            GUI.Label(new Rect(chip.x + 30, chip.y, chip.width - 40, 30),
                server == null ? "服务器未启动" : (linked ? "平板已连接（" + clientCount + " 台）" : "等待平板连接..."), Styles.Label);
            if (server != null)
            {
                GUILayout.Space(4);
                GUILayout.Label("   地址 " + ThartNetworkUtils.GetLocalIPAddress() + " : " + serverPort, Styles.SmallLabel);
            }

            SectionHeader("音频");
            if (!HasAudio)
            {
                var warn = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
                Styles.DrawRoundSm(warn, new Color(0.34f, 0.24f, 0.1f));
                GUI.Label(new Rect(warn.x + 12, warn.y, warn.width - 20, 32), "尚未导入音频，导入后才能录制", Styles.SmallLabel);
            }
            else
            {
                InfoRow("文件", audioFileName);
                InfoRow("时长", FormatDuration(currentAudio.length)
                    + "   " + (audioFileBytes / 1024f / 1024f).ToString("F1") + " MB");

                string pushState;
                Color pushColor = Styles.Warn;
                if (audioPushing) pushState = "推送中 " + Mathf.RoundToInt(audioPushProgress * 100f) + "%";
                else if (IsAudioPushedToTablet) { pushState = "平板已就绪"; pushColor = Styles.Ok; }
                else if (clientCount == 0) pushState = "等待平板连接";
                else pushState = "尚未推送";
                InfoRow("平板", pushState, pushColor);

                if (audioPushing)
                {
                    Rect prog = GUILayoutUtility.GetRect(0, 6, GUILayout.ExpandWidth(true));
                    Styles.DrawRoundSm(prog, new Color(1f, 1f, 1f, 0.08f));
                    float w = Mathf.Max(4f, prog.width * Mathf.Clamp01(audioPushProgress));
                    Styles.DrawRoundSm(new Rect(prog.x, prog.y, w, prog.height), Styles.Accent);
                }
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(HasAudio ? "换一首" : "导入音频", Styles.Button, GUILayout.Height(34)))
                OpenAudioPicker();
            if (HasAudio && GUILayout.Button("重新推送", Styles.Button, GUILayout.Height(34)))
                StartPushAudioToTablet(false);
            GUILayout.EndHorizontal();

            SectionHeader("录制");

            if (recordingState == RecordingState.Recording)
            {
                if (GUILayout.Button("停止录制", Styles.DangerButton, GUILayout.Height(48)))
                    StopRecording();

                GUILayout.Space(8);
                if (inCountdown)
                {
                    float remain = Mathf.Max(0f, countdownSeconds - (float)recordingDuration);
                    InfoRow("倒计时", Mathf.CeilToInt(remain) + " 秒（平板正在播放音频）", Styles.Warn);
                }
                else
                {
                    InfoRow("已录", FormatTime(recordingDuration));
                }
                // 实时收到的帧数：能直观看到平板数据有没有过来
                bool live = Time.realtimeSinceStartup - lastSampleWallClock < 1.5f;
                InfoRow("收到触控", samplesThisSession + " 帧",
                    live ? Styles.Ok : (samplesThisSession > 0 ? Styles.Warn : Styles.DimText));
            }
            else if (recordingState == RecordingState.Stopping)
            {
                GUILayout.Button("等待平板回传数据...", Styles.Button, GUILayout.Height(48));
                GUILayout.Space(8);
                InfoRow("收到触控", samplesThisSession + " 帧", Styles.Warn);
            }
            else if (armingTablet)
            {
                GUILayout.Button("正在与平板同步...", Styles.Button, GUILayout.Height(48));
            }
            else
            {
                bool canStart = HasAudio && clientCount > 0;
                GUI.enabled = canStart;
                if (GUILayout.Button("开始录制", canStart ? Styles.BigButton : Styles.Button, GUILayout.Height(48)))
                    StartRecording();
                GUI.enabled = true;

                if (!canStart)
                {
                    GUILayout.Space(6);
                    GUILayout.Label(clientCount == 0 ? "等待平板连接" : "请先导入音频", Styles.SmallLabel);
                }
                else
                {
                    GUILayout.Space(6);
                    GUILayout.Label("快捷键 R 也可以开始 / 停止", Styles.SmallLabel);
                }
            }

            SectionHeader("倒计时与播放");
            GUILayout.Label("开始前倒数 " + countdownSeconds.ToString("F0") + " 秒", Styles.Label);
            countdownSeconds = Mathf.Round(GUILayout.HorizontalSlider(countdownSeconds, 0f, 8f));
            GUILayout.Label("倒计时期间平板端同步播放音频", Styles.SmallLabel);
            GUILayout.Space(4);
            playAudioOnDesktop = GUILayout.Toggle(playAudioOnDesktop, "  电脑端同时播放", Styles.Foldout);

            SectionHeader("吸附网格");
            autoSnapAfterRecording = GUILayout.Toggle(autoSnapAfterRecording, "  录制结束后自动吸附", Styles.Foldout);
            GUILayout.Space(6);
            GUILayout.Label("网格密度: " + ThartChartBuilder.GridLabel(snapDivisor), Styles.Label);
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            foreach (var d in ThartChartBuilder.GridPresets)
            {
                bool on = snapDivisor == d;
                if (GUILayout.Button(ThartChartBuilder.GridLabel(d), on ? Styles.PillOn : Styles.PillOff, GUILayout.Height(28)))
                    snapDivisor = d;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("1/n 拍 n =", Styles.SmallLabel, GUILayout.Width(78));
            customGridInput = GUILayout.TextField(customGridInput, Styles.TextField, GUILayout.Width(56), GUILayout.Height(26));
            if (GUILayout.Button("应用", Styles.SmallButton, GUILayout.Width(52), GUILayout.Height(26)))
            {
                int n;
                if (int.TryParse(customGridInput.Trim(), out n) && n > 0 && n <= 192)
                {
                    snapDivisor = n;
                    UpdateStatus("网格密度已设为 1/" + n + " 拍");
                }
                else UpdateStatus("请输入 1~192 之间的整数");
            }
            if (GUILayout.Button("关闭", Styles.SmallButton, GUILayout.Width(52), GUILayout.Height(26)))
                snapDivisor = 0;
            GUILayout.EndHorizontal();

            SectionHeader("布局与转换");
            GUILayout.Label("铺面没有固定轨道：音符的位置来自平板上真实的触控位置。"
                + "录入框与游玩界面都固定 16:9，所以换屏幕比例也不会变形。", Styles.SmallLabel);
            GUILayout.Space(6);

            conversionConfig.autoColumns = GUILayout.Toggle(conversionConfig.autoColumns,
                "  按触控位置自动分列（二维：上下位置也算）", Styles.Foldout);

            if (conversionConfig.autoColumns)
            {
                GUILayout.Space(4);
                GUILayout.Label("最大列数 " + conversionConfig.maxColumns, Styles.Label);
                conversionConfig.maxColumns = Mathf.RoundToInt(
                    GUILayout.HorizontalSlider(conversionConfig.maxColumns, 1, 32));

                GUILayout.Label("分列合并阈值 " + conversionConfig.columnMergePercent.ToString("F1") + "%", Styles.Label);
                conversionConfig.columnMergePercent =
                    GUILayout.HorizontalSlider(conversionConfig.columnMergePercent, 0.5f, 15f);
            }
            else
            {
                GUILayout.Space(4);
                GUILayout.Label("固定列数 " + conversionConfig.fixedColumnCount, Styles.Label);
                conversionConfig.fixedColumnCount = Mathf.RoundToInt(
                    GUILayout.HorizontalSlider(conversionConfig.fixedColumnCount, 1, 16));
            }

            GUILayout.Space(6);
            GUILayout.Label("录入框纵向跨度 " + conversionConfig.fieldHeight.ToString("F1"), Styles.Label);
            conversionConfig.fieldHeight = GUILayout.HorizontalSlider(conversionConfig.fieldHeight, 2f, 16f);
            GUILayout.Label("横向跨度 " + conversionConfig.FieldWidth.ToString("F1")
                + "（= 纵向 × 16/9，固定比例不能单独改）", Styles.SmallLabel);

            showRecordingPanel = GUILayout.Toggle(showRecordingPanel, "  显示高级参数", Styles.Foldout);
            if (showRecordingPanel)
            {
                GUILayout.Space(4);
                GUILayout.Label("Tap 最大时长 " + conversionConfig.tapMaxDuration.ToString("F2") + "s", Styles.Label);
                conversionConfig.tapMaxDuration = GUILayout.HorizontalSlider(conversionConfig.tapMaxDuration, 0.05f, 0.3f);

                GUILayout.Label(conversionConfig.minPressure <= 0f
                    ? "最小压力 关闭"
                    : "最小压力 " + conversionConfig.minPressure.ToString("F2"), Styles.Label);
                conversionConfig.minPressure = GUILayout.HorizontalSlider(conversionConfig.minPressure, 0f, 0.5f);
                GUILayout.Label("设备压力恒定时会自动跳过过滤（多数安卓平板都恒定）", Styles.SmallLabel);

                GUILayout.Label("合并窗口 " + conversionConfig.mergeWindow.ToString("F3") + "s", Styles.Label);
                conversionConfig.mergeWindow = GUILayout.HorizontalSlider(conversionConfig.mergeWindow, 0f, 0.1f);
            }

            GUILayout.Space(8);
            if (GUILayout.Button("重新转换并应用", Styles.Button, GUILayout.Height(32)))
            {
                if (currentRecording != null) { PushUndo(); ConvertRecordingToNotes(); }
                else UpdateStatus("还没有录制数据");
            }
            if (GUILayout.Button("保存触控数据", Styles.Button, GUILayout.Height(32)))
                SaveTouchRecording();

            SectionHeader("转换结果");
            InfoRow("音符总数", convertedNotes.Count.ToString());
            int tapCount = 0, dragCount = 0;
            foreach (var n in convertedNotes)
            {
                if (n.noteType == "tap") tapCount++;
                else dragCount++;
            }
            InfoRow("Tap", tapCount.ToString());
            InfoRow("Drag", dragCount.ToString());
            InfoRow("列数", (convertedLayout != null ? convertedLayout.Count : 0).ToString());
        }

        private void DrawPreviewInfoPanel()
        {
            SectionHeader("预览");
            GUILayout.Label("3D 铺面预览，按空格播放或拖动时间轴播放头查看", Styles.SmallLabel);
            GUILayout.Space(10);

            InfoRow("谱面", string.IsNullOrEmpty(chart.title) ? "Untitled" : chart.title);
            InfoRow("BPM", chart.tempos != null && chart.tempos.Length > 0 ? chart.tempos[0].bpm.ToString("F0") : "-");
            InfoRow("列数", pathCount.ToString());
            InfoRow("音符数", (chart.notes != null ? chart.notes.Length : 0).ToString());
            InfoRow("时长", FormatTime(GetDuration()));
            InfoRow("当前", FormatTime(songTime), Styles.Accent);

            SectionHeader("相机");
            GUILayout.Label("预览相机由代码生成，跟随播放头推进", Styles.SmallLabel);

            SectionHeader("播放");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(playing ? "暂停" : "播放", Styles.AccentButton, GUILayout.Height(36)))
                TogglePlayback();
            if (GUILayout.Button("回到开头", Styles.Button, GUILayout.Height(36)))
            {
                StopPlayback();
                songTime = 0;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("音符流速 " + scrollSpeed.ToString("F1") + "x", Styles.Label);
            scrollSpeed = GUILayout.HorizontalSlider(scrollSpeed, 2f, 48f);
            GUILayout.Label("流速同时影响 3D 预览里音符推进的速度", Styles.SmallLabel);
        }

        private void DrawEditingPanel()
        {
            SectionHeader("音符");
            showNotePanel = GUILayout.Toggle(showNotePanel, "  显示音符面板", Styles.Foldout);
            if (showNotePanel)
            {
                GUILayout.Space(6);
                GUILayout.Label("选中列（位置来自触控）", Styles.Label);
                selectedPathIndex = Mathf.Clamp(selectedPathIndex, 0, Mathf.Max(0, pathCount - 1));

                // 列比较多时换行排列，避免挤出面板
                int perRow = Mathf.Max(1, Mathf.FloorToInt((GetLeftPanelRect().width - 60f) / 48f));
                for (int i = 0; i < pathCount; i++)
                {
                    if (i % perRow == 0)
                    {
                        if (i > 0) GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }
                    if (GUILayout.Button("P" + (i + 1), selectedPathIndex == i ? Styles.PillOn : Styles.PillOff,
                        GUILayout.Height(28), GUILayout.Width(44)))
                        selectedPathIndex = i;
                }
                if (pathCount > 0) GUILayout.EndHorizontal();

                GUILayout.Space(10);
                if (selectedNoteIndex >= 0 && chart.notes != null && selectedNoteIndex < chart.notes.Length)
                {
                    var note = chart.notes[selectedNoteIndex];
                    if (selectedNoteIds.Count > 1)
                        GUILayout.Label("已选中 " + selectedNoteIds.Count + " 个音符（主选中如下）", Styles.Label);
                    InfoRow("ID", note.id);
                    InfoRow("类型", note.action);
                    InfoRow("列", note.pathId);
                    float beat = note.tick / (float)chart.ticksPerBeat;
                    InfoRow("Beat", beat.ToString("F3"));
                    InfoRow("时间", tempo != null ? FormatTime(tempo.SecondsAtBeat(beat)) : "-");

                    GUILayout.Space(6);
                    note.protectedNote = GUILayout.Toggle(note.protectedNote, "  保护音符", Styles.Foldout);

                    GUILayout.Space(6);
                    if (GUILayout.Button("删除音符", Styles.DangerButton, GUILayout.Height(32)))
                        DeleteSelectedNotes();
                }
                else
                {
                    GUILayout.Label("在时间轴行内左键点空白即可新建音符", Styles.SmallLabel);
                    GUILayout.Label("左键 选中 · Shift+左键 多选 · 右键 删除 · Ctrl+A 全选", Styles.SmallLabel);
                }

                GUILayout.Space(10);
                GUILayout.Label("添加音符", Styles.SubHeading);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Tap", Styles.Button, GUILayout.Height(32)))
                    AddNoteAtPlayhead("tap");
                if (GUILayout.Button("+ Drag", Styles.Button, GUILayout.Height(32)))
                    AddNoteAtPlayhead("drag");
                GUILayout.EndHorizontal();

                GUILayout.Space(8);
                if (GUILayout.Button(selectedNoteIds.Count > 1
                        ? "删除选中（" + selectedNoteIds.Count + " 个）"
                        : "删除选中", Styles.Button, GUILayout.Height(30)))
                    DeleteSelectedNotes();
            }

            SectionHeader("时间与速度");
            showTimingPanel = GUILayout.Toggle(showTimingPanel, "  显示 BPM / 流速", Styles.Foldout);
            if (showTimingPanel)
            {
                GUILayout.Space(6);
                if (chart.tempos != null && chart.tempos.Length > 0)
                {
                    GUILayout.Label("BPM " + chart.tempos[0].bpm, Styles.Label);
                    float newBpm = GUILayout.HorizontalSlider(chart.tempos[0].bpm, 60, 240);
                    if (Mathf.Abs(newBpm - chart.tempos[0].bpm) > 0.1f)
                    {
                        PushUndo();
                        chart.tempos[0].bpm = Mathf.Round(newBpm * 100f) / 100f;
                        RebuildTempoMap();
                    }
                }

                GUILayout.Space(8);
                GUILayout.Label("音符流速 " + scrollSpeed.ToString("F1") + "x", Styles.Label);
                scrollSpeed = GUILayout.HorizontalSlider(scrollSpeed, 0.5f, 48f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("4x", Styles.SmallButton)) scrollSpeed = 4;
                if (GUILayout.Button("8x", Styles.SmallButton)) scrollSpeed = 8;
                if (GUILayout.Button("12x", Styles.SmallButton)) scrollSpeed = 12;
                if (GUILayout.Button("24x", Styles.SmallButton)) scrollSpeed = 24;
                if (GUILayout.Button("48x", Styles.SmallButton)) scrollSpeed = 48;
                GUILayout.EndHorizontal();
            }

            SectionHeader("谱面信息");
            // 从外部加载的谱面可能没有 title/author，直接喂给 TextField 会抛异常
            chart.title = GUILayout.TextField(chart.title ?? "", Styles.TextField, GUILayout.Height(30));
            GUILayout.Space(6);
            chart.author = GUILayout.TextField(chart.author ?? "", Styles.TextField, GUILayout.Height(30));
            GUILayout.Space(8);
            InfoRow("列数", pathCount.ToString());
            InfoRow("音符数", (chart.notes != null ? chart.notes.Length : 0).ToString());
            InfoRow("时长", FormatTime(GetDuration()));
        }

        #endregion

        #region 主视图

        private void DrawMainView()
        {
            Rect view = GetMainViewLogicalRect();

            if (mode == ThartEditorMode.Preview)
            {
                // 预览画面由预览摄像机渲染到一张 16:9 贴图，这里贴进主视图区域。
                // 贴图还没准备好时画一张卡片兜底，避免露出没画到的区域。
                Styles.DrawCard(view);
                var preview = PreviewTexture;
                if (preview != null)
                {
                    // 只贴进 16:9 的那块区域：游玩界面是 16:9，预览不能按窗口比例被拉扁
                    GUI.DrawTexture(GetMainViewFieldLogicalRect(), preview, ScaleMode.StretchToFill, false);
                }

                DrawPreviewOverlay(view);
                return;
            }

            Styles.DrawCard(view);
            GUILayout.BeginArea(new Rect(view.x + 16, view.y + 14, view.width - 32, view.height - 28));

            if (mode == ThartEditorMode.Recording) DrawRecordingView(view.width - 32, view.height - 28);
            else DrawEditingView(view.width - 32, view.height - 28);

            GUILayout.EndArea();
        }

        private void DrawPreviewOverlay(Rect view)
        {
            // 顶部信息条
            var info = new Rect(view.x, view.y, view.width, 34f);
            Styles.DrawRoundSm(info, new Color(0f, 0f, 0f, 0.42f));
            GUI.Label(new Rect(info.x + 14, info.y, 380, 34),
                "3D 预览   " + FormatTime(songTime) + " / " + FormatTime(GetDuration()), Styles.TimeReadout);

            int visibleNotes = 0;
            if (chart.notes != null && tempo != null)
            {
                float approach = Mathf.Max(0.6f, chart.approachSeconds);
                foreach (var n in chart.notes)
                {
                    double t = tempo.SecondsAtBeat(n.tick / (float)chart.ticksPerBeat);
                    double dt = t - songTime;
                    if (dt <= approach && dt >= -0.15) visibleNotes++;
                }
            }
            GUI.Label(new Rect(info.xMax - 300, info.y, 286, 34),
                "屏幕上 " + visibleNotes + " 个音符   ·   总 " + (chart.notes != null ? chart.notes.Length : 0), Styles.SmallLabel);

            // 空闲提示
            if (!playing)
            {
                var hint = new Rect(view.center.x - 190, view.yMax - 70, 380, 34);
                Styles.DrawRoundSm(hint, new Color(0f, 0f, 0f, 0.42f));
                GUI.Label(hint, "空格播放 / 暂停 · 拖动下方时间轴跳转", Styles.HintLabel);
            }
        }

        private void DrawRecordingView(float width, float height)
        {
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            string bigTitle;
            if (recordingState == RecordingState.Recording)
                bigTitle = inCountdown ? "准备开始" : "录制中";
            else if (recordingState == RecordingState.Stopping) bigTitle = "等待数据回传";
            else if (armingTablet) bigTitle = "与平板同步中";
            else bigTitle = "准备录制";

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(bigTitle, Styles.BigTitleLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (recordingState == RecordingState.Recording && inCountdown)
            {
                int remain = Mathf.Max(1, Mathf.CeilToInt(countdownSeconds - (float)recordingDuration));
                GUI.color = Styles.Warn;
                GUILayout.Label(remain.ToString(), Styles.BigCountdownLabel);
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label(FormatTime(recordingState == RecordingState.Recording ? recordingDuration : 0), Styles.BigTimeLabel);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            string hint;
            if (recordingState == RecordingState.Recording)
                hint = inCountdown ? "音频已在平板端播放，倒计时结束后开始记录" : "请在平板上的 16:9 录入框内跟随音乐点击";
            else if (recordingState == RecordingState.Stopping)
                hint = "正在等平板把完整触控数据发回来";
            else if (!HasAudio) hint = "请先在左侧「音频」面板导入音频";
            else if (server == null || server.ClientCount == 0) hint = "等待平板连接";
            else hint = "音频已就绪，点击「开始录制」";

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(hint, Styles.HintLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            // 录入框内的触控实时可视化（16:9，看到的就是真实录入范围）
            DrawLiveTouchField(width, height);
        }

        /// <summary>
        /// 平板上录到的触控点，按录入框内的百分比原样画出来 —— 没有轨道，只有位置。
        /// 画的就是那个 16:9 录入框（框外本来就没数据），所以这里看到的
        /// 比例与铺面、游玩界面完全一致。
        /// </summary>
        private void DrawLiveTouchField(float width, float height)
        {
            float fieldH = Mathf.Clamp(height * 0.34f, 110f, 220f);
            float fieldW = fieldH * TouchToNoteConfig.FieldAspect;
            if (fieldW > width)
            {
                fieldW = width;
                fieldH = fieldW / TouchToNoteConfig.FieldAspect;
            }
            var field = new Rect((width - fieldW) * 0.5f, height - fieldH - 14f, fieldW, fieldH);

            Styles.DrawRoundSm(field, new Color(1f, 1f, 1f, 0.045f));
            GUI.color = new Color(1f, 1f, 1f, 0.09f);
            GUI.DrawTexture(new Rect(field.x, field.center.y, field.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(field.center.x, field.y, 1f, field.height), Texture2D.whiteTexture);
            GUI.color = new Color(0.32f, 0.78f, 1f, 0.35f);
            GUI.DrawTexture(new Rect(field.x, field.y, field.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(field.x, field.yMax - 2f, field.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(field.x, field.y, 2f, field.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(field.xMax - 2f, field.y, 2f, field.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 帧尺寸优先用包里的录制数据，还没有数据时用平板握手时报上来的值
            int frameW = currentRecording != null && currentRecording.HasCaptureFrame
                ? currentRecording.frameWidth : tabletFrameWidth;
            int frameH = currentRecording != null && currentRecording.HasCaptureFrame
                ? currentRecording.frameHeight : tabletFrameHeight;
            int screenW = currentRecording != null && currentRecording.screenWidth > 0
                ? currentRecording.screenWidth : tabletScreenWidth;
            int screenH = currentRecording != null && currentRecording.screenHeight > 0
                ? currentRecording.screenHeight : tabletScreenHeight;

            GUI.Label(new Rect(field.x, field.y - 20f, field.width, 18f),
                "平板录入框 16:9" + (frameW > 0 && frameH > 0
                    ? "（" + frameW + " × " + frameH
                      + (screenW > 0 ? "，屏幕 " + screenW + " × " + screenH : "") + "）"
                    : "（等待数据）"),
                Styles.SmallLabel);

            if (liveSamples.Count == 0) return;

            var last = liveSamples[liveSamples.Count - 1];
            if (last == null || last.touches == null) return;

            float sw = frameW > 0 ? frameW : 1920f;
            float sh = frameH > 0 ? frameH : 1080f;

            foreach (var touch in last.touches)
            {
                float xp = touch.xPercent > 0f || touch.yPercent > 0f ? touch.xPercent : touch.x / sw * 100f;
                float yp = touch.xPercent > 0f || touch.yPercent > 0f ? touch.yPercent : touch.y / sh * 100f;
                float dx = field.x + Mathf.Clamp01(xp / 100f) * field.width;
                float dy = field.y + Mathf.Clamp01(yp / 100f) * field.height;

                GUI.color = new Color(1f, 0.42f, 0.45f, 0.9f);
                GUI.DrawTexture(new Rect(dx - 11f, dy - 11f, 22f, 22f), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// 编辑视图 = 俯视的 16:9 铺面平面图。
        ///
        /// 这是唯一能如实反映「手指按在哪里」的画法：列标记和音符都画在
        /// 它真实的世界 (x, y) 上，所以录入时按的四个角在编辑器里就是四个角。
        /// 音符用一圈随时间收缩的方框表示提前量：圈最大 = 刚进入视野，收到刚好
        /// 套住音符 = 正好是该按的时刻。
        /// </summary>
        private void DrawEditingView(float width, float height)
        {
            var view = new Rect(0, 0, width, height);
            // 顶部一行信息、底部一行跨度说明，中间的区域放 16:9 铺面
            var field = AspectFieldRect(new Rect(view.x, view.y + 24f, view.width, Mathf.Max(40f, view.height - 46f)));

            // 铺面底板与 16:9 边框
            GUI.color = new Color(0.02f, 0.024f, 0.036f, 1f);
            GUI.DrawTexture(field, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawFieldGuides(field);

            float noteSize = Mathf.Clamp(field.height * 0.07f, 9f, 26f);
            float unit = conversionConfig.fieldHeight > 0.1f ? field.height / conversionConfig.fieldHeight : 1f;

            // 列标记：画在真实位置上（四角录入 → 四角标记）
            if (chart != null && chart.paths != null)
            {
                for (int i = 0; i < chart.paths.Length; i++)
                {
                    Vector2 p = PathFieldPos(chart.paths[i].id, field);
                    bool active = i == selectedPathIndex;
                    float size = Mathf.Max(14f, noteSize * 1.1f);
                    var marker = new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size);

                    Styles.DrawRoundSm(marker, active
                        ? new Color(0.32f, 0.78f, 1f, 0.85f)
                        : new Color(0.32f, 0.78f, 1f, 0.35f));
                    GUI.Label(new Rect(p.x - 34f, p.y - size * 0.5f - 20f, 68f, 18f),
                        "P" + (i + 1), Styles.CenterLabel);
                }
            }

            // 音符：按列位置画收缩圈
            if (chart != null && chart.notes != null && tempo != null && chart.notes.Length > 0)
            {
                float approachTime = chart.approachSeconds * 8f / Mathf.Max(0.5f, scrollSpeed);

                for (int i = 0; i < chart.notes.Length; i++)
                {
                    var note = chart.notes[i];
                    float beat = note.tick / (float)chart.ticksPerBeat;
                    double noteTime = tempo.SecondsAtBeat(beat);
                    double timeUntilHit = noteTime - songTime;
                    if (timeUntilHit > approachTime || timeUntilHit < -0.2) continue;

                    Vector2 p = PathFieldPos(note.pathId, field);
                    float progress = Mathf.Clamp01(1f - (float)(timeUntilHit / approachTime));
                    float side = Mathf.Lerp(noteSize * 2.6f, noteSize, progress * progress);

                    Color c = note.protectedNote ? Styles.NoteProtected
                        : (note.action == "tap" ? Styles.NoteTap : Styles.NoteDrag);

                    var ring = new Rect(p.x - side * 0.5f, p.y - side * 0.5f, side, side);
                    if (IsNoteSelected(i))
                        Styles.DrawRoundSm(new Rect(ring.x - 5f, ring.y - 5f, ring.width + 10f, ring.height + 10f),
                            new Color(1f, 0.85f, 0.3f, 0.9f));

                    DrawRing(ring, c, Mathf.Clamp(side * 0.12f, 1.5f, 4f));

                    // 判定时刻的实心芯：越接近越亮，方便对时间
                    float core = Mathf.Lerp(0.25f, 1f, progress);
                    Styles.DrawRoundSm(new Rect(p.x - noteSize * 0.25f, p.y - noteSize * 0.25f,
                        noteSize * 0.5f, noteSize * 0.5f), new Color(c.r, c.g, c.b, core));
                }
            }

            // 顶部：时间与说明
            GUI.Label(new Rect(0, 2, width - 210, 24),
                "俯视 16:9 铺面 · 列与音符就是录入时的真实位置（圈越大 = 离判定越远）", Styles.SmallLabel);
            GUI.Label(new Rect(width - 200, 2, 190, 24),
                FormatTime(songTime) + " / " + FormatTime(GetDuration()), Styles.RightLabel);

            // 底部：真实世界跨度，方便判断铺面大小
            GUI.Label(new Rect(0, height - 18f, width, 18f),
                "世界范围 " + conversionConfig.FieldWidth.ToString("F1") + " × "
                + conversionConfig.fieldHeight.ToString("F1") + "（16:9）"
                + "   ·   1 单位 ≈ " + unit.ToString("F1") + "px", Styles.SmallLabel);
        }

        /// <summary>在给定区域里取最大的 16:9 矩形：录入框与铺面永远是 16:9</summary>
        private static Rect AspectFieldRect(Rect area)
        {
            float fw = area.width;
            float fh = fw / TouchToNoteConfig.FieldAspect;
            if (fh > area.height)
            {
                fh = area.height;
                fw = fh * TouchToNoteConfig.FieldAspect;
            }
            return new Rect(area.x + (area.width - fw) * 0.5f, area.y + (area.height - fh) * 0.5f, fw, fh);
        }

        /// <summary>铺面世界坐标 → 16:9 矩形内的视图坐标（屏幕上方 = 更大的 Y）</summary>
        private Vector2 WorldToField(Vector2 world, Rect field)
        {
            float w = Mathf.Max(0.1f, conversionConfig.FieldWidth);
            float h = Mathf.Max(0.1f, conversionConfig.fieldHeight);
            return new Vector2(field.x + (world.x / w + 0.5f) * field.width,
                field.y + (0.5f - world.y / h) * field.height);
        }

        /// <summary>某一列在铺面平面上的位置</summary>
        private Vector2 PathFieldPos(string pathId, Rect field)
        {
            return WorldToField(GetPathWorld(pathId), field);
        }

        /// <summary>铺面底图：边框、中心十字、三等分参考线</summary>
        private static void DrawFieldGuides(Rect field)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.05f);
            for (int i = 1; i <= 2; i++)
            {
                float x = field.x + field.width * i / 3f;
                float y = field.y + field.height * i / 3f;
                GUI.DrawTexture(new Rect(x, field.y, 1f, field.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(field.x, y, field.width, 1f), Texture2D.whiteTexture);
            }
            GUI.color = new Color(1f, 1f, 1f, 0.09f);
            GUI.DrawTexture(new Rect(field.center.x, field.y, 1f, field.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(field.x, field.center.y, field.width, 1f), Texture2D.whiteTexture);

            GUI.color = new Color(0.32f, 0.78f, 1f, 0.45f);
            GUI.DrawTexture(new Rect(field.x, field.y, field.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(field.x, field.yMax - 2f, field.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(field.x, field.y, 2f, field.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(field.xMax - 2f, field.y, 2f, field.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>空心方框：4 条细边拼成（圆角贴图在细条上会切片错位，所以直接画矩形）</summary>
        private static void DrawRing(Rect r, Color color, float thickness)
        {
            float t = Mathf.Max(1f, thickness);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y + t, t, Mathf.Max(0f, r.height - t * 2f)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y + t, t, Mathf.Max(0f, r.height - t * 2f)), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        #endregion

        #region 时间轴

        private void DrawTimeline()
        {
            Rect tl = GetTimelineRect();
            Styles.DrawCard(tl);
            HandleTimelineZoom(tl);

            float innerX = tl.x + 14f;
            float innerW = tl.width - 28f;
            float contentX = innerX + TrackLeftPad;
            float contentW = Mathf.Max(40f, innerW - TrackLeftPad - TrackRightPad);

            float rulerY = tl.y + 8f;
            float rowsTop = rulerY + RulerH + 6f;
            float rowsBottom = tl.yMax - 10f - TimelineToolbarH;
            float rowsH = Mathf.Max(24f, rowsBottom - rowsTop);

            // 拖动跳转只留在刻度带 + 左侧列标签槽：音符行留给「左键新建 / 右键删除」
            Rect scrubArea = new Rect(tl.x + 6f, tl.y + 4f, tl.width - 12f, RulerH + 12f);
            HandleScrub(scrubArea, contentX, contentW);

            Rect gutterArea = new Rect(innerX, rowsTop, Mathf.Max(0f, contentX - innerX), rowsH);
            HandleScrub(gutterArea, contentX, contentW);

            DrawTimelineRuler(contentX, contentW, rulerY, RulerH);

            // 列行
            int rows = Mathf.Max(1, pathCount);
            float rowH = rowsH / rows;
            float labelW = 40f;

            for (int p = 0; p < rows; p++)
            {
                var row = new Rect(innerX, rowsTop + p * rowH, innerW, Mathf.Max(2f, rowH - 2f));
                Styles.DrawRoundSm(row, p % 2 == 0 ? new Color(1f, 1f, 1f, 0.045f) : new Color(1f, 1f, 1f, 0.02f));

                var tag = new Rect(innerX + 4f, row.y + Mathf.Max(1f, (row.height - 18f) * 0.5f), labelW - 8f, 18f);
                if (rowH >= 16f)
                {
                    Styles.DrawRoundSm(tag, new Color(1f, 1f, 1f, 0.07f));
                    GUI.Label(tag, "P" + (p + 1), Styles.ChipLabel);
                }
            }

            // 音符
            if (chart != null && chart.notes != null && tempo != null && chart.notes.Length > 0)
            {
                for (int i = 0; i < chart.notes.Length; i++)
                {
                    var note = chart.notes[i];
                    int pathIdx = PathIndex(note.pathId);
                    if (pathIdx >= rows) continue;

                    Rect r = GetTimelineNoteRect(note, pathIdx, contentX, contentW, rowsTop, rowH);

                    // 放大后窗口外的音符别画到别的面板上去
                    if (r.xMax < contentX - 6f || r.xMin > contentX + contentW + 6f) continue;

                    Color c = note.protectedNote ? Styles.NoteProtected
                        : (note.action == "tap" ? Styles.NoteTap : Styles.NoteDrag);

                    if (IsNoteSelected(i))
                        Styles.DrawRoundSm(new Rect(r.x - 4, r.y - 3, r.width + 8, r.height + 6), new Color(1f, 0.85f, 0.3f, 0.95f));
                    Styles.DrawRoundSm(r, c);
                }
            }

            // 音符行的编辑交互（左键新建/选中、右键删除、Shift 多选）
            Rect rowsArea = new Rect(contentX, rowsTop, contentW, rowsH);
            HandleTimelineEdit(rowsArea, contentX, contentW, rowsTop, rowH, rows);

            // 播放头
            float playheadX = contentX + TimeToTimelineX(songTime, contentW);
            GUI.color = scrubbing ? Styles.Warn : Styles.Danger;
            GUI.DrawTexture(new Rect(playheadX - 1f, rulerY - 2f, 2f, rowsH + RulerH + 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 播放头手柄（鼠标在刻度带附近就能抓住）
            float knobW = 34f;
            float knobH = 16f;
            Rect knob = new Rect(playheadX - knobW / 2f, rulerY - knobH - 2f, knobW, knobH);
            Styles.DrawRoundSm(new Rect(knob.x - 1, knob.y - 1, knob.width + 2, knob.height + 2),
                new Color(1f, 1f, 1f, 0.18f));
            Styles.DrawRoundSm(knob, scrubbing ? Styles.Warn : Styles.Danger);
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.DrawTexture(new Rect(knob.x + 9f, knob.y + 5f, knob.width - 18f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(knob.x + 9f, knob.y + 9f, knob.width - 18f, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 拖动时的时间提示
            if (scrubbing)
            {
                var tip = new Rect(Mathf.Clamp(playheadX - 44f, tl.x + 4f, tl.xMax - 92f), tl.y + 6f, 88f, 26f);
                Styles.DrawRoundSm(tip, new Color(0f, 0f, 0f, 0.78f));
                GUI.Label(tip, FormatTime(songTime), Styles.TimeReadout);
            }

            // 底部工具条：单独用一个 Area 固定在时间轴底部，绝不会压到 P1 行
            Rect barRect = new Rect(innerX, tl.yMax - 8f - TimelineToolbarH, innerW, TimelineToolbarH);
            GUILayout.BeginArea(barRect);
            GUILayout.BeginHorizontal();

            GUILayout.Label("节拍网格", Styles.SmallLabel, GUILayout.Width(56));
            foreach (var d in ThartChartBuilder.GridPresets)
            {
                bool on = snapDivisor == d;
                if (GUILayout.Button(ThartChartBuilder.GridLabel(d), on ? Styles.PillOn : Styles.PillOff,
                    GUILayout.Width(56), GUILayout.Height(26)))
                    snapDivisor = d;
            }
            if (GUILayout.Button("关闭", Styles.PillOff, GUILayout.Width(44), GUILayout.Height(26)))
                snapDivisor = 0;

            GUILayout.Space(14);
            GUILayout.Label("新建", Styles.SmallLabel, GUILayout.Width(34));
            if (GUILayout.Button("Tap", addNoteAction == "tap" ? Styles.PillOn : Styles.PillOff,
                GUILayout.Width(48), GUILayout.Height(26)))
                addNoteAction = "tap";
            if (GUILayout.Button("Drag", addNoteAction == "drag" ? Styles.PillOn : Styles.PillOff,
                GUILayout.Width(52), GUILayout.Height(26)))
                addNoteAction = "drag";

            GUILayout.Space(14);
            if (GUILayout.Button("后退", Styles.SmallButton, GUILayout.Width(54), GUILayout.Height(26))) StepBackward();
            if (GUILayout.Button("前进", Styles.SmallButton, GUILayout.Width(54), GUILayout.Height(26))) StepForward();
            if (GUILayout.Button("回到开头", Styles.SmallButton, GUILayout.Width(76), GUILayout.Height(26)))
            {
                StopPlayback();
                songTime = 0;
            }

            GUILayout.Space(14);
            GUILayout.Label("缩放 " + timelineZoom.ToString("F1") + "x", Styles.SmallLabel, GUILayout.Width(74));
            if (GUILayout.Button("复位", Styles.SmallButton, GUILayout.Width(52), GUILayout.Height(26)))
                timelineZoom = 1f;

            GUILayout.FlexibleSpace();
            GUILayout.Label("音符 " + (chart.notes != null ? chart.notes.Length : 0)
                + (selectedNoteIds.Count > 0 ? " · 选中 " + selectedNoteIds.Count : "")
                + " · 左键新建/选中 · 右键删除 · Ctrl+A 全选 · Ctrl+滚轮 缩放",
                Styles.SmallLabel, GUILayout.Height(26));

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>Ctrl+鼠标滚轮缩放下方时间轴（视图以播放头为中心）</summary>
        private void HandleTimelineZoom(Rect tl)
        {
            if (fileDialog != null && fileDialog.IsOpen) return;

            // Windows 下 IMGUI 的 ScrollWheel 事件不带修饰键状态（Event.control 恒为 false），
            // 所以这里直接轮询输入：滚轮量 + Ctrl 键，才能稳定触发。
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.001f) return;

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            if (!ctrl) return;

            Event e = Event.current;
            if (e != null && !tl.Contains(e.mousePosition)) return;

            if (Time.frameCount == lastZoomFrame) return;
            lastZoomFrame = Time.frameCount;

            float factor = scroll > 0f ? TimelineZoomStep : 1f / TimelineZoomStep;
            timelineZoom = Mathf.Clamp(timelineZoom * factor, MinTimelineZoom, MaxTimelineZoom);
            if (e != null) e.Use();
        }

        /// <summary>处理时间轴上的点击 / 拖动跳转</summary>
        private void HandleScrub(Rect area, float contentX, float contentW)
        {
            Event e = Event.current;
            if (e == null) return;
            // 模态文件浏览器开着时，时间轴不应该再响应点击
            if (fileDialog != null && fileDialog.IsOpen) return;

            bool ready = tempo != null && contentW > 4f;

            if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition))
            {
                scrubbing = true;
                StopPlayback();
                // 抓住播放头的一刻把可见窗口钉死。窗口本来跟着播放头走，
                // 拖动时如果不锁：鼠标右移 → 播放头右移 → 窗口右移 → 同一鼠标位置
                // 又对应更晚的时间，缩放越大越像「指针一拖就飞出去」。
                LockTimelineView();
                if (ready) SeekToLocalX(e.mousePosition.x, contentX, contentW);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && scrubbing && e.button == 0)
            {
                // 拖出时间轴也继续跟随，手感更顺
                if (ready) SeekToLocalX(e.mousePosition.x, contentX, contentW);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && scrubbing)
            {
                scrubbing = false;
                timelineViewLocked = false;
                e.Use();
            }
        }

        /// <summary>把当前可见窗口钉死（拖动播放头期间不再跟随播放头重算）</summary>
        private void LockTimelineView()
        {
            timelineViewLocked = false;
            double start, span;
            GetTimelineView(out start, out span);
            lockedViewStart = start;
            lockedViewSpan = span;
            timelineViewLocked = true;
        }

        private void SeekToLocalX(float globalX, float contentX, float contentW)
        {
            if (GetDuration() <= 0) return;

            double viewStart, viewSpan;
            GetTimelineView(out viewStart, out viewSpan);

            songTime = TimelineTimeAtLocalX(globalX - contentX, contentW, viewStart, viewSpan);
        }

        /// <summary>
        /// 时间轴上的局部 X → 时间。给定窗口(viewStart, viewSpan)后这个映射是纯函数：
        /// 同一个鼠标位置永远给出同一个时间，所以拖动不会自我加速。
        /// </summary>
        public static double TimelineTimeAtLocalX(float localX, float contentW, double viewStart, double viewSpan)
        {
            if (contentW <= 1f || viewSpan <= 0) return viewStart;
            return viewStart + Mathf.Clamp01(localX / contentW) * viewSpan;
        }

        /// <summary>时间轴可见窗口：缩放为 1 时整首歌铺满，放大后以播放头为中心</summary>
        public static void TimelineWindow(double duration, float zoom, double playhead,
            out double viewStart, out double viewSpan)
        {
            if (duration <= 0) { viewStart = 0; viewSpan = 1; return; }

            viewSpan = duration / Mathf.Max(0.01f, zoom);

            double maxStart = duration - viewSpan;
            if (maxStart < 0) maxStart = 0;

            double start = playhead - viewSpan * 0.5;
            if (start < 0) start = 0;
            if (start > maxStart) start = maxStart;
            viewStart = start;
        }

        private void DrawTimelineRuler(float contentX, float contentW, float topY, float height)
        {
            if (tempo == null || chart == null || chart.tempos == null || chart.tempos.Length == 0) return;

            double duration = GetDuration();
            if (duration <= 0) return;

            GUI.color = new Color(1f, 1f, 1f, 0.06f);
            GUI.DrawTexture(new Rect(contentX, topY + height - 4f, contentW, 1f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float secondsPerBeat = 60.0f / chart.tempos[0].bpm;
            if (secondsPerBeat <= 0f) secondsPerBeat = 0.5f;

            double viewStart, viewSpan;
            GetTimelineView(out viewStart, out viewSpan);
            if (viewSpan <= 0) return;

            float pxPerBeat = contentW / (float)(viewSpan / secondsPerBeat);
            bool dense = pxPerBeat >= 8f;
            bool showTicks = pxPerBeat >= 3f;

            // 从可见窗口起点附近开始画，放大后才不会白跑几万个刻度
            int beat = Mathf.Max(0, Mathf.FloorToInt((float)(viewStart / secondsPerBeat)));
            double viewEnd = viewStart + viewSpan;
            int guard = 0;
            while (guard++ < 20000)
            {
                double time = beat * secondsPerBeat;
                if (time > viewEnd) break;

                float x = contentX + TimeToTimelineX(time, contentW);
                bool bar = beat % 4 == 0;

                if (x < contentX - 2f) { beat++; continue; }

                if (bar || (dense && showTicks))
                {
                    GUI.color = bar ? new Color(1f, 1f, 1f, 0.28f) : new Color(1f, 1f, 1f, 0.1f);
                    GUI.DrawTexture(new Rect(x, topY + (bar ? 5f : 9f), 1f, bar ? 11f : 7f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                if (bar && dense)
                    GUI.Label(new Rect(x + 3f, topY - 1f, 46f, 16f), (beat / 4 + 1).ToString(), Styles.TinyLabel);

                beat++;
            }
        }

        #endregion

        #region 状态栏

        private void DrawStatusBar()
        {
            Rect bar = GetStatusRect();
            // 用和整屏一致的渐变收尾，而不是一条纯黑长条
            Styles.DrawStatusBg(bar);
            GUI.color = Styles.Line;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(bar.x + 14, bar.y, bar.width - 28, bar.height));
            GUILayout.BeginHorizontal();

            var dotRect = GUILayoutUtility.GetRect(8, bar.height, GUILayout.Width(8), GUILayout.Height(bar.height));
            GUI.color = recordingState == RecordingState.Recording ? Styles.Danger
                : (server != null && server.ClientCount > 0 ? Styles.Ok : Styles.DimText);
            GUI.DrawTexture(new Rect(dotRect.x, dotRect.y + bar.height / 2f - 4, 8, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.Space(8);
            GUILayout.Label(statusText, Styles.SmallLabel, GUILayout.Height(bar.height));

            GUILayout.FlexibleSpace();

            if (recordingState == RecordingState.Recording || waitingForFullData)
            {
                bool live = Time.realtimeSinceStartup - lastSampleWallClock < 1.5f;
                GUILayout.Label("已收触控 " + samplesThisSession + " 帧" + (live ? " ●" : " ○"),
                    Styles.SmallLabel, GUILayout.Height(bar.height));
                GUILayout.Space(16);
            }

            GUILayout.Label("列 " + pathCount + " · 音符 " + (chart != null && chart.notes != null ? chart.notes.Length : 0),
                Styles.SmallLabel, GUILayout.Height(bar.height));
            GUILayout.Space(16);
            GUILayout.Label("R 录制 · 空格 播放 · F9 截图", Styles.SmallLabel, GUILayout.Height(bar.height));
            GUILayout.Space(16);
            GUILayout.Label("Thart v1.2", Styles.SmallLabel, GUILayout.Height(bar.height));

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        #endregion

        #region 时间轴工具

        /// <summary>
        /// 当前时间轴可见的时间窗口。缩放为 1 时就是整首歌铺满；
        /// 放大后窗口变窄，并以播放头为中心，播放时自动跟着走。
        /// 拖动播放头期间用锁定窗口，避免「窗口跟着播放头跑」导致的加速滑动。
        /// </summary>
        private void GetTimelineView(out double viewStart, out double viewSpan)
        {
            if (timelineViewLocked)
            {
                viewStart = lockedViewStart;
                viewSpan = lockedViewSpan;
                return;
            }

            TimelineWindow(GetDuration(), timelineZoom, songTime, out viewStart, out viewSpan);
        }

        private float TimeToTimelineX(double time, float width)
        {
            double viewStart, viewSpan;
            GetTimelineView(out viewStart, out viewSpan);
            if (viewSpan <= 0) return 0;
            return (float)((time - viewStart) / viewSpan) * width;
        }

        private double TimelineXToTime(float globalX, float contentX, float contentW)
        {
            if (contentW <= 1f) return 0;
            double viewStart, viewSpan;
            GetTimelineView(out viewStart, out viewSpan);
            float t = Mathf.Clamp01((globalX - contentX) / contentW);
            return viewStart + t * viewSpan;
        }

        /// <summary>时间轴上某个音符的绘制矩形（绘制与命中检测共用，保证点得到）</summary>
        private Rect GetTimelineNoteRect(NoteData note, int pathIdx, float contentX, float contentW,
            float rowsTop, float rowH)
        {
            float beat = note.tick / (float)chart.ticksPerBeat;
            double noteTime = tempo != null ? tempo.SecondsAtBeat(beat) : 0.0;
            float x = contentX + TimeToTimelineX(noteTime, contentW);

            float rowY = rowsTop + pathIdx * rowH;
            float noteW = 9f;
            float noteH = Mathf.Max(4f, Mathf.Min(rowH * 0.66f, rowH - 3f));
            float y = rowY + Mathf.Max(0f, (rowH - 2f - noteH) * 0.5f);
            return new Rect(x - noteW / 2f, y, noteW, noteH);
        }

        /// <summary>找出鼠标下方的音符（取最近的一个），没有则返回 -1</summary>
        private int FindNoteNear(Vector2 mouse, float contentX, float contentW, float rowsTop, float rowH, int rows)
        {
            if (chart == null || chart.notes == null || tempo == null) return -1;

            int best = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < chart.notes.Length; i++)
            {
                int pathIdx = PathIndex(chart.notes[i].pathId);
                if (pathIdx >= rows) continue;

                Rect r = GetTimelineNoteRect(chart.notes[i], pathIdx, contentX, contentW, rowsTop, rowH);
                r.xMin -= 4f; r.xMax += 4f; r.yMin -= 2f; r.yMax += 2f;
                if (!r.Contains(mouse)) continue;

                float d = Mathf.Abs(mouse.x - r.center.x) + Mathf.Abs(mouse.y - r.center.y) * 0.5f;
                if (d < bestDist) { bestDist = d; best = i; }
            }

            return best;
        }

        /// <summary>
        /// 时间轴音符行的鼠标交互：
        /// 左键点空白 = 在该列该时间新建音符；左键点音符 = 选中；
        /// Shift+左键 = 加入/移出选中；右键点音符 = 删除。
        /// </summary>
        private void HandleTimelineEdit(Rect area, float contentX, float contentW,
            float rowsTop, float rowH, int rows)
        {
            Event e = Event.current;
            if (e == null) return;
            if (fileDialog != null && fileDialog.IsOpen) return;
            if (!area.Contains(e.mousePosition)) return;

            // IMGUI 里右键是 ContextClick，不是 MouseDown + button==1；
            // 只认 MouseDown 的话右键删除永远不会触发。
            bool rightClick = e.type == EventType.ContextClick ||
                              (e.type == EventType.MouseDown && e.button == 1);
            bool leftClick = e.type == EventType.MouseDown && e.button == 0;
            if (!rightClick && !leftClick) return;

            int hit = FindNoteNear(e.mousePosition, contentX, contentW, rowsTop, rowH, rows);

            if (rightClick)
            {
                if (hit >= 0) DeleteNoteById(chart.notes[hit].id);
                e.Use();
                return;
            }

            if (hit >= 0)
            {
                string id = chart.notes[hit].id;
                if (e.shift) ToggleNoteSelection(id);
                else SelectOnlyNote(id);
                e.Use();
                return;
            }

            // 空白处：Shift 不新建，避免多选时误加音符
            if (e.shift) { e.Use(); return; }

            int row = Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.y - rowsTop) / Mathf.Max(1f, rowH)), 0, rows - 1);
            double time = TimelineXToTime(e.mousePosition.x, contentX, contentW);

            string added = AddNoteAt(row, time, addNoteAction);
            if (!string.IsNullOrEmpty(added)) SelectOnlyNote(added);
            e.Use();
        }

        /// <summary>
        /// 某一列在铺面里的真实世界位置。
        /// 不做任何「用最左/最右列拉伸铺满视图」的归一化 —— 那样四角的录入
        /// 会被拉成贴着左右两边、纵向还被平均掉，看起来就不像四角了。
        /// </summary>
        private Vector2 GetPathWorld(string pathId)
        {
            if (chart == null || chart.sections == null || chart.sections.Length == 0) return Vector2.zero;
            var placements = chart.sections[0].placements;
            if (placements == null) return Vector2.zero;
            foreach (var p in placements)
                if (p.pathId == pathId) return new Vector2(p.x, p.y);
            return Vector2.zero;
        }

        private int PathIndex(string pathId)
        {
            if (chart == null || chart.paths == null) return 0;
            for (int i = 0; i < chart.paths.Length; i++)
                if (chart.paths[i].id == pathId) return i;
            return 0;
        }

        private void StepForward()
        {
            songTime = Math.Min(songTime + GetSnapSeconds(), GetDuration());
        }

        private void StepBackward()
        {
            songTime = Math.Max(songTime - GetSnapSeconds(), 0);
        }

        private double GetSnapSeconds()
        {
            if (tempo == null || chart == null || chart.tempos == null || chart.tempos.Length == 0) return 0.1;
            if (snapDivisor <= 0) return 0.05;
            double beatSeconds = 60.0 / chart.tempos[0].bpm;
            return beatSeconds / snapDivisor;
        }

        private void AddNoteAtPlayhead(string action)
        {
            string id = AddNoteAt(selectedPathIndex, songTime, action);
            if (!string.IsNullOrEmpty(id)) SelectOnlyNote(id);
        }

        /// <summary>
        /// 在指定列 + 指定时间插入一个音符（时间按当前网格吸附），返回新音符 ID。
        /// 同一列同一 tick 上已经有点就不重复插入，直接返回已有的那个。
        /// </summary>
        private string AddNoteAt(int pathIndex, double time, string action)
        {
            if (tempo == null || chart == null || chart.paths == null || chart.paths.Length == 0) return "";

            double beat = tempo.BeatAtSeconds(time);
            int tick = Mathf.RoundToInt((float)(beat * chart.ticksPerBeat));
            tick = ThartChartBuilder.SnapTick(tick, chart.ticksPerBeat, snapDivisor);
            if (tick < 0) tick = 0;

            pathIndex = Mathf.Clamp(pathIndex, 0, chart.paths.Length - 1);
            string pathId = chart.paths[pathIndex].id;

            string existing = FindNoteId(pathId, tick);
            if (!string.IsNullOrEmpty(existing)) return existing;

            PushUndo();

            string noteId = "n" + Guid.NewGuid().ToString("N").Substring(0, 8);

            var list = new System.Collections.Generic.List<NoteData>(chart.notes ?? new NoteData[0]);
            list.Add(new NoteData
            {
                id = noteId,
                tick = tick,
                pathId = pathId,
                action = action,
                protectedNote = false
            });
            list.Sort((a, b) => a.tick.CompareTo(b.tick));
            chart.notes = list.ToArray();
            return noteId;
        }

        private string FindNoteId(string pathId, int tick)
        {
            if (chart == null || chart.notes == null) return "";
            foreach (var n in chart.notes)
                if (n.tick == tick && n.pathId == pathId) return n.id;
            return "";
        }

        #endregion

        #region 选择

        private bool IsNoteSelected(int index)
        {
            return chart != null && chart.notes != null && index >= 0 && index < chart.notes.Length
                && selectedNoteIds.Contains(chart.notes[index].id);
        }

        private void ClearNoteSelection()
        {
            selectedNoteIds.Clear();
            primarySelectedNoteId = "";
        }

        private void SelectOnlyNote(string id)
        {
            selectedNoteIds.Clear();
            if (!string.IsNullOrEmpty(id)) selectedNoteIds.Add(id);
            primarySelectedNoteId = id ?? "";
        }

        private void ToggleNoteSelection(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (selectedNoteIds.Contains(id)) selectedNoteIds.Remove(id);
            else selectedNoteIds.Add(id);
            SyncPrimarySelection();
        }

        private void SelectAllNotes()
        {
            selectedNoteIds.Clear();
            if (chart != null && chart.notes != null)
                foreach (var n in chart.notes)
                    if (!string.IsNullOrEmpty(n.id)) selectedNoteIds.Add(n.id);
            SyncPrimarySelection();
        }

        /// <summary>主选中音符不在集合里时，按铺面顺序挑一个补上</summary>
        private void SyncPrimarySelection()
        {
            if (selectedNoteIds.Count == 0)
            {
                primarySelectedNoteId = "";
                return;
            }
            if (!string.IsNullOrEmpty(primarySelectedNoteId) && selectedNoteIds.Contains(primarySelectedNoteId)) return;

            if (chart != null && chart.notes != null)
            {
                foreach (var n in chart.notes)
                {
                    if (selectedNoteIds.Contains(n.id))
                    {
                        primarySelectedNoteId = n.id;
                        return;
                    }
                }
            }

            foreach (var id in selectedNoteIds)
            {
                primarySelectedNoteId = id;
                return;
            }
        }

        private void DeleteSelectedNotes()
        {
            if (chart == null || chart.notes == null || selectedNoteIds.Count == 0) return;

            PushUndo();
            var list = new System.Collections.Generic.List<NoteData>(chart.notes.Length);
            foreach (var n in chart.notes)
                if (!selectedNoteIds.Contains(n.id)) list.Add(n);

            int removed = chart.notes.Length - list.Count;
            chart.notes = list.ToArray();
            ClearNoteSelection();
            UpdateStatus("已删除 " + removed + " 个音符");
        }

        private void DeleteNoteById(string id)
        {
            if (chart == null || chart.notes == null || string.IsNullOrEmpty(id)) return;

            PushUndo();
            var list = new System.Collections.Generic.List<NoteData>(chart.notes.Length);
            foreach (var n in chart.notes)
                if (n.id != id) list.Add(n);

            chart.notes = list.ToArray();
            selectedNoteIds.Remove(id);
            SyncPrimarySelection();
            UpdateStatus("已删除 1 个音符");
        }

        #endregion

        #region 快捷键

        private void HandleKeyboardShortcuts()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;

            // 正在输入框里打字，或文件浏览器开着：不能抢键盘，
            // 否则输入标题时的空格会触发播放、R 会触发录制、退格会删音符。
            if (GUIUtility.keyboardControl != 0) return;
            if (fileDialog != null && fileDialog.IsOpen) return;

            if (e.control || e.command)
            {
                switch (e.keyCode)
                {
                    case KeyCode.Z: Undo(); e.Use(); break;
                    case KeyCode.Y: Redo(); e.Use(); break;
                    case KeyCode.A:
                        SelectAllNotes();
                        e.Use();
                        break;
                    case KeyCode.S:
                        SaveChartPackage();
                        e.Use();
                        break;
                }
                return;
            }

            switch (e.keyCode)
            {
                case KeyCode.Space:
                    TogglePlayback();
                    e.Use();
                    break;

                case KeyCode.R:
                    if (recordingState == RecordingState.Recording) StopRecording();
                    else StartRecording();
                    e.Use();
                    break;

                case KeyCode.F10:
                    if (HasAudio) StartPushAudioToTablet(false);
                    e.Use();
                    break;

                case KeyCode.Home:
                    StopPlayback();
                    songTime = 0;
                    e.Use();
                    break;

                case KeyCode.Alpha1:
                    mode = ThartEditorMode.Recording;
                    e.Use();
                    break;

                case KeyCode.Alpha2:
                    mode = ThartEditorMode.Editing;
                    e.Use();
                    break;

                case KeyCode.Alpha3:
                    mode = ThartEditorMode.Preview;
                    e.Use();
                    break;

                case KeyCode.End:
                    songTime = GetDuration();
                    e.Use();
                    break;

                case KeyCode.LeftArrow:
                    if (chart != null && chart.tempos != null && chart.tempos.Length > 0 && e.shift)
                        songTime = Math.Max(0, songTime - 60.0 / chart.tempos[0].bpm);
                    else StepBackward();
                    e.Use();
                    break;

                case KeyCode.RightArrow:
                    if (chart != null && chart.tempos != null && chart.tempos.Length > 0 && e.shift)
                        songTime = Math.Min(GetDuration(), songTime + 60.0 / chart.tempos[0].bpm);
                    else StepForward();
                    e.Use();
                    break;

                case KeyCode.Delete:
                case KeyCode.Backspace:
                    DeleteSelectedNotes();
                    e.Use();
                    break;
                case KeyCode.Escape:
                    ClearNoteSelection();
                    e.Use();
                    break;
            }
        }

        #endregion

        #region 格式化

        private string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int totalMs = Mathf.RoundToInt((float)(seconds * 1000.0));
            int m = totalMs / 60000;
            int s = (totalMs / 1000) % 60;
            int ms = totalMs % 1000;
            return string.Format("{0:00}:{1:00}.{2:000}", m, s, ms);
        }

        #endregion
    }
}
