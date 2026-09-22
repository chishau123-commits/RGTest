using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GeometryRhythm
{
    public enum FrontendPage { Title, Main, Friends, News, MainTransitionLoad, SongModes, StoryLevels, StoryPlayer, Songs, Loading, Playing, Results }

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

    /// <summary>Owns navigation and MainUI route transitions, not note timing. Only one gameplay session/camera/audio source
    /// is active. A new chart replaces that session; navigating menus does not reload the scene.</summary>
    public sealed class RhythmFrontend : MonoBehaviour
    {
        [SerializeField] string gameTitle="RHYTHM\nHACKER";
        public FrontendPage Page { get; private set; }
        public IReadOnlyList<SongEntry> Songs => songs;
        public int Selected { get; private set; }
        public PlayResult Result { get; private set; }
        public int StoryLevelIndex { get; private set; }
        public bool MenuBgmActive => menuBgmTargetVolume>0;
        public bool SmokeMode => smoke;
        public bool BeginCueAvailable => beginCueSource!=null && beginCueSource.clip!=null;
        readonly List<SongEntry> songs=new List<SongEntry>();
        RhythmFrontendView view;
        RhythmDemoController session;
        GameObject sessionRoot;
        TextAsset externalAsset;
        AudioSource menuBgm;
        AudioSource storyBgm;
        AudioSource beginCueSource;
        AudioSource notificationSource;
        float menuBgmTargetVolume,menuBgmFadeSpeed;
        float storyBgmTargetVolume,storyBgmFadeSpeed;
        int loadedIndex=-1;
        float inputOffset, volume;
        bool pendingAuto, configured, smoke, enteringMain, exitingNews, exitingFriends, confirmingReturn, enteringSongs, returningFromSongs;
        StoryRuntimeProjectData storyProject;
        StoryRuntimeNodeData storyCurrentNode;
        StoryRuntimeNodeData storyChoiceResponse;
        readonly Dictionary<string,string> storyVariables=new Dictionary<string,string>(StringComparer.Ordinal);
        readonly List<string> storyCast=new List<string>();
        int storyNodeIndex;
        string storyBackgroundId="";
        bool storyStageActive;
        bool storyEntering,storyStageEntering;
        bool storySceneTransitionPending;
        const float MenuBgmVolume=.46f;

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
            SetupMenuBgm();
            SetupStoryBgm();
            SetupBeginCue();
            SetupNotificationSound();
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
        void SetupMenuBgm()
        {
            var clip=Resources.Load<AudioClip>("Music/main bgm");
            if(clip==null)
            {
                Debug.LogWarning("Main UI background music was not imported: Resources/Music/main bgm");
                return;
            }
            menuBgm=gameObject.GetComponent<AudioSource>();
            if(menuBgm==null)menuBgm=gameObject.AddComponent<AudioSource>();
            menuBgm.clip=clip;menuBgm.playOnAwake=false;menuBgm.loop=true;
            menuBgm.spatialBlend=0;menuBgm.volume=0;menuBgm.mute=smoke;
        }
        void SetupStoryBgm()
        {
            storyBgm=gameObject.AddComponent<AudioSource>();
            storyBgm.playOnAwake=false;
            storyBgm.loop=false;
            storyBgm.spatialBlend=0;
            storyBgm.volume=0;
            storyBgm.mute=smoke;
        }
        void SetupBeginCue()
        {
            var cueObject=new GameObject("Now lets begin cue");cueObject.transform.SetParent(transform,false);
            beginCueSource=cueObject.AddComponent<AudioSource>();
            beginCueSource.clip=Resources.Load<AudioClip>("Audio/now-lets-begin");
            beginCueSource.playOnAwake=false;beginCueSource.loop=false;beginCueSource.spatialBlend=0;
            beginCueSource.volume=.95f;beginCueSource.mute=smoke;beginCueSource.ignoreListenerPause=true;
            if(beginCueSource.clip==null)Debug.LogWarning("Begin cue was not imported: Resources/Audio/now-lets-begin");
        }
        void SetupNotificationSound()
        {
            var sourceObject=new GameObject("Notification sound");sourceObject.transform.SetParent(transform,false);
            notificationSource=sourceObject.AddComponent<AudioSource>();
            notificationSource.clip=Resources.Load<AudioClip>("Audio/notification");
            notificationSource.playOnAwake=false;notificationSource.loop=false;notificationSource.spatialBlend=0;
            notificationSource.volume=1f;notificationSource.mute=smoke;
            if(notificationSource.clip==null)Debug.LogWarning("Notification sound was not imported: Resources/Audio/notification");
        }
        public void PlayNotificationSound()
        {
            if(notificationSource!=null&&notificationSource.clip!=null)notificationSource.PlayOneShot(notificationSource.clip,2.2f);
        }
        void SetMenuBgm(bool active,float fadeSeconds=.45f,bool startSilent=false)
        {
            if(menuBgm==null||menuBgm.clip==null)return;
            if(active && !menuBgm.isPlaying)
            {
                if(startSilent)menuBgm.volume=0;
                menuBgm.Play();
            }
            menuBgmTargetVolume=active?MenuBgmVolume:0;
            float distance=Mathf.Abs(menuBgmTargetVolume-menuBgm.volume);
            menuBgmFadeSpeed=distance/Mathf.Max(.05f,fadeSeconds);
        }
        void UpdateMenuBgm()
        {
            if(menuBgm==null||menuBgm.clip==null)return;
            menuBgm.volume=Mathf.MoveTowards(menuBgm.volume,menuBgmTargetVolume,menuBgmFadeSpeed*Time.unscaledDeltaTime);
            if(menuBgmTargetVolume<=0 && menuBgm.volume<=0 && menuBgm.isPlaying)menuBgm.Pause();
            if(storyBgm==null||storyBgm.clip==null)return;
            bool storyActive=storyBgmTargetVolume>0;
            if(storyActive&&!storyBgm.isPlaying)
            {
                storyBgm.time=0;
                storyBgm.volume=0;
                storyBgm.Play();
            }
            float loopFade=Mathf.Clamp(storyBgm.clip.length*.08f,.18f,.8f);
            float loopGain=1;
            if(storyActive)
            {
                float fadeIn=Mathf.Clamp01(storyBgm.time/Mathf.Max(.01f,loopFade));
                float fadeOut=Mathf.Clamp01((storyBgm.clip.length-storyBgm.time)/Mathf.Max(.01f,loopFade));
                loopGain=Mathf.Min(fadeIn,fadeOut);
            }
            float storyTarget=storyActive?storyBgmTargetVolume*loopGain:0;
            storyBgm.volume=Mathf.MoveTowards(storyBgm.volume,storyTarget,storyBgmFadeSpeed*Time.unscaledDeltaTime);
            if(!storyActive&&storyBgm.volume<=0&&storyBgm.isPlaying)storyBgm.Pause();
        }
        void SetStoryBgm(bool active,string resource,float fadeSeconds=.45f)
        {
            if(storyBgm==null)return;
            if(active&&!string.IsNullOrWhiteSpace(resource))
            {
                var clip=Resources.Load<AudioClip>(resource);
                if(clip==null)Debug.LogWarning("Story scene music was not found: Resources/"+resource);
                if(clip!=null&&storyBgm.clip!=clip)
                {
                    storyBgm.Stop();
                    storyBgm.clip=clip;
                }
                if(clip!=null&&!storyBgm.isPlaying)
                {
                    storyBgm.volume=0;
                    storyBgm.Play();
                }
            }
            storyBgmTargetVolume=active?.34f:0;
            float distance=Mathf.Abs(storyBgmTargetVolume-storyBgm.volume);
            storyBgmFadeSpeed=distance/Mathf.Max(.05f,fadeSeconds);
        }
        void PrepareSession(int index)
        {
            // Deactivate before deferred Destroy: no duplicate camera, sound, or event handling.
            if(sessionRoot!=null) {session.SetMenuMode(true);sessionRoot.SetActive(false);Destroy(sessionRoot);}
            sessionRoot=new GameObject("Active song / "+songs[index].Title);sessionRoot.transform.SetParent(transform,false);
            session=sessionRoot.AddComponent<RhythmDemoController>();
            session.showFrontend=false;session.startInMenu=true;session.chartOverride=songs[index].Asset;
            session.inputOffsetMilliseconds=inputOffset;session.musicVolume=smoke?0:volume;
            session.ReturnToSongs=storyStageActive?ReturnToStoryLevels:ShowSongs;
            loadedIndex=index;
        }
        void Update()
        {
            if(!configured) return;
            UpdateMenuBgm();
            view.UpdateLayout();
            if(returningFromSongs&&!view.PageSlideActive)returningFromSongs=false;
            if(Page==FrontendPage.Loading)
            {
                if(session.LoadError!=null) {ShowSongs();view.ShowError(session.LoadError);}
                else if(session.Ready) {Page=FrontendPage.Playing;view.Hide();session.BeginRun(pendingAuto);}
            }
            else if(Page==FrontendPage.Playing && session.HasFinished) ShowResults();
            // Buttons have navigation disabled, preventing Enter being handled twice by UGUI.
            if(smoke || Page==FrontendPage.Playing || enteringSongs || returningFromSongs) return;
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                if(Page==FrontendPage.Main) ShowTitle();
                else if(Page==FrontendPage.Friends) ReturnFromFriends();
                else if(Page==FrontendPage.News) ReturnFromNews();
                else if(Page==FrontendPage.SongModes) ReturnToMainFromModes();
                else if(Page==FrontendPage.StoryLevels||Page==FrontendPage.Songs) ShowSongModes();
                else if(Page==FrontendPage.StoryPlayer) ReturnToStoryLevels();
                else if(Page==FrontendPage.Results || Page==FrontendPage.Loading)
                {
                    if(storyStageActive) ReturnToStoryFromResults();
                    else ShowSongs();
                }
            }
            if(Input.GetKeyDown(KeyCode.Return))
            {
                if(Page==FrontendPage.Title) EnterGame();
                else if(Page==FrontendPage.Main) EnterMusic();
                else if(Page==FrontendPage.Songs) Play(false);
                else if(Page==FrontendPage.Results) Play(Result.Automatic);
                else if(Page==FrontendPage.StoryPlayer)
                {
                    if(storyCurrentNode!=null&&storyCurrentNode.kind==StoryRuntimeNodeKind.Stage) EnterStoryStage();
                    else AdvanceStory();
                }
            }
        }
        public void ShowTitle()
        {
            if(session!=null) session.SetMenuMode(true);
            Page=FrontendPage.Title;SetMenuBgm(false,.55f);view.ShowTitle();
        }
        public void EnterGame()
        {
            if(Page!=FrontendPage.Title||enteringMain)return;
            StartCoroutine(EnterGameSequence());
        }
        IEnumerator EnterGameSequence()
        {
            enteringMain=true;
            view.SetTitleVideoDucked(true);
            if(beginCueSource!=null && beginCueSource.clip!=null)
            {
                beginCueSource.Play();
            }

            const float exitDuration=3f;
            float elapsed=0;
            while(elapsed<exitDuration)
            {
                elapsed+=Time.unscaledDeltaTime;
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/exitDuration));
                view.SetPageAlpha(1-t);
                yield return null;
            }
            if(beginCueSource!=null)beginCueSource.Stop();
            if(notificationSource!=null)notificationSource.Stop();
            ShowMain(1.8f,true,1.8f);
            enteringMain=false;
        }
        public void ShowMain()
        {
            ShowMain(.55f,false,.55f);
        }
        void ShowMain(float bgmFade,bool startSilent,float pageFade)
        {
            if(session!=null) session.SetMenuMode(true);
            Page=FrontendPage.Main;SetMenuBgm(true,bgmFade,startSilent);view.ShowMain(pageFade,false,StoryLevelIndex);
        }
        public void ShowNews()
        {
            if(session!=null) session.SetMenuMode(true);
            Page=FrontendPage.News;SetMenuBgm(true,.55f);view.ShowNews();
        }
        public void ShowFriends()
        {
            if(session!=null) session.SetMenuMode(true);
            Page=FrontendPage.Friends;SetMenuBgm(true,.55f);view.ShowFriends();
        }
        public void OpenNewsFromNotification(int index)
        {
            view.CloseTransientWindows();
            if(session!=null)session.SetMenuMode(true);
            Page=FrontendPage.News;SetMenuBgm(true,.55f);view.ShowNews();view.OpenNewsPostFromNotification(index);
        }
        public void OpenFriendRequestsFromNotification()
        {
            view.CloseTransientWindows();
            if(session!=null)session.SetMenuMode(true);
            Page=FrontendPage.Friends;SetMenuBgm(true,.55f);view.ShowFriends();view.OpenFriendRequestsFromNotification();
        }
        public void OpenFriendChatFromNotification(int friendIndex)
        {
            view.CloseTransientWindows();
            if(session!=null)session.SetMenuMode(true);
            Page=FrontendPage.Friends;SetMenuBgm(true,.55f);view.ShowFriends();view.OpenFriendChatFromNotification(friendIndex);
        }
        public void ReturnFromFriends()
        {
            if(Page!=FrontendPage.Friends||exitingFriends)return;
            StartCoroutine(ExitFriendsSequence());
        }
        IEnumerator ExitFriendsSequence()
        {
            exitingFriends=true;
            view.BeginFriendExit();
            if(!smoke)yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            view.FinishFriendExit();
            Page=FrontendPage.Main;
            exitingFriends=false;
        }
        public void ConfirmReturnFromMain()
        {
            if(Page!=FrontendPage.Main||confirmingReturn)return;
            StartCoroutine(ConfirmReturnSequence());
        }
        IEnumerator ConfirmReturnSequence()
        {
            confirmingReturn=true;
            view.BeginReturnExit();
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            view.FinishReturnExit();
            ShowTitle();
            confirmingReturn=false;
        }
        public void ReturnFromNews()
        {
            if(Page!=FrontendPage.News||exitingNews)return;
            StartCoroutine(ExitNewsSequence());
        }
        IEnumerator ExitNewsSequence()
        {
            exitingNews=true;
            view.BeginNewsExit();
            if(!smoke)yield return new WaitForSecondsRealtime(view.NewsTransitionDuration);
            view.FinishNewsExit();
            Page=FrontendPage.Main;
            exitingNews=false;
        }
        public void ShowSongs()
        {
            if(session!=null) session.SetMenuMode(true);
            Page=FrontendPage.Songs;SetMenuBgm(true,.45f);view.ShowSongs(songs,Selected);
        }
        public void EnterMusic()
        {
            if(Page!=FrontendPage.Main||enteringSongs)return;
            enteringSongs=true;
            StartCoroutine(EnterModesSequence());
        }

        IEnumerator EnterModesSequence()
        {
            if(session!=null)session.SetMenuMode(true);
            SetMenuBgm(true,.25f);
            yield return FadePageAlpha(1f,0f,.26f);
            Page=FrontendPage.MainTransitionLoad;
            view.ShowModeLoading();
            yield return FadePageAlpha(0f,1f,.34f);
            float elapsed=0;
            const float loadDuration=1.8f;
            while(elapsed<loadDuration)
            {
                elapsed+=Time.unscaledDeltaTime;
                float t=Mathf.Clamp01(elapsed/loadDuration);
                view.SetModeLoadingProgress(CurvedLoad(t),Time.unscaledTime);
                yield return null;
            }
            view.SetModeLoadingProgress(1f,Time.unscaledTime);
            elapsed=0;
            view.BeginModeGate();
            const float gateInDuration=.78f;
            while(elapsed<gateInDuration)
            {
                elapsed+=Time.unscaledDeltaTime;
                view.SetModeGateIn(Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/gateInDuration)));
                yield return null;
            }
            view.SetModeGateIn(1f);
            Page=FrontendPage.SongModes;
            view.ShowSongModes(false);
            view.BringModeGateToFront();
            elapsed=0;
            const float gateOutDuration=.72f;
            while(elapsed<gateOutDuration)
            {
                elapsed+=Time.unscaledDeltaTime;
                view.SetModeGateOut(Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/gateOutDuration)));
                yield return null;
            }
            view.EndModeGate();
            enteringSongs=false;
        }

        static float CurvedLoad(float t)
        {
            t=Mathf.Clamp01(t);
            float value;
            if(t<.2f)value=Mathf.SmoothStep(0f,.43f,t/.2f);
            else if(t<.82f)value=Mathf.SmoothStep(.43f,.86f,(t-.2f)/.62f);
            else value=Mathf.SmoothStep(.86f,1f,(t-.82f)/.18f);
            return Mathf.Floor(value*28f)/28f;
        }

        IEnumerator FadePageAlpha(float from,float to,float duration)
        {
            float elapsed=0;
            duration=Mathf.Max(.01f,duration);
            while(elapsed<duration)
            {
                elapsed+=Time.unscaledDeltaTime;
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/duration));
                view.SetPageAlpha(Mathf.Lerp(from,to,t));
                yield return null;
            }
            view.SetPageAlpha(to);
        }
        public void ShowSongModes()
        {
            if(Page==FrontendPage.SongModes)return;
            if(session!=null)session.SetMenuMode(true);
            Page=FrontendPage.SongModes;SetMenuBgm(true,.45f);view.ShowSongModes(false);
        }
        public void ShowStoryLevels()
        {
            if(session!=null)session.SetMenuMode(true);
            Page=FrontendPage.StoryLevels;SetMenuBgm(true,.45f);view.ShowStoryLevels(StoryLevelIndex);
        }
        public void ReturnToMainFromModes()
        {
            if(Page!=FrontendPage.SongModes||returningFromSongs)return;
            returningFromSongs=true;
            if(session!=null)session.SetMenuMode(true);
            Page=FrontendPage.Main;SetMenuBgm(true,.45f);
            view.ShowMain(.18f,true,StoryLevelIndex);
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
            SetMenuBgm(false,.35f);pendingAuto=automatic;Page=FrontendPage.Loading;view.ShowLoading(songs[Selected].ShortTitle);
        }
        void ShowResults()
        {
            Result=new PlayResult(session.Engine,session.autoPlay,session.IsPractice);
            session.SetMenuMode(true);Page=FrontendPage.Results;
            view.ShowResults(songs[Selected],Result,storyStageActive);
        }

        public void EnterStoryLevel(int index)
        {
            if(index!=0)
            {
                view.ShowError("CHAPTER LOCKED  /  COMPLETE 0-1 FIRST");
                return;
            }
            if(!storyEntering)StartCoroutine(EnterStorySequence());
        }

        IEnumerator EnterStorySequence()
        {
            storyEntering=true;
            storyProject=StoryRuntimeLoader.LoadMainStory();
            if(storyProject==null)
            {
                view.ShowError("STORY DATA MISSING  /  Resources/Story/main-story.json");
                storyEntering=false;
                yield break;
            }
            storyNodeIndex=0;
            storyVariables.Clear();
            storyCast.Clear();
            storyBackgroundId="";
            storyStageActive=false;
            storyChoiceResponse=null;
            storySceneTransitionPending=false;
            Page=FrontendPage.StoryPlayer;
            SetMenuBgm(false,.35f);
            SetStoryBgm(true,storyProject.sceneMusicResource,.55f);
            view.ShowStoryIntro(storyProject.title,"STORY 0-1");
            yield return FadeStoryPage(0,1,.55f);
            yield return new WaitForSecondsRealtime(.9f);
            yield return FadeStoryPage(1,0,.35f);
            ShowCurrentStoryNode();
            yield return FadeStoryPage(0,1,.5f);
            storyEntering=false;
        }

        IEnumerator FadeStoryPage(float from,float to,float duration)
        {
            float elapsed=0;
            duration=Mathf.Max(.01f,duration);
            while(elapsed<duration)
            {
                elapsed+=Time.unscaledDeltaTime;
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/duration));
                view.SetPageAlpha(Mathf.Lerp(from,to,t));
                yield return null;
            }
            view.SetPageAlpha(to);
        }

        void ShowCurrentStoryNode()
        {
            if(storyProject==null||storyProject.nodes==null||storyProject.nodes.Length==0)
            {
                ReturnToStoryLevels();
                return;
            }
            if(storyNodeIndex>=storyProject.nodes.Length)
            {
                ReturnToStoryLevels();
                return;
            }
            storyCurrentNode=storyProject.nodes[Mathf.Clamp(storyNodeIndex,0,storyProject.nodes.Length-1)];
            if(storyCurrentNode==null)
            {
                ReturnToStoryLevels();
                return;
            }
            if(storyChoiceResponse!=null)
            {
                storyCurrentNode=storyChoiceResponse;
                view.ShowStory(storyProject,storyCurrentNode,storyCurrentNode,storyBackgroundId,
                    storyVariables,storyCast,false);
                return;
            }
            if(storyCurrentNode.kind==StoryRuntimeNodeKind.Background&&!string.IsNullOrEmpty(storyCurrentNode.backgroundId))
            {
                storyBackgroundId=storyCurrentNode.backgroundId;
                if(storyCurrentNode.castIds!=null&&storyCurrentNode.castIds.Length>0)
                {
                    storyCast.Clear();
                    foreach(string id in storyCurrentNode.castIds)
                        if(!string.IsNullOrWhiteSpace(id))storyCast.Add(id);
                }
                storySceneTransitionPending=true;
                storyNodeIndex++;
                ShowCurrentStoryNode();
                return;
            }
            if(storyCurrentNode.castIds!=null&&storyCurrentNode.castIds.Length>0)
            {
                storyCast.Clear();
                foreach(string id in storyCurrentNode.castIds)
                    if(!string.IsNullOrWhiteSpace(id))storyCast.Add(id);
            }
            StoryRuntimeNodeData speakerNode=FindStorySpeakerNode(storyNodeIndex);
            bool sceneTransition=storySceneTransitionPending;
            storySceneTransitionPending=false;
            view.ShowStory(storyProject,storyCurrentNode,speakerNode,storyBackgroundId,storyVariables,storyCast,
                sceneTransition);
        }

        public void AdvanceStory()
        {
            if(Page!=FrontendPage.StoryPlayer||storyCurrentNode==null)return;
            if(storyChoiceResponse!=null)
            {
                storyChoiceResponse=null;
                ShowCurrentStoryNode();
                return;
            }
            if(storyCurrentNode.kind==StoryRuntimeNodeKind.Choice)return;
            if(storyCurrentNode.kind==StoryRuntimeNodeKind.End)
            {
                ReturnToStoryLevels();
                return;
            }
            storyNodeIndex++;
            ShowCurrentStoryNode();
        }

        public void ChooseStoryOption(int index)
        {
            if(Page!=FrontendPage.StoryPlayer||storyCurrentNode==null||
                storyCurrentNode.kind!=StoryRuntimeNodeKind.Choice||
                storyCurrentNode.choices==null||index<0||index>=storyCurrentNode.choices.Length)
                return;
            var choice=storyCurrentNode.choices[index];
            if(choice==null||string.IsNullOrWhiteSpace(choice.variable))return;
            storyVariables[choice.variable]=choice.value??"";
            storyNodeIndex++;
            storyChoiceResponse=new StoryRuntimeNodeData
            {
                id="choice-response",
                kind=StoryRuntimeNodeKind.Dialogue,
                title="节奏骇客的决定",
                speakerId="rhythm-hacker",
                expressionId="voice",
                text="我决定了：" + choice.text + "。"
            };
            ShowCurrentStoryNode();
        }

        public void EnterStoryStage()
        {
            if(Page!=FrontendPage.StoryPlayer||storyCurrentNode==null||
                storyCurrentNode.kind!=StoryRuntimeNodeKind.Stage)
                return;
            if(!storyStageEntering)StartCoroutine(EnterStoryStageSequence());
        }

        public void SkipStoryToStage()
        {
            if(storyProject==null||storyProject.nodes==null)return;
            int stageIndex=-1;
            for(int i=0;i<storyProject.nodes.Length;i++)
            {
                if(storyProject.nodes[i]!=null&&storyProject.nodes[i].kind==StoryRuntimeNodeKind.Stage)
                {
                    stageIndex=i;
                    break;
                }
            }
            if(stageIndex<0)
            {
                view.ShowError("STORY STAGE NOT FOUND");
                return;
            }
            storyChoiceResponse=null;
            storyNodeIndex=stageIndex;
            storyCurrentNode=storyProject.nodes[stageIndex];
            if(!storyStageEntering)StartCoroutine(EnterStoryStageSequence());
        }

        IEnumerator EnterStoryStageSequence()
        {
            if(storyCurrentNode==null||storyCurrentNode.kind!=StoryRuntimeNodeKind.Stage) yield break;
            storyStageEntering=true;
            SetStoryBgm(false,"",.3f);
            yield return FadeStoryPage(1,0,.35f);
            string chartName=Path.GetFileNameWithoutExtension(storyCurrentNode.chartAssetPath);
            int index=-1;
            for(int i=0;i<songs.Count;i++)
            {
                if(string.Equals(songs[i].Asset.name,chartName,StringComparison.OrdinalIgnoreCase))
                {
                    index=i;
                    break;
                }
            }
            if(index<0)
            {
                view.ShowError("STORY CHART NOT FOUND  /  "+chartName);
                storyStageEntering=false;
                yield break;
            }
            Selected=index;
            storyStageActive=true;
            if(session!=null)session.ReturnToSongs=ReturnToStoryLevels;
            Play(storyCurrentNode.autoplay);
            yield return FadeStoryPage(0,1,.45f);
            storyStageEntering=false;
        }

        public void ReturnToStoryFromResults()
        {
            if(!storyStageActive)return;
            storyStageActive=false;
            storyNodeIndex++;
            Page=FrontendPage.StoryPlayer;
            SetStoryBgm(true,storyProject.sceneMusicResource,.35f);
            ShowCurrentStoryNode();
        }

        public void ReturnToStoryLevels()
        {
            storyStageActive=false;
            Page=FrontendPage.StoryLevels;
            SetStoryBgm(false,"",.35f);
            SetMenuBgm(true,.45f);
            view.ShowStoryLevels(StoryLevelIndex);
        }

        StoryRuntimeNodeData FindStorySpeakerNode(int index)
        {
            if(storyProject==null||storyProject.nodes==null)return null;
            for(int i=Mathf.Min(index,storyProject.nodes.Length-1);i>=0;i--)
            {
                var node=storyProject.nodes[i];
                if(node!=null&&node.kind==StoryRuntimeNodeKind.Dialogue)return node;
            }
            return null;
        }
        void OnDestroy()
        {
            if(beginCueSource!=null)beginCueSource.Stop();
            if(notificationSource!=null)notificationSource.Stop();
            if(menuBgm!=null)menuBgm.Stop();
            if(storyBgm!=null)storyBgm.Stop();
            if(externalAsset!=null) Destroy(externalAsset);
        }

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
            yield return null;
            deadline=Time.realtimeSinceStartup+8;
            while(view.TitleVideoAvailable && !view.TitleVideoPlaying && Time.realtimeSinceStartup<deadline) yield return null;
            passed &= view.ButtonCount==1 && view.TitleUsesIndependentArtwork
                && (!view.TitleVideoAvailable || view.TitleVideoPlaying)
                && BeginCueAvailable && !view.Invoke("Open songs") && !MenuBgmActive;
            passed &= Capture(directory,"ui-title");
            passed &= view.Invoke("Enter game");
            deadline=Time.realtimeSinceStartup+5;
            while(Page==FrontendPage.Title && Time.realtimeSinceStartup<deadline) yield return null;
            passed &= Page==FrontendPage.Main && view.ButtonCount==6 && MenuBgmActive;
            yield return null;passed &= Capture(directory,"ui-main");
            passed &= view.HomeBackgroundLoaded;
            passed &= view.Invoke("Open friends")&&Page==FrontendPage.Friends;
            yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            yield return null;passed &= Capture(directory,"ui-friends");
            passed &= view.Invoke("Open friend 0");yield return null;
            passed &= Capture(directory,"ui-friend-chat");
            passed &= view.Invoke("Chat choice 0");
            deadline=Time.realtimeSinceStartup+8;
            while(!view.FriendReplyReady&&Time.realtimeSinceStartup<deadline)yield return null;
            passed &= view.FriendReplyReady;
            passed &= Capture(directory,"ui-friend-branch");
            passed &= view.Invoke("Chat choice 1");
            deadline=Time.realtimeSinceStartup+8;
            while(!view.FriendConversationComplete&&Time.realtimeSinceStartup<deadline)yield return null;
            passed &= view.FriendConversationComplete;
            passed &= view.Invoke("Back to friends");
            passed &= view.Invoke("Back to main");
            deadline=Time.realtimeSinceStartup+5;
            while(Page==FrontendPage.Friends&&Time.realtimeSinceStartup<deadline)yield return null;
            passed &= Page==FrontendPage.Main&&view.NewsPostCount==3&&view.NewsUnread;yield return null;
            passed &= view.Invoke("Open news") && Page==FrontendPage.News;
            yield return new WaitForSecondsRealtime(view.NewsTransitionDuration);
            yield return null;passed &= Capture(directory,"ui-news");
            passed &= view.Invoke("Open news post 0");yield return null;
            passed &= Capture(directory,"ui-news-detail");
            bool likedBefore=view.NewsPostLiked(0);
            passed &= view.Invoke("Like post")&&view.NewsPostLiked(0)!=likedBefore;
            passed &= view.Invoke("Post comment");yield return null;
            passed &= Capture(directory,"ui-news-comment");
            passed &= view.Invoke("Close detail");
            yield return new WaitForSecondsRealtime(view.NewsDetailTransitionDuration);
            passed &= view.Invoke("Back to main");
            deadline=Time.realtimeSinceStartup+5;
            while(Page==FrontendPage.News && Time.realtimeSinceStartup<deadline) yield return null;
            passed &= Page==FrontendPage.Main;yield return null;
            passed &= view.Invoke("Open background menu");
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= Capture(directory,"ui-background-menu");
            int backgroundIndex=view.HomeBackgroundIndex;
            int nextBackground=(backgroundIndex+1)%3;
            passed &= view.Invoke("Select background "+(nextBackground+1))
                && view.HomeBackgroundIndex==nextBackground && view.HomeBackgroundLoaded;
            yield return new WaitForSecondsRealtime(.8f);passed &= Capture(directory,"ui-main-alt");
            passed &= view.Invoke("Open settings");
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= Capture(directory,"ui-controls");
            passed &= view.Invoke("Close controls");
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= view.Invoke("Return to title") && Page==FrontendPage.Main;
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= Capture(directory,"ui-return-confirm");
            passed &= view.Invoke("Cancel return") && Page==FrontendPage.Main;
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= view.Invoke("Open songs");
            yield return new WaitForSecondsRealtime(view.EnterTransitionDuration);
            passed &= Page==FrontendPage.SongModes;
            yield return null;passed &= Capture(directory,"ui-song-modes");
            passed &= view.Invoke("Open free mode")&&Page==FrontendPage.Songs;
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
            passed &= view.Invoke("Back to modes") && Page==FrontendPage.SongModes;
            passed &= view.Invoke("Back to main");
            yield return new WaitForSecondsRealtime(view.EnterTransitionDuration);
            passed &= Page==FrontendPage.Main;
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
            File.WriteAllText(Path.Combine(directory,"frontend-smoke.txt"),"PASS="+passed+"\nSongs="+songs.Count+"\nAutoScore="+Result.Score+"\nFlow=Title/Main/Songs/Play/Pause/Return/Manual/Results/Retry/Autoplay/Results/Main\n");
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
