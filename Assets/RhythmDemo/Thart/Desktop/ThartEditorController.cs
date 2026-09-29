using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using GeometryRhythm.Thart.Network;

namespace GeometryRhythm.Thart.Editor
{
    /// <summary>
    /// Thart 编辑器工作模式
    /// </summary>
    public enum ThartEditorMode
    {
        Recording,  // 录制模式 - 接收平板触控数据
        Editing,    // 编辑模式 - 编辑铺面
        Preview     // 预览模式 - 3D 铺面预览
    }

    /// <summary>
    /// Thart 电脑端编辑器主控制器
    /// 负责：网络服务、录制控制、铺面编辑、导入导出
    /// </summary>
    public sealed partial class ThartEditorController : MonoBehaviour
    {
        [Header("Network")]
        public int serverPort = 28765;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip currentAudio;

        [Header("Recording")]
        public TouchToNoteConfig conversionConfig = new TouchToNoteConfig();

        // 核心状态
        private ThartEditorMode mode = ThartEditorMode.Recording;
        private ThartServer server;
        private RecordingState recordingState = RecordingState.Idle;
        private ThartRecordingClock recordingClock;
        private double recordingDuration;

        // 录制会话（解决开始/停止不同步与数据丢失）
        private int sessionId;
        private int activeSession = -1;        // 当前正在进行的会话号
        private bool armingTablet;             // 已发出 Prepare，等待平板就绪
        private bool waitingForFullData;       // 已停止，等待平板回传完整数据
        private double stopRequestDsp;
        private double lastSampleDsp;
        private int samplesThisSession;
        private const double FullDataTimeout = 6.0;
        private const double ArmTimeout = 6.0;
        private const int MaxUndoSteps = 60;
        private int fullDataRetries;
        private double lastRequestDsp;
        private double lastSampleWallClock = -1000;
        private double armRequestDsp;
        // 平板自己上报的分辨率：即便拿到的是旧版驱动送来的像素数据也能正确归一化
        private int tabletScreenWidth;
        private int tabletScreenHeight;
        // 平板的 16:9 录入框尺寸：百分比坐标就是相对它归一化的
        private int tabletFrameWidth;
        private int tabletFrameHeight;

        // 数据
        private ChartData chart;
        private TempoMap tempo;
        private ThartTouchRecording currentRecording;
        private List<TouchSample> liveSamples = new List<TouchSample>();
        private List<ThartNoteEvent> convertedNotes = new List<ThartNoteEvent>();
        private TouchNoteLayout convertedLayout;

        // UI 状态
        private string statusText = "就绪";
        private string chartFilePath = "";
        private float uiScale = 1f;
        private float leftPanelWidth = 360f;
        private float timelineHeight = 260f;
        private Vector2 leftPanelScroll;
        private Vector2 timelineScroll;
        private bool showRecordingPanel = true;
        private bool showNotePanel = true;
        private bool showTimingPanel = false;

        // 播放状态
        private bool playing;
        private double songTime;
        private double transportDspStart;
        private double transportSongStart;
        private bool transportSilent; // 没有音频时用 dsp 时钟静默走带

        // 选中状态：音符用 ID 记，排序/增删/撤销后都不会错位
        private string primarySelectedNoteId = "";
        private readonly HashSet<string> selectedNoteIds = new HashSet<string>();
        private int selectedPathIndex = 0;
        // 时间轴左键新建音符时用的类型
        private string addNoteAction = "tap";

        /// <summary>主选中音符的下标（跟随 ID 实时解析，找不到返回 -1）</summary>
        private int selectedNoteIndex
        {
            get
            {
                if (chart == null || chart.notes == null || string.IsNullOrEmpty(primarySelectedNoteId)) return -1;
                for (int i = 0; i < chart.notes.Length; i++)
                    if (chart.notes[i].id == primarySelectedNoteId) return i;
                return -1;
            }
        }

        // 线程安全队列
        private readonly Queue<Action> mainThreadQueue = new Queue<Action>();
        private readonly object queueLock = new object();

        // 撤销/重做
        private readonly Stack<string> undoStack = new Stack<string>();
        private readonly Stack<string> redoStack = new Stack<string>();

        // 吸附设置：每拍等分数（1/2/3/4/6/8/n），0 表示关闭吸附
        private int snapDivisor = 4;
        private string customGridInput = "";
        private bool autoSnapAfterRecording = true;

        // 预备拍（数到 0 才开始播放音频与录入，录制时间原点 = 音频起点）
        private float countdownSeconds = 3f;
        private bool inCountdown;
        private bool playAudioOnDesktop;

