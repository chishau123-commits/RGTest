using System;
using System.Collections.Generic;
using UnityEngine;
using GeometryRhythm.Thart.Network;

namespace GeometryRhythm.Thart.TouchRecorder
{
    /// <summary>
    /// 平板端触控录制器 - 运行在平板/手机上，录制触控数据并发送到电脑端
    /// </summary>
    public sealed partial class ThartTouchRecorder : MonoBehaviour
    {
        [Header("Network Settings")]
        // 用 USB 连接时配合 adb reverse，默认直连本机转发端口；走 Wi-Fi 时改成电脑的局域网 IP。
        public string serverAddress = "127.0.0.1";
        public int serverPort = 28765;

        [Header("Recording Settings")]
        public int targetSampleRate = 60;
        public bool autoReconnect = true;

        /// <summary>录制时顶部操作条高度（逻辑像素）；该区域内不录入触控</summary>
        private const float HudHeight = 52f;
        /// <summary>顶部应用栏总高度：28 状态栏留白 + 56 标题条，录制时也画着，必须排除</summary>
        private const float TopBarHeight = 84f;
        /// <summary>底部状态条高度（逻辑像素）；该区域内同样不录入触控</summary>
        private const float BottomBarHeight = 56f;
        /// <summary>
        /// 录入框宽高比：游玩界面固定 16:9，录入框必须同比例，
        /// 否则同样的手型在不同比例的平板上会得到不同形状的铺面。
        /// </summary>
        private const float CaptureAspect = 16f / 9f;
        private bool showRecordingHud = true;

        // 状态
        private ThartClient client;
        private RecordingState recordingState = RecordingState.Idle;
        private ThartRecordingClock recordingClock;
        private List<TouchSample> recordedSamples = new List<TouchSample>();
        private float lastSampleTime;

        // 音频与预备拍
        private AudioSource audioSource;
        private float countdownSeconds;
        private bool inCountdown;
        private string audioStateText = "音频未加载";

        // 会话
        private int tabletSession = -1;
        private int localSessionCounter;

        // UI 状态
        private string statusText = "准备中...";
        private string serverInput = "";
        private Vector2 scrollPosition;
        private bool showSettings = false;
        private bool reconnectPending;
        private float pulseTimer;
        private float uiScale = 1f;

        // 线程安全的消息队列
        private readonly Queue<Action> mainThreadQueue = new Queue<Action>();
        private readonly object queueLock = new object();

        // 录制统计
        private double recordingDuration;

        // 背景渐变纹理
        private Texture2D bgGradient;
        private Texture2D cardGradient;
        private Texture2D btnGradient;
        private Texture2D btnDangerGradient;
        private Texture2D recordRingTex;
        private Texture2D recordPulseTex;
        private Texture2D recordDotTex;

        void Awake()
        {
            serverInput = serverAddress;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // 音频播放器（录制时在平板端播放歌曲）
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;

            GenerateTextures();
        }

        void Start()
        {
            ConnectToServer();
        }

        void Update()
        {
            lock (queueLock)
            {
                while (mainThreadQueue.Count > 0)
                    mainThreadQueue.Dequeue()?.Invoke();
            }

            if (recordingState == RecordingState.Recording)
            {
                double now = AudioSettings.dspTime;

                // 预备拍期间不采触控、录制时间恒为 0：音频要数到 0 才出声，
                // 这段时间的按压不属于歌曲，记下来就等于把整首歌往后推。
                if (recordingClock.InPreRoll(now))
                {
                    recordingDuration = 0;
                }
                else
                {
                    if (inCountdown)
                    {
                        // 到原点的这一帧才真正开始：第一个采样点不必再等满一个间隔
                        inCountdown = false;
                        statusText = "正在录制...";
                        lastSampleTime = Time.realtimeSinceStartup;
                    }

                    float timeSinceLastSample = Time.realtimeSinceStartup - lastSampleTime;
                    float sampleInterval = 1f / targetSampleRate;
                    if (timeSinceLastSample >= sampleInterval)
                    {
                        CaptureTouchSample();
                        lastSampleTime = Time.realtimeSinceStartup;
                    }

                    recordingDuration = recordingClock.Elapsed(now);
                }
            }

            pulseTimer += Time.deltaTime;
        }

        void OnDestroy()
        {
            client?.Dispose();
            if (bgGradient != null) Destroy(bgGradient);
            if (cardGradient != null) Destroy(cardGradient);
            if (btnGradient != null) Destroy(btnGradient);
            if (btnDangerGradient != null) Destroy(btnDangerGradient);
            if (recordRingTex != null) Destroy(recordRingTex);
            if (recordPulseTex != null) Destroy(recordPulseTex);
            if (recordDotTex != null) Destroy(recordDotTex);
        }

        #region 录入框

        /// <summary>与 OnGUI 一致的 UI 缩放（录制逻辑在任何时刻都要能算录入框）</summary>
        private static float UiScale { get { return Mathf.Clamp(Screen.height / 800f, 1.0f, 1.8f); } }

