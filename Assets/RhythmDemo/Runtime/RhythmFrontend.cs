using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GeometryRhythm
{
    public enum FrontendPage { Boot, Title, Main, Device, Diary, Friends, News, MainTransitionLoad, SongModes, StoryLevels, StoryPlayer, Songs, Loading, Playing, Results, MusicPlayer, PowerChoice, ShutdownMonologue, Dormitory }

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
    public sealed partial class RhythmFrontend : MonoBehaviour
    {
        [SerializeField] string gameTitle="RHYTHM\nHACKER";
        public FrontendPage Page { get; private set; }
        public IReadOnlyList<SongEntry> Songs => songs;
        public int Selected { get; private set; }
        public PlayResult Result { get; private set; }
        public int StoryLevelIndex { get; private set; }
        public bool MenuBgmActive => menuBgmTargetVolume>0;
        public bool DiaryOpenedFromDevice => diaryOpenedFromDevice;
        public bool UiTransitionLocked => enteringMain||enteringSongs||enteringAlbum||returningFromSongs||
            exitingNews||exitingFriends||exitingDiary||confirmingReturn;
        public bool SmokeMode => smoke;
        public bool BeginCueAvailable => uiAudio!=null && uiAudio.Has("login");
        readonly List<SongEntry> songs=new List<SongEntry>();
        RhythmFrontendView view;
        RhythmDemoController session;
        GameObject sessionRoot;
        TextAsset externalAsset;
        AudioSource menuBgm;
        AudioSource bootBgm;
        AudioSource modeBgm;
        AudioSource storyBgm;
        float menuBgmTargetVolume,menuBgmFadeSpeed;
        float bootBgmTargetVolume,bootBgmFadeSpeed;
        const float BootBgmVolume=.17f;
        float modeBgmTargetVolume,modeBgmFadeSpeed;
        const float ModeBgmVolume=.08f;
        float menuBgmVolume=.50f;
        const float MenuBgmOutputScale=.64f;
        float storyBgmTargetVolume,storyBgmFadeSpeed;
        bool menuBgmRequestedActive;
        int loadedIndex=-1;
        float inputOffset, volume;
        bool pendingAuto, configured, smoke, bootSequenceActive, enteringMain, exitingNews, exitingFriends, exitingDiary, confirmingReturn, enteringSongs, returningFromSongs;
        bool friendsOpenedFromDevice;
        bool loginMonologueCompleted,friendsMonologueShown;
        bool diaryOpenedFromDevice;
        DormitoryDuskScene dormitory;
        bool outdoorDrinkDialogueShown;
        ApartmentDepartureCutscene departure;
        int departureCueCount;
        bool powerTransition;
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
        const string MenuBgmVolumeKey="RhythmHacker.MenuBgmVolume";
        const string MenuBgmTrackKey="RhythmHacker.MenuBgmTrack";
        static readonly string[] MenuBgmResources={"Music/main bgm","Music/player-whack-a-mole","Music/player-cover-song","Music/player-tic-tac-toe","Music/player-gymnopedie-no1"};
        static readonly string[] MenuBgmTitles={"预设 BGM","打地鼠","封面曲","井字棋","Gymnopedie No.1"};
        readonly AudioClip[] menuBgmTracks=new AudioClip[MenuBgmResources.Length];
        int selectedMenuBgmTrack;
        public int MusicTrackCount=>menuBgmTracks.Length;
        public int SelectedMusicTrack=>selectedMenuBgmTrack;
        public string MusicTrackTitle(int index)=>index>=0&&index<MenuBgmTitles.Length?MenuBgmTitles[index]:"";
        public bool MusicTrackAvailable(int index)=>index>=0&&index<menuBgmTracks.Length&&menuBgmTracks[index]!=null;
        public float MusicPlaybackSeconds=>menuBgm!=null&&menuBgm.clip!=null?menuBgm.time:0;
        public float MusicDurationSeconds=>menuBgm!=null&&menuBgm.clip!=null?menuBgm.clip.length:0;

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
            SetupBootBgm();
            SetupModeBgm();
            SetupStoryBgm();
            SetupUiSounds();
            view=new RhythmFrontendView(transform,this,gameTitle);
            configured=true;
            if(songs.Count>0) PrepareSession(0);
            ShowBoot();
            if(Array.IndexOf(args,"-frontendAudioCapture")>=0)StartCoroutine(FrontendAudioCapture(args));
            else if(Array.IndexOf(args,"-storyDialogueCapture")>=0)StartCoroutine(StoryDialogueCapture(args));
            else if(Array.IndexOf(args,"-musicScreensCapture")>=0)StartCoroutine(MusicScreensCapture(args));
            else if(Array.IndexOf(args,"-desktopMotionCapture")>=0)StartCoroutine(DesktopMotionCapture(args));
            else if(smoke) StartCoroutine(SmokeFlow(args));
            if(Array.IndexOf(args,"-duskSmoke")>=0)StartCoroutine(DuskSmoke());
        }
        void AddSong(TextAsset asset)
        {
            try { songs.Add(SongEntry.Parse(asset)); }
            catch(Exception ex) { Debug.LogWarning("Skipped invalid chart '"+asset.name+"': "+ex.Message); }
        }
        void SetupMenuBgm()
        {
            for(int i=0;i<menuBgmTracks.Length;i++)
            {
                menuBgmTracks[i]=Resources.Load<AudioClip>(MenuBgmResources[i]);
                if(menuBgmTracks[i]==null)Debug.LogWarning("Desktop music was not imported: Resources/"+MenuBgmResources[i]);
            }
            const int defaultTrack=4;
            const string defaultRevision="RhythmHacker.GymnopedieDefault.v1";
            bool applyDefault=!PlayerPrefs.HasKey(defaultRevision);
            selectedMenuBgmTrack=smoke||applyDefault?defaultTrack:Mathf.Clamp(PlayerPrefs.GetInt(MenuBgmTrackKey,defaultTrack),0,menuBgmTracks.Length-1);
            if(applyDefault&&!smoke)
            {
                PlayerPrefs.SetInt(MenuBgmTrackKey,defaultTrack);PlayerPrefs.SetInt(defaultRevision,1);PlayerPrefs.Save();
            }
            if(menuBgmTracks[selectedMenuBgmTrack]==null)selectedMenuBgmTrack=0;
            if(menuBgmTracks[selectedMenuBgmTrack]==null)
            {
                Debug.LogWarning("Main UI background music was not imported: Resources/Music/main bgm");
                return;
            }
            menuBgm=gameObject.GetComponent<AudioSource>();
            if(menuBgm==null)menuBgm=gameObject.AddComponent<AudioSource>();
            float storedVolume=PlayerPrefs.GetFloat(MenuBgmVolumeKey,.50f);
            if(Mathf.Abs(storedVolume-.46f)<.001f)
            {
                storedVolume=.50f;
                PlayerPrefs.SetFloat(MenuBgmVolumeKey,storedVolume);
            }
            menuBgmVolume=Mathf.Clamp01(storedVolume);
            menuBgm.clip=menuBgmTracks[selectedMenuBgmTrack];menuBgm.playOnAwake=false;menuBgm.loop=true;
            menuBgm.spatialBlend=0;menuBgm.volume=0;menuBgm.mute=smoke;
        }
        public bool SelectMusicTrack(int index)
        {
            if(!MusicTrackAvailable(index)||menuBgm==null)return false;
            if(index==selectedMenuBgmTrack)return true;
            selectedMenuBgmTrack=index;
            PlayerPrefs.SetInt(MenuBgmTrackKey,index);PlayerPrefs.Save();
            bool active=menuBgmRequestedActive;
            menuBgm.Stop();menuBgm.clip=menuBgmTracks[index];menuBgm.volume=0;
            menuBgmTargetVolume=active?menuBgmVolume*MenuBgmOutputScale:0;
            menuBgmFadeSpeed=menuBgmTargetVolume/.35f;
            if(active&&menuBgmVolume>.001f)menuBgm.Play();
            return true;
        }
        void SetupBootBgm()
        {
            var clip=Resources.Load<AudioClip>("Music/Kylo - Tell You");
            if(clip==null)
            {
                Debug.LogWarning("Boot music was not imported: Resources/Music/Kylo - Tell You");
                return;
            }
            bootBgm=gameObject.AddComponent<AudioSource>();
            bootBgm.clip=clip;bootBgm.playOnAwake=false;bootBgm.loop=true;
            bootBgm.spatialBlend=0;bootBgm.volume=0;bootBgm.mute=smoke;
        }
        void SetupModeBgm()
        {
            var clip=Resources.Load<AudioClip>("Music/暴力四叶草 - 『未许之地』活动剧情BGM2");
            if(clip==null)clip=Resources.Load<AudioClip>("Music/The Way You Ignited the Sky");
            if(clip==null)
            {
                Debug.LogWarning("Mode music unavailable. Add a standard OGG/WAV/MP3 under Resources/Music");
                return;
            }
            modeBgm=gameObject.AddComponent<AudioSource>();
            modeBgm.clip=clip;modeBgm.playOnAwake=false;modeBgm.loop=true;
            modeBgm.spatialBlend=0;modeBgm.volume=0;modeBgm.mute=smoke;
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
        public float MenuBgmVolume => menuBgmVolume;

        public void SetMenuBgmVolume(float value)
        {
            menuBgmVolume=Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MenuBgmVolumeKey,menuBgmVolume);
            PlayerPrefs.Save();
            if(menuBgmRequestedActive&&menuBgm!=null&&menuBgm.clip!=null)
            {
                if(!menuBgm.isPlaying&&menuBgmVolume>.001f)
                {
                    menuBgm.volume=0;
                    menuBgm.Play();
                }
                menuBgmTargetVolume=menuBgmVolume*MenuBgmOutputScale;
                menuBgmFadeSpeed=1.6f;
            }
        }

        void SetMenuBgm(bool active,float fadeSeconds=.45f,bool startSilent=false)
        {
            if(menuBgm==null||menuBgm.clip==null)return;
            menuBgmRequestedActive=active;
            if(active && !menuBgm.isPlaying)
            {
                if(startSilent)menuBgm.volume=0;
                menuBgm.Play();
            }
            menuBgmTargetVolume=active?menuBgmVolume*MenuBgmOutputScale:0;
            float distance=Mathf.Abs(menuBgmTargetVolume-menuBgm.volume);
            menuBgmFadeSpeed=distance/Mathf.Max(.05f,fadeSeconds);
        }
        void UpdateMenuBgm()
        {
            UpdateBootBgm();
            UpdateModeBgm();
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
        void SetBootBgm(bool active,float fadeSeconds,bool startSilent=false)
        {
            if(bootBgm==null||bootBgm.clip==null)return;
            if(active&&!bootBgm.isPlaying)
            {
                bootBgm.volume=startSilent?0:bootBgm.volume;
                bootBgm.Play();
            }
            bootBgmTargetVolume=active?BootBgmVolume:0;
            float distance=Mathf.Abs(bootBgmTargetVolume-bootBgm.volume);
            bootBgmFadeSpeed=distance/Mathf.Max(.05f,fadeSeconds);
        }
        void RestartBootBgm(float fadeSeconds=3f)
        {
            if(bootBgm==null||bootBgm.clip==null)return;
            bootBgm.Stop();
            bootBgm.time=0;
            bootBgm.volume=0;
            SetBootBgm(true,fadeSeconds,true);
        }
        void UpdateBootBgm()
        {
            if(bootBgm==null||bootBgm.clip==null)return;
            bootBgm.volume=Mathf.MoveTowards(bootBgm.volume,bootBgmTargetVolume,bootBgmFadeSpeed*Time.unscaledDeltaTime);
            if(bootBgmTargetVolume<=0&&bootBgm.volume<=.001f&&bootBgm.isPlaying)bootBgm.Pause();
        }
        void SetModeBgm(bool active,float fadeSeconds=.45f,bool restart=false)
        {
            if(modeBgm==null||modeBgm.clip==null)return;
            if(active&&(restart||!modeBgm.isPlaying))
            {
                if(restart)
                {
                    modeBgm.Stop();
                    modeBgm.time=0;
                }
                modeBgm.volume=0;
                modeBgm.Play();
            }
            modeBgmTargetVolume=active?ModeBgmVolume:0;
            float distance=Mathf.Abs(modeBgmTargetVolume-modeBgm.volume);
            modeBgmFadeSpeed=distance/Mathf.Max(.05f,fadeSeconds);
        }
        void UpdateModeBgm()
        {
            if(modeBgm==null||modeBgm.clip==null)return;
            modeBgm.volume=Mathf.MoveTowards(modeBgm.volume,modeBgmTargetVolume,modeBgmFadeSpeed*Time.unscaledDeltaTime);
            if(modeBgmTargetVolume<=0&&modeBgm.volume<=.001f&&modeBgm.isPlaying)modeBgm.Pause();
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
            session.ReturnToSongs=storyStageActive?(Action)ReturnToStoryLevels:ShowSongs;
            loadedIndex=index;
        }
        void Update()
        {
            if(!configured) return;
            UpdateMenuBgm();
            view.UpdateLayout();
            if(session!=null)
                session.SetMenuBackdropRendering(true);
            if(Page==FrontendPage.Loading)
            {
                if(session.LoadError!=null) {ShowSongs();view.ShowError(session.LoadError);}
                else if(session.Ready) {Page=FrontendPage.Playing;view.Hide();session.BeginRun(pendingAuto);}
            }
            else if(Page==FrontendPage.Playing && session.HasFinished) ShowResults();
            // Buttons have navigation disabled, preventing Enter being handled twice by UGUI.
            if(smoke || Page==FrontendPage.Playing || enteringSongs || enteringAlbum || returningFromSongs || powerTransition) return;
            if(view.DiaryPromptActive)return;
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                if(Page==FrontendPage.PowerChoice) {CancelPowerChoice();return;}
                if(Page==FrontendPage.Main) ShowTitle(true);
                else if(Page==FrontendPage.Device) CloseDeviceHome();
                else if(Page==FrontendPage.Diary) CloseDiaryToDevice();
                else if(Page==FrontendPage.Friends) ReturnFromFriends();
                else if(Page==FrontendPage.News) ReturnFromNews();
                else if(Page==FrontendPage.MusicPlayer) CloseMusicPlayer();
                else if(Page==FrontendPage.SongModes) ReturnToMainFromModes();
                else if(Page==FrontendPage.StoryLevels||Page==FrontendPage.Songs) {if(!view.PutAlbumBack())ShowSongModes();}
                else if(Page==FrontendPage.StoryPlayer) ReturnToStoryLevels();
                else if(Page==FrontendPage.Results || Page==FrontendPage.Loading)
                {
                    if(storyStageActive) ReturnToStoryFromResults();
                    else ShowSongs();
                }
            }
            if(Page==FrontendPage.Dormitory&&Input.GetKeyDown(KeyCode.E))
            {
                if(dormitory==null||!dormitory.TryUseVending())ReturnFromDormitory();
            }
            if(Page==FrontendPage.Dormitory&&dormitory!=null)
            {
                view.UpdateDormitoryHint(dormitory.InteractionHint);
                view.UpdateDormitoryLocation(dormitory.LocationTitleAlpha);
            }
            if(Input.GetKeyDown(KeyCode.Return))
            {
                if(Page==FrontendPage.Title) view.SubmitTitleLogin();
                else if(Page==FrontendPage.Main) EnterMusic();
                else if(Page==FrontendPage.Songs||Page==FrontendPage.StoryLevels) view.ConfirmAlbum();
                else if(Page==FrontendPage.Results) Play(Result.Automatic);
                else if(Page==FrontendPage.StoryPlayer)
                {
                    if(storyCurrentNode!=null&&storyCurrentNode.kind==StoryRuntimeNodeKind.Stage) EnterStoryStage();
                    else AdvanceStory();
                }
            }
        }
        public void ShowBoot()
        {
            if(bootSequenceActive) return;
            bootSequenceActive=true;
            if(session!=null) session.SetMenuMode(true);
            Page=FrontendPage.Boot;
            SetMenuBgm(false,.15f);
            SetBootBgm(true,8.5f,true);
            view.ShowBootBlack();
            StartCoroutine(FinishBootSequence());
        }
        public void OpenPowerChoice()
        {
            if(Page!=FrontendPage.Title||UiTransitionLocked||powerTransition)return;
            Page=FrontendPage.PowerChoice;
            SetBootBgm(false,.65f);
            StartCoroutine(RevealPowerChoice());
        }
        IEnumerator RevealPowerChoice()
        {
            powerTransition=true;
            view.ShowBootBlack();
            yield return new WaitForSecondsRealtime(.7f);
            powerTransition=false;
            if(Page==FrontendPage.PowerChoice)view.ShowPowerChoice();
        }
        public void CancelPowerChoice()
        {
            if(Page!=FrontendPage.PowerChoice||powerTransition)return;
            SetBootBgm(true,.5f);
            ShowTitle();
        }
        public void ShutDownComputer()
        {
            if(Page!=FrontendPage.PowerChoice||powerTransition)return;
            Page=FrontendPage.ShutdownMonologue;
            SetMenuBgm(false,.4f);SetModeBgm(false,.4f);SetBootBgm(false,.4f);
            departure=view.ShowApartmentDeparture(()=>{departureCueCount=departure.CueCount;departure=null;EnterDormitory();});
        }
        public void QuitFromPowerChoice()
        {
            if(Page!=FrontendPage.PowerChoice||powerTransition)return;
            powerTransition=true;
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#endif
        }
        void EnterDormitory()
        {
            if(Page!=FrontendPage.ShutdownMonologue)return;
            Page=FrontendPage.Dormitory;
            var root=new GameObject("Dormitory / clear evening");
            root.transform.SetParent(transform,false);
            dormitory=root.AddComponent<DormitoryDuskScene>();
            dormitory.Initialize(()=>StartCoroutine(FadeOutDormitory()));
            outdoorDrinkDialogueShown=false;
            dormitory.DrinkCollected+=OnDormitoryDrinkCollected;
            view.ShowDormitory(dormitory.Output,dormitory);
            StartCoroutine(RevealDormitory());
        }
        void OnDormitoryDrinkCollected()
        {
            if(outdoorDrinkDialogueShown)return;
            outdoorDrinkDialogueShown=true;
            var script=Resources.Load<TextAsset>("Dialogue/Outdoor/Outdoor01");
            if(script==null){Debug.LogError("Missing outdoor dialogue: Outdoor01");return;}
            var pages=new List<MonologuePage>();
            foreach(string line in script.text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))
            {
                int split=line.IndexOf('：');
                if(split<0)continue;
                var expression=pages.Count==0?TuriExpression.Sarcastic:TuriExpression.Neutral;
                pages.Add(new MonologuePage(line.Substring(0,split),line.Substring(split+1),
                    TuriPortraits.Load(expression),CyberTheme.Purple));
            }
            PlayOutdoorDialogue(pages.ToArray());
        }

        // All outdoor scripts use this route: a clear world overlay and absolute speaker framing.
        // Supply a group of world transforms per page when more than one person is speaking.
        public void PlayOutdoorDialogue(MonologuePage[] pages,Transform[][] speakerGroups=null)
        {
            if(Page!=FrontendPage.Dormitory||dormitory==null||view.DiaryPromptActive||pages==null||pages.Length==0)return;
            view.UpdateDormitoryHint("");view.UpdateDormitoryLocation(0);
            view.SetDormitoryDialogue(true);
            view.ShowMonologueSequence(pages,.15f,()=>{if(dormitory!=null)dormitory.EndOutdoorDialogue();view.SetDormitoryDialogue(false);},
                false,index=>
                {
                    var group=speakerGroups!=null&&index<speakerGroups.Length?speakerGroups[index]:null;
                    dormitory.FocusOutdoorSpeakers(group??new[]{dormitory.OutdoorPlayer});
                });
        }

        IEnumerator RevealDormitory()
        {
            // Keep the first rendered exterior fully black, then reveal it continuously.
            view.SetPageAlpha(0);
            if(dormitory!=null)dormitory.RenderFrame();
            // Asset baking and the first GPU upload belong to the black frame, not the fade clock.
            yield return null;
            float started=Time.unscaledTime;
            while(Page==FrontendPage.Dormitory&&Time.unscaledTime-started<3.2f)
            {
                float t=Mathf.Clamp01((Time.unscaledTime-started)/3.2f);
                view.SetPageAlpha(t*t*t*(t*(t*6-15)+10));
                yield return null;
            }
            if(Page==FrontendPage.Dormitory)view.SetPageAlpha(1);
        }
        public void ReturnFromDormitory()
        {
            if(Page!=FrontendPage.Dormitory||powerTransition||dormitory==null)return;
            dormitory.TryAscend();
        }
        IEnumerator FadeOutDormitory()
        {
            if(powerTransition)yield break;
            powerTransition=true;
            float started=Time.unscaledTime;
            while(Time.unscaledTime-started<2.5f)
            {
                view.SetPageAlpha(1-Mathf.SmoothStep(0,1,(Time.unscaledTime-started)/2.5f));
                if(dormitory!=null)dormitory.SetExitFade(1-Mathf.SmoothStep(0,1,(Time.unscaledTime-started)/2.5f));
                yield return null;
            }
            view.SetPageAlpha(0);
            yield return new WaitForSecondsRealtime(.5f);
            if(dormitory!=null){dormitory.gameObject.SetActive(false);Destroy(dormitory.gameObject);dormitory=null;}
            powerTransition=false;
            ShowBoot();
        }
        IEnumerator DuskSmoke()
        {
            while(Page!=FrontendPage.Title)yield return null;
            yield return new WaitForSecondsRealtime(2f);
            Capture(Directory.GetCurrentDirectory(),"dusk-login-power-symbol");
            bool passed=view.Invoke("Turn off computer")&&Page==FrontendPage.PowerChoice;
            OpenPowerChoice(); // A repeated click must not create another transition.
            yield return new WaitForSecondsRealtime(1f);
            passed&=Capture(Directory.GetCurrentDirectory(),"dusk-choice");
            passed&=view.Invoke("Cancel power")&&Page==FrontendPage.Title;
            yield return new WaitForSecondsRealtime(.7f);
            passed&=view.Invoke("Turn off computer");
            yield return new WaitForSecondsRealtime(1f);
            passed&=view.Invoke("Leave computer")&&Page==FrontendPage.ShutdownMonologue;
            ShutDownComputer();
            yield return new WaitForSecondsRealtime(3.5f);
            passed&=departure!=null&&Page==FrontendPage.ShutdownMonologue;
            passed&=Capture(Directory.GetCurrentDirectory(),"departure-downstairs",false,true);
            float deadline=Time.unscaledTime+8;
            while(Page!=FrontendPage.Dormitory&&Time.unscaledTime<deadline)yield return null;
            passed&=departureCueCount==10;
            // Capture the real screen without the deterministic Capture helper clearing its shade.
            yield return new WaitForSecondsRealtime(.1f);
            Capture(Directory.GetCurrentDirectory(),"dusk-fade-start",preserveTransition:true);
            yield return new WaitForSecondsRealtime(1.5f);
            Capture(Directory.GetCurrentDirectory(),"dusk-fade-middle",preserveTransition:true);
            yield return new WaitForSecondsRealtime(1.6f);
            Capture(Directory.GetCurrentDirectory(),"dusk-fade-end",preserveTransition:true);
            yield return new WaitForSecondsRealtime(3.3f);
            passed&=Page==FrontendPage.Dormitory&&dormitory!=null;
            if(dormitory!=null)
            {
                dormitory.enabled=false;
                passed&=dormitory.MusicPlaying;
                passed&=dormitory.State==DormitoryDuskScene.TravelState.Walking;
                float start=dormitory.PlayerX;
                for(int i=0;i<20;i++)dormitory.Step(1,.05f);
                passed&=dormitory.PlayerX>start&&!dormitory.FacingLeft&&dormitory.CurrentFrame>=2;
                dormitory.RenderFrame();
                passed&=Capture(Directory.GetCurrentDirectory(),"dusk-scene");
                yield return new WaitForEndOfFrame();
                passed&=!dormitory.TryUseVending();
                dormitory.MoveTo(6.6f);
                for(int i=0;i<110;i++)dormitory.Step(0,.02f);
                passed&=Mathf.Abs(dormitory.PlayerX-6.6f)<.01f&&dormitory.CoinHintVisible;
                float wide=dormitory.ZoomSize;
                dormitory.ScrollZoom(3);
                passed&=Mathf.Abs(dormitory.ZoomSize-wide)<.001f;
                for(int i=0;i<30;i++)dormitory.Step(0,.05f);
                passed&=dormitory.ZoomSize<wide-.5f&&dormitory.ZoomSize>3.8f;
                yield return null;
                passed&=Capture(Directory.GetCurrentDirectory(),"dusk-coin-hint-zoom");
                dormitory.ScrollZoom(-100);
                for(int i=0;i<100;i++)dormitory.Step(0,.05f);
                passed&=Mathf.Abs(dormitory.ZoomSize-7.5f)<.01f;
                passed&=dormitory.NearVending&&dormitory.TryUseVending()&&dormitory.TryUseVending();
                for(int i=0;i<52;i++)dormitory.Step(0,.05f);
                passed&=dormitory.DrinkReady;
                passed&=!dormitory.OutdoorDialogueActive;
                dormitory.RenderFrame();
                yield return null;
                passed&=Capture(Directory.GetCurrentDirectory(),"dusk-vending");
                passed&=dormitory.TryUseVending()&&!dormitory.DrinkReady&&dormitory.DrinksCollected==1;
                passed&=view.DiaryPromptActive&&!view.MonologueDimsBackground&&dormitory.OutdoorDialogueActive;
                float conversationX=dormitory.PlayerX;
                for(int i=0;i<30;i++)dormitory.Step(1,.05f);
                passed&=Mathf.Abs(dormitory.PlayerX-conversationX)<.001f&&!dormitory.TryUseVending();
                passed&=Mathf.Abs(dormitory.ActualCameraSize-DormitoryDuskScene.OutdoorConversationSize)<.01f;
                yield return new WaitForSecondsRealtime(.65f);view.AdvanceMonologue();
                passed&=Capture(Directory.GetCurrentDirectory(),"dusk-outdoor-01");
                yield return new WaitForSecondsRealtime(.2f);view.AdvanceMonologue();
                yield return new WaitForSecondsRealtime(.2f);view.AdvanceMonologue();
                passed&=Capture(Directory.GetCurrentDirectory(),"dusk-outdoor-02");
                yield return new WaitForSecondsRealtime(.2f);view.AdvanceMonologue();
                for(int i=0;i<20;i++)dormitory.Step(0,.05f);
                passed&=!dormitory.OutdoorInputLocked&&Mathf.Abs(dormitory.ActualCameraSize-7.5f)<.01f;
                dormitory.ScrollZoom(100);
                for(int i=0;i<80;i++)dormitory.Step(0,.05f);
                passed&=Mathf.Abs(dormitory.ActualCameraSize-3.8f)<.01f;
                dormitory.FocusOutdoorSpeakers(dormitory.OutdoorPlayer);
                for(int i=0;i<30;i++)dormitory.Step(0,.05f);
                passed&=Mathf.Abs(dormitory.ActualCameraSize-DormitoryDuskScene.OutdoorConversationSize)<.01f;
                dormitory.EndOutdoorDialogue();
                for(int i=0;i<20;i++)dormitory.Step(0,.05f);
                passed&=Mathf.Abs(dormitory.ActualCameraSize-3.8f)<.01f;
                dormitory.ScrollZoom(-100);
                for(int i=0;i<80;i++)dormitory.Step(0,.05f);
                passed&=dormitory.TryUseVending()&&dormitory.DrinksCollected==1;
                for(int i=0;i<52;i++)dormitory.Step(0,.05f);
                passed&=dormitory.TryUseVending()&&dormitory.DrinksCollected==2&&!dormitory.OutdoorDialogueActive;
                for(int i=0;i<250;i++)dormitory.Step(-1,.05f);
                passed&=Mathf.Abs(dormitory.PlayerX+10.5f)<.01f&&dormitory.FacingLeft;
                for(int i=0;i<250;i++)dormitory.Step(1,.05f);
                passed&=Mathf.Abs(dormitory.PlayerX-10.5f)<.01f;
                passed&=!dormitory.TryAscend(); // E cannot return from across the courtyard.
                for(int i=0;i<84;i++)dormitory.Step(-1,.05f);
                passed&=dormitory.CanUseStairs;
                ReturnFromDormitory();
                passed&=dormitory.State==DormitoryDuskScene.TravelState.Ascending;
                passed&=!dormitory.TryAscend(); // Repeated E must not restart the animation.
                for(int i=0;i<61;i++)dormitory.Step(1,.05f);
                passed&=dormitory.State==DormitoryDuskScene.TravelState.Finished;
                passed&=Page==FrontendPage.Dormitory; // Keep the scene alive throughout the fade.
                yield return new WaitForSecondsRealtime(3.2f);
                passed&=Page==FrontendPage.Boot;
                yield return null;
                passed&=FindObjectsOfType<DormitoryDuskScene>().Length==0;
            }
            Debug.Log("DUSK_SMOKE PASS="+passed);
            Application.Quit(passed?0:1);
        }
        IEnumerator FinishBootSequence()
        {
            yield return new WaitForSecondsRealtime(2f);
            if(Page!=FrontendPage.Boot){bootSequenceActive=false;yield break;}
            view.ShowBoot();
            float minimum=Time.realtimeSinceStartup+4.55f;
            float readyAt=-1f;
            while(true)
            {
                if(session!=null&&session.Ready&&readyAt<0f)
                    readyAt=Time.realtimeSinceStartup;
                bool minimumElapsed=Time.realtimeSinceStartup>=minimum;
                bool assetsReady=session==null||session.Ready||session.LoadError!=null;
                if(minimumElapsed&&assetsReady&&
                    (readyAt<0f||Time.realtimeSinceStartup>=readyAt+.65f))
                    break;
                yield return null;
            }
            bootSequenceActive=false;
            if(Page==FrontendPage.Boot) ShowTitle(false);
        }
        public void ShowTitle(bool restartMusic=false)
        {
            bootSequenceActive=false;
            if(restartMusic)RestartBootBgm(3f);
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
            SetBootBgm(false,1.2f);
            view.SetTitleVideoDucked(true);
            PlayUiCue("login");

            const float exitDuration=3f;
            float elapsed=0;
            while(elapsed<exitDuration)
            {
                elapsed+=Time.unscaledDeltaTime;
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/exitDuration));
                view.SetPageAlpha(1-t);
                yield return null;
            }
            ShowMain(1.8f,true,1.8f);
            if(!smoke)ShowLoginMonologue();
            enteringMain=false;
        }
        void ShowLoginMonologue()
        {
            loginMonologueCompleted=false;
            friendsMonologueShown=false;
            var accent=CyberTheme.Purple;
            view.ShowMonologueSequence(new[]
            {
                new MonologuePage("Turi","人们总是点开了电脑，但是不知道干点什么。",
                    TuriPortraits.Load(TuriExpression.Neutral),accent),
                new MonologuePage("Turi","我也不例外....",
                    TuriPortraits.Load(TuriExpression.Sarcastic),accent),
                new MonologuePage("Turi","每天打开电脑就是看看聊天软件有没有人给我发消息，不知道为什么，只要有人私聊我我就会莫名地感到安心啊。",
                    TuriPortraits.Load(TuriExpression.Shy),accent),
                new MonologuePage("Turi","看看新闻，然后检查一下到底有没有人在意我吧。",
                    TuriPortraits.Load(TuriExpression.Focused),accent)
            },1.8f,()=>loginMonologueCompleted=true);
        }
        public void ShowMain()
        {
            ShowMain(.55f,false,.55f);
        }
        public void ActivateWindow(FrontendPage target)
        {
            if(target!=FrontendPage.Main&&target!=FrontendPage.Diary&&
                target!=FrontendPage.Friends&&target!=FrontendPage.News&&target!=FrontendPage.MusicPlayer)
                return;
            if(!view.ActivateWindow(target))return;
            if(session!=null)session.SetMenuMode(true);
            SetModeBgm(false,.25f);
            SetMenuBgm(true,.35f);
            Page=target;
        }
        public void FocusDesktopWindow(FrontendPage target)
        {
            if(target==FrontendPage.Main||target==FrontendPage.Device||
                target==FrontendPage.Diary||target==FrontendPage.Friends||target==FrontendPage.News||target==FrontendPage.MusicPlayer)
                Page=target;
        }
        public void ShowMusicPlayer()
        {
            if(session!=null)session.SetMenuMode(true);
            SetModeBgm(false,.35f);
            Page=FrontendPage.MusicPlayer;SetMenuBgm(true,.45f);
            if(!view.ActivateWindow(FrontendPage.MusicPlayer))view.ShowMusicPlayer();
        }
        public void CloseMusicPlayer()
        {
            view.CloseMusicPlayer();
            FocusDesktopWindow(view.FrontmostWindow);
        }
        void ShowMain(float bgmFade,bool startSilent,float pageFade)
        {
            if(session!=null) session.SetMenuMode(true);
            SetModeBgm(false,.35f);
            Page=FrontendPage.Main;SetMenuBgm(true,bgmFade,startSilent);view.ShowMain(pageFade,false,StoryLevelIndex);
        }
        public void ShowNews()
        {
            if(session!=null) session.SetMenuMode(true);
            SetModeBgm(false,.35f);
            Page=FrontendPage.News;SetMenuBgm(true,.55f);
            if(!view.ActivateWindow(FrontendPage.News))view.ShowNews();
        }
        public void ShowFriends()
        {
            if(!smoke&&loginMonologueCompleted&&!friendsMonologueShown)
            {
                friendsMonologueShown=true;
                var accent=CyberTheme.Purple;
                view.ShowMonologueSequence(new[]
                {
                    new MonologuePage("Turi","没想到真有人给我发消息啊，嘿嘿....",
                        TuriPortraits.Load(TuriExpression.Happy),accent),
                    new MonologuePage("Turi","看看他说了些什么吧。",
                        TuriPortraits.Load(TuriExpression.Focused),accent)
                },0f,OpenFriendsWindow);
                return;
            }
            OpenFriendsWindow();
        }
        void OpenFriendsWindow()
        {
            if(session!=null) session.SetMenuMode(true);
            SetModeBgm(false,.35f);
            Page=FrontendPage.Friends;SetMenuBgm(true,.55f);
            if(!view.ActivateWindow(FrontendPage.Friends))view.ShowFriends();
        }
        public void OpenFriendsFromDevice()
        {
            friendsOpenedFromDevice=true;
            ShowFriends();
        }
        public void ShowDeviceHome()
        {
            if(session!=null) session.SetMenuMode(true);
            SetModeBgm(false,.35f);
            Page=FrontendPage.Device;SetMenuBgm(true,.45f);view.ShowDeviceHome();
        }
        public void CloseDeviceHome()
        {
            Page=FrontendPage.Main;
            view.CloseDeviceHome();
            ShowQueuedBakaMonologue();
        }
        public void ShowDiary()
        {
            if(session!=null) session.SetMenuMode(true);
            SetModeBgm(false,.35f);
            Page=FrontendPage.Diary;SetMenuBgm(true,.45f);
            if(!view.ActivateWindow(FrontendPage.Diary))view.ShowDiary();
        }
        public void OpenDiaryFromDevice()
        {
            diaryOpenedFromDevice=true;
            ShowDiary();
        }
        public void CloseDiaryToDevice()
        {
            if(Page!=FrontendPage.Diary||exitingDiary)return;
            StartCoroutine(ExitDiarySequence());
        }
        IEnumerator ExitDiarySequence()
        {
            exitingDiary=true;
            view.BeginDiaryExit();
            yield return new WaitForSecondsRealtime(view.DiaryTransitionDuration);
            bool returnToDevice=diaryOpenedFromDevice;
            diaryOpenedFromDevice=false;
            view.CloseDiaryWindow();
            if(returnToDevice)view.FocusWindow(FrontendPage.Device);
            else FocusDesktopWindow(view.FrontmostWindow);
            exitingDiary=false;
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
            if(friendsOpenedFromDevice)
            {
                friendsOpenedFromDevice=false;
                view.FocusWindow(FrontendPage.Device);
            }
            else
            {
                FocusDesktopWindow(view.FrontmostWindow);
                ShowQueuedBakaMonologue();
            }
            exitingFriends=false;
        }
        void ShowQueuedBakaMonologue()
        {
            if(!view.ConsumeBakaMonologue())return;
            var accent=CyberTheme.Purple;
            view.ShowMonologueSequence(new[]
            {
                new MonologuePage("Turi","OK，今天的第一单外快。",
                    TuriPortraits.Load(TuriExpression.Happy),accent),
                new MonologuePage("Turi","至于老板那边的活？不急不急~",
                    TuriPortraits.Load(TuriExpression.Sarcastic),accent),
                new MonologuePage("Turi","现在我应该“开始演奏”了。",
                    TuriPortraits.Load(TuriExpression.Focused),accent)
            });
        }
        public void ConfirmReturnFromMain()
        {
            if(Page!=FrontendPage.Main||confirmingReturn)return;
            StartCoroutine(ConfirmReturnSequence());
        }
        IEnumerator ConfirmReturnSequence()
        {
            confirmingReturn=true;
            PlayUiCue("logout");
            view.BeginReturnExit();
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            view.FinishReturnExit();
            ShowTitle(true);
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
            bool bakaInvitationSent=view.FinishNewsExit();
            FocusDesktopWindow(view.FrontmostWindow);
            exitingNews=false;
            if(bakaInvitationSent&&!smoke)
            {
                var accent=CyberTheme.Purple;
                view.ShowMonologueSequence(new[]
                {
                    new MonologuePage("Turi","呀，那个人加我好友了。",
                        TuriPortraits.Load(TuriExpression.Surprised),accent),
                    new MonologuePage("Turi","在好友界面同意他的好友请求然后看看他想说什么吧。",
                        TuriPortraits.Load(TuriExpression.Focused),accent)
                },.18f);
            }
        }
        public void ShowSongs()
        {
            storyStageActive=false;
            if(session!=null)
            {
                session.SetMenuMode(true);
                // The same chart/session may have just been used by a story stage.
                session.ReturnToSongs=ShowSongs;
            }
            SetMenuBgm(false,.25f);SetModeBgm(true,.45f);
            Page=FrontendPage.Songs;view.ShowSongs(songs,Selected);
        }
        public void EnterMusic()
        {
            if(Page!=FrontendPage.Main||enteringSongs)return;
            PlayUiCue("performance");
            SetMenuBgm(false,.25f);
            SetModeBgm(true,2.5f,true);
            PlayModeLoadingTransition();
        }

        IEnumerator RunSignalTransition(float collapseDuration,float expandDuration,float ringDuration,Action switchPage)
        {
            view.BeginModeGate();
            float elapsed=0;
            collapseDuration=Mathf.Max(.05f,collapseDuration);
            expandDuration=Mathf.Max(.05f,expandDuration);
            ringDuration=Mathf.Max(expandDuration,ringDuration);
            while(elapsed<collapseDuration)
            {
                elapsed+=Time.unscaledDeltaTime;
                view.SetModeGateIn(Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/collapseDuration)));
                yield return null;
            }
            view.SetModeGateIn(1f);
            switchPage?.Invoke();
            view.BringModeGateToFront();
            elapsed=0;
            while(elapsed<ringDuration)
            {
                elapsed+=Time.unscaledDeltaTime;
                view.SetModeGateOut(Mathf.Clamp01(elapsed/expandDuration));
                yield return null;
            }
            view.SetModeGateOut(1f);
            view.EndModeGate();
        }

        /// <summary>Reusable legacy transition: non-linear loading bar plus staggered curtains.</summary>
        public void PlayModeLoadingTransition()
        {
            if(enteringSongs)return;
            enteringSongs=true;
            StartCoroutine(EnterModesWithLoadingSequence());
        }

        IEnumerator EnterModesWithLoadingSequence()
        {
            if(session!=null)session.SetMenuMode(true);
            SetMenuBgm(false,.25f);
            SetModeBgm(true,.25f);
            view.BeginModeGate();
            float gateElapsed=0;
            const float enterGateIn=.48f;
            while(gateElapsed<enterGateIn)
            {
                gateElapsed+=Time.unscaledDeltaTime;
                view.SetModeGateIn(Mathf.SmoothStep(0,1,Mathf.Clamp01(gateElapsed/enterGateIn)));
                yield return null;
            }
            view.SetModeGateIn(1f);
            Page=FrontendPage.MainTransitionLoad;
            view.ShowModeLoading();
            view.BringModeGateToFront();
            gateElapsed=0;
            const float enterGateOut=.42f;
            while(gateElapsed<enterGateOut)
            {
                gateElapsed+=Time.unscaledDeltaTime;
                view.SetModeGateOut(Mathf.SmoothStep(0,1,Mathf.Clamp01(gateElapsed/enterGateOut)));
                yield return null;
            }
            view.EndModeGate();
            float elapsed=0;
            const float loadDuration=2.1f;
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
            PlayUiCue("ready");
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
            SetMenuBgm(false,.25f);SetModeBgm(true,.45f);
            Page=FrontendPage.SongModes;view.ShowSongModes(false);
        }
        public void ShowStoryLevels()
        {
            if(session!=null)session.SetMenuMode(true);
            SetMenuBgm(false,.25f);SetModeBgm(true,.45f);
            Page=FrontendPage.StoryLevels;view.ShowStoryLevels(StoryLevelIndex);
        }
        public void ReturnToMainFromModes()
        {
            if(Page!=FrontendPage.SongModes||returningFromSongs)return;
            returningFromSongs=true;
            PlayUiCue("close");
            StartCoroutine(ReturnToMainFromModesSequence());
        }

        IEnumerator ReturnToMainFromModesSequence()
        {
            yield return RunSignalTransition(.7f,.74f,1.58f,()=>{
                if(session!=null)session.SetMenuMode(true);
                Page=FrontendPage.Main;
                SetModeBgm(false,.35f);
                SetMenuBgm(true,.45f);
                view.ShowMain(.18f,false,StoryLevelIndex);
            });
            returningFromSongs=false;
        }
        public void SelectSong(int index)
        {
            if(index<0 || index>=songs.Count) return;
            Selected=index;view.ShowSongs(songs,Selected);
        }
        public void Play(bool automatic)
        {
            if(songs.Count==0 || Page==FrontendPage.Playing || Page==FrontendPage.Loading) return;
            PlayUiCue("stage-start");
            if(loadedIndex!=Selected || session==null || session.LoadError!=null) PrepareSession(Selected);
            SetMenuBgm(false,.35f);SetModeBgm(false,.35f);pendingAuto=automatic;
            Page=FrontendPage.Loading;view.ShowLoading(songs[Selected].ShortTitle);
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
            SetModeBgm(false,.35f);
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
            SetMenuBgm(false,.25f);
            SetModeBgm(true,.45f);
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
            if(menuBgm!=null)menuBgm.Stop();
            if(bootBgm!=null)bootBgm.Stop();
            if(modeBgm!=null)modeBgm.Stop();
            if(storyBgm!=null)storyBgm.Stop();
            if(externalAsset!=null) Destroy(externalAsset);
        }

        // Deterministic UI integration checks use actual Button callbacks and a rendered player.
        // This opt-in fast path is QA only; normal play always reaches results from the DSP clock.
        IEnumerator SmokeFlow(string[] args)
        {
            string smokeTrace="";
            int arg=Array.IndexOf(args,"-demoCapture");
            string directory=arg>=0 && arg+1<args.Length?args[arg+1]:Path.Combine(Application.persistentDataPath,"FrontendCaptures");
            Directory.CreateDirectory(directory);
            float deadline=Time.realtimeSinceStartup+30;
            while(session!=null && !session.Ready && session.LoadError==null && Time.realtimeSinceStartup<deadline) yield return null;
            bool passed=session!=null && session.Ready && Page==FrontendPage.Boot && session.IsPaused;
            if(!passed) {Debug.LogError("GEOMETRY_FRONTEND_SMOKE initialization failed");Application.Quit(1);yield break;}
            yield return null;Capture(directory,"ui-boot-black");
            deadline=Time.realtimeSinceStartup+4;
            while(!view.BootProgressVisible&&Time.realtimeSinceStartup<deadline)yield return null;
            passed &= view.BootProgressVisible;
            passed &= Capture(directory,"ui-boot");
            deadline=Time.realtimeSinceStartup+8;
            while(Page==FrontendPage.Boot && Time.realtimeSinceStartup<deadline) yield return null;
            passed &= Page==FrontendPage.Title;
            deadline=Time.realtimeSinceStartup+8;
            while(view.TitleVideoAvailable && !view.TitleVideoPlaying && Time.realtimeSinceStartup<deadline) yield return null;
            // Inspect the settled login, after its delayed input-enable fade and button tint.
            yield return new WaitForSecondsRealtime(1.85f);
            passed &= view.ButtonCount==2 && view.TitleUsesIndependentArtwork
                && view.HasTitlePasswordField
                && view.TitleAvatarLoaded
                && (!view.TitleVideoAvailable || view.TitleVideoPlaying)
                && BeginCueAvailable && !view.Invoke("Open songs") && !MenuBgmActive;
            passed &= Capture(directory,"ui-title");
            var powerSymbol=FindObjectOfType<PowerSymbolGraphic>();
            passed &= powerSymbol!=null&&powerSymbol.GetComponent<CanvasRenderer>()!=null;
            view.SetTitlePassword("1111");
            passed &= view.Invoke("Enter game")&&Page==FrontendPage.Title&&view.TitleLoginErrorVisible;
            yield return new WaitForSecondsRealtime(.2f);
            passed &= Capture(directory,"ui-title-error");
            yield return new WaitForSecondsRealtime(1.05f);
            passed &= !view.TitleLoginErrorVisible;
            view.SetTitlePassword("0000");
            yield return null;
            passed &= Capture(directory,"ui-title-password");
            passed &= view.Invoke("Enter game");
            deadline=Time.realtimeSinceStartup+5;
            while(Page==FrontendPage.Title && Time.realtimeSinceStartup<deadline) yield return null;
            passed &= Page==FrontendPage.Main && view.ButtonCount==9 && MenuBgmActive;
            smokeTrace+="main="+passed+";";
            yield return null;passed &= Capture(directory,"ui-main");
            if(Array.IndexOf(args,"-startupSmoke")>=0)
            {
                Debug.Log("GEOMETRY_STARTUP_SMOKE PASS="+passed+" / boot, password, main, power symbol renderer");
                Application.Quit(passed?0:1);yield break;
            }
            passed &= view.HomeBackgroundLoaded;
            int originalMusicTrack=SelectedMusicTrack;
            float originalMusicVolume=MenuBgmVolume;
            for(int i=0;i<MusicTrackCount;i++)passed &= MusicTrackAvailable(i);
            passed &= view.Invoke("Open music player")&&Page==FrontendPage.MusicPlayer;
            yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            passed &= Capture(directory,"ui-music-player");
            passed &= view.Invoke("Select music track 1")&&SelectedMusicTrack==1;
            passed &= view.SetMusicPlayerVolumeForTest(.31f);
            passed &= view.Invoke("Minimize music player");
            yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            passed &= view.MusicWindowMinimized&&view.TaskbarTaskCount==2;
            passed &= view.Invoke("Restore musicplayer")&&Page==FrontendPage.MusicPlayer;
            yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            passed &= !view.MusicWindowMinimized;
            passed &= view.Invoke("Close music player");
            yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            passed &= Page==FrontendPage.Main&&view.TaskbarTaskCount==1;
            passed &= SelectMusicTrack(originalMusicTrack);
            SetMenuBgmVolume(originalMusicVolume);
            smokeTrace+="music="+passed+";";
            passed &= view.Invoke("Restore main");
            yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            passed &= view.MainWindowMinimized&&view.TaskbarTaskCount==1;
            yield return null;passed &= Capture(directory,"ui-main-minimized");
            passed &= view.Invoke("Restore main");
            yield return new WaitForSecondsRealtime(.45f);
            passed &= !view.MainWindowMinimized;
            passed &= view.Invoke("Open diary")&&Page==FrontendPage.Diary;
            yield return new WaitForSecondsRealtime(.45f);
            yield return null;passed &= Capture(directory,"ui-diary");
            passed &= view.Invoke("Restore diary");
            yield return new WaitForSecondsRealtime(view.DiaryTransitionDuration);
            passed &= view.DiaryWindowMinimized&&view.TaskbarTaskCount==2;
            yield return null;passed &= Capture(directory,"ui-diary-minimized");
            passed &= view.Invoke("Open friends")&&Page==FrontendPage.Friends;
            yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            passed &= view.Invoke("Restore friends");
            yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            passed &= view.FriendsWindowMinimized&&view.DiaryWindowMinimized&&view.TaskbarTaskCount==3;
            yield return null;passed &= Capture(directory,"ui-multi-taskbar");
            smokeTrace+="tasks1="+passed+";";
            passed &= view.Invoke("Restore diary")&&Page==FrontendPage.Diary&&view.FriendsWindowMinimized;
            yield return new WaitForSecondsRealtime(.45f);
            passed &= view.Invoke("Restore friends")&&Page==FrontendPage.Friends&&!view.DiaryWindowMinimized;
            smokeTrace+="tasks2="+passed+";";
            yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            yield return null;passed &= Capture(directory,"ui-friends");
            passed &= view.Invoke("Minimize friends");
            yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            passed &= view.FriendsWindowMinimized;
            smokeTrace+="friend-min="+passed+";";
            yield return null;passed &= Capture(directory,"ui-friends-minimized");
            passed &= view.Invoke("Restore friends");
            yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            passed &= !view.FriendsWindowMinimized;
            if(Array.IndexOf(args,"-desktopDockSmoke")>=0)
            {
                passed &= view.Invoke("Open news");
                yield return new WaitForSecondsRealtime(view.NewsTransitionDuration);
                passed &= view.Invoke("Open music player");
                yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
                passed &= view.TaskbarTaskCount==5;
                passed &= Capture(directory,"ui-dock-five-tasks");
                passed &= view.Invoke("Minimize music player");
                yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
                passed &= view.MusicWindowMinimized;
                passed &= view.Invoke("Restore main");
                yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
                passed &= Page==FrontendPage.Main&&!view.MainWindowMinimized;
                passed &= view.Invoke("Open start menu");
                yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
                passed &= Capture(directory,"ui-start-menu");
                passed &= view.Invoke("Close start menu");
                yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
                passed &= view.Invoke("Restore news");
                yield return new WaitForSecondsRealtime(view.NewsTransitionDuration);
                passed &= view.Invoke("Minimize news");
                yield return new WaitForSecondsRealtime(view.NewsTransitionDuration);
                passed &= view.Invoke("Restore news");
                yield return new WaitForSecondsRealtime(view.NewsTransitionDuration);
                passed &= Page==FrontendPage.News;
                File.WriteAllText(Path.Combine(directory,"desktop-dock-smoke.txt"),
                    "PASS="+passed+"\nTrace="+smokeTrace+"\nTasks="+view.TaskbarTaskCount);
                Debug.Log("GEOMETRY_DESKTOP_DOCK_SMOKE PASS="+passed);
                Application.Quit(passed?0:1);yield break;
            }
            passed &= view.Invoke("Open friend 0");yield return null;
            for(int exchange=0;exchange<4;exchange++)
            {
                deadline=Time.realtimeSinceStartup+12;
                while(!view.FriendComposerReady&&Time.realtimeSinceStartup<deadline)yield return null;
                passed &= view.FriendComposerReady;
                while(!view.FriendMessageReadyToSend){view.TypeFriendCharacterForTest();yield return null;}
                if(exchange==0)passed &= Capture(directory,"ui-friend-chat");
                else if(exchange==1)passed &= Capture(directory,"ui-friend-reply");
                passed &= view.Invoke("Send friend message");
                yield return null;
            }
            deadline=Time.realtimeSinceStartup+12;
            while(!view.FriendConversationComplete&&Time.realtimeSinceStartup<deadline)yield return null;
            smokeTrace+="friend-complete="+view.FriendConversationComplete+";";
            passed &= view.FriendConversationComplete;
            bool backToFriends=view.Invoke("Back to friends");
            bool backToMain=view.Invoke("Back to main");
            smokeTrace+="friend-return="+backToFriends+"/"+backToMain+";";
            passed &= backToFriends&&backToMain;
            deadline=Time.realtimeSinceStartup+5;
            while(Page==FrontendPage.Friends&&Time.realtimeSinceStartup<deadline)yield return null;
            smokeTrace+="friend-state="+Page+"/"+view.NewsPostCount+"/"+view.NewsUnread+";";
            passed &= Page==FrontendPage.Diary&&view.NewsPostCount==3&&view.NewsUnread;yield return null;
            smokeTrace+="friends="+passed+";";
            passed &= view.Invoke("Open news") && Page==FrontendPage.News;
            yield return new WaitForSecondsRealtime(view.NewsTransitionDuration);
            yield return null;passed &= Capture(directory,"ui-news");
            passed &= view.Invoke("Restore news");
            yield return new WaitForSecondsRealtime(view.NewsTransitionDuration);
            passed &= view.NewsWindowMinimized&&!view.DiaryWindowMinimized&&view.TaskbarTaskCount==3;
            yield return null;passed &= Capture(directory,"ui-news-minimized");
            passed &= view.Invoke("Restore news")&&Page==FrontendPage.News&&!view.NewsWindowMinimized;
            yield return new WaitForSecondsRealtime(view.NewsTransitionDuration);
            passed &= view.Invoke("Restore diary")&&Page==FrontendPage.Diary;
            passed &= view.Invoke("Restore news")&&Page==FrontendPage.News&&!view.DiaryWindowMinimized;
            passed &= Capture(directory,"ui-window-stack");
            smokeTrace+="news-open="+passed+";";
            view.OpenNewsPostFromNotification(0);yield return null;
            passed &= Capture(directory,"ui-news-detail");
            smokeTrace+="news-detail="+passed+";";
            bool likedBefore=view.NewsPostLiked(0);
            passed &= view.Invoke("Like post")&&view.NewsPostLiked(0)!=likedBefore;
            smokeTrace+="news-like="+passed+";";
            passed &= view.Invoke("Post comment");yield return null;
            passed &= Capture(directory,"ui-news-comment");
            smokeTrace+="news-comment="+passed+";";
            passed &= view.Invoke("Close detail");
            yield return new WaitForSecondsRealtime(view.NewsDetailTransitionDuration);
            passed &= view.Invoke("Back to device");
            deadline=Time.realtimeSinceStartup+3;
            while(UiTransitionLocked&&Time.realtimeSinceStartup<deadline)yield return null;
            passed &= Page==FrontendPage.News&&view.TaskbarTaskCount==2;
            passed &= view.Invoke("Back to main");
            smokeTrace+="news-return="+passed+";";
            deadline=Time.realtimeSinceStartup+5;
            while(Page==FrontendPage.News && Time.realtimeSinceStartup<deadline) yield return null;
            passed &= Page==FrontendPage.Main;yield return null;
            passed &= view.TaskbarTaskCount==1;
            smokeTrace+="menu="+passed+";";
            passed &= view.Invoke("Open start menu");
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= Capture(directory,"ui-start-menu");
            smokeTrace+="start="+passed+";";
            passed &= view.Invoke("Open music from start")&&Page==FrontendPage.MusicPlayer;
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= Capture(directory,"ui-music-from-start");
            smokeTrace+="music-start="+passed+";";
            passed &= view.Invoke("Close music player");
            yield return new WaitForSecondsRealtime(view.FriendTransitionDuration);
            passed &= view.Invoke("Open start menu");
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= view.Invoke("Open background menu");
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= Capture(directory,"ui-background-menu");
            smokeTrace+="background="+passed+";";
            int backgroundIndex=view.HomeBackgroundIndex;
            int nextBackground=(backgroundIndex+1)%3;
            passed &= view.Invoke("Select background "+(nextBackground+1))
                && view.HomeBackgroundIndex==nextBackground && view.HomeBackgroundLoaded;
            smokeTrace+="background-select="+passed+";";
            yield return new WaitForSecondsRealtime(.8f);passed &= Capture(directory,"ui-main-alt");
            passed &= view.Invoke("Open start menu");
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= view.Invoke("Open settings");
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= Capture(directory,"ui-controls");
            smokeTrace+="controls="+passed+";";
            passed &= view.Invoke("Close controls");
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= view.Invoke("Open start menu");
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= view.Invoke("Return to title") && Page==FrontendPage.Main;
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            passed &= Capture(directory,"ui-return-confirm");
            smokeTrace+="return="+passed+";";
            passed &= view.Invoke("Cancel return") && Page==FrontendPage.Main;
            yield return new WaitForSecondsRealtime(view.PopupTransitionDuration);
            smokeTrace+="popup="+passed+";";
            passed &= view.Invoke("Open songs");
            deadline=Time.realtimeSinceStartup+8;
            while(Page!=FrontendPage.SongModes&&Time.realtimeSinceStartup<deadline) yield return null;
            passed &= Page==FrontendPage.SongModes;
            yield return null;passed &= Capture(directory,"ui-song-modes");
            smokeTrace+="modes="+passed+";";
            deadline=Time.realtimeSinceStartup+8;
            while(UiTransitionLocked&&Time.realtimeSinceStartup<deadline)yield return null;
            passed &= view.Invoke("Open free mode");
            deadline=Time.realtimeSinceStartup+8;
            while(UiTransitionLocked&&Time.realtimeSinceStartup<deadline)yield return null;
            passed &= Page==FrontendPage.Songs&&view.AlbumEntryReady&&!view.AlbumRouteVisible;
            yield return null;passed &= Capture(directory,"ui-songs");
            // Inspect short-lived and fallback screens without changing the shipped catalog.
            view.ShowLoading(songs[Selected].ShortTitle);yield return null;
            passed &= Capture(directory,"ui-loading");
            view.ShowSongs(songs,Selected);view.ShowError("QA preview: the selected audio resource could not be found.");yield return null;
            passed &= Capture(directory,"ui-error");
            view.ShowSongs(new List<SongEntry>(),0);yield return null;
            passed &= Capture(directory,"ui-empty");
            view.ShowSongs(songs,Selected);
            bool autoplayInvoked=view.Invoke("Watch autoplay");
            smokeTrace+="auto-invoke="+autoplayInvoked+";";
            passed &= autoplayInvoked;
            deadline=Time.realtimeSinceStartup+15;
            while(Page==FrontendPage.Loading&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;
            smokeTrace+="auto-page="+Page+";ready="+(session!=null&&session.Ready)+";";
            passed &= Page==FrontendPage.Playing && session.autoPlay && !session.IsPaused;
            smokeTrace+="play="+passed+";";
            // Exercise pause -> return and confirm that menu time does not advance.
            session.TogglePause();yield return null;
            passed &= Capture(directory,"ui-pause",true);
            session.ReturnToSongs();double frozen=session.CurrentTime;
            yield return null;passed &= Page==FrontendPage.Songs && session.IsPaused && session.CurrentTime==frozen;
            passed &= view.Invoke("Play chart");
            deadline=Time.realtimeSinceStartup+15;
            while(Page==FrontendPage.Loading&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;
            passed &= Page==FrontendPage.Playing && !session.autoPlay && session.Engine.Judged==0;
            session.Engine.Advance(session.Duration+.2,false);
            ShowResults();passed &= Result.Miss==songs[Selected].Chart.notes.Length && !Result.Automatic;
            smokeTrace+="manual="+passed+";";
            passed &= view.Invoke("Retry");
            deadline=Time.realtimeSinceStartup+15;
            while(Page==FrontendPage.Loading&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;
            passed &= Page==FrontendPage.Playing && session.Engine.Judged==0;
            ShowSongs();Play(true);
            deadline=Time.realtimeSinceStartup+15;
            while(Page==FrontendPage.Loading&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;
            session.Engine.Advance(session.Duration,true);ShowResults();
            passed &= Result.Score==1000000 && Result.Perfect==songs[Selected].Chart.notes.Length && Result.Automatic;
            smokeTrace+="auto="+passed+";";
            yield return null;passed &= Capture(directory,"ui-results");
            passed &= view.Invoke("Back to songs") && Page==FrontendPage.Songs;
            deadline=Time.realtimeSinceStartup+8;
            while(UiTransitionLocked&&Time.realtimeSinceStartup<deadline)yield return null;
            passed &= view.Invoke("Back to modes") && Page==FrontendPage.SongModes;
            deadline=Time.realtimeSinceStartup+8;
            while(UiTransitionLocked&&Time.realtimeSinceStartup<deadline)yield return null;
            passed &= view.Invoke("Back to main");
            deadline=Time.realtimeSinceStartup+8;
            while(Page!=FrontendPage.Main&&Time.realtimeSinceStartup<deadline) yield return null;
            while(UiTransitionLocked&&Time.realtimeSinceStartup<deadline)yield return null;
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
            smokeTrace+="catalog="+passed+";";
            ShowTitle();songs.RemoveAt(bundledCount);Destroy(alternateAsset);
            File.WriteAllText(Path.Combine(directory,"frontend-smoke.txt"),"PASS="+passed+"\nSongs="+songs.Count+"\nAutoScore="+Result.Score+"\nAutoPerfect="+Result.Perfect+"/"+Result.Total+"\nAutoMode="+Result.Automatic+"\nTrace="+smokeTrace+"\nFlow=Boot/Title/Password/Main/Songs/Play/Pause/Return/Manual/Results/Retry/Autoplay/Results/Main\n");
            Debug.Log("GEOMETRY_FRONTEND_SMOKE "+(passed?"PASS":"FAIL"));Application.Quit(passed?0:1);
        }
        bool Capture(string directory,string name,bool captureHud=false,bool sparseIntertitle=false,bool preserveTransition=false)
        {
            if(dormitory!=null)dormitory.RenderFrame(); // Hidden Players do not present the offscreen stage automatically.
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
            view.UseCaptureCamera(camera,preserveTransition);Canvas.ForceUpdateCanvases();
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
            // A dark theme may legitimately have a nearly black center. Test image contrast
            // across the artboard instead of depending on one bright background pixel.
            float min=1,max=0;int bright=0;
            for(int y=25;y<900;y+=25)for(int x=25;x<1600;x+=25)
            {float value=texture.GetPixel(x,y).grayscale;min=Mathf.Min(min,value);max=Mathf.Max(max,value);if(value>.25f)bright++;}
            bool valid=max-min>.25f && bright>12 && (!captureHud || hud!=null);
            if(sparseIntertitle)
            {
                // A black intertitle intentionally has no bright UI panels. Inspect its text area densely.
                int textPixels=0;
                for(int y=400;y<500;y+=2)for(int x=400;x<1200;x+=2)
                    if(texture.GetPixel(x,y).grayscale>.25f)textPixels++;
                valid=textPixels>40;
            }
            File.WriteAllBytes(Path.Combine(directory,name+".png"),texture.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=previous;view.UseCaptureCamera(null);
            if(hud!=null){hud.renderMode=oldMode;hud.worldCamera=oldCamera;hud.planeDistance=oldPlane;}
            RenderTexture.ReleaseTemporary(target);Destroy(texture);return valid;
        }
    }
}