        // 音符流速
        private float scrollSpeed = 8f;

        /// <summary>当前铺面的列数（来自触控位置自动布局，没有固定轨道）</summary>
        private int pathCount
        {
            get { return chart != null && chart.paths != null ? chart.paths.Length : 0; }
        }

        #region Lifecycle

        void Awake()
        {
            Application.targetFrameRate = 60;

            // 不强制全屏：始终以可缩放的窗口运行，并且窗口不超过桌面
            try
            {
                if (Screen.fullScreen) Screen.fullScreen = false;
                Screen.fullScreenMode = FullScreenMode.Windowed;

                int desktopW = Screen.currentResolution.width;
                int desktopH = Screen.currentResolution.height;
                if (desktopW > 0 && desktopH > 0)
                {
                    int targetW = Mathf.Min(1600, desktopW - 60);
                    int targetH = Mathf.Min(900, desktopH - 120);
                    if (targetW > 640 && targetH > 480 &&
                        (Mathf.Abs(Screen.width - targetW) > 4 || Mathf.Abs(Screen.height - targetH) > 4))
                    {
                        Screen.SetResolution(targetW, targetH, FullScreenMode.Windowed);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Thart] 切换窗口模式失败: " + e.Message);
            }

            // 主摄像机底色与 UI 面板保持一致（预览模式下 UI 不再画整屏底色）
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = new Color(0.055f, 0.058f, 0.078f, 1f);
            }

            LoadOrCreateChart();
        }

        void Start()
        {
            StartServer();
            AutoLoadLastSong();
            SetupPreview();
            LoadCommandLineRecording();
            UpdateStatus("Thart 编辑器已启动");
        }

        /// <summary>
        /// QA / 复查用：-thartRecording &lt;json&gt; 直接载入一份触控录制并转换，
        /// 用来核对「平板上按的位置」与「编辑器里画的位置」是否一致，
        /// 不必每次都连平板重录一遍。
        /// </summary>
        private void LoadCommandLineRecording()
        {
            string path = CommandLineArgument("-thartRecording");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                currentRecording = ThartChartIO.LoadRecording(path);
                mode = ThartEditorMode.Editing;
                ConvertRecordingToNotes();
                UpdateStatus("已载入触控录制: " + Path.GetFileName(path)
                    + "（" + (currentRecording != null && currentRecording.HasCaptureFrame
                        ? "录入框 " + currentRecording.frameWidth + "×" + currentRecording.frameHeight
                        : "旧数据：按整屏归一化") + "）");
            }
            catch (Exception e)
            {
                UpdateStatus("载入触控录制失败: " + e.Message);
            }
        }

        private static string CommandLineArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        void Update()
        {
            // 处理主线程队列
            lock (queueLock)
            {
                while (mainThreadQueue.Count > 0)
                    mainThreadQueue.Dequeue()?.Invoke();
            }

            // 更新播放时间
            if (playing)
            {
                if (!transportSilent && AudioSettings.dspTime < transportDspStart)
                {
                    // PlayScheduled 的预排期还没到，此时 isPlaying 还是 false，
                    // 不能据此判定「已停止」，否则一按播放就立刻被取消。
                    songTime = Math.Max(0.0, transportSongStart);
                }
                else if (transportSilent || (audioSource != null && audioSource.isPlaying))
                {
                    songTime = Math.Max(0.0, AudioSettings.dspTime - transportDspStart + transportSongStart);
                    if (songTime >= GetDuration())
                    {
                        StopPlayback();
                        songTime = GetDuration();
                    }
                }
                else
                {
                    // 音频意外停止（例如播放位置越界）时不能一直卡在「播放中」
                    playing = false;
                }
            }

            // 录制时更新时钟（预备拍期间时间轴停在 0：倒计时不属于录制时间）
            if (recordingState == RecordingState.Recording)
            {
                double now = AudioSettings.dspTime;

                if (inCountdown && !recordingClock.InPreRoll(now))
                {
                    inCountdown = false;
                    UpdateStatus("录制中...");
                }

                recordingDuration = recordingClock.Elapsed(now);
                songTime = recordingDuration;
            }

            // 等待平板确认就绪：超时自动放弃，避免一直卡在「正在与平板同步」
            if (armingTablet && AudioSettings.dspTime - armRequestDsp > ArmTimeout)
            {
                armingTablet = false;
                UpdateStatus("平板没有响应，已取消本次开始；请检查平板是否在录制界面");
            }

            // 停止后等待平板回传完整数据：先主动重发请求，超时才退回实时采样
            if (waitingForFullData)
            {
                double waited = AudioSettings.dspTime - stopRequestDsp;

                if (waited > 1.2 && fullDataRetries < 3 && AudioSettings.dspTime - lastRequestDsp > 1.2)
                {
                    fullDataRetries++;
                    lastRequestDsp = AudioSettings.dspTime;
                    server?.BroadcastMessage(ThartMessageType.RequestFullData,
                        "{\"session\":" + activeSession + "}");
                    UpdateStatus("正在向平板索取完整数据（第 " + fullDataRetries + " 次）...");
                }

                if (waited > FullDataTimeout)
                {
                    waitingForFullData = false;
                    if (samplesThisSession > 0)
                    {
                        UpdateStatus("平板未回传完整数据，改用实时接收的 " + samplesThisSession + " 帧");
                        BuildRecordingFromLiveSamples();
                    }
                    else
                    {
                        UpdateStatus("录制结束，但没有收到任何平板数据（请检查平板是否已连接）");
                        recordingState = RecordingState.Idle;
                    }
                }
            }

            // F9 截图（方便预览 UI 效果）
            if (Input.GetKeyDown(KeyCode.F9))
            {
                string path = Application.dataPath + "/../thart_editor_screenshot.png";
                ScreenCapture.CaptureScreenshot(path);
                UpdateStatus("截图已保存: " + path);
            }

            // 3D 预览（摄像机 / 音符位置）
            UpdatePreview();
        }

