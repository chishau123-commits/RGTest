using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RingGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RingGame.Runtime
{
    /// <summary>Android first, single-scene prototype. All author data comes through Core.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        enum SessionState { Ready, Playing, Paused, Interrupted, Complete, Error }
        SongClock clock;
        InputCollector input;
        DiagnosticLog log;
        GameHud hud;
        CompiledChart chart;
        JudgeEngine judge;
        AudioClip songAudio;
        Camera gameCamera;
        Transform stage;
        Material lineMaterial;
        Sprite disc;
        readonly PoseHistory poses = new PoseHistory(.5);
        readonly List<CapturedTouch> captured = new List<CapturedTouch>();
        readonly List<JudgeContact> contacts = new List<JudgeContact>();
        readonly Dictionary<string, NoteView> views = new Dictionary<string, NoteView>();
        readonly List<Burst> bursts = new List<Burst>();
        readonly HashSet<string> previewed = new HashSet<string>();
        SessionState state;
        float inputMs, visualMs, displayMs;
        int perfect, great, good, miss;
        bool preview, smoke, capturedScreenshot;
        long frame, previewContact = 2000000000;
        double lastUpdateTime, lastFlushTime;
        string message = "", chartHash = "", chartSource = "", capturePath = "";

        sealed class Burst { public LineRenderer Line; public Vector2 Center; public double Started; public Color Color; }
        [Serializable] sealed class InputRecord
        {
            public long contactId, frameId;
            public int fingerId, epoch;
            public double eventInputTime, rawSongSeconds, correctedSongSeconds, selectedVisualSongSeconds;
            public Vector2 screen, chartPoint;
            public string source, decision;
        }
        [Serializable] sealed class PoseRecord
        {
            public long frameId;
            public double submittedInputTime, visualSongSeconds;
            public Vector2 position;
            public float rotation, scale;
            public Rect viewport;
        }
        [Serializable] sealed class ResultRecord
        { public string noteId, grade; public long contactId; public double errorSeconds; public float spatialDistance; public bool preview; }
        [Serializable] sealed class SessionRecord
        {
            public string chartHash, source, schemaVersion;
            public int requiredTouches, inputStreamSlots, maxObservedTouches;
            public float inputCalibrationMs, visualCalibrationMs, estimatedDisplayLatencyMs;
            public bool preview;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindObjectOfType<GameBootstrap>() == null)
                new GameObject("Ring Game").AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            try
            {
                inputMs = PlayerPrefs.GetFloat("ring.inputMs", 0);
                visualMs = PlayerPrefs.GetFloat("ring.visualMs", 0);
                displayMs = PlayerPrefs.GetFloat("ring.displayMs", 20);
                smoke = HasArgument("-ringSmoke");
                if (smoke || Application.isEditor) Application.runInBackground = true;
                capturePath = Argument("-ringCapture");
                hud = new GameHud();
                CreateStage();
                clock = new SongClock(gameObject.AddComponent<AudioSource>());
                input = new InputCollector(clock); input.Enable();
                log = new DiagnosticLog("{\"prototype\":true,\"displayLatencyMeasured\":false}", input.DeviceTouchSlots);
                songAudio = Resources.Load<AudioClip>("Audio/metronome");
                if (songAudio == null) throw new InvalidOperationException("Missing Resources/Audio/metronome.wav");
                hud.Play.Action = PlayPause;
                hud.Restart.Action = Restart;
                hud.Preview.Action = () => { if (state != SessionState.Playing) { preview = !preview; Restart(); } };
                hud.Calibration[0].Action = () => AdjustCalibration(0, -5);
                hud.Calibration[1].Action = () => AdjustCalibration(0, 5);
                hud.Calibration[2].Action = () => AdjustCalibration(1, -5);
                hud.Calibration[3].Action = () => AdjustCalibration(1, 5);
                hud.Calibration[4].Action = () => AdjustCalibration(2, -5);
                hud.Calibration[5].Action = () => AdjustCalibration(2, 5);
                Restart();
                lastUpdateTime = InputState.currentTime;
                if (smoke) { preview = true; PlayPause(); }
                Debug.Log("RING_READY diagnostics=" + log.FilePath);
            }
            catch (Exception exception) { Fail(exception); }
        }

        void CreateStage()
        {
            // The gameplay camera only clears its letterboxed viewport. Clear the whole screen
            // first so overlay HUD text never leaves pixels from the preceding frame in the bars.
            var backdrop = new GameObject("Background Camera").AddComponent<Camera>();
            backdrop.clearFlags = CameraClearFlags.SolidColor;
            backdrop.backgroundColor = new Color(.006f,.009f,.015f);
            backdrop.cullingMask = 0; backdrop.depth = -10;
            var cameraObject = new GameObject("Gameplay Camera"); gameCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0, 0, -10);
            gameCamera.orthographic = true; gameCamera.orthographicSize = 50;
            gameCamera.clearFlags = CameraClearFlags.SolidColor; gameCamera.backgroundColor = new Color(.018f, .03f, .055f);
            stage = new GameObject("Chart-space stage").transform;
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[128*128];
            for (int y=0;y<128;y++) for (int x=0;x<128;x++)
            {
                float distance = new Vector2(x-63.5f,y-63.5f).magnitude;
                pixels[y*128+x] = new Color(1,1,1, Mathf.Clamp01(63.5f-distance));
            }
            texture.SetPixels(pixels); texture.Apply();
            disc = Sprite.Create(texture, new Rect(0,0,128,128), Vector2.one*.5f,128);
            for (int i=-4;i<=4;i++)
            {
                var grid = NoteView.Line("Stage grid", stage, lineMaterial, new Color(.12f,.22f,.3f,.45f),.15f,-3);
                grid.loop = false; grid.positionCount = 2;
                grid.SetPositions(new[] {new Vector3(i*20,-50,0),new Vector3(i*20,50,0)});
            }
            for (int i=-2;i<=2;i++)
            {
                var grid = NoteView.Line("Stage grid", stage, lineMaterial, new Color(.12f,.22f,.3f,.45f),.15f,-3);
                grid.loop = false; grid.positionCount = 2;
                grid.SetPositions(new[] {new Vector3(-89,i*20,0),new Vector3(89,i*20,0)});
            }
        }

        void LoadChart()
        {
            string path = Path.Combine(Application.persistentDataPath, "prototype-chart.json");
            string json;
            if (File.Exists(path)) { json = File.ReadAllText(path); chartSource = path; }
            else
            {
                var asset = Resources.Load<TextAsset>("Charts/prototype");
                if (asset == null) throw new InvalidOperationException("Missing Resources/Charts/prototype.json");
                json = asset.text; chartSource = "Resources/Charts/prototype";
            }
            chart = ChartCompiler.Compile(ChartJson.Parse(json));
            if (chart.Notes.Any(note => note.HitSeconds < 0 || note.HitSeconds >= songAudio.length))
                throw new InvalidOperationException("Note hit time must be within the preloaded metronome [0, length).");
            using (var sha = SHA256.Create()) chartHash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant();
            foreach (var view in views.Values) view.Destroy(); views.Clear();
            foreach (var note in chart.Notes) views.Add(note.Id, new NoteView(stage, note, disc, lineMaterial));
            Debug.Log("RING_CHART source=" + chartSource + " sha256=" + chartHash);
        }

        void Restart()
        {
            try
            {
                if (clock.IsRunning) clock.Pause();
                LoadChart(); judge = new JudgeEngine(chart);
                input.Clear(); poses.Clear(); previewed.Clear();
                perfect = great = good = miss = 0;
                state = SessionState.Ready; message = "";
                foreach (var burst in bursts) Destroy(burst.Line.gameObject); bursts.Clear();
                RecordSession("ready");
            }
            catch (Exception exception) { Fail(exception); }
        }

        public void StartPreview()
        {
            if (input == null || hud == null) return;
            preview = true; Restart(); PlayPause();
        }

        void PlayPause()
        {
            if (state == SessionState.Error) return;
            if (state == SessionState.Playing)
            {
                clock.Pause(); input.Clear(); poses.Clear(); state = SessionState.Paused;
                log.Record("pause", "{}"); log.Flush(); return;
            }
            if (state == SessionState.Complete) Restart();
            if (state == SessionState.Error) return;
            if (Application.isMobilePlatform && !preview &&
                (input.DeviceTouchSlots < chart.RequiredTouches || input.MaxConcurrentObserved < chart.RequiredTouches))
            {
                message = "Before PLAY: touch with " + chart.RequiredTouches + " fingers to verify this device.";
                return;
            }
            if (state == SessionState.Ready) clock.Begin(songAudio); else clock.Resume();
            input.Clear(); poses.Clear(); state = SessionState.Playing; message = "";
            RecordSession("play");
        }

        void AdjustCalibration(int type, float delta)
        {
            if (state == SessionState.Playing) return;
            if (type == 0) inputMs = Mathf.Clamp(inputMs+delta,-200,200);
            else if (type == 1) visualMs = Mathf.Clamp(visualMs+delta,-200,200);
            else displayMs = Mathf.Clamp(displayMs+delta,0,100);
            PlayerPrefs.SetFloat("ring.inputMs",inputMs); PlayerPrefs.SetFloat("ring.visualMs",visualMs);
            PlayerPrefs.SetFloat("ring.displayMs",displayMs); PlayerPrefs.Save();
            Restart();
        }

        void Update()
        {
            if (hud == null) return;
            hud.Layout(state != SessionState.Playing);
            UpdateHud();
            // A rejected external chart must still allow the RESTART button after it is repaired.
            if (input == null) return;
            double now = InputState.currentTime;
            if (state == SessionState.Playing && now-lastUpdateTime > .25)
                Interrupt("Frame gap >250ms; resume explicitly. No enlarged hit windows.");
            lastUpdateTime = now;
            captured.Clear();
            if (!Application.isMobilePlatform && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                input.CapturePointerDown(-1, now, Mouse.current.position.ReadValue(), "desktop-mouse-processing-time");
            input.Drain(captured);
            contacts.Clear();
            foreach (var touch in captured)
            {
                var entry = new InputRecord {contactId=touch.ContactId, fingerId=touch.FingerId,epoch=touch.Epoch,
                    eventInputTime=touch.InputTime,rawSongSeconds=touch.RawSongSeconds,
                    correctedSongSeconds=touch.RawSongSeconds+inputMs*.001,screen=touch.ScreenPosition,source=touch.Source, frameId=-1};
                if (hud.Consume(touch.ScreenPosition)) entry.decision = "ui-or-outside-playfield";
                else if (state != SessionState.Playing || preview || touch.Epoch != clock.Epoch || !touch.EligibleForGameplay) entry.decision = "inactive-preview-or-stale-epoch";
                else if (!poses.TrySelect(touch.InputTime,displayMs*.001,out var selected)) entry.decision = "no-render-history";
                else if (!selected.Viewport.Contains(touch.ScreenPosition)) entry.decision = "outside-recorded-viewport";
                else
                {
                    entry.frameId=selected.FrameId; entry.selectedVisualSongSeconds=selected.VisualSongSeconds;
                    entry.chartPoint = selected.ChartToScreen.inverse.MultiplyPoint3x4(touch.ScreenPosition);
                    contacts.Add(new JudgeContact(touch.ContactId,touch.RawSongSeconds,entry.chartPoint));
                    entry.decision = "candidate";
                }
                log.Record("input",JsonUtility.ToJson(entry));
            }
            if (state == SessionState.Playing)
            {
                double song = clock.NowSeconds;
                if (preview)
                {
                    foreach (var note in chart.Notes)
                        if (song >= note.HitSeconds && previewed.Add(note.Id))
                            contacts.Add(new JudgeContact(++previewContact,note.HitSeconds,note.Target));
                }
                // Queued input MUST be consumed before the calibrated timeout scan.
                ApplyResults(judge.ProcessBatch(contacts,preview ? 0 : inputMs*.001));
                ApplyResults(judge.Expire(song+(preview ? 0 : inputMs*.001)));
                double finish = Math.Max(songAudio.length,
                    chart.Notes.Max(note => note.HitSeconds)+JudgeEngine.GoodWindowSeconds-(preview ? 0 : inputMs*.001)+.002);
                if (song >= finish)
                {
                    clock.Pause(); state = SessionState.Complete;
                    RecordSession("complete"); log.Flush();
                    if (smoke) { Debug.Log("RING_SMOKE_PASS notes="+chart.Notes.Length+" hit="+(perfect+great+good)+" miss="+miss); Application.Quit(miss==0 ? 0 : 1); }
                }
                if (smoke && !capturedScreenshot && song >= 10.25 && !string.IsNullOrEmpty(capturePath))
                { ScreenCapture.CaptureScreenshot(capturePath); capturedScreenshot=true; }
            }
        }

        void LateUpdate()
        {
            if (chart == null || hud == null || state == SessionState.Error) return;
            double visual = state == SessionState.Ready ? 0 : clock.NowSeconds+visualMs*.001;
            var pose = CameraEvaluator.Evaluate(chart,visual);
            stage.position = pose.Position; stage.rotation = Quaternion.Euler(0,0,pose.RotationDegrees);
            stage.localScale = Vector3.one*pose.Scale;
            var viewport = hud.Viewport;
            gameCamera.rect = new Rect(viewport.x/Screen.width,viewport.y/Screen.height,viewport.width/Screen.width,viewport.height/Screen.height);
            foreach (var note in chart.Notes) views[note.Id].Draw(note,visual,judge.GetState(note.Id), Math.Max(.1,.1-inputMs*.001+visualMs*.001));
            for (int i=bursts.Count-1;i>=0;i--)
            {
                float age = (float)(Time.realtimeSinceStartupAsDouble-bursts[i].Started);
                if (age > .4f) { Destroy(bursts[i].Line.gameObject); bursts.RemoveAt(i); continue; }
                var burst = bursts[i]; NoteView.SetCircle(burst.Line,burst.Center,6+age*22);
                var color=burst.Color; color.a=1-age/.4f; burst.Line.startColor=burst.Line.endColor=color;
            }
            float pixelsPerUnit=viewport.height/100;
            var chartToScreen = Matrix4x4.TRS(viewport.center,Quaternion.identity,Vector3.one*pixelsPerUnit)
                * Matrix4x4.TRS(pose.Position,Quaternion.Euler(0,0,pose.RotationDegrees),Vector3.one*pose.Scale);
            var snapshot = new PoseSnapshot {FrameId=++frame,SubmittedInputTime=InputState.currentTime,
                VisualSongSeconds=visual,Viewport=viewport,ChartToScreen=chartToScreen};
            poses.Add(snapshot);
            if (state == SessionState.Playing)
                log.Record("pose",JsonUtility.ToJson(new PoseRecord {frameId=frame,submittedInputTime=snapshot.SubmittedInputTime,
                    visualSongSeconds=visual,position=pose.Position,rotation=pose.RotationDegrees,scale=pose.Scale,viewport=viewport}));
            if (snapshot.SubmittedInputTime-lastFlushTime >= 1)
            {
                RecordSession("device_status"); log.Flush(); lastFlushTime=snapshot.SubmittedInputTime;
            }
        }

        void ApplyResults(List<JudgeResult> results)
        {
            foreach (var result in results)
            {
                if (result.Grade == JudgeGrade.Perfect) perfect++;
                else if (result.Grade == JudgeGrade.Great) great++;
                else if (result.Grade == JudgeGrade.Good) good++;
                else miss++;
                var color=result.Grade==JudgeGrade.Miss ? new Color(1,.25f,.4f) : new Color(.35f,1,.85f);
                var note=chart.Notes.First(n=>n.Id==result.NoteId);
                var line=NoteView.Line("Hit burst",stage,lineMaterial,color,.6f,10);
                bursts.Add(new Burst {Line=line,Center=note.Target,Started=Time.realtimeSinceStartupAsDouble,Color=color});
                message=result.Grade+"  "+Math.Round(result.ErrorSeconds*1000)+" ms";
                log.Record("judgement",JsonUtility.ToJson(new ResultRecord {noteId=result.NoteId,contactId=result.ContactId,
                    grade=result.Grade.ToString(),errorSeconds=result.ErrorSeconds,spatialDistance=float.IsNaN(result.SpatialDistance) ? -1 : result.SpatialDistance,preview=preview}));
            }
        }

        void UpdateHud()
        {
            hud.Status.text=state+"  "+(clock==null ? "" : clock.NowSeconds.ToString("F2")+" s");
            hud.Play.Text.text=state==SessionState.Playing ? "PAUSE" : state==SessionState.Paused||state==SessionState.Interrupted ? "RESUME" : "PLAY";
            hud.Preview.Text.text=preview ? "PREVIEW: ON" : "PREVIEW: OFF";
            hud.Score.text=$"PERFECT {perfect}    GREAT {great}    GOOD {good}    MISS {miss}      {(preview ? "AUTO PREVIEW" : "TOUCH PLAY")}";
            hud.Hint.text=message.Length>0 ? message : "Tap the receiving circle when edges align. Cyan: shrink / Orange: arrival.";
            hud.Configuration.text=$"Input {inputMs:+0;-0;0}ms  /  Visual {visualMs:+0;-0;0}ms  /  Display estimate {displayMs:0}ms (unmeasured)   |   touches observed {input?.MaxConcurrentObserved ?? 0} / stream slots {input?.DeviceTouchSlots ?? 0}";
        }

        void RecordSession(string type)
        {
            log.Record(type,JsonUtility.ToJson(new SessionRecord {chartHash=chartHash,source=chartSource,schemaVersion=ChartCompiler.SchemaVersion,
                requiredTouches=chart.RequiredTouches,inputStreamSlots=input.DeviceTouchSlots,maxObservedTouches=input.MaxConcurrentObserved,
                inputCalibrationMs=inputMs,visualCalibrationMs=visualMs,estimatedDisplayLatencyMs=displayMs,preview=preview}));
        }
        void Interrupt(string reason)
        {
            if (state!=SessionState.Playing) return;
            clock.Pause(); input.Clear(); poses.Clear(); state=SessionState.Interrupted; message=reason;
            log.Record("interrupted","{\"reason\":\""+reason+"\"}"); log.Flush();
        }
        void OnApplicationPause(bool paused) { if (paused) Interrupt("Application paused; resume explicitly."); }
        void OnApplicationFocus(bool focus) { if (!focus && !smoke && !Application.isEditor) Interrupt("Application lost focus; resume explicitly."); }
        void Fail(Exception exception)
        {
            state=SessionState.Error; message=exception.Message; Debug.LogException(exception);
            foreach (var view in views.Values) view.Destroy(); views.Clear();
            clock?.Pause(); log?.Record("error",JsonUtility.ToJson(new ErrorRecord {message=exception.Message})); log?.Flush();
            if (smoke) Application.Quit(1);
        }
        [Serializable] sealed class ErrorRecord { public string message; }
        void OnDestroy()
        {
            input?.Disable(); log?.Dispose();
            if (lineMaterial!=null) Destroy(lineMaterial);
            if (disc!=null) { Destroy(disc.texture); Destroy(disc); }
        }
        static bool HasArgument(string key) { return Environment.GetCommandLineArgs().Contains(key); }
        static string Argument(string key)
        {
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,key);
            return index>=0&&index+1<args.Length ? args[index+1] : "";
        }
    }
}
