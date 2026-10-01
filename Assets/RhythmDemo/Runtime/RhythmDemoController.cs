using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace GeometryRhythm
{
    /// <summary>Composition root for the playable demo. JSON/clock/judgment are separate from
    /// views so the later chart editor can reuse evaluation without simulating a whole song.</summary>
    public sealed partial class RhythmDemoController : MonoBehaviour
    {
        [Header("JSON chart (empty = bundled demo)")]
        public TextAsset chartOverride;
        public Camera demoCamera;
        [Header("Application flow")]
        [TextArea(1,3)] public string gameTitle = "GEOMETRY\nRHYTHM";
        public bool showFrontend = true;
        [HideInInspector] public bool startInMenu;
        [Header("Play options")]
        public bool autoPlay = true;
        [Range(-200,200)] public float inputOffsetMilliseconds;
        [Range(0,1)] public float musicVolume = .55f;
        public ChartData Chart { get; private set; }
        public JudgementEngine Engine { get; private set; }
        public SpatialDirector Spatial { get; private set; }
        public double CurrentTime => clock == null ? 0 : clock.Time;
        public double Duration { get; private set; }
        public bool Ready { get; private set; }
        public string LoadError => fatal;
        public bool HasFinished { get; private set; }
        public bool IsPractice { get; private set; }
        public bool IsPaused => clock == null || clock.Paused;
        public float NoteSpeed => Spatial == null ? NoteScrollSettings.Load() : Spatial.NoteSpeedMultiplier;
        public float NoteSpawnPosition => Spatial == null ? NoteSpawnSettings.Load() : Spatial.NoteSpawnPercent;
        public void SetNoteSpawnPosition(float value)
        {
            float sanitized=NoteSpawnSettings.Sanitize(value);
            NoteSpawnSettings.Save(sanitized);
            if(Spatial==null) return;
            Spatial.SetNoteSpawnPosition(sanitized);
            EvaluateVisuals(clock==null?0:clock.Time);
        }
        /// <summary>Personal reading speed. It applies to the running chart immediately and is
        /// stored for every other chart, the pause sheet and the song list both write it.</summary>
        public void SetNoteSpeed(float value)
        {
            float sanitized=NoteScrollSettings.Sanitize(value);
            NoteScrollSettings.Save(sanitized);
            if(Spatial==null) return;
            Spatial.SetNoteSpeed(sanitized);
            // Re-seat the visible notes at the new window; ticks, offsets and judgement
            // times are untouched, and the audio clock is not restarted.
            EvaluateVisuals(clock==null?0:clock.Time);
        }
        public Action ReturnToSongs;
        TempoMap tempo;
        SongClock clock;
        AudioSource source;
        VisualLibrary library;
        StageVisuals stage;
        AuthoredVisualDirector authoredVisuals;
        DemoHud hud;
        Transform notesRoot;
        Transform pathRoot;
        bool menuMode, waitForPointerRelease;
        readonly Stack<NoteVisual> pool=new Stack<NoteVisual>();
        readonly Dictionary<string,PathVisual> paths=new Dictionary<string,PathVisual>();
        readonly HashSet<int> uiPointers=new HashSet<int>();
        readonly List<Vector2> contacts=new List<Vector2>(10);
        readonly List<Vector2> downs=new List<Vector2>(10);
        readonly List<RaycastResult> uiHits=new List<RaycastResult>();
        readonly List<HitPulse> pulses=new List<HitPulse>();
        readonly Vector2[] judgementPolygon=new Vector2[24];
        double visualTime;
        Coroutine displayFrameRateRoutine;
        MaterialPropertyBlock pulseBlock;
        bool smoke, capture, ownsAudio;
        string fatal;
        sealed class HitPulse { public Transform Transform; public Renderer Renderer; public double Start=-100; public Color Color; }

        void Start()
        {
            // The existing demo scene is also the application entry point. Child sessions
            // opt out of this bootstrap; smoke mode keeps its direct gameplay entry.
            // -demoBgaSmoke 同样要直进玩法：它测的是视频 BGA 这条通路，前端会接管画面与时钟。
            var commandLine=Environment.GetCommandLineArgs();
            if(showFrontend && Array.IndexOf(commandLine,"-demoSmoke")<0 && Array.IndexOf(commandLine,"-demoBgaSmoke")<0 && Array.IndexOf(commandLine,"-demoBgaClip")<0)
            {
                gameObject.AddComponent<RhythmFrontend>().Configure(chartOverride,inputOffsetMilliseconds,musicVolume,gameTitle);
                enabled=false;
                return;
            }
            try { Initialize(); }
            catch(Exception e) { fatal=e.Message; Debug.LogException(e); }
        }
        void Initialize()
        {
            // Unity native objects must be created on the main thread, not in field initializers.
            pulseBlock=new MaterialPropertyBlock();
            var args=Environment.GetCommandLineArgs();
            smoke=Array.IndexOf(args,"-demoSmoke")>=0;
            // BGA 冒烟也要走 smoke 的路径：不读快捷键、不收集指针，时钟由探针自己控制
            if(Array.IndexOf(args,"-demoBgaSmoke")>=0) smoke=true;
            // 抓帧做动图也要直进玩法：它同样靠程序定位，不走前端
            if(Array.IndexOf(args,"-demoBgaClip")>=0) smoke=true;
            string chartPath=Argument(args,"-demoChart");
            // A session that was handed a chart owns it: the frontend assigns one chart per
            // song, so the command-line file is only the fallback for the plain demo scene.
            string json=chartOverride!=null?chartOverride.text
                :(chartPath!=null?File.ReadAllText(chartPath):Resources.Load<TextAsset>("Charts/geometry-demo").text);
            Chart=ChartLoader.Parse(json);tempo=new TempoMap(Chart.tempos,Chart.ticksPerBeat);
            Duration=tempo.SecondsAtBeat(Chart.endBeat);
            // Desktop: remove the old 120 FPS cap and do not wait for vertical sync.
            // Mobile: -1 means 30 FPS, so request the fastest mode the panel advertises.
            Application.targetFrameRate=Application.isMobilePlatform?MobileRefreshRate():-1;
            if(Application.isMobilePlatform) RefreshDisplayFrameRate();
            QualitySettings.vSyncCount=0;
            OnDemandRendering.renderFrameInterval=1; // Render every update; no frame skipping.
            QualitySettings.antiAliasing=4; // Keep the thin 3D rings and paths clean.
            Screen.sleepTimeout=SleepTimeout.NeverSleep;
            library=new VisualLibrary();
            InitializePalette();
            RenderSettings.skybox=library.Sky;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=new Color(.965f,.945f,.91f);RenderSettings.fogStartDistance=15;RenderSettings.fogEndDistance=125;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.72f,.715f,.70f);RenderSettings.ambientIntensity=1;
            if(demoCamera==null)
            {
                var c=new GameObject("Demo Camera",typeof(Camera),typeof(AudioListener));c.transform.SetParent(transform,false);
                demoCamera=c.GetComponent<Camera>();c.tag="MainCamera";
            }
            demoCamera.clearFlags=CameraClearFlags.Skybox;demoCamera.nearClipPlane=.15f;demoCamera.farClipPlane=220;
            demoCamera.allowHDR=true;demoCamera.allowMSAA=true;
            demoCamera.allowDynamicResolution=false; // Keep native output resolution for clarity.
            var lightObject=new GameObject("Warm daylight",typeof(Light));lightObject.transform.SetParent(transform,false);
            lightObject.transform.rotation=Quaternion.Euler(45,-32,0);
            var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.intensity=.72f;
            light.color=new Color(1,.985f,.96f);light.shadows=LightShadows.Soft;light.shadowStrength=.2f;
            Spatial=new SpatialDirector(Chart,tempo,NoteScrollSettings.Load());
            var worldRoot=new GameObject("Abstract world / real meshes").transform;worldRoot.SetParent(transform,false);
            stage=new StageVisuals(worldRoot,library,Spatial);
            authoredVisuals=new AuthoredVisualDirector(worldRoot,Chart,tempo,Spatial,demoCamera);
            LoadVideoBga(chartPath);
            notesRoot=new GameObject("Note pool").transform;notesRoot.SetParent(transform,false);
            pathRoot=new GameObject("3D paths").transform;pathRoot.SetParent(transform,false);
            foreach(var path in Chart.paths) paths.Add(path.id,new PathVisual(path.id,pathRoot,library));
            // Prewarm to the actual maximum number of notes in an approach window, not a fixed path count.
            Engine=new JudgementEngine(Chart,tempo);
            int maximum=0,left=0;
            for(int right=0;right<Engine.Notes.Length;right++)
            {
                // A distance gate can span different times on eased Z tracks.
                // Evaluate the actual gate at the last pending instant of the left
                // note instead of assuming a constant approach duration.
                while(!Spatial.NoteInView(Engine.Notes[right].HitTime,Engine.Notes[left].HitTime+JudgementEngine.GoodWindow,JudgementEngine.GoodWindow)) left++;
                maximum=Math.Max(maximum,right-left+1);
            }
            for(int i=0;i<maximum+4;i++) pool.Push(new NoteVisual(notesRoot,library));
            // Prewarm the full distance first so widening the spawn setting later
            // does not allocate additional note visuals during play.
            Spatial.SetNoteSpawnPosition(NoteSpawnSettings.Load());
            for(int i=0;i<12;i++)
            {
                var r=VisualLibrary.MeshObject("Pooled hit ripple",notesRoot,library.Ring,library.Tap);
                r.gameObject.SetActive(false);pulses.Add(new HitPulse{Transform=r.transform,Renderer=r});
            }
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.volume=musicVolume;
            source.clip=string.IsNullOrEmpty(Chart.audioResource)?null:Resources.Load<AudioClip>(Chart.audioResource);
            if(!string.IsNullOrEmpty(Chart.audioResource) && source.clip==null) throw new FileNotFoundException("Audio resource not found: "+Chart.audioResource);
            if(source.clip==null) { source.clip=DemoSoundtrack.Create((float)Duration);ownsAudio=true; }
            clock=new SongClock(source,Duration,Chart.audioOffsetSeconds,AudioSyncSettings.Load()/1000.0);
            hud=new DemoHud(transform,TogglePause,Restart,ToggleAuto,ToggleMute,()=>ReturnToSongs?.Invoke(),
                ()=>NoteSpeed,SetNoteSpeed,()=>NoteSpawnPosition,SetNoteSpawnPosition,
                AudioSyncSettings.Load,value=>{AudioSyncSettings.Save(value);clock.SetOutputDelay(value/1000.0);});
            Engine.OnJudged=OnJudged;
            clock.Seek(0,startInMenu);Ready=true;
            if(startInMenu) SetMenuMode(true);
            if(Array.IndexOf(args,"-demoBgaClip")>=0) StartCoroutine(RunBgaClip(args));
            else if(Array.IndexOf(args,"-demoBgaSmoke")>=0) StartCoroutine(RunBgaSmoke(args));
            else if(smoke) StartCoroutine(SmokeCapture(args));
        }
        void Update()
        {
            if(!Ready) return;
            if(menuMode)
            {
                // Menus reuse the actual 3D environment, without advancing a chart or audio.
                double preview=Math.Min(Duration*.4,8)+Math.Sin(Time.unscaledTimeAsDouble*.08)*1.2;
                Spatial.EvaluateCamera(demoCamera,preview);stage.Evaluate(preview,tempo.BeatAtSeconds(preview));
                // The menu shows the world without advancing the chart: keep the authored
                // backdrops in sync with the preview time, and keep the 16:9 playfield lock.
                authoredVisuals?.EvaluateBackdrops(preview);
                UpdateVideoBga(preview,false);
                ApplyPlayfieldViewport();
                return;
            }
            if(!smoke) ReadShortcuts();
            double time=clock.Time;
            EvaluateVisuals(time);
            UpdateVideoBga(time,!clock.Paused);
            if(!clock.Paused && !smoke)
            {
                // A click/held finger used to start or resume must not hit an anywhere Note.
                if(waitForPointerRelease)
                {
                    contacts.Clear();downs.Clear();
                    if(Input.touchCount==0 && !Input.GetMouseButton(0)) waitForPointerRelease=false;
                }
                else CollectPointers();
                JudgePointers(time);
                if(time>=Duration)
                {
                    // Final notes also get resolved when the DSP clock is clamped at song end.
                    Engine.Advance(Duration+JudgementEngine.GoodWindow+.001,autoPlay);
                    HasFinished=true;clock.SetPaused(true);
                }
            }
            hud.Update(Engine,time,Duration,Spatial.Section(time),clock.Paused,autoPlay,source.mute,capture);
        }
        void JudgePointers(double time)
        {
            double inputTime=autoPlay?time:time+inputOffsetMilliseconds/1000.0;
            if(!autoPlay)
            {
                foreach(var point in downs) Engine.Tap(inputTime,n=>Inside(n,point));
                Engine.Drag(inputTime,n=>InsideAny(n),contacts.Count>0);
            }
            // Expiry and input share the calibrated clock, including on frames without input.
            Engine.Advance(inputTime,autoPlay);
        }
        void ReadShortcuts()
        {
            if(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if(Input.GetKeyDown(KeyCode.R)) Restart();
            if(Input.GetKeyDown(KeyCode.A)) ToggleAuto();
            if(Input.GetKeyDown(KeyCode.M)) ToggleMute();
            if(Input.GetKeyDown(KeyCode.RightArrow)) {IsPractice=true;Seek(clock.Time+8,clock.Paused);}
            if(Input.GetKeyDown(KeyCode.LeftArrow)) {IsPractice=true;Seek(clock.Time-8,clock.Paused);}
        }
        bool OverUI(Vector2 position)
        {
            if(EventSystem.current==null) return false;
            var pointer=new PointerEventData(EventSystem.current){position=position};uiHits.Clear();
            EventSystem.current.RaycastAll(pointer,uiHits);return uiHits.Count>0;
        }
        void CollectPointers()
        {
            contacts.Clear();downs.Clear();
            if(Input.touchCount>0)
            {
                for(int i=0;i<Input.touchCount;i++)
                {
                    Touch t=Input.GetTouch(i);
                    if(t.phase==TouchPhase.Began && OverUI(t.position)) uiPointers.Add(t.fingerId);
                    if(t.phase==TouchPhase.Ended || t.phase==TouchPhase.Canceled) { uiPointers.Remove(t.fingerId);continue; }
                    if(uiPointers.Contains(t.fingerId)||OverUI(t.position)) continue;
                    contacts.Add(t.position);if(t.phase==TouchPhase.Began) downs.Add(t.position);
                }
            }
            else
            {
                if(Input.GetMouseButtonDown(0) && OverUI(Input.mousePosition)) uiPointers.Add(-1);
                if(!Input.GetMouseButton(0)) uiPointers.Remove(-1);
                if(Input.GetMouseButton(0) && !uiPointers.Contains(-1) && !OverUI(Input.mousePosition))
                { contacts.Add(Input.mousePosition);if(Input.GetMouseButtonDown(0)) downs.Add(Input.mousePosition); }
            }
        }
        bool Inside(RuntimeNote note,Vector2 point)
        {
            // A finger does not aim like a mouse. Keep the margin proportional to
            // the actual camera viewport; the former 20 px cap shrank it on tablets.
            float shortSide=Mathf.Min(demoCamera.pixelWidth,demoCamera.pixelHeight);
            float padding=Mathf.Max(8,shortSide*.03f);
            // Preserve direct touches on the visible ring, including its hollow centre.
            if(note.View!=null && NoteProjection.Contains(demoCamera,note.View.Transform,1,point,note.View.HitPolygon,padding)) return true;
            // High reading speeds can move a ring past the camera while it is still
            // inside the Good window. The timing-marker target must remain hittable.
            Spatial.JudgementPose(note.Data.pathId,visualTime,out var position,out var rotation);
            var target=Matrix4x4.TRS(position,rotation,Vector3.one);
            if(NoteProjection.Contains(demoCamera,target,NoteVisual.Radius,point,judgementPolygon,padding)) return true;
            // Finger placement is not a projected tilted disc: real tablet presses
            // also land above/right of it. Give the target a bounded screen-space
            // contact area, partitioned by the closest visible judgement centre.
            // This cannot move the note's timing window or consume a second Tap.
            Vector3 centre=demoCamera.WorldToScreenPoint(position);
            Vector2 delta=point-(Vector2)centre;
            if(centre.z<=demoCamera.nearClipPlane || Mathf.Abs(delta.x)>shortSide*.20f ||
                delta.y>shortSide*.10f || delta.y<-shortSide*.20f) return false;
            float distanceSq=delta.sqrMagnitude;
            bool earlierPath=true;
            foreach(var path in Chart.paths)
            {
                if(path.id==note.Data.pathId) { earlierPath=false;continue; }
                if(Spatial.Visibility(path.id,visualTime)<=.01f) continue;
                Vector3 other=demoCamera.WorldToScreenPoint(Spatial.Point(path.id,SpatialDirector.NearDepth,visualTime));
                if(other.z<=demoCamera.nearClipPlane) continue;
                float otherDistanceSq=(point-(Vector2)other).sqrMagnitude;
                // Chart order owns an exact midpoint, including local Drag contacts.
                if(otherDistanceSq<distanceSq-.01f || (earlierPath&&Mathf.Abs(otherDistanceSq-distanceSq)<=.01f)) return false;
            }
            return true;
        }
        bool InsideAny(RuntimeNote note)
        { foreach(var contact in contacts) if(Inside(note,contact)) return true;return false; }
        /// <summary>Lock the rendered playfield to 16:9 so a chart keeps its shape on every
        /// display. A chart without a video space owns no projection of its own, so any
        /// aspect/matrix left behind by a previous video chart is cleared first; a video
        /// chart keeps the projection its rig authored.</summary>
        public void ApplyPlayfieldViewport()
        {
            if(!VideoChartSpace.Enabled(Chart))
            {
                demoCamera.ResetAspect();
                demoCamera.ResetProjectionMatrix();
            }
            GameViewport.Apply(demoCamera);
        }
        public void EvaluateVisuals(double time)
        {
            visualTime=time;
            ApplyPalette(time);
            Spatial.EvaluateCamera(demoCamera,time);
            if (!VideoChartSpace.Enabled(Chart)) CameraMotionEvaluator.Apply(Chart,tempo,demoCamera,time);
            ApplyPlayfieldViewport();
            stage.Evaluate(time,tempo.BeatAtSeconds(time));authoredVisuals?.Evaluate(time);
            foreach(var path in paths.Values) path.Evaluate(Spatial,time);
            foreach(var n in Engine.Notes)
            {
                bool visible=n.Result==NoteResult.Pending && Spatial.NoteInView(n.HitTime,time,JudgementEngine.GoodWindow);
                if(visible)
                {
                    if(n.View==null) { n.View=pool.Count>0?pool.Pop():new NoteVisual(notesRoot,library);n.View.Bind(n.Data); }
                    Spatial.NotePose(n,time,out var pos,out var rotation);n.View.Transform.SetPositionAndRotation(pos,rotation);
                }
                else if(n.View!=null) { n.View.Release();pool.Push(n.View);n.View=null; }
            }
            foreach(var pulse in pulses)
            {
                float age=(float)(time-pulse.Start);
                bool active=age>=0 && age<.35f;pulse.Transform.gameObject.SetActive(active);
                if(!active) continue;
                pulse.Transform.localScale=Vector3.one*(.7f+age*1.7f);
                pulseBlock.SetColor("_Color",Color.Lerp(pulse.Color,RenderSettings.fogColor,age/.35f));pulse.Renderer.SetPropertyBlock(pulseBlock);
            }
        }
        void OnJudged(RuntimeNote note,NoteResult result)
        {
            hud.Judge(result,clock.Time);
            // 判定驱动的特效（effectClips 里 target=="judgement"）在这里拿到触发时刻。
            // 注意要在 Miss 提前返回之前调用，否则漏掉的音符永远触发不了任何东西——
            // 而「漏掉时画面有反应」正是 BMS poor_events 那个先例的核心。
            authoredVisuals?.NotifyJudgement(result,clock.Time);
            if(result==NoteResult.Miss) return;
            foreach(var pulse in pulses)
            {
                if(clock.Time-pulse.Start<.35) continue;
                // Feedback belongs to the place the player pressed. At high speed a
                // late-but-valid note may already be behind the camera.
                Spatial.JudgementPose(note.Data.pathId,clock.Time,out var p,out var r);pulse.Transform.SetPositionAndRotation(p,r);
                pulse.Start=clock.Time;pulse.Color=note.Data.action=="tap"?new Color(.25f,.64f,1):Color.white;break;
            }
        }
        public void Seek(double time,bool paused)
        {
            time=Math.Max(0,Math.Min(Duration,time));Engine.Reset(time);clock.Seek(time,paused);
            uiPointers.Clear();foreach(var p in pulses) p.Start=-100;
            EvaluateVisuals(time);
        }
        public void SetMenuMode(bool menu)
        {
            menuMode=menu;
            if(!Ready) return;
            if(menu) clock.SetPaused(true);
            hud.SetVisible(!menu);notesRoot.gameObject.SetActive(!menu);pathRoot.gameObject.SetActive(!menu);
            foreach(var pulse in pulses) pulse.Transform.gameObject.SetActive(false);
            contacts.Clear();downs.Clear();uiPointers.Clear();waitForPointerRelease=true;
        }
        public void BeginRun(bool automatic)
        {
            if(!Ready) return;
            autoPlay=automatic;HasFinished=false;IsPractice=false;
            SetMenuMode(false);hud.ResetFeedback();Seek(0,false);
        }
        public void TogglePause() { if(Ready && !menuMode && !HasFinished) { clock.SetPaused(!clock.Paused);uiPointers.Clear();waitForPointerRelease=true; } }
        public void Restart() { if(Ready && !menuMode) BeginRun(autoPlay); }
        public void ToggleAuto() { if(Ready && !menuMode) BeginRun(!autoPlay); }
        public void ToggleMute() { if(source!=null) source.mute=!source.mute; }
        void OnApplicationFocus(bool focused)
        {
            // Surface hints may be lost in the background. Recheck the maximum permitted
            // mode on return, including after the user changes system/game settings.
            if(focused && Application.isMobilePlatform && Ready) RefreshDisplayFrameRate();
            if(!Ready || smoke) return;
            if(!focused) { clock.SetPaused(true);uiPointers.Clear(); }
            // Keep paused on return, preventing surprise misses while the player refocuses.
        }
        void OnDestroy()
        {
            if(source!=null) { source.Stop();if(ownsAudio && source.clip!=null) Destroy(source.clip); }
            authoredVisuals?.Dispose();
            DisposeVideoBga();
            library?.Dispose();
        }
        void OnGUI()
        {
            if(fatal!=null) GUI.Label(new Rect(30,30,1000,300),"Demo could not load:\n"+fatal);
        }
        static string Argument(string[] args,string key)
        { int index=Array.IndexOf(args,key);return index>=0 && index+1<args.Length?args[index+1]:null; }

        /// <summary>Frame rate to ask the display for on mobile. <c>Screen.currentResolution</c>
        /// only reports the mode the system already picked, so requesting it pins a 120/144 Hz
        /// panel at whatever it booted at: the app asks the display for 60 Hz in turn and the
        /// panel never switches up, which also quantises touch to 16.7 ms frames. The fastest
        /// advertised mode is requested instead; devices that refuse simply keep their mode.</summary>
        static int MobileRefreshRate()
        {
            double fastest=FastestAndroidRefreshRate();
            if(fastest<=0) fastest=Screen.currentResolution.refreshRateRatio.value;
            // Round to the nearest whole rate: panels report 119.88 Hz and 144.00002 Hz, and only
            // the real mode is accepted. Rounding up asked for 145 Hz, which no display offers.
            return fastest>0?(int)Math.Round(fastest):60;
        }
        static double FastestAndroidRefreshRate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var manager=activity.Call<AndroidJavaObject>("getWindowManager"))
                using(var display=manager.Call<AndroidJavaObject>("getDefaultDisplay"))
                {
                    double fastest=0;
                    foreach(var mode in display.Call<AndroidJavaObject[]>("getSupportedModes"))
                    {
                        if(mode==null) continue;
                        fastest=Math.Max(fastest,mode.Call<float>("getRefreshRate"));
                        mode.Dispose();
                    }
                    return fastest;
                }
            }
            catch(Exception exception) { Debug.LogWarning("Display modes unavailable: "+exception.Message); }