        void OnDestroy()
        {
            server?.Dispose();
            DestroyPreview();
        }

        #endregion

        #region Network

        private void StartServer()
        {
            server = new ThartServer();
            server.OnClientConnected += (client) =>
            {
                EnqueueMainThread(() =>
                {
                    UpdateStatus("平板已连接");
                    // 新连上的设备需要重新推送音频（它本地没有音频）
                    clientGeneration++;
                    OnTabletReadyForAudio();
                });
            };
            server.OnClientDisconnected += (client) =>
            {
                EnqueueMainThread(() =>
                {
                    UpdateStatus("平板已断开连接");
                    if (recordingState == RecordingState.Recording)
                    {
                        StopRecording();
                    }
                });
            };
            server.OnMessageReceived += (client, msg) =>
            {
                EnqueueMainThread(() => HandleClientMessage(client, msg));
            };
            server.OnError += (err) =>
            {
                EnqueueMainThread(() => UpdateStatus("网络错误: " + err));
            };

            if (server.Start(serverPort))
            {
                string ip = ThartNetworkUtils.GetLocalIPAddress();
                UpdateStatus("服务器已启动: " + ip + ":" + serverPort);
            }
            else
            {
                UpdateStatus("服务器启动失败: " + server.ErrorMessage);
            }
        }

        private void HandleClientMessage(TcpClient client, ThartNetworkMessage msg)
        {
            switch (msg.Type)
            {
                case ThartMessageType.Hello:
                    UpdateStatus("平板已连接并握手");
                    server.SendMessage(client, ThartMessageType.Acknowledge, "{\"status\":\"ok\"}");
                    // 平板可能需要当前歌曲
                    OnTabletReadyForAudio();
                    break;

                case ThartMessageType.Acknowledge:
                    HandleAcknowledge(msg);
                    break;

                case ThartMessageType.TouchSample:
                    HandleTouchSample(msg);
                    break;

                case ThartMessageType.RecordingComplete:
                    HandleFullRecording(msg);
                    break;

                case ThartMessageType.Ping:
                    server.SendMessage(client, ThartMessageType.Pong, "");
                    break;
            }
        }

