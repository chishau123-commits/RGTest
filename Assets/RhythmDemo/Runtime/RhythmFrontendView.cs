using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace GeometryRhythm
{
    /// <summary>Cyberpunk frontend presentation. A safe-area fitted 1600x900 canvas;
    /// it never changes render resolution, note geometry, clock or judgement coordinates.</summary>
    public sealed partial class RhythmFrontendView
    {
        readonly RhythmFrontend owner;
        readonly string gameTitle;
        readonly Canvas canvas;
        readonly RectTransform artboard;
        static readonly Color DesktopWindowSurface=new Color(.965f,.95f,.995f,.985f);
        static readonly Color DesktopWindowAlt=new Color(.93f,.91f,.99f,.98f);
        static readonly Color DesktopWindowHeader=new Color(.59f,.53f,.87f,.98f);
        static readonly Color DesktopWindowText=new Color(.20f,.12f,.42f,.98f);
        static readonly Color DesktopWindowMuted=new Color(.47f,.39f,.68f,.94f);
        readonly Dictionary<string,Button> buttons=new Dictionary<string,Button>();
        readonly Dictionary<string,float> buttonClickBlockedUntil=new Dictionary<string,float>(StringComparer.Ordinal);
        readonly List<string> titleVideos=new List<string>();
        readonly Dictionary<string,Texture2D> newsImages=new Dictionary<string,Texture2D>();
        readonly Dictionary<int,Image> newsUnreadMarkers=new Dictionary<int,Image>();
        readonly List<NotificationItem> notifications=new List<NotificationItem>();
        RectTransform notificationLayer;
        int notificationSequence;
        RectTransform page,scan;
        CanvasGroup pageFade;
        Image transitionShade;
        float pageStarted,pageFadeDuration=.18f,lastAnimation;
        bool pageAlphaLocked;
        Rect lastSafe;
        Vector2 lastCanvas;
        readonly Texture2D[] homeBackgrounds=new Texture2D[3];
        RawImage homeBackgroundImage;
        RawImage homeBackgroundImageNext;
        float homeBackgroundAspect=16f/9f;
        float homeBackgroundFadeStarted;
        bool homeBackgroundFading;
        Text homeLevelValue,homeBackgroundCounter;
        GameObject homeFigureBackdrop;
        CyberIdolGraphic homeHeroPlaceholder;
        CyberHomeBackdropGraphic homeBackdrop;
        CyberMainDesktopGraphic homeDesktop;
        CyberTitleGraphic titleBackdrop;
        VideoPlayer titleVideoPlayer;
        RenderTexture titleVideoTexture;
        RawImage titleVideoImage;
        RawImage titleAvatarImage;
        CanvasGroup titleCtaGroup;
        float titleCtaStarted;
        InputField titlePasswordInput;
        Text titleLoginMessage;
        RectTransform titleLoginFieldRect;
        Vector2 titleLoginFieldBasePosition;
        float titleLoginErrorUntil=-1f;
        bool titlePasswordFocusPending;
        RectTransform titleLoginScan;
        Image titleLoginScanImage;
        float titleLoginScanHeight=72f;
        float titleVideoPreparedAt=-1;
        int titleVideoIndex=-1;
        bool titleVideoDucked;
        RectTransform bootProgressFill;
        Image bootProgressFillImage;
        RectTransform bootProgressCursor;
        Image bootProgressCursorImage;
        Image[] bootProgressSegments;
        Text bootStatusText;
        float bootStarted;
        PopupMotion returnPopup,controlsPopup,backgroundPopup,startMenuPopup;
        RectTransform newsWindowRoot;
        RectTransform musicWindowRoot;
        CanvasGroup musicWindowGroup;
        Vector2 musicWindowPosition=new Vector2(420,-120);
        float musicWindowStarted;
        bool musicWindowMinimized,musicWindowMinimizing,musicWindowRestoring,musicWindowClosing,musicWindowFromTaskbar;
        Text musicNowTitle,musicNowNumber,musicElapsed,musicDuration;
        Image musicProgressFill;
        Slider musicVolumeSlider;
        readonly List<CyberPanelGraphic> musicTrackPanels=new List<CyberPanelGraphic>();
        readonly List<Text> musicTrackStates=new List<Text>();
        CanvasGroup newsWindowContent;
        GameObject newsWindowBlocker;
        GameObject newsFeedLayer;
        RectTransform newsFeedContent;
        Button newsCloseButton;
        float newsWindowStarted;
        bool newsWindowClosing;
        RectTransform newsDetailRoot;
        CanvasGroup newsDetailContent;
        GameObject newsDetailBlocker;
        float newsDetailStarted;
        bool newsDetailClosing;
        Vector2 newsDetailIconPosition,newsDetailIconSize;
        RectTransform friendWindowRoot;
        CanvasGroup friendWindowContent;
        GameObject friendWindowBlocker,friendListLayer,friendChatLayer,friendRequestPanel;
        Button friendCloseButton;
        Image newsUnreadDot,friendUnreadDot,homeDiaryUnreadDot;
        Image tnelisUnreadMarker,friendRequestUnreadDot;
        Image bakaRequestMarker,bakaUnreadMarker;
        Text friendTypingText;
        Text friendComposerText,friendComposerHint;
        CyberPanelGraphic friendComposerPanel;
        Button friendSendButton;
        readonly List<string> friendTypingFrames=new List<string>();
        string friendPresetLine="",friendRevealedLine="";
        int friendRevealIndex;
        bool friendAwaitingPlayer;
        GameObject diaryPromptRoot;
        CanvasGroup diaryPromptGroup;
        CyberPanelGraphic monologuePanel;
        readonly List<MonologuePage> monologuePages=new List<MonologuePage>();
        Action monologueCompleted;
        int monologuePageIndex;
        float monologueAdvanceAfter;
        Text monologueText,monologueHint,monologueArrow;
        string monologueFullText="";
        int monologueVisibleCount;
        float monologueNextCharAt;
        float diaryPromptStarted;
        float diaryPromptDismissedAt=-1f;
        RectTransform deviceWindowRoot,diaryWindowRoot;
        GameObject deviceWindowBlocker,diaryWindowBlocker;
        CanvasGroup deviceWindowContent,diaryWindowContent;
        Image deviceDiaryUnreadDot,deviceFriendUnreadDot;
        Text diaryDetailTitle,diaryDetailBody;
        Text diaryEditorText,diaryEditorHint;
        Button diarySaveButton;
        readonly List<string> diaryTypingFrames=new List<string>();
        string diaryFullText="",diaryVisibleText="";
        int diaryFrameIndex;
        int diarySelectedEntry;
        float deviceWindowStarted,diaryWindowStarted;
        bool deviceWindowClosing,diaryWriting;
        bool diaryWriteUnlocked,diaryUnread,bakaMonologueQueued,bakaMonologueTriggered;
        float friendWindowStarted;
        bool friendWindowClosing;
        bool diaryWindowClosing;
        bool friendWindowMinimized,diaryWindowMinimized,newsWindowMinimized;
        bool friendWindowMinimizing,diaryWindowMinimizing,newsWindowMinimizing;
        bool friendWindowRestoring,diaryWindowRestoring,newsWindowRestoring;
        bool friendWindowFromTaskbar,diaryWindowFromTaskbar,newsWindowFromTaskbar;
        bool mainWindowMinimized,mainWindowMinimizing,mainWindowRestoring;
        RectTransform mainWindowRoot;
        WindowFocusHandler mainWindowFocus,deviceWindowFocus,diaryWindowFocus,friendWindowFocus,newsWindowFocus;
        Vector2 mainWindowPosition=new Vector2(172,-58);
        Vector2 diaryWindowPosition=new Vector2(220,-68),friendWindowPosition=new Vector2(260,-80);
        Vector2 newsWindowPosition=new Vector2(300,-92),deviceWindowPosition=new Vector2(210,-90);
        CanvasGroup mainWindowGroup;
        float mainWindowMotionStarted;
        CanvasGroup friendWindowGroup,diaryWindowGroup,newsWindowGroup;
        readonly List<TaskbarTask> taskbarTasks=new List<TaskbarTask>();
        float friendTypingUntil,friendNextMessageAt;
        bool friendTyping,tnelisChatOpen;
        int tnelisUnreadCount=1;
        int bakaUnreadCount;
        readonly List<FriendChatMessage> tnelisMessages=new List<FriendChatMessage>();
        readonly Queue<FriendChatMessage> tnelisPending=new Queue<FriendChatMessage>();
        readonly FriendChatMessage[] tnelisChat001=
        {
            new FriendChatMessage{Text="Turi，之前要你查的资料查好了吗？"},
            new FriendChatMessage{Text="我知道天天催你也不太好，但是相信我，那个真的很重要。"},
            new FriendChatMessage{FromPlayer=true,Text="放心放心，肯定没问题哒，老板。"},
            new FriendChatMessage{Text="？"},
            new FriendChatMessage{Text="你这是事情办好了还是没办好？"},
            new FriendChatMessage{FromPlayer=true,Text="额...."},
            new FriendChatMessage{FromPlayer=true,Text="准确来说，快办好了，正在进行中"},
            new FriendChatMessage{Text="我真的懒得吐槽你了，Turi。"},
            new FriendChatMessage{Text="DDL就在明天中午十二点，我希望你不要拖。"},
            new FriendChatMessage{Text="不过你每次都能完美卡线完成工作，挺佩服你的。"},
            new FriendChatMessage{FromPlayer=true,Text="那你把DDL提早一点不就行了？"},
            new FriendChatMessage{Text="好主意，下次我就这么干。"},
            new FriendChatMessage{Text="好好干活，我先下了。"}
        };
        int tnelisChatPosition;
        readonly List<FriendChatMessage> bakaMessages=new List<FriendChatMessage>();
        readonly Queue<FriendChatMessage> bakaPending=new Queue<FriendChatMessage>();
        readonly FriendChatMessage[] bakaChat002=
        {
            new FriendChatMessage{Text="您好您好！"},
            new FriendChatMessage{Text="您就是那位说要帮咱的大佬吗！"},
            new FriendChatMessage{FromPlayer=true,Text="没错，说说你家的情况吧。"},
            new FriendChatMessage{Text="咱前几天在P公司的网购平台上买了台调律器，然后过了几天，它就不运行了。"},
            new FriendChatMessage{Text="咱联系P公司的人保修，结果他们说咱的调律器是被人恶意损坏的！"},
            new FriendChatMessage{Text="说是这种情况不包含在保修条例内..."},
            new FriendChatMessage{FromPlayer=true,Text="恶意损坏？你招惹黑客了？"},
            new FriendChatMessage{Text="没有没有！咱在互联网上安分守己，怎么会招惹到这种可怕的存在！"},
            new FriendChatMessage{FromPlayer=true,Text="可怕的存在？小子，告诉你个秘密。"},
            new FriendChatMessage{Text="？"},
            new FriendChatMessage{FromPlayer=true,Text="老娘我。"},
            new FriendChatMessage{FromPlayer=true,Text="也是黑客。"},
            new FriendChatMessage{Text="！！！"},
            new FriendChatMessage{Text="大佬"},
            new FriendChatMessage{Text="不是"},
            new FriendChatMessage{Text="老大"},
            new FriendChatMessage{Text="咱错了。老大。虽然不知道怎么惹到你了。"},
            new FriendChatMessage{Text="但是，把咱的调律器弄坏这种行为。"},
            new FriendChatMessage{Text="这是不对的！"},
            new FriendChatMessage{FromPlayer=true,Text="呵呵。逗你玩的。"},
            new FriendChatMessage{FromPlayer=true,Text="我是个黑客，但我确实是个好黑客哦。"},
            new FriendChatMessage{FromPlayer=true,Text="你家的调律器不是我弄坏的，但是我可以帮你修复。"},
            new FriendChatMessage{Text="真的吗老大！"},
            new FriendChatMessage{FromPlayer=true,Text="要不然你以为我在你博客下面评论干嘛？"},
            new FriendChatMessage{Text="感谢老大！！！咱这就把咱家调律器的序列号发到您电脑上！"},
            new FriendChatMessage{FromPlayer=true,Text="我收费不低的哦。"},
            new FriendChatMessage{Text="老大咱是学生党..."},
            new FriendChatMessage{FromPlayer=true,Text="算了，干完活再谈钱吧。"}
        };
        int bakaChatPosition;
        int activeFriendIndex;
        bool bakaRequestPending,bakaFriendAccepted,bakaNewsCreated,bakaRequestDeclined;
        int bakaNewsIndex=-1;
        bool tnelisConversationComplete,bakaConversationComplete,bakaNewsQueued,bakaRequestQueued;
        Vector2 newsDetailCloseFromPosition,newsDetailCloseFromSize;
        int selectedNewsPost=-1;
        bool newsUnread=true,tnelisNewsCreated;
        readonly List<bool> newsLiked=new List<bool>{false,false};
        readonly List<bool> newsUnreadItems=new List<bool>{true,true};
        readonly List<bool> newsCommentPosted=new List<bool>{false,false};
        readonly List<List<string>> newsCommentMessages=new List<List<string>>{new List<string>(),new List<string>()};
        const float NewsIconX=22,NewsIconY=326,NewsIconWidth=130,NewsIconHeight=118,NewsWindowDuration=.64f;
        const float AppWindowX=210,AppWindowY=90,AppWindowWidth=1180,AppWindowHeight=720;
        const float NewsDetailDuration=.52f;
        const float FriendsWindowDuration=.62f;
        const string TitlePasswordHint="密码提示：四个鸡蛋";
        int homeBackgroundIndex;
        int storyLevelIndex;
        CanvasGroup storyPortraitGroup,storyDialogueGroup;
        RectTransform storyPortraitRect,storyDialogueRect;
        Vector2 storyPortraitFrom,storyPortraitTo,storyDialogueFrom,storyDialogueTo;
        float storyTransitionStarted=-1;
        Text storyLineText;
        string storyFullLine="";
        int storyTypingIndex;
        float storyNextCharAt,storyLastClickAt=-1,storySkipClickUntil=-1;
        bool storyTyping;
        Image[] modeLoadingBlocks;
        Text modeLoadingPercent;
        Text modeLoadingStatus;
        RectTransform modePixelOverlay;
        Image[] modePixelBlocks;
        Color[] modePixelColors;
        Color[] modePixelTargetColors;
        float[] modePixelDelays;
        Vector2[] modePixelBasePositions;
        Vector2[] modePixelTargetPositions;
        RectTransform modeGateRoot;
        CyberPanelGraphic[] modeGateBands;
        Image modeGateShade;
        RectTransform signalTransitionRoot;
        Image signalShade,signalCore;
        CyberPanelGraphic[] signalRings;
        Text signalModeLabel;
        public int HomeBackgroundIndex => homeBackgroundIndex;
        public bool HomeBackgroundLoaded => homeBackgroundImage!=null && homeBackgroundImage.texture!=null;
        public int ButtonCount => buttons.Count;
        public bool TitleUsesIndependentArtwork => titleBackdrop!=null && homeBackgroundImage==null
            && homeBackdrop==null && homeFigureBackdrop==null && homeHeroPlaceholder==null;
        public bool TitleVideoAvailable => titleVideoPlayer!=null && !string.IsNullOrEmpty(titleVideoPlayer.url);
        public bool TitleVideoPlaying => titleVideoPlayer!=null && titleVideoPlayer.isPlaying;
        public bool TitleAvatarLoaded => titleAvatarImage!=null&&titleAvatarImage.texture!=null;
        public bool BootProgressVisible => bootProgressFill!=null;
        public bool TitleLoginErrorVisible => titleLoginErrorUntil>Time.unscaledTime;
        public bool HasTitlePasswordField => titlePasswordInput!=null;
        public float NewsTransitionDuration => NewsWindowDuration;
        public float NewsDetailTransitionDuration => NewsDetailDuration;
        public float FriendTransitionDuration => FriendsWindowDuration;
        public float DiaryTransitionDuration => FriendsWindowDuration;
        public bool NewsPostLiked(int index)=>index>=0&&index<newsLiked.Count&&newsLiked[index];
        public int NewsPostCount=>newsPosts.Count;
        public bool NewsUnread=>newsUnread;
        public bool FriendConversationComplete=>tnelisConversationComplete&&!friendTyping&&tnelisPending.Count==0;
        public bool FriendComposerReady=>friendAwaitingPlayer&&friendPresetLine.Length>0;
        public bool FriendMessageReadyToSend=>FriendComposerReady&&friendRevealIndex>=friendTypingFrames.Count;
        public string FriendRevealedLine=>friendRevealedLine;
        public bool DiaryPromptActive=>diaryPromptRoot!=null||Time.unscaledTime-diaryPromptDismissedAt<.12f;
        public bool ActiveWindowMinimized =>
            (owner.Page==FrontendPage.Diary&&diaryWindowMinimized)||
            (owner.Page==FrontendPage.Friends&&friendWindowMinimized)||
            (owner.Page==FrontendPage.News&&newsWindowMinimized);
        public bool MainWindowMinimized=>mainWindowMinimized;
        public bool DiaryWindowMinimized=>diaryWindowMinimized;
        public bool FriendsWindowMinimized=>friendWindowMinimized;
        public bool NewsWindowMinimized=>newsWindowMinimized;
        public bool MusicWindowMinimized=>musicWindowMinimized;
        public int TaskbarTaskCount=>taskbarTasks.Count;

        sealed class NewsPost
        {
            public string Name,Handle,Time,Title,Body,Tag,ImageCaption,ImageResource,CommentDraft;
            public string[] Comments;
            public bool HasImage;
            public int Likes;
        }

        sealed class PopupMotion
        {
            public RectTransform Root;
            public CanvasGroup Content,Scrim;
            public Vector2 IconPosition,IconSize,FinalPosition,FinalSize;
            public float Started,ScrimAlpha;
            public bool Closing,Settled,SlideFromTaskbar;
            public Action OnFinished;
        }

        sealed class FriendChatMessage
        {
            public bool FromPlayer;
            public string Text;
        }

        sealed class NotificationItem
        {
            public GameObject Root;
            public RectTransform Rect;
            public CanvasGroup Group;
            public string Kind;
            public int Target;
            public float ExpiresAt;
            public float SpawnedAt;
            public float CloseStarted;
            public Vector2 TargetPosition,ReflowVelocity;
            public Vector2? ExitPosition;
            public bool Closing;
            public Action OnClick;
        }

        sealed class TaskbarTask
        {
            public FrontendPage Page;
            public Button Button;
            public Text Label;
            public Image Rail;
            public bool Minimized;
        }

        readonly List<NewsPost> newsPosts=new List<NewsPost>
        {
            new NewsPost{
                Name="NYX_07",Handle="@saltline",Time="02:14 / 第09区",
                Title="雨记得每一段信号",
                Body="雨水正把一段新的信号带过下城区。发射器熄灭前，我录下了三个音符。有人知道它的频率吗？",
                Tag="#信号 #雨夜 #下城区",ImageCaption="雨夜 / 信号记录",HasImage=true,ImageResource="news-01",
                CommentDraft="我在下城区也听见了同样的147 BPM脉冲。它正在朝第09区移动。",
                Likes=1840,
                Comments=new[]{"线狐：我也听见了。它以147 BPM循环。","无声合唱：发坐标，我可以追踪。"}
            },
            new NewsPost{
                Name="K-01",Handle="@night_shift",Time="01:48 / 夜班巴士",
                Title="03:00 / 夜班巴士 4号线",
                Body="第4区的旧中继器醒了。如果你坐上03:00那班车，穿过隧道后仔细听。这座城市还在说话。",
                Tag="#夜班巴士 #第4区",ImageCaption="夜班线路 / 第04区",HasImage=true,ImageResource="news-02",
                CommentDraft="隧道回声与路线时间完全吻合。我在旧车站附近记下了第二次脉冲。",
                Likes=932,
                Comments=new[]{"第七号：隧道会挡住我的接收器，但我看见那道脉冲了。","米拉.EXE：在宵禁扫描前把它保存下来。"}
            }
        };

        RectTransform outgoingPage;
        float pageSlideStarted;
        bool pageSlideActive;
        bool pageSlideReverse;

        public RhythmFrontendView(Transform parent,RhythmFrontend owner,string title)
        {
            this.owner=owner;gameTitle=title;
            var obj=new GameObject("Application UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            obj.transform.SetParent(parent,false);canvas=obj.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=obj.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            var wash=new GameObject("Midnight menu background",typeof(RectTransform),typeof(Image));wash.transform.SetParent(obj.transform,false);
            var r=wash.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            wash.GetComponent<Image>().color=CyberTheme.Background;
            // The opaque menu background catches input: a menu click never reaches gameplay.
            artboard=new GameObject("Safe area / 1600 x 900",typeof(RectTransform)).GetComponent<RectTransform>();
            artboard.SetParent(obj.transform,false);artboard.anchorMin=artboard.anchorMax=new Vector2(.5f,.5f);artboard.sizeDelta=new Vector2(1600,900);
            artboard.gameObject.AddComponent<RectMask2D>();
            notificationLayer=Rect("Notification layer",artboard,0,0,1600,900);
        }
        public void UpdateLayout()
        {
            if(!canvas.gameObject.activeSelf) return;
            UpdateBoot();
            UpdateDiaryPrompt();
            UpdateDeviceWindow();
            UpdateDiaryWindow();
            UpdateDiaryInput();
            UpdateMainWindowMotion();
            UpdateDesktopPresentation();
            // Tiny, non-flashing presentation animation. No per-frame artwork mesh rebuild.
            if(pageFade!=null && transitionShade!=null && !pageAlphaLocked)
            {
                float t=Mathf.Clamp01((Time.unscaledTime-pageStarted)/pageFadeDuration);
                float visibility=mainUiPixelTheme?DesktopMotion.Smooth(t):Mathf.SmoothStep(0,1,t);
                SetTransitionShade(1-visibility);
                pageFade.interactable=visibility>.85f;
                pageFade.blocksRaycasts=pageFade.interactable;
            }
            if(titleCtaGroup!=null)
            {
                float ctaT=Mathf.Clamp01((Time.unscaledTime-titleCtaStarted-.9f)/.75f);
                titleCtaGroup.alpha=DesktopMotion.Enter(ctaT);
                PositionLoginCard(ctaT);
                titleCtaGroup.interactable=titleCtaGroup.alpha>.85f;
                titleCtaGroup.blocksRaycasts=titleCtaGroup.interactable;
            }
            if(titleLoginScan!=null)
            {
                float phase=Mathf.Repeat((Time.unscaledTime-titleCtaStarted)/5.6f,1);
                float y=phase*titleLoginScanHeight;
                titleLoginScan.anchoredPosition=new Vector2(0,-y);
                if(titleLoginScanImage!=null)
                {
                    Color color=titleLoginScanImage.color;
                    color.a=.13f*Mathf.Pow(Mathf.Sin(phase*Mathf.PI),2);
                    titleLoginScanImage.color=color;
                }
            }
            if(titleLoginErrorUntil>0f)
            {
                if(Time.unscaledTime<titleLoginErrorUntil)
                {
                    float phase=Mathf.Clamp01((Time.unscaledTime-(titleLoginErrorUntil-1.05f))/.44f);
                    float shake=Mathf.Sin(phase*Mathf.PI*6f)*6f*Mathf.Pow(1-phase,2);
                    if(titleLoginFieldRect!=null)
                        titleLoginFieldRect.anchoredPosition=titleLoginFieldBasePosition+new Vector2(shake,0);
                }
                else
                {
                    titleLoginErrorUntil=-1f;
                    if(titleLoginFieldRect!=null)titleLoginFieldRect.anchoredPosition=titleLoginFieldBasePosition;
                    if(titleLoginMessage!=null)
                    {
                        titleLoginMessage.text=TitlePasswordHint;
                        titleLoginMessage.color=DesktopWindowMuted;
                    }
                }
            }
            if(titlePasswordFocusPending&&titleCtaGroup!=null&&titleCtaGroup.alpha>.85f)
            {
                titlePasswordFocusPending=false;
                if(titlePasswordInput!=null)titlePasswordInput.ActivateInputField();
            }
            UpdateTitleVideoVolume();
            UpdateTitleVideoReveal();
            UpdateNewsWindow();
            UpdateMusicPlayerWindow();
            UpdateNewsDetail();
            UpdateFriendWindow();
            UpdatePageSlide();
            UpdatePopup(returnPopup,.48f);
            UpdatePopup(controlsPopup,.48f);
            UpdatePopup(backgroundPopup,.48f);
            UpdatePopup(startMenuPopup,.48f);
            UpdateNotifications();
            UpdateHomeBackgroundFade();
            UpdateStoryTransition();
            UpdateStoryTyping();
            UpdateStoryReadingState();
            if(scan!=null && Time.unscaledTime-lastAnimation>.05f)
            {
                lastAnimation=Time.unscaledTime;
                scan.anchoredPosition=new Vector2(scan.anchoredPosition.x,-(scanStart+Mathf.Repeat(Time.unscaledTime*.08f,1)*scanHeight));
            }
            var size=((RectTransform)canvas.transform).rect.size;var safe=Screen.safeArea;
            if(lastSafe==safe && lastCanvas==size) return;
            lastSafe=safe;lastCanvas=size;
            float sx=size.x/Math.Max(1,Screen.width),sy=size.y/Math.Max(1,Screen.height);
            artboard.anchoredPosition=new Vector2((safe.center.x-Screen.width*.5f)*sx,(safe.center.y-Screen.height*.5f)*sy);
            artboard.localScale=Vector3.one*Mathf.Min(safe.width*sx/1600,safe.height*sy/900);
        }

        void UpdateStoryTransition()
        {
            if(storyTransitionStarted<0)return;
            float t=DesktopMotion.Enter(Mathf.Clamp01((Time.unscaledTime-storyTransitionStarted)/.52f));
            if(storyPortraitGroup!=null)
            {
                storyPortraitGroup.alpha=t;
                if(storyPortraitRect!=null)
                    storyPortraitRect.anchoredPosition=Vector2.Lerp(storyPortraitFrom,storyPortraitTo,t);
            }
            if(storyDialogueGroup!=null)
            {
                storyDialogueGroup.alpha=t;
                if(storyDialogueRect!=null)
                    storyDialogueRect.anchoredPosition=Vector2.Lerp(storyDialogueFrom,storyDialogueTo,t);
            }
            if(t>=1f)storyTransitionStarted=-1;
        }

        void UpdateStoryTyping()
        {
            if(storyLineText==null)return;
            if(Input.GetMouseButtonDown(0))
            {
                if(storyTyping&&Time.unscaledTime-storyLastClickAt<=.28f)
                {
                    CompleteStoryTyping();
                    storySkipClickUntil=Time.unscaledTime+.2f;
                }
                storyLastClickAt=Time.unscaledTime;
            }
            if(!storyTyping)return;
            if(Time.unscaledTime<storyNextCharAt)return;
            storyTypingIndex=Mathf.Min(storyTypingIndex+1,storyFullLine.Length);
            storyLineText.text=storyFullLine.Substring(0,storyTypingIndex);
            storyNextCharAt=Time.unscaledTime+.028f;
            if(storyTypingIndex>=storyFullLine.Length)storyTyping=false;
        }

        void BeginStoryTyping(Text target,string value)
        {
            storyLineText=target;
            storyFullLine=value??"";
            storyTypingIndex=0;
            storyTyping=storyFullLine.Length>0;
            storyNextCharAt=Time.unscaledTime;
            storyLineText.text="";
        }

        void CompleteStoryTyping()
        {
            if(storyLineText==null)return;
            storyTypingIndex=storyFullLine.Length;
            storyLineText.text=storyFullLine;
            storyTyping=false;
        }

        void StoryContinue()
        {
            if(Time.unscaledTime<storySkipClickUntil)return;
            if(storyTyping)return;
            owner.AdvanceStory();
        }

        float scanStart,scanHeight;
        bool mainUiPixelTheme;
        Font UiChineseFont => mainUiPixelTheme?CyberTheme.Pixel:CyberTheme.Chinese;
        void Begin(string name,string section,bool chrome=true,float fadeDuration=.18f,bool preservePrevious=false,float startY=0)
        {
            musicPixelPage=name=="Mode select"||name=="Story levels"||name=="Song selection"||name=="Main to mode loading";
            mainUiPixelTheme=name=="Main interface"||name=="Title login"||name=="Story player"||name=="Story intro"||musicPixelPage;
            mainEntryActive=false;
            ReleaseTitleVideo();
            if(mainWindowRoot!=null){mainWindowRoot.gameObject.SetActive(false);UnityEngine.Object.Destroy(mainWindowRoot.gameObject);}
            ClearFriendWindow();
            ClearNewsWindow();
            ClearMusicPlayerWindow();
            ClearDeviceWindow();
            ClearDiaryWindow();
            if(diaryPromptRoot!=null)
            {
                diaryPromptRoot.SetActive(false);
                UnityEngine.Object.Destroy(diaryPromptRoot);
            }
            canvas.gameObject.SetActive(true);buttons.Clear();scan=null;
            if(transitionShade!=null){transitionShade.gameObject.SetActive(false);UnityEngine.Object.Destroy(transitionShade.gameObject);}
            if(outgoingPage!=null){outgoingPage.gameObject.SetActive(false);UnityEngine.Object.Destroy(outgoingPage.gameObject);outgoingPage=null;}
            if(page!=null)
            {
                if(preservePrevious)outgoingPage=page;
                else
                {
                    page.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(page.gameObject);
                }
            }
            page=Rect(name,artboard,0,startY,1600,900);pageFade=page.gameObject.AddComponent<CanvasGroup>();
            pageStarted=Time.unscaledTime;pageFadeDuration=Mathf.Max(.01f,fadeDuration);
            pageFade.alpha=1;pageFade.interactable=false;pageFade.blocksRaycasts=false;pageAlphaLocked=false;
            transitionShade=Box(artboard,"Transition shade",0,0,1600,900,musicPixelPage?new Color(.79f,.76f,.92f):Color.black);
            transitionShade.raycastTarget=true;
            homeBackdrop=null;homeBackgroundImage=null;homeBackgroundImageNext=null;
            homeDesktop=null;
            homeBackgroundFading=false;
            homeFigureBackdrop=null;homeHeroPlaceholder=null;
            homeLevelValue=null;homeBackgroundCounter=null;titleBackdrop=null;
            taskbarTasks.Clear();mainWindowRoot=null;mainWindowGroup=null;
            mainWindowFocus=null;deviceWindowFocus=null;diaryWindowFocus=null;
            friendWindowFocus=null;newsWindowFocus=null;
            mainWindowMinimized=false;mainWindowMinimizing=false;mainWindowRestoring=false;
            friendWindowMinimized=false;diaryWindowMinimized=false;newsWindowMinimized=false;
            musicWindowMinimized=false;
            titleVideoImage=null;titleAvatarImage=null;titleCtaGroup=null;
            titleLoginScan=null;titleLoginScanImage=null;
            titlePasswordInput=null;titleLoginMessage=null;titleLoginFieldRect=null;
            titleLoginFieldBasePosition=Vector2.zero;titleLoginErrorUntil=-1f;titlePasswordFocusPending=false;
            titleLoginScanHeight=72f;
            bootProgressFill=null;bootProgressFillImage=null;bootProgressCursor=null;bootProgressCursorImage=null;
            bootProgressSegments=null;bootStatusText=null;bootStarted=0f;
            storyPortraitGroup=null;storyDialogueGroup=null;storyPortraitRect=null;storyDialogueRect=null;
            storyTransitionStarted=-1;
            storyLineText=null;storyFullLine="";storyTypingIndex=0;storyTyping=false;
            storyContinueButton=null;storyReadingHint=null;
            storyNextCharAt=0;storyLastClickAt=-1;storySkipClickUntil=-1;
            newsUnreadDot=null;friendUnreadDot=null;homeDiaryUnreadDot=null;
            diaryPromptRoot=null;diaryPromptGroup=null;monologuePanel=null;monologuePages.Clear();
            monologueCompleted=null;
            monologueText=null;monologueHint=null;monologueArrow=null;monologueFullText="";
            monologuePageIndex=0;diaryDetailTitle=null;diaryDetailBody=null;
            diaryEditorText=null;diaryEditorHint=null;diarySaveButton=null;
            diaryTypingFrames.Clear();diaryFrameIndex=0;diaryVisibleText="";diaryWriting=false;
            ClearPopup(ref returnPopup);ClearPopup(ref controlsPopup);
            ClearPopup(ref backgroundPopup);ClearPopup(ref startMenuPopup);
            if(chrome)
            {
                var frame=Rect("Circuit frame",page,0,0,1600,900).gameObject.AddComponent<CyberFrameGraphic>();frame.raycastTarget=false;
                var logo=Panel(page,"Brand plate",48,35,95,60,CyberTheme.PanelLight,CyberTheme.Cyan);
                Label(logo.rectTransform,"Monogram","R/H",8,3,79,51,34,true,CyberTheme.Cyan,TextAnchor.MiddleCenter);
                var brand=Label(page,"Brand",gameTitle.Replace('\n',' ').ToUpperInvariant(),166,40,680,31,25,true);Fit(brand,14,25);
                Mono(page,"Subsystem","SPATIAL RHYTHM SYSTEM  /  LOCAL CLIENT",168,75,720,17,12,CyberTheme.Muted);
                Mono(page,"Page index",section,1020,45,500,29,15,CyberTheme.Text,TextAnchor.MiddleRight);
                Mono(page,"Connection","OFFLINE  /  STANDALONE",1100,77,420,17,11,CyberTheme.Cyan,TextAnchor.MiddleRight);
                Mono(page,"Footer","R/H   //   TRACE THE SIGNAL",48,861,610,22,12,CyberTheme.Muted);
                Mono(page,"Version","REAL-TIME 3D  /  PROTOTYPE 01",1030,851,490,22,12,CyberTheme.Muted,TextAnchor.MiddleRight);
            }
            UpdateLayout();
        }
        public void ShowBootBlack()
        {
            Begin("Boot black screen","00   /   POWER ON",false,.05f);
            Box(page,"Boot pre-roll black",0,0,1600,900,Color.black);
        }
        public void ShowBoot()
        {
            Begin("Boot sequence","00   /   POWER ON",false,.35f);
            Box(page,"Boot black field",0,0,1600,900,Color.black);
            SignalLogo(page,668,218,2.3f);
            Label(page,"Boot product family","GEOMETRY RHYTHM",560,488,480,28,17,false,
                new Color(.82f,.86f,.92f,.92f),TextAnchor.MiddleCenter);
            var bootTitle=Label(page,"Boot product title","SIGNAL EDITION",548,514,504,58,42,true,
                Color.white,TextAnchor.MiddleCenter);
            bootTitle.font=CyberTheme.FutureTitle;Fit(bootTitle,25,42);
            Mono(page,"Boot product detail","SPATIAL RHYTHM SYSTEM  /  LOCAL WORKSTATION",500,570,600,22,13,
                new Color(.68f,.78f,.88f,.70f),TextAnchor.MiddleCenter);
            var track=Panel(page,"Boot progress track",450,650,700,32,new Color(.01f,.025f,.055f,.98f),
                new Color(.18f,.46f,.88f,.95f));
            track.rounded=true;track.cornerRadius=16;
            Box(track.rectTransform,"Boot progress inset",12,11,676,10,new Color(.025f,.07f,.15f,.96f));
            bootProgressFillImage=Box(track.rectTransform,"Boot progress fill",12,11,0,10,
                new Color(.10f,.38f,.94f,.96f));
            bootProgressFill=bootProgressFillImage.rectTransform;
            const int segmentCount=18;
            bootProgressSegments=new Image[segmentCount];
            for(int i=0;i<segmentCount;i++)
            {
                bootProgressSegments[i]=Box(track.rectTransform,"Boot progress segment "+i,
                    22+i*36,10,26,12,new Color(.045f,.10f,.20f,.98f));
            }
            bootProgressCursorImage=Box(track.rectTransform,"Boot progress cursor",12,8,12,16,
                new Color(.92f,.97f,1f,.96f));
            bootProgressCursor=bootProgressCursorImage.rectTransform;
            bootStatusText=Mono(page,"Boot status","STARTING SIGNAL SERVICES",500,700,600,22,13,
                new Color(.62f,.72f,.84f,.64f),TextAnchor.MiddleCenter);
            Mono(page,"Boot footer","R/H SIGNAL SYSTEMS  /  BUILD 01.09",500,845,600,20,11,
                new Color(.42f,.52f,.65f,.52f),TextAnchor.MiddleCenter);
            bootStarted=Time.unscaledTime;
        }

        void UpdateBoot()
        {
            if(bootProgressFill==null)return;
            float progress=Mathf.Clamp01((Time.unscaledTime-bootStarted)/4.35f);
            float eased=1f-Mathf.Pow(1f-progress,3f);
            bootProgressFill.sizeDelta=new Vector2(676f*eased,10f);
            bootProgressFillImage.color=Color.Lerp(CyberTheme.Cyan,CyberTheme.Purple,eased);
            if(bootProgressSegments!=null)
            {
                int active=Mathf.CeilToInt(eased*bootProgressSegments.Length);
                for(int i=0;i<bootProgressSegments.Length;i++)
                {
                    if(bootProgressSegments[i]==null)continue;
                    bool lit=i<active;
                    Color color=lit
                        ?Color.Lerp(CyberTheme.Cyan,CyberTheme.Pink,i/(float)Mathf.Max(1,bootProgressSegments.Length-1))
                        :new Color(.045f,.10f,.20f,.98f);
                    if(i==active-1)
                        color.a=.72f+.28f*(.5f+.5f*Mathf.Sin(Time.unscaledTime*12f));
                    else color.a=.94f;
                    bootProgressSegments[i].color=color;
                }
            }
            if(bootProgressCursor!=null)
            {
                bootProgressCursor.anchoredPosition=new Vector2(12f+676f*eased-6f,-8f);
                if(bootProgressCursorImage!=null)
                {
                    Color cursor=bootProgressCursorImage.color;
                    cursor.a=.72f+.28f*(.5f+.5f*Mathf.Sin(Time.unscaledTime*9f));
                    bootProgressCursorImage.color=cursor;
                }
            }
            if(bootStatusText!=null)
            {
                int dots=Mathf.FloorToInt(Time.unscaledTime*2.2f)%4;
                bootStatusText.text="STARTING SIGNAL SERVICES"+new string('.',dots);
                Color color=bootStatusText.color;
                color.a=.46f+.18f*(.5f+.5f*Mathf.Sin(Time.unscaledTime*2.5f));
                bootStatusText.color=color;
            }
        }

        void SignalLogo(RectTransform parent,float x,float y,float scale,bool themeOnly=false)
        {
            float width=60f*scale,height=54f*scale,gap=3f*scale;
            Color topLeft=themeOnly?CyberTheme.Purple:new Color(.96f,.38f,.12f);
            Color topRight=themeOnly?CyberTheme.Pink:new Color(.65f,.82f,.16f);
            Color bottomLeft=themeOnly?new Color(.35f,.46f,.78f):new Color(.16f,.62f,.90f);
            Color bottomRight=themeOnly?CyberTheme.Cyan:new Color(.54f,.30f,.92f);
            var orange=Panel(parent,"Signal logo top left",x,y,width,height,topLeft,Color.white);
            var lime=Panel(parent,"Signal logo top right",x+width+gap,y,width,height,topRight,Color.white);
            var blue=Panel(parent,"Signal logo bottom left",x,y+height+gap,width,height,bottomLeft,Color.white);
            var violet=Panel(parent,"Signal logo bottom right",x+width+gap,y+height+gap,width,height,bottomRight,Color.white);
            orange.cut=lime.cut=blue.cut=violet.cut=7f*scale;
            orange.outline=lime.outline=blue.outline=violet.outline=false;
        }

        public void ShowTitle()
        {
            Begin("Title login","00   /   SIGN IN",false,1.15f);
            titleBackdrop=Rect("Login pixel sky",page,0,0,1600,900).gameObject.AddComponent<CyberTitleGraphic>();
            titleBackdrop.raycastTarget=false;
            Box(page,"Login desktop wash",0,0,1600,900,new Color(.96f,.94f,1f,.10f));
            Mono(page,"Login masthead","LOCAL  /  09",24,25,132,18,11,new Color(.29f,.19f,.59f));
            Box(page,"Login masthead line",22,48,126,2,new Color(.38f,.27f,.76f,.58f));

            // Keep the product/account composition, using the desktop's pixel furniture.
            SignalLogo(page,340,282,1.25f,true);
            Label(page,"Login product","GEOMETRY RHYTHM",196,456,440,40,28,true,
                DesktopWindowText,TextAnchor.MiddleCenter);
            Mono(page,"Login edition","SIGNAL EDITION",206,502,420,24,14,
                DesktopWindowMuted,TextAnchor.MiddleCenter);
            Box(page,"Login identity rule",386,546,60,3,CyberTheme.Purple);
            Label(page,"Login prompt","请输入密码以开始",206,571,420,32,20,false,
                DesktopWindowText,TextAnchor.MiddleCenter);
            Mono(page,"Login prompt english","ENTER YOUR PASSWORD TO BEGIN",206,615,420,22,12,
                DesktopWindowMuted,TextAnchor.MiddleCenter);

            var accountRoot=Rect("Account window",page,770,222,640,474);
            var shadow=Panel(accountRoot,"Account window shadow",8,8,640,474,
                new Color(.29f,.19f,.58f,.16f),CyberTheme.Purple);
            shadow.rounded=true;shadow.cornerRadius=12;shadow.outline=false;
            var card=Panel(accountRoot,"Account window frame",0,0,640,474,
                new Color(.965f,.95f,.995f,.98f),new Color(.43f,.29f,.85f));
            card.rounded=true;card.cornerRadius=12;
            var titleBar=Panel(card.rectTransform,"Login window title bar",0,0,640,52,
                new Color(.59f,.53f,.87f,.98f),new Color(.92f,.69f,.98f));
            titleBar.rounded=true;titleBar.cornerRadius=12;
            Box(titleBar.rectTransform,"Login title lower edge",0,45,640,7,new Color(.48f,.40f,.78f,.90f));
            var mark=Panel(titleBar.rectTransform,"Login window mark",14,10,32,32,
                new Color(.36f,.27f,.72f,.96f),CyberTheme.Cyan);
            mark.rounded=true;mark.cornerRadius=7;
            var icon=Rect("Login music icon",mark.rectTransform,6,6,20,20).gameObject.AddComponent<CyberDesktopIconGraphic>();
            icon.kind=DesktopIconKind.Music;icon.iconColor=new Color(.95f,.93f,1f);icon.raycastTarget=false;
            Label(titleBar.rectTransform,"Login window title","RHYTHM HACKER  /  用户登录",58,10,474,30,18,true,
                new Color(.98f,.97f,1f),TextAnchor.MiddleLeft);
            Box(titleBar.rectTransform,"Login ready light",552,22,8,8,CyberTheme.Cyan);
            Mono(titleBar.rectTransform,"Login ready","READY",570,14,58,24,11,
                new Color(.95f,.92f,1f),TextAnchor.MiddleLeft);

            var avatarFrame=Panel(card.rectTransform,"Avatar frame",28,80,92,92,
                new Color(.84f,.80f,.96f),CyberTheme.Purple);
            avatarFrame.rounded=true;avatarFrame.cornerRadius=6;
            var previewRoot=Rect("Operator avatar",avatarFrame.rectTransform,4,4,84,84);
            previewRoot.gameObject.AddComponent<RectMask2D>();
            Box(previewRoot,"Avatar fallback",0,0,84,84,new Color(.35f,.31f,.62f,.98f));
            Label(previewRoot,"Avatar monogram","R/H",4,22,76,38,24,true,Color.white,TextAnchor.MiddleCenter);
            var avatarImage=Rect("Operator avatar image",previewRoot,0,0,84,84).gameObject.AddComponent<RawImage>();
            var avatarTexture=Resources.Load<Texture2D>("UI/LoginAvatar");
            avatarImage.texture=avatarTexture;
            avatarImage.enabled=avatarTexture!=null;
            avatarImage.color=Color.white;
            avatarImage.raycastTarget=false;
            avatarImage.uvRect=avatarTexture!=null?CoverTexture(avatarTexture,1f):new Rect(0,0,1,1);
            titleAvatarImage=avatarImage;
            titleLoginScanHeight=84f;
            titleLoginScanImage=Box(previewRoot,"Avatar scanline",0,0,84,2,new Color(.62f,.94f,1f,.14f));
            titleLoginScan=titleLoginScanImage.rectTransform;

            Mono(card.rectTransform,"Operator label","LOCAL OPERATOR",140,82,420,20,12,DesktopWindowMuted);
            Label(card.rectTransform,"Operator name","RHYTHM_HACKER",138,111,462,34,24,true,DesktopWindowText);
            Mono(card.rectTransform,"Operator status","本地账户  /  信号已就绪",140,151,420,22,12,DesktopWindowMuted);
            Box(card.rectTransform,"Account divider",32,194,576,2,new Color(.46f,.34f,.76f,.24f));
            Label(card.rectTransform,"Password label","输入密码",32,211,330,26,16,true,DesktopWindowText);

            // The frame travels with the input during invalid-password feedback.
            titleLoginFieldRect=Rect("Password field",card.rectTransform,32,246,400,52);
            titleLoginFieldBasePosition=titleLoginFieldRect.anchoredPosition;
            var fieldBackground=titleLoginFieldRect.gameObject.AddComponent<CyberPanelGraphic>();
            fieldBackground.color=new Color(.995f,.985f,1f);
            fieldBackground.pixelStyle=true;fieldBackground.rounded=true;fieldBackground.cornerRadius=6;
            fieldBackground.accent=CyberTheme.Purple;
            var fieldInput=titleLoginFieldRect.gameObject.AddComponent<InputField>();
            fieldInput.targetGraphic=fieldBackground;
            fieldInput.lineType=InputField.LineType.SingleLine;
            fieldInput.contentType=InputField.ContentType.Password;
            fieldInput.characterLimit=4;
            fieldInput.shouldHideMobileInput=true;
            fieldInput.caretWidth=2;
            fieldInput.customCaretColor=true;
            fieldInput.caretColor=CyberTheme.Purple;
            fieldInput.selectionColor=new Color(.61f,.39f,1f,.30f);
            var fieldText=Label(titleLoginFieldRect,"Password text","",16,0,368,52,24,false,
                DesktopWindowText,TextAnchor.MiddleLeft);
            fieldText.horizontalOverflow=HorizontalWrapMode.Overflow;
            var fieldPlaceholder=Label(titleLoginFieldRect,"Password placeholder","PASSWORD",16,0,368,52,16,false,
                new Color(.47f,.39f,.68f,.72f),TextAnchor.MiddleLeft);
            fieldInput.textComponent=fieldText;
            fieldInput.placeholder=fieldPlaceholder;
            titlePasswordInput=fieldInput;
            titlePasswordFocusPending=true;

            var submit=ActionButton(card.rectTransform,"Enter game","登录  →",452,242,156,60,()=>SubmitTitleLogin(),
                true,CyberTheme.Purple,20);
            RoundIcon(submit,8);
            SetButtonCaptionColor(submit,DesktopWindowText);

            titleLoginMessage=Mono(card.rectTransform,"Login message",TitlePasswordHint,
                32,318,576,26,14,DesktopWindowMuted);
            Box(card.rectTransform,"Login help panel",24,382,592,68,new Color(.91f,.88f,.98f,.98f));
            Box(card.rectTransform,"Login help accent",24,382,4,68,CyberTheme.Cyan);
            Label(card.rectTransform,"Login help","登录后进入信号桌面",44,391,548,26,16,true,DesktopWindowText);
            Mono(card.rectTransform,"Account footer","按 Enter 确认  /  或点击登录",44,423,548,20,12,DesktopWindowMuted);

            titleCtaGroup=accountRoot.gameObject.AddComponent<CanvasGroup>();
            titleCtaGroup.alpha=0;titleCtaGroup.interactable=false;titleCtaGroup.blocksRaycasts=false;
            titleCtaStarted=Time.unscaledTime;

            var dock=CreateSoftDesktopDock("Login");
            var footerMark=Rect("Login footer mark",dock.rectTransform,18,15,36,32);
            SignalLogo(footerMark,0,0,.28f,true);
            foreach(var tile in footerMark.GetComponentsInChildren<CyberPanelGraphic>())
            {tile.rounded=true;tile.cornerRadius=3;tile.outline=false;}
            Mono(dock.rectTransform,"Login footer left","RHYTHM HACKER  /  SIGN IN",74,18,510,26,13,
                DesktopWindowText,TextAnchor.MiddleLeft);
            Mono(dock.rectTransform,"Login footer status","LOCAL SIGNAL DESKTOP",1018,20,310,24,11,
                new Color(.43f,.35f,.62f),TextAnchor.MiddleRight);
            var power=ActionButton(dock.rectTransform,"Turn off computer","",1360,8,194,46,owner.OpenPowerChoice,
                false,new Color(.73f,.66f,.87f),18);
            RoundIcon(power,18);
            ((CyberPanelGraphic)power.targetGraphic).outline=false;
            var powerIcon=Rect("Power symbol",(RectTransform)power.transform,18,11,24,24).gameObject.AddComponent<PowerSymbolGraphic>();
            powerIcon.color=DesktopWindowText;powerIcon.raycastTarget=false;
            Label((RectTransform)power.transform,"Power label","电源选项",54,7,124,32,18,true,
                DesktopWindowText,TextAnchor.MiddleLeft);
            BeginLoginEntrance();
        }
        public void ShowPowerChoice()
        {
            Begin("Power choices","",false,.5f);
            Box(page,"Black screen",0,0,1600,900,Color.black);
            Label(page,"Power question","接下来，想去哪里？",400,292,800,64,34,true,
                new Color(.88f,.84f,.91f),TextAnchor.MiddleCenter);
            Label(page,"Power explanation","退出游戏，或关掉电脑，去外面走走。",350,373,900,40,20,false,
                new Color(.58f,.54f,.65f),TextAnchor.MiddleCenter);
            ActionButton(page,"Leave computer","关闭电脑",830,472,340,78,owner.ShutDownComputer,
                true,new Color(.81f,.66f,.47f),25);
            ActionButton(page,"Exit game","完全退出游戏",430,472,340,78,owner.QuitFromPowerChoice,
                false,new Color(.57f,.52f,.66f),25);
            Label(page,"Computer choice hint","继续 Turi 的故事",835,566,330,32,17,false,
                new Color(.61f,.55f,.47f),TextAnchor.MiddleCenter);
            ActionButton(page,"Cancel power","返回登录",660,686,280,48,owner.CancelPowerChoice,
                false,new Color(.40f,.38f,.47f),18);
        }
        Text dormitoryHint;
        Text dormitoryControls;
        public void SetDormitoryDialogue(bool active)
        {
            if(dormitoryHint!=null)dormitoryHint.gameObject.SetActive(!active);
            if(dormitoryControls!=null)dormitoryControls.gameObject.SetActive(!active);
        }
        CanvasGroup dormitoryLocationGroup;
        public void UpdateDormitoryLocation(float alpha)
        {
            if(dormitoryLocationGroup!=null)dormitoryLocationGroup.alpha=alpha;
        }
        public ApartmentDepartureCutscene ShowApartmentDeparture(Action completed)
        {
            Begin("Apartment departure","",false,.01f);
            Box(page,"Departure black backing",0,0,1600,900,Color.black);
            var cutscene=Rect("Apartment departure timeline",page,0,0,1600,900).gameObject.AddComponent<ApartmentDepartureCutscene>();
            cutscene.Initialize(completed);return cutscene;
        }
        public void UpdateDormitoryHint(string hint)
        {
            if(dormitoryHint!=null)dormitoryHint.text=hint;
        }
        public void ShowDormitory(RenderTexture scene,DormitoryDuskScene stage=null)
        {
            Begin("Dormitory evening","",false,3f);
            Box(page,"Dusk letterbox",0,0,1600,900,Color.black);
            var image=Rect("Dusk world",page,0,0,1600,900).gameObject.AddComponent<RawImage>();
            image.texture=scene;image.raycastTarget=false;
            if(stage!=null)stage.SetInputViewport(image.rectTransform);
            var location=Rect("Location reveal after pullback",page,72,680,740,130);
            dormitoryLocationGroup=location.gameObject.AddComponent<CanvasGroup>();dormitoryLocationGroup.alpha=0;
            dormitoryLocationGroup.interactable=false;dormitoryLocationGroup.blocksRaycasts=false;
            Label(location,"Location title shadow","宿舍楼下",3,3,700,88,58,true,new Color(.08f,.045f,.10f,.8f));
            Label(location,"Location title","宿舍楼下",0,0,700,88,58,true,new Color(.98f,.90f,.77f));
            Box(location,"Location gold rule",3,99,236,2,new Color(.86f,.65f,.37f));
            Mono(location,"Location evening","18:24   /   CLEAR EVENING",3,108,520,20,13,new Color(.94f,.83f,.70f));
            dormitoryControls=Label(page,"Dusk controls","左键点击移动 · 滚轮调整镜头 · E 互动",72,835,740,32,18,false,
                new Color(.92f,.86f,.80f,.88f));
            dormitoryHint=Label(page,"Stair interaction","正在下楼…",840,835,680,32,20,false,
                new Color(1f,.85f,.57f),TextAnchor.MiddleRight);
        }
        public bool SubmitTitleLogin()
        {
            if(titlePasswordInput==null||owner.Page!=FrontendPage.Title)return false;
            string password=(titlePasswordInput.text??"").Trim();
            if(password=="0000")
            {
                titlePasswordInput.DeactivateInputField();
                titlePasswordFocusPending=false;
                owner.EnterGame();
                return true;
            }
            owner.PlayUiCue("error");
            titleLoginErrorUntil=Time.unscaledTime+1.05f;
            if(titleLoginMessage!=null)
            {
                titleLoginMessage.text="密码错误，请重新输入。";
                titleLoginMessage.color=new Color(.69f,.16f,.39f);
            }
            titlePasswordInput.text="";
            titlePasswordInput.ActivateInputField();
            return false;
        }
        public void SetTitlePassword(string value)
        {
            if(titlePasswordInput!=null)titlePasswordInput.text=value??"";
        }
        public void TypeFriendCharacterForTest()
        {
            RevealFriendCharacter();
        }
        public void ShowMain(float fadeDuration=.55f,bool slideFromSongs=false,int storyLevel=0)
        {
            storyLevelIndex=Mathf.Max(0,storyLevel);
            Begin("Main interface","01   /   MAIN MENU",false,Mathf.Max(.18f,fadeDuration),slideFromSongs,slideFromSongs?-900:0);
            homeDesktop=Rect("Lavender desktop wallpaper",page,0,0,1600,900).gameObject.AddComponent<CyberMainDesktopGraphic>();
            homeDesktop.raycastTarget=false;
            Box(page,"Desktop glass wash",0,0,1600,900,new Color(.96f,.94f,1f,.10f));
            Box(page,"Desktop masthead line",22,48,126,2,new Color(.38f,.27f,.76f,.58f));
            Mono(page,"Desktop masthead","LOCAL  /  09",24,25,132,18,11,new Color(.29f,.19f,.59f));

            // Desktop shortcuts sit directly on the wallpaper; there is no enclosing app tray.
            CreateDesktopShortcut(page,"Open diary","日记本",DesktopIconKind.Diary,
                22,78,owner.ShowDiary,CyberTheme.Purple,out homeDiaryUnreadDot);
            CreateDesktopShortcut(page,"Open friends","朋友系统",DesktopIconKind.Friends,
                22,202,owner.ShowFriends,new Color(.16f,.68f,.82f),out friendUnreadDot);
            CreateDesktopShortcut(page,"Open news","新闻系统",DesktopIconKind.News,
                22,326,owner.ShowNews,CyberTheme.Pink,out newsUnreadDot);
            CreateDesktopShortcut(page,"Open music player","音乐播放器",DesktopIconKind.Music,
                22,450,owner.ShowMusicPlayer,CyberTheme.Cyan,out _);

            // Main application window: one framed window contains the stage and its launch action.
            var mainWindow=Panel(artboard,"Main application window",172,58,1386,742,new Color(.965f,.95f,.995f,.94f),new Color(.43f,.29f,.85f));
            mainWindow.rounded=true;mainWindow.cornerRadius=12;
            mainWindowRoot=mainWindow.rectTransform;mainWindowGroup=mainWindowRoot.gameObject.AddComponent<CanvasGroup>();
            mainWindowRoot.gameObject.AddComponent<RectMask2D>();
            mainWindowFocus=AddWindowFocus(mainWindowRoot,FrontendPage.Main);
            mainWindowRoot.anchoredPosition=mainWindowPosition;
            var titleBar=Panel(mainWindow.rectTransform,"Main window title bar",0,0,1386,52,new Color(.59f,.53f,.87f,.98f),new Color(.92f,.69f,.98f));
            titleBar.raycastTarget=true;
            AddWindowDrag(titleBar.rectTransform,mainWindowRoot,()=>!mainEntryActive&&!mainWindowMinimizing&&!mainWindowRestoring,position=>mainWindowPosition=position,()=>FocusWindow(FrontendPage.Main));
            titleBar.rounded=true;titleBar.cornerRadius=12;
            Box(titleBar.rectTransform,"Title bar lower edge",0,45,1386,7,new Color(.48f,.40f,.78f,.90f));
            var windowMark=Panel(titleBar.rectTransform,"Window signal mark",14,10,32,32,new Color(.36f,.27f,.72f,.96f),CyberTheme.Cyan);
            windowMark.rounded=true;windowMark.cornerRadius=7;
            var markIcon=Rect("Window music icon",windowMark.rectTransform,6,6,20,20).gameObject.AddComponent<CyberDesktopIconGraphic>();
            markIcon.kind=DesktopIconKind.Music;markIcon.iconColor=new Color(.95f,.93f,1f);markIcon.raycastTarget=false;
            Label(titleBar.rectTransform,"Main window title","RHYTHM HACKER  /  信号桌面",58,10,550,30,19,true,
                new Color(.98f,.97f,1f),TextAnchor.MiddleLeft).font=UiChineseFont;
            Box(titleBar.rectTransform,"Title status light",1036,21,8,8,CyberTheme.Cyan);
            Mono(titleBar.rectTransform,"Title status","LOCAL SESSION  /  ACTIVE",1052,14,235,24,11,new Color(.95f,.92f,1f),TextAnchor.MiddleRight);
            CreateWindowControl(titleBar.rectTransform,"Minimize main window","-",1298,38,MinimizeMainWindow,
                new Color(.49f,.42f,.78f,.92f));
            CreateWindowControl(titleBar.rectTransform,"Close main window","X",1342,38,ShowReturnToTitleConfirmation,
                new Color(.49f,.42f,.78f,.92f));

            var stageViewport=Rect("Main stage viewport",mainWindow.rectTransform,18,68,812,578);
            stageViewport.gameObject.AddComponent<RectMask2D>();
            Box(stageViewport,"Stage base",0,0,812,578,new Color(.84f,.81f,.95f,.98f));
            homeBackgroundAspect=812f/578f;
            var stageBackground=Rect("Stage background",stageViewport,0,0,812,578);
            homeBackgroundImage=stageBackground.gameObject.AddComponent<RawImage>();
            homeBackgroundImage.color=Color.white;
            homeBackgroundImage.raycastTarget=false;
            homeBackgroundImageNext=Rect("Next stage background",stageBackground,0,0,812,578).gameObject.AddComponent<RawImage>();
            homeBackgroundImageNext.color=new Color(1,1,1,0);
            homeBackgroundImageNext.raycastTarget=false;
            homeBackgroundImageNext.enabled=false;
            homeFigureBackdrop=Rect("Stage fallback",stageViewport,0,0,812,578).gameObject;
            homeHeroPlaceholder=homeFigureBackdrop.AddComponent<CyberIdolGraphic>();
            homeHeroPlaceholder.raycastTarget=false;
            Box(stageViewport,"Stage glass top",0,0,812,62,new Color(.18f,.12f,.37f,.52f));
            Box(stageViewport,"Stage accent top",0,0,812,3,CyberTheme.Cyan);
            Mono(stageViewport,"Stage eyebrow","STAGE PREVIEW   /   当前舞台",24,12,420,20,12,new Color(.98f,.97f,1f));
            Label(stageViewport,"Stage image cue","本地画面 · 可在开始菜单切换",24,37,510,18,11,false,new Color(.91f,.86f,1f)).font=UiChineseFont;
            Box(stageViewport,"Stage chapter plate",657,14,131,33,new Color(.91f,.88f,1f,.91f));
            Mono(stageViewport,"Stage chapter plate text","AREA  "+(storyLevelIndex+1).ToString("D2"),669,19,108,24,13,new Color(.29f,.18f,.58f),TextAnchor.MiddleCenter);
            Box(stageViewport,"Stage lower tint",0,458,812,120,new Color(.16f,.10f,.37f,.78f));
            Box(stageViewport,"Stage lower rail",24,478,4,70,CyberTheme.Cyan);
            Mono(stageViewport,"Stage kicker","CURRENT ROUTE  /  SIGNAL STAGE",42,474,548,22,12,new Color(.83f,.78f,1f));
            Label(stageViewport,"Stage caption","第 "+(storyLevelIndex+1).ToString("D2")+" 区 · 信号舞台",40,499,555,40,26,true,
                new Color(.98f,.97f,1f),TextAnchor.MiddleLeft).font=UiChineseFont;
            Label(stageViewport,"Stage detail","选择演奏模式，继续当前章节",42,543,460,22,12,false,
                new Color(.87f,.82f,1f),TextAnchor.MiddleLeft).font=UiChineseFont;
            homeBackgroundCounter=Mono(stageViewport,"Background counter","BACKGROUND  "+(homeBackgroundIndex+1).ToString("D2")+" / 03",585,531,202,23,11,
                new Color(.92f,.88f,1f),TextAnchor.MiddleRight);

            var overview=Panel(mainWindow.rectTransform,"Mission overview",852,68,516,578,new Color(.91f,.89f,.985f,.97f),new Color(.61f,.39f,1f));
            overview.rounded=true;overview.cornerRadius=10;
            Box(overview.rectTransform,"Overview accent",22,24,4,66,CyberTheme.Purple);
            Mono(overview.rectTransform,"Overview subtitle","DESKTOP OVERVIEW  /  主控面板",40,19,410,20,11,CyberTheme.Purple);
            Label(overview.rectTransform,"Overview heading","从这里继续",38,44,442,43,27,true,
                DesktopWindowText,TextAnchor.MiddleLeft).font=UiChineseFont;
            Label(overview.rectTransform,"Overview hint","你的章节、联系人与最新动态都在这里。",40,87,444,24,13,false,
                DesktopWindowMuted,TextAnchor.MiddleLeft).font=UiChineseFont;
            int unreadNews=0;foreach(bool unread in newsUnreadItems)if(unread)unreadNews++;
            int onlineFriends=1+(bakaFriendAccepted?1:0);
            Box(overview.rectTransform,"Overview divider",40,126,436,1,new Color(.46f,.34f,.76f,.24f));
            CreateHomeStatusCard(overview.rectTransform,"当前章节","0-"+(storyLevelIndex+1),"继续推进主线",40,148,CyberTheme.Purple,out homeLevelValue);
            CreateHomeStatusCard(overview.rectTransform,"在线好友",onlineFriends.ToString("D2"),"打开朋友系统查看",264,148,CyberTheme.Cyan,out _);
            CreateHomeStatusCard(overview.rectTransform,"未读新闻",unreadNews.ToString("D2"),"去新闻系统阅读",40,284,CyberTheme.Pink,out _);
            CreateHomeStatusCard(overview.rectTransform,"舞台入口","READY","选择模式后开始",264,284,new Color(.84f,.59f,.18f),out _);
            Box(overview.rectTransform,"Next action panel",40,441,436,111,new Color(.84f,.80f,.96f,.66f));
            Box(overview.rectTransform,"Next action rail",40,441,5,111,CyberTheme.Cyan);
            Mono(overview.rectTransform,"Next action tag","NEXT STEP  /  下一步",57,453,392,20,11,CyberTheme.Purple);
            Label(overview.rectTransform,"Next action headline","进入演奏模式",56,477,392,30,19,true,DesktopWindowText).font=UiChineseFont;
            Label(overview.rectTransform,"Next action detail","可选择剧情关卡或自由选曲",57,514,394,22,13,false,DesktopWindowMuted).font=UiChineseFont;

            Box(mainWindow.rectTransform,"Launch strip",18,664,1350,58,new Color(.91f,.88f,.98f,.98f));
            Box(mainWindow.rectTransform,"Launch strip accent",18,664,5,58,CyberTheme.Cyan);
            var launchMark=Panel(mainWindow.rectTransform,"Launch mark",28,674,38,38,new Color(.45f,.29f,.85f,.96f),CyberTheme.Cyan);
            launchMark.rounded=true;launchMark.cornerRadius=8;
            var launchIcon=Rect("Launch icon",launchMark.rectTransform,8,8,22,22).gameObject.AddComponent<CyberDesktopIconGraphic>();
            launchIcon.kind=DesktopIconKind.Music;launchIcon.iconColor=new Color(.98f,.97f,1f);launchIcon.raycastTarget=false;
            Label(mainWindow.rectTransform,"Launch title","准备进入演奏",80,669,410,29,18,true,
                new Color(.20f,.12f,.42f),TextAnchor.MiddleLeft).font=UiChineseFont;
            Label(mainWindow.rectTransform,"Launch copy","选择剧情关卡或自由选曲 · 按 Enter 继续",80,698,600,19,12,false,
                new Color(.47f,.39f,.68f)).font=UiChineseFont;
            var enter=ActionButton(mainWindow.rectTransform,"Open songs","选择演奏模式   →",1062,670,302,46,owner.EnterMusic,true,CyberTheme.Purple,18);
            ((CyberPanelGraphic)enter.targetGraphic).cut=14;

            // Share the login's inset dock while preserving desktop window controls.
            CreateSoftDesktopDock("Desktop");
            var startButton=ActionButton(page,"Open start menu","",28,834,144,46,ToggleStartMenu,false,
                new Color(.73f,.66f,.87f),18);
            var startPanel=(CyberPanelGraphic)startButton.targetGraphic;
            startPanel.rounded=true;startPanel.cornerRadius=18;startPanel.outline=false;
            var startIcon=Rect("Start icon",startPanel.rectTransform,14,8,30,30).gameObject.AddComponent<CyberDesktopIconGraphic>();
            startIcon.kind=DesktopIconKind.Music;startIcon.iconColor=DesktopWindowText;startIcon.raycastTarget=false;
            Label(startPanel.rectTransform,"Start label","开始",52,7,78,31,18,true,DesktopWindowText,TextAnchor.MiddleLeft).font=UiChineseFont;
            EnsureTaskbarTask(FrontendPage.Main,"RHYTHM HACKER  /  MAINUI");
            Mono(page,"Taskbar system","LOCAL SIGNAL DESKTOP",1160,846,210,24,11,
                new Color(.43f,.35f,.62f),TextAnchor.MiddleRight);
            Label(page,"Taskbar chapter","第 "+((storyLevelIndex+1).ToString("D2"))+" 区",1404,842,146,30,17,true,
                DesktopWindowText,TextAnchor.MiddleRight).font=UiChineseFont;
            ApplyHomeBackground();
            RefreshUnreadIndicators();
            BeginDesktopEntrance();
            if(slideFromSongs)StartPageSlide(true);
        }

        CyberPanelGraphic CreateSoftDesktopDock(string name)
        {
            var shadow=Panel(page,name+" dock shadow",18,830,1564,62,
                new Color(.36f,.28f,.58f,.10f),CyberTheme.Purple);
            shadow.rounded=true;shadow.cornerRadius=20;shadow.outline=false;
            var dock=Panel(page,name+" bottom dock",18,826,1564,62,
                new Color(.82f,.78f,.92f,.96f),CyberTheme.Purple);
            dock.rounded=true;dock.cornerRadius=20;dock.outline=false;
            return dock;
        }

        void CreateDesktopShortcut(RectTransform parent,string buttonName,string caption,DesktopIconKind kind,
            float x,float y,Action callback,Color accent,out Image unreadDot)
        {
            var button=ActionButton(parent,buttonName,"",x,y,130,118,callback,false,new Color(1f,1f,1f,.01f));
            var hitPanel=(CyberPanelGraphic)button.targetGraphic;hitPanel.outline=false;
            var icon=Rect("Pixel desktop shortcut",hitPanel.rectTransform,29,1,72,72).gameObject.AddComponent<PixelDesktopIconGraphic>();
            icon.kind=kind;icon.raycastTarget=false;
            Box(hitPanel.rectTransform,"Shortcut label backing",11,77,108,23,new Color(.96f,.94f,1f,.56f));
            Label(hitPanel.rectTransform,"Shortcut label",caption,8,75,114,26,15,true,
                new Color(.19f,.11f,.40f),TextAnchor.MiddleCenter).font=UiChineseFont;
            string hint=kind==DesktopIconKind.Diary?"DIARY.TXT":kind==DesktopIconKind.Friends?"FRIENDS.EXE":
                kind==DesktopIconKind.News?"NEWS.EXE":"MUSIC.EXE";
            Mono(hitPanel.rectTransform,"Shortcut file type",hint,7,101,116,14,9,new Color(.39f,.28f,.62f),TextAnchor.MiddleCenter);
            unreadDot=Box(hitPanel.rectTransform,"Shortcut unread dot",102,6,11,11,new Color(.96f,.13f,.28f));
            if(kind==DesktopIconKind.Music)unreadDot.gameObject.SetActive(false);
        }

        void CreateDesktopSystemButton(RectTransform parent,string buttonName,string caption,DesktopIconKind kind,
            float x,float y,Action callback,Color accent)
        {
            var button=ActionButton(parent,buttonName,"",x,y,164,108,callback,false,new Color(1f,1f,1f,.01f));
            var hitPanel=(CyberPanelGraphic)button.targetGraphic;hitPanel.outline=false;
            var iconPlate=Panel(hitPanel.rectTransform,"System icon plate",48,2,68,68,new Color(.92f,.90f,1f,.98f),accent);
            iconPlate.rounded=true;iconPlate.cornerRadius=12;
            Box(iconPlate.rectTransform,"System icon top light",7,6,54,5,new Color(1f,1f,1f,.88f));
            Box(iconPlate.rectTransform,"System icon bottom shade",7,57,54,5,new Color(.30f,.21f,.57f,.16f));
            var icon=Rect("System icon",iconPlate.rectTransform,12,12,44,44).gameObject.AddComponent<CyberDesktopIconGraphic>();
            icon.kind=kind;icon.iconColor=accent;icon.raycastTarget=false;
            Label(hitPanel.rectTransform,"System label",caption,6,72,152,30,14,true,
                new Color(.20f,.12f,.44f),TextAnchor.UpperCenter).font=UiChineseFont;
        }

        void CreateHomeStatusCard(RectTransform parent,string label,string value,string hint,
            float x,float y,Color accent,out Text valueText)
        {
            var card=Panel(parent,label+" status card",x,y,212,116,new Color(.975f,.96f,1f,.90f),accent);
            card.cut=10;card.outline=false;
            Box(card.rectTransform,"Card color rail",0,0,212,4,accent);
            Box(card.rectTransform,"Card signal point",182,17,8,8,accent);
            Label(card.rectTransform,"Card label",label,16,16,164,24,13,true,DesktopWindowMuted).font=UiChineseFont;
            valueText=Label(card.rectTransform,"Card value",value,15,42,182,43,31,true,DesktopWindowText);
            valueText.font=mainUiPixelTheme?CyberTheme.Pixel:(label=="舞台入口"?CyberTheme.Mono:CyberTheme.Chinese);
            Label(card.rectTransform,"Card hint",hint,16,88,182,20,11,false,DesktopWindowMuted).font=UiChineseFont;
        }
        void CreateHomeMetric(RectTransform parent,string label,string value,float amount,float y,Color accent)
        {
            Label(parent,label+" label",label,22,y,150,26,15,true,new Color(.22f,.14f,.46f),TextAnchor.MiddleLeft).font=UiChineseFont;
            Mono(parent,label+" value",value,184,y-3,116,30,18,new Color(.25f,.17f,.49f),TextAnchor.MiddleRight);
            var track=Box(parent,label+" track",22,y+34,282,13,new Color(.78f,.76f,.88f,.86f));
            Box(track.rectTransform,"Metric fill",0,0,282f*Mathf.Clamp01(amount),13,accent);
        }

        void CreateSystemInfoRow(RectTransform parent,string label,string value,float y)
        {
            Label(parent,label+" label",label,20,y,140,24,13,true,new Color(.28f,.20f,.50f),TextAnchor.MiddleLeft).font=UiChineseFont;
            Mono(parent,label+" value",value,174,y,150,24,12,new Color(.38f,.29f,.61f),TextAnchor.MiddleRight);
            Box(parent,label+" divider",20,y+30,304,1,new Color(.44f,.31f,.72f,.14f));
        }
        void EnsureTaskbarTask(FrontendPage taskPage,string caption)
        {
            if(page==null)return;
            for(int i=0;i<taskbarTasks.Count;i++)
            {
                if(taskbarTasks[i].Page!=taskPage)continue;
                taskbarTasks[i].Label.text=caption;
                RefreshTaskbarTasks();
                return;
            }
            var taskPageForButton=taskPage;
            var button=ActionButton(page,"Restore "+taskPage.ToString().ToLowerInvariant(),"",184,834,184,46,
                ()=>ToggleTaskbarWindow(taskPageForButton),false,new Color(.73f,.66f,.87f),14);
            var panel=(CyberPanelGraphic)button.targetGraphic;
            panel.rounded=true;panel.cornerRadius=18;panel.outline=false;
            Color accent=taskPage==FrontendPage.News?CyberTheme.Pink:
                taskPage==FrontendPage.Friends||taskPage==FrontendPage.MusicPlayer?CyberTheme.Cyan:CyberTheme.Purple;
            var rail=Box(panel.rectTransform,"Taskbar active rail",70,39,44,3,accent);
            Box(panel.rectTransform,"Taskbar app marker",14,19,5,8,accent);
            var label=Label(panel.rectTransform,"Taskbar title",caption,26,7,144,28,12,true,
                DesktopWindowText,TextAnchor.MiddleLeft);
            taskbarTasks.Add(new TaskbarTask{Page=taskPage,Button=button,Label=label,Rail=rail});
            RefreshTaskbarTasks();
        }
        void RemoveTaskbarTask(FrontendPage taskPage)
        {
            for(int i=taskbarTasks.Count-1;i>=0;i--)
            {
                if(taskbarTasks[i].Page!=taskPage)continue;
                if(taskbarTasks[i].Button!=null)UnityEngine.Object.Destroy(taskbarTasks[i].Button.gameObject);
                taskbarTasks.RemoveAt(i);
            }
            RefreshTaskbarTasks();
        }
        void RefreshTaskbarTasks()
        {
            for(int i=0;i<taskbarTasks.Count;i++)
            {
                var task=taskbarTasks[i];
                if(task.Button==null)continue;
                var rect=(RectTransform)task.Button.transform;
                DesktopMotion.GetOrAdd<DesktopReflowMotion>(rect.gameObject)
                    .MoveTo(new Vector2(184+i*192,-834));
            }
        }
        Vector2 TaskbarSlotPosition(FrontendPage taskPage)
        {
            for(int i=0;i<taskbarTasks.Count;i++)
                if(taskbarTasks[i].Page==taskPage)
                    return new Vector2(184+i*192,-834);
            return new Vector2(184,-834);
        }
        void ToggleTaskbarWindow(FrontendPage taskPage)
        {
            bool visible=taskPage==FrontendPage.Main
                ?!mainWindowMinimized&&!mainWindowMinimizing
                :taskPage==FrontendPage.Diary
                    ?diaryWindowRoot!=null&&diaryWindowRoot.gameObject.activeSelf&&!diaryWindowMinimized
                    :taskPage==FrontendPage.Friends
                        ?friendWindowRoot!=null&&friendWindowRoot.gameObject.activeSelf&&!friendWindowMinimized
                        :taskPage==FrontendPage.News
                            ?newsWindowRoot!=null&&newsWindowRoot.gameObject.activeSelf&&!newsWindowMinimized
                            :taskPage==FrontendPage.MusicPlayer&&musicWindowRoot!=null&&musicWindowRoot.gameObject.activeSelf&&!musicWindowMinimized;
            if(owner.Page==taskPage&&visible&&FrontmostWindow==taskPage)
            {
                if(taskPage==FrontendPage.Main)MinimizeMainWindow();else MinimizeCurrentWindow();
                return;
            }
            owner.ActivateWindow(taskPage);
        }
        void SetTaskbarTaskMinimized(FrontendPage taskPage,bool minimized)
        {
            for(int i=0;i<taskbarTasks.Count;i++)
            {
                if(taskbarTasks[i].Page!=taskPage||taskbarTasks[i].Button==null)continue;
                taskbarTasks[i].Minimized=minimized;
            }
        }
        void MinimizeCurrentWindow()
        {
            if(owner.Page==FrontendPage.Friends&&friendWindowRoot!=null&&!friendWindowMinimizing)
            {
                friendWindowMinimizing=true;friendWindowRestoring=false;friendWindowStarted=Time.unscaledTime;
                SetTaskbarTaskMinimized(FrontendPage.Friends,true);
            }
            else if(owner.Page==FrontendPage.Diary&&diaryWindowRoot!=null&&!diaryWindowMinimizing)
            {
                diaryWindowMinimizing=true;diaryWindowRestoring=false;diaryWindowStarted=Time.unscaledTime;
                SetTaskbarTaskMinimized(FrontendPage.Diary,true);
            }
            else if(owner.Page==FrontendPage.News&&newsWindowRoot!=null&&!newsWindowMinimizing)
            {
                newsWindowMinimizing=true;newsWindowRestoring=false;newsWindowStarted=Time.unscaledTime;
                SetTaskbarTaskMinimized(FrontendPage.News,true);
            }
            else if(owner.Page==FrontendPage.MusicPlayer&&musicWindowRoot!=null&&!musicWindowMinimizing)
            {
                musicWindowMinimizing=true;musicWindowRestoring=false;musicWindowStarted=Time.unscaledTime;
                SetTaskbarTaskMinimized(FrontendPage.MusicPlayer,true);
            }
            owner.FocusDesktopWindow(FrontmostWindow);
        }
        void MinimizeMainWindow()
        {
            if(mainWindowRoot==null||mainWindowMinimized||mainWindowMinimizing)return;
            mainWindowMinimizing=true;mainWindowRestoring=false;mainWindowMotionStarted=Time.unscaledTime;
            SetTaskbarTaskMinimized(FrontendPage.Main,true);
        }
        void RestoreMainWindow()
        {
            if(mainWindowRoot==null)return;
            if(mainWindowRoot.gameObject.activeSelf&&!mainWindowMinimized&&!mainWindowMinimizing)
            {
                FocusWindow(FrontendPage.Main);
                return;
            }
            mainWindowRoot.gameObject.SetActive(true);
            mainWindowMinimized=false;mainWindowMinimizing=false;mainWindowRestoring=true;
            mainWindowMotionStarted=Time.unscaledTime;
            SetTaskbarTaskMinimized(FrontendPage.Main,false);
            FocusWindow(FrontendPage.Main);
        }
        RectTransform WindowRoot(FrontendPage target)
        {
            switch(target)
            {
                case FrontendPage.Main:return mainWindowRoot;
                case FrontendPage.Device:return deviceWindowRoot;
                case FrontendPage.Diary:return diaryWindowRoot;
                case FrontendPage.Friends:return friendWindowRoot;
                case FrontendPage.News:return newsWindowRoot;
                case FrontendPage.MusicPlayer:return musicWindowRoot;
                default:return null;
            }
        }
        public FrontendPage FrontmostWindow
        {
            get
            {
                FrontendPage top=FrontendPage.Main;
                int order=-1;
                foreach(FrontendPage candidate in new[]{FrontendPage.Main,FrontendPage.Device,
                    FrontendPage.Diary,FrontendPage.Friends,FrontendPage.News,FrontendPage.MusicPlayer})
                {
                    var root=WindowRoot(candidate);
                    if(root==null||!root.gameObject.activeInHierarchy)continue;
                    if(candidate==FrontendPage.Main&&mainWindowMinimizing)continue;
                    if(candidate==FrontendPage.Diary&&diaryWindowMinimizing)continue;
                    if(candidate==FrontendPage.Friends&&friendWindowMinimizing)continue;
                    if(candidate==FrontendPage.News&&newsWindowMinimizing)continue;
                    if(candidate==FrontendPage.MusicPlayer&&(musicWindowMinimizing||musicWindowClosing))continue;
                    if(root.GetSiblingIndex()<=order)continue;
                    top=candidate;order=root.GetSiblingIndex();
                }
                return top;
            }
        }
        public void FocusWindow(FrontendPage target)
        {
            var root=WindowRoot(target);
            if(root==null||!root.gameObject.activeInHierarchy)return;
            root.SetAsLastSibling();
            owner.FocusDesktopWindow(target);
        }
        WindowFocusHandler AddWindowFocus(RectTransform root,FrontendPage target)
        {
            var focus=root.gameObject.AddComponent<WindowFocusHandler>();
            focus.Focus=()=>FocusWindow(target);
            return focus;
        }
        public bool ActivateWindow(FrontendPage target)
        {
            if(target==FrontendPage.Main)
            {
                RestoreMainWindow();
                return true;
            }
            return RestoreWindow(target);
        }
        void HideApplicationWindowsExcept(FrontendPage target)
        {
            if(target!=FrontendPage.Friends&&friendWindowRoot!=null&&friendWindowRoot.gameObject.activeSelf)
            {
                friendWindowRoot.gameObject.SetActive(false);
                if(friendWindowBlocker!=null)friendWindowBlocker.SetActive(false);
                friendWindowMinimized=true;
                SetTaskbarTaskMinimized(FrontendPage.Friends,true);
            }
            if(target!=FrontendPage.Diary&&diaryWindowRoot!=null&&diaryWindowRoot.gameObject.activeSelf)
            {
                diaryWindowRoot.gameObject.SetActive(false);
                if(diaryWindowBlocker!=null)diaryWindowBlocker.SetActive(false);
                diaryWindowMinimized=true;
                SetTaskbarTaskMinimized(FrontendPage.Diary,true);
            }
            if(target!=FrontendPage.News&&newsWindowRoot!=null&&newsWindowRoot.gameObject.activeSelf)
            {
                newsWindowRoot.gameObject.SetActive(false);
                if(newsWindowBlocker!=null)newsWindowBlocker.SetActive(false);
                newsWindowMinimized=true;
                SetTaskbarTaskMinimized(FrontendPage.News,true);
            }
            if(target!=FrontendPage.MusicPlayer&&musicWindowRoot!=null&&musicWindowRoot.gameObject.activeSelf)
            {
                musicWindowRoot.gameObject.SetActive(false);
                musicWindowMinimized=true;
                SetTaskbarTaskMinimized(FrontendPage.MusicPlayer,true);
            }
        }
        bool RestoreWindow(FrontendPage target)
        {
            if(target==FrontendPage.Friends&&friendWindowRoot!=null)
            {
                if(friendWindowRoot.gameObject.activeSelf&&!friendWindowMinimized&&!friendWindowMinimizing)
                {FocusWindow(target);return true;}
                friendWindowMinimized=false;friendWindowRestoring=true;friendWindowMinimizing=false;
                friendWindowFromTaskbar=true;
                friendWindowStarted=Time.unscaledTime;
                friendWindowRoot.gameObject.SetActive(true);
                if(friendWindowBlocker!=null)friendWindowBlocker.SetActive(true);
                FocusWindow(target);
                SetTaskbarTaskMinimized(target,false);
                return true;
            }
            if(target==FrontendPage.Diary&&diaryWindowRoot!=null)
            {
                if(diaryWindowRoot.gameObject.activeSelf&&!diaryWindowMinimized&&!diaryWindowMinimizing)
                {FocusWindow(target);return true;}
                diaryWindowMinimized=false;diaryWindowRestoring=true;diaryWindowMinimizing=false;
                diaryWindowFromTaskbar=true;
                diaryWindowStarted=Time.unscaledTime;
                diaryWindowRoot.gameObject.SetActive(true);
                if(diaryWindowBlocker!=null)diaryWindowBlocker.SetActive(true);
                FocusWindow(target);
                SetTaskbarTaskMinimized(target,false);
                return true;
            }
            if(target==FrontendPage.News&&newsWindowRoot!=null)
            {
                if(newsWindowRoot.gameObject.activeSelf&&!newsWindowMinimized&&!newsWindowMinimizing)
                {FocusWindow(target);return true;}
                newsWindowMinimized=false;newsWindowRestoring=true;newsWindowMinimizing=false;
                newsWindowFromTaskbar=true;
                newsWindowStarted=Time.unscaledTime;
                newsWindowRoot.gameObject.SetActive(true);
                if(newsWindowBlocker!=null)newsWindowBlocker.SetActive(true);
                FocusWindow(target);
                SetTaskbarTaskMinimized(target,false);
                return true;
            }
            if(target==FrontendPage.MusicPlayer&&musicWindowRoot!=null)
            {
                if(musicWindowRoot.gameObject.activeSelf&&!musicWindowMinimized&&!musicWindowMinimizing)
                {FocusWindow(target);return true;}
                musicWindowMinimized=false;musicWindowRestoring=true;musicWindowMinimizing=false;
                musicWindowFromTaskbar=true;musicWindowStarted=Time.unscaledTime;
                musicWindowRoot.gameObject.SetActive(true);
                FocusWindow(target);SetTaskbarTaskMinimized(target,false);
                return true;
            }
            return false;
        }
        void UpdateMainWindowMotion()
        {
            if(mainWindowRoot==null||mainWindowGroup==null)return;
            if(!mainWindowMinimizing&&!mainWindowRestoring)return;
            float duration=mainWindowRestoring?.28f:FriendsWindowDuration;
            float raw=Mathf.Clamp01((Time.unscaledTime-mainWindowMotionStarted)/duration);
            float t=Mathf.SmoothStep(0,1,raw);
            Vector2 fullPosition=mainWindowPosition,taskPosition=TaskbarSlotPosition(FrontendPage.Main);
            Vector2 fullSize=new Vector2(1386,742),taskSize=new Vector2(184,46);
            if(mainWindowMinimizing)
            {
                DesktopMotion.Window(mainWindowRoot,mainWindowGroup,mainWindowMotionStarted,raw,true,true,
                    fullPosition,fullSize,taskPosition,taskSize);
                if(raw>=1f)
                {
                    mainWindowMinimizing=false;mainWindowMinimized=true;
                    mainWindowRoot.gameObject.SetActive(false);
                    owner.FocusDesktopWindow(FrontmostWindow);
                }
            }
            else
            {
                DesktopMotion.Window(mainWindowRoot,mainWindowGroup,mainWindowMotionStarted,raw,false,true,
                    fullPosition,fullSize,taskPosition,taskSize);
                if(raw>=1f)mainWindowRestoring=false;
            }
            mainWindowGroup.interactable=!mainWindowMinimizing&&(!mainWindowRestoring||raw>.72f);
            mainWindowGroup.blocksRaycasts=mainWindowGroup.interactable;
        }
        void ToggleStartMenu()
        {
            if(startMenuPopup!=null)
            {
                if(!startMenuPopup.Closing)StartPopup(startMenuPopup,true);
                return;
            }
            startMenuPopup=CreatePopup(page,"Start menu",28,444,346,376,
                new Vector2(28,834),new Vector2(144,46),()=>ClearPopup(ref startMenuPopup));
            startMenuPopup.SlideFromTaskbar=true;
            startMenuPopup.ScrimAlpha=0;
            var dismissStart=startMenuPopup.Scrim.gameObject.AddComponent<Button>();
            dismissStart.transition=Selectable.Transition.None;
            dismissStart.onClick.AddListener(CloseStartMenu);
            var menu=Panel(startMenuPopup.Root,"Start menu",0,0,346,376,DesktopWindowSurface,CyberTheme.Cyan);
            menu.rounded=true;menu.cornerRadius=12;
            menu.raycastTarget=true;
            Box(menu.rectTransform,"Start menu rail",0,0,346,6,CyberTheme.Cyan);
            Mono(menu.rectTransform,"Start eyebrow","RHYTHM HACKER  /  LOCAL",20,12,260,17,10,CyberTheme.Purple);
            Label(menu.rectTransform,"Start heading","信号控制台",20,29,258,29,20,true,DesktopWindowText,TextAnchor.MiddleLeft).font=UiChineseFont;
            var closeStart=ActionButton(menu.rectTransform,"Close start menu","X",292,12,38,38,CloseStartMenu,false,DesktopWindowAlt,16);
            SetButtonCaptionColor(closeStart,DesktopWindowText);
            RoundIcon(closeStart,10);
            CreateStartMenuItem(menu.rectTransform,"Open music from start","音乐播放器",DesktopIconKind.Music,58,
                ()=>{CloseStartMenu();owner.ShowMusicPlayer();},new Color(.18f,.65f,.82f));
            CreateStartMenuItem(menu.rectTransform,"Open background menu","背景切换",DesktopIconKind.Background,112,
                ()=>{CloseStartMenu();ToggleBackgroundMenu();},CyberTheme.Purple);
            CreateStartMenuItem(menu.rectTransform,"Open settings","操作说明",DesktopIconKind.Help,166,
                ()=>{CloseStartMenu();ShowControls();},new Color(.94f,.52f,.25f));
            CreateStartMenuItem(menu.rectTransform,"Return to title","回到标题",DesktopIconKind.Home,220,
                ()=>{CloseStartMenu();ShowReturnToTitleConfirmation();},CyberTheme.Pink);
            Box(menu.rectTransform,"Start footer divider",20,299,306,1,new Color(.45f,.33f,.72f,.25f));
            Label(menu.rectTransform,"Start footer","桌面设置与导航集中于此",20,313,306,20,11,false,
                DesktopWindowMuted,TextAnchor.MiddleLeft).font=UiChineseFont;
            StartPopup(startMenuPopup,false);
        }
        void CreateStartMenuItem(RectTransform parent,string buttonName,string caption,DesktopIconKind kind,float y,
            Action callback,Color accent)
        {
            var button=ActionButton(parent,buttonName,"",18,y,310,48,callback,false,DesktopWindowAlt,15);
            var panel=(CyberPanelGraphic)button.targetGraphic;
            panel.cut=8;panel.accent=accent;
            var icon=Rect("Start icon",panel.rectTransform,12,8,32,32).gameObject.AddComponent<CyberDesktopIconGraphic>();
            icon.kind=kind;icon.iconColor=accent;icon.raycastTarget=false;
            Label(panel.rectTransform,"Start item label",caption,58,3,230,24,15,true,DesktopWindowText,TextAnchor.MiddleLeft).font=UiChineseFont;
            string hint=kind==DesktopIconKind.Music?"切换音乐与调节音量":kind==DesktopIconKind.Background?"更换舞台画面":
                kind==DesktopIconKind.Help?"查看操作与按键":"离开当前桌面";
            Label(panel.rectTransform,"Start item hint",hint,59,27,230,16,10,false,DesktopWindowMuted).font=UiChineseFont;
        }
        Button CreateWindowControl(RectTransform parent,string buttonName,string glyph,float x,float width,
            Action callback,Color background)
        {
            var button=ActionButton(parent,buttonName,"",x,10,width,32,callback,false,background,16);
            var panel=(CyberPanelGraphic)button.targetGraphic;
            panel.cut=6;panel.accent=CyberTheme.Purple;
            Label(panel.rectTransform,"Control glyph",glyph,0,0,width,32,16,true,DesktopWindowText,TextAnchor.MiddleCenter);
            return button;
        }
        void CloseStartMenu()
        {
            if(startMenuPopup!=null&&!startMenuPopup.Closing)StartPopup(startMenuPopup,true);
        }
        public void ShowMusicPlayer()
        {
            ClearMusicPlayerWindow();
            SetPageAlpha(1);
            const float width=840,height=650;
            musicWindowRoot=Rect("Music player window",artboard,musicWindowPosition.x,-musicWindowPosition.y,width,height);
            musicWindowRoot.gameObject.AddComponent<RectMask2D>();
            musicWindowGroup=musicWindowRoot.gameObject.AddComponent<CanvasGroup>();
            Box(musicWindowRoot,"Music window background",0,0,width,height,DesktopWindowSurface);
            var frame=Panel(musicWindowRoot,"Music window frame",0,0,width,height,new Color(0,0,0,0),CyberTheme.Cyan);
            frame.raycastTarget=true;
            var titleBar=Panel(musicWindowRoot,"Music title bar",0,0,width,58,DesktopWindowHeader,new Color(.91f,.72f,1f));
            titleBar.raycastTarget=true;
            AddWindowFocus(musicWindowRoot,FrontendPage.MusicPlayer);
            AddWindowDrag(titleBar.rectTransform,musicWindowRoot,
                ()=>!musicWindowMinimizing&&!musicWindowRestoring&&!musicWindowClosing,
                position=>musicWindowPosition=position,()=>FocusWindow(FrontendPage.MusicPlayer));
            Box(titleBar.rectTransform,"Music title rail",0,0,width,5,CyberTheme.Cyan);
            var windowIcon=Rect("Music window icon",titleBar.rectTransform,20,12,34,34).gameObject.AddComponent<CyberDesktopIconGraphic>();
            windowIcon.kind=DesktopIconKind.Music;windowIcon.iconColor=new Color(.96f,.94f,1f);windowIcon.raycastTarget=false;
            Mono(titleBar.rectTransform,"Music window name","MUSIC PLAYER  /  DESKTOP AUDIO",62,19,560,22,14,new Color(.98f,.97f,1f));
            CreateWindowControl(titleBar.rectTransform,"Minimize music player","-",742,40,MinimizeCurrentWindow,DesktopWindowAlt);
            CreateWindowControl(titleBar.rectTransform,"Close music player","X",792,40,owner.CloseMusicPlayer,DesktopWindowAlt);

            Mono(musicWindowRoot,"Music eyebrow","04  /  NOW PLAYING",30,78,320,22,13,CyberTheme.Purple);
            var art=Panel(musicWindowRoot,"Music album artwork",28,108,224,224,DesktopWindowAlt,CyberTheme.Cyan);
            Box(art.rectTransform,"Music artwork inset",14,14,196,196,new Color(.83f,.78f,.97f,.96f));
            var cover=Rect("Music album icon",art.rectTransform,30,26,164,164).gameObject.AddComponent<PixelDesktopIconGraphic>();
            cover.kind=DesktopIconKind.Music;cover.showShortcutArrow=false;cover.raycastTarget=false;
            var current=Panel(musicWindowRoot,"Music now playing",272,108,540,224,new Color(.95f,.93f,1f,.98f),CyberTheme.Purple);
            Mono(current.rectTransform,"Now playing tag","[ DESKTOP PLAYLIST / "+owner.MusicTrackCount.ToString("D2")+" TRACKS ]",25,20,465,23,12,CyberTheme.Purple);
            musicNowTitle=Label(current.rectTransform,"Current music title","",25,62,480,56,33,true,DesktopWindowText,TextAnchor.MiddleLeft);
            musicNowNumber=Mono(current.rectTransform,"Current music number","",26,122,475,24,13,DesktopWindowMuted);
            Box(current.rectTransform,"Playback track",26,174,485,8,new Color(.78f,.75f,.89f,.92f));
            musicProgressFill=Box(current.rectTransform,"Playback position",26,174,0,8,CyberTheme.Cyan);
            musicElapsed=Mono(current.rectTransform,"Elapsed","00:00",26,190,100,20,12,DesktopWindowMuted);
            musicDuration=Mono(current.rectTransform,"Duration","00:00",405,190,106,20,12,DesktopWindowMuted,TextAnchor.MiddleRight);

            Mono(musicWindowRoot,"Music library tag","PLAYLIST  /  SELECT TO CHANGE BGM",30,351,500,20,12,CyberTheme.Purple);
            for(int i=0;i<owner.MusicTrackCount;i++)
            {
                int index=i;
                var button=ActionButton(musicWindowRoot,"Select music track "+i,"",28,378+i*39,784,35,
                    ()=>SelectMusicPlayerTrack(index),false,DesktopWindowAlt,14);
                button.interactable=owner.MusicTrackAvailable(i);
                var row=(CyberPanelGraphic)button.targetGraphic;row.cut=7;
                musicTrackPanels.Add(row);
                Mono(row.rectTransform,"Track index","0"+(i+1),15,7,60,22,14,CyberTheme.Purple);
                Label(row.rectTransform,"Track title",owner.MusicTrackTitle(i),85,4,530,27,17,true,DesktopWindowText,TextAnchor.MiddleLeft);
                var state=Mono(row.rectTransform,"Track state","",636,6,130,22,11,CyberTheme.Purple,TextAnchor.MiddleRight);
                musicTrackStates.Add(state);
            }
            Mono(musicWindowRoot,"Volume label","BACKGROUND VOLUME",30,582,420,20,12,CyberTheme.Purple);
            var volumeValue=Mono(musicWindowRoot,"Volume value","",686,582,126,20,13,DesktopWindowText,TextAnchor.MiddleRight);
            volumeValue.text=Mathf.RoundToInt(owner.MenuBgmVolume*100).ToString("D3")+"%";
            var slider=Rect("Music volume slider",musicWindowRoot,30,610,782,30).gameObject.AddComponent<Slider>();
            musicVolumeSlider=slider;
            var hit=slider.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            slider.transition=Selectable.Transition.None;slider.direction=Slider.Direction.LeftToRight;
            slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;
            Box(slider.transform as RectTransform,"Music volume track",0,11,782,8,new Color(.76f,.74f,.87f,.96f));
            var fill=Box(slider.transform as RectTransform,"Music volume fill",0,11,0,8,CyberTheme.Cyan);
            var handle=Box(slider.transform as RectTransform,"Music volume handle",0,1,16,28,CyberTheme.Purple);
            var driver=Box(slider.transform as RectTransform,"Music volume slider driver",0,0,782,30,Color.clear);
            slider.fillRect=driver.rectTransform;slider.targetGraphic=hit;
            Action<float> redrawVolume=value=>{
                fill.rectTransform.sizeDelta=new Vector2(782*Mathf.Clamp01(value),8);
                handle.rectTransform.anchoredPosition=new Vector2(Mathf.Clamp(782*value-8,0,766),-1);
            };
            slider.value=owner.MenuBgmVolume;
            redrawVolume(slider.value);
            slider.onValueChanged.AddListener(value=>{
                owner.SetMenuBgmVolume(value);
                volumeValue.text=Mathf.RoundToInt(value*100).ToString("D3")+"%";
                redrawVolume(value);
            });
            RefreshMusicPlayerSelection();
            EnsureTaskbarTask(FrontendPage.MusicPlayer,"音乐播放器  /  MUSIC");
            musicWindowMinimized=false;musicWindowMinimizing=false;musicWindowClosing=false;
            musicWindowRestoring=true;musicWindowFromTaskbar=false;musicWindowStarted=Time.unscaledTime;
            musicWindowRoot.localScale=Vector3.one*.78f;musicWindowGroup.alpha=0;
            FocusWindow(FrontendPage.MusicPlayer);
        }
        void SelectMusicPlayerTrack(int index)
        {
            if(owner.SelectMusicTrack(index))RefreshMusicPlayerSelection();
        }
        public bool SetMusicPlayerVolumeForTest(float value)
        {
            if(musicVolumeSlider==null)return false;
            musicVolumeSlider.value=value;
            return Mathf.Abs(owner.MenuBgmVolume-value)<.001f;
        }
        void RefreshMusicPlayerSelection()
        {
            int selected=owner.SelectedMusicTrack;
            if(musicNowTitle!=null)musicNowTitle.text=owner.MusicTrackTitle(selected);
            if(musicNowNumber!=null)musicNowNumber.text="TRACK  0"+(selected+1)+"  /  "+(selected==4?"Erik Satie · Mark Allaway":selected==0?"PRESET BGM":"LOCAL COLLECTION");
            for(int i=0;i<musicTrackPanels.Count;i++)
            {
                if(musicTrackPanels[i]!=null)musicTrackPanels[i].accent=i==selected?CyberTheme.Cyan:CyberTheme.Purple;
                if(musicTrackStates[i]!=null)musicTrackStates[i].text=i==selected?"PLAYING":"SELECT";
            }
        }
        void UpdateMusicPlayerWindow()
        {
            if(musicWindowRoot==null)return;
            float length=owner.MusicDurationSeconds,time=owner.MusicPlaybackSeconds;
            if(musicProgressFill!=null)musicProgressFill.rectTransform.sizeDelta=new Vector2(length>0?485*Mathf.Clamp01(time/length):0,8);
            if(musicElapsed!=null)musicElapsed.text=FormatTime(time);
            if(musicDuration!=null)musicDuration.text=FormatTime(length);
            if(!musicWindowMinimizing&&!musicWindowRestoring&&!musicWindowClosing)return;
            float raw=Mathf.Clamp01((Time.unscaledTime-musicWindowStarted)/FriendsWindowDuration);
            float t=Mathf.SmoothStep(0,1,raw);
            Vector2 fullPosition=musicWindowPosition,fullSize=new Vector2(840,650);
            Vector2 targetPosition=musicWindowClosing?new Vector2(22,-450):TaskbarSlotPosition(FrontendPage.MusicPlayer);
            Vector2 targetSize=musicWindowClosing?new Vector2(130,118):new Vector2(184,46);
            if(musicWindowMinimizing||musicWindowClosing)
            {
                DesktopMotion.Window(musicWindowRoot,musicWindowGroup,musicWindowStarted,raw,true,true,
                    fullPosition,fullSize,targetPosition,targetSize);
                if(raw>=1)
                {
                    if(musicWindowClosing){ClearMusicPlayerWindow();return;}
                    else
                    {
                        musicWindowMinimizing=false;musicWindowMinimized=true;
                        musicWindowRoot.gameObject.SetActive(false);
                        owner.FocusDesktopWindow(FrontmostWindow);
                    }
                }
            }
            else if(musicWindowRestoring)
            {
                DesktopMotion.Window(musicWindowRoot,musicWindowGroup,musicWindowStarted,raw,false,musicWindowFromTaskbar,
                    fullPosition,fullSize,targetPosition,targetSize);
                if(raw>=1){musicWindowRestoring=false;musicWindowFromTaskbar=false;}
            }
            musicWindowGroup.interactable=!musicWindowMinimizing&&!musicWindowClosing&&(!musicWindowRestoring||raw>.72f);
            musicWindowGroup.blocksRaycasts=musicWindowGroup.interactable;
        }
        public void CloseMusicPlayer()
        {
            if(musicWindowRoot==null||musicWindowClosing)return;
            musicWindowClosing=true;musicWindowMinimizing=false;musicWindowRestoring=false;
            musicWindowStarted=Time.unscaledTime;
            owner.FocusDesktopWindow(FrontmostWindow);
        }
        void ClearMusicPlayerWindow()
        {
            buttons.Remove("Minimize music player");buttons.Remove("Close music player");
            for(int i=0;i<4;i++)buttons.Remove("Select music track "+i);
            if(musicWindowRoot!=null){musicWindowRoot.gameObject.SetActive(false);UnityEngine.Object.Destroy(musicWindowRoot.gameObject);musicWindowRoot=null;}
            musicWindowGroup=null;musicNowTitle=null;musicNowNumber=null;musicElapsed=null;musicDuration=null;musicProgressFill=null;musicVolumeSlider=null;
            musicTrackPanels.Clear();musicTrackStates.Clear();
            musicWindowMinimized=false;musicWindowMinimizing=false;musicWindowRestoring=false;musicWindowClosing=false;
            musicWindowFromTaskbar=false;
            RemoveTaskbarTask(FrontendPage.MusicPlayer);
        }
        public void ShowFriends()
        {
            ClearFriendWindow();
            SetPageAlpha(1);
            friendWindowBlocker=Rect("Friend window input blocker",artboard,0,0,1600,900).gameObject;
            var blockerImage=friendWindowBlocker.AddComponent<Image>();
            blockerImage.color=new Color(.08f,.04f,.18f,.16f);blockerImage.raycastTarget=false;
            friendWindowRoot=Rect("Friend phone window",artboard,friendWindowPosition.x,-friendWindowPosition.y,AppWindowWidth,AppWindowHeight);
            friendWindowRoot.gameObject.AddComponent<RectMask2D>();
            friendWindowGroup=friendWindowRoot.gameObject.AddComponent<CanvasGroup>();
            Box(friendWindowRoot,"Friend window background",0,0,1180,720,DesktopWindowSurface);
            var frame=Panel(friendWindowRoot,"Friend window frame",0,0,1180,720,new Color(0,0,0,0),CyberTheme.Purple);
            frame.outline=true;frame.raycastTarget=true;
            var titleBar=Panel(friendWindowRoot,"Friend title bar",0,0,1180,64,DesktopWindowHeader,new Color(.91f,.72f,1f));
            friendWindowFocus=AddWindowFocus(friendWindowRoot,FrontendPage.Friends);
            titleBar.raycastTarget=true;
            AddWindowDrag(titleBar.rectTransform,friendWindowRoot,()=>!friendWindowMinimizing&&!friendWindowRestoring&&!friendWindowClosing,position=>friendWindowPosition=position,()=>FocusWindow(FrontendPage.Friends));
            titleBar.cut=12;
            Box(titleBar.rectTransform,"Friend cyan rail",0,0,1180,5,CyberTheme.Cyan);
            Box(titleBar.rectTransform,"Friend pink rail",0,5,1180,2,CyberTheme.Pink);
            Mono(titleBar.rectTransform,"Friend title","MESSENGER  /  CONTACTS",24,18,540,24,13,new Color(.98f,.97f,1f));
            var requestButton=ActionButton(titleBar.rectTransform,"Open friend requests","",1028,12,54,40,ToggleFriendRequests,false,
                DesktopWindowAlt,18);
            var requestPanel=(CyberPanelGraphic)requestButton.targetGraphic;requestPanel.cut=6;requestPanel.accent=CyberTheme.Pink;
            var requestIcon=Rect("Friend request icon",requestPanel.rectTransform,11,7,32,28).gameObject.AddComponent<FriendRequestIconGraphic>();
            requestIcon.color=CyberTheme.Purple;requestIcon.raycastTarget=false;
            friendRequestUnreadDot=Box(requestPanel.rectTransform,"Request unread dot",42,7,10,10,new Color(1f,.12f,.20f,1f));
            CreateWindowControl(titleBar.rectTransform,"Minimize friends","-",1078,44,MinimizeCurrentWindow,DesktopWindowAlt);
            var closeWindow=CreateWindowControl(titleBar.rectTransform,"Close friends window","X",1128,44,owner.ReturnFromFriends,DesktopWindowAlt);
            friendCloseButton=closeWindow;
            var closePanel=(CyberPanelGraphic)closeWindow.targetGraphic;closePanel.cut=6;closePanel.accent=CyberTheme.Pink;

            friendWindowContent=Rect("Friend window content",friendWindowRoot,0,0,1180,720).gameObject.AddComponent<CanvasGroup>();
            friendWindowContent.alpha=0;
            friendListLayer=Rect("Friend list",friendWindowContent.GetComponent<RectTransform>(),0,0,1180,720).gameObject;
            var list=friendListLayer.GetComponent<RectTransform>();
            Mono(list,"Friend eyebrow","[ CONTACTS / 01 ONLINE ]",42,92,520,24,13,CyberTheme.Purple);
            Label(list,"Friend heading","FRIENDS",38,126,500,60,42,true,DesktopWindowText);
            CreateFriendCard(list,210,"Open friend 0","Friends/tnelis","Tnelis",
                "在线  /  第09区中继","“Turi，之前要你查的资料查好了吗？”",OpenFriendChat,out tnelisUnreadMarker);
            tnelisUnreadMarker.gameObject.SetActive(tnelisUnreadCount>0);
            if(bakaFriendAccepted)
            {
                CreateFriendCard(list,414,"Open Baka chat","Friends/baka","Baka",
                    "好友  /  学校中继","“您好您好！您就是那位说要帮咱的大佬吗！”",OpenBakaChat,out bakaUnreadMarker);
                bakaUnreadMarker.gameObject.SetActive(bakaUnreadCount>0);
            }
            if(tnelisChatOpen)
            {
                friendListLayer.SetActive(false);
                BuildFriendChat();
            }
            friendWindowMinimized=false;
            EnsureTaskbarTask(FrontendPage.Friends,"朋友系统  /  FRIENDS");
            SetTaskbarTaskMinimized(FrontendPage.Friends,false);
            BeginFriendWindow(false);
            RefreshUnreadIndicators();
        }
        public void ShowDeviceHome()
        {
            if(page==null)return;
            ClearDiaryWindow();
            ClearDeviceWindow();
            SetPageAlpha(1);
            deviceWindowBlocker=Rect("Device window input blocker",artboard,0,0,1600,900).gameObject;
            var blockerImage=deviceWindowBlocker.AddComponent<Image>();
            blockerImage.color=new Color(0,0,0,.16f);blockerImage.raycastTarget=false;
            deviceWindowRoot=Rect("Device popup window",artboard,1474,730,92,78);
            deviceWindowRoot.gameObject.AddComponent<RectMask2D>();
            Box(deviceWindowRoot,"Device window background",0,0,1180,720,new Color(.012f,.02f,.045f,.99f));
            var frame=Panel(deviceWindowRoot,"Device window frame",0,0,1180,720,new Color(0,0,0,0),CyberTheme.Purple);
            frame.outline=true;frame.raycastTarget=true;
            var titleBar=Panel(deviceWindowRoot,"Device title bar",0,0,1180,64,new Color(.045f,.025f,.10f,.99f),CyberTheme.Purple);
            deviceWindowFocus=AddWindowFocus(deviceWindowRoot,FrontendPage.Device);
            titleBar.raycastTarget=true;
            AddWindowDrag(titleBar.rectTransform,deviceWindowRoot,
                ()=>!deviceWindowClosing&&Time.unscaledTime-deviceWindowStarted>=FriendsWindowDuration,
                position=>deviceWindowPosition=position,()=>FocusWindow(FrontendPage.Device));
            titleBar.cut=12;
            Box(titleBar.rectTransform,"Device cyan rail",0,0,1180,5,CyberTheme.Cyan);
            Box(titleBar.rectTransform,"Device pink rail",0,5,1180,2,CyberTheme.Pink);
            Mono(titleBar.rectTransform,"Device title","LOCAL DEVICE  /  APPLICATIONS",24,18,620,24,13,CyberTheme.Text);
            ActionButton(titleBar.rectTransform,"Close device","X",1114,12,48,40,owner.CloseDeviceHome,false,
                new Color(.10f,.025f,.13f,.98f),18);

            var content=Rect("Device content",deviceWindowRoot,0,0,1180,720);
            deviceWindowContent=content.gameObject.AddComponent<CanvasGroup>();
            Label(content,"Device heading","YOUR PHONE",42,92,560,56,40,true,CyberTheme.Text);
            Mono(content,"Device subtitle","CHOOSE AN APPLICATION TO CONTINUE",46,150,640,22,12,CyberTheme.Muted);

            var diary=ActionButton(content,"Open diary","",52,210,520,420,owner.OpenDiaryFromDevice,false,
                new Color(.08f,.055f,.16f,.98f),18);
            var diaryPanel=(CyberPanelGraphic)diary.targetGraphic;diaryPanel.cut=26;diaryPanel.accent=CyberTheme.Purple;
            var diaryMark=Panel(diaryPanel.rectTransform,"Diary mark",38,38,128,128,new Color(.45f,.26f,.75f,.98f),CyberTheme.Pink);
            diaryMark.cut=24;
            Label(diaryMark.rectTransform,"Diary monogram","D",0,0,128,128,64,true,Color.white,TextAnchor.MiddleCenter);
            Label(diaryPanel.rectTransform,"Diary title","DIARY",42,190,520,58,42,true,CyberTheme.Text);
            var diaryState=Label(diaryPanel.rectTransform,"Diary state",
                diaryWriteUnlocked?"过去的日记  /  可新建":"查看过去的日记",
                46,258,500,34,18,false,new Color(.72f,.62f,.94f,.92f));
            diaryState.font=UiChineseFont;
            Mono(diaryPanel.rectTransform,"Diary detail",diaryWriteUnlocked
                ?"PRIVATE ARCHIVE  /  READ + WRITE":"PRIVATE ARCHIVE  /  READ ONLY",44,330,510,24,12,CyberTheme.Muted);
            deviceDiaryUnreadDot=Box(diaryPanel.rectTransform,"Diary unread",548,32,18,18,new Color(1f,.12f,.20f,1f));
            deviceDiaryUnreadDot.gameObject.SetActive(diaryUnread);

            bool friendUnread=tnelisUnreadCount>0||bakaUnreadCount>0||(bakaRequestPending&&!bakaRequestDeclined);
            var friends=ActionButton(content,"Open friends from device","",608,210,520,420,owner.OpenFriendsFromDevice,false,
                new Color(.035f,.065f,.12f,.98f),18);
            var friendsPanel=(CyberPanelGraphic)friends.targetGraphic;friendsPanel.cut=26;friendsPanel.accent=CyberTheme.Cyan;
            var friendsMark=Panel(friendsPanel.rectTransform,"Friends mark",38,38,128,128,new Color(.05f,.30f,.36f,.98f),CyberTheme.Cyan);
            friendsMark.cut=24;
            Label(friendsMark.rectTransform,"Friends monogram","F",0,0,128,128,64,true,Color.white,TextAnchor.MiddleCenter);
            Label(friendsPanel.rectTransform,"Friends title","FRIENDS",42,190,520,58,42,true,CyberTheme.Text);
            var friendsState=Label(friendsPanel.rectTransform,"Friends state","聊天记录  /  点击进入",46,258,500,34,18,false,
                new Color(.55f,.83f,.90f,.92f));
            friendsState.font=UiChineseFont;
            Mono(friendsPanel.rectTransform,"Friends detail","CONTACTS  /  MESSAGE RELAY",44,330,510,24,12,CyberTheme.Muted);
            deviceFriendUnreadDot=Box(friendsPanel.rectTransform,"Friends unread",548,32,18,18,new Color(1f,.12f,.20f,1f));
            deviceFriendUnreadDot.gameObject.SetActive(friendUnread);

            Mono(content,"Device footer","DIARY UNLOCKS AS THE STORY ADVANCES  /  LOCAL STORAGE ONLY",52,660,1076,20,11,
                CyberTheme.Muted,TextAnchor.MiddleCenter);
            BeginDeviceWindow(false);
        }
        static readonly List<string> DiaryEntryTitles=new List<string>
        {
            "第一次听见脉冲",
            "城市里的回声",
            "写给 Baka 的旋律"
        };
        static readonly List<string> DiaryEntryBodies=new List<string>
        {
            "今天的雨很大，下城区的中继器一直在发出同一个频率。我原本以为那只是设备故障，直到第三次回声出现时，我才意识到有人在用节奏留下路线。",
            "我沿着旧线路走到第09区。墙上的蓝色记号已经褪色，但耳机里的脉冲仍然清晰。这里不像废弃区，更像是有人刻意保存下来的记忆。",
            "Baka说那段信号像她很久以前写下的歌。我没有告诉她，其实我也在旋律里听见了熟悉的东西。也许有些记录不是为了忘记，而是为了等某个人回来。"
        };
        public void ShowDiary()
        {
            if(page==null)return;
            ClearDiaryWindow();
            SetPageAlpha(1);
            diaryWindowBlocker=Rect("Diary window input blocker",artboard,0,0,1600,900).gameObject;
            var blockerImage=diaryWindowBlocker.AddComponent<Image>();
            blockerImage.color=new Color(.08f,.04f,.18f,.18f);blockerImage.raycastTarget=false;
            diaryWindowRoot=Rect("Diary popup window",artboard,diaryWindowPosition.x,-diaryWindowPosition.y,1180,720);
            diaryWindowRoot.gameObject.AddComponent<RectMask2D>();
            diaryWindowGroup=diaryWindowRoot.gameObject.AddComponent<CanvasGroup>();
            Box(diaryWindowRoot,"Diary window background",0,0,1180,720,DesktopWindowSurface);
            var frame=Panel(diaryWindowRoot,"Diary window frame",0,0,1180,720,new Color(0,0,0,0),CyberTheme.Purple);
            frame.outline=true;frame.raycastTarget=true;
            var titleBar=Panel(diaryWindowRoot,"Diary title bar",0,0,1180,64,DesktopWindowHeader,new Color(.91f,.72f,1f));
            diaryWindowFocus=AddWindowFocus(diaryWindowRoot,FrontendPage.Diary);
            titleBar.raycastTarget=true;
            AddWindowDrag(titleBar.rectTransform,diaryWindowRoot,()=>!diaryWindowMinimizing&&!diaryWindowRestoring&&!diaryWindowClosing,position=>diaryWindowPosition=position,()=>FocusWindow(FrontendPage.Diary));
            titleBar.cut=12;
            Box(titleBar.rectTransform,"Diary rail",0,0,1180,5,CyberTheme.Purple);
            Mono(titleBar.rectTransform,"Diary label","PRIVATE DIARY  /  ARCHIVE",24,18,620,24,13,new Color(.98f,.97f,1f));
            CreateWindowControl(titleBar.rectTransform,"Minimize diary","-",948,50,MinimizeCurrentWindow,DesktopWindowAlt);
            ActionButton(titleBar.rectTransform,"Back to device",owner.DiaryOpenedFromDevice?"<  DEVICE":"<  MAIN",1008,12,144,40,owner.CloseDiaryToDevice,false,
                DesktopWindowAlt,14);
            SetButtonCaptionColor(buttons["Back to device"],DesktopWindowText);
            var content=Rect("Diary content",diaryWindowRoot,0,0,1180,720);
            diaryWindowContent=content.gameObject.AddComponent<CanvasGroup>();
            var list=Panel(content,"Diary entries",28,92,310,596,DesktopWindowAlt,CyberTheme.Purple);
            Mono(list.rectTransform,"Diary list label","PAST ENTRIES  /  03",24,22,260,22,12,CyberTheme.Purple);
            for(int i=0;i<DiaryEntryTitles.Count;i++)
            {
                int entry=i;
                var entryButton=ActionButton(list.rectTransform,"Open diary entry "+i,DiaryEntryTitles[i],20,62+i*112,270,88,
                    ()=>SelectDiaryEntry(entry),false,DesktopWindowSurface,18);
                SetButtonCaptionColor(entryButton,DesktopWindowText);
            }
            if(diaryWriteUnlocked)
            {
                var newDiary=ActionButton(list.rectTransform,"New diary","新建日记",20,62+DiaryEntryTitles.Count*112,270,88,
                    BeginDiaryWriting,false,DesktopWindowSurface,18);
                SetButtonCaptionColor(newDiary,DesktopWindowText);
                if(diaryUnread)Box(newDiary.transform as RectTransform,"New diary unread",282,12,14,14,
                    new Color(1f,.12f,.20f,1f));
            }
            var paper=Panel(content,"Diary reader",356,92,796,596,new Color(.93f,.91f,.98f,.99f),CyberTheme.Purple);
            paper.cut=14;
            diaryDetailTitle=Label(paper.rectTransform,"Diary detail title","",32,28,720,52,28,true,
                new Color(.10f,.08f,.22f,.98f));
            diaryDetailTitle.font=UiChineseFont;
            diaryDetailBody=Label(paper.rectTransform,"Diary detail body","",34,102,724,420,21,false,
                new Color(.14f,.12f,.28f,.96f));
            diaryDetailBody.font=UiChineseFont;diaryDetailBody.lineSpacing=1.14f;
            diaryEditorText=Label(paper.rectTransform,"Diary editor body","",34,102,724,420,21,false,
                new Color(.10f,.08f,.22f,.98f));
            diaryEditorText.font=UiChineseFont;diaryEditorText.lineSpacing=1.14f;
            diaryEditorText.gameObject.SetActive(false);
            diaryEditorHint=Label(paper.rectTransform,"Diary editor hint","按任意键逐字书写",34,536,500,28,15,false,
                CyberTheme.Purple);
            diaryEditorHint.font=UiChineseFont;diaryEditorHint.gameObject.SetActive(false);
            diarySaveButton=ActionButton(paper.rectTransform,"Save diary","SAVE DIARY",610,518,150,54,SaveDiary,true,
                CyberTheme.Purple,17);
            diarySaveButton.gameObject.SetActive(false);
            SelectDiaryEntry(0);
            diaryWindowMinimized=false;
            diaryWindowRestoring=true;diaryWindowMinimizing=false;diaryWindowStarted=Time.unscaledTime;
            if(diaryWindowGroup!=null)diaryWindowGroup.alpha=0;
            diaryWindowRoot.localScale=Vector3.one*.78f;
            EnsureTaskbarTask(FrontendPage.Diary,"日记本  /  DIARY");
            SetTaskbarTaskMinimized(FrontendPage.Diary,false);
            diaryWindowStarted=Time.unscaledTime;
            diaryWindowRoot.SetAsLastSibling();
        }
        public void ShowMonologue(string speakerName,string body,Texture2D portrait,Color accent)
        {
            ShowMonologueSequence(new[]{new MonologuePage(speakerName,body,portrait,accent)});
        }
        Action<int> monologuePageShown;
        bool outdoorMonologue;
        public bool MonologueDimsBackground=>diaryPromptRoot!=null&&diaryPromptRoot.GetComponent<Image>().color.a>0;
        public void ShowMonologueSequence(IEnumerable<MonologuePage> entries,float delay=0f,Action onComplete=null,
            bool dimBackground=true,Action<int> onPageShown=null)
        {
            if(page==null||diaryPromptRoot!=null)return;
            monologuePages.Clear();
            if(entries!=null)
                foreach(var entry in entries)if(entry!=null)monologuePages.Add(entry);
            if(monologuePages.Count==0)return;
            monologueCompleted=onComplete;
            outdoorMonologue=!dimBackground;
            monologuePageShown=onPageShown;
            monologuePageIndex=0;
            diaryPromptRoot=Rect("Monologue overlay",artboard,0,0,1600,900).gameObject;
            var shade=diaryPromptRoot.AddComponent<Image>();
            shade.color=dimBackground?new Color(.08f,.04f,.18f,.38f):Color.clear;shade.raycastTarget=true;
            diaryPromptStarted=Time.unscaledTime+Mathf.Max(0f,delay);
            ShowCurrentMonologuePage();
            diaryPromptGroup=diaryPromptRoot.AddComponent<CanvasGroup>();
            diaryPromptGroup.alpha=0;
            monologueAdvanceAfter=diaryPromptStarted+.35f;
            diaryPromptRoot.transform.SetAsLastSibling();
        }
        void ShowCurrentMonologuePage()
        {
            if(monologuePanel!=null)
            {
                monologuePanel.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(monologuePanel.gameObject);
            }
            var entry=monologuePages[monologuePageIndex];
            monologuePageShown?.Invoke(monologuePageIndex);
            monologuePanel=BuildMonologuePanel(diaryPromptRoot.transform as RectTransform,
                entry.SpeakerName,entry.Portrait,entry.Accent);
            monologueFullText=entry.Text??"";
            monologueVisibleCount=0;
            monologueText.text="";
            monologueNextCharAt=Mathf.Max(Time.unscaledTime,diaryPromptStarted)+.04f;
            monologueHint.text=monologueFullText.Length==0?"点击 / 按任意键继续":"点击 / 按任意键显示全文";
            monologueArrow.gameObject.SetActive(monologueFullText.Length==0);
        }
        CyberPanelGraphic BuildMonologuePanel(RectTransform parent,string speakerName,Texture2D portrait,Color accent)
        {
            var panel=Panel(parent,"Monologue dialogue",150,outdoorMonologue?680:582,1300,outdoorMonologue?196:278,DesktopWindowSurface,accent);
            Box(panel.rectTransform,"Monologue top rail",0,0,1300,5,accent);
            if(portrait!=null)
            {
                float portraitSize=outdoorMonologue?152:188;
                Box(panel.rectTransform,"Monologue portrait frame",48,12-portraitSize,portraitSize,portraitSize,accent);
                Box(panel.rectTransform,"Monologue portrait background",52,16-portraitSize,portraitSize-8,portraitSize-8,DesktopWindowSurface);
                var portraitRect=Rect("Monologue portrait",panel.rectTransform,54,18-portraitSize,portraitSize-12,portraitSize-12);
                var image=portraitRect.gameObject.AddComponent<RawImage>();
                image.texture=portrait;
                image.uvRect=new Rect(.10f,.18f,.80f,.80f);
                image.color=Color.white;
                image.raycastTarget=false;
            }
            var name=Label(panel.rectTransform,"Monologue speaker",speakerName,52,outdoorMonologue?12:24,500,38,24,true,accent);
            name.font=UiChineseFont;
            Box(panel.rectTransform,"Monologue name underline",52,outdoorMonologue?50:62,174,3,accent);
            monologueText=Label(panel.rectTransform,"Monologue text","",52,outdoorMonologue?64:82,1172,outdoorMonologue?82:154,22,false,DesktopWindowText);
            monologueText.font=UiChineseFont;
            monologueText.lineSpacing=1.14f;
            monologueHint=Label(panel.rectTransform,"Monologue continue hint","",52,outdoorMonologue?161:244,1100,25,15,false,
                DesktopWindowMuted);
            monologueHint.font=UiChineseFont;
            monologueArrow=Mono(panel.rectTransform,"Monologue continue arrow","▶",1220,outdoorMonologue?145:226,44,40,24,accent,
                TextAnchor.MiddleCenter);
            return panel;
        }
        void DismissDiaryPrompt()
        {
            if(diaryPromptRoot!=null)
            {
                diaryPromptRoot.SetActive(false);
                UnityEngine.Object.Destroy(diaryPromptRoot);
            }
            diaryPromptRoot=null;diaryPromptGroup=null;monologuePanel=null;
            monologueText=null;monologueHint=null;monologueArrow=null;monologueFullText="";
            monologuePages.Clear();monologuePageIndex=0;
            monologueCompleted=null;
            monologuePageShown=null;
        }
        void UpdateDiaryPrompt()
        {
            if(diaryPromptGroup==null)return;
            diaryPromptRoot.transform.SetAsLastSibling();
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-diaryPromptStarted)/.32f));
            diaryPromptGroup.alpha=t;
            if(Time.unscaledTime<diaryPromptStarted)return;
            while(monologueText!=null&&monologueVisibleCount<monologueFullText.Length&&
                Time.unscaledTime>=monologueNextCharAt)
            {
                char next=monologueFullText[monologueVisibleCount++];
                monologueText.text=monologueFullText.Substring(0,monologueVisibleCount);
                float pause=.035f;
                if(next=='。'||next=='！'||next=='？')pause=.15f;
                else if(next=='，'||next=='、')pause=.08f;
                monologueNextCharAt+=pause;
            }
            if(monologueText!=null&&monologueVisibleCount>=monologueFullText.Length&&
                monologueArrow!=null&&!monologueArrow.gameObject.activeSelf)
            {
                monologueHint.text="点击 / 按任意键继续";
                monologueArrow.gameObject.SetActive(true);
            }
            bool dismissInput=Input.anyKeyDown||Input.GetMouseButtonDown(0);
            if(dismissInput)AdvanceMonologue();
        }
        public void AdvanceMonologue()
        {
            if(diaryPromptGroup!=null&&Time.unscaledTime>=monologueAdvanceAfter)
            {
                if(monologueVisibleCount<monologueFullText.Length)
                {
                    monologueVisibleCount=monologueFullText.Length;
                    monologueText.text=monologueFullText;
                    monologueHint.text="点击 / 按任意键继续";
                    monologueArrow.gameObject.SetActive(true);
                    monologueAdvanceAfter=Time.unscaledTime+.18f;
                    return;
                }
                if(monologuePageIndex+1<monologuePages.Count)
                {
                    monologuePageIndex++;
                    ShowCurrentMonologuePage();
                    monologueAdvanceAfter=Time.unscaledTime+.18f;
                }
                else
                {
                    Action completed=monologueCompleted;
                    diaryPromptDismissedAt=Time.unscaledTime;
                    DismissDiaryPrompt();
                    completed?.Invoke();
                }
            }
        }
        void BeginDeviceWindow(bool closing)
        {
            if(deviceWindowRoot==null)return;
            deviceWindowClosing=closing;
            deviceWindowStarted=Time.unscaledTime;

        }
        void UpdateDeviceWindow()
        {
            if(deviceWindowRoot==null)return;
            float raw=Mathf.Clamp01((Time.unscaledTime-deviceWindowStarted)/FriendsWindowDuration);
            float t=Mathf.SmoothStep(0,1,raw);
            Vector2 iconPosition=new Vector2(1474,-730),iconSize=new Vector2(92,78);
            Vector2 finalPosition=deviceWindowPosition,finalSize=new Vector2(1180,720);
            var group=DesktopMotion.GetOrAdd<CanvasGroup>(deviceWindowRoot.gameObject);
            DesktopMotion.Window(deviceWindowRoot,group,deviceWindowStarted,raw,deviceWindowClosing,true,
                finalPosition,finalSize,iconPosition,iconSize);
            if(deviceWindowContent!=null)
            {
                float contentT=deviceWindowClosing
                    ?1-Mathf.SmoothStep(0,1,Mathf.Clamp01((raw-.08f)/.5f))
                    :Mathf.SmoothStep(0,1,Mathf.Clamp01((raw-.24f)/.76f));
                deviceWindowContent.alpha=contentT;
                deviceWindowContent.interactable=contentT>.85f;
                deviceWindowContent.blocksRaycasts=contentT>.85f;
            }
            if(deviceWindowClosing&&raw>=1)ClearDeviceWindow();
        }
        public void CloseDeviceHome()
        {
            if(deviceWindowRoot==null)return;
            BeginDeviceWindow(true);
        }
        void ClearDeviceWindow()
        {
            buttons.Remove("Close device");
            buttons.Remove("Open diary");
            buttons.Remove("Open friends from device");
            if(deviceWindowRoot!=null){deviceWindowRoot.gameObject.SetActive(false);UnityEngine.Object.Destroy(deviceWindowRoot.gameObject);deviceWindowRoot=null;}
            if(deviceWindowBlocker!=null){deviceWindowBlocker.SetActive(false);UnityEngine.Object.Destroy(deviceWindowBlocker);deviceWindowBlocker=null;}
            deviceWindowContent=null;deviceWindowClosing=false;
            deviceDiaryUnreadDot=null;deviceFriendUnreadDot=null;
        }
        void SelectDiaryEntry(int index)
        {
            if(DiaryEntryTitles.Count==0)return;
            diarySelectedEntry=Mathf.Clamp(index,0,DiaryEntryTitles.Count-1);
            diaryWriting=false;
            if(diaryDetailTitle!=null)diaryDetailTitle.text=DiaryEntryTitles[diarySelectedEntry];
            if(diaryDetailBody!=null)diaryDetailBody.text=DiaryEntryBodies[diarySelectedEntry];
            if(diaryDetailBody!=null)diaryDetailBody.gameObject.SetActive(true);
            if(diaryEditorText!=null)diaryEditorText.gameObject.SetActive(false);
            if(diaryEditorHint!=null)diaryEditorHint.gameObject.SetActive(false);
            if(diarySaveButton!=null)diarySaveButton.gameObject.SetActive(false);
            if(diaryDetailBody!=null)
                DesktopMotion.GetOrAdd<DesktopEntranceMotion>(diaryDetailBody.gameObject).Play(0,.24f,6);
        }
        const string NewDiaryEntryText="今天，我终于听完了 Baka 留下的旋律。那段信号并不危险，它更像是一封写给过去的信。我会继续追查第09区，也不会忘记那些没有留下名字的人。";
        void BeginDiaryWriting()
        {
            if(!diaryWriteUnlocked)return;
            diaryWriting=true;
            diaryFullText=NewDiaryEntryText;
            BuildTypingFrames(diaryFullText,diaryTypingFrames);
            diaryFrameIndex=0;diaryVisibleText="";
            if(diaryDetailTitle!=null)diaryDetailTitle.text="新的日记";
            if(diaryDetailBody!=null)diaryDetailBody.gameObject.SetActive(false);
            if(diaryEditorText!=null){diaryEditorText.gameObject.SetActive(true);diaryEditorText.text="";}
            if(diaryEditorHint!=null){diaryEditorHint.gameObject.SetActive(true);diaryEditorHint.text="按任意键逐字书写";}
            if(diarySaveButton!=null){diarySaveButton.gameObject.SetActive(true);diarySaveButton.interactable=false;}
        }
        void UpdateDiaryInput()
        {
            if(owner.Page!=FrontendPage.Diary||!diaryWriting||diaryEditorText==null)return;
            bool enter=Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter);
            if(enter)
            {
                if(diaryFrameIndex>=diaryTypingFrames.Count)SaveDiary();
                return;
            }
            if(Input.GetKeyDown(KeyCode.Escape))return;
            bool revealInput=Input.anyKeyDown||Input.GetMouseButtonDown(0);
            if(!revealInput||diaryFrameIndex>=diaryTypingFrames.Count)return;
            diaryVisibleText=diaryTypingFrames[diaryFrameIndex];
            diaryFrameIndex++;
            owner.PlayKeyboardKeySound();
            diaryEditorText.text=diaryVisibleText;
            bool ready=diaryFrameIndex>=diaryTypingFrames.Count;
            if(diaryEditorHint!=null)diaryEditorHint.text=ready?"ENTER 保存":diaryFrameIndex+"/"+diaryTypingFrames.Count;
            if(diarySaveButton!=null)diarySaveButton.interactable=ready;
        }
        void SaveDiary()
        {
            if(!diaryWriting||diaryFrameIndex<diaryTypingFrames.Count)return;
            owner.PlayUiCue("save");
            string title="第"+(DiaryEntryTitles.Count+1).ToString("D2")+"篇 / 新的记录";
            DiaryEntryTitles.Add(title);
            DiaryEntryBodies.Add(diaryVisibleText);
            diaryWriting=false;
            diaryWriteUnlocked=false;
            diaryUnread=false;
            RefreshUnreadIndicators();
            ShowDiary();
            SelectDiaryEntry(DiaryEntryTitles.Count-1);
        }
        void UpdateDiaryWindow()
        {
            if(diaryWindowRoot==null||diaryWindowContent==null)return;
            float duration=diaryWindowRestoring?.28f:FriendsWindowDuration;
            float raw=Mathf.Clamp01((Time.unscaledTime-diaryWindowStarted)/duration);
            float t=Mathf.SmoothStep(0,1,raw);
            Vector2 fullPosition=diaryWindowPosition,fullSize=new Vector2(1180,720);
            Vector2 taskPosition=TaskbarSlotPosition(FrontendPage.Diary),taskSize=new Vector2(184,46);
            if(diaryWindowClosing||diaryWindowMinimizing)
            {
                DesktopMotion.Window(diaryWindowRoot,diaryWindowGroup,diaryWindowStarted,raw,true,true,
                    fullPosition,fullSize,taskPosition,taskSize);
                if(raw>=1f&&diaryWindowMinimizing)
                {
                    diaryWindowMinimizing=false;diaryWindowMinimized=true;
                    diaryWindowRoot.gameObject.SetActive(false);
                    if(diaryWindowBlocker!=null)diaryWindowBlocker.SetActive(false);
                    owner.FocusDesktopWindow(FrontmostWindow);
                }
            }
            else if(diaryWindowRestoring)
            {
                DesktopMotion.Window(diaryWindowRoot,diaryWindowGroup,diaryWindowStarted,raw,false,diaryWindowFromTaskbar,
                    fullPosition,fullSize,taskPosition,taskSize);
                if(raw>=1f){diaryWindowRestoring=false;diaryWindowFromTaskbar=false;}
            }
            else
            {
                diaryWindowRoot.anchoredPosition=fullPosition;
                diaryWindowRoot.sizeDelta=fullSize;
                diaryWindowRoot.localScale=Vector3.one;
                if(diaryWindowGroup!=null)diaryWindowGroup.alpha=1;
            }
            diaryWindowContent.alpha=1;
            diaryWindowContent.interactable=!diaryWindowClosing&&!diaryWindowMinimizing&&
                (!diaryWindowRestoring||raw>.72f);
            diaryWindowContent.blocksRaycasts=diaryWindowContent.interactable;
        }
        void ClearDiaryWindow()
        {
            buttons.Remove("Back to device");
            buttons.Remove("Minimize diary");
            buttons.Remove("New diary");
            buttons.Remove("Save diary");
            for(int i=0;i<DiaryEntryTitles.Count+2;i++)buttons.Remove("Open diary entry "+i);
            if(diaryWindowRoot!=null){diaryWindowRoot.gameObject.SetActive(false);UnityEngine.Object.Destroy(diaryWindowRoot.gameObject);diaryWindowRoot=null;}
            if(diaryWindowBlocker!=null){diaryWindowBlocker.SetActive(false);UnityEngine.Object.Destroy(diaryWindowBlocker);diaryWindowBlocker=null;}
            diaryWindowContent=null;diaryDetailTitle=null;diaryDetailBody=null;
            diaryEditorText=null;diaryEditorHint=null;diarySaveButton=null;
            diaryTypingFrames.Clear();diaryFrameIndex=0;diaryVisibleText="";diaryWriting=false;
            diaryWindowMinimized=false;
            diaryWindowMinimizing=false;diaryWindowRestoring=false;
            diaryWindowClosing=false;
            diaryWindowFromTaskbar=false;
            RemoveTaskbarTask(FrontendPage.Diary);
        }
        public void CloseDiaryWindow()
        {
            ClearDiaryWindow();
        }
        void CreateFriendCard(RectTransform list,float y,string buttonName,string avatarResource,string friendName,
            string status,string preview,Action action,out Image unreadMarker)
        {
            var card=ActionButton(list,buttonName,"",42,y,1098,184,action,false,DesktopWindowAlt);
            var panel=(CyberPanelGraphic)card.targetGraphic;panel.cut=18;panel.accent=CyberTheme.Cyan;
            var avatarRect=Rect(friendName+" avatar",panel.rectTransform,18,18,146,146);
            var avatar=avatarRect.gameObject.AddComponent<RawImage>();
            Texture2D texture=Resources.Load<Texture2D>(avatarResource);
            avatar.texture=texture;avatar.raycastTarget=false;
            avatar.uvRect=texture!=null?CoverTexture(texture,1f):new Rect(0,0,1,1);
            var friendNameText=Label(panel.rectTransform,"Friend name",friendName,188,30,430,42,30,true,DesktopWindowText);
            friendNameText.font=UiChineseFont;
            var friendStatusText=Mono(panel.rectTransform,"Friend status",status,190,78,420,22,12,CyberTheme.Purple);
            friendStatusText.font=UiChineseFont;
            var friendPreviewText=Mono(panel.rectTransform,"Friend preview",preview,190,118,820,34,14,DesktopWindowMuted);
            friendPreviewText.font=UiChineseFont;
            unreadMarker=Box(panel.rectTransform,"Friend unread dot",1058,16,14,14,new Color(1f,.12f,.20f,1f));
        }
        void BeginFriendWindow(bool closing)
        {
            if(friendWindowRoot==null)return;
            SetPageAlpha(1);
            friendWindowClosing=closing;friendWindowStarted=Time.unscaledTime;
            friendWindowRestoring=!closing;friendWindowMinimizing=false;
            friendWindowFromTaskbar=false;

            if(friendWindowContent!=null)friendWindowContent.alpha=1;
        }
        void ToggleFriendRequests()
        {
            if(friendRequestPanel!=null){UnityEngine.Object.Destroy(friendRequestPanel);friendRequestPanel=null;return;}
            if(friendWindowContent==null)return;
            RemoveNotifications("request",-1);
            var host=friendWindowContent.GetComponent<RectTransform>();
            var scrim=Box(host,"Friend request scrim",0,0,1180,720,new Color(.08f,.03f,.18f,.48f));
            scrim.raycastTarget=true;friendRequestPanel=scrim.gameObject;
            var panel=Panel(scrim.rectTransform,"Friend request panel",196,104,788,512,DesktopWindowSurface,CyberTheme.Pink);
            var requestDrag=Box(panel.rectTransform,"Request drag handle",0,0,788,130,Color.clear);
            requestDrag.raycastTarget=true;
            AddWindowDrag(requestDrag.rectTransform,panel.rectTransform,()=>true,null);
            Box(panel.rectTransform,"Request top rail",0,0,788,6,CyberTheme.Pink);
            Mono(panel.rectTransform,"Request tag","[ CONTACT REQUESTS ]",34,28,500,24,13,CyberTheme.Purple);
            Label(panel.rectTransform,"Request heading","FRIEND REQUESTS",30,66,600,56,36,true,DesktopWindowText);
            if(bakaRequestPending&&!bakaRequestDeclined)
            {
                var request=Panel(panel.rectTransform,"Baka request",30,150,728,222,DesktopWindowAlt,CyberTheme.Pink);
                var avatarRect=Rect("Baka request avatar",request.rectTransform,18,18,132,132);
                var avatar=avatarRect.gameObject.AddComponent<RawImage>();
                Texture2D texture=Resources.Load<Texture2D>("Friends/baka");
                avatar.texture=texture;avatar.raycastTarget=false;avatar.uvRect=texture!=null?CoverTexture(texture,1f):new Rect(0,0,1,1);
                var requestName=Label(request.rectTransform,"Baka request name","Baka",172,24,420,38,27,true,DesktopWindowText);
                requestName.font=UiChineseFont;
                var requestCopy=Mono(request.rectTransform,"Baka request copy","想把你添加为好友",174,70,450,22,12,CyberTheme.Pink);
                requestCopy.font=UiChineseFont;
                var requestPreview=Mono(request.rectTransform,"Baka request preview","“您好您好！您就是那位说要帮咱的大佬吗！”",174,106,450,54,14,DesktopWindowMuted);
                requestPreview.font=UiChineseFont;
                ActionButton(request.rectTransform,"Accept Baka request","ACCEPT",506,136,202,58,AcceptBakaRequest,true,null,17);
            }
            else
            {
                Label(panel.rectTransform,"No requests","NO PENDING REQUESTS",62,250,664,50,28,true,DesktopWindowMuted,TextAnchor.MiddleCenter);
                Mono(panel.rectTransform,"No requests hint","New contact requests will appear here as the story progresses.",72,318,644,40,14,DesktopWindowMuted,TextAnchor.MiddleCenter);
            }
            ActionButton(panel.rectTransform,"Close requests","CLOSE",286,430,216,52,()=>{UnityEngine.Object.Destroy(friendRequestPanel);friendRequestPanel=null;},false,null,17);
        }
        public void BeginFriendExit()
        {
            BeginFriendWindow(true);
        }
        public void BeginDiaryExit()
        {
            if(diaryWindowRoot==null)return;
            diaryWindowClosing=true;diaryWindowMinimizing=false;diaryWindowRestoring=false;
            diaryWindowFromTaskbar=false;
            diaryWindowStarted=Time.unscaledTime;
        }
        public void FinishFriendExit()
        {
            if(!friendWindowClosing)return;
            if(tnelisChatPosition>=tnelisChat001.Length)FlushTnelisConversation();
            if(activeFriendIndex==1&&bakaConversationComplete&&!bakaMonologueTriggered)
            {
                bakaMonologueTriggered=true;
                bakaMonologueQueued=true;
            }
            bool createTnelisNews=tnelisConversationComplete&&!tnelisNewsCreated;
            ClearFriendWindow();
            if(createTnelisNews)AddTnelisNews();
        }
        public bool ConsumeBakaMonologue()
        {
            bool queued=bakaMonologueQueued;
            bakaMonologueQueued=false;
            return queued;
        }
        void FlushTnelisConversation()
        {
            while(tnelisPending.Count>0)tnelisMessages.Add(tnelisPending.Dequeue());
            friendTyping=false;tnelisConversationComplete=true;tnelisUnreadCount=0;RefreshUnreadIndicators();
        }
        void UpdateFriendWindow()
        {
            if(friendWindowRoot==null)return;
            if(!friendWindowClosing)UpdateFriendChatFlow();
            if(!friendWindowClosing)UpdateFriendComposerInput();
            float duration=friendWindowRestoring?.28f:FriendsWindowDuration;
            float raw=Mathf.Clamp01((Time.unscaledTime-friendWindowStarted)/duration);
            float t=Mathf.SmoothStep(0,1,raw);
            Vector2 finalPosition=friendWindowPosition,finalSize=new Vector2(AppWindowWidth,AppWindowHeight);
            Vector2 taskPosition=TaskbarSlotPosition(FrontendPage.Friends),taskSize=new Vector2(184,46);
            if(friendWindowClosing||friendWindowMinimizing)
            {
                DesktopMotion.Window(friendWindowRoot,friendWindowGroup,friendWindowStarted,raw,true,true,
                    finalPosition,finalSize,taskPosition,taskSize);
            }
            else if(friendWindowRestoring)
            {
                DesktopMotion.Window(friendWindowRoot,friendWindowGroup,friendWindowStarted,raw,false,friendWindowFromTaskbar,
                    finalPosition,finalSize,taskPosition,taskSize);
            }
            else
            {
                friendWindowRoot.anchoredPosition=finalPosition;
                friendWindowRoot.sizeDelta=finalSize;
                friendWindowRoot.localScale=Vector3.one;
                if(friendWindowGroup!=null)friendWindowGroup.alpha=1;
            }
            if(friendWindowContent!=null)
            {
                friendWindowContent.alpha=1;
                friendWindowContent.interactable=!friendWindowClosing&&!friendWindowMinimizing&&
                    (!friendWindowRestoring||raw>.72f);
                friendWindowContent.blocksRaycasts=friendWindowContent.interactable;
            }
            if(raw>=1&&friendWindowClosing)
            {
                // FinishFriendExit owns teardown so post-conversation events can run once.
            }
            if(raw>=1f&&friendWindowMinimizing)
            {
                friendWindowMinimizing=false;friendWindowMinimized=true;
                friendWindowRoot.gameObject.SetActive(false);
                if(friendWindowBlocker!=null)friendWindowBlocker.SetActive(false);
                owner.FocusDesktopWindow(FrontmostWindow);
            }
            if(raw>=1f&&friendWindowRestoring){friendWindowRestoring=false;friendWindowFromTaskbar=false;}
        }
        void ClearFriendWindow()
        {
            buttons.Remove("Close friends window");buttons.Remove("Open friend 0");
            buttons.Remove("Minimize friends");
            buttons.Remove("Open friend requests");
            buttons.Remove("Back to friends");
            buttons.Remove("Send friend message");
            buttons.Remove("Accept Baka request");buttons.Remove("Reject Baka request");buttons.Remove("Open Baka chat");
            buttons.Remove("Close requests");
            if(friendListLayer!=null){friendListLayer.SetActive(false);UnityEngine.Object.Destroy(friendListLayer);friendListLayer=null;}
            if(friendChatLayer!=null){friendChatLayer.SetActive(false);UnityEngine.Object.Destroy(friendChatLayer);friendChatLayer=null;}
            if(friendRequestPanel!=null){friendRequestPanel.SetActive(false);UnityEngine.Object.Destroy(friendRequestPanel);friendRequestPanel=null;}
            if(friendWindowRoot!=null){friendWindowRoot.gameObject.SetActive(false);UnityEngine.Object.Destroy(friendWindowRoot.gameObject);friendWindowRoot=null;}
            if(friendWindowBlocker!=null){friendWindowBlocker.SetActive(false);UnityEngine.Object.Destroy(friendWindowBlocker);friendWindowBlocker=null;}
            friendWindowContent=null;friendCloseButton=null;friendWindowClosing=false;
            friendComposerText=null;friendComposerHint=null;friendComposerPanel=null;friendSendButton=null;
            friendPresetLine="";friendRevealedLine="";friendRevealIndex=0;
            friendAwaitingPlayer=false;
            friendWindowMinimized=false;
            friendWindowMinimizing=false;friendWindowRestoring=false;
            friendWindowFromTaskbar=false;
            RemoveTaskbarTask(FrontendPage.Friends);
            tnelisUnreadMarker=null;friendRequestUnreadDot=null;
        }
        void RefreshUnreadIndicators()
        {
            if(newsUnreadDot!=null)newsUnreadDot.gameObject.SetActive(newsUnread);
            bool friendHasUnread=tnelisUnreadCount>0||bakaUnreadCount>0||(bakaRequestPending&&!bakaRequestDeclined);
            if(friendUnreadDot!=null)friendUnreadDot.gameObject.SetActive(friendHasUnread);
            if(homeDiaryUnreadDot!=null)homeDiaryUnreadDot.gameObject.SetActive(diaryUnread);
            if(deviceDiaryUnreadDot!=null)deviceDiaryUnreadDot.gameObject.SetActive(diaryUnread);
            if(deviceFriendUnreadDot!=null)deviceFriendUnreadDot.gameObject.SetActive(friendHasUnread);
            if(tnelisUnreadMarker!=null)tnelisUnreadMarker.gameObject.SetActive(friendHasUnread);
            if(bakaRequestMarker!=null)bakaRequestMarker.gameObject.SetActive(bakaRequestPending&&!bakaRequestDeclined);
            if(bakaUnreadMarker!=null)bakaUnreadMarker.gameObject.SetActive(bakaUnreadCount>0);
            if(friendRequestUnreadDot!=null)friendRequestUnreadDot.gameObject.SetActive(bakaRequestPending&&!bakaRequestDeclined);
        }
        void OpenFriendChat()
        {
            RemoveNotifications("friend",0);
            activeFriendIndex=0;
            tnelisUnreadCount=0;RefreshUnreadIndicators();
            tnelisChatOpen=true;
            friendPresetLine="";friendRevealedLine="";friendRevealIndex=0;
            friendAwaitingPlayer=false;
            if(friendListLayer!=null)friendListLayer.SetActive(false);
            if(tnelisMessages.Count==0&&tnelisPending.Count==0&&tnelisChatPosition==0)
            {
                QueueTnelisReplies();
                tnelisConversationComplete=false;
            }
            BuildFriendChat();
            UpdateFriendChatFlow();
        }
        void QueueTnelisReplies()
        {
            while(tnelisChatPosition<tnelisChat001.Length&&!tnelisChat001[tnelisChatPosition].FromPlayer)
            {
                var line=tnelisChat001[tnelisChatPosition++];
                tnelisPending.Enqueue(new FriendChatMessage{Text=line.Text});
            }
        }
        void QueueBakaReplies()
        {
            while(bakaChatPosition<bakaChat002.Length&&!bakaChat002[bakaChatPosition].FromPlayer)
            {
                var line=bakaChat002[bakaChatPosition++];
                bakaPending.Enqueue(new FriendChatMessage{Text=line.Text});
            }
        }
        void AcceptBakaRequest()
        {
            bakaRequestPending=false;bakaFriendAccepted=true;bakaUnreadCount=0;
            bakaRequestMarker=null;RefreshUnreadIndicators();
            tnelisChatOpen=false;
            ShowFriends();
            OpenBakaChat();
        }
        void RejectBakaRequest()
        {
            bakaRequestPending=false;bakaRequestDeclined=true;bakaUnreadCount=0;
            bakaRequestMarker=null;RefreshUnreadIndicators();
            ShowFriends();
        }
        void OpenBakaChat()
        {
            RemoveNotifications("friend",1);
            activeFriendIndex=1;tnelisChatOpen=true;bakaUnreadCount=0;RefreshUnreadIndicators();
            friendPresetLine="";friendRevealedLine="";friendRevealIndex=0;
            friendAwaitingPlayer=false;
            if(friendListLayer!=null)friendListLayer.SetActive(false);
            if(bakaMessages.Count==0&&bakaPending.Count==0&&bakaChatPosition==0)
            {
                bakaConversationComplete=false;
                QueueBakaReplies();
            }
            BuildFriendChat();
            UpdateFriendChatFlow();
        }
        void BuildFriendChat()
        {
            if(friendWindowContent==null)return;
            if(friendChatLayer!=null){friendChatLayer.SetActive(false);UnityEngine.Object.Destroy(friendChatLayer);friendChatLayer=null;}
            buttons.Remove("Back to friends");buttons.Remove("Send friend message");
            friendTypingText=null;friendComposerText=null;friendComposerHint=null;friendComposerPanel=null;friendSendButton=null;
            bool baka=activeFriendIndex==1;
            List<FriendChatMessage> messages=baka?bakaMessages:tnelisMessages;
            Queue<FriendChatMessage> pending=baka?bakaPending:tnelisPending;
            string friendName=baka?"Baka":"Tnelis";
            string avatarResource=baka?"Friends/baka":"Friends/tnelis";
            string friendStatus=baka?"好友  /  学校中继":"在线  /  安全中继";
            var host=friendWindowContent.GetComponent<RectTransform>();
            friendChatLayer=Rect("Friend chat",host,0,0,1180,720).gameObject;
            var chat=friendChatLayer.GetComponent<RectTransform>();
            var backToFriends=ActionButton(chat,"Back to friends","<  FRIENDS",28,82,190,42,CloseFriendChat,false,
                DesktopWindowAlt,13);
            SetButtonCaptionColor(backToFriends,DesktopWindowText);
            var avatar=Rect("Chat avatar",chat,230,76,58,58).gameObject.AddComponent<RawImage>();
            Texture2D texture=Resources.Load<Texture2D>(avatarResource);
            avatar.texture=texture;avatar.raycastTarget=false;avatar.uvRect=texture!=null?CoverTexture(texture,1f):new Rect(0,0,1,1);
            var chatName=Label(chat,"Chat name",friendName,302,76,300,34,25,true,DesktopWindowText);
            chatName.font=UiChineseFont;
            var chatStatus=Mono(chat,"Chat status",friendStatus,304,111,330,20,11,CyberTheme.Purple);
            chatStatus.font=UiChineseFont;

            var viewport=Rect("Chat viewport",chat,28,146,1124,458);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color=new Color(1f,1f,1f,.42f);
            var content=Rect("Chat messages",viewport,0,0,1124,458);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            scroll.verticalNormalizedPosition=0;
            Texture2D playerTexture=Resources.Load<Texture2D>("UI/LoginAvatar");
            float cursorY=18f;
            for(int i=0;i<messages.Count;i++)
            {
                var message=messages[i];
                float bubbleHeight=FriendBubbleHeight(message.Text);
                float bubbleX=message.FromPlayer?472:72;
                var bubble=Panel(content,"Chat message "+i,bubbleX,cursorY,580,bubbleHeight,
                    message.FromPlayer?new Color(.83f,.96f,.99f,.98f):new Color(.95f,.88f,.99f,.98f),
                    message.FromPlayer?CyberTheme.Cyan:CyberTheme.Pink);
                bubble.cut=14;
                var text=Label(bubble.rectTransform,"Chat text",message.Text,18,10,544,bubbleHeight-20,17,false,
                    DesktopWindowText);
                text.font=UiChineseFont;
                text.lineSpacing=.9f;Fit(text,12,17);
                var messageAvatar=Rect("Message avatar",content,message.FromPlayer?1066:16,cursorY+8,42,42)
                    .gameObject.AddComponent<RawImage>();
                messageAvatar.texture=message.FromPlayer?playerTexture:texture;
                messageAvatar.enabled=messageAvatar.texture!=null;
                messageAvatar.raycastTarget=false;
                messageAvatar.uvRect=messageAvatar.texture!=null?CoverTexture(messageAvatar.texture,1f):new Rect(0,0,1,1);
                cursorY+=bubbleHeight+14f;
            }
            if(friendTyping)
            {
                var typing=Panel(content,friendName+" typing",72,cursorY,184,64,
                    new Color(.95f,.88f,.99f,.98f),CyberTheme.Pink);
                typing.cut=16;
                friendTypingText=Label(typing.rectTransform,"Typing dots","···",0,0,184,64,29,true,
                    CyberTheme.Pink,TextAnchor.MiddleCenter);
                var typingAvatar=Rect("Typing avatar",content,16,cursorY+8,42,42)
                    .gameObject.AddComponent<RawImage>();
                typingAvatar.texture=texture;
                typingAvatar.enabled=typingAvatar.texture!=null;
                typingAvatar.raycastTarget=false;
                typingAvatar.uvRect=typingAvatar.texture!=null?CoverTexture(typingAvatar.texture,1f):new Rect(0,0,1,1);
                cursorY+=78f;
            }
            float contentHeight=Mathf.Max(458,cursorY+18f);
            content.sizeDelta=new Vector2(1124,contentHeight);
            content.anchoredPosition=new Vector2(0,Mathf.Max(0,contentHeight-458));

            bool complete=baka?bakaConversationComplete:tnelisConversationComplete;
            if(!complete&&!friendTyping&&pending.Count==0&&!friendAwaitingPlayer)
                BeginFriendPlayerLine();
            var composer=Panel(chat,"Chat composer",28,612,1124,86,DesktopWindowAlt,CyberTheme.Purple);
            friendComposerPanel=composer;
            composer.raycastTarget=false;
            var composerState=Label(composer.rectTransform,"Composer state",
                complete?"对话已经结束":friendTyping||pending.Count>0?"正在等待对方输入":
                "按任意键逐字输入预设台词",
                20,8,940,20,13,false,CyberTheme.Purple);
            composerState.font=UiChineseFont;
            friendComposerText=Label(composer.rectTransform,"Composer line",friendRevealedLine,
                20,30,940,44,23,false,DesktopWindowText,TextAnchor.MiddleLeft);
            friendComposerText.font=UiChineseFont;
            friendComposerHint=Label(composer.rectTransform,"Composer hint","",832,32,128,40,13,false,
                DesktopWindowMuted,TextAnchor.MiddleRight);
            friendComposerHint.font=UiChineseFont;
            var send=ActionButton(composer.rectTransform,"Send friend message","SEND",1000,28,106,46,
                SendFriendMessage,true,CyberTheme.Purple,16);
            friendSendButton=send;
            RefreshFriendComposer();
        }
        static float FriendBubbleHeight(string text)
        {
            int length=text==null?0:text.Length;
            return Mathf.Clamp(62f+Mathf.Max(0,Mathf.CeilToInt(length/48f)-1)*20f,62f,142f);
        }
        void BeginFriendPlayerLine()
        {
            bool baka=activeFriendIndex==1;
            friendPresetLine=baka
                ?bakaChatPosition<bakaChat002.Length&&bakaChat002[bakaChatPosition].FromPlayer
                    ?bakaChat002[bakaChatPosition].Text:""
                :tnelisChatPosition<tnelisChat001.Length&&tnelisChat001[tnelisChatPosition].FromPlayer
                    ?tnelisChat001[tnelisChatPosition].Text:"";
            BuildTypingFrames(friendPresetLine,friendTypingFrames);
            friendRevealedLine="";
            friendRevealIndex=0;
            friendAwaitingPlayer=friendPresetLine.Length>0;
        }
        void RefreshFriendComposer()
        {
            if(friendComposerText!=null)
                friendComposerText.text=friendRevealedLine;
            bool ready=FriendMessageReadyToSend;
            if(friendComposerHint!=null)
                friendComposerHint.text=!FriendComposerReady?"等待对方":
                    ready?"ENTER 发送":friendRevealIndex+"/"+friendTypingFrames.Count;
            if(friendComposerPanel!=null)
                friendComposerPanel.accent=FriendComposerReady?CyberTheme.Cyan:CyberTheme.Purple;
            if(friendSendButton!=null)friendSendButton.interactable=ready;
        }
        void RevealFriendCharacter()
        {
            if(!FriendComposerReady||friendRevealIndex>=friendTypingFrames.Count)return;
            friendRevealedLine=friendTypingFrames[friendRevealIndex];
            friendRevealIndex++;
            owner.PlayKeyboardKeySound();
            RefreshFriendComposer();
        }
        static void BuildTypingFrames(string line,List<string> frames)
        {
            frames.Clear();
            string committed="";
            for(int i=0;i<line.Length;i++)
            {
                char value=line[i];
                string pinyin=TypingPinyin(value);
                if(string.IsNullOrEmpty(pinyin))
                {
                    committed+=value;
                    frames.Add(committed);
                    continue;
                }
                string typed="";
                for(int p=0;p<pinyin.Length;p++)
                {
                    typed+=pinyin[p];
                    frames.Add(committed+typed);
                }
                committed+=value;
                frames.Add(committed);
            }
        }
        static string TypingPinyin(char value)
        {
            switch(value)
            {
                case '你':return "ni";
                case '也':return "ye";
                case '好':return "hao";
                case '呀':return "ya";
                case '把':return "ba";
                case '坐':return "zuo";
                case '标':return "biao";
                case '发':return "fa";
                case '给':return "gei";
                case '我':return "wo";
                case '会':return "hui";
                case '小':return "xiao";
                case '心':return "xin";
                case '明':return "ming";
                case '白':return "bai";
                case '了':return "le";
                case '们':return "men";
                case '一':return "yi";
                case '起':return "qi";
                case '听':return "ting";
                case '答':return "da";
                case '应':return "ying";
                case '想':return "xiang";
                case '需':return "xu";
                case '要':return "yao";
                case '去':return "qu";
                case '写':return "xie";
                case '点':return "dian";
                case '什':return "shen";
                case '么':return "me";
                case '今':return "jin";
                case '天':return "tian";
                case '的':return "de";
                case '信':return "xin";
                case '号':return "hao";
                case '还':return "hai";
                case '在':return "zai";
                case '耳':return "er";
                case '边':return "bian";
                case '旋':return "xuan";
                case '律':return "lv";
                case '让':return "rang";
                case '过':return "guo";
                case '继':return "ji";
                case '续':return "xu";
                case '追':return "zhui";
                case '查':return "cha";
                case '第':return "di";
                case '区':return "qu";
                case '住':return "zhu";
                case '那':return "na";
                case '些':return "xie";
                case '没':return "mei";
                case '留':return "liu";
                case '下':return "xia";
                case '名':return "ming";
                case '字':return "zi";
                case '人':return "ren";
                case '终':return "zhong";
                case '于':return "yu";
                case '完':return "wan";
                case '段':return "duan";
                case '并':return "bing";
                case '不':return "bu";
                case '危':return "wei";
                case '险':return "xian";
                case '它':return "ta";
                case '更':return "geng";
                case '像':return "xiang";
                case '是':return "shi";
                case '封':return "feng";
                case '忘':return "wang";
                case '记':return "ji";
                default:return null;
            }
        }
        void UpdateFriendComposerInput()
        {
            if(!FriendComposerReady)return;
            bool enter=Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter);
            if(enter)
            {
                if(FriendMessageReadyToSend)SendFriendMessage();
                return;
            }
            if(Input.GetKeyDown(KeyCode.Escape))return;
            bool revealInput=Input.anyKeyDown||Input.GetMouseButtonDown(0);
            if(revealInput)RevealFriendCharacter();
        }
        void SendFriendMessage()
        {
            if(!FriendMessageReadyToSend)return;
            string playerText=friendPresetLine;
            bool baka=activeFriendIndex==1;
            List<FriendChatMessage> messages=baka?bakaMessages:tnelisMessages;
            Queue<FriendChatMessage> pending=baka?bakaPending:tnelisPending;
            messages.Add(new FriendChatMessage{FromPlayer=true,Text=playerText});
            friendAwaitingPlayer=false;
            friendPresetLine="";friendRevealedLine="";friendRevealIndex=0;
            if(baka)
            {
                bakaChatPosition++;
                QueueBakaReplies();
                if(bakaChatPosition>=bakaChat002.Length&&pending.Count==0)
                    bakaConversationComplete=true;
            }
            else if(!baka)
            {
                tnelisChatPosition++;
                QueueTnelisReplies();
            }
            friendNextMessageAt=Time.unscaledTime+.18f;
            BuildFriendChat();
            UpdateFriendChatFlow();
        }
        void UpdateFriendChatFlow()
        {
            if(friendWindowRoot==null||friendWindowClosing)return;
            float now=Time.unscaledTime;
            bool baka=activeFriendIndex==1;
            Queue<FriendChatMessage> pending=baka?bakaPending:tnelisPending;
            List<FriendChatMessage> messages=baka?bakaMessages:tnelisMessages;
            if(friendTyping)
            {
                if(friendTypingText!=null)
                {
                    float pulse=.45f+.45f*Mathf.Sin(now*8f);
                    Color color=friendTypingText.color;color.a=pulse;friendTypingText.color=color;
                }
                if(now>=friendTypingUntil)
                {
                    FriendChatMessage delivered=pending.Dequeue();
                    messages.Add(delivered);
                    friendTyping=false;
                    friendNextMessageAt=now+.34f;
                    if(tnelisChatOpen)
                    {
                        if(baka)bakaUnreadCount=0;else tnelisUnreadCount=0;
                    }
                    else
                    {
                        if(baka)
                        {
                            bakaUnreadCount++;
                            AddNotification("friend","新消息 / Baka",delivered.Text,1,()=>owner.OpenFriendChatFromNotification(1));
                        }
                        else
                        {
                            tnelisUnreadCount++;
                            AddNotification("friend","新消息 / Tnelis",delivered.Text,0,()=>owner.OpenFriendChatFromNotification(0));
                        }
                    }
                    RefreshUnreadIndicators();
                    if(baka&&bakaChatPosition>=bakaChat002.Length&&pending.Count==0)
                        bakaConversationComplete=true;
                    if(!baka&&tnelisChatPosition>=tnelisChat001.Length&&pending.Count==0)
                        tnelisConversationComplete=true;
                    if(friendChatLayer!=null)BuildFriendChat();
                }
                return;
            }
            if(pending.Count>0&&now>=friendNextMessageAt)
            {
                FriendChatMessage next=pending.Peek();
                friendTyping=true;
                friendTypingUntil=now+Mathf.Clamp(.45f+next.Text.Length*.035f,.7f,2.2f);
                if(friendChatLayer!=null)BuildFriendChat();
            }
        }
        void AddTnelisNews()
        {
            if(tnelisNewsCreated)return;
            tnelisNewsCreated=true;
            int index=newsPosts.Count;
            newsPosts.Add(new NewsPost{
                Name="Tnelis",Handle="@tnelis_09",Time="刚刚 / 私人中继",
                Title="怎样提高员工的工作效率？",
                Body="无论如何，提升员工的工作效率是每个老板的必备技能，但是为什么我的员工工作积极性就这么低？是我对他们太仁慈了吗？可恶，企业整治任重道远.......\n\n二编：一想到我的员工可能不上班在这里摸鱼刷我博客我更气了...",
                Tag="#工作效率 #老板 #摸鱼",ImageCaption="Tnelis / 工作日志",HasImage=true,ImageResource="Friends/tnelis",
                CommentDraft="对不起老板我错了。",
                Likes=17,
                Comments=new[]{"无产阶级斗士：你这波资本家思维了，只要你薪资发的够哪有不干活的道理。"}
            });
            newsLiked.Add(false);newsUnreadItems.Add(true);newsCommentPosted.Add(false);newsCommentMessages.Add(new List<string>());
            newsUnread=true;RefreshUnreadIndicators();
            RebuildNewsCards();
            AddNotification("news","新动态 / Tnelis","Tnelis 发布了工作效率话题。",index,()=>owner.OpenNewsFromNotification(index));
        }
        void CreateBakaNews()
        {
            if(bakaNewsCreated)return;
            bakaNewsCreated=true;
            bakaNewsIndex=newsPosts.Count;
            int index=bakaNewsIndex;
            newsPosts.Add(new NewsPost{
                Name="Baka",Handle="@baka_wonders",Time="刚刚 / 学校中继",
                Title="【求助】我家的调律器坏了，求大佬远程修复！",
                Body="最近几天在P公司买的调律器，结果没用今天就坏了....找他们保修还被“个人原因导致的损坏”拒绝了...气煞我也！\n\n想问问有无超级黑客能远程修复我的调律器！",
                Tag="#求助 #调律器 #远程修复",ImageCaption="Baka / 调律器求助",HasImage=true,ImageResource="News/baka-post",
                CommentDraft="这活我接了。",
                Likes=6,
                Comments=new[]{"P公司官方客服：您好，请不要发布有损我司形象的相关言论。"}
            });
            newsLiked.Add(false);newsUnreadItems.Add(true);newsCommentPosted.Add(false);newsCommentMessages.Add(new List<string>());
            newsUnread=true;RefreshUnreadIndicators();
            RebuildNewsCards();
            AddNotification("news","新动态 / Baka","Baka 的调律器坏了，正在求助。",index,()=>owner.OpenNewsFromNotification(index));
        }
        void CloseFriendChat()
        {
            tnelisChatOpen=false;
            if(friendChatLayer!=null){friendChatLayer.SetActive(false);UnityEngine.Object.Destroy(friendChatLayer);friendChatLayer=null;}
            buttons.Remove("Back to friends");
            buttons.Remove("Send friend message");
            friendComposerText=null;friendComposerHint=null;friendComposerPanel=null;friendSendButton=null;
            friendPresetLine="";friendRevealedLine="";friendRevealIndex=0;
            friendAwaitingPlayer=false;
            if(friendListLayer!=null)friendListLayer.SetActive(true);
        }
        public void ShowNews()
        {
            newsUnread=false;RefreshUnreadIndicators();
            ClearNewsWindow();
            newsUnreadMarkers.Clear();
            SetPageAlpha(1);
            newsWindowBlocker=Rect("News window input blocker",artboard,0,0,1600,900).gameObject;
            var blockerImage=newsWindowBlocker.AddComponent<Image>();
            blockerImage.color=new Color(.08f,.04f,.18f,.16f);blockerImage.raycastTarget=false;
            newsWindowRoot=Rect("News popup window",artboard,newsWindowPosition.x,-newsWindowPosition.y,AppWindowWidth,AppWindowHeight);
            newsWindowRoot.gameObject.AddComponent<RectMask2D>();
            newsWindowGroup=newsWindowRoot.gameObject.AddComponent<CanvasGroup>();
            Box(newsWindowRoot,"Window background",0,0,AppWindowWidth,AppWindowHeight,DesktopWindowSurface);
            var frame=Panel(newsWindowRoot,"Window frame",0,0,AppWindowWidth,AppWindowHeight,new Color(0,0,0,0),CyberTheme.Cyan);
            frame.outline=true;frame.raycastTarget=true;
            var surface=Rect("News design surface",newsWindowRoot,0,0,1600,900);
            surface.localScale=Vector3.one*(AppWindowWidth/1600f);
            var titleBar=Panel(surface,"Window title bar",0,0,1600,64,DesktopWindowHeader,new Color(.91f,.72f,1f));
            newsWindowFocus=AddWindowFocus(newsWindowRoot,FrontendPage.News);
            titleBar.raycastTarget=true;
            AddWindowDrag(titleBar.rectTransform,newsWindowRoot,()=>!newsWindowMinimizing&&!newsWindowRestoring&&!newsWindowClosing,position=>newsWindowPosition=position,()=>FocusWindow(FrontendPage.News));
            titleBar.cut=12;
            Box(titleBar.rectTransform,"Window cyan rail",0,0,1600,5,CyberTheme.Cyan);
            Box(titleBar.rectTransform,"Window pink rail",0,5,1600,2,CyberTheme.Pink);
            Mono(titleBar.rectTransform,"Window title","SIGNAL NETWORK  /  COMMUNITY TRANSMISSIONS",26,18,720,24,13,new Color(.98f,.97f,1f));
            CreateWindowControl(titleBar.rectTransform,"Minimize news","-",1448,48,MinimizeCurrentWindow,DesktopWindowAlt);
            var closeWindow=CreateWindowControl(titleBar.rectTransform,"Close news window","X",1544,48,owner.ReturnFromNews,DesktopWindowAlt);
            newsCloseButton=closeWindow;
            var closePanel=(CyberPanelGraphic)closeWindow.targetGraphic;closePanel.cut=6;closePanel.accent=CyberTheme.Pink;

            newsWindowContent=Rect("News window content",surface,0,0,1600,900).gameObject.AddComponent<CanvasGroup>();
            newsWindowContent.alpha=0;
            newsFeedLayer=Rect("News card feed",newsWindowContent.GetComponent<RectTransform>(),0,0,1600,900).gameObject;
            var feed=newsFeedLayer.GetComponent<RectTransform>();
            Mono(feed,"Feed eyebrow","[ COMMUNITY NETWORK / PUBLIC TRANSMISSIONS ]",64,137,780,24,13,CyberTheme.Purple);
            Label(feed,"Feed heading","SIGNAL FEED",64,174,760,74,54,true,DesktopWindowText);
            Mono(feed,"Feed count",newsPosts.Count.ToString("D2")+" ACTIVE POST"+(newsPosts.Count==1?"":"S")+"  /  LIVE RELAY",68,250,720,22,13,DesktopWindowMuted);

            var viewport=Rect("News feed viewport",feed,64,270,1472,540);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color=new Color(1f,1f,1f,.42f);
            int rows=Mathf.CeilToInt(newsPosts.Count/4f);
            var content=Rect("News feed posts",viewport,0,0,1472,Math.Max(500,rows*580));
            newsFeedContent=content;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=34;
            for(int i=0;i<newsPosts.Count;i++)CreateNewsCard(content,newsPosts[i],i);
            Mono(feed,"Feed footer","SIGNAL FEED IS A LOCAL DEMO CHANNEL  /  SCROLL TO READ MORE",68,817,900,20,11,DesktopWindowMuted);
            newsWindowMinimized=false;
            newsWindowRestoring=true;newsWindowMinimizing=false;newsWindowStarted=Time.unscaledTime;
            if(newsWindowGroup!=null)newsWindowGroup.alpha=0;
            newsWindowRoot.localScale=Vector3.one*.78f;
            EnsureTaskbarTask(FrontendPage.News,"新闻系统  /  SIGNAL FEED");
            SetTaskbarTaskMinimized(FrontendPage.News,false);
            BeginNewsWindow(false);
        }
        void CreateNewsCard(RectTransform parent,NewsPost post,int index)
        {
            int column=index%4,row=index/4;
            float x=column*368,y=row*580;
            RectTransform source=null;
            var card=ActionButton(parent,"Open news post "+index,"",x,y,344,540,()=>ShowNewsDetail(index,source),false,DesktopWindowAlt);
            var panel=(CyberPanelGraphic)card.targetGraphic;
            source=panel.rectTransform;
            panel.cut=18;panel.accent=index%2==0?CyberTheme.Cyan:CyberTheme.Purple;
            var imageRect=Rect("News card image",panel.rectTransform,16,16,312,300);
            var image=imageRect.gameObject.AddComponent<RawImage>();
            Texture2D texture=LoadNewsImage(post.ImageResource);
            image.texture=texture;image.raycastTarget=false;
            image.uvRect=texture!=null?CoverTexture(texture,312f/300f):new Rect(0,0,1,1);
            if(texture==null)
            {
                image.enabled=false;
                imageRect.gameObject.AddComponent<CyberNewsArtwork>().raycastTarget=false;
            }
            var title=Label(panel.rectTransform,"News card title",post.Title,18,326,308,54,20,true,DesktopWindowText);
            title.font=UiChineseFont;
            Fit(title,14,20);
            if(newsUnreadItems[index])
                newsUnreadMarkers[index]=Box(panel.rectTransform,"News unread marker",315,12,15,15,new Color(1f,.12f,.20f,1f));
            var publisher=Mono(panel.rectTransform,"News card publisher",post.Name+"  "+post.Handle,18,386,308,22,13,CyberTheme.Purple);
            publisher.font=UiChineseFont;
            var excerpt=Label(panel.rectTransform,"News card excerpt",Excerpt(post.Body,96),18,414,308,92,16,false,DesktopWindowMuted);
            excerpt.font=UiChineseFont;
            excerpt.lineSpacing=.92f;
        }
        static string Excerpt(string value,int maxLength)
        {
            if(string.IsNullOrEmpty(value)||value.Length<=maxLength)return value;
            return value.Substring(0,maxLength-3).TrimEnd()+"...";
        }
        void RebuildNewsCards()
        {
            if(newsFeedContent==null)return;
            for(int i=0;i<newsPosts.Count+4;i++)buttons.Remove("Open news post "+i);
            for(int i=newsFeedContent.childCount-1;i>=0;i--)
            {
                GameObject child=newsFeedContent.GetChild(i).gameObject;
                child.SetActive(false);UnityEngine.Object.Destroy(child);
            }
            newsUnreadMarkers.Clear();
            int rows=Mathf.CeilToInt(newsPosts.Count/4f);
            newsFeedContent.sizeDelta=new Vector2(1472,Math.Max(500,rows*580));
            for(int i=0;i<newsPosts.Count;i++)CreateNewsCard(newsFeedContent,newsPosts[i],i);
        }
        void ShowNewsDetail(int index,RectTransform sourceCard)
        {
            if(index<0||index>=newsPosts.Count||newsWindowContent==null)return;
            ClearNewsDetail();
            selectedNewsPost=index;
            RemoveNotifications("news",index);
            var post=newsPosts[index];
            newsUnreadItems[index]=false;
            newsUnread=false;
            for(int i=0;i<newsUnreadItems.Count;i++)if(newsUnreadItems[i]){newsUnread=true;break;}
            if(newsUnreadMarkers.TryGetValue(index,out Image marker)&&marker!=null)marker.gameObject.SetActive(false);
            RefreshUnreadIndicators();
            if((post.Name=="Tnelis"||post.Name=="特内莉丝")&&!bakaNewsCreated)bakaNewsQueued=true;
            RectTransform host=newsWindowContent.GetComponent<RectTransform>();
            newsDetailBlocker=Rect("News detail blocker",host,0,64,1600,836).gameObject;
            var blockerImage=newsDetailBlocker.AddComponent<Image>();
            blockerImage.color=new Color(0,0,0,.38f);blockerImage.raycastTarget=true;
            newsDetailIconPosition=new Vector2(0,64);
            newsDetailIconSize=new Vector2(1600,836);
            newsDetailRoot=Rect("News detail window",host,newsDetailIconPosition.x,-newsDetailIconPosition.y,
                newsDetailIconSize.x,newsDetailIconSize.y);
            newsDetailRoot.gameObject.AddComponent<RectMask2D>();
            Box(newsDetailRoot,"Detail window background",0,0,1600,900,DesktopWindowSurface);
            newsDetailContent=newsDetailRoot.gameObject.AddComponent<CanvasGroup>();
            newsDetailContent.alpha=sourceCard!=null?0:1;
            var detail=Rect("News detail content",newsDetailRoot,0,-64,1600,900);
            var closeDetail=ActionButton(detail,"Close detail","<  BACK TO FEED",64,88,240,46,CloseNewsDetail,false,
                DesktopWindowAlt,14);
            SetButtonCaptionColor(closeDetail,DesktopWindowText);
            var imageRect=Rect("Detail image",detail,64,160,620,430);
            var image=imageRect.gameObject.AddComponent<RawImage>();
            Texture2D texture=LoadNewsImage(post.ImageResource);
            image.texture=texture;image.raycastTarget=false;
            image.uvRect=texture!=null?CoverTexture(texture,620f/430f):new Rect(0,0,1,1);
            if(texture==null)
            {
                image.enabled=false;
                imageRect.gameObject.AddComponent<CyberNewsArtwork>().raycastTarget=false;
            }
            Mono(detail,"Detail image caption",post.ImageCaption,70,606,600,20,11,CyberTheme.Purple);
            var detailTitle=Label(detail,"Detail title",post.Title,744,150,792,78,39,true,DesktopWindowText);
            detailTitle.font=UiChineseFont;
            var detailHandle=Mono(detail,"Detail handle",post.Name+"  /  "+post.Handle+"  /  "+post.Time,
                748,234,760,28,15,DesktopWindowMuted);
            detailHandle.font=UiChineseFont;
            var body=Label(detail,"Detail body",post.Body,744,280,792,195,27,false,DesktopWindowText);
            body.font=UiChineseFont;
            body.lineSpacing=.92f;Fit(body,18,27);
            Mono(detail,"Detail tag",post.Tag,748,485,760,22,12,CyberTheme.Purple);
            string likeCaption=(newsLiked[index]?"LIKED  ":"LIKE  ")+(post.Likes+(newsLiked[index]?1:0));
            var like=ActionButton(detail,"Like post","",744,520,250,62,()=>ToggleNewsLike(index),newsLiked[index],
                newsLiked[index]?(Color?)new Color(.95f,.72f,.84f,.98f):DesktopWindowAlt,19);
            var likePanel=(CyberPanelGraphic)like.targetGraphic;
            var heart=Rect("Heart like icon",likePanel.rectTransform,16,15,32,32).gameObject.AddComponent<HeartIconGraphic>();
            heart.color=newsLiked[index]?new Color(.92f,.22f,.48f,1f):CyberTheme.Purple;heart.raycastTarget=false;
            Mono(likePanel.rectTransform,"Like count",likeCaption,58,16,176,30,17,
                newsLiked[index]?new Color(.62f,.12f,.32f,1f):DesktopWindowText,TextAnchor.MiddleCenter);
            var comments=Panel(detail,"Detail comments",744,596,792,204,DesktopWindowAlt,CyberTheme.Purple);
            int commentCount=post.Comments!=null?post.Comments.Length:0;
            var visibleComments=new List<string>();
            if(post.Comments!=null)for(int i=0;i<commentCount;i++)visibleComments.Add(post.Comments[i]);
            List<string> authoredComments=newsCommentMessages[index];
            for(int i=0;i<authoredComments.Count;i++)visibleComments.Add(authoredComments[i]);
            Mono(comments.rectTransform,"Comments heading","COMMENTS  /  "+visibleComments.Count.ToString("D2"),
                22,14,730,26,15,CyberTheme.Purple);
            int firstComment=Mathf.Max(0,visibleComments.Count-2);
            for(int i=firstComment;i<visibleComments.Count;i++)
            {
                int row=i-firstComment;
                float rowY=42+row*42;
                var commentLine=Mono(comments.rectTransform,"Detail comment "+row,visibleComments[i],22,rowY,742,34,18,
                    row==0?DesktopWindowText:DesktopWindowMuted);
                commentLine.font=UiChineseFont;
                if(row==0)Box(comments.rectTransform,"Detail comment divider",22,84,742,1,CyberTheme.Alpha(CyberTheme.Purple,.18f));
            }
            if(!newsCommentPosted[index])
            {
                var postComment=ActionButton(comments.rectTransform,"Post comment","POST COMMENT",20,134,742,44,
                    ()=>PostNewsComment(index),false,DesktopWindowSurface,14);
                SetButtonCaptionColor(postComment,DesktopWindowText);
            }
            else
            {
                Mono(comments.rectTransform,"Comment posted","COMMENT POSTED  /  TRANSMISSION LOGGED",22,148,742,28,13,CyberTheme.Purple,TextAnchor.MiddleCenter);
            }
            newsDetailStarted=sourceCard!=null?Time.unscaledTime:Time.unscaledTime-NewsDetailDuration;
            newsDetailClosing=false;
        }
        void ToggleNewsLike(int index)
        {
            if(index<0||index>=newsLiked.Count)return;
            newsLiked[index]=!newsLiked[index];
            ShowNewsDetail(index,null);
        }
        void PostNewsComment(int index)
        {
            if(index<0||index>=newsCommentPosted.Count)return;
            if(newsCommentPosted[index])return;
            newsCommentPosted[index]=true;
            newsCommentMessages[index].Add("YOU: "+newsPosts[index].CommentDraft);
            if((newsPosts[index].Name=="Baka"||newsPosts[index].Name=="巴卡")&&!bakaFriendAccepted&&!bakaRequestDeclined)
            {
                bakaRequestQueued=true;
            }
            ShowNewsDetail(index,null);
        }
        void CloseNewsDetail()
        {
            if(newsDetailRoot==null||newsDetailClosing)return;
            newsDetailClosing=true;
            newsDetailStarted=Time.unscaledTime;
            newsDetailCloseFromPosition=newsDetailRoot.anchoredPosition;
            newsDetailCloseFromSize=newsDetailRoot.sizeDelta;
        }
        void ClearNewsDetail()
        {
            buttons.Remove("Close detail");buttons.Remove("Like post");
            if(newsDetailRoot!=null)
            {
                newsDetailRoot.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(newsDetailRoot.gameObject);
                newsDetailRoot=null;
            }
            if(newsDetailBlocker!=null)
            {
                newsDetailBlocker.SetActive(false);
                UnityEngine.Object.Destroy(newsDetailBlocker);
                newsDetailBlocker=null;
            }
            newsDetailContent=null;selectedNewsPost=-1;newsDetailClosing=false;
        }
        void UpdateNewsDetail()
        {
            if(newsDetailRoot==null)return;
            float raw=Mathf.Clamp01((Time.unscaledTime-newsDetailStarted)/NewsDetailDuration);
            float t=newsDetailClosing?DesktopMotion.Smooth(raw):DesktopMotion.Enter(raw);
            Vector2 iconPosition=new Vector2(newsDetailIconPosition.x,-newsDetailIconPosition.y);
            Vector2 iconSize=newsDetailIconSize;
            newsDetailRoot.sizeDelta=iconSize;
            newsDetailRoot.anchoredPosition=newsDetailClosing?
                Vector2.Lerp(newsDetailCloseFromPosition,iconPosition+Vector2.right*18,t):
                iconPosition+Vector2.right*(18*(1-t));
            if(newsDetailContent!=null)
            {
                float contentT=newsDetailClosing?1-t:t;
                newsDetailContent.alpha=contentT;
                newsDetailContent.interactable=!newsDetailClosing&&raw>=1;
                newsDetailContent.blocksRaycasts=newsDetailContent.interactable;
            }
            if(raw>=1&&newsDetailClosing)
            {
                ClearNewsDetail();
                if(bakaNewsQueued&&!bakaNewsCreated){bakaNewsQueued=false;CreateBakaNews();}
                if(newsCloseButton!=null)newsCloseButton.gameObject.SetActive(true);
                if(newsFeedLayer!=null)newsFeedLayer.SetActive(true);
            }
        }
        public void BeginNewsExit()
        {
            BeginNewsWindow(true);
        }
        void BeginNewsWindow(bool closing)
        {
            if(newsWindowRoot==null)return;
            SetPageAlpha(1);
            newsWindowClosing=closing;newsWindowStarted=Time.unscaledTime;
            newsWindowRestoring=!closing;newsWindowMinimizing=false;
            if(newsWindowContent!=null)newsWindowContent.alpha=1;
        }
        void UpdateNewsWindow()
        {
            if(newsWindowRoot==null)return;
            float duration=newsWindowRestoring?.28f:NewsWindowDuration;
            float raw=Mathf.Clamp01((Time.unscaledTime-newsWindowStarted)/duration);
            float t=Mathf.SmoothStep(0,1,raw);
            Vector2 finalPosition=newsWindowPosition;
            Vector2 finalSize=new Vector2(AppWindowWidth,AppWindowHeight);
            Vector2 taskPosition=TaskbarSlotPosition(FrontendPage.News),taskSize=new Vector2(184,46);
            if(newsWindowClosing||newsWindowMinimizing)
            {
                DesktopMotion.Window(newsWindowRoot,newsWindowGroup,newsWindowStarted,raw,true,true,
                    finalPosition,finalSize,taskPosition,taskSize);
            }
            else if(newsWindowRestoring)
            {
                DesktopMotion.Window(newsWindowRoot,newsWindowGroup,newsWindowStarted,raw,false,newsWindowFromTaskbar,
                    finalPosition,finalSize,taskPosition,taskSize);
            }
            else
            {
                newsWindowRoot.anchoredPosition=finalPosition;
                newsWindowRoot.sizeDelta=finalSize;
                newsWindowRoot.localScale=Vector3.one;
                if(newsWindowGroup!=null)newsWindowGroup.alpha=1;
            }
            if(newsWindowContent!=null)
            {
                newsWindowContent.alpha=1;
                newsWindowContent.interactable=!newsWindowClosing&&!newsWindowMinimizing&&
                    (!newsWindowRestoring||raw>.72f);
                newsWindowContent.blocksRaycasts=newsWindowContent.interactable;
            }
            if(raw>=1)
            {
                if(newsWindowClosing)ClearNewsWindow();
                else if(newsWindowMinimizing)
                {
                    newsWindowMinimizing=false;newsWindowMinimized=true;
                    newsWindowRoot.gameObject.SetActive(false);
                    if(newsWindowBlocker!=null)newsWindowBlocker.SetActive(false);
                    owner.FocusDesktopWindow(FrontmostWindow);
                }
                else if(newsWindowRestoring){newsWindowRestoring=false;newsWindowFromTaskbar=false;}
                else pageAlphaLocked=false;
            }
        }
        public bool FinishNewsExit()
        {
            if(newsWindowClosing)ClearNewsWindow();
            if(bakaRequestQueued&&!bakaFriendAccepted&&!bakaRequestDeclined)
            {
                bakaRequestQueued=false;bakaRequestPending=true;bakaUnreadCount=1;RefreshUnreadIndicators();
                AddNotification("request","好友申请","Baka 想把你添加为好友。",-1,()=>owner.OpenFriendRequestsFromNotification());
                return true;
            }
            return false;
        }
        void ClearNewsWindow()
        {
            buttons.Remove("Close news window");
            buttons.Remove("Minimize news");
            newsCloseButton=null;
            ClearNewsDetail();
            if(newsFeedLayer!=null){newsFeedLayer.SetActive(false);UnityEngine.Object.Destroy(newsFeedLayer);newsFeedLayer=null;}
            var stalePosts=new List<string>();
            foreach(string key in buttons.Keys)if(key.StartsWith("Open news post ",StringComparison.Ordinal))stalePosts.Add(key);
            foreach(string key in stalePosts)buttons.Remove(key);
            if(newsWindowRoot!=null)
            {
                newsWindowRoot.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(newsWindowRoot.gameObject);
                newsWindowRoot=null;
            }
            newsWindowContent=null;
            newsFeedContent=null;
            newsUnreadMarkers.Clear();
            newsWindowMinimized=false;
            newsWindowMinimizing=false;newsWindowRestoring=false;
            newsWindowFromTaskbar=false;
            RemoveTaskbarTask(FrontendPage.News);
            if(newsWindowBlocker!=null){newsWindowBlocker.SetActive(false);UnityEngine.Object.Destroy(newsWindowBlocker);newsWindowBlocker=null;}
        }
        public float PopupTransitionDuration => .48f;
        public void BeginReturnExit()
        {
            if(returnPopup!=null)StartPopup(returnPopup,true);
        }
        public void FinishReturnExit()
        {
            ClearPopup(ref returnPopup);
        }
        PopupMotion CreatePopup(RectTransform parent,string name,float finalX,float finalY,float finalWidth,float finalHeight,
            Vector2 iconPosition,Vector2 iconSize,Action onFinished)
        {
            if(parent==page)parent=artboard;
            var scrim=Box(parent,name+" scrim",0,0,1600,900,new Color(0,0,0,0));
            scrim.raycastTarget=true;
            var scrimGroup=scrim.gameObject.AddComponent<CanvasGroup>();
            var root=Rect(name,parent,iconPosition.x,iconPosition.y,iconSize.x,iconSize.y);
            root.gameObject.AddComponent<RectMask2D>();
            var content=root.gameObject.AddComponent<CanvasGroup>();
            content.alpha=0;
            return new PopupMotion{
                Root=root,Content=content,Scrim=scrimGroup,
                IconPosition=new Vector2(iconPosition.x,-iconPosition.y),IconSize=iconSize,
                FinalPosition=new Vector2(finalX,-finalY),FinalSize=new Vector2(finalWidth,finalHeight),
                ScrimAlpha=.68f,OnFinished=onFinished
            };
        }
        void StartPopup(PopupMotion popup,bool closing)
        {
            if(popup==null||popup.Root==null)return;
            SetPageAlpha(1);
            if(closing&&popup.Settled)popup.FinalPosition=popup.Root.anchoredPosition;
            popup.Started=Time.unscaledTime;popup.Closing=closing;popup.Settled=false;
            if(popup.SlideFromTaskbar)
            {
                popup.IconPosition=popup.FinalPosition+new Vector2(0,-32);
                popup.IconSize=popup.FinalSize;
            }
            if(!closing&&!popup.SlideFromTaskbar&&popup.Root.GetComponentInChildren<WindowDragHandler>()==null)
            {
                var handle=Box(popup.Root,"Window drag handle",0,0,popup.FinalSize.x-64,64,Color.clear);
                handle.raycastTarget=true;
                AddWindowDrag(handle.rectTransform,popup.Root,()=>popup.Settled&&!popup.Closing,
                    position=>popup.FinalPosition=position);
            }

        }
        void UpdatePopup(PopupMotion popup,float duration)
        {
            if(popup==null||popup.Root==null||popup.Settled)return;
            if(popup.SlideFromTaskbar)duration=popup.Closing?.16f:.22f;
            float raw=Mathf.Clamp01((Time.unscaledTime-popup.Started)/Mathf.Max(.05f,duration));
            float t=popup.Closing?DesktopMotion.Smooth(raw):DesktopMotion.Enter(raw);
            DesktopMotion.Window(popup.Root,popup.Content,popup.Started,raw,popup.Closing,!popup.SlideFromTaskbar,
                popup.FinalPosition,popup.FinalSize,popup.IconPosition,popup.IconSize);
            if(popup.Content!=null)
            {
                popup.Content.interactable=!popup.Closing&&raw>=1;
                popup.Content.blocksRaycasts=popup.Content.interactable;
            }
            if(popup.Scrim!=null)popup.Scrim.alpha=popup.ScrimAlpha*(popup.Closing?1-t:t);
            if(raw>=1&&popup.Closing)popup.OnFinished?.Invoke();
            else if(raw>=1)popup.Settled=true;
        }
        void ClearPopup(ref PopupMotion popup)
        {
            if(popup==null)return;
            if(popup.Root!=null)
            {
                var stale=new List<string>();
                foreach(var pair in buttons)
                    if(pair.Value==null||!pair.Value||pair.Value.transform.IsChildOf(popup.Root))
                        stale.Add(pair.Key);
                foreach(string key in stale)buttons.Remove(key);
            }
            if(popup.Root!=null)
            {
                popup.Root.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(popup.Root.gameObject);
            }
            if(popup.Scrim!=null)
            {
                popup.Scrim.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(popup.Scrim.gameObject);
            }
            popup=null;
        }
        public float EnterTransitionDuration => .84f;
        public bool PageSlideActive => pageSlideActive;
        void StartPageSlide(bool reverse)
        {
            if(outgoingPage==null)return;
            SetPageAlpha(1);
            if(pageFade!=null){pageFade.alpha=1;pageFade.interactable=true;pageFade.blocksRaycasts=true;}
            outgoingPage.anchoredPosition=Vector2.zero;
            pageSlideReverse=reverse;
            page.anchoredPosition=new Vector2(0,reverse?900:-900);
            pageSlideStarted=Time.unscaledTime;pageSlideActive=true;
        }
        void UpdatePageSlide()
        {
            if(!pageSlideActive||page==null)return;
            float raw=Mathf.Clamp01((Time.unscaledTime-pageSlideStarted)/EnterTransitionDuration);
            float eased=DesktopMotion.Smooth(raw);
            float offset=900*eased;
            page.anchoredPosition=new Vector2(0,pageSlideReverse?900-offset:-900+offset);
            if(outgoingPage!=null)outgoingPage.anchoredPosition=new Vector2(0,pageSlideReverse?-offset:offset);
            if(raw>=1)
            {
                if(outgoingPage!=null){outgoingPage.gameObject.SetActive(false);UnityEngine.Object.Destroy(outgoingPage.gameObject);outgoingPage=null;}
                page.anchoredPosition=Vector2.zero;pageSlideActive=false;pageAlphaLocked=false;
            }
        }
        public void BeginModePixelate(Texture2D source)
        {
            EndModePixelate();
            const int columns=64,rows=36;
            const float width=1600f/columns,height=900f/rows;
            modePixelOverlay=Rect("Pixel transition overlay",artboard,0,0,1600,900);
            modePixelBlocks=new Image[columns*rows];
            modePixelColors=new Color[modePixelBlocks.Length];
            modePixelTargetColors=new Color[modePixelBlocks.Length];
            modePixelDelays=new float[modePixelBlocks.Length];
            modePixelBasePositions=new Vector2[modePixelBlocks.Length];
            modePixelTargetPositions=new Vector2[modePixelBlocks.Length];
            for(int y=0;y<rows;y++)
            {
                for(int x=0;x<columns;x++)
                {
                    int index=y*columns+x;
                    var block=Box(modePixelOverlay,"Pixel "+index,x*width,y*height,width+.5f,height+.5f,Color.clear);
                    Color sampled=new Color(.02f,.04f,.08f,1);
                    if(source!=null)
                    {
                        int sx=Mathf.Clamp(Mathf.FloorToInt((x+.5f)/columns*source.width),0,source.width-1);
                        int sy=Mathf.Clamp(Mathf.FloorToInt((y+.5f)/rows*source.height),0,source.height-1);
                        sampled=source.GetPixel(sx,sy);
                    }
                    sampled.a=0;
                    block.color=sampled;
                    modePixelBlocks[index]=block;
                    modePixelColors[index]=sampled;
                    modePixelTargetColors[index]=sampled;
                    float wave=x/Mathf.Max(1f,columns-1f)*.65f+y/Mathf.Max(1f,rows-1f)*.35f;
                    modePixelDelays[index]=wave*.52f+Mathf.Repeat(x*12.9898f+y*78.233f,1f)*.06f;
                    var rect=block.rectTransform;
                    modePixelBasePositions[index]=rect.anchoredPosition;
                    modePixelTargetPositions[index]=rect.anchoredPosition;
                    float pattern=Mathf.Repeat(Mathf.Repeat(wave*3.5f,1f)+Mathf.Repeat(x*.19f+y*.07f,1f)*.08f,1f);
                    Color target;
                    if(y<4)target=new Color(.012f,.025f,.055f,.98f);
                    else if(y>rows-5)target=new Color(.008f,.016f,.035f,.98f);
                    else if(pattern>.91f)target=CyberTheme.Cyan*new Color(.75f,.75f,.75f,.82f);
                    else if(pattern>.78f)target=CyberTheme.Purple*new Color(.65f,.65f,.65f,.82f);
                    else target=Color.Lerp(new Color(.02f,.045f,.085f,.98f),
                        new Color(.07f,.13f,.19f,.98f),pattern);
                    target.a=1f;
                    modePixelTargetColors[index]=target;
                }
            }
            modePixelOverlay.SetAsLastSibling();
        }

        public void SetModePixelTargets(Texture2D source)
        {
            if(modePixelBlocks==null||source==null)return;
            const int columns=64,rows=36;
            for(int y=0;y<rows;y++)
            {
                for(int x=0;x<columns;x++)
                {
                    int index=y*columns+x;
                    int sx=Mathf.Clamp(Mathf.FloorToInt((x+.5f)/columns*source.width),0,source.width-1);
                    int sy=Mathf.Clamp(Mathf.FloorToInt((y+.5f)/rows*source.height),0,source.height-1);
                    Color target=source.GetPixel(sx,sy);
                    target.a=1;
                    modePixelTargetColors[index]=target;
                }
            }
        }

        public void SetModePixelate(float value)
        {
            if(modePixelBlocks==null)return;
            value=Mathf.Clamp01(value);
            for(int i=0;i<modePixelBlocks.Length;i++)
            {
                if(modePixelBlocks[i]==null)continue;
                float delay=modePixelDelays[i];
                float local=Mathf.Clamp01((value-delay)/Mathf.Max(.01f,1f-delay));
                Color color=modePixelColors[i];
                color.a=local;
                modePixelBlocks[i].color=color;
                modePixelBlocks[i].rectTransform.localScale=Vector3.one;
            }
        }

        public void SetModePixelReconfigure(float value)
        {
            if(modePixelBlocks==null)return;
            value=Mathf.Clamp01(value);
            for(int i=0;i<modePixelBlocks.Length;i++)
            {
                if(modePixelBlocks[i]==null)continue;
                float delay=modePixelDelays[i];
                float local=Mathf.Clamp01((value-delay)/Mathf.Max(.01f,1f-delay));
                Color color=Color.Lerp(modePixelColors[i],modePixelTargetColors[i],local);
                color.a=1;
                modePixelBlocks[i].color=color;
                var rect=modePixelBlocks[i].rectTransform;
                rect.anchoredPosition=modePixelBasePositions[i];
                rect.localScale=Vector3.one;
            }
        }

        public void SetModePixelDissolve(float value)
        {
            if(modePixelBlocks==null)return;
            value=Mathf.Clamp01(value);
            for(int i=0;i<modePixelBlocks.Length;i++)
            {
                if(modePixelBlocks[i]==null)continue;
                float delay=modePixelDelays[i];
                float local=Mathf.Clamp01((value-delay)/Mathf.Max(.01f,1f-delay));
                Color color=modePixelTargetColors[i];
                color.a=1-local;
                modePixelBlocks[i].color=color;
                modePixelBlocks[i].rectTransform.localScale=Vector3.one;
            }
        }

        public void SetModePixelOverlayVisible(bool visible)
        {
            if(modePixelOverlay!=null)modePixelOverlay.gameObject.SetActive(visible);
        }

        public void BringModePixelateToFront()
        {
            if(modePixelOverlay!=null)modePixelOverlay.SetAsLastSibling();
        }

        public void BringModeGateToFront()
        {
            if(modeGateRoot!=null)modeGateRoot.SetAsLastSibling();
        }

        public void EndModeGate()
        {
            if(modeGateRoot!=null)UnityEngine.Object.Destroy(modeGateRoot.gameObject);
            modeGateRoot=null;
            modeGateBands=null;
            modeGateGroup=null;modeGateMark=null;
            modeGateShade=null;
        }

        public void BeginSignalTransition()
        {
            EndSignalTransition();
            signalTransitionRoot=Rect("Signal transition",artboard,0,0,1600,900);
            signalShade=Box(signalTransitionRoot,"Signal shade",0,0,1600,900,new Color(.006f,.012f,.028f,0));
            signalCore=Box(signalTransitionRoot,"Signal core",0,0,18,18,CyberTheme.Cyan);
            CenterTransitionGraphic(signalCore.rectTransform,18,18,45);
            signalModeLabel=Mono(signalTransitionRoot,"Signal mode label","MODE SELECT",0,0,420,40,22,
                CyberTheme.Cyan,TextAnchor.MiddleCenter);
            CenterTransitionGraphic(signalModeLabel.rectTransform,420,40,0);
            Color labelColor=signalModeLabel.color;labelColor.a=0;signalModeLabel.color=labelColor;
            signalRings=new CyberPanelGraphic[3];
            for(int i=0;i<signalRings.Length;i++)
            {
                var ring=Panel(signalTransitionRoot,"Signal ring "+i,0,0,150+i*95,150+i*95,
                    new Color(.01f,.03f,.06f,.08f),i==0?CyberTheme.Cyan:i==1?CyberTheme.Purple:CyberTheme.Pink);
                ring.outline=true;
                ring.raycastTarget=false;
                CenterTransitionGraphic(ring.rectTransform,150+i*95,150+i*95,45+i*12);
                signalRings[i]=ring;
            }
            signalTransitionRoot.SetAsLastSibling();
        }

        public void SetSignalCollapse(float value)
        {
            if(signalTransitionRoot==null)return;
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(value));
            CenterPageForSignalTransition();
            float pageScale=Mathf.Lerp(1f,.01f,t);
            page.localScale=new Vector3(pageScale,pageScale,1f);
            if(pageFade!=null)pageFade.alpha=Mathf.Lerp(1f,.04f,t);
            if(signalShade!=null)signalShade.color=new Color(.006f,.012f,.028f,t*.96f);
            if(signalCore!=null)
            {
                float coreScale=Mathf.Lerp(.45f,3.4f,t);
                signalCore.rectTransform.localScale=new Vector3(coreScale,coreScale,1f);
                signalCore.color=new Color(1f,1f,1f,Mathf.Lerp(.35f,1f,t));
            }
            if(signalModeLabel!=null)
            {
                Color color=signalModeLabel.color;
                color.a=Mathf.Clamp01((t-.28f)/.5f);
                signalModeLabel.color=color;
                float scale=Mathf.Lerp(.72f,1f,t);
                signalModeLabel.rectTransform.localScale=new Vector3(scale,scale,1f);
            }
            if(signalRings!=null)
            {
                for(int i=0;i<signalRings.Length;i++)
                {
                    if(signalRings[i]==null)continue;
                    float local=Mathf.Clamp01((t-i*.09f)/Mathf.Max(.01f,1f-i*.09f));
                    float scale=Mathf.Lerp(2.6f,.22f,local);
                    signalRings[i].rectTransform.localScale=new Vector3(scale,scale,1f);
                    signalRings[i].rectTransform.Rotate(0,0,-14f*Time.unscaledDeltaTime);
                    Color color=signalRings[i].accent;
                    color.a=local*.7f;
                    signalRings[i].color=new Color(.01f,.03f,.06f,local*.16f);
                    signalRings[i].accent=color;
                }
            }
        }

        public void SetSignalExpand(float value)
        {
            if(signalTransitionRoot==null)return;
            float t=Mathf.Clamp01(value);
            CenterPageForSignalTransition();
            float pageScale=Mathf.Lerp(.01f,1f,t);
            page.localScale=new Vector3(pageScale,pageScale,1f);
            if(pageFade!=null)pageFade.alpha=Mathf.Lerp(.04f,1f,t);
            if(signalShade!=null)signalShade.color=new Color(.006f,.012f,.028f,(1f-t)*.96f);
            if(signalCore!=null)
            {
                float coreScale=Mathf.Lerp(4.2f,.45f,t);
                signalCore.rectTransform.localScale=new Vector3(coreScale,coreScale,1f);
                signalCore.color=new Color(1f,1f,1f,1f-t);
            }
            if(signalModeLabel!=null)
            {
                Color color=signalModeLabel.color;
                color.a=Mathf.Clamp01((1f-t)*1.4f);
                signalModeLabel.color=color;
                float scale=Mathf.Lerp(.82f,1.18f,t);
                signalModeLabel.rectTransform.localScale=new Vector3(scale,scale,1f);
            }
        }

        public void SetSignalRingsExpansion(float value)
        {
            if(signalRings==null)return;
            float t=Mathf.Clamp01(value);
            for(int i=0;i<signalRings.Length;i++)
            {
                if(signalRings[i]==null)continue;
                float scale=Mathf.Lerp(.22f,20f+i*2f,t);
                signalRings[i].rectTransform.localScale=new Vector3(scale,scale,1f);
                signalRings[i].rectTransform.Rotate(0,0,-14f*Time.unscaledDeltaTime);
                float fade=t<.82f?1f:Mathf.Clamp01((1f-t)/.18f);
                Color color=signalRings[i].accent;
                color.a=fade*.52f;
                signalRings[i].color=new Color(.01f,.03f,.06f,fade*.14f);
                signalRings[i].accent=color;
            }
        }

        public void SetSignalRingsRelease(float value)
        {
            if(signalRings==null)return;
            float t=Mathf.Clamp01(value);
            for(int i=0;i<signalRings.Length;i++)
            {
                if(signalRings[i]==null)continue;
                float scale=Mathf.Lerp(2.2f+i*.25f,20f+i*2f,t);
                signalRings[i].rectTransform.localScale=new Vector3(scale,scale,1f);
                signalRings[i].rectTransform.Rotate(0,0,-(14f+i*2f)*Time.unscaledDeltaTime);
                Color color=signalRings[i].accent;
                float fade=t<.82f?1f:Mathf.Clamp01((1f-t)/.18f);
                color.a=fade*.52f;
                signalRings[i].color=new Color(.01f,.03f,.06f,fade*.14f);
                signalRings[i].accent=color;
            }
            if(signalCore!=null)signalCore.color=new Color(1,1,1,0);
            if(signalModeLabel!=null)
            {
                Color label=signalModeLabel.color;
                label.a=(1f-t)*.8f;
                signalModeLabel.color=label;
            }
        }

        public void BringSignalTransitionToFront()
        {
            if(signalTransitionRoot!=null)signalTransitionRoot.SetAsLastSibling();
        }

        public void EndSignalTransition()
        {
            if(signalTransitionRoot!=null)UnityEngine.Object.Destroy(signalTransitionRoot.gameObject);
            signalTransitionRoot=null;signalShade=null;signalCore=null;signalRings=null;signalModeLabel=null;
        }

        void CenterPageForSignalTransition()
        {
            if(page==null)return;
            page.anchorMin=page.anchorMax=new Vector2(.5f,.5f);
            page.pivot=new Vector2(.5f,.5f);
            page.anchoredPosition=Vector2.zero;
            page.sizeDelta=new Vector2(1600,900);
        }

        static void CenterTransitionGraphic(RectTransform rect,float width,float height,float rotation)
        {
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=Vector2.zero;
            rect.sizeDelta=new Vector2(width,height);
            rect.localRotation=Quaternion.Euler(0,0,rotation);
        }

        public void EndModePixelate()
        {
            if(modePixelOverlay!=null)
                UnityEngine.Object.Destroy(modePixelOverlay.gameObject);
            modePixelOverlay=null;
            modePixelBlocks=null;
            modePixelColors=null;
            modePixelTargetColors=null;
            modePixelDelays=null;
            modePixelBasePositions=null;
            modePixelTargetPositions=null;
        }

        public void ShowStoryIntro(string title,string chapter)
        {
            Begin("Story intro","00   /   STORY 0-1",false,.01f);
            MusicArt(page,"Story intro background",0,0,1600,900,0);
            var card=MusicPanel(page,"Story archive cover",340,292,920,316);
            Mono(card.rectTransform,"Story intro chapter",chapter,40,38,840,26,12,DesktopWindowMuted,TextAnchor.MiddleCenter);
            var heading=StoryText(card.rectTransform,"Story intro title",title,40,106,840,76,36,true,DesktopWindowText,TextAnchor.MiddleCenter);
            Fit(heading,24,36);
            Box(card.rectTransform,"Story intro rail",370,216,180,3,CyberTheme.Purple);
            Mono(card.rectTransform,"Story intro status","故事记录   /   即将开始",40,254,840,24,12,DesktopWindowMuted,TextAnchor.MiddleCenter);
            Reveal(page,"Story archive cover",0,12);
        }

        public void ShowStory(StoryRuntimeProjectData story,StoryRuntimeNodeData node,
            StoryRuntimeNodeData speakerNode,string backgroundId,Dictionary<string,string> variables,
            List<string> castIds,bool sceneTransition)
        {
            Begin("Story player","00   /   STORY 0-1",false,sceneTransition?.32f:.01f);
            if(!sceneTransition&&transitionShade!=null)
            {
                transitionShade.color=new Color(0,0,0,0);
                transitionShade.raycastTarget=false;
            }
            var background=StoryRuntimeLoader.FindBackground(story,backgroundId);
            Texture2D backgroundTexture=background==null?null:StoryRuntimeLoader.LoadTexture(background.assetPath);
            var backgroundRect=Rect("Story background",page,0,0,1600,900);
            var backgroundImage=backgroundRect.gameObject.AddComponent<RawImage>();
            backgroundImage.texture=backgroundTexture;
            backgroundImage.color=Color.white;
            backgroundImage.uvRect=backgroundTexture!=null?CoverTexture(backgroundTexture):new Rect(0,0,1,1);
            backgroundImage.raycastTarget=false;

            var topLayer=Rect("Story top layer",page,0,0,1600,92);
            DrawStoryChrome(story,topLayer,sceneTransition);
            bool choicePanel=node.kind==StoryRuntimeNodeKind.Choice;
            var dialoguePanel=CreateStoryDialogue(choicePanel,node.choices==null?0:node.choices.Length,sceneTransition);
            if(node.kind==StoryRuntimeNodeKind.Dialogue)
            {
                dialoguePanel.raycastTarget=true;
                var advanceButton=dialoguePanel.gameObject.AddComponent<Button>();
                advanceButton.targetGraphic=dialoguePanel;
                advanceButton.transition=Selectable.Transition.None;
                // Adding a Selectable while the page is entering can leave its disabled tint behind.
                dialoguePanel.CrossFadeColor(Color.white,0,true,true);
                advanceButton.navigation=new Navigation{mode=Navigation.Mode.None};
                advanceButton.onClick.AddListener(StoryContinue);
            }
            var character=speakerNode==null?null:StoryRuntimeLoader.FindCharacter(story,speakerNode.speakerId);
            var expression=character==null?null:StoryRuntimeLoader.FindExpression(character,speakerNode==null?"":speakerNode.expressionId);
            var visibleCast=castIds==null?new List<string>():new List<string>(castIds);
            if(visibleCast.Count==0&&speakerNode!=null&&!string.IsNullOrEmpty(speakerNode.speakerId))
                visibleCast.Add(speakerNode.speakerId);
            if(visibleCast.Count>0)
            {
                var portraitRoot=Rect("Story portrait group",page,0,0,1600,900);
                storyPortraitGroup=portraitRoot.gameObject.AddComponent<CanvasGroup>();
                storyPortraitRect=portraitRoot;
                storyPortraitTo=portraitRoot.anchoredPosition;
                storyPortraitFrom=storyPortraitTo+Vector2.down*14;
                portraitRoot.anchoredPosition=sceneTransition?storyPortraitFrom:storyPortraitTo;
                storyPortraitGroup.alpha=sceneTransition?0:1;
                var castCharacters=new List<StoryRuntimeCharacterData>();
                var castTextures=new List<Texture2D>();
                var castWidths=new List<float>();
                float maxPortraitWidth=visibleCast.Count==1?1450f:visibleCast.Count==2?1450f:980f;
                float targetHeight=visibleCast.Count==1?1350f:visibleCast.Count==2?1450f:1180f;
                for(int i=0;i<visibleCast.Count;i++)
                {
                    var castCharacter=StoryRuntimeLoader.FindCharacter(story,visibleCast[i]);
                    if(castCharacter==null)continue;
                    string expressionId=speakerNode!=null&&speakerNode.speakerId==castCharacter.id
                        ?speakerNode.expressionId
                        :castCharacter.expressions!=null&&castCharacter.expressions.Length>0
                            ?castCharacter.expressions[0].id:"";
                    var castExpression=StoryRuntimeLoader.FindExpression(castCharacter,expressionId);
                    Texture2D portrait=castExpression==null?null:StoryRuntimeLoader.LoadTexture(castExpression.assetPath);
                    if(portrait==null)continue;
                    float aspect=portrait.width/Mathf.Max(1f,portrait.height);
                    float height=targetHeight;
                    float width=height*aspect;
                    if(width>maxPortraitWidth)
                    {
                        width=maxPortraitWidth;
                        height=width/aspect;
                    }
                    castCharacters.Add(castCharacter);
                    castTextures.Add(portrait);
                    castWidths.Add(width);
                }
                for(int i=0;i<castCharacters.Count;i++)
                {
                    var castCharacter=castCharacters[i];
                    Texture2D portrait=castTextures[i];
                    float width=castWidths[i];
                    float aspect=portrait.width/Mathf.Max(1f,portrait.height);
                    float height=width/aspect;
                    float portraitX;
                    if(castCharacters.Count==1)portraitX=(1600-width)*.5f;
                    else if(castCharacters.Count==2)portraitX=i==0?-250f:500f;
                    else portraitX=-160f+i*(width-70f);
                    float portraitY=castCharacters.Count==1?-40f:castCharacters.Count==2?-70f:20f;
                    var portraitRect=Rect("Story portrait "+castCharacter.id,portraitRoot,portraitX,portraitY,width,height);
                    var portraitImage=portraitRect.gameObject.AddComponent<RawImage>();
                    portraitImage.texture=portrait;
                    portraitImage.uvRect=new Rect(0,0,1,1);
                    portraitImage.raycastTarget=false;
                    bool speaking=speakerNode==null||speakerNode.speakerId==castCharacter.id;
                    portraitImage.color=speaking?Color.white:new Color(.46f,.51f,.60f,1f);
                }
            }
            if(node.kind==StoryRuntimeNodeKind.Dialogue||choicePanel)DrawStoryDialogueShadow();
            dialoguePanel.rectTransform.SetAsLastSibling();
            topLayer.SetAsLastSibling();

            if(node.kind==StoryRuntimeNodeKind.Dialogue)
            {
                string speakerName=character==null?"旁白":character.displayName;
                DrawStoryLine(dialoguePanel.rectTransform,speakerName,ResolveStoryText(node,variables));
            }
            else if(node.kind==StoryRuntimeNodeKind.Choice)
            {
                DrawStoryChoices(dialoguePanel.rectTransform,node);
            }
            else if(node.kind==StoryRuntimeNodeKind.Stage)
            {
                dialoguePanel.gameObject.SetActive(false);
                DrawStoryCommand(story,node,"演奏准备",
                    "关卡  "+node.levelId+"   /   "+(node.autoplay?"自动演奏":"手动演奏"),
                    "开始演奏   →",owner.EnterStoryStage);
            }
            else
            {
                dialoguePanel.gameObject.SetActive(false);
                DrawStoryCommand(story,node,"本章记录结束",
                    string.IsNullOrWhiteSpace(node.text)?"这段记录已经结束。":ResolveStoryText(node,variables),
                    "返回章节   →",owner.ReturnToStoryLevels);
            }
            storyTransitionStarted=sceneTransition?Time.unscaledTime:-1;
        }

        static int GetStoryNodeIndex(StoryRuntimeProjectData story,StoryRuntimeNodeData node)
        {
            if(story==null||story.nodes==null||node==null)return 0;
            for(int i=0;i<story.nodes.Length;i++)if(story.nodes[i]!=null&&story.nodes[i].id==node.id)return i;
            return 0;
        }

        static string FormatStoryVariables(Dictionary<string,string> variables)
        {
            if(variables==null||variables.Count==0)return "NONE";
            var values=new List<string>();
            foreach(var pair in variables)values.Add(pair.Key+" = "+pair.Value);
            return string.Join("  /  ",values.ToArray());
        }

        static string ResolveStoryText(StoryRuntimeNodeData node,Dictionary<string,string> variables)
        {
            if(node==null)return "";
            if(variables!=null&&node.textVariants!=null)
            {
                foreach(var variant in node.textVariants)
                {
                    if(variant==null)continue;
                    if(variables.TryGetValue(variant.variable,out string value)&&value==variant.value)return variant.text;
                }
            }
            return node.text??"";
        }

        static Color ParseStoryColor(string html)
        {
            return ColorUtility.TryParseHtmlString(string.IsNullOrEmpty(html)?"#69A7FF":html,out Color color)
                ? color : CyberTheme.Text;
        }

        Text StoryText(RectTransform parent,string name,string value,float x,float y,float width,float height,
            int size,bool bold,Color color,TextAnchor align=TextAnchor.UpperLeft)
        {
            var text=Label(parent,name,value,x,y,width,height,size,bold,color,align);
            text.font=UiChineseFont;
            return text;
        }

        Button StoryActionButton(RectTransform parent,string name,string caption,float x,float y,float width,
            float height,Action callback,bool primary,Color? background,int fontSize)
        {
            var button=ActionButton(parent,name,caption,x,y,width,height,callback,primary,background,fontSize);
            foreach(var text in button.GetComponentsInChildren<Text>())text.font=UiChineseFont;
            return button;
        }

        public void ShowResults(SongEntry song,PlayResult result,bool storyMode=false)
        {
            Begin("Results page","04   /   SESSION REPORT");
            Mono(page,"Result eyebrow","[ "+result.Mode+" ]",68,145,1050,25,14,CyberTheme.Cyan);
            Label(page,"Result heading","SIGNAL COMPLETE",64,183,1100,76,58,true);
            Label(page,"Result song",song.ShortTitle.ToUpperInvariant()+"  /  "+FormatTime(song.Duration),68,267,1090,37,25,false,CyberTheme.Muted);
            Panel(page,"Score display",64,333,1000,211,CyberTheme.Panel,CyberTheme.Cyan);
            Mono(page,"Score caption","TOTAL SCORE   //   SESSION DATA",90,355,850,26,14,CyberTheme.Cyan);
            Label(page,"Final score",result.Score.ToString("D7"),81,380,940,160,113,true);
            Panel(page,"Rank display",1090,183,446,361,CyberTheme.Panel,CyberTheme.Purple);
            Mono(page,"Rank caption","PERFORMANCE RANK",1120,208,386,24,13,CyberTheme.Purple,TextAnchor.MiddleCenter);
            Label(page,"Grade",result.Grade,1102,231,422,182,132,true,CyberTheme.Lime,TextAnchor.MiddleCenter);
            Box(page,"Rank rail",1160,423,306,1,CyberTheme.Alpha(CyberTheme.Purple,.55f));
            Label(page,"Accuracy",result.Accuracy.ToString("F2")+"%",1120,436,386,65,47,true,CyberTheme.Cyan,TextAnchor.MiddleCenter);
            Mono(page,"Accuracy caption","ACCURACY",1120,509,386,20,11,CyberTheme.Muted,TextAnchor.MiddleCenter);
            Mono(page,"Result disclaimer",result.Automatic?"AUTOPLAY PREVIEW / NOT A PLAYER RECORD":result.Practice?"PRACTICE SEGMENT / NOT A FULL-SONG RECORD":"SESSION ONLY / LOCAL RECORD SAVING IS NOT ENABLED",68,563,1470,25,13,CyberTheme.Muted);
            Metric("PERFECT",result.Perfect.ToString("D3"),64,614,350,112,CyberTheme.Cyan);
            Metric("GOOD",result.Good.ToString("D3"),438,614,350,112,CyberTheme.Lime);
            Metric("MISS",result.Miss.ToString("D3"),812,614,350,112,CyberTheme.Pink);
            Metric("MAX COMBO",result.MaxCombo+" / "+result.Total,1186,614,350,112,CyberTheme.Purple);
            ActionButton(page,"Back to songs",storyMode?"<  RETURN TO STORY":"<  MUSIC ARCHIVE",64,761,395,58,
                storyMode?owner.ReturnToStoryFromResults:(Action)owner.ShowSongs);
            ActionButton(page,"Retry","RECONNECT   /   PLAY AGAIN  >",1062,761,474,58,()=>owner.Play(result.Automatic),true);
        }
        public void ShowLoading(string title)
        {
            Begin("Loading page","00   /   LOADING SIGNAL");
            Artwork(page,1050,266,450,420);
            Mono(page,"Loading tag","[ PREPARING AUDIO + CHART ]",83,276,850,28,16,CyberTheme.Cyan);
            Label(page,"Loading heading","CONNECTING\nTO THE RHYTHM",77,319,978,205,67,true);
            Label(page,"Loading track",title.ToUpperInvariant(),83,552,920,71,30,false,CyberTheme.Muted);
            ActionButton(page,"Cancel loading","<  MUSIC ARCHIVE",83,699,400,65,owner.ShowSongs);
        }
        public void ShowError(string message)
        {
            var panel=Panel(page,"Load error",528,255,1018,572,CyberTheme.Panel,CyberTheme.Pink);panel.raycastTarget=true;
            Mono(panel.rectTransform,"Error code","[ SIGNAL INTERRUPTED ]",35,38,900,30,18,CyberTheme.Pink);
            Label(panel.rectTransform,"Error heading","THIS TRACK COULD NOT LOAD",33,97,932,99,42,true);
            Label(panel.rectTransform,"Error detail",message,35,218,932,201,23,false,CyberTheme.Muted);
            Label(panel.rectTransform,"Error hint","Check its JSON / audio resource, or select another track.",35,459,932,65,23,false,CyberTheme.Text);
        }
        void ShowReturnToTitleConfirmation()
        {
            if(returnPopup!=null)return;
            returnPopup=CreatePopup(page,"Return confirmation",360,190,880,520,
                new Vector2(1394,334),new Vector2(164,108),()=>ClearPopup(ref returnPopup));
            var panel=Panel(returnPopup.Root,"Return confirmation",0,0,880,520,
                new Color(.965f,.95f,.995f,.985f),CyberTheme.Purple);
            panel.raycastTarget=true;
            Box(panel.rectTransform,"Confirmation top rail",0,0,880,6,CyberTheme.Purple);
            Mono(panel.rectTransform,"Confirmation tag","[ NAVIGATION / RETURN ]",46,34,650,24,13,new Color(.42f,.29f,.76f));
            Label(panel.rectTransform,"Confirmation heading","要回到标题页吗？",44,84,790,72,38,true,DesktopWindowText).font=UiChineseFont;
            Label(panel.rectTransform,"Confirmation detail","当前选曲会保留，桌面上打开的窗口将关闭。",48,176,780,72,20,false,DesktopWindowMuted).font=UiChineseFont;
            Box(panel.rectTransform,"Confirmation rail",48,277,784,1,CyberTheme.Alpha(CyberTheme.Purple,.48f));
            var stay=ActionButton(panel.rectTransform,"Cancel return","留在桌面",62,354,330,76,CloseReturnConfirmation,false,new Color(.90f,.88f,.98f,.98f),21);
            SetButtonCaptionColor(stay,new Color(.20f,.12f,.42f));
            ActionButton(panel.rectTransform,"Confirm return","返回标题页",488,354,330,76,owner.ConfirmReturnFromMain,true,null,21);
            Label(panel.rectTransform,"Confirmation footer","留在桌面，可以继续查看当前章节与消息。",48,458,780,20,11,false,DesktopWindowMuted,TextAnchor.MiddleCenter).font=UiChineseFont;
            StartPopup(returnPopup,false);
        }
        void CloseReturnConfirmation()
        {
            if(returnPopup!=null)StartPopup(returnPopup,true);
        }
        void ShowControls()
        {
            if(controlsPopup!=null)return;
            controlsPopup=CreatePopup(page,"Controls popup",418,100,764,700,
                new Vector2(1214,334),new Vector2(164,108),()=>ClearPopup(ref controlsPopup));
            var overlay=Panel(controlsPopup.Root,"Controls overlay",0,0,764,700,new Color(.965f,.95f,.995f,.985f),CyberTheme.Cyan);
            overlay.raycastTarget=true;
            Box(overlay.rectTransform,"Controls beam",0,0,764,6,CyberTheme.Cyan);
            Mono(overlay.rectTransform,"Controls tag","[ PILOT CONTROLS / LOCAL SESSION ]",34,28,520,23,13,new Color(.18f,.58f,.72f));
            Label(overlay.rectTransform,"Controls heading","HOW TO PLAY",31,64,680,60,42,true,new Color(.20f,.12f,.42f));
            Mono(overlay.rectTransform,"Controls intro","The rhythm enters on the spatial paths. Follow the signal, not the screen edges.",34,132,690,24,14,new Color(.43f,.35f,.64f));
            ControlRow(overlay.rectTransform,"TAP","01","PRESS WHEN A NOTE REACHES THE RING",34,185,CyberTheme.Cyan);
            ControlRow(overlay.rectTransform,"DRAG","02","HOLD AND FOLLOW A DRAG NOTE",34,266,CyberTheme.Purple);
            ControlRow(overlay.rectTransform,"PAUSE","SPACE / ESC","OPEN THE PAUSE CONSOLE AT ANY TIME",34,347,CyberTheme.Pink);
            ControlRow(overlay.rectTransform,"PRACTICE","LEFT / RIGHT","SKIP FORWARD OR BACK IN THE CHART",34,428,CyberTheme.Lime);
            Mono(overlay.rectTransform,"Audio settings label","DESKTOP AUDIO",34,516,260,22,13,new Color(.18f,.58f,.72f));
            Label(overlay.rectTransform,"Audio settings hint","请在音乐播放器中切换背景音乐并调整音量。",34,553,696,36,18,false,DesktopWindowText).font=UiChineseFont;
            Mono(overlay.rectTransform,"Controls footnote","AUTOPLAY PREVIEW IS AVAILABLE FROM THE TRACK ARCHIVE.",34,618,450,20,11,new Color(.45f,.37f,.66f));
            ActionButton(overlay.rectTransform,"Close controls","CLOSE",546,610,180,50,CloseControls,true,null,19);
            StartPopup(controlsPopup,false);
        }
        void CloseControls()
        {
            if(controlsPopup!=null)StartPopup(controlsPopup,true);
        }
        void ToggleBackgroundMenu()
        {
            if(backgroundPopup!=null)
            {
                if(!backgroundPopup.Closing)StartPopup(backgroundPopup,true);
                return;
            }
            backgroundPopup=CreatePopup(page,"Background selection",1122,118,438,472,
                new Vector2(1394,200),new Vector2(164,108),()=>ClearPopup(ref backgroundPopup));
            var menu=Panel(backgroundPopup.Root,"Background selection",0,0,438,472,new Color(.965f,.95f,.995f,.985f),CyberTheme.Pink);
            menu.raycastTarget=true;
            Box(menu.rectTransform,"Menu top rail",0,0,438,6,CyberTheme.Pink);
            Mono(menu.rectTransform,"Menu tag","[ VISUAL STAGE ]",25,22,250,21,12,new Color(.18f,.58f,.72f));
            Label(menu.rectTransform,"Menu heading","CHANGE BACKGROUND",23,48,330,42,27,true,new Color(.20f,.12f,.42f));
            var closeBackground=ActionButton(menu.rectTransform,"Close background menu","X",372,20,42,42,CloseBackgroundMenu,false,new Color(.89f,.87f,.97f,.98f),17);
            SetButtonCaptionColor(closeBackground,new Color(.22f,.14f,.46f));
            RoundIcon(closeBackground,16);
            for(int i=0;i<homeBackgrounds.Length;i++)
            {
                int index=i;bool selected=i==homeBackgroundIndex;
                var button=ActionButton(menu.rectTransform,"Select background "+(i+1),"",23,104+i*122,392,104,()=>SelectHomeBackground(index),false,new Color(.92f,.90f,.98f,.98f));
                var buttonPanel=(CyberPanelGraphic)button.targetGraphic;buttonPanel.accent=selected?CyberTheme.Cyan:CyberTheme.Purple;
                RoundIcon(button,14);
                var thumbnail=Rect("Thumbnail",buttonPanel.rectTransform,8,8,190,88).gameObject.AddComponent<RawImage>();
                Texture2D texture=LoadHomeBackground(i);thumbnail.texture=texture;thumbnail.uvRect=texture!=null?CoverTexture(texture):new Rect(0,0,1,1);thumbnail.raycastTarget=false;
                Label(buttonPanel.rectTransform,"Stage label","0-"+(i+1),220,18,148,35,28,true,selected?CyberTheme.Purple:new Color(.22f,.14f,.46f),TextAnchor.MiddleLeft);
                Mono(buttonPanel.rectTransform,"Stage state",selected?"CURRENT":"SELECT",220,58,148,18,10,selected?CyberTheme.Purple:new Color(.47f,.39f,.68f));
                if(selected)Box(buttonPanel.rectTransform,"Selected rail",0,0,5,104,CyberTheme.Cyan);
            }
            StartPopup(backgroundPopup,false);
        }
        void CloseBackgroundMenu()
        {
            if(backgroundPopup==null)return;
            StartPopup(backgroundPopup,true);
        }
        void SelectHomeBackground(int index)
        {
            if(index<0||index>=homeBackgrounds.Length)return;
            BeginHomeBackgroundTransition(index);
            CloseBackgroundMenu();
        }
        Texture2D LoadHomeBackground(int index)
        {
            Texture2D texture=homeBackgrounds[index];
            if(texture!=null)return texture;
            string path="Backgrounds/stage-"+(index+1).ToString("D2");
            Sprite sprite=Resources.Load<Sprite>(path);
            texture=sprite!=null?sprite.texture:Resources.Load<Texture2D>(path);
            homeBackgrounds[index]=texture;
            return texture;
        }
        void ApplyHomeBackground()
        {
            if(homeBackgroundImage==null)return;
            Texture2D texture=LoadHomeBackground(homeBackgroundIndex);
            homeBackgroundImage.texture=texture;
            homeBackgroundImage.enabled=texture!=null;
            homeBackgroundImage.uvRect=texture!=null?CoverTexture(texture,homeBackgroundAspect):new Rect(0,0,1,1);
            if(homeBackgroundImageNext!=null)
            {
                homeBackgroundImageNext.enabled=false;
                homeBackgroundImageNext.color=new Color(1,1,1,0);
            }
            if(homeBackdrop!=null)homeBackdrop.enabled=texture==null;
            if(homeFigureBackdrop!=null)homeFigureBackdrop.SetActive(texture==null);
            if(homeHeroPlaceholder!=null)homeHeroPlaceholder.enabled=texture==null;
            if(homeLevelValue!=null)homeLevelValue.text="0-"+(storyLevelIndex+1);
            if(homeBackgroundCounter!=null)homeBackgroundCounter.text="BACKGROUND  "+(homeBackgroundIndex+1).ToString("D2")+" / 03";
        }
        void BeginHomeBackgroundTransition(int index)
        {
            if(homeBackgroundImageNext==null||homeBackgroundImage==null||index<0||index>=homeBackgrounds.Length)return;
            if(homeBackgroundFading)CompleteHomeBackgroundTransition();
            Texture2D texture=LoadHomeBackground(index);
            if(texture==null)return;
            homeBackgroundImageNext.texture=texture;
            homeBackgroundImageNext.uvRect=CoverTexture(texture,homeBackgroundAspect);
            homeBackgroundImageNext.color=new Color(1,1,1,0);
            homeBackgroundImageNext.enabled=true;
            homeBackgroundIndex=index;
            homeBackgroundFadeStarted=Time.unscaledTime;
            homeBackgroundFading=true;
            if(homeBackdrop!=null)homeBackdrop.enabled=false;
            if(homeFigureBackdrop!=null)homeFigureBackdrop.SetActive(false);
            if(homeHeroPlaceholder!=null)homeHeroPlaceholder.enabled=false;
            if(homeLevelValue!=null)homeLevelValue.text="0-"+(storyLevelIndex+1);
            if(homeBackgroundCounter!=null)homeBackgroundCounter.text="BACKGROUND  "+(homeBackgroundIndex+1).ToString("D2")+" / 03";
        }
        void CompleteHomeBackgroundTransition()
        {
            if(homeBackgroundImage==null||homeBackgroundImageNext==null)return;
            if(homeBackgroundImageNext.texture!=null)
            {
                homeBackgroundImage.texture=homeBackgroundImageNext.texture;
                homeBackgroundImage.uvRect=homeBackgroundImageNext.uvRect;
                homeBackgroundImage.color=Color.white;
                homeBackgroundImage.enabled=true;
            }
            homeBackgroundImageNext.enabled=false;
            homeBackgroundFading=false;
        }
        void UpdateHomeBackgroundFade()
        {
            if(!homeBackgroundFading||homeBackgroundImageNext==null)return;
            float t=DesktopMotion.Smooth(Mathf.Clamp01((Time.unscaledTime-homeBackgroundFadeStarted)/.72f));
            homeBackgroundImageNext.color=new Color(1,1,1,t);
            if(t>=1)CompleteHomeBackgroundTransition();
        }
        Texture2D LoadNewsImage(string resource)
        {
            if(string.IsNullOrEmpty(resource))return null;
            if(newsImages.TryGetValue(resource,out Texture2D cached))return cached;
            string path=resource.Contains("/")?resource:"News/"+resource;
            Texture2D texture=Resources.Load<Texture2D>(path);
            newsImages[resource]=texture;
            return texture;
        }
        static Rect CoverTexture(Texture texture)
        {
            return CoverTexture(texture,1600f/900f);
        }
        static Rect CoverTexture(Texture texture,float targetAspect)
        {
            float imageAspect=texture.width/Mathf.Max(1f,texture.height);
            if(imageAspect>targetAspect)
            {
                float width=targetAspect/imageAspect;
                return new Rect((1f-width)*.5f,0,width,1);
            }
            float height=imageAspect/targetAspect;
            return new Rect(0,(1f-height)*.5f,1,height);
        }
        void ControlRow(RectTransform parent,string name,string key,string description,float x,float y,Color accent)
        {
            var row=Panel(parent,name+" row",x,y,696,63,new Color(.04f,.065f,.13f,.82f),accent);
            Label(row.rectTransform,"Key",key,17,5,112,53,21,true,accent,TextAnchor.MiddleCenter);
            Mono(row.rectTransform,"Name",name,150,10,116,20,12,CyberTheme.Text);
            Mono(row.rectTransform,"Description",description,150,31,520,18,11,CyberTheme.Muted);
        }
        void RefreshTitleVideos()
        {
            if(titleVideos.Count>0)return;
            string directory=Path.Combine(Application.streamingAssetsPath,"TitleVideos");
            if(!Directory.Exists(directory))return;
            try
            {
                foreach(string file in Directory.GetFiles(directory,"*",SearchOption.TopDirectoryOnly))
                {
                    string extension=Path.GetExtension(file).ToLowerInvariant();
                    if(extension==".mp4"||extension==".mov"||extension==".webm"||extension==".m4v")
                        titleVideos.Add(file);
                }
            }
            catch(Exception ex)
            {
                Debug.LogWarning("Title video folder could not be scanned: "+ex.Message);
            }
            if(titleVideos.Count==0)return;
            titleVideos.Sort(StringComparer.OrdinalIgnoreCase);
            titleVideoIndex=UnityEngine.Random.Range(0,titleVideos.Count);
        }
        bool PrepareTitleVideo()
        {
            if(titleVideos.Count==0||titleVideoImage==null)return false;
            if(titleVideoIndex<0||titleVideoIndex>=titleVideos.Count)
                titleVideoIndex=UnityEngine.Random.Range(0,titleVideos.Count);

            titleVideoTexture=new RenderTexture(1920,1080,0,RenderTextureFormat.ARGB32){name="Title video frame"};
            titleVideoTexture.Create();
            titleVideoImage.texture=titleVideoTexture;

            titleVideoPlayer=page.gameObject.AddComponent<VideoPlayer>();
            titleVideoPlayer.playOnAwake=false;
            titleVideoPlayer.source=VideoSource.Url;
            titleVideoPlayer.url=titleVideos[titleVideoIndex];
            titleVideoPlayer.isLooping=true;
            titleVideoPlayer.waitForFirstFrame=true;
            titleVideoPlayer.skipOnDrop=true;
            titleVideoPlayer.renderMode=VideoRenderMode.RenderTexture;
            titleVideoPlayer.targetTexture=titleVideoTexture;
            titleVideoPlayer.aspectRatio=VideoAspectRatio.FitVertically;
            titleVideoPlayer.audioOutputMode=VideoAudioOutputMode.Direct;
            titleVideoPlayer.controlledAudioTrackCount=1;
            titleVideoPlayer.SetDirectAudioMute(0,owner.SmokeMode);
            titleVideoPlayer.prepareCompleted+=HandleTitleVideoPrepared;
            titleVideoPlayer.errorReceived+=HandleTitleVideoError;
            titleVideoPlayer.Prepare();
            return true;
        }
        void HandleTitleVideoPrepared(VideoPlayer source)
        {
            if(titleVideoPlayer!=source)return;
            titleVideoPreparedAt=Time.unscaledTime;
            if(titleVideoImage!=null)
            {
                titleVideoImage.enabled=true;
                titleVideoImage.color=new Color(1,1,1,0);
            }
            source.SetDirectAudioVolume(0,0);
            source.Play();
        }
        void HandleTitleVideoError(VideoPlayer source,string message)
        {
            if(titleVideoPlayer!=source)return;
            Debug.LogWarning("Title video could not be played: "+message);
            if(titleVideoImage!=null)titleVideoImage.enabled=false;
            if(titleBackdrop!=null)titleBackdrop.enabled=true;
        }
        public void SetTitleVideoDucked(bool ducked)
        {
            titleVideoDucked=ducked;
            UpdateTitleVideoVolume();
        }
        void UpdateTitleVideoVolume()
        {
            if(titleVideoPlayer==null)return;
            float visualAlpha=transitionShade!=null?1-transitionShade.color.a:1;
            float duck=titleVideoDucked?.18f:1f;
            titleVideoPlayer.SetDirectAudioVolume(0,Mathf.Clamp01(visualAlpha*duck));
        }
        void SetTransitionShade(float alpha)
        {
            if(transitionShade==null)return;
            Color color=transitionShade.color;
            color.a=Mathf.Clamp01(alpha);
            transitionShade.color=color;
            transitionShade.raycastTarget=color.a>.01f;
        }
        void UpdateTitleVideoReveal()
        {
            if(titleVideoImage==null||titleVideoPreparedAt<0)return;
            float reveal=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-titleVideoPreparedAt)/1.15f));
            titleVideoImage.color=new Color(1,1,1,reveal);
        }
        void ReleaseTitleVideo()
        {
            if(titleVideoPlayer!=null)
            {
                titleVideoPlayer.prepareCompleted-=HandleTitleVideoPrepared;
                titleVideoPlayer.errorReceived-=HandleTitleVideoError;
                titleVideoPlayer.Stop();
                titleVideoPlayer.targetTexture=null;
                UnityEngine.Object.Destroy(titleVideoPlayer);
                titleVideoPlayer=null;
            }
            if(titleVideoTexture!=null)
            {
                titleVideoTexture.Release();
                UnityEngine.Object.Destroy(titleVideoTexture);
                titleVideoTexture=null;
            }
            titleVideoImage=null;
            titleVideoPreparedAt=-1;
            titleVideoDucked=false;
        }
        public void SetPageAlpha(float alpha)
        {
            if(pageFade==null)return;
            pageAlphaLocked=true;
            float visibility=Mathf.Clamp01(alpha);
            if(owner.Page==FrontendPage.Title)loginExitProgress=1-visibility;
            pageFade.alpha=1;
            pageFade.interactable=visibility>.85f;
            pageFade.blocksRaycasts=pageFade.interactable;
            SetTransitionShade(1-visibility);
            UpdateTitleVideoVolume();
        }
        void AddNotification(string kind,string title,string detail,int target,Action onClick)
        {
            if(notificationLayer==null)return;
            notificationLayer.SetAsLastSibling();
            int id=++notificationSequence;
            var button=ActionButton(notificationLayer,"Notification "+id,"",24,88,430,88,
                ()=>{RemoveNotificationById(id);onClick?.Invoke();},false,
                new Color(.025f,.04f,.09f,.98f),16);
            RectTransform rect=(RectTransform)button.transform;
            CanvasGroup group=button.gameObject.AddComponent<CanvasGroup>();
            rect.anchoredPosition=new Vector2(-460,-(88+notifications.Count*100));
            rect.localScale=Vector3.one*.96f;group.alpha=0;
            var panel=(CyberPanelGraphic)button.targetGraphic;panel.cut=16;
            panel.accent=kind=="news"?CyberTheme.Cyan:kind=="request"?CyberTheme.Pink:CyberTheme.Purple;
            Label(panel.rectTransform,"Notification kind",kind=="news"?"N":kind=="request"?"+":"F",16,18,44,52,25,true,
                kind=="news"?CyberTheme.Cyan:kind=="request"?CyberTheme.Pink:CyberTheme.Purple,TextAnchor.MiddleCenter);
            var heading=Label(panel.rectTransform,"Notification title",title,72,14,340,28,17,true);
            heading.font=UiChineseFont;
            Fit(heading,12,17);
            var copy=Label(panel.rectTransform,"Notification detail",Excerpt(detail,52),72,44,340,34,13,false,CyberTheme.Muted);
            copy.font=UiChineseFont;
            copy.lineSpacing=.9f;
            var item=new NotificationItem{Root=button.gameObject,Rect=rect,Group=group,Kind=kind,Target=target,
                ExpiresAt=Time.unscaledTime+1.5f,SpawnedAt=Time.unscaledTime,TargetPosition=new Vector2(24,-(88+notifications.Count*100)),OnClick=onClick};
            notifications.Add(item);
            RefreshNotificationStack();
            owner.PlayNotificationSound();
        }
        void UpdateNotifications()
        {
            if(notificationLayer==null)return;
            float now=Time.unscaledTime;
            for(int i=notifications.Count-1;i>=0;i--)
            {
                NotificationItem item=notifications[i];
                if(item.Root==null){notifications.RemoveAt(i);continue;}
                if(!item.Closing&&now>=item.ExpiresAt){item.Closing=true;item.CloseStarted=now;}
                if(item.Closing)
                {
                    float closeT=Mathf.Clamp01((now-item.CloseStarted)/.22f);
                    if(!item.ExitPosition.HasValue)item.ExitPosition=item.Rect.anchoredPosition;
                    float ease=DesktopMotion.Smooth(closeT);
                    item.Rect.anchoredPosition=Vector2.Lerp(item.ExitPosition.Value,item.ExitPosition.Value+Vector2.left*44,ease);
                    item.Group.alpha=1-ease;
                    if(closeT>=1f)
                    {
                        UnityEngine.Object.Destroy(item.Root);
                        notifications.RemoveAt(i);
                    }
                }
                else
                {
                    float enterT=DesktopMotion.Enter(Mathf.Clamp01((now-item.SpawnedAt)/.32f));
                    Vector2 target=Vector2.Lerp(item.TargetPosition+Vector2.left*44,item.TargetPosition,enterT);
                    if(now-item.SpawnedAt<.32f)item.Rect.anchoredPosition=target;
                    else item.Rect.anchoredPosition=Vector2.SmoothDamp(item.Rect.anchoredPosition,target,ref item.ReflowVelocity,.16f,10000,Time.unscaledDeltaTime);
                    item.Group.alpha=enterT;
                    item.Rect.localScale=Vector3.one*Mathf.Lerp(.96f,1f,enterT);
                }
            }
            RefreshNotificationStack();
        }
        void RefreshNotificationStack()
        {
            if(notificationLayer==null)return;
            notificationLayer.SetAsLastSibling();
            for(int i=0;i<notifications.Count;i++)
            {
                if(notifications[i].Root==null)continue;
                notifications[i].TargetPosition=new Vector2(24,-(88+i*100));
            }
        }
        void RemoveNotificationById(int id)
        {
            for(int i=0;i<notifications.Count;i++)
            {
                if(notifications[i].Root!=null&&notifications[i].Root.name=="Notification "+id)
                {
                    if(!notifications[i].Closing){notifications[i].Closing=true;notifications[i].CloseStarted=Time.unscaledTime;}
                    return;
                }
            }
        }
        void RemoveNotifications(string kind,int target)
        {
            for(int i=0;i<notifications.Count;i++)
            {
                if(notifications[i].Kind==kind&&(target<0||notifications[i].Target==target))
                {
                    if(notifications[i].Root!=null&&!notifications[i].Closing){notifications[i].Closing=true;notifications[i].CloseStarted=Time.unscaledTime;}
                }
            }
            RefreshNotificationStack();
        }
        public void CloseTransientWindows()
        {
            ClearFriendWindow();ClearNewsWindow();
            ClearPopup(ref returnPopup);ClearPopup(ref controlsPopup);ClearPopup(ref startMenuPopup);
        }
        public void OpenNewsPostFromNotification(int index)
        {
            if(index>=0&&index<newsPosts.Count)ShowNewsDetail(index,null);
        }
        public void OpenFriendRequestsFromNotification()
        {
            if(friendRequestPanel==null)ToggleFriendRequests();
        }
        public void OpenFriendChatFromNotification(int friendIndex)
        {
            if(friendIndex==1)OpenBakaChat();else OpenFriendChat();
        }
        public void Hide()
        {
            ReleaseTitleVideo();
            canvas.gameObject.SetActive(false);
        }
        public bool Invoke(string name)
        {
            if(name=="Back to main"&&owner.Page==FrontendPage.Friends)name="Close friends window";
            else if(name=="Back to main"&&owner.Page==FrontendPage.News)name="Close news window";
            if(!buttons.TryGetValue(name,out var button)||!button.IsActive()||!button.interactable)return false;
            button.onClick.Invoke();return true;
        }
        public void UseCaptureCamera(Camera camera,bool preserveTransition=false)
        {
            canvas.renderMode=camera==null?RenderMode.ScreenSpaceOverlay:RenderMode.ScreenSpaceCamera;
            canvas.worldCamera=camera;canvas.planeDistance=.5f;
            if(camera!=null&&!preserveTransition){if(pageFade!=null)pageFade.alpha=1;SetTransitionShade(0);} // Deterministic screenshot, without waiting for entry fade.
            if(camera!=null&&!preserveTransition && titleCtaGroup!=null){titleCtaGroup.alpha=1;titleCtaGroup.interactable=true;titleCtaGroup.blocksRaycasts=true;}
        }
        void Metric(string name,string value,float x,float y,float width,float height,Color color,string unit="")
        {
            var p=Panel(page,name+" panel",x,y,width,height,CyberTheme.Panel,color).rectTransform;
            Mono(p,name+" label",name,16,12,width-32,21,12,CyberTheme.Muted);
            var t=Label(p,name+" value",value,14,36,width-28,height-38,42,true,color);Fit(t,23,42);
            if(unit.Length>0)Mono(p,"Unit",unit,width-65,53,50,22,12,CyberTheme.Muted,TextAnchor.MiddleRight);
        }
        static RectTransform Rect(string name,RectTransform parent,float x,float y,float width,float height)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);return r;
        }
        static Image Box(RectTransform parent,string name,float x,float y,float width,float height,Color color)
        {
            var r=Rect(name,parent,x,y,width,height);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        CyberPanelGraphic Panel(RectTransform parent,string name,float x,float y,float width,float height,Color color,Color accent)
        {
            var p=Rect(name,parent,x,y,width,height).gameObject.AddComponent<CyberPanelGraphic>();p.color=color;p.accent=accent;p.pixelStyle=mainUiPixelTheme;
            p.pixelChrome=mainUiPixelTheme&&(name.IndexOf("title bar",StringComparison.OrdinalIgnoreCase)>=0||name=="Main application window"||name.EndsWith("window frame",StringComparison.OrdinalIgnoreCase));
            p.raycastTarget=false;return p;
        }
        Text Label(RectTransform parent,string name,string value,float x,float y,float width,float height,int size,bool bold=false,Color? color=null,TextAnchor align=TextAnchor.UpperLeft)
        {
            var t=Rect(name,parent,x,y,width,height).gameObject.AddComponent<Text>();
            t.font=mainUiPixelTheme?CyberTheme.Pixel:(bold?CyberTheme.Bold:CyberTheme.Regular);t.fontSize=size;t.text=value;t.color=color??CyberTheme.Text;
            t.alignment=align;t.raycastTarget=false;t.supportRichText=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;
            if(mainUiPixelTheme&&bold&&size>=26)
            {
                var shadow=t.gameObject.AddComponent<Shadow>();
                shadow.effectColor=new Color(.24f,.13f,.53f,.34f);shadow.effectDistance=new Vector2(2,-2);
            }
            return t;
        }
        Text Mono(RectTransform parent,string name,string value,float x,float y,float width,float height,int size,Color color,TextAnchor align=TextAnchor.UpperLeft)
        {
            var t=Label(parent,name,value,x,y,width,height,size,false,color,align);t.font=mainUiPixelTheme?CyberTheme.Pixel:CyberTheme.Mono;return t;
        }
        static void Fit(Text t,int min,int max){t.resizeTextForBestFit=true;t.resizeTextMinSize=min;t.resizeTextMaxSize=max;}
        Button ActionButton(RectTransform parent,string name,string caption,float x,float y,float width,float height,Action callback,bool primary=false,Color? background=null,int fontSize=23)
        {
            var image=Panel(parent,name,x,y,width,height,background??(primary?CyberTheme.Cyan:CyberTheme.Panel),primary?CyberTheme.Cyan:CyberTheme.Purple);image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(.77f,.84f,1);colors.pressedColor=new Color(.55f,.69f,.85f);colors.fadeDuration=.1f;button.colors=colors;
            if(mainUiPixelTheme)
            {
                colors=button.colors;colors.highlightedColor=new Color(.97f,.95f,1f);
                colors.pressedColor=new Color(.83f,.77f,.94f);colors.selectedColor=colors.highlightedColor;
                colors.fadeDuration=.16f;button.colors=colors;
                button.gameObject.AddComponent<DesktopControlMotion>().HoverSound=()=>owner.PlayUiHover(name);
            }
            button.navigation=new Navigation{mode=Navigation.Mode.None};
            button.onClick.AddListener(()=>{
                float now=Time.unscaledTime;
                if(owner.UiTransitionLocked)return;
                var desktopFocus=button.GetComponentInParent<WindowFocusHandler>();
                if(desktopFocus!=null)desktopFocus.Focus?.Invoke();
                if(buttonClickBlockedUntil.TryGetValue(name,out float blockedUntil)&&now<blockedUntil)return;
                float throttle=name.StartsWith("Like post",StringComparison.Ordinal)?.34f:
                    name.StartsWith("Post comment",StringComparison.Ordinal)?.42f:
                    IsNavigationButton(name)?.42f:.16f;
                buttonClickBlockedUntil[name]=now+throttle;
                owner.PlayUiSound(name);callback();
            });buttons[name]=button;
            if(caption.Length>0)Label(image.rectTransform,"Caption",caption,16,0,width-32,height,fontSize,true,primary?CyberTheme.Background:CyberTheme.Text,TextAnchor.MiddleCenter);
            return button;
        }

        static bool IsNavigationButton(string name)
        {
            return name.StartsWith("Open ",StringComparison.Ordinal)||
                name.StartsWith("Back",StringComparison.Ordinal)||
                name.StartsWith("Close",StringComparison.Ordinal)||
                name.StartsWith("Enter ",StringComparison.Ordinal)||
                name.StartsWith("Return",StringComparison.Ordinal)||
                name.StartsWith("Select ",StringComparison.Ordinal)||
                name.StartsWith("Accept ",StringComparison.Ordinal);
        }
        static void RoundIcon(Button button,float radius)
        {
            var panel=button.targetGraphic as CyberPanelGraphic;
            if(panel==null)return;
            panel.rounded=true;
            panel.cornerRadius=radius;
        }
        static void SetButtonCaptionColor(Button button,Color color)
        {
            foreach(Text text in button.GetComponentsInChildren<Text>())text.color=color;
        }
        static CyberHomeBackdropGraphic Backdrop(RectTransform parent)
        {
            var backdrop=Rect("Lobby city backdrop / original vector scene",parent,0,0,1600,900).gameObject.AddComponent<CyberHomeBackdropGraphic>();
            backdrop.raycastTarget=false;return backdrop;
        }
        static void BackgroundFigure(RectTransform parent,string name,float x,float y,float width,float height,float alpha)
        {
            var root=Rect(name,parent,x,y,width,height);
            root.gameObject.AddComponent<CyberIdolGraphic>().raycastTarget=false;
            var group=root.gameObject.AddComponent<CanvasGroup>();
            group.alpha=alpha;group.interactable=false;group.blocksRaycasts=false;
        }
        static void Diamond(RectTransform parent,float x,float y,float radius,Color color)
        {
            var image=Box(parent,"Signal diamond",x-radius,y-radius,radius*2,radius*2,color);
            image.rectTransform.localRotation=Quaternion.Euler(0,0,45);
        }
        static void Artwork(RectTransform parent,float x,float y,float width,float height)
        {
            Rect("Signal core / original vector album art",parent,x,y,width,height).gameObject.AddComponent<MenuArtwork>().raycastTarget=false;
        }
        void Scanner(float x,float y,float width,float height)
        {
            scan=Box(page,"Slow scanner / decorative",x,y,width,1,CyberTheme.Alpha(CyberTheme.Cyan,.15f)).rectTransform;scanStart=y;scanHeight=height;
        }
        static void AddWindowDrag(RectTransform handle,RectTransform target,Func<bool> canDrag,
            Action<Vector2> moved,Action focus=null)
        {
            var drag=handle.gameObject.AddComponent<WindowDragHandler>();
            drag.Target=target;drag.CanDrag=canDrag;drag.Moved=moved;drag.Focus=focus;
        }
        static string FormatTime(double time)=>((int)time/60).ToString("D2")+":"+((int)time%60).ToString("D2");
    }

    internal sealed class WindowFocusHandler : MonoBehaviour,IPointerDownHandler
    {
        public Action Focus;
        public void OnPointerDown(PointerEventData eventData)
        {
            if(eventData.button==PointerEventData.InputButton.Left)Focus?.Invoke();
        }
    }

    internal sealed class WindowDragHandler : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        public RectTransform Target;
        RectTransform parent;
        public Func<bool> CanDrag;
        public Action<Vector2> Moved;
        public Action Focus;
        Vector2 pointerStart,positionStart;

        public void OnBeginDrag(PointerEventData eventData)
        {
            parent=null;
            if(eventData.button!=PointerEventData.InputButton.Left||Target==null||
                (CanDrag!=null&&!CanDrag()))return;
            parent=Target.parent as RectTransform;
            if(parent==null)return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,eventData.position,
                eventData.pressEventCamera,out pointerStart)){parent=null;return;}
            positionStart=Target.anchoredPosition;
            if(Focus!=null)Focus();else Target.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if(Target==null||parent==null||(CanDrag!=null&&!CanDrag()))return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,eventData.position,
                eventData.pressEventCamera,out Vector2 pointer))return;
            Vector2 next=positionStart+pointer-pointerStart;
            next.x=Mathf.Clamp(next.x,0f,Mathf.Max(0f,parent.rect.width-Target.rect.width));
            float bottomInset=parent.rect.height>=900?72:0;
            next.y=Mathf.Clamp(next.y,-Mathf.Max(0f,parent.rect.height-bottomInset-Target.rect.height),0f);
            Target.anchoredPosition=next;
            Moved?.Invoke(next);
        }
        public void OnEndDrag(PointerEventData eventData){parent=null;}
    }
}
