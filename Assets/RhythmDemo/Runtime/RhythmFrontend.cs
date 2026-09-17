using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GeometryRhythm
{
    public enum FrontendPage { Title, Songs, Loading, Playing, Results }

    /// <summary>A real library entry, backed by the same JSON used by gameplay/the chart editor.</summary>
    public sealed class SongEntry
    {
        public TextAsset Asset;
        public ChartData Chart;
        public double Duration;
        public int MaxPaths;
        public string Title => string.IsNullOrWhiteSpace(Chart.title) ? Asset.name : Chart.title;
        public string ShortTitle => Title.Contains(" / ") ? Title.Substring(Title.LastIndexOf(" / ",StringComparison.Ordinal)+3) : Title;
        public string Bpm
        {
            get
            {
                float min=Chart.tempos[0].bpm,max=min;
                foreach(var t in Chart.tempos) {min=Math.Min(min,t.bpm);max=Math.Max(max,t.bpm);}
                return min==max ? min.ToString("0.#") : min.ToString("0.#")+" - "+max.ToString("0.#");
            }
        }
        public static SongEntry Parse(TextAsset asset)
        {
            var chart=ChartLoader.Parse(asset.text);
            var entry=new SongEntry{Asset=asset,Chart=chart,Duration=new TempoMap(chart.tempos,chart.ticksPerBeat).SecondsAtBeat(chart.endBeat)};
            foreach(var section in chart.sections) entry.MaxPaths=Math.Max(entry.MaxPaths,section.placements.Length);
            return entry;
        }
    }

    /// <summary>Snapshot before retry/return can reset the mutable judgement engine.</summary>
    public sealed class PlayResult
    {
        public readonly int Score, Perfect, Good, Miss, MaxCombo, Total;
        public readonly double Accuracy;
        public readonly bool Automatic, Practice;
        public string Mode => Automatic ? "AUTOPLAY / PREVIEW" : Practice ? "PRACTICE SESSION" : "MANUAL PLAY";
        public string Grade => Accuracy>=100 ? "SSS" : Accuracy>=99 ? "SS" : Accuracy>=98 ? "S" : Accuracy>=95 ? "A" : Accuracy>=90 ? "B" : Accuracy>=80 ? "C" : "D";
        public PlayResult(JudgementEngine engine,bool automatic,bool practice)
        {
            Score=engine.Score;Perfect=engine.Perfects;Good=engine.Goods;Miss=engine.Misses;
            MaxCombo=engine.MaxCombo;Total=engine.Eligible;Accuracy=engine.Accuracy;
            Automatic=automatic;Practice=practice;
        }
    }

    /// <summary>Owns navigation, not note timing. Only one gameplay session/camera/audio source
    /// is active. A new chart replaces that session; navigating menus does not reload the scene.</summary>
    public sealed class RhythmFrontend : MonoBehaviour
    {
        [SerializeField] string gameTitle="GEOMETRY\nRHYTHM";
        public FrontendPage Page { get; private set; }
        public IReadOnlyList<SongEntry> Songs => songs;
        public int Selected { get; private set; }
        public PlayResult Result { get; private set; }
        readonly List<SongEntry> songs=new List<SongEntry>();
        RhythmFrontendView view;
        RhythmDemoController session;
        GameObject sessionRoot;
        TextAsset externalAsset;
        int loadedIndex=-1;
        float inputOffset, volume;
        bool pendingAuto, configured, smoke;

        public void Configure(TextAsset chartOverride,float offset,float musicVolume,string title)
        {
            if(!string.IsNullOrWhiteSpace(title)) gameTitle=title;
            inputOffset=offset;volume=musicVolume;
            var args=Environment.GetCommandLineArgs();smoke=Array.IndexOf(args,"-frontendSmoke")>=0;
            // An explicit chart remains the first entry; other bundled charts are discoverable.
            int chartArg=Array.IndexOf(args,"-demoChart");
            if(chartArg>=0 && chartArg+1<args.Length)
            {
                try { externalAsset=new TextAsset(File.ReadAllText(args[chartArg+1]));externalAsset.name=Path.GetFileNameWithoutExtension(args[chartArg+1]);chartOverride=externalAsset; }
                catch(Exception ex) { Debug.LogWarning("Chart file could not be opened: "+ex.Message); }
            }
            if(chartOverride!=null) AddSong(chartOverride);
            var assets=Resources.LoadAll<TextAsset>("Charts");
            Array.Sort(assets,(a,b)=>string.Compare(a.name,b.name,StringComparison.OrdinalIgnoreCase));
            foreach(var asset in assets) if(asset!=chartOverride && (chartOverride==null || asset.text!=chartOverride.text)) AddSong(asset);
            if(FindObjectOfType<EventSystem>()==null)
                new GameObject("Menu input",typeof(EventSystem),typeof(StandaloneInputModule)).transform.SetParent(transform,false);
            view=new RhythmFrontendView(transform,this,gameTitle);
            configured=true;
            if(songs.Count>0) PrepareSession(0);
            ShowTitle();
            if(smoke) StartCoroutine(SmokeFlow(args));
        }
        void AddSong(TextAsset asset)
        {
            try { songs.Add(SongEntry.Parse(asset)); }
            catch(Exception ex) { Debug.LogWarning("Skipped invalid chart '"+asset.name+"': "+ex.Message); }
        }
        void PrepareSession(int index)
        {
            // Deactivate before deferred Destroy: no duplicate camera, sound, or event handling.
            if(sessionRoot!=null) {session.SetMenuMode(true);sessionRoot.SetActive(false);Destroy(sessionRoot);}
            sessionRoot=new GameObject("Active song / "+songs[index].Title);sessionRoot.transform.SetParent(transform,false);
            session=sessionRoot.AddComponent<RhythmDemoController>();
            session.showFrontend=false;session.startInMenu=true;session.chartOverride=songs[index].Asset;
            session.inputOffsetMilliseconds=inputOffset;session.musicVolume=smoke?0:volume;
            session.ReturnToSongs=ShowSongs;
            loadedIndex=index;
        }
        void Update()
        {
            if(!configured) return;
            view.UpdateLayout();
            if(Page==FrontendPage.Loading)
            {
                if(session.LoadError!=null) {ShowSongs();view.ShowError(session.LoadError);}
                else if(session.Ready) {Page=FrontendPage.Playing;view.Hide();session.BeginRun(pendingAuto);}
            }
            else if(Page==FrontendPage.Playing && session.HasFinished) ShowResults();
            // Buttons have navigation disabled, preventing Enter being handled twice by UGUI.
            if(smoke || Page==FrontendPage.Playing) return;
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                if(Page==FrontendPage.Songs) ShowTitle();
                else if(Page==FrontendPage.Results || Page==FrontendPage.Loading) ShowSongs();
            }
            if(Input.GetKeyDown(KeyCode.Return))
            {
                if(Page==FrontendPage.Title) ShowSongs();
                else if(Page==FrontendPage.Songs) Play(false);
                else if(Page==FrontendPage.Results) Play(Result.Automatic);
            }
        }
        public void ShowTitle()
        {
            if(session!=null) session.SetMenuMode(true);
            Page=FrontendPage.Title;view.ShowTitle();
        }
        public void ShowSongs()
        {
            if(session!=null) session.SetMenuMode(true);
            Page=FrontendPage.Songs;view.ShowSongs(songs,Selected);
        }
        public void SelectSong(int index)
        {
            if(index<0 || index>=songs.Count) return;
            Selected=index;view.ShowSongs(songs,Selected);
        }
        public void Play(bool automatic)
        {
            if(songs.Count==0 || Page==FrontendPage.Playing || Page==FrontendPage.Loading) return;
            if(loadedIndex!=Selected || session==null || session.LoadError!=null) PrepareSession(Selected);
            pendingAuto=automatic;Page=FrontendPage.Loading;view.ShowLoading(songs[Selected].ShortTitle);
        }
        void ShowResults()
        {
            Result=new PlayResult(session.Engine,session.autoPlay,session.IsPractice);
            session.SetMenuMode(true);Page=FrontendPage.Results;view.ShowResults(songs[Selected],Result);
        }
        void OnDestroy() { if(externalAsset!=null) Destroy(externalAsset); }

        // Deterministic UI integration checks use actual Button callbacks and a rendered player.
        // This opt-in fast path is QA only; normal play always reaches results from the DSP clock.
        IEnumerator SmokeFlow(string[] args)
        {
            int arg=Array.IndexOf(args,"-demoCapture");
            string directory=arg>=0 && arg+1<args.Length?args[arg+1]:Path.Combine(Application.persistentDataPath,"FrontendCaptures");
            Directory.CreateDirectory(directory);
            float deadline=Time.realtimeSinceStartup+30;
            while(session!=null && !session.Ready && session.LoadError==null && Time.realtimeSinceStartup<deadline) yield return null;
            bool passed=session!=null && session.Ready && Page==FrontendPage.Title && session.IsPaused;
            if(!passed) {Debug.LogError("GEOMETRY_FRONTEND_SMOKE initialization failed");Application.Quit(1);yield break;}
            yield return null;passed &= Capture(directory,"ui-title");
            passed &= view.Invoke("Open songs") && Page==FrontendPage.Songs;
            yield return null;passed &= Capture(directory,"ui-songs");
            // Inspect short-lived and fallback screens without changing the shipped catalog.
            view.ShowLoading(songs[Selected].ShortTitle);yield return null;
            passed &= Capture(directory,"ui-loading");
            view.ShowSongs(songs,Selected);view.ShowError("QA preview: the selected audio resource could not be found.");yield return null;
            passed &= Capture(directory,"ui-error");
            view.ShowSongs(new List<SongEntry>(),0);yield return null;
            passed &= Capture(directory,"ui-empty");
            view.ShowSongs(songs,Selected);
            passed &= view.Invoke("Watch autoplay");
            yield return null;yield return null;
            passed &= Page==FrontendPage.Playing && session.autoPlay && !session.IsPaused;
            // Exercise pause -> return and confirm that menu time does not advance.
            session.TogglePause();yield return null;
            passed &= Capture(directory,"ui-pause",true);
            session.ReturnToSongs();double frozen=session.CurrentTime;
            yield return null;passed &= Page==FrontendPage.Songs && session.IsPaused && session.CurrentTime==frozen;
            passed &= view.Invoke("Play chart");yield return null;yield return null;
            passed &= Page==FrontendPage.Playing && !session.autoPlay && session.Engine.Judged==0;
            session.Engine.Advance(session.Duration+.2,false);
            ShowResults();passed &= Result.Miss==songs[Selected].Chart.notes.Length && !Result.Automatic;
            passed &= view.Invoke("Retry");yield return null;yield return null;
            passed &= Page==FrontendPage.Playing && session.Engine.Judged==0;
            ShowSongs();view.Invoke("Watch autoplay");yield return null;yield return null;
            session.Engine.Advance(session.Duration,true);ShowResults();
            passed &= Result.Score==1000000 && Result.Perfect==songs[Selected].Chart.notes.Length && Result.Automatic;
            yield return null;passed &= Capture(directory,"ui-results");
            passed &= view.Invoke("Back to songs") && Page==FrontendPage.Songs;
            passed &= view.Invoke("Back to title") && Page==FrontendPage.Title;
            // An in-memory QA entry exercises actual chart replacement without shipping a
            // fake song or modifying the user's JSON/library. It exists only in smoke mode.
            int bundledCount=songs.Count;
            var alternate=ChartLoader.Parse(songs[0].Asset.text);alternate.title="QA / Alternate chart";
            var alternateAsset=new TextAsset(JsonUtility.ToJson(alternate)){name="qa-alternate"};
            AddSong(alternateAsset);ShowSongs();
            passed &= view.Invoke("Select song "+bundledCount) && Selected==bundledCount;
            passed &= view.Invoke("Play chart");
            deadline=Time.realtimeSinceStartup+30;
            while(Page==FrontendPage.Loading && Time.realtimeSinceStartup<deadline) yield return null;
            yield return null;
            passed &= Page==FrontendPage.Playing && session.Chart.title==alternate.title
                && FindObjectsOfType<Camera>().Length==1 && FindObjectsOfType<AudioListener>().Length==1;
            ShowSongs();SelectSong(0);Play(false);
            deadline=Time.realtimeSinceStartup+30;
            while(Page==FrontendPage.Loading && Time.realtimeSinceStartup<deadline) yield return null;
            yield return null;
            passed &= Page==FrontendPage.Playing && session.Chart.title==songs[0].Chart.title
                && session.Engine.Judged==0 && FindObjectsOfType<Camera>().Length==1;
            ShowTitle();songs.RemoveAt(bundledCount);Destroy(alternateAsset);
            File.WriteAllText(Path.Combine(directory,"frontend-smoke.txt"),"PASS="+passed+"\nSongs="+songs.Count+"\nAutoScore="+Result.Score+"\nFlow=Title/Songs/Play/Pause/Return/Manual/Results/Retry/Autoplay/Results/Title\n");
            Debug.Log("GEOMETRY_FRONTEND_SMOKE "+(passed?"PASS":"FAIL"));Application.Quit(passed?0:1);
        }
        bool Capture(string directory,string name,bool captureHud=false)
        {
            var camera=session.demoCamera;
            var target=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            Canvas hud=null;
            if(captureHud)
                foreach(var candidate in session.GetComponentsInChildren<Canvas>(true))
                    if(candidate.name=="Gameplay HUD") {hud=candidate;break;}
            var oldMode=hud!=null?hud.renderMode:RenderMode.ScreenSpaceOverlay;
            Camera oldCamera=hud!=null?hud.worldCamera:null;
            float oldPlane=hud!=null?hud.planeDistance:0;
            if(hud!=null){hud.renderMode=RenderMode.ScreenSpaceCamera;hud.worldCamera=camera;hud.planeDistance=.5f;}
            view.UseCaptureCamera(camera);Canvas.ForceUpdateCanvases();
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
            // A dark theme may legitimately have a nearly black center. Test image contrast
            // across the artboard instead of depending on one bright background pixel.
            float min=1,max=0;int bright=0;
            for(int y=25;y<900;y+=25)for(int x=25;x<1600;x+=25)
            {float value=texture.GetPixel(x,y).grayscale;min=Mathf.Min(min,value);max=Mathf.Max(max,value);if(value>.25f)bright++;}
            bool valid=max-min>.25f && bright>12 && (!captureHud || hud!=null);
            File.WriteAllBytes(Path.Combine(directory,name+".png"),texture.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=previous;view.UseCaptureCamera(null);
            if(hud!=null){hud.renderMode=oldMode;hud.worldCamera=oldCamera;hud.planeDistance=oldPlane;}
            RenderTexture.ReleaseTemporary(target);Destroy(texture);return valid;
        }
    }
}