        /// <summary>
        /// 平板回执：音频状态、就绪、已开始、已停止
        /// </summary>
        private void HandleAcknowledge(ThartNetworkMessage msg)
        {
            string json = ThartNetworkUtils.GetPayloadString(msg);
            if (string.IsNullOrEmpty(json)) return;

            string evt = JsonStr(json, "event");
            int sess = (int)Math.Round(JsonNum(json, "session", -1));

            // 平板会带上自己的屏幕分辨率与 16:9 录入框尺寸，先用它把像素数据归一化
            int tw = (int)Math.Round(JsonNum(json, "w", 0));
            int th = (int)Math.Round(JsonNum(json, "h", 0));
            if (tw > 0 && th > 0)
            {
                tabletScreenWidth = tw;
                tabletScreenHeight = th;
            }
            int fw = (int)Math.Round(JsonNum(json, "fw", 0));
            int fh = (int)Math.Round(JsonNum(json, "fh", 0));
            if (fw > 0 && fh > 0)
            {
                tabletFrameWidth = fw;
                tabletFrameHeight = fh;
            }

            if (json.Contains("\"audio\":\"ok\""))
            {
                UpdateStatus("平板已收到音频");
                return;
            }
            if (json.Contains("\"audio\":\"fail\""))
            {
                UpdateStatus("平板解码音频失败，请换 wav / ogg 格式");
                return;
            }

            switch (evt)
            {
                case "ready":
                    // 平板确认可以开始 → 现在才真正开始，保证两端同步
                    if (armingTablet)
                    {
                        armingTablet = false;
                        BeginRecordingInternal(sess);
                    }
                    break;

                case "notready":
                    armingTablet = false;
                    UpdateStatus("平板还没拿到音频，正在重新推送...");
                    pushedEpoch = -1;
                    StartPushAudioToTablet(false);
                    break;

                case "started":
                    // 平板本地启动的情况：电脑端接管这个会话
                    if (recordingState == RecordingState.Idle)
                    {
                        // 平板自己开始录制时不会带 session，这里必须补一个，
                        // 否则下面会一直认为「没有活动会话」而把触控数据全部丢掉。
                        activeSession = sess >= 0 ? sess : ++sessionId;
                        recordingState = RecordingState.Recording;
                        // 平板本地启动也带 countdown：照同一套规则接管时钟，
                        // 预备拍期间电脑端同样停在 0，等原点到了再算录制时间。
                        double tabletCountdown = Math.Max(0.0, JsonNum(json, "countdown", 0));
                        recordingClock = ThartRecordingClock.Start(AudioSettings.dspTime, (float)tabletCountdown);
                        recordingDuration = 0;
                        songTime = 0;
                        inCountdown = tabletCountdown > 0.01;
                        waitingForFullData = false;
                        fullDataRetries = 0;
                        liveSamples.Clear();
                        samplesThisSession = 0;
                        UpdateStatus("平板已开始录制，电脑端已接管会话 #" + activeSession);
                    }
                    break;

                case "busy":
                    // 平板自己还在录：不能重复开始，否则两端状态会错位
                    armingTablet = false;
                    UpdateStatus("平板正在录制中，请先在平板上停止再重新开始");
                    break;

                case "stopped":
                {
                    int stoppedFrames = JsonNum(json, "frames", -1) >= 0
                        ? (int)Math.Round(JsonNum(json, "frames", 0)) : -1;
                    string stoppedText = "平板已停止录制"
                        + (stoppedFrames >= 0 ? "（" + stoppedFrames + " 帧）" : "");

                    // 平板是自己按停止的：电脑端也要进入「等回传」状态，
                    // 否则万一完整数据丢了，电脑端会一直停在「录制中」。
                    if (recordingState == RecordingState.Recording)
                    {
                        recordingState = RecordingState.Stopping;
                        inCountdown = false;
                        recordingDuration = recordingClock.Elapsed(AudioSettings.dspTime);
                        waitingForFullData = true;
                        stopRequestDsp = AudioSettings.dspTime;
                        lastRequestDsp = stopRequestDsp;
                        fullDataRetries = 0;
                        UpdateStatus(stoppedText + "，等待数据回传...");
                    }
                    else if (waitingForFullData)
                    {
                        UpdateStatus(stoppedText + "，等待数据回传...");
                    }
                    // 完整数据总是先于这条回执到达（平板先发数据、再发回执）。
                    // 那种情况下 HandleFullRecording 已经报过「已接收完整录制数据」，
                    // 这里再盖成「等待回传」会让人以为数据丢了 —— 状态栏会一直挂着
                    // 这句谎话，直到下一次操作才被替换掉。
                }
                break;
            }
        }

        private void HandleTouchSample(ThartNetworkMessage msg)
        {
            // 录制中与「等待回传」期间都要接收，避免丢掉尾部数据。
            // 不再用 session 做硬门槛——只要还在收数据就说明平板在录，直接接管。
            bool accepting = recordingState == RecordingState.Recording
                || recordingState == RecordingState.Stopping
                || waitingForFullData
                || activeSession >= 0;

            if (!accepting) return;

            string json = ThartNetworkUtils.GetPayloadString(msg);
            try
            {
                var sample = JsonUtility.FromJson<TouchSample>(json);
                if (sample == null) return;

                if (activeSession < 0) activeSession = ++sessionId;

                liveSamples.Add(sample);
                samplesThisSession++;
                lastSampleDsp = AudioSettings.dspTime;
                lastSampleWallClock = Time.realtimeSinceStartup;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to parse touch sample: " + e.Message);
            }
        }