#endif
            return 0;
        }
        void RefreshDisplayFrameRate()
        {
            if(displayFrameRateRoutine!=null) StopCoroutine(displayFrameRateRoutine);
            displayFrameRateRoutine=StartCoroutine(ApplyDisplayFrameRate());
        }
        /// <summary>Keep the fastest supported target even while system policy temporarily
        /// limits the display. Lowering the target to the current mode makes that limit
        /// self-sustaining, and a 60 FPS target on a restored 144 Hz panel can pace at 48 FPS.</summary>
        System.Collections.IEnumerator ApplyDisplayFrameRate()
        {
            int target=MobileRefreshRate();
            Application.targetFrameRate=target;
            RequestDisplayFrameRate(target);
            // Unity may recreate its rendering Surface during launch or resume. Reapply
            // after setup without treating a delayed/system-limited switch as rejection.
            yield return new WaitForSecondsRealtime(1f);
            // Re-arm Unity's native frame pacer after the asynchronous mode switch.
            // Reassigning an unchanged target can retain the resume-time half-rate interval.
            Application.targetFrameRate=-1;
            yield return null;
            Application.targetFrameRate=target;
            RequestDisplayFrameRate(target);
            displayFrameRateRoutine=null;
        }
        /// <summary>Request the matching physical display mode on the Android UI thread.
        /// A window-level preference survives Unity recreating or updating its SurfaceView.</summary>
        static void RequestDisplayFrameRate(int targetFrameRate)
        {
            if(targetFrameRate<=0) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    activity.Call("runOnUiThread",new AndroidJavaRunnable(()=>
                    {
                        try
                        {
                            using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                            using(var current=unity.GetStatic<AndroidJavaObject>("currentActivity"))
                            using(var manager=current.Call<AndroidJavaObject>("getWindowManager"))
                            using(var display=manager.Call<AndroidJavaObject>("getDefaultDisplay"))
                            using(var active=display.Call<AndroidJavaObject>("getMode"))
                            using(var window=current.Call<AndroidJavaObject>("getWindow"))
                            using(var attributes=window.Call<AndroidJavaObject>("getAttributes"))
                            {
                                int width=active.Call<int>("getPhysicalWidth"),height=active.Call<int>("getPhysicalHeight");
                                int modeId=0;float requested=targetFrameRate;
                                foreach(var mode in display.Call<AndroidJavaObject[]>("getSupportedModes"))
                                {
                                    if(mode==null) continue;
                                    using(mode)
                                    {
                                        float rate=mode.Call<float>("getRefreshRate");
                                        if(Math.Abs(rate-targetFrameRate)<.5f && mode.Call<int>("getPhysicalWidth")==width && mode.Call<int>("getPhysicalHeight")==height)
                                        {modeId=mode.Call<int>("getModeId");requested=rate;}
                                    }
                                }
                                if(modeId==0) return;
                                attributes.Set("preferredDisplayModeId",modeId);
                                attributes.Set("preferredRefreshRate",requested);
                                window.Call("setAttributes",attributes);
                                RequestXiaomiGameFrameRate(current,targetFrameRate);
                                // Android 11+ games also declare their frame rate directly
                                // on the rendering surface, after Unity's initial setup.
                                using(var version=new AndroidJavaClass("android.os.Build$VERSION"))
                                if(version.GetStatic<int>("SDK_INT")>=30)
                                using(var resources=current.Call<AndroidJavaObject>("getResources"))
                                {
                                    int id=resources.Call<int>("getIdentifier","unitySurfaceView","id",current.Call<string>("getPackageName"));
                                    if(id==0) return;
                                    using(var view=current.Call<AndroidJavaObject>("findViewById",id))
                                    using(var holder=view.Call<AndroidJavaObject>("getHolder"))
                                    using(var surface=holder.Call<AndroidJavaObject>("getSurface"))
                                        surface.Call("setFrameRate",requested,0); // FRAME_RATE_COMPATIBILITY_DEFAULT (games)
                                }
                            }
                        }
                        catch(Exception exception) { Debug.LogWarning("Display mode request failed: "+exception.Message); }
                    }));
                }
            }
            catch(Exception exception) { Debug.LogWarning("Display frame rate request failed: "+exception.Message); }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>HyperOS can cap an unrecognised game at 60 Hz despite a 144 Hz Surface
        /// vote. Its exported PowerKeeper receiver accepts a per-game FPS request. This
        /// optional vendor interface may be absent; standard Android hints still apply.</summary>
        static void RequestXiaomiGameFrameRate(AndroidJavaObject activity,int targetFrameRate)
        {
            try
            {
                using(var build=new AndroidJavaClass("android.os.Build"))
                {
                    if(!string.Equals(build.GetStatic<string>("MANUFACTURER"),"Xiaomi",StringComparison.OrdinalIgnoreCase)) return;
                }
                // Scope both the receiver and the affected package. No global settings,
                // privileged permissions, or changes to thermal/power services are needed.
                using(var intent=new AndroidJavaObject("android.content.Intent","com.xiaomi.joyose.OVERRIDE_GAME_FRESHRATE"))
                using(var receiver=intent.Call<AndroidJavaObject>("setPackage","com.miui.powerkeeper"))
                using(var package=intent.Call<AndroidJavaObject>("putExtra","override_pkg_name",activity.Call<string>("getPackageName")))
                using(var rate=intent.Call<AndroidJavaObject>("putExtra","override_freshrate",targetFrameRate))
                    activity.Call("sendBroadcast",intent);
            }
            catch(AndroidJavaException)
            {
                // This is a best-effort OEM extension, not a required Android API.
            }
        }