        /// <summary>
        /// 在给定屏幕尺寸里取 16:9 录入框，单位是屏幕像素，原点在屏幕左下角（与 Input.touch 一致）。
        /// 只占顶部应用栏与底部状态条之间的区域，并按 16x9 的整数块取整，
        /// 保证宽高比精确等于 16:9 —— 非 16:9 的平板上留下来的就是黑边。
        /// 抽成纯函数是为了能脱离真实设备自检（见 ThartFieldValidation）。
        /// </summary>
        public static Rect CaptureFrameFor(int screenWidth, int screenHeight, float uiScale)
        {
            float top = TopBarHeight * uiScale;
            float bottom = BottomBarHeight * uiScale;
            float usableW = Mathf.Max(16f, screenWidth);
            float usableH = Mathf.Max(9f, screenHeight - top - bottom);

            int blocks = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(usableW / 16f, usableH / 9f)));
            float w = blocks * 16f;
            float h = blocks * 9f;
            float x = Mathf.Round((screenWidth - w) * 0.5f);
            // 纵向居中后再夹回可用区域：整数取整会让框往上/往下多出不到 1px，
            // 那点像素正好压在应用栏或状态条上（自检会抓到它）。
            float y = Mathf.Round(bottom + (usableH - h) * 0.5f);
            y = Mathf.Clamp(y, bottom, bottom + Mathf.Max(0f, usableH - h));
            return new Rect(x, y, w, h);
        }

        private static Rect CaptureFrameScreen()
        {
            return CaptureFrameFor(Screen.width, Screen.height, UiScale);
        }

        /// <summary>录入框在逻辑坐标（左上角原点、已除以 uiScale）里的位置，只用于绘制</summary>
        private Rect CaptureFrameLogical()
        {
            Rect frame = CaptureFrameScreen();
            float s = Mathf.Max(0.01f, UiScale);
            return new Rect(frame.x / s, (Screen.height - frame.yMax) / s, frame.width / s, frame.height / s);
        }

        /// <summary>
        /// 屏幕触控点（Unity 屏幕坐标：左下角原点）→ 录入框内的像素与百分比。
        /// 框外返回 false，这样触控位置永远不会跑到铺面的四个角之外。
        ///
        /// 注意两个原点：`screenPosition.y` 与 `frame.y/frame.yMax` 都是左下角原点，
        /// 所以「离录入框顶部多远」必须是 `frame.yMax - screenPosition.y`；
        /// 混用「屏幕高 - y」的左上角原点和 `frame.y` 会让整段录制的 Y 整体偏移。
        /// 抽成纯函数就是为了能脱离设备自检（见 ThartFieldValidation）。
        /// </summary>
        public static bool FramePoint(Rect frame, Vector2 screenPosition,
            out float localX, out float localY, out float xPercent, out float yPercent)
        {
            localX = screenPosition.x - frame.x;
            localY = frame.yMax - screenPosition.y;
            xPercent = Mathf.Clamp01(localX / Mathf.Max(1f, frame.width)) * 100f;
            yPercent = Mathf.Clamp01(localY / Mathf.Max(1f, frame.height)) * 100f;
            return frame.Contains(screenPosition);
        }

        private bool TryFramePoint(Vector2 screenPosition, out float localX, out float localY,
            out float xPercent, out float yPercent)
        {
            return FramePoint(CaptureFrameScreen(), screenPosition, out localX, out localY,
                out xPercent, out yPercent);
        }

        #endregion

        #region 纹理生成

        private void GenerateTextures()
        {
            // 主背景渐变（深蓝紫 → 深青）
            bgGradient = MakeGradient(2, 512,
                new Color(0.08f, 0.06f, 0.18f),
                new Color(0.04f, 0.08f, 0.15f));

            // 卡片渐变（半透明玻璃感）
            cardGradient = MakeGradient(2, 256,
                new Color(1f, 1f, 1f, 0.08f),
                new Color(1f, 1f, 1f, 0.03f));

            // 主按钮渐变（青色）
            btnGradient = MakeGradient(2, 256,
                new Color(0.2f, 0.75f, 0.95f),
                new Color(0.1f, 0.45f, 0.75f));

            // 危险按钮渐变（红色）
            btnDangerGradient = MakeGradient(2, 256,
                new Color(0.95f, 0.3f, 0.4f),
                new Color(0.65f, 0.15f, 0.25f));

            // 录制按钮圆环（白色底，用 GUI.color 控制颜色）
            recordRingTex = MakeRingTexture(256, 12f, Color.white);

            // 脉冲圆环（白色底，用 GUI.color 控制颜色）
            recordPulseTex = MakeRingTexture(256, 3f, Color.white);

            // 实心圆点（录制按钮内圆）
            recordDotTex = MakeCircleTexture(256, Color.white);
        }

        private static Texture2D MakeGradient(int w, int h, Color top, Color bottom)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1);
                Color c = Color.Lerp(top, bottom, t);
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeRingTexture(int size, float thickness, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = size / 2f;
            float outerR = size / 2f - 2f;
            float innerR = outerR - thickness;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= outerR && dist >= innerR)
                {
                    float edge = Mathf.Min(dist - innerR, outerR - dist);
                    float a = Mathf.Clamp01(edge / 2f) * color.a;
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, a));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeCircleTexture(int size, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = size / 2f;
            float radius = size / 2f - 2f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= radius)
                {
                    float edge = radius - dist;
                    float a = Mathf.Clamp01(edge / 2f) * color.a;
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, a));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
            tex.Apply();
            return tex;
        }

        #endregion

        #region UI

        void OnGUI()
        {
            // 以屏幕高度为基准缩放（横屏平板上高度是瓶颈），基准 800px 高，范围 1.0~1.8x
            uiScale = UiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

            float viewW = Screen.width / uiScale;
            float viewH = Screen.height / uiScale;

            // 背景
            GUI.DrawTexture(new Rect(0, 0, viewW, viewH), bgGradient);

            // 顶部状态栏
            DrawTopBar(viewW);

            // 主内容区
            if (showSettings)
                DrawSettingsPanel(viewW, viewH);
            else
                DrawRecordingView(viewW, viewH);

            // 底部状态栏
            DrawBottomBar(viewW, viewH);
        }

        private void DrawTopBar(float width)
        {
            const float barHeight = 56f;
            const float topPad = 28f; // 避开系统状态栏/刘海
            float totalH = barHeight + topPad;

            // 顶部栏不透明背景（完全盖住下面的内容，防止滚动内容透出）
            var topRect = new Rect(0, 0, width, totalH + 2f);
            GUI.color = new Color(0.06f, 0.05f, 0.12f, 1f);
            GUI.DrawTexture(topRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 玻璃高光
            GUI.color = new Color(1f, 1f, 1f, 0.06f);
            GUI.DrawTexture(new Rect(0, 0, width, totalH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 底部细线
            float bottomY = topPad + barHeight - 1;
            GUI.color = new Color(1f, 1f, 1f, 0.1f);
            GUI.DrawTexture(new Rect(0, bottomY, width, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 左侧标题（从 topPad 往下排，避开系统状态栏）
            GUI.Label(new Rect(20, topPad, 300, barHeight), "Thart 触控录制器", Styles.TitleLabel);

            // 设置按钮（最右侧）
            float btnY = topPad + (barHeight - 36f) / 2f;
            float btnX = width - 56;
            string btnLabel = showSettings ? "关闭" : "设置";
            if (GUI.Button(new Rect(btnX, btnY, 44, 36), btnLabel, Styles.SmallButton))
            {
                showSettings = !showSettings;
            }

            // 连接状态（按钮左侧）
            bool connected = client != null && client.State == ConnectionState.Connected;
            bool connecting = client != null && client.State == ConnectionState.Connecting;

            string statusStr = GetConnectionStatusText();
            var statusSize = Styles.StatusLabel.CalcSize(new GUIContent(statusStr));
            float statusW = Mathf.Max(statusSize.x + 22f, 80f);
            float statusX = btnX - statusW - 12f;

            // 状态点
            float dotY = topPad + barHeight / 2f - 5f;
            if (connected)
                GUI.color = new Color(0.35f, 0.9f, 0.5f);
            else if (connecting)
                GUI.color = new Color(1f, 0.8f, 0.3f) * (0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(pulseTimer * 6f)));
            else
                GUI.color = new Color(0.5f, 0.5f, 0.55f);
            GUI.DrawTexture(new Rect(statusX, dotY, 10, 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 状态文字
            GUI.Label(new Rect(statusX + 16, topPad, statusW - 16, barHeight), statusStr, Styles.StatusLabel);
        }

        private void DrawSettingsPanel(float width, float height)
        {
            float topBarBottom = 88f;
            float panelY = topBarBottom + 12f;
            float panelH = height - panelY - 60f;
            float cardW = width - 40;
            float cardX = 20f;

            // 连接设置卡片背景
            var cardRect = new Rect(cardX, panelY, cardW, panelH);
            DrawCard(cardRect);

            // 限定 GUILayout 区域（从卡片内部开始，防止内容溢出到顶部栏）
            GUILayout.BeginArea(new Rect(cardX + 4, panelY + 4, cardW - 8, panelH - 8));

            scrollPosition = GUILayout.BeginScrollView(scrollPosition,
                GUILayout.Width(cardW - 8), GUILayout.Height(panelH - 8));

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label("连接设置", Styles.Heading);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            // IP 输入
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label("服务器地址", Styles.FieldLabel);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUI.SetNextControlName("ipInput");
            serverInput = GUILayout.TextField(serverInput, Styles.TextField, GUILayout.Height(48), GUILayout.Width(cardW - 48));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label("端口: " + serverPort, Styles.HintLabel);
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            // 连接 / 断开按钮
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            bool isConnected = client != null && client.State == ConnectionState.Connected;
            bool isConnecting = client != null && client.State == ConnectionState.Connecting;
            if (!isConnected)
            {
                if (GUILayout.Button(isConnecting ? "连接中..." : "连接到电脑", Styles.PrimaryButton,
                    GUILayout.Height(52), GUILayout.Width(cardW - 48)))
                {
                    serverAddress = serverInput.Trim();
                    ConnectToServer();
                }
            }
            else
            {
                if (GUILayout.Button("断开连接", Styles.DangerButton,
                    GUILayout.Height(52), GUILayout.Width(cardW - 48)))
                {
                    DisconnectFromServer();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(28);

            // 分隔线
            DrawDivider(cardW);

            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label("录制设置", Styles.Heading);
            GUILayout.EndHorizontal();
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label("采样率", Styles.FieldLabel, GUILayout.Width(120));
            GUILayout.FlexibleSpace();
            GUILayout.Label(targetSampleRate + " Hz", Styles.ValueLabel, GUILayout.Width(100));
            GUILayout.Space(24);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            // 必须用 GUILayout 版本：GUI.HorizontalSlider(Rect) 是按绝对坐标绘制的，
            // 在滚动区里会被画到内容最顶端，和这里的布局位置完全对不上。
            targetSampleRate = Mathf.RoundToInt(
                GUILayout.HorizontalSlider(targetSampleRate, 30, 120, GUILayout.Width(cardW - 48)));
            GUILayout.EndHorizontal();
            GUILayout.Space(18);

            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            if (GUILayout.Button(showRecordingHud ? "顶部操作条：开" : "顶部操作条：关",
                Styles.SmallButton, GUILayout.Height(36), GUILayout.Width(cardW - 48)))
                showRecordingHud = !showRecordingHud;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label(showRecordingHud
                ? "顶部 " + (int)HudHeight + "px 计时条不录入；录入区是 16:9 录入框，框外不录入"
                : "不显示计时条；录入区仍是 16:9 录入框，框外不录入", Styles.HintLabel);
            GUILayout.EndHorizontal();
            GUILayout.Space(20);

            // 分隔线
            DrawDivider(cardW);

            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label("设备信息", Styles.Heading);
            GUILayout.EndHorizontal();
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label("屏幕分辨率", Styles.FieldLabel, GUILayout.Width(120));
            GUILayout.FlexibleSpace();
            GUILayout.Label(Screen.width + " × " + Screen.height, Styles.ValueLabel, GUILayout.Width(160));
            GUILayout.Space(24);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label("实时触控点", Styles.FieldLabel, GUILayout.Width(120));
            GUILayout.FlexibleSpace();
            GUILayout.Label(Input.touchCount.ToString(), Styles.ValueLabel, GUILayout.Width(100));
            GUILayout.Space(24);
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label("歌曲音频", Styles.FieldLabel, GUILayout.Width(120));
            GUILayout.FlexibleSpace();
            GUILayout.Label(audioStateText, Styles.ValueLabel, GUILayout.Width(280));
            GUILayout.Space(24);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.Label("音频由电脑端推送，无需手动导入", Styles.HintLabel);
            GUILayout.EndHorizontal();

            GUILayout.Space(20);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRecordingView(float width, float height)
        {
            // 未开始录制：显示说明和开始按钮（此时不存在录入误触问题）
            if (recordingState == RecordingState.Idle)
            {
                DrawIdleView(width, height);
                return;
            }

            // 录制/预备拍：只有 16:9 录入框内是录入区，只画不拦截输入的内容
            DrawCaptureFrame(width, height);

            if (inCountdown)
            {
                Rect frame = CaptureFrameLogical();
                int remain = recordingClock.CountdownLabel(AudioSettings.dspTime);
                GUI.color = new Color(1f, 0.85f, 0.35f, 0.95f);
                GUI.Label(new Rect(frame.x, frame.center.y - 135f, frame.width, 240f), remain.ToString(), Styles.CountdownLabel);
                GUI.color = Color.white;
                GUI.Label(new Rect(frame.x, frame.center.y + 110f, frame.width, 40f),
                    "预备拍 · 数到 0 才开始播放和录入", Styles.HintBigLabel);
            }

            if (showRecordingHud) DrawRecordingHud(width);
        }

        /// <summary>
        /// 16:9 录入框：框外压成黑边（那里的按压不录入），框内只画四条细边，
        /// 完全挡住中间看清谱面落点。
        /// </summary>
        private void DrawCaptureFrame(float width, float height)
        {
            Rect frame = CaptureFrameLogical();

            // 框外黑边：明确告诉玩家「这里按了不算」
            GUI.color = new Color(0.015f, 0.015f, 0.03f, 0.94f);
            float left = Mathf.Clamp(frame.x, 0f, width);
            float right = Mathf.Clamp(width - frame.xMax, 0f, width);
            float top = Mathf.Clamp(frame.y, 0f, height);
            float bottom = Mathf.Clamp(height - frame.yMax, 0f, height);
            if (left > 0.5f) GUI.DrawTexture(new Rect(0, 0, left, height), Texture2D.whiteTexture);
            if (right > 0.5f) GUI.DrawTexture(new Rect(width - right, 0, right, height), Texture2D.whiteTexture);
            if (top > 0.5f) GUI.DrawTexture(new Rect(left, 0, frame.width, top), Texture2D.whiteTexture);
            if (bottom > 0.5f) GUI.DrawTexture(new Rect(left, height - bottom, frame.width, bottom), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 录入框边框
            GUI.color = new Color(0.35f, 0.8f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(frame.x, frame.y, frame.width, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.x, frame.yMax - 3f, frame.width, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.x, frame.y, 3f, frame.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.xMax - 3f, frame.y, 3f, frame.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(frame.x + 10f, frame.yMax - 26f, frame.width - 20f, 22f),
                "16:9 录入框 · 框外不录入", Styles.HudInfoLabel);
        }

        /// <summary>录制时的极简顶部 HUD：计时 + 一个小号停止按钮</summary>
        private void DrawRecordingHud(float width)
        {
            var bar = new Rect(0, 0, width, HudHeight);
            GUI.color = new Color(0.06f, 0.05f, 0.12f, 0.86f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.05f);
            GUI.DrawTexture(new Rect(0, 0, width, HudHeight * 0.5f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.08f);
            GUI.DrawTexture(new Rect(0, HudHeight - 1f, width, 1f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 呼吸的录制指示点
            float pulse = 0.55f + 0.45f * Mathf.Sin(pulseTimer * 6f);
            GUI.color = new Color(1f, 0.35f, 0.4f, pulse);
            GUI.DrawTexture(new Rect(16f, HudHeight * 0.5f - 5f, 10f, 10f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(34f, 0, 300f, HudHeight),
                FormatTime(Mathf.Max(0f, (float)recordingDuration)), Styles.HudTimeLabel);

            GUI.Label(new Rect(334f, 0, 620f, HudHeight),
                (inCountdown ? "预备拍中" : "录入框内录入中")
                + "   ·   " + recordedSamples.Count + " 帧"
                + "   ·   " + Input.touchCount + " 触点", Styles.HudInfoLabel);

            // 停止按钮放右上角，尺寸压到最小
            if (GUI.Button(new Rect(width - 104f, 7f, 90f, HudHeight - 14f), "停止", Styles.HudStopButton))
            {
                StopRecording("{}");
            }
        }

        /// <summary>未开始录制时的界面</summary>
        private void DrawIdleView(float width, float height)
        {
            float cx = width / 2f;
            float cy = height / 2f + 10f;
            bool connected = client != null && client.State == ConnectionState.Connected;
            bool ready = IsSongReady;

            string title;
            string sub;
            if (!connected)
            {
                title = "等待连接电脑...";
                sub = "确认 USB 已连接，或在设置中输入电脑 IP";
            }
            else if (!ready)
            {
                title = "等待音频...";
                sub = audioStateText + "（音频由电脑端推送）";
            }
            else
            {
                title = "已就绪";
                sub = "开始后屏幕中间会出现 16:9 录入框，跟着音乐在框内点";
            }

            GUI.Label(new Rect(0, cy - 235f, width, 60f), title, Styles.HintBigLabel);
            GUI.Label(new Rect(0, cy - 178f, width, 34f), sub, Styles.HintCenterLabel);

            float btnSize = Mathf.Min(width * 0.32f, 220f);
            float btnX = cx - btnSize / 2f;
            float btnY = cy - btnSize / 2f + 10f;
            bool canStart = connected && ready;

            GUI.color = canStart ? new Color(0.3f, 0.85f, 1f, 0.9f) : new Color(0.4f, 0.4f, 0.45f, 0.6f);
            GUI.DrawTexture(new Rect(btnX, btnY, btnSize, btnSize), recordRingTex, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;

            float innerSize = btnSize * 0.55f;
            GUI.color = canStart ? new Color(0.95f, 0.3f, 0.35f) : new Color(0.45f, 0.45f, 0.5f);
            GUI.DrawTexture(new Rect(cx - innerSize / 2f, btnY + btnSize / 2f - innerSize / 2f, innerSize, innerSize),
                recordDotTex, ScaleMode.ScaleToFit, true);
            GUI.color = Color.white;

            if (canStart && GUI.Button(new Rect(btnX, btnY, btnSize, btnSize), "", Styles.EmptyButton))
            {
                // 平板端本地快捷开始（3 秒预备拍，数到 0 才出声并开始录入）
                StartRecording("{\"countdown\":3}");
            }

            GUI.Label(new Rect(0, btnY + btnSize + 24f, width, 34f),
                canStart ? "点击开始（预备拍 3 秒）" : "暂不可开始", Styles.StatsLabel);

            GUI.Label(new Rect(0, btnY + btnSize + 64f, width, 30f),
                showRecordingHud ? "录制时顶部是 " + (int)HudHeight + "px 计时条，录入区固定 16:9"
                                 : "录制时无 HUD 遮挡，录入区固定 16:9", Styles.HintCenterLabel);
        }

        private void DrawBottomBar(float width, float height)
        {
            float barY = height - 56;
            var barRect = new Rect(0, barY, width, 56);

            GUI.color = new Color(1f, 1f, 1f, 0.04f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.color = new Color(1f, 1f, 1f, 0.08f);
            GUI.DrawTexture(new Rect(0, barY, width, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(barRect);
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);

            // 状态点
            var dotRect = GUILayoutUtility.GetRect(8, 56, GUILayout.Width(8), GUILayout.Height(56));
            dotRect.y += 24f;
            dotRect.height = 8f;
            GUI.color = recordingState == RecordingState.Recording
                ? new Color(0.95f, 0.3f, 0.35f)
                : new Color(0.35f, 0.9f, 0.5f);
            GUI.DrawTexture(dotRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.Space(10);
            // 状态文字用固定宽度避免截断
            string statusDisplay = statusText.Length > 28 ? statusText.Substring(0, 26) + "..." : statusText;
            GUILayout.Label(statusDisplay, Styles.BottomStatusLabel, GUILayout.Width(width * 0.55f), GUILayout.Height(56));

            GUILayout.FlexibleSpace();
            GUILayout.Label(recordedSamples.Count + " 帧", Styles.BottomStatusLabel, GUILayout.Width(80), GUILayout.Height(56));
            GUILayout.Space(20);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // 辅助：绘制卡片
        private void DrawCard(Rect rect)
        {
            // 卡片背景
            GUI.color = new Color(1f, 1f, 1f, 0.05f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 卡片边框（顶部高亮）
            GUI.color = new Color(1f, 1f, 1f, 0.08f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // 辅助：分隔线
        private void DrawDivider(float width)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUI.color = new Color(1f, 1f, 1f, 0.08f);
            GUILayout.Box("", GUIStyle.none, GUILayout.Width(width - 48), GUILayout.Height(1));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        #endregion

        #region Network

        private void ConnectToServer()
        {
            statusText = "正在连接...";
            var c = new ThartClient();
            client = c;

            c.OnConnected += () =>
            {
                EnqueueMainThread(() =>
                {
                    statusText = "已连接到电脑";
                    Debug.Log("[ThartRec] connected to " + serverAddress + ":" + serverPort);
                });
            };

            c.OnDisconnected += () =>
            {
                EnqueueMainThread(() =>
                {
                    statusText = "连接已断开";
                    Debug.Log("[ThartRec] disconnected");
                    if (recordingState == RecordingState.Recording)
                    {
                        StopRecording("");
                    }
                    ScheduleReconnect();
                });
            };

            c.OnError += (msg) =>
            {
                EnqueueMainThread(() =>
                {
                    statusText = "错误: " + msg;
                    Debug.LogError("[ThartRec] error: " + msg);
                });
            };

            c.OnMessageReceived += (msg) =>
            {
                EnqueueMainThread(() => HandleServerMessage(msg));
            };

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                bool ok = c.Connect(serverAddress, serverPort);
                if (!ok)
                {
                    EnqueueMainThread(() =>
                    {
                        statusText = "连接失败，自动重试中...";
                        Debug.LogWarning("[ThartRec] connect failed: " + c.ErrorMessage);
                        ScheduleReconnect();
                    });
                }
            });
        }

        private void ScheduleReconnect()
        {
            if (!autoReconnect || reconnectPending) return;
            reconnectPending = true;
            StartCoroutine(ReconnectAfterDelay());
        }

        private System.Collections.IEnumerator ReconnectAfterDelay()
        {
            yield return new WaitForSeconds(2f);
            reconnectPending = false;
            if (client == null || client.State != ConnectionState.Connected)
            {
                client?.Dispose();
                ConnectToServer();
            }
        }

        private void DisconnectFromServer()
        {
            if (recordingState == RecordingState.Recording)
            {
                StopRecording("");
            }
            client?.Disconnect();
            statusText = "已断开连接";
        }

        private void HandleServerMessage(ThartNetworkMessage msg)
        {
            switch (msg.Type)
            {
                case ThartMessageType.PrepareRecording:
                    HandlePrepareRecording(ThartNetworkUtils.GetPayloadString(msg));
                    break;
                case ThartMessageType.StartRecording:
                    StartRecording(ThartNetworkUtils.GetPayloadString(msg));
                    break;
                case ThartMessageType.StopRecording:
                    StopRecording(ThartNetworkUtils.GetPayloadString(msg));
                    break;
                case ThartMessageType.SessionAbort:
                    AbortSession();
                    break;
                case ThartMessageType.Ping:
                    client?.SendMessage(ThartMessageType.Pong, "");
                    break;
                case ThartMessageType.RequestFullData:
                    SendFullRecording();
                    break;
                case ThartMessageType.AudioBegin:
                    HandleAudioBegin(ThartNetworkUtils.GetPayloadString(msg));
                    break;
                case ThartMessageType.AudioChunk:
                    HandleAudioChunk(msg.Payload);
                    break;
                case ThartMessageType.AudioEnd:
                    HandleAudioEnd();
                    break;
                case ThartMessageType.AudioUnload:
                    UnloadSong();
                    break;
            }
        }

        /// <summary>
        /// 电脑端要求准备：确认音频已就绪后回执，保证两端同步开始
        /// </summary>
        private void HandlePrepareRecording(string payload)
        {
            tabletSession = Mathf.RoundToInt(GetJsonFloat(payload, "session", -1));

            // 自己已经在录：必须明确回绝，否则电脑端会以为同步成功，两端状态错位
            if (recordingState == RecordingState.Recording)
            {
                statusText = "正在录制中，忽略了电脑端的开始请求";
                client?.SendMessage(ThartMessageType.Acknowledge,
                    "{\"event\":\"busy\",\"session\":" + tabletSession + DeviceSizeJson() + "}");
                Debug.LogWarning("[ThartRec] prepare -> busy (already recording)");
                return;
            }

            if (IsSongReady)
            {
                statusText = "已就绪，等待开始...";
                client?.SendMessage(ThartMessageType.Acknowledge,
                    "{\"event\":\"ready\",\"session\":" + tabletSession + DeviceSizeJson() + "}");
                Debug.Log("[ThartRec] prepare -> ready (session " + tabletSession + ")");
            }
            else
            {
                statusText = "音频还没准备好";
                client?.SendMessage(ThartMessageType.Acknowledge,
                    "{\"event\":\"notready\",\"session\":" + tabletSession + DeviceSizeJson() + "}");
                Debug.LogWarning("[ThartRec] prepare -> notready (no song)");
            }
        }

        /// <summary>把平板的屏幕分辨率与 16:9 录入框尺寸一起回执给电脑端</summary>
        private static string DeviceSizeJson()
        {
            Rect frame = CaptureFrameScreen();
            return ",\"w\":" + Screen.width + ",\"h\":" + Screen.height
                + ",\"fw\":" + Mathf.RoundToInt(frame.width) + ",\"fh\":" + Mathf.RoundToInt(frame.height);
        }

        /// <summary>
        /// 取消当前会话（例如电脑端提前中止）
        /// </summary>
        private void AbortSession()
        {
            if (recordingState == RecordingState.Recording)
            {
                recordingState = RecordingState.Idle;
                inCountdown = false;
                StopSongPlayback();
            }
            recordedSamples.Clear();
            tabletSession = -1;
            statusText = "会话已取消";
        }

        #endregion

        #region Recording

        /// <summary>
        /// 开始录制：先数 countdown 秒预备拍（不放音频、不采触控、录制时间停在 0），
        /// 数到 0 的瞬间音频与录入同时开始，录制时间原点 = 音频起点
        /// </summary>
        private void StartRecording(string payload)
        {
            if (recordingState == RecordingState.Recording)
            {
                // 已经在录了：回执 busy，让电脑端别一直卡在「与平板同步中」
                client?.SendMessage(ThartMessageType.Acknowledge,
                    "{\"event\":\"busy\",\"session\":" + tabletSession + DeviceSizeJson() + "}");
                return;
            }

            if (!IsSongReady)
            {
                statusText = "没有音频，无法开始录制";
                client?.SendMessage(ThartMessageType.Acknowledge,
                    "{\"event\":\"notready\",\"session\":" + tabletSession + "}");
                Debug.LogWarning("[ThartRec] 拒绝开始：音频未就绪");
                return;
            }

            float cd = GetJsonFloat(payload, "countdown", 3f);
            int sess = Mathf.RoundToInt(GetJsonFloat(payload, "session", tabletSession));
            // 平板自己点开始的时候没有 session：必须自己生成一个并回执给电脑端，
            // 否则电脑端无法把它和触控数据关联起来（这正是之前数据不显示的原因之一）。
            if (sess < 0) sess = NextLocalSession();
            tabletSession = sess;

            recordingState = RecordingState.Recording;

            // 预备拍：先数 countdown 秒，数到 0 才同时开始播放音频与录入。
            // 录制时间原点 = 音频起点，倒计时不属于录制时间。
            countdownSeconds = Mathf.Max(0f, cd);
            inCountdown = countdownSeconds > 0.01f;
            recordingClock = ThartRecordingClock.Start(AudioSettings.dspTime, countdownSeconds);

            recordedSamples.Clear();
            lastSampleTime = Time.realtimeSinceStartup;

            // 音频排期在原点上：预备拍期间只是排期，还没有出声
            StartSongPlayback(recordingClock.origin);

            statusText = inCountdown
                ? ("预备拍 " + Mathf.RoundToInt(countdownSeconds) + " 秒")
                : "正在录制...";
            showSettings = false;

            // 回执，电脑端据此确认同步开始
            client?.SendMessage(ThartMessageType.Acknowledge,
                "{\"event\":\"started\",\"session\":" + tabletSession
                + ",\"countdown\":" + countdownSeconds.ToString("F2") + DeviceSizeJson() + "}");
            Debug.Log("[ThartRec] started (session " + tabletSession + ", countdown " + countdownSeconds.ToString("F2") + ")");
        }

        private void StopRecording(string payload)
        {
            int sess = Mathf.RoundToInt(GetJsonFloat(payload, "session", tabletSession));
            if (sess >= 0) tabletSession = sess;

            // 已经停止过了（例如电脑端重复下发停止）：把上一次的完整数据再发一遍，
            // 这样即便第一次回传丢包，电脑端也能拿到数据。
            if (recordingState != RecordingState.Recording)
            {
                if (recordedSamples.Count > 0) SendFullRecording();
                return;
            }

            recordingState = RecordingState.Stopping;
            // 预备拍里就被停下：录制时间还是 0，回传的是空会话
            recordingDuration = recordingClock.Elapsed(AudioSettings.dspTime);
            inCountdown = false;

            StopSongPlayback();

            statusText = "录制结束，共 " + recordedSamples.Count + " 帧";
            Debug.Log("[ThartRec] stopping (session " + tabletSession + ", frames " + recordedSamples.Count + ")");

            SendFullRecording();

            // 回执给电脑端
            client?.SendMessage(ThartMessageType.Acknowledge,
                "{\"event\":\"stopped\",\"session\":" + tabletSession
                + ",\"frames\":" + recordedSamples.Count + "}");

            recordingState = RecordingState.Idle;
        }

        /// <summary>平板本地开始的会话号（和电脑端的编号互不干扰）</summary>
        private int NextLocalSession()
        {
            localSessionCounter++;
            return 100000 + localSessionCounter;
        }

        private void CaptureTouchSample()
        {
            double time = recordingClock.Elapsed(AudioSettings.dspTime);
            var touches = new List<TouchPoint>();

            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (!AddFrameTouch(touches, touch.fingerId, touch.position, touch.pressure, MapTouchPhase(touch.phase)))
                    continue;
            }

            if (Input.touchCount == 0 && Input.GetMouseButton(0))
            {
                // 编辑器/桌面端的鼠标模拟：同样只在录入框内生效
                AddFrameTouch(touches, 0, Input.mousePosition, 1f, TouchPhase.Moved);
            }

            var sample = new TouchSample
            {
                time = time,
                touches = touches.ToArray()
            };

            recordedSamples.Add(sample);
            liveSamples.Add(sample);
            if (liveSamples.Count > 5) liveSamples.RemoveAt(0);

            if (client != null && client.State == ConnectionState.Connected)
            {
                client.SendTouchSample(sample);
            }
        }

        /// <summary>
        /// 录入框内的触控点才收：百分比相对录入框（不是整屏），
        /// 所以四周的黑边不会被记成铺面的边缘。
        /// </summary>
        private bool AddFrameTouch(List<TouchPoint> touches, int fingerId, Vector2 screenPosition,
            float pressure, TouchPhase phase)
        {
            float localX, localY, xPercent, yPercent;
            if (!TryFramePoint(screenPosition, out localX, out localY, out xPercent, out yPercent)) return false;

            touches.Add(new TouchPoint
            {
                fingerId = fingerId,
                x = localX,
                y = localY,
                xPercent = xPercent,
                yPercent = yPercent,
                pressure = pressure > 0 ? pressure : 1f,
                phase = phase
            });
            return true;
        }

        private List<TouchSample> liveSamples = new List<TouchSample>();

        private void SendFullRecording()
        {
            Rect frame = CaptureFrameScreen();
            var recording = new ThartTouchRecording
            {
                schemaVersion = 3,
                title = "Thart Recording " + DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                session = tabletSession,
                durationSeconds = recordingDuration,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                // 百分比坐标是相对这个 16:9 录入框的
                frameWidth = Mathf.RoundToInt(frame.width),
                frameHeight = Mathf.RoundToInt(frame.height),
                samples = recordedSamples.ToArray()
            };

            bool sent = false;
            if (client != null && client.State == ConnectionState.Connected)
                sent = client.SendFullRecording(recording);

            // 不管发没发出去，本地都留一份「全屏」原始数据，避免数据丢失
            try
            {
                string path = Application.persistentDataPath + "/thart_touch_"
                    + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
                System.IO.File.WriteAllText(path, JsonUtility.ToJson(recording));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ThartRec] 本地保存触控数据失败: " + e.Message);
            }

            statusText = sent
                ? ("已发送全屏触控数据 (" + recordedSamples.Count + " 帧)")
                : ("已保存到平板本地 (" + recordedSamples.Count + " 帧)");
        }

        #endregion

        #region Helpers

        private TouchPhase MapTouchPhase(UnityEngine.TouchPhase phase)
        {
            switch (phase)
            {
                case UnityEngine.TouchPhase.Began: return TouchPhase.Began;
                case UnityEngine.TouchPhase.Moved: return TouchPhase.Moved;
                case UnityEngine.TouchPhase.Stationary: return TouchPhase.Stationary;
                case UnityEngine.TouchPhase.Ended: return TouchPhase.Ended;
                case UnityEngine.TouchPhase.Canceled: return TouchPhase.Canceled;
                default: return TouchPhase.Canceled;
            }
        }

        private string GetConnectionStatusText()
        {
            if (client == null) return "未连接";
            switch (client.State)
            {
                case ConnectionState.Connected: return "已连接";
                case ConnectionState.Connecting: return "连接中";
                case ConnectionState.Disconnected: return "已断开";
                case ConnectionState.Error: return "错误";
                default: return "未知";
            }
        }

        private string FormatTime(double seconds)
        {
            int mins = (int)(seconds / 60);
            int secs = (int)(seconds % 60);
            int ms = (int)((seconds - (int)seconds) * 1000);
            return string.Format("{0:00}:{1:00}.{2:000}", mins, secs, ms);
        }

        private void EnqueueMainThread(Action action)
        {
            lock (queueLock)
            {
                mainThreadQueue.Enqueue(action);
            }
        }

        #endregion
    }

    /// <summary>
    /// UI 样式 - 现代化深色主题
    /// </summary>
    internal static class Styles
    {
        public static GUIStyle TitleLabel;
        public static GUIStyle SubtitleLabel;
        public static GUIStyle Heading;
        public static GUIStyle FieldLabel;
        public static GUIStyle ValueLabel;
        public static GUIStyle HintLabel;
        public static GUIStyle HintCenterLabel;
        public static GUIStyle HintBigLabel;
        public static GUIStyle StatusLabel;
        public static GUIStyle BottomStatusLabel;
        public static GUIStyle RecordingTimeLabel;
        public static GUIStyle RecordingStatusLabel;
        public static GUIStyle CountdownLabel;
        public static GUIStyle HudTimeLabel;
        public static GUIStyle HudInfoLabel;
        public static GUIStyle HudStopButton;
        public static GUIStyle StatsLabel;
        public static GUIStyle BigTimeLabel;
        public static GUIStyle TextField;
        public static GUIStyle PrimaryButton;
        public static GUIStyle DangerButton;
        public static GUIStyle SmallButton;
        public static GUIStyle IconButton;
        public static GUIStyle EmptyButton;

        static Styles() { Init(); }

        static void Init()
        {
            var textColor = new Color(0.95f, 0.96f, 1f);
            var subTextColor = new Color(0.7f, 0.72f, 0.82f);
            var accentColor = new Color(0.3f, 0.85f, 1f);

            TitleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = textColor }
            };

            SubtitleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = subTextColor }
            };

            Heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = textColor }
            };

            FieldLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = subTextColor }
            };

            ValueLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = textColor }
            };

            HintLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.55f, 0.58f, 0.7f) }
            };

            HintCenterLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.55f, 0.58f, 0.7f) }
            };

            HintBigLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.62f, 0.75f) }
            };

            StatusLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = subTextColor }
            };

            BottomStatusLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = subTextColor }
            };

            RecordingTimeLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor }
            };

            RecordingStatusLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            StatsLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = subTextColor }
            };

            BigTimeLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = accentColor }
            };

            // 文本输入框
            var tfNormal = MakeTex(2, 2, new Color(0.15f, 0.16f, 0.22f));
            var tfHover = MakeTex(2, 2, new Color(0.18f, 0.2f, 0.28f));
            var tfActive = MakeTex(2, 2, new Color(0.12f, 0.14f, 0.2f));
            TextField = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 0, 0),
                normal = { textColor = textColor, background = tfNormal },
                hover = { textColor = textColor, background = tfHover },
                active = { textColor = textColor, background = tfActive },
                focused = { textColor = textColor, background = tfActive }
            };

            // 主按钮
            var btnNorm = MakeGradientTex(2, 64, new Color(0.2f, 0.75f, 0.95f), new Color(0.1f, 0.45f, 0.75f));
            var btnHover = MakeGradientTex(2, 64, new Color(0.3f, 0.85f, 1f), new Color(0.15f, 0.55f, 0.85f));
            var btnActive = MakeGradientTex(2, 64, new Color(0.1f, 0.55f, 0.75f), new Color(0.08f, 0.35f, 0.65f));
            PrimaryButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = btnNorm },
                hover = { textColor = Color.white, background = btnHover },
                active = { textColor = Color.white, background = btnActive }
            };

            // 危险按钮
            var dngNorm = MakeGradientTex(2, 64, new Color(0.95f, 0.3f, 0.4f), new Color(0.65f, 0.15f, 0.25f));
            var dngHover = MakeGradientTex(2, 64, new Color(1f, 0.4f, 0.5f), new Color(0.75f, 0.2f, 0.3f));
            var dngActive = MakeGradientTex(2, 64, new Color(0.75f, 0.2f, 0.3f), new Color(0.5f, 0.1f, 0.2f));
            DangerButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = dngNorm },
                hover = { textColor = Color.white, background = dngHover },
                active = { textColor = Color.white, background = dngActive }
            };

            // 图标按钮
            var iconNorm = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.08f));
            var iconHover = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.15f));
            IconButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor, background = iconNorm },
                hover = { textColor = Color.white, background = iconHover },
                active = { textColor = Color.white, background = iconNorm }
            };

            // 小按钮
            var smlNorm = MakeTex(2, 2, new Color(0.25f, 0.27f, 0.35f));
            var smlHover = MakeTex(2, 2, new Color(0.35f, 0.37f, 0.45f));
            var smlActive = MakeTex(2, 2, new Color(0.2f, 0.22f, 0.3f));
            SmallButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor, background = smlNorm },
                hover = { textColor = Color.white, background = smlHover },
                active = { textColor = Color.white, background = smlActive }
            };

            // 倒计时大数字
            CountdownLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 110,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            // 录制 HUD
            HudTimeLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 1f, 1f, 0.92f) }
            };

            HudInfoLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.72f, 0.76f, 0.88f) }
            };

            var hudNorm = MakeTex(2, 2, new Color(0.75f, 0.22f, 0.28f));
            var hudHover = MakeTex(2, 2, new Color(0.9f, 0.3f, 0.36f));
            HudStopButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = hudNorm },
                hover = { textColor = Color.white, background = hudHover },
                active = { textColor = Color.white, background = hudNorm }
            };

            // 空按钮（透明点击区）
            EmptyButton = new GUIStyle(GUI.skin.button)
            {
                normal = { textColor = Color.clear, background = null },
                hover = { textColor = Color.clear, background = null },
                active = { textColor = Color.clear, background = null }
            };
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeGradientTex(int w, int h, Color top, Color bottom)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1);
                Color c = Color.Lerp(top, bottom, t);
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }
    }
}