        private void HandleFullRecording(ThartNetworkMessage msg)
        {
            string json = ThartNetworkUtils.GetPayloadString(msg);
            try
            {
                var incoming = JsonUtility.FromJson<ThartTouchRecording>(json);
                if (incoming == null) return;

                int frames = incoming.samples != null ? incoming.samples.Length : 0;

                // 空数据不能应用：否则会把当前铺面清成 0 个音符
                if (frames == 0)
                {
                    UpdateStatus("平板回传的录制数据是空的，已忽略（铺面保持不变）");
                    waitingForFullData = false;
                    activeSession = -1;
                    recordingState = RecordingState.Idle;
                    return;
                }

                // 重复回传（重发 / 多次索取）不能覆盖用户已经改好的铺面
                if (!waitingForFullData && activeSession < 0 && currentRecording != null &&
                    incoming.session >= 0 && incoming.session == currentRecording.session)
                {
                    UpdateStatus("已忽略重复回传的录制数据（" + frames + " 帧）");
                    return;
                }

                currentRecording = incoming;
                recordingDuration = currentRecording.durationSeconds;
                UpdateStatus("已接收完整录制数据: " + frames + " 帧");

                waitingForFullData = false;
                fullDataRetries = 0;
                activeSession = -1;
                recordingState = RecordingState.Idle;

                ConvertRecordingToNotes();
            }
            catch (Exception e)
            {
                UpdateStatus("解析录制数据失败: " + e.Message);
                waitingForFullData = false;
                activeSession = -1;
                recordingState = RecordingState.Idle;
            }
        }

        /// <summary>
        /// 用实时接收到的采样拼一份录制数据（完整数据没回传时的兜底）
        /// </summary>
        private void BuildRecordingFromLiveSamples()
        {
            if (liveSamples.Count == 0)
            {
                activeSession = -1;
                recordingState = RecordingState.Idle;
                return;
            }

            double maxTime = 0;
            foreach (var s in liveSamples)
                if (s != null && s.time > maxTime) maxTime = s.time;

            currentRecording = new ThartTouchRecording
            {
                schemaVersion = 3,
                title = "Thart Recording " + DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                durationSeconds = maxTime,
                // 平板如果上报过分辨率就用它的，否则退回 1920×1080（绝不能用电竞屏幕的尺寸，
                // 否则旧数据里的像素坐标会被错误归一化，音符全挤在一起）
                screenWidth = tabletScreenWidth > 0 ? tabletScreenWidth : 1920,
                screenHeight = tabletScreenHeight > 0 ? tabletScreenHeight : 1080,
                // 百分比坐标相对 16:9 录入框；这边只有实时采样时用上报值兜底
                frameWidth = tabletFrameWidth > 0 ? tabletFrameWidth : 1920,
                frameHeight = tabletFrameHeight > 0 ? tabletFrameHeight : 1080,
                samples = liveSamples.ToArray()
            };

            activeSession = -1;
            recordingState = RecordingState.Idle;
            ConvertRecordingToNotes();
        }

        #region 极简 JSON 取值

        private static string JsonStr(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            string token = "\"" + key + "\"";
            int i = json.IndexOf(token, StringComparison.Ordinal);
            if (i < 0) return "";
            i = json.IndexOf(':', i + token.Length);
            if (i < 0) return "";
            i = json.IndexOf('"', i);
            if (i < 0) return "";
            int end = json.IndexOf('"', i + 1);
            if (end < 0) return "";
            return json.Substring(i + 1, end - i - 1);
        }

        private static double JsonNum(string json, string key, double fallback)
        {
            if (string.IsNullOrEmpty(json)) return fallback;
            string token = "\"" + key + "\"";
            int i = json.IndexOf(token, StringComparison.Ordinal);
            if (i < 0) return fallback;
            i = json.IndexOf(':', i + token.Length);
            if (i < 0) return fallback;
            i++;
            int end = i;
            while (end < json.Length &&
                   (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '+' || json[end] == '.' || json[end] == 'e' || json[end] == 'E'))
                end++;
            double v;
            if (double.TryParse(json.Substring(i, end - i),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out v))
                return v;
            return fallback;
        }

        #endregion

        #endregion

        #region Recording Control