#endif

        // Automated standalone smoke mode exercises the actual built player and captures real
        // render frames. It is opt-in via command line and has no effect on ordinary play.
        IEnumerator SmokeCapture(string[] args)
        {
            capture=true;source.mute=true;
            string directory=Argument(args,"-demoCapture") ?? Path.Combine(Application.persistentDataPath,"DemoCaptures");
            Directory.CreateDirectory(directory);
            hud.UseCaptureCamera(demoCamera);
            bool imagesValid=true;
            foreach(float time in new[]{8f,16f,23f,34f,44f,56f})
            {
                Seek(time,true);
                // Reconstruct a genuine autoplay prefix so screenshots show earned score,
                // without replaying a burst of historical hit effects at the preview time.
                var callback=Engine.OnJudged;Engine.OnJudged=null;
                Engine.Reset(0);Engine.Advance(time-.001,true);Engine.OnJudged=callback;
                EvaluateVisuals(time);
                yield return null;
                // A hidden Windows player may not present its backbuffer. Render the real
                // camera and camera-space Canvas to an offscreen target for dependable QA.
                Canvas.ForceUpdateCanvases();
                var target=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32);
                var previous=RenderTexture.active;
                demoCamera.targetTexture=target;
                // The capture target owns the whole frame; the HUD is rendered through this
                // camera in screen space, so the playfield letterbox must step aside here.
                GameViewport.Apply(demoCamera);
                demoCamera.Render();RenderTexture.active=target;
                var screenshot=new Texture2D(1600,900,TextureFormat.RGB24,false);
                screenshot.ReadPixels(new Rect(0,0,1600,900),0,0);screenshot.Apply();
                imagesValid &= screenshot.GetPixel(800,450).grayscale>.05f;
                File.WriteAllBytes(Path.Combine(directory,"demo-"+time.ToString("00")+"s.png"),screenshot.EncodeToPNG());
                demoCamera.targetTexture=null;RenderTexture.active=previous;
                ApplyPlayfieldViewport();
                RenderTexture.ReleaseTemporary(target);Destroy(screenshot);
            }
            // A converted chart can ship only plain taps and drags: verify the kinds this
            // chart actually contains instead of dereferencing a sleeve note that is absent.
            bool inputPassed=true;int kindsVerified=0;
            foreach(string action in new[]{"tap","drag"})
            foreach(bool sleeve in new[]{false,true})
            {
                var targetNote=Array.Find(Engine.Notes,n=>n.Data.action==action&&n.Data.protectedNote==sleeve);
                if(targetNote==null) continue;
                kindsVerified++;
                Seek(targetNote.HitTime,true);
                Vector2 centre=demoCamera.WorldToScreenPoint(targetNote.View.Transform.position);
                Vector2 testPoint=sleeve?new Vector2(10,Screen.height*.5f):centre;
                if(action=="tap") Engine.Tap(targetNote.HitTime,n=>Inside(n,testPoint));
                else Engine.Drag(targetNote.HitTime,n=>Inside(n,testPoint),true);
                inputPassed &= targetNote.Result==NoteResult.Perfect;
            }
            inputPassed &= kindsVerified>0;
            // Check real DSP advance and a pause, not only arithmetic in the score engine.
            Seek(4,false);yield return new WaitForSecondsRealtime(.35f);
            bool clockPassed=clock.Time>4.1;
            clock.SetPaused(true);double frozen=clock.Time;
            yield return new WaitForSecondsRealtime(.1f);
            clockPassed &= Math.Abs(clock.Time-frozen)<.000001;
            Engine.Reset(0);Engine.Advance(Duration,true);
            bool renderingPassed=QualitySettings.vSyncCount==0 && OnDemandRendering.renderFrameInterval==1
                && QualitySettings.antiAliasing==4 && demoCamera.allowMSAA && !demoCamera.allowDynamicResolution
                && (Application.isMobilePlatform ? Application.targetFrameRate>0 : Application.targetFrameRate==-1);
            bool passed=imagesValid && inputPassed && clockPassed && renderingPassed && Engine.Misses==0 && Engine.Judged==Chart.notes.Length && Engine.Score==1000000;
            File.WriteAllText(Path.Combine(directory,"player-smoke.txt"),"PASS="+passed+"\nImages="+imagesValid+"\nFourRules="+inputPassed+"\nNoteKinds="+kindsVerified+"\nDspClock="+clockPassed+"\nRenderSettings="+renderingPassed+"\nTargetFrameRate="+Application.targetFrameRate+"\nVSync="+QualitySettings.vSyncCount+"\nNotes="+Engine.Judged+"\nScore="+Engine.Score+"\nMisses="+Engine.Misses+"\n");
            Debug.Log("GEOMETRY_DEMO_SMOKE "+(passed?"PASS":"FAIL"));
            Application.Quit(passed?0:1);
        }
    }
}
