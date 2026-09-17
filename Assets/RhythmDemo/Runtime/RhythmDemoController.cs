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
    public sealed class RhythmDemoController : MonoBehaviour
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
        public Action ReturnToSongs;
        TempoMap tempo;
        SongClock clock;
        AudioSource source;
        VisualLibrary library;
        StageVisuals stage;
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
        MaterialPropertyBlock pulseBlock;
        bool smoke, capture, ownsAudio;
        string fatal;
        sealed class HitPulse { public Transform Transform; public Renderer Renderer; public double Start=-100; public Color Color; }

        void Start()
        {
            // The existing demo scene is also the application entry point. Child sessions
            // opt out of this bootstrap; smoke mode keeps its direct gameplay entry.
            if(showFrontend && Array.IndexOf(Environment.GetCommandLineArgs(),"-demoSmoke")<0)
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
            string chartPath=Argument(args,"-demoChart");
            string json=chartPath!=null?File.ReadAllText(chartPath):(chartOverride!=null?chartOverride:Resources.Load<TextAsset>("Charts/geometry-demo")).text;
            Chart=ChartLoader.Parse(json);tempo=new TempoMap(Chart.tempos,Chart.ticksPerBeat);
            Duration=tempo.SecondsAtBeat(Chart.endBeat);
            // Desktop: remove the old 120 FPS cap and do not wait for vertical sync.
            // Mobile: -1 means 30 FPS, so explicitly request the current display refresh rate.
            // Ceil preserves fractional rates such as 119.88 Hz instead of requesting 119 FPS.
            double refreshRate=Screen.currentResolution.refreshRateRatio.value;
            Application.targetFrameRate=Application.isMobilePlatform
                ? (refreshRate>0 ? (int)Math.Ceiling(refreshRate) : 60) : -1;
            QualitySettings.vSyncCount=0;
            OnDemandRendering.renderFrameInterval=1; // Render every update; no frame skipping.
            QualitySettings.antiAliasing=4; // Keep the thin 3D rings and paths clean.
            Screen.sleepTimeout=SleepTimeout.NeverSleep;
            library=new VisualLibrary();
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
            Spatial=new SpatialDirector(Chart,tempo);
            var worldRoot=new GameObject("Abstract world / real meshes").transform;worldRoot.SetParent(transform,false);
            stage=new StageVisuals(worldRoot,library,Spatial);
            notesRoot=new GameObject("Note pool").transform;notesRoot.SetParent(transform,false);
            pathRoot=new GameObject("3D paths").transform;pathRoot.SetParent(transform,false);
            foreach(var path in Chart.paths) paths.Add(path.id,new PathVisual(path.id,pathRoot,library));
            // Prewarm to the actual maximum number of notes in an approach window, not a fixed path count.
            Engine=new JudgementEngine(Chart,tempo);
            int maximum=0,left=0;
            for(int right=0;right<Engine.Notes.Length;right++)
            {
                while(Engine.Notes[right].HitTime-Engine.Notes[left].HitTime>Chart.approachSeconds+JudgementEngine.GoodWindow) left++;
                maximum=Math.Max(maximum,right-left+1);
            }
            for(int i=0;i<maximum+4;i++) pool.Push(new NoteVisual(notesRoot,library));
            for(int i=0;i<12;i++)
            {
                var r=VisualLibrary.MeshObject("Pooled hit ripple",notesRoot,library.Ring,library.Tap);
                r.gameObject.SetActive(false);pulses.Add(new HitPulse{Transform=r.transform,Renderer=r});
            }
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.volume=musicVolume;
            source.clip=string.IsNullOrEmpty(Chart.audioResource)?null:Resources.Load<AudioClip>(Chart.audioResource);
            if(!string.IsNullOrEmpty(Chart.audioResource) && source.clip==null) throw new FileNotFoundException("Audio resource not found: "+Chart.audioResource);
            if(source.clip==null) { source.clip=DemoSoundtrack.Create((float)Duration);ownsAudio=true; }
            clock=new SongClock(source,Duration,Chart.audioOffsetSeconds);
            hud=new DemoHud(transform,TogglePause,Restart,ToggleAuto,ToggleMute,()=>ReturnToSongs?.Invoke());
            Engine.OnJudged=OnJudged;
            clock.Seek(0,startInMenu);Ready=true;
            if(startInMenu) SetMenuMode(true);
            if(smoke) StartCoroutine(SmokeCapture(args));
        }
        void Update()
        {
            if(!Ready) return;
            if(menuMode)
            {
                // Menus reuse the actual 3D environment, without advancing a chart or audio.
                double preview=Math.Min(Duration*.4,8)+Math.Sin(Time.unscaledTimeAsDouble*.08)*1.2;
                Spatial.EvaluateCamera(demoCamera,preview);stage.Evaluate(preview,tempo.BeatAtSeconds(preview));
                return;
            }
            if(!smoke) ReadShortcuts();
            double time=clock.Time;
            EvaluateVisuals(time);
            if(!clock.Paused && !smoke)
            {
                // A click/held finger used to start or resume must not hit an anywhere Note.
                if(waitForPointerRelease)
                {
                    contacts.Clear();downs.Clear();
                    if(Input.touchCount==0 && !Input.GetMouseButton(0)) waitForPointerRelease=false;
                }
                else CollectPointers();
                if(!autoPlay)
                {
                    double inputTime=time+inputOffsetMilliseconds/1000.0;
                    foreach(var point in downs) Engine.Tap(inputTime,n=>Inside(n,point));
                    Engine.Drag(inputTime,n=>InsideAny(n),contacts.Count>0);
                }
                Engine.Advance(time,autoPlay);
                if(time>=Duration)
                {
                    // Final notes also get resolved when the DSP clock is clamped at song end.
                    Engine.Advance(Duration+JudgementEngine.GoodWindow+.001,autoPlay);
                    HasFinished=true;clock.SetPaused(true);
                }
            }
            hud.Update(Engine,time,Duration,Spatial.Section(time),clock.Paused,autoPlay,source.mute,capture);
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
            if(note.View==null) return false;
            return NoteProjection.Contains(demoCamera,note.View.Transform,1,point,note.View.HitPolygon,
                Mathf.Clamp(Mathf.Min(Screen.width,Screen.height)*.013f,8,20));
        }
        bool InsideAny(RuntimeNote note)
        { foreach(var contact in contacts) if(Inside(note,contact)) return true;return false; }
        public void EvaluateVisuals(double time)
        {
            Spatial.EvaluateCamera(demoCamera,time);stage.Evaluate(time,tempo.BeatAtSeconds(time));
            foreach(var path in paths.Values) path.Evaluate(Spatial,time);
            foreach(var n in Engine.Notes)
            {
                double delta=n.HitTime-time;
                bool visible=n.Result==NoteResult.Pending && delta<=Chart.approachSeconds && delta>=-JudgementEngine.GoodWindow;
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
            if(result==NoteResult.Miss) return;
            foreach(var pulse in pulses)
            {
                if(clock.Time-pulse.Start<.35) continue;
                Spatial.NotePose(note,clock.Time,out var p,out var r);pulse.Transform.SetPositionAndRotation(p,r);
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
            if(!Ready || smoke) return;
            if(!focused) { clock.SetPaused(true);uiPointers.Clear(); }
            // Keep paused on return, preventing surprise misses while the player refocuses.
        }
        void OnDestroy()
        {
            if(source!=null) { source.Stop();if(ownsAudio && source.clip!=null) Destroy(source.clip); }
            library?.Dispose();
        }
        void OnGUI()
        {
            if(fatal!=null) GUI.Label(new Rect(30,30,1000,300),"Demo could not load:\n"+fatal);
        }
        static string Argument(string[] args,string key)
        { int index=Array.IndexOf(args,key);return index>=0 && index+1<args.Length?args[index+1]:null; }

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
                demoCamera.targetTexture=target;demoCamera.Render();RenderTexture.active=target;
                var screenshot=new Texture2D(1600,900,TextureFormat.RGB24,false);
                screenshot.ReadPixels(new Rect(0,0,1600,900),0,0);screenshot.Apply();
                imagesValid &= screenshot.GetPixel(800,450).grayscale>.05f;
                File.WriteAllBytes(Path.Combine(directory,"demo-"+time.ToString("00")+"s.png"),screenshot.EncodeToPNG());
                demoCamera.targetTexture=null;RenderTexture.active=previous;
                RenderTexture.ReleaseTemporary(target);Destroy(screenshot);
            }
            bool inputPassed=true;
            foreach(string action in new[]{"tap","drag"})
            foreach(bool sleeve in new[]{false,true})
            {
                var targetNote=Array.Find(Engine.Notes,n=>n.Data.action==action&&n.Data.protectedNote==sleeve);
                Seek(targetNote.HitTime,true);
                Vector2 centre=demoCamera.WorldToScreenPoint(targetNote.View.Transform.position);
                Vector2 testPoint=sleeve?new Vector2(10,Screen.height*.5f):centre;
                if(action=="tap") Engine.Tap(targetNote.HitTime,n=>Inside(n,testPoint));
                else Engine.Drag(targetNote.HitTime,n=>Inside(n,testPoint),true);
                inputPassed &= targetNote.Result==NoteResult.Perfect;
            }
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
            File.WriteAllText(Path.Combine(directory,"player-smoke.txt"),"PASS="+passed+"\nImages="+imagesValid+"\nFourRules="+inputPassed+"\nDspClock="+clockPassed+"\nRenderSettings="+renderingPassed+"\nTargetFrameRate="+Application.targetFrameRate+"\nVSync="+QualitySettings.vSyncCount+"\nNotes="+Engine.Judged+"\nScore="+Engine.Score+"\nMisses="+Engine.Misses+"\n");
            Debug.Log("GEOMETRY_DEMO_SMOKE "+(passed?"PASS":"FAIL"));
            Application.Quit(passed?0:1);
        }
    }
}