        /// <summary>
        /// 开始录制：先确保音频已导入并推送到平板
        /// </summary>
        public void StartRecording()
        {
            if (armingTablet)
            {
                UpdateStatus("正在与平板同步，请稍候...");
                return;
            }
            if (recordingState != RecordingState.Idle)
            {
                UpdateStatus("正在录制或等待数据回传，请先停止");
                return;
            }

            if (server == null || server.ClientCount == 0)
            {
                UpdateStatus("没有已连接的平板设备");
                return;
            }

            if (!HasAudio)
            {
                UpdateStatus("请先导入音频（左侧「音频」面板 → 导入音频）");
                return;
            }

            // 平板还没有这首歌，先推送，推完再握手
            if (!IsAudioPushedToTablet)
            {
                UpdateStatus("先把音频推送到平板...");
                StartPushAudioToTablet(true);
                return;
            }

            ArmAndStart();
        }

        /// <summary>
        /// 第一步：通知平板做准备，等它确认就绪后再正式开始（避免两端不同步）
        /// </summary>
        private void ArmAndStart()
        {
            if (armingTablet) return;

            sessionId++;
            armingTablet = true;
            armRequestDsp = AudioSettings.dspTime;
            activeSession = -1;
            inCountdown = false;
            recordingDuration = 0;
            waitingForFullData = false;
            fullDataRetries = 0;
            liveSamples.Clear();
            samplesThisSession = 0;

            string payload = "{\"session\":" + sessionId
                + ",\"countdown\":" + countdownSeconds.ToString("F2")
                + ",\"duration\":" + (currentAudio != null ? currentAudio.length.ToString("F3") : "0.000")
                + ",\"audio\":\"" + EscapeJsonArg(audioFileName) + "\"}";

            server.BroadcastMessage(ThartMessageType.PrepareRecording, payload);
            UpdateStatus("正在与平板同步...");
        }

        /// <summary>
        /// 第二步：平板已就绪，正式进入录制（先走预备拍，数到 0 音频与录入同时开始）
        /// </summary>
        private void BeginRecordingInternal(int sess)
        {
            if (recordingState == RecordingState.Recording) return;

            activeSession = sess >= 0 ? sess : (++sessionId);

            PushUndo();

            recordingState = RecordingState.Recording;
            recordingDuration = 0;
            waitingForFullData = false;
            fullDataRetries = 0;
            liveSamples.Clear();
            samplesThisSession = 0;

            // 预备拍：先数 countdown 秒，数到 0 才同时开始播音频与录入。
            // 倒计时不属于录制时间，所以播放头在预备拍期间停在 0 不动。
            recordingClock = ThartRecordingClock.Start(AudioSettings.dspTime, countdownSeconds);
            inCountdown = countdownSeconds > 0.01f;

            songTime = 0;
            // 电脑端要不要一起出声：同样排期在原点上，预备拍期间不出声
            if (playAudioOnDesktop) StartPlaybackFrom(0, recordingClock.origin);

            string payload = "{\"session\":" + activeSession
                + ",\"countdown\":" + countdownSeconds.ToString("F2")
                + ",\"duration\":" + (currentAudio != null ? currentAudio.length.ToString("F3") : "0.000")
                + ",\"audio\":\"" + EscapeJsonArg(audioFileName) + "\"}";
            server.BroadcastMessage(ThartMessageType.StartRecording, payload);

            UpdateStatus(inCountdown
                ? ("预备拍 " + Mathf.RoundToInt(countdownSeconds) + " 秒，数到 0 才开始播放与录入")
                : "开始录制...");
        }

        public void StopRecording()
        {
            if (recordingState != RecordingState.Recording) return;

            recordingState = RecordingState.Stopping;
            inCountdown = false;

            // 停止播放
            StopPlayback();

            // 通知平板停止录制
            server.BroadcastMessage(ThartMessageType.StopRecording, "{\"session\":" + activeSession + "}");

            // 预备拍里就被停下：录制时间还是 0
            recordingDuration = recordingClock.Elapsed(AudioSettings.dspTime);

            // 继续接收实时采样，等平板回传完整数据
            waitingForFullData = true;
            stopRequestDsp = AudioSettings.dspTime;
            lastRequestDsp = stopRequestDsp;
            fullDataRetries = 0;

            UpdateStatus("录制停止，等待平板回传数据...");

            // 平板已断开就等不到回传了，直接用实时数据
            if (server == null || server.ClientCount == 0)
            {
                waitingForFullData = false;
                BuildRecordingFromLiveSamples();
            }
        }

        private static string EscapeJsonArg(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void ConvertRecordingToNotes()
        {
            if (currentRecording == null) return;

            convertedNotes = ThartChartBuilder.ConvertTouchToNotes(
                currentRecording, conversionConfig, out convertedLayout);

            int grid = autoSnapAfterRecording ? snapDivisor : 0;
            UpdateStatus("已转换 " + convertedNotes.Count + " 个音符，按触控位置分成 "
                + (convertedLayout != null ? convertedLayout.Count : 0) + " 列"
                + (grid > 0 ? "（吸附到 " + ThartChartBuilder.GridLabel(grid) + " 网格）" : "（未吸附）"));

            // 应用到铺面
            ApplyConvertedNotesToChart();
        }

        private void ApplyConvertedNotesToChart()
        {
            if (tempo == null) return;

            // 自由布局：列的位置来自触控数据本身，不套用固定轨道
            if (convertedLayout == null || convertedLayout.Count == 0)
            {
                if (currentRecording == null) return;
                TouchNoteLayout rebuilt;
                convertedNotes = ThartChartBuilder.ConvertTouchToNotes(
                    currentRecording, conversionConfig, out rebuilt);
                convertedLayout = rebuilt;
            }

            chart.paths = convertedLayout.paths;
            chart.sections = new[]
            {
                new SectionData
                {
                    startBeat = 0,
                    name = chart.sections != null && chart.sections.Length > 0
                        ? chart.sections[0].name : "Section 1",
                    placements = convertedLayout.placements
                }
            };

            int grid = autoSnapAfterRecording ? snapDivisor : 0;
            var notes = ThartChartBuilder.ConvertToChartNotes(
                convertedNotes, tempo, chart.ticksPerBeat, convertedLayout.pathIds, grid);
            chart.notes = notes;

            // 更新 endBeat
            if (notes.Length > 0)
            {
                float lastBeat = notes[notes.Length - 1].tick / (float)chart.ticksPerBeat;
                chart.endBeat = Mathf.Max(chart.endBeat, lastBeat + 4);
            }

            selectedPathIndex = Mathf.Clamp(selectedPathIndex, 0, Mathf.Max(0, chart.paths.Length - 1));

            UpdateStatus("已应用 " + notes.Length + " 个音符到铺面（" + chart.paths.Length + " 列，位置来自触控）"
                + (grid > 0 ? "，网格 " + ThartChartBuilder.GridLabel(grid) : ""));
            MarkDirty();
        }

        /// <summary>把当前触控录制（全屏数据）另存为 JSON</summary>
        public void SaveTouchRecording()
        {
            if (currentRecording == null)
            {
                UpdateStatus("还没有录制数据可保存");
                return;
            }
            try
            {
                string dir = Path.Combine(Application.dataPath, "../Charts");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "thart_touch_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
                ThartChartIO.SaveRecording(currentRecording, path);
                UpdateStatus("触控数据（全屏）已保存: " + Path.GetFileName(path));
            }
            catch (Exception e)
            {
                UpdateStatus("保存触控数据失败: " + e.Message);
            }
        }

        #endregion

        #region Playback

        /// <summary>
        /// 从 <paramref name="startTime"/> 开始走带。
        /// <paramref name="scheduleDsp"/> 给一个未来的 dsp 时刻时，音频被排期到那个时刻才出声
        /// （预备拍就是这样把电脑端的音频对齐到录制原点的）；缺省是「现在 + 一点缓冲」。
        /// </summary>
        private void StartPlaybackFrom(double startTime, double scheduleDsp = -1)
        {
            double duration = GetDuration();
            if (duration <= 0) return;

            transportSongStart = Mathf.Clamp((float)startTime, 0f, (float)duration);
            playing = true;

            if (audioSource == null || currentAudio == null)
            {
                // 没有音频（例如只加载了谱面）：用 dsp 时钟静默走带，预览依然可用
                transportSilent = true;
                transportDspStart = scheduleDsp > 0 ? scheduleDsp : AudioSettings.dspTime;
                return;
            }

            transportSilent = false;
            transportSongStart = Mathf.Clamp((float)startTime, 0f, currentAudio.length);

            audioSource.Stop();
            audioSource.clip = currentAudio;
            audioSource.time = (float)transportSongStart;

            // 起点只记录一次：先排期，再用同一个 dsp 时刻当时间原点
            double startDsp = scheduleDsp > 0 ? scheduleDsp : AudioSettings.dspTime + 0.05;
            audioSource.PlayScheduled(startDsp);
            transportDspStart = startDsp;
        }

        private void StopPlayback()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
            }
            playing = false;
            transportSilent = false;
        }

        private void TogglePlayback()
        {
            if (playing)
            {
                StopPlayback();
            }
            else
            {
                StartPlaybackFrom(songTime);
            }
        }

        private double GetDuration()
        {
            double chartDuration = (tempo == null || chart == null) ? 0 : tempo.SecondsAtBeat(chart.endBeat);
            double audioDuration = currentAudio != null ? currentAudio.length : 0;
            return Math.Max(chartDuration, audioDuration);
        }

        #endregion

        #region Chart Management

        private void LoadOrCreateChart()
        {
            // 创建默认铺面
            chart = new ChartData
            {
                version = 1,
                title = "Untitled",
                author = "Thart",
                ticksPerBeat = 480,
                endBeat = 64,
                approachSeconds = 3.4f,
                tempos = new[] { new TempoData { tick = 0, bpm = 120 } },
                paths = new PathData[0],
                sections = new SectionData[0],
                cameraKeys = new[] { new CameraKey { beat = 0, distance = 25, height = 6, fov = 53 } },
                notes = new NoteData[0]
            };

            EnsureMinimumPaths(1);
            RebuildTempoMap();
        }

        /// <summary>
        /// 保证铺面至少有 count 列路径（仅用于空白铺面；录制后列数由触控位置决定）
        /// </summary>
        private void EnsureMinimumPaths(int count)
        {
            count = Mathf.Max(1, count);
            if (chart.paths == null || chart.paths.Length < count)
            {
                var newPaths = new List<PathData>(chart.paths ?? new PathData[0]);
                for (int i = newPaths.Count; i < count; i++)
                {
                    newPaths.Add(new PathData
                    {
                        id = "p" + i,
                        roll = 0
                    });
                }
                chart.paths = newPaths.ToArray();
            }

            // 确保有一个 section，且每个路径都有摆放
            if (chart.sections == null || chart.sections.Length == 0)
            {
                var placements = new List<PathPlacement>();
                for (int i = 0; i < chart.paths.Length; i++)
                {
                    placements.Add(new PathPlacement
                    {
                        pathId = chart.paths[i].id,
                        x = 0f,
                        y = 0f,
                        bend = 0f,
                        lift = 0f
                    });
                }
                chart.sections = new[]
                {
                    new SectionData
                    {
                        startBeat = 0,
                        name = "Section 1",
                        placements = placements.ToArray()
                    }
                };
            }
        }

        private void RebuildTempoMap()
        {
            if (chart?.tempos != null)
            {
                tempo = new TempoMap(chart.tempos, chart.ticksPerBeat);
            }
        }

        private void MarkDirty()
        {
            // 标记为已修改
        }

        #endregion

        #region Undo/Redo

        private void PushUndo()
        {
            if (chart == null) return;
            undoStack.Push(JsonUtility.ToJson(chart));
            redoStack.Clear();
            TrimHistory(undoStack);
        }

        /// <summary>
        /// 只保留最新的 N 步。
        /// 注意 Stack 的枚举顺序是「栈顶在前」，也就是最新在前，
        /// 所以不能直接 Pop 来裁剪 —— 那样会把最新的状态丢掉，只留下最老的。
        /// </summary>
        private static void TrimHistory(Stack<string> stack)
        {
            if (stack.Count <= MaxUndoSteps) return;

            var newestFirst = new List<string>(stack);
            newestFirst.RemoveRange(MaxUndoSteps, newestFirst.Count - MaxUndoSteps);

            stack.Clear();
            for (int i = newestFirst.Count - 1; i >= 0; i--)
                stack.Push(newestFirst[i]);
        }

        private void Undo()
        {
            if (undoStack.Count == 0) return;
            redoStack.Push(JsonUtility.ToJson(chart));
            chart = JsonUtility.FromJson<ChartData>(undoStack.Pop());
            RebuildTempoMap();
            ClearNoteSelection();
            UpdateStatus("撤销");
        }

        private void Redo()
        {
            if (redoStack.Count == 0) return;
            undoStack.Push(JsonUtility.ToJson(chart));
            TrimHistory(undoStack);
            chart = JsonUtility.FromJson<ChartData>(redoStack.Pop());
            RebuildTempoMap();
            ClearNoteSelection();
            UpdateStatus("重做");
        }

        #endregion

        #region Helpers

        private void UpdateStatus(string text)
        {
            statusText = text;
            Debug.Log("[Thart] " + text);
        }

        private void EnqueueMainThread(Action action)
        {
            lock (queueLock)
            {
                mainThreadQueue.Enqueue(action);
            }
        }

        private float CurrentBeat => tempo == null ? 0 : (float)tempo.BeatAtSeconds(songTime);

        private int CurrentTick => Mathf.RoundToInt(CurrentBeat * chart.ticksPerBeat);

        #endregion
    }
}
