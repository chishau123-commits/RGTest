using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace GeometryRhythm
{
    /// <summary>Cyberpunk frontend presentation. A safe-area fitted 1600x900 canvas;
    /// it never changes render resolution, note geometry, clock or judgement coordinates.</summary>
    public sealed class RhythmFrontendView
    {
        readonly RhythmFrontend owner;
        readonly string gameTitle;
        readonly Canvas canvas;
        readonly RectTransform artboard;
        readonly Dictionary<string,Button> buttons=new Dictionary<string,Button>();
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
        float homeBackgroundFadeStarted;
        bool homeBackgroundFading;
        Text homeLevelValue,homeBackgroundCounter;
        GameObject homeFigureBackdrop;
        CyberIdolGraphic homeHeroPlaceholder;
        CyberHomeBackdropGraphic homeBackdrop;
        CyberTitleGraphic titleBackdrop;
        VideoPlayer titleVideoPlayer;
        RenderTexture titleVideoTexture;
        RawImage titleVideoImage;
        CanvasGroup titleCtaGroup;
        float titleCtaStarted;
        float titleVideoPreparedAt=-1;
        int titleVideoIndex=-1;
        bool titleVideoDucked;
        PopupMotion returnPopup,controlsPopup,backgroundPopup;
        RectTransform newsWindowRoot;
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
        Image newsUnreadDot,friendUnreadDot;
        Image tnelisUnreadMarker,friendRequestUnreadDot;
        Image bakaRequestMarker,bakaUnreadMarker;
        Text friendTypingText;
        float friendWindowStarted;
        float lastFriendClickTime;
        bool friendWindowClosing;
        float friendTypingUntil,friendNextMessageAt;
        bool friendTyping,tnelisChatOpen;
        int tnelisUnreadCount=1;
        int bakaUnreadCount;
        readonly List<FriendChatMessage> tnelisMessages=new List<FriendChatMessage>();
        readonly Queue<FriendChatMessage> tnelisPending=new Queue<FriendChatMessage>();
        readonly List<FriendChatMessage> bakaMessages=new List<FriendChatMessage>();
        readonly Queue<FriendChatMessage> bakaPending=new Queue<FriendChatMessage>();
        int tnelisDialogueStep;
        int activeFriendIndex;
        bool bakaRequestPending,bakaFriendAccepted,bakaNewsCreated,bakaRequestDeclined;
        int bakaNewsIndex=-1;
        bool tnelisConversationComplete,bakaNewsQueued,bakaRequestQueued;
        Vector2 newsDetailCloseFromPosition,newsDetailCloseFromSize;
        int selectedNewsPost=-1;
        bool newsUnread=true,tnelisNewsCreated;
        readonly List<bool> newsLiked=new List<bool>{false,false};
        readonly List<bool> newsUnreadItems=new List<bool>{true,true};
        readonly List<int> newsCommentStep=new List<int>{0,0};
        readonly List<List<string>> newsCommentMessages=new List<List<string>>{new List<string>(),new List<string>()};
        const float NewsIconX=140,NewsIconY=827,NewsIconWidth=190,NewsIconHeight=54,NewsWindowDuration=.64f;
        const float NewsDetailDuration=.52f;
        const float FriendsWindowDuration=.62f;
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
        RectTransform modeLoadingCursor;
        RectTransform modeLoadingScan;
        RectTransform modePixelOverlay;
        Image[] modePixelBlocks;
        Color[] modePixelColors;
        Color[] modePixelTargetColors;
        float[] modePixelDelays;
        Vector2[] modePixelBasePositions;
        Vector2[] modePixelTargetPositions;
        RectTransform modeGateRoot;
        CyberPanelGraphic[] modeGateBands;
        Vector2[] modeGateStartPositions;
        Vector2[] modeGateTargetPositions;
        float[] modeGateDelays;
        float[] modeGateDurations;
        Image modeGateShade;
        public int HomeBackgroundIndex => homeBackgroundIndex;
        public bool HomeBackgroundLoaded => homeBackgroundImage!=null && homeBackgroundImage.texture!=null;
        public int ButtonCount => buttons.Count;
        public bool TitleUsesIndependentArtwork => titleBackdrop!=null && homeBackgroundImage==null
            && homeBackdrop==null && homeFigureBackdrop==null && homeHeroPlaceholder==null;
        public bool TitleVideoAvailable => titleVideoPlayer!=null && !string.IsNullOrEmpty(titleVideoPlayer.url);
        public bool TitleVideoPlaying => titleVideoPlayer!=null && titleVideoPlayer.isPlaying;
        public float NewsTransitionDuration => NewsWindowDuration;
        public float NewsDetailTransitionDuration => NewsDetailDuration;
        public float FriendTransitionDuration => FriendsWindowDuration;
        public bool NewsPostLiked(int index)=>index>=0&&index<newsLiked.Count&&newsLiked[index];
        public int NewsPostCount=>newsPosts.Count;
        public bool NewsUnread=>newsUnread;
        public bool FriendReplyReady=>!friendTyping&&tnelisPending.Count==0&&tnelisDialogueStep<2;
        public bool FriendConversationComplete=>tnelisConversationComplete&&!friendTyping&&tnelisPending.Count==0;

        sealed class NewsPost
        {
            public string Name,Handle,Time,Title,Body,Tag,ImageCaption,ImageResource,CommentPrompt,CommentDraft;
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
            public bool Closing;
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
            public Vector2 TargetPosition;
            public bool Closing;
            public Action OnClick;
        }

        readonly List<NewsPost> newsPosts=new List<NewsPost>
        {
            new NewsPost{
                Name="NYX_07",Handle="@saltline",Time="02:14 / SECTOR 09",
                Title="THE RAIN REMEMBERS EVERY SIGNAL",
                Body="The rain is carrying a new signal through the lower blocks. I recorded three notes before the transmitter went dark. Does anyone know the frequency?",
                Tag="#signal #rain #lowerblocks",ImageCaption="RAIN / SIGNAL CAPTURE",HasImage=true,ImageResource="news-01",
                CommentPrompt="I heard the same 147 BPM pulse.",CommentDraft="I heard the same 147 BPM pulse near the lower blocks. It was moving toward Sector 09.",
                Likes=1840,
                Comments=new[]{"WIREFOX: I heard it too. It repeats at 147 BPM.","NULL_CHOIR: Send coordinates. I can run a trace."}
            },
            new NewsPost{
                Name="K-01",Handle="@night_shift",Time="01:48 / NIGHT BUS",
                Title="03:00 / NIGHT BUS ROUTE 4",
                Body="District 4's old repeater is awake. If you ride the 03:00 line, listen after the tunnel. The city is still talking.",
                Tag="#nightbus #district4",ImageCaption="NIGHT LINE / SECTOR 04",HasImage=true,ImageResource="news-02",
                CommentPrompt="The tunnel echo matched the route.",CommentDraft="The tunnel echo matched the route timing. I logged the second pulse near the old station.",
                Likes=932,
                Comments=new[]{"SEVEN_V: The tunnel blocks my receiver, but I saw the pulse.","MIRA.EXE: Save it before the curfew sweep."}
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
            // Tiny, non-flashing presentation animation. No per-frame artwork mesh rebuild.
            if(pageFade!=null && transitionShade!=null && !pageAlphaLocked)
            {
                float t=Mathf.Clamp01((Time.unscaledTime-pageStarted)/pageFadeDuration);
                float visibility=Mathf.SmoothStep(0,1,t);
                SetTransitionShade(1-visibility);
                pageFade.interactable=visibility>.85f;
                pageFade.blocksRaycasts=pageFade.interactable;
            }
            if(titleCtaGroup!=null)
            {
                float ctaT=Mathf.Clamp01((Time.unscaledTime-titleCtaStarted-.9f)/.75f);
                titleCtaGroup.alpha=Mathf.SmoothStep(0,1,ctaT);
                titleCtaGroup.interactable=titleCtaGroup.alpha>.85f;
                titleCtaGroup.blocksRaycasts=titleCtaGroup.interactable;
            }
            UpdateTitleVideoVolume();
            UpdateTitleVideoReveal();
            UpdateNewsWindow();
            UpdateNewsDetail();
            UpdateFriendWindow();
            UpdatePageSlide();
            UpdatePopup(returnPopup,.48f);
            UpdatePopup(controlsPopup,.48f);
            UpdatePopup(backgroundPopup,.48f);
            UpdateNotifications();
            UpdateHomeBackgroundFade();
            UpdateStoryTransition();
            UpdateStoryTyping();
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
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-storyTransitionStarted)/.32f));
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
        void Begin(string name,string section,bool chrome=true,float fadeDuration=.18f,bool preservePrevious=false,float startY=0)
        {
            ReleaseTitleVideo();
            ClearFriendWindow();
            ClearNewsWindow();
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
            transitionShade=Box(artboard,"Transition shade",0,0,1600,900,Color.black);
            transitionShade.raycastTarget=true;
            homeBackdrop=null;homeBackgroundImage=null;homeBackgroundImageNext=null;
            homeBackgroundFading=false;
            homeFigureBackdrop=null;homeHeroPlaceholder=null;
            homeLevelValue=null;homeBackgroundCounter=null;titleBackdrop=null;
            titleVideoImage=null;titleCtaGroup=null;
            storyPortraitGroup=null;storyDialogueGroup=null;storyPortraitRect=null;storyDialogueRect=null;
            storyTransitionStarted=-1;
            storyLineText=null;storyFullLine="";storyTypingIndex=0;storyTyping=false;
            storyNextCharAt=0;storyLastClickAt=-1;storySkipClickUntil=-1;
            newsUnreadDot=null;friendUnreadDot=null;
            ClearPopup(ref returnPopup);ClearPopup(ref controlsPopup);
            ClearPopup(ref backgroundPopup);
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
        public void ShowTitle()
        {
            Begin("Title screen","00   /   INITIALIZE",false,3f);
            titleBackdrop=Rect("Title-only signal artwork",page,0,0,1600,900).gameObject.AddComponent<CyberTitleGraphic>();
            titleBackdrop.raycastTarget=false;
            titleVideoImage=Rect("Title video",page,0,0,1600,900).gameObject.AddComponent<RawImage>();
            titleVideoImage.color=Color.white;titleVideoImage.raycastTarget=false;titleVideoImage.enabled=false;
            RefreshTitleVideos();
            if(PrepareTitleVideo())titleBackdrop.enabled=false;

            // Cinematic letterboxing also serves as the only title-screen interaction area.
            Box(page,"Cinema top bar",0,0,1600,116,Color.black);
            string displayTitle=gameTitle.Replace('\n',' ').ToUpperInvariant();
            var futureTitle=Label(page,"Future game title",displayTitle,180,18,1240,74,52,
                true,Color.white,TextAnchor.MiddleCenter);
            futureTitle.font=CyberTheme.FutureTitle;Fit(futureTitle,32,60);
            Box(page,"Future title left rail",330,88,220,1,new Color(1,1,1,.34f));
            Box(page,"Future title right rail",1050,88,220,1,new Color(1,1,1,.34f));
            var enter=ActionButton(page,"Enter game","",0,738,1600,162,owner.EnterGame,false,Color.black,18);
            var enterPanel=(CyberPanelGraphic)enter.targetGraphic;enterPanel.cut=0;enterPanel.outline=false;
            titleCtaGroup=enter.gameObject.AddComponent<CanvasGroup>();
            titleCtaGroup.alpha=0;titleCtaGroup.interactable=false;titleCtaGroup.blocksRaycasts=false;
            titleCtaStarted=Time.unscaledTime;
            Box(enterPanel.rectTransform,"CTA hairline",420,22,760,1,new Color(1,1,1,.22f));
            var startChinese=Label(enterPanel.rectTransform,"Start game Chinese","开始游戏",0,30,1600,58,38,
                false,Color.white,TextAnchor.MiddleCenter);
            startChinese.font=CyberTheme.Chinese;startChinese.fontStyle=FontStyle.Bold;
            Mono(enterPanel.rectTransform,"Start game English","Game Start",0,93,1600,24,14,
                new Color(1,1,1,.68f),TextAnchor.MiddleCenter);
        }
        public void ShowMain(float fadeDuration=.55f,bool slideFromSongs=false,int storyLevel=0)
        {
            storyLevelIndex=Mathf.Max(0,storyLevel);
            Begin("Main interface","01   /   MAIN MENU",false,Mathf.Max(.18f,fadeDuration),slideFromSongs,slideFromSongs?-900:0);
            homeBackdrop=Backdrop(page);
            homeBackgroundImage=Rect("Stage background",page,0,0,1600,900).gameObject.AddComponent<RawImage>();
            homeBackgroundImage.color=Color.white;
            homeBackgroundImage.raycastTarget=false;
            homeBackgroundImageNext=Rect("Next stage background",page,0,0,1600,900).gameObject.AddComponent<RawImage>();
            homeBackgroundImageNext.color=new Color(1,1,1,0);
            homeBackgroundImageNext.raycastTarget=false;
            homeBackgroundImageNext.enabled=false;

            // Full-width top canopy and bright rim restore the framing visible in the reference.
            Box(page,"Top violet rail",0,0,1600,17,new Color(.34f,.04f,.62f,.98f));
            Box(page,"Top pink rail",0,17,1600,4,new Color(.93f,.22f,.68f,.96f));
            var topLeftCanopy=Panel(page,"Top left canopy",0,0,568,42,new Color(.20f,.035f,.43f,.96f),new Color(.88f,.24f,.72f));
            topLeftCanopy.cut=28;
            var topRightCanopy=Panel(page,"Top right canopy",1032,0,568,42,new Color(.20f,.035f,.43f,.96f),new Color(.45f,.31f,.95f));
            topRightCanopy.cut=28;
            Box(page,"Top edge highlight",0,15,1600,2,CyberTheme.Alpha(CyberTheme.Cyan,.38f));

            // Layout follows the supplied reference: a narrow top canopy, a full-bleed
            // character stage, and one dominant entry bar across the bottom.
            var topLeft=ActionButton(page,"Return to title","<",12,9,68,48,ShowReturnToTitleConfirmation,false,new Color(.10f,.025f,.22f,.94f),29);
            RoundIcon(topLeft,16);

            var centerBanner=Panel(page,"Track level banner",596,0,408,68,new Color(.97f,.72f,.06f,.98f),CyberTheme.Cyan);
            centerBanner.rounded=true;centerBanner.cornerRadius=22;
            Diamond(centerBanner.rectTransform,38,34,7,new Color(.92f,.60f,.02f));
            Diamond(centerBanner.rectTransform,370,34,7,new Color(.92f,.60f,.02f));
            Mono(centerBanner.rectTransform,"Banner tag","STAGE",74,6,260,15,10,new Color(.25f,.10f,.31f),TextAnchor.MiddleCenter);
            homeLevelValue=Label(centerBanner.rectTransform,"Banner value","0-1",74,18,260,42,31,true,new Color(.12f,.055f,.22f),TextAnchor.MiddleCenter);
            Box(page,"Banner cable",716,68,168,9,new Color(.18f,.68f,.79f,.90f));
            Box(page,"Banner cable inner",738,70,124,3,CyberTheme.Cyan);

            var settings=ActionButton(page,"Open settings","SET",1520,10,66,46,ShowControls,false,new Color(.10f,.025f,.22f,.94f),13);
            RoundIcon(settings,16);
            var status=Box(page,"Status marker",1480,26,14,14,CyberTheme.Lime);status.rectTransform.localRotation=Quaternion.Euler(0,0,45);

            // Two low-contrast background silhouettes preserve the reference composition
            // while the large foreground slot stays available for the final portrait.
            homeFigureBackdrop=Rect("Background silhouette layer",page,0,0,1600,900).gameObject;
            var figureRoot=homeFigureBackdrop.GetComponent<RectTransform>();
            BackgroundFigure(figureRoot,"Rear character silhouette",476,171,474,612,.13f);
            BackgroundFigure(figureRoot,"Right character silhouette",766,92,652,742,.21f);

            var characterSlot=Rect("Lobby character slot",page,58,58,680,770);
            homeHeroPlaceholder=characterSlot.gameObject.AddComponent<CyberIdolGraphic>();
            homeHeroPlaceholder.raycastTarget=false;
            var characterArtwork=Rect("Replaceable character artwork",characterSlot,0,0,680,770).gameObject.AddComponent<Image>();
            characterArtwork.sprite=Resources.Load<Sprite>("Characters/hero");
            characterArtwork.preserveAspect=true;characterArtwork.raycastTarget=false;
            characterArtwork.enabled=characterArtwork.sprite!=null;

            Box(page,"Bottom action bar",0,812,1600,88,new Color(.075f,.025f,.14f,.98f));
            Box(page,"Bottom cyan rail",0,808,1600,4,CyberTheme.Alpha(CyberTheme.Cyan,.72f));
            Label(page,"Bottom monogram","R/H",46,836,82,34,23,true,CyberTheme.Cyan,TextAnchor.MiddleCenter);
            var newsButton=ActionButton(page,"Open news","",140,827,190,54,owner.ShowNews,false,
                new Color(.055f,.075f,.14f,.96f),18);
            var newsPanel=(CyberPanelGraphic)newsButton.targetGraphic;newsPanel.cut=8;newsPanel.accent=CyberTheme.Cyan;
            var newsIcon=Rect("News newspaper icon",newsPanel.rectTransform,12,14,27,26).gameObject.AddComponent<NewsIconGraphic>();
            newsIcon.color=CyberTheme.Cyan;newsIcon.raycastTarget=false;
            Mono(newsPanel.rectTransform,"News label","NEWS",51,13,122,28,16,CyberTheme.Text);
            newsUnreadDot=Box(newsPanel.rectTransform,"News unread dot",168,8,11,11,new Color(1f,.12f,.20f,1f));
            Mono(page,"Bottom system","LOCAL RHYTHM INTERFACE",350,843,300,20,10,CyberTheme.Muted);

            var enter=ActionButton(page,"Open songs","ENTER!",548,784,504,102,owner.EnterMusic,true,CyberTheme.Purple,38);
            var enterPanel=(CyberPanelGraphic)enter.targetGraphic;enterPanel.cut=42;
            Mono(page,"Input glyph","[ ENTER ]",914,877,164,16,10,CyberTheme.Alpha(CyberTheme.Text,.65f),TextAnchor.MiddleRight);

            var backgroundButton=ActionButton(page,"Open background menu","BG",1458,568,98,98,ToggleBackgroundMenu,false,new Color(.12f,.04f,.24f,.94f),29);
            RoundIcon(backgroundButton,49);((CyberPanelGraphic)backgroundButton.targetGraphic).accent=CyberTheme.Purple;
            var counterPlate=Panel(page,"Background counter plate",1414,676,186,27,new Color(.07f,.025f,.15f,.86f),CyberTheme.Purple);
            homeBackgroundCounter=Mono(counterPlate.rectTransform,"Background counter","",4,4,178,19,9,CyberTheme.Alpha(CyberTheme.Text,.78f),TextAnchor.MiddleCenter);
            for(int i=0;i<5;i++)Box(page,"Background chip "+i,1466+i*17,650,12,2,i%2==0?CyberTheme.Purple:CyberTheme.Cyan);
            var friendsButton=ActionButton(page,"Open friends","",1474,730,92,78,owner.ShowFriends,false,
                new Color(.10f,.03f,.18f,.97f),18);
            var friendsPanel=(CyberPanelGraphic)friendsButton.targetGraphic;friendsPanel.cut=18;friendsPanel.accent=CyberTheme.Pink;
            var phoneIcon=Rect("Phone icon",friendsPanel.rectTransform,29,22,34,34).gameObject.AddComponent<PhoneIconGraphic>();
            phoneIcon.color=CyberTheme.Cyan;phoneIcon.raycastTarget=false;
            friendUnreadDot=Box(friendsPanel.rectTransform,"Friend notification",66,13,10,10,new Color(1f,.12f,.20f,1f));
            friendUnreadDot.rectTransform.localRotation=Quaternion.Euler(0,0,45);
            ApplyHomeBackground();
            RefreshUnreadIndicators();
            if(slideFromSongs)StartPageSlide(true);
        }
        public void ShowFriends()
        {
            ClearFriendWindow();
            SetPageAlpha(1);
            friendWindowBlocker=Rect("Friend window input blocker",artboard,0,0,1600,900).gameObject;
            var blockerImage=friendWindowBlocker.AddComponent<Image>();
            blockerImage.color=new Color(0,0,0,.18f);blockerImage.raycastTarget=true;
            friendWindowRoot=Rect("Friend phone window",artboard,1474,730,92,78);
            friendWindowRoot.gameObject.AddComponent<RectMask2D>();
            Box(friendWindowRoot,"Friend window background",0,0,1180,720,new Color(.012f,.02f,.045f,.99f));
            var frame=Panel(friendWindowRoot,"Friend window frame",0,0,1180,720,new Color(0,0,0,0),CyberTheme.Cyan);
            frame.outline=true;frame.raycastTarget=true;
            var titleBar=Panel(friendWindowRoot,"Friend title bar",0,0,1180,64,new Color(.035f,.025f,.09f,.99f),CyberTheme.Cyan);
            titleBar.cut=12;
            Box(titleBar.rectTransform,"Friend cyan rail",0,0,1180,5,CyberTheme.Cyan);
            Box(titleBar.rectTransform,"Friend pink rail",0,5,1180,2,CyberTheme.Pink);
            Mono(titleBar.rectTransform,"Friend title","MESSENGER  /  CONTACTS",24,18,540,24,13,CyberTheme.Text);
            var requestButton=ActionButton(titleBar.rectTransform,"Open friend requests","",1028,12,54,40,ToggleFriendRequests,false,
                new Color(.06f,.045f,.13f,.98f),18);
            var requestPanel=(CyberPanelGraphic)requestButton.targetGraphic;requestPanel.cut=6;requestPanel.accent=CyberTheme.Pink;
            var requestIcon=Rect("Friend request icon",requestPanel.rectTransform,11,7,32,28).gameObject.AddComponent<FriendRequestIconGraphic>();
            requestIcon.color=CyberTheme.Cyan;requestIcon.raycastTarget=false;
            friendRequestUnreadDot=Box(requestPanel.rectTransform,"Request unread dot",42,7,10,10,new Color(1f,.12f,.20f,1f));
            var closeWindow=ActionButton(titleBar.rectTransform,"Back to main","X",1114,12,48,40,owner.ReturnFromFriends,false,
                new Color(.10f,.025f,.13f,.98f),18);
            friendCloseButton=closeWindow;
            var closePanel=(CyberPanelGraphic)closeWindow.targetGraphic;closePanel.cut=6;closePanel.accent=CyberTheme.Pink;

            friendWindowContent=Rect("Friend window content",friendWindowRoot,0,0,1180,720).gameObject.AddComponent<CanvasGroup>();
            friendWindowContent.alpha=0;
            friendListLayer=Rect("Friend list",friendWindowContent.GetComponent<RectTransform>(),0,0,1180,720).gameObject;
            var list=friendListLayer.GetComponent<RectTransform>();
            Mono(list,"Friend eyebrow","[ CONTACTS / 01 ONLINE ]",42,92,520,24,13,CyberTheme.Cyan);
            Label(list,"Friend heading","FRIENDS",38,126,500,60,42,true);
            CreateFriendCard(list,210,"Open friend 0","Friends/tnelis","Tnelis",
                "ONLINE  /  SECTOR 09 RELAY","\"You actually made it past the first signal...\"",OpenFriendChat,out tnelisUnreadMarker);
            tnelisUnreadMarker.gameObject.SetActive(tnelisUnreadCount>0);
            if(bakaFriendAccepted)
            {
                CreateFriendCard(list,414,"Open Baka chat","Friends/baka","Baka",
                    "FRIEND  /  SCHOOL RELAY","\"Sorry about the chaos. Can we talk?\"",OpenBakaChat,out bakaUnreadMarker);
                bakaUnreadMarker.gameObject.SetActive(bakaUnreadCount>0);
            }
            if(tnelisChatOpen)
            {
                friendListLayer.SetActive(false);
                BuildFriendChat();
            }
            BeginFriendWindow(false);
            RefreshUnreadIndicators();
        }
        void CreateFriendCard(RectTransform list,float y,string buttonName,string avatarResource,string friendName,
            string status,string preview,Action action,out Image unreadMarker)
        {
            var card=ActionButton(list,buttonName,"",42,y,1098,184,action,false,CyberTheme.Panel);
            var panel=(CyberPanelGraphic)card.targetGraphic;panel.cut=18;panel.accent=CyberTheme.Cyan;
            var avatarRect=Rect(friendName+" avatar",panel.rectTransform,18,18,146,146);
            var avatar=avatarRect.gameObject.AddComponent<RawImage>();
            Texture2D texture=Resources.Load<Texture2D>(avatarResource);
            avatar.texture=texture;avatar.raycastTarget=false;
            avatar.uvRect=texture!=null?CoverTexture(texture,1f):new Rect(0,0,1,1);
            Label(panel.rectTransform,"Friend name",friendName,188,30,430,42,30,true);
            Mono(panel.rectTransform,"Friend status",status,190,78,420,22,12,CyberTheme.Lime);
            Mono(panel.rectTransform,"Friend preview",preview,190,118,820,34,14,CyberTheme.Muted);
            unreadMarker=Box(panel.rectTransform,"Friend unread dot",1058,16,14,14,new Color(1f,.12f,.20f,1f));
        }
        void BeginFriendWindow(bool closing)
        {
            if(friendWindowRoot==null)return;
            SetPageAlpha(1);
            friendWindowClosing=closing;friendWindowStarted=Time.unscaledTime;
            if(closing)
            {
                friendWindowRoot.anchoredPosition=new Vector2(210,-90);
                friendWindowRoot.sizeDelta=new Vector2(1180,720);
                if(friendWindowContent!=null)friendWindowContent.alpha=1;
            }
            else
            {
                friendWindowRoot.anchoredPosition=new Vector2(1474,-730);
                friendWindowRoot.sizeDelta=new Vector2(92,78);
                if(friendWindowContent!=null)friendWindowContent.alpha=0;
            }
        }
        void ToggleFriendRequests()
        {
            if(friendRequestPanel!=null){UnityEngine.Object.Destroy(friendRequestPanel);friendRequestPanel=null;return;}
            if(friendWindowContent==null)return;
            RemoveNotifications("request",-1);
            var host=friendWindowContent.GetComponent<RectTransform>();
            var scrim=Box(host,"Friend request scrim",0,0,1180,720,new Color(0,0,0,.58f));
            scrim.raycastTarget=true;friendRequestPanel=scrim.gameObject;
            var panel=Panel(scrim.rectTransform,"Friend request panel",196,104,788,512,new Color(.018f,.026f,.075f,.99f),CyberTheme.Pink);
            Box(panel.rectTransform,"Request top rail",0,0,788,6,CyberTheme.Pink);
            Mono(panel.rectTransform,"Request tag","[ CONTACT REQUESTS ]",34,28,500,24,13,CyberTheme.Cyan);
            Label(panel.rectTransform,"Request heading","FRIEND REQUESTS",30,66,600,56,36,true);
            if(bakaRequestPending&&!bakaRequestDeclined)
            {
                var request=Panel(panel.rectTransform,"Baka request",30,150,728,222,new Color(.055f,.035f,.10f,.98f),CyberTheme.Pink);
                var avatarRect=Rect("Baka request avatar",request.rectTransform,18,18,132,132);
                var avatar=avatarRect.gameObject.AddComponent<RawImage>();
                Texture2D texture=Resources.Load<Texture2D>("Friends/baka");
                avatar.texture=texture;avatar.raycastTarget=false;avatar.uvRect=texture!=null?CoverTexture(texture,1f):new Rect(0,0,1,1);
                Label(request.rectTransform,"Baka request name","Baka",172,24,420,38,27,true);
                Mono(request.rectTransform,"Baka request copy","WANTS TO ADD YOU AS A FRIEND",174,70,450,22,12,CyberTheme.Pink);
                Mono(request.rectTransform,"Baka request preview","\"I need to talk about the signal...\"",174,106,450,54,14,CyberTheme.Muted);
                ActionButton(request.rectTransform,"Accept Baka request","ACCEPT",506,136,202,58,AcceptBakaRequest,true,null,17);
            }
            else
            {
                Label(panel.rectTransform,"No requests","NO PENDING REQUESTS",62,250,664,50,28,true,CyberTheme.Muted,TextAnchor.MiddleCenter);
                Mono(panel.rectTransform,"No requests hint","New contact requests will appear here as the story progresses.",72,318,644,40,14,CyberTheme.Muted,TextAnchor.MiddleCenter);
            }
            ActionButton(panel.rectTransform,"Close requests","CLOSE",286,430,216,52,()=>{UnityEngine.Object.Destroy(friendRequestPanel);friendRequestPanel=null;},false,null,17);
        }
        public void BeginFriendExit()
        {
            BeginFriendWindow(true);
        }
        public void FinishFriendExit()
        {
            if(!friendWindowClosing)return;
            if(tnelisDialogueStep>=2)FlushTnelisConversation();
            bool createTnelisNews=tnelisConversationComplete&&!tnelisNewsCreated;
            ClearFriendWindow();
            if(createTnelisNews)AddTnelisNews();
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
            if(friendChatLayer!=null&&Input.GetMouseButtonDown(0))
            {
                float clickTime=Time.unscaledTime;
                if(clickTime-lastFriendClickTime<=.32f)SkipActiveFriendTyping();
                lastFriendClickTime=clickTime;
            }
            float raw=Mathf.Clamp01((Time.unscaledTime-friendWindowStarted)/FriendsWindowDuration);
            float t=Mathf.SmoothStep(0,1,raw);
            Vector2 iconPosition=new Vector2(1474,-730),iconSize=new Vector2(92,78);
            Vector2 finalPosition=new Vector2(210,-90),finalSize=new Vector2(1180,720);
            if(friendWindowClosing)
            {
                friendWindowRoot.anchoredPosition=Vector2.Lerp(finalPosition,iconPosition,t);
                friendWindowRoot.sizeDelta=Vector2.Lerp(finalSize,iconSize,t);
            }
            else
            {
                friendWindowRoot.anchoredPosition=Vector2.Lerp(iconPosition,finalPosition,t);
                friendWindowRoot.sizeDelta=Vector2.Lerp(iconSize,finalSize,t);
            }
            if(friendWindowContent!=null)
            {
                float contentT=friendWindowClosing
                    ? 1-Mathf.SmoothStep(0,1,Mathf.Clamp01((raw-.1f)/.48f))
                    : Mathf.SmoothStep(0,1,Mathf.Clamp01((raw-.26f)/.74f));
                friendWindowContent.alpha=contentT;
                friendWindowContent.interactable=contentT>.85f;
                friendWindowContent.blocksRaycasts=friendWindowContent.interactable;
            }
            if(raw>=1&&friendWindowClosing)
            {
                // FinishFriendExit owns teardown so post-conversation events can run once.
            }
        }
        void ClearFriendWindow()
        {
            buttons.Remove("Back to main");buttons.Remove("Open friend 0");
            buttons.Remove("Open friend requests");
            buttons.Remove("Back to friends");buttons.Remove("Chat choice 0");buttons.Remove("Chat choice 1");
            buttons.Remove("Accept Baka request");buttons.Remove("Reject Baka request");buttons.Remove("Open Baka chat");
            buttons.Remove("Close requests");
            if(friendListLayer!=null){friendListLayer.SetActive(false);UnityEngine.Object.Destroy(friendListLayer);friendListLayer=null;}
            if(friendChatLayer!=null){friendChatLayer.SetActive(false);UnityEngine.Object.Destroy(friendChatLayer);friendChatLayer=null;}
            if(friendRequestPanel!=null){friendRequestPanel.SetActive(false);UnityEngine.Object.Destroy(friendRequestPanel);friendRequestPanel=null;}
            if(friendWindowRoot!=null){friendWindowRoot.gameObject.SetActive(false);UnityEngine.Object.Destroy(friendWindowRoot.gameObject);friendWindowRoot=null;}
            if(friendWindowBlocker!=null){friendWindowBlocker.SetActive(false);UnityEngine.Object.Destroy(friendWindowBlocker);friendWindowBlocker=null;}
            friendWindowContent=null;friendCloseButton=null;friendWindowClosing=false;
            tnelisUnreadMarker=null;friendRequestUnreadDot=null;
        }
        void RefreshUnreadIndicators()
        {
            if(newsUnreadDot!=null)newsUnreadDot.gameObject.SetActive(newsUnread);
            bool friendHasUnread=tnelisUnreadCount>0||bakaUnreadCount>0||(bakaRequestPending&&!bakaRequestDeclined);
            if(friendUnreadDot!=null)friendUnreadDot.gameObject.SetActive(friendHasUnread);
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
            if(friendListLayer!=null)friendListLayer.SetActive(false);
            if(tnelisMessages.Count==0&&tnelisPending.Count==0)
            {
                tnelisPending.Enqueue(new FriendChatMessage{FromPlayer=false,
                    Text="Hey. You actually made it past the first signal. I knew you would."});
                tnelisDialogueStep=0;
            }
            BuildFriendChat();
            UpdateFriendChatFlow();
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
            if(friendListLayer!=null)friendListLayer.SetActive(false);
            if(bakaMessages.Count==0&&bakaPending.Count==0)
            {
                bakaPending.Enqueue(new FriendChatMessage{FromPlayer=false,
                    Text="Hi. I'm Baka. Sorry about the practice room. Everything went wrong at once."});
                bakaPending.Enqueue(new FriendChatMessage{FromPlayer=false,
                    Text="You told me to check the cables. I did, and the room is quiet again. Thank you."});
                bakaPending.Enqueue(new FriendChatMessage{FromPlayer=false,
                    Text="Can I ask you something? The signal you found... it sounds like my old song."});
            }
            BuildFriendChat();
            UpdateFriendChatFlow();
        }
        void BuildFriendChat()
        {
            if(friendWindowContent==null)return;
            if(friendChatLayer!=null){friendChatLayer.SetActive(false);UnityEngine.Object.Destroy(friendChatLayer);friendChatLayer=null;}
            buttons.Remove("Back to friends");buttons.Remove("Chat choice 0");buttons.Remove("Chat choice 1");
            friendTypingText=null;
            bool baka=activeFriendIndex==1;
            List<FriendChatMessage> messages=baka?bakaMessages:tnelisMessages;
            Queue<FriendChatMessage> pending=baka?bakaPending:tnelisPending;
            string friendName=baka?"Baka":"Tnelis";
            string avatarResource=baka?"Friends/baka":"Friends/tnelis";
            string friendStatus=baka?"FRIEND  /  SCHOOL RELAY":"ONLINE  /  SECURE RELAY";
            var host=friendWindowContent.GetComponent<RectTransform>();
            friendChatLayer=Rect("Friend chat",host,0,0,1180,720).gameObject;
            var chat=friendChatLayer.GetComponent<RectTransform>();
            ActionButton(chat,"Back to friends","<  FRIENDS",28,82,190,42,CloseFriendChat,false,
                new Color(.05f,.075f,.14f,.96f),13);
            var avatar=Rect("Chat avatar",chat,230,76,58,58).gameObject.AddComponent<RawImage>();
            Texture2D texture=Resources.Load<Texture2D>(avatarResource);
            avatar.texture=texture;avatar.raycastTarget=false;avatar.uvRect=texture!=null?CoverTexture(texture,1f):new Rect(0,0,1,1);
            Label(chat,"Chat name",friendName,302,76,300,34,25,true);
            Mono(chat,"Chat status",friendStatus,304,111,330,20,11,CyberTheme.Lime);

            var viewport=Rect("Chat viewport",chat,28,156,704,500);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color=CyberTheme.Alpha(CyberTheme.Panel,.18f);
            int visibleRows=messages.Count+(friendTyping?1:0);
            float contentHeight=Mathf.Max(500,visibleRows*94+20);
            var content=Rect("Chat messages",viewport,0,0,704,contentHeight);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            scroll.verticalNormalizedPosition=0;
            for(int i=0;i<messages.Count;i++)
            {
                var message=messages[i];
                float y=18+i*94;
                var bubble=Panel(content,"Chat message "+i,message.FromPlayer?258:16,y,430,76,
                    message.FromPlayer?new Color(.05f,.22f,.28f,.98f):new Color(.07f,.055f,.13f,.98f),
                    message.FromPlayer?CyberTheme.Cyan:CyberTheme.Pink);
                bubble.cut=14;
                var text=Label(bubble.rectTransform,"Chat text",message.Text,18,10,394,56,17,false,
                    message.FromPlayer?CyberTheme.Text:CyberTheme.Text);
                text.lineSpacing=.9f;Fit(text,12,17);
            }
            if(friendTyping)
            {
                var typing=Panel(content,"Tnelis typing",16,18+messages.Count*94,136,62,
                    new Color(.07f,.055f,.13f,.98f),CyberTheme.Pink);
                typing.cut=16;
                friendTypingText=Label(typing.rectTransform,"Typing dots","···",0,0,136,62,29,true,
                    CyberTheme.Pink,TextAnchor.MiddleCenter);
            }
            content.anchoredPosition=new Vector2(0,Mathf.Max(0,contentHeight-500));

            var side=Panel(chat,"Reply panel",760,156,392,500,new Color(.018f,.035f,.07f,.95f),CyberTheme.Purple);
            Mono(side.rectTransform,"Reply tag","[ REPLY CHANNEL ]",28,24,336,22,12,CyberTheme.Cyan);
            Mono(side.rectTransform,"Reply history",baka?"FRIEND CONNECTION  /  ACTIVE":"DIALOGUE NODE  /  0"+(tnelisDialogueStep+1),28,55,336,20,11,CyberTheme.Muted);
            if(baka)
            {
                if(friendTyping||pending.Count>0)
                {
                    Mono(side.rectTransform,"Baka waiting","···  BAKA IS TYPING",28,120,336,28,16,CyberTheme.Pink,TextAnchor.MiddleCenter);
                    Mono(side.rectTransform,"Baka wait hint","Wait for the next message.",28,172,336,60,14,CyberTheme.Muted);
                }
                else
                {
                    Panel(side.rectTransform,"Baka conversation",28,104,336,182,new Color(.06f,.12f,.17f,.98f),CyberTheme.Purple);
                    Label(side.rectTransform,"Baka complete","FRIEND REQUEST ACCEPTED",54,128,284,42,20,true,CyberTheme.Cyan,TextAnchor.MiddleCenter);
                    Mono(side.rectTransform,"Baka complete body","Baka is glad you answered. This conversation will continue with the story.",48,184,296,72,13,CyberTheme.Muted,TextAnchor.MiddleCenter);
                }
            }
            else if(friendTyping||pending.Count>0)
            {
                Mono(side.rectTransform,"Reply waiting","···  TNELIS IS TYPING",28,120,336,28,16,CyberTheme.Pink,TextAnchor.MiddleCenter);
                Mono(side.rectTransform,"Reply hint","Wait for the next message.",28,172,336,60,14,CyberTheme.Muted);
            }
            else if(tnelisDialogueStep==0)
            {
                ActionButton(side.rectTransform,"Chat choice 0","WHO ARE YOU?",28,104,336,76,()=>ChooseFriendReply(0),false,null,17);
                ActionButton(side.rectTransform,"Chat choice 1","YOU WERE WATCHING ME?",28,198,336,76,()=>ChooseFriendReply(1),false,null,17);
                Mono(side.rectTransform,"Reply hint","Choose one response to continue the branch.",28,318,336,60,14,CyberTheme.Muted);
            }
            else if(tnelisDialogueStep==1)
            {
                ActionButton(side.rectTransform,"Chat choice 0","SEND ME THE COORDINATES.",28,104,336,76,()=>ChooseFriendReply(0),false,null,17);
                ActionButton(side.rectTransform,"Chat choice 1","IS IT DANGEROUS?",28,198,336,76,()=>ChooseFriendReply(1),false,null,17);
                Mono(side.rectTransform,"Reply hint","Tnelis is waiting for your answer.",28,318,336,60,14,CyberTheme.Muted);
            }
            else
            {
                Panel(side.rectTransform,"Conversation complete",28,104,336,150,new Color(.06f,.12f,.17f,.98f),CyberTheme.Cyan);
                Label(side.rectTransform,"Complete title","CONNECTION STABLE",54,128,284,42,23,true,CyberTheme.Cyan,TextAnchor.MiddleCenter);
                Mono(side.rectTransform,"Complete body","The channel stays open. More messages will arrive as the story advances.",48,184,296,60,13,CyberTheme.Muted,TextAnchor.MiddleCenter);
            }
        }
        void ChooseFriendReply(int choice)
        {
            if(tnelisDialogueStep==0)
            {
                string playerText=choice==0?"Who are you?":"You were watching me?";
                string reply=choice==0
                    ?"Someone who remembers the old network. Call me Tnelis."
                    :"Only the parts that mattered. The city hides too much.";
                tnelisMessages.Add(new FriendChatMessage{FromPlayer=true,Text=playerText});
                tnelisPending.Enqueue(new FriendChatMessage{FromPlayer=false,Text=reply});
                tnelisPending.Enqueue(new FriendChatMessage{FromPlayer=false,
                    Text="I found something beneath Sector 09. It responds when the rhythm changes."});
                tnelisDialogueStep=1;
            }
            else if(tnelisDialogueStep==1)
            {
                string playerText=choice==0?"Send me the coordinates.":"Is it dangerous?";
                string reply=choice==0
                    ?"Already sent. Follow the blue pulse after midnight."
                    :"Everything useful is dangerous. That's why I'm asking you.";
                tnelisMessages.Add(new FriendChatMessage{FromPlayer=true,Text=playerText});
                tnelisPending.Enqueue(new FriendChatMessage{FromPlayer=false,Text=reply});
                tnelisPending.Enqueue(new FriendChatMessage{FromPlayer=false,
                    Text="Come back when you're ready. I'll keep the channel open."});
                tnelisDialogueStep=2;
            }
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
                            AddNotification("friend","BAKA",delivered.Text,1,()=>owner.OpenFriendChatFromNotification(1));
                        }
                        else
                        {
                            tnelisUnreadCount++;
                            AddNotification("friend","TNELIS",delivered.Text,0,()=>owner.OpenFriendChatFromNotification(0));
                        }
                    }
                    RefreshUnreadIndicators();
                    BuildFriendChat();
                    if(!baka&&tnelisDialogueStep==2&&pending.Count==0)tnelisConversationComplete=true;
                }
                return;
            }
            if(pending.Count>0&&now>=friendNextMessageAt)
            {
                FriendChatMessage next=pending.Peek();
                friendTyping=true;
                friendTypingUntil=now+Mathf.Clamp(.45f+next.Text.Length*.035f,.7f,2.2f);
                BuildFriendChat();
            }
        }
        void SkipActiveFriendTyping()
        {
            bool baka=activeFriendIndex==1;
            Queue<FriendChatMessage> pending=baka?bakaPending:tnelisPending;
            List<FriendChatMessage> messages=baka?bakaMessages:tnelisMessages;
            while(pending.Count>0)messages.Add(pending.Dequeue());
            friendTyping=false;friendNextMessageAt=0;
            if(!baka&&tnelisDialogueStep==2)tnelisConversationComplete=true;
            BuildFriendChat();
        }
        void AddTnelisNews()
        {
            if(tnelisNewsCreated)return;
            tnelisNewsCreated=true;
            int index=newsPosts.Count;
            newsPosts.Add(new NewsPost{
                Name="Tnelis",Handle="@tnelis_09",Time="NOW / PRIVATE RELAY",
                Title="I FOUND THE PULSE",
                Body="The signal under Sector 09 is moving. I mapped three clean routes before the relay closed. If you are reading this, follow the blue pulse and do not answer the red channel.",
                Tag="#sector09 #bluepulse #findme",ImageCaption="TNELIS / PRIVATE RELAY",HasImage=true,ImageResource="Friends/tnelis",
                CommentPrompt="Blue route confirmed. Red channel ignored.",CommentDraft="Blue route confirmed. I kept off the red channel and marked the relay entrance.",
                Likes=17,
                Comments=new[]{"NYX_07: I saw the same pulse near the lower blocks.","K-01: Route 4 is still clear. Move before dawn."}
            });
            newsLiked.Add(false);newsUnreadItems.Add(true);newsCommentStep.Add(0);newsCommentMessages.Add(new List<string>());
            newsUnread=true;RefreshUnreadIndicators();
            RebuildNewsCards();
            AddNotification("news","NEWS / TNELIS","Tnelis posted a new signal update.",index,()=>owner.OpenNewsFromNotification(index));
        }
        void CreateBakaNews()
        {
            if(bakaNewsCreated)return;
            bakaNewsCreated=true;
            bakaNewsIndex=newsPosts.Count;
            int index=bakaNewsIndex;
            newsPosts.Add(new NewsPost{
                Name="Baka",Handle="@baka_wonders",Time="JUST NOW / SCHOOL RELAY",
                Title="I THINK I BROKE THE RHYTHM",
                Body="I tried to help everyone at the same time and now the practice room is a disaster. Everyone is laughing, the equipment is making strange noises, and I do not know how to fix it before the teacher comes back. What should I do?",
                Tag="#help #practice #chaos",ImageCaption="Baka / practice room emergency",HasImage=true,ImageResource="News/baka-post",
                CommentPrompt="Breathe first. Then check the cables.",CommentDraft="Breathe first. Then check the cables one by one. Nobody is going to blame you for one messy practice.",
                Likes=6,
                Comments=new[]{"MIRA.EXE: Turn off the amplifier before checking anything.","K-01: This looks like a five-minute cleanup, not a disaster."}
            });
            newsLiked.Add(false);newsUnreadItems.Add(true);newsCommentStep.Add(0);newsCommentMessages.Add(new List<string>());
            newsUnread=true;RefreshUnreadIndicators();
            RebuildNewsCards();
            AddNotification("news","NEWS / BAKA","Baka needs help in the practice room.",index,()=>owner.OpenNewsFromNotification(index));
        }
        void CloseFriendChat()
        {
            tnelisChatOpen=false;
            if(friendChatLayer!=null){friendChatLayer.SetActive(false);UnityEngine.Object.Destroy(friendChatLayer);friendChatLayer=null;}
            buttons.Remove("Back to friends");buttons.Remove("Chat choice 0");buttons.Remove("Chat choice 1");
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
            blockerImage.color=new Color(0,0,0,.18f);blockerImage.raycastTarget=true;
            newsWindowRoot=Rect("News popup window",artboard,NewsIconX,NewsIconY,NewsIconWidth,NewsIconHeight);
            newsWindowRoot.gameObject.AddComponent<RectMask2D>();
            Box(newsWindowRoot,"Window background",0,0,1600,900,new Color(.012f,.02f,.045f,.985f));
            var frame=Panel(newsWindowRoot,"Window frame",0,0,1600,900,new Color(0,0,0,0),CyberTheme.Cyan);
            frame.outline=true;frame.raycastTarget=true;
            var titleBar=Panel(newsWindowRoot,"Window title bar",0,0,1600,64,new Color(.035f,.025f,.09f,.99f),CyberTheme.Cyan);
            titleBar.cut=12;
            Box(titleBar.rectTransform,"Window cyan rail",0,0,1600,5,CyberTheme.Cyan);
            Box(titleBar.rectTransform,"Window pink rail",0,5,1600,2,CyberTheme.Pink);
            Mono(titleBar.rectTransform,"Window title","SIGNAL NETWORK  /  COMMUNITY TRANSMISSIONS",26,18,720,24,13,CyberTheme.Text);
            for(int i=0;i<3;i++)Box(titleBar.rectTransform,"Window dot "+i,1472+i*22,24,9,9,i==0?CyberTheme.Pink:i==1?CyberTheme.Cyan:CyberTheme.Purple);
            var closeWindow=ActionButton(titleBar.rectTransform,"Back to main","X",1536,12,50,42,owner.ReturnFromNews,false,
                new Color(.10f,.025f,.13f,.98f),19);
            newsCloseButton=closeWindow;
            var closePanel=(CyberPanelGraphic)closeWindow.targetGraphic;closePanel.cut=6;closePanel.accent=CyberTheme.Pink;

            newsWindowContent=Rect("News window content",newsWindowRoot,0,0,1600,900).gameObject.AddComponent<CanvasGroup>();
            newsWindowContent.alpha=0;
            newsFeedLayer=Rect("News card feed",newsWindowContent.GetComponent<RectTransform>(),0,0,1600,900).gameObject;
            var feed=newsFeedLayer.GetComponent<RectTransform>();
            Mono(feed,"Feed eyebrow","[ COMMUNITY NETWORK / PUBLIC TRANSMISSIONS ]",64,137,780,24,13,CyberTheme.Cyan);
            Label(feed,"Feed heading","SIGNAL FEED",64,174,760,74,54,true);
            Mono(feed,"Feed count",newsPosts.Count.ToString("D2")+" ACTIVE POST"+(newsPosts.Count==1?"":"S")+"  /  LIVE RELAY",68,250,720,22,13,CyberTheme.Muted);

            var viewport=Rect("News feed viewport",feed,64,270,1472,540);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color=CyberTheme.Alpha(CyberTheme.Panel,.22f);
            int rows=Mathf.CeilToInt(newsPosts.Count/4f);
            var content=Rect("News feed posts",viewport,0,0,1472,Math.Max(500,rows*580));
            newsFeedContent=content;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=34;
            for(int i=0;i<newsPosts.Count;i++)CreateNewsCard(content,newsPosts[i],i);
            Mono(feed,"Feed footer","SIGNAL FEED IS A LOCAL DEMO CHANNEL  /  SCROLL TO READ MORE",68,817,900,20,11,CyberTheme.Muted);
            BeginNewsWindow(false);
        }
        void CreateNewsCard(RectTransform parent,NewsPost post,int index)
        {
            int column=index%4,row=index/4;
            float x=column*368,y=row*580;
            RectTransform source=null;
            var card=ActionButton(parent,"Open news post "+index,"",x,y,344,540,()=>ShowNewsDetail(index,source),false,CyberTheme.Panel);
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
            var title=Label(panel.rectTransform,"News card title",post.Title,18,326,308,54,20,true,CyberTheme.Text);
            Fit(title,14,20);
            if(newsUnreadItems[index])
                newsUnreadMarkers[index]=Box(panel.rectTransform,"News unread marker",315,12,15,15,new Color(1f,.12f,.20f,1f));
            Mono(panel.rectTransform,"News card publisher",post.Name+"  "+post.Handle,18,386,308,22,13,CyberTheme.Cyan);
            var excerpt=Label(panel.rectTransform,"News card excerpt",Excerpt(post.Body,96),18,414,308,92,16,false,CyberTheme.Muted);
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
            if(post.Name=="Tnelis"&&!bakaNewsCreated)bakaNewsQueued=true;
            if(newsFeedLayer!=null)newsFeedLayer.SetActive(false);
            if(newsCloseButton!=null)newsCloseButton.gameObject.SetActive(false);
            RectTransform host=newsWindowContent.GetComponent<RectTransform>();
            newsDetailBlocker=Rect("News detail blocker",host,0,0,1600,900).gameObject;
            var blockerImage=newsDetailBlocker.AddComponent<Image>();
            blockerImage.color=new Color(0,0,0,.38f);blockerImage.raycastTarget=true;
            if(sourceCard!=null)
            {
                Vector3 local=host.InverseTransformPoint(sourceCard.TransformPoint(new Vector3(sourceCard.rect.xMin,sourceCard.rect.yMax,0)));
                newsDetailIconPosition=new Vector2(local.x,-local.y);
                newsDetailIconSize=sourceCard.rect.size;
            }
            else
            {
                newsDetailIconPosition=Vector2.zero;
                newsDetailIconSize=new Vector2(1600,900);
            }
            newsDetailRoot=Rect("News detail window",host,newsDetailIconPosition.x,-newsDetailIconPosition.y,
                newsDetailIconSize.x,newsDetailIconSize.y);
            newsDetailRoot.gameObject.AddComponent<RectMask2D>();
            Box(newsDetailRoot,"Detail window background",0,0,1600,900,new Color(.012f,.02f,.045f,.99f));
            newsDetailContent=newsDetailRoot.gameObject.AddComponent<CanvasGroup>();
            newsDetailContent.alpha=sourceCard!=null?0:1;
            var detail=Rect("News detail content",newsDetailRoot,0,0,1600,900);
            ActionButton(detail,"Close detail","<  BACK TO FEED",64,88,240,46,CloseNewsDetail,false,
                new Color(.05f,.075f,.14f,.96f),14);
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
            Mono(detail,"Detail image caption",post.ImageCaption,70,606,600,20,11,CyberTheme.Cyan);
            Label(detail,"Detail title",post.Title,744,150,792,78,39,true);
            Mono(detail,"Detail handle",post.Name+"  /  "+post.Handle+"  /  "+post.Time,748,234,760,28,15,CyberTheme.Muted);
            var body=Label(detail,"Detail body",post.Body,744,280,792,195,27,false,CyberTheme.Text);
            body.lineSpacing=.92f;Fit(body,18,27);
            Mono(detail,"Detail tag",post.Tag,748,485,760,22,12,CyberTheme.Cyan);
            string likeCaption=(newsLiked[index]?"LIKED  ":"LIKE  ")+(post.Likes+(newsLiked[index]?1:0));
            var like=ActionButton(detail,"Like post","",744,520,250,62,()=>ToggleNewsLike(index),newsLiked[index],
                newsLiked[index]?new Color(.16f,.035f,.12f,.98f):null,19);
            var likePanel=(CyberPanelGraphic)like.targetGraphic;
            var heart=Rect("Heart like icon",likePanel.rectTransform,16,15,32,32).gameObject.AddComponent<HeartIconGraphic>();
            heart.color=newsLiked[index]?new Color(1f,.38f,.58f,1f):CyberTheme.Text;heart.raycastTarget=false;
            Mono(likePanel.rectTransform,"Like count",likeCaption,58,16,176,30,17,
                newsLiked[index]?new Color(1f,.78f,.84f,1f):CyberTheme.Text,TextAnchor.MiddleCenter);
            var comments=Panel(detail,"Detail comments",744,596,792,204,new Color(.018f,.035f,.07f,.94f),CyberTheme.Purple);
            int commentCount=post.Comments!=null?post.Comments.Length:0;
            Mono(comments.rectTransform,"Comments heading","COMMENTS  /  "+commentCount.ToString("D2"),22,14,730,26,15,CyberTheme.Cyan);
            var visibleComments=new List<string>();
            if(post.Comments!=null)for(int i=0;i<commentCount;i++)visibleComments.Add(post.Comments[i]);
            List<string> authoredComments=newsCommentMessages[index];
            for(int i=0;i<authoredComments.Count;i++)visibleComments.Add(authoredComments[i]);
            int firstComment=Mathf.Max(0,visibleComments.Count-2);
            for(int i=firstComment;i<visibleComments.Count;i++)
            {
                int row=i-firstComment;
                float rowY=42+row*42;
                Mono(comments.rectTransform,"Detail comment "+row,visibleComments[i],22,rowY,742,34,18,
                    row==0?CyberTheme.Text:CyberTheme.Muted);
                if(row==0)Box(comments.rectTransform,"Detail comment divider",22,84,742,1,CyberTheme.Alpha(CyberTheme.Muted,.18f));
            }
            if(newsCommentStep[index]==0)
                ActionButton(comments.rectTransform,"Post comment",Excerpt(post.CommentPrompt,34),20,134,742,44,()=>PostNewsComment(index),false,null,14);
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
            if(index<0||index>=newsCommentStep.Count)return;
            if(newsCommentStep[index]!=0)return;
            newsCommentStep[index]=1;
            newsCommentMessages[index].Add("YOU: "+newsPosts[index].CommentDraft);
            if(newsPosts[index].Name=="Baka"&&!bakaFriendAccepted&&!bakaRequestDeclined)
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
            float t=Mathf.SmoothStep(0,1,raw);
            Vector2 iconPosition=new Vector2(newsDetailIconPosition.x,-newsDetailIconPosition.y);
            Vector2 iconSize=newsDetailIconSize;
            if(newsDetailClosing)
            {
                newsDetailRoot.anchoredPosition=Vector2.Lerp(newsDetailCloseFromPosition,iconPosition,t);
                newsDetailRoot.sizeDelta=Vector2.Lerp(newsDetailCloseFromSize,iconSize,t);
            }
            else
            {
                newsDetailRoot.anchoredPosition=Vector2.Lerp(iconPosition,Vector2.zero,t);
                newsDetailRoot.sizeDelta=Vector2.Lerp(iconSize,new Vector2(1600,900),t);
            }
            if(newsDetailContent!=null)
            {
                float contentT=newsDetailClosing
                    ? 1-Mathf.SmoothStep(0,1,Mathf.Clamp01((raw-.08f)/.48f))
                    : Mathf.SmoothStep(0,1,Mathf.Clamp01((raw-.24f)/.76f));
                newsDetailContent.alpha=contentT;
                newsDetailContent.interactable=contentT>.85f;
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
            if(closing)
            {
                newsWindowRoot.anchoredPosition=Vector2.zero;
                newsWindowRoot.sizeDelta=new Vector2(1600,900);
                if(newsWindowContent!=null)newsWindowContent.alpha=1;
            }
            else
            {
                newsWindowRoot.anchoredPosition=new Vector2(NewsIconX,-NewsIconY);
                newsWindowRoot.sizeDelta=new Vector2(NewsIconWidth,NewsIconHeight);
                if(newsWindowContent!=null)newsWindowContent.alpha=0;
            }
        }
        void UpdateNewsWindow()
        {
            if(newsWindowRoot==null)return;
            float raw=Mathf.Clamp01((Time.unscaledTime-newsWindowStarted)/NewsWindowDuration);
            float t=Mathf.SmoothStep(0,1,raw);
            Vector2 iconPosition=new Vector2(NewsIconX,-NewsIconY);
            Vector2 iconSize=new Vector2(NewsIconWidth,NewsIconHeight);
            if(newsWindowClosing)
            {
                newsWindowRoot.anchoredPosition=Vector2.Lerp(Vector2.zero,iconPosition,t);
                newsWindowRoot.sizeDelta=Vector2.Lerp(new Vector2(1600,900),iconSize,t);
            }
            else
            {
                newsWindowRoot.anchoredPosition=Vector2.Lerp(iconPosition,Vector2.zero,t);
                newsWindowRoot.sizeDelta=Vector2.Lerp(iconSize,new Vector2(1600,900),t);
            }
            if(newsWindowContent!=null)
            {
                float contentT=newsWindowClosing
                    ? 1-Mathf.SmoothStep(0,1,Mathf.Clamp01((raw-.12f)/.48f))
                    : Mathf.SmoothStep(0,1,Mathf.Clamp01((raw-.28f)/.72f));
                newsWindowContent.alpha=contentT;
                newsWindowContent.interactable=contentT>.85f;
                newsWindowContent.blocksRaycasts=newsWindowContent.interactable;
            }
            if(raw>=1)
            {
                if(newsWindowClosing)ClearNewsWindow();
                else pageAlphaLocked=false;
            }
        }
        public void FinishNewsExit()
        {
            if(newsWindowClosing)ClearNewsWindow();
            if(bakaRequestQueued&&!bakaFriendAccepted&&!bakaRequestDeclined)
            {
                bakaRequestQueued=false;bakaRequestPending=true;bakaUnreadCount=1;RefreshUnreadIndicators();
                AddNotification("request","FRIEND REQUEST","Baka wants to add you as a friend.",-1,()=>owner.OpenFriendRequestsFromNotification());
            }
        }
        void ClearNewsWindow()
        {
            buttons.Remove("Back to main");
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
            var scrim=Box(parent,name+" scrim",0,0,1600,900,new Color(0,0,0,0));
            scrim.raycastTarget=true;
            var scrimGroup=scrim.gameObject.AddComponent<CanvasGroup>();
            var root=Rect(name,parent,iconPosition.x,-iconPosition.y,iconSize.x,iconSize.y);
            root.gameObject.AddComponent<RectMask2D>();
            var content=root.gameObject.AddComponent<CanvasGroup>();
            content.alpha=0;
            return new PopupMotion{
                Root=root,Content=content,Scrim=scrimGroup,
                IconPosition=iconPosition,IconSize=iconSize,
                FinalPosition=new Vector2(finalX,-finalY),FinalSize=new Vector2(finalWidth,finalHeight),
                ScrimAlpha=.68f,OnFinished=onFinished
            };
        }
        void StartPopup(PopupMotion popup,bool closing)
        {
            if(popup==null||popup.Root==null)return;
            SetPageAlpha(1);
            popup.Started=Time.unscaledTime;popup.Closing=closing;
            if(closing)
            {
                popup.Root.anchoredPosition=popup.FinalPosition;
                popup.Root.sizeDelta=popup.FinalSize;
                if(popup.Content!=null)popup.Content.alpha=1;
                if(popup.Scrim!=null)popup.Scrim.alpha=popup.ScrimAlpha;
            }
            else
            {
                popup.Root.anchoredPosition=popup.IconPosition;
                popup.Root.sizeDelta=popup.IconSize;
                if(popup.Content!=null)popup.Content.alpha=0;
                if(popup.Scrim!=null)popup.Scrim.alpha=0;
            }
        }
        void UpdatePopup(PopupMotion popup,float duration)
        {
            if(popup==null||popup.Root==null)return;
            float raw=Mathf.Clamp01((Time.unscaledTime-popup.Started)/Mathf.Max(.05f,duration));
            float t=Mathf.SmoothStep(0,1,raw);
            Vector2 fromPosition=popup.Closing?popup.FinalPosition:popup.IconPosition;
            Vector2 toPosition=popup.Closing?popup.IconPosition:popup.FinalPosition;
            Vector2 fromSize=popup.Closing?popup.FinalSize:popup.IconSize;
            Vector2 toSize=popup.Closing?popup.IconSize:popup.FinalSize;
            popup.Root.anchoredPosition=Vector2.Lerp(fromPosition,toPosition,t);
            popup.Root.sizeDelta=Vector2.Lerp(fromSize,toSize,t);
            if(popup.Content!=null)
            {
                float contentT=popup.Closing
                    ? 1-Mathf.SmoothStep(0,1,Mathf.Clamp01((raw-.08f)/.5f))
                    : Mathf.SmoothStep(0,1,Mathf.Clamp01((raw-.24f)/.76f));
                popup.Content.alpha=contentT;
                popup.Content.interactable=contentT>.85f;
                popup.Content.blocksRaycasts=popup.Content.interactable;
            }
            if(popup.Scrim!=null)popup.Scrim.alpha=popup.ScrimAlpha*(popup.Closing?1-t:t);
            if(raw>=1&&popup.Closing)popup.OnFinished?.Invoke();
        }
        void ClearPopup(ref PopupMotion popup)
        {
            if(popup==null)return;
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
            buttons.Remove("Cancel return");buttons.Remove("Confirm return");buttons.Remove("Close controls");
            buttons.Remove("Close background menu");
            for(int i=1;i<=3;i++)buttons.Remove("Select background "+i);
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
            float eased=Mathf.SmoothStep(0,1,raw);
            float offset=900*eased;
            page.anchoredPosition=new Vector2(0,pageSlideReverse?900-offset:-900+offset);
            if(outgoingPage!=null)outgoingPage.anchoredPosition=new Vector2(0,pageSlideReverse?-offset:offset);
            if(raw>=1)
            {
                if(outgoingPage!=null){outgoingPage.gameObject.SetActive(false);UnityEngine.Object.Destroy(outgoingPage.gameObject);outgoingPage=null;}
                page.anchoredPosition=Vector2.zero;pageSlideActive=false;pageAlphaLocked=false;
            }
        }
        public void ShowModeLoading()
        {
            Begin("Main to mode loading","00   /   ROUTE HANDSHAKE",false,.12f);
            var backdrop=Rect("Mode loading backdrop",page,0,0,1600,900).gameObject.AddComponent<CyberHomeBackdropGraphic>();
            backdrop.color=new Color(.015f,.03f,.065f);
            Box(page,"Mode loading shade",0,0,1600,900,new Color(.01f,.02f,.045f,.72f));
            modeLoadingScan=Box(page,"Mode loading scan",0,180,1600,3,new Color(.29f,.93f,.96f,.28f)).rectTransform;
            Panel(page,"Mode loading panel",270,245,1060,410,new Color(.025f,.055f,.105f,.96f),CyberTheme.Cyan);
            Mono(page,"Mode loading tag","[ ROUTE ACCESS / PIXEL HANDSHAKE ]",330,290,940,24,13,CyberTheme.Cyan);
            var heading=Label(page,"Mode loading heading","OPENING SELECT MODE",300,333,1000,74,48,true);
            Fit(heading,25,48);
            modeLoadingStatus=Mono(page,"Mode loading status","SYNCHRONIZING MAIN ROUTE DATA",330,414,940,22,13,CyberTheme.Muted,TextAnchor.MiddleCenter);
            Panel(page,"Pixel bar back",390,486,820,66,new Color(.01f,.02f,.04f,.96f),CyberTheme.Purple);
            modeLoadingCursor=Box(page,"Mode packet cursor",390,474,8,90,CyberTheme.Cyan).rectTransform;
            modeLoadingBlocks=new Image[28];
            for(int i=0;i<modeLoadingBlocks.Length;i++)
            {
                float height=i%3==0?50f:42f;
                modeLoadingBlocks[i]=Box(page,"Pixel load "+i,402+i*28,501-(height-44)*.5f,22,height,
                    new Color(.10f,.16f,.22f,.9f));
            }
            modeLoadingPercent=Mono(page,"Mode loading percent","000%",570,570,460,28,17,CyberTheme.Cyan,
                TextAnchor.MiddleCenter);
            Mono(page,"Mode loading footnote","ENTER! ROUTE LOCKED  /  PLEASE WAIT",580,636,440,20,11,CyberTheme.Muted,
                TextAnchor.MiddleCenter);
        }

        public void SetModeLoadingProgress(float value,float time)
        {
            value=Mathf.Clamp01(value);
            if(modeLoadingBlocks!=null)
            {
                int active=Mathf.FloorToInt(value*modeLoadingBlocks.Length);
                for(int i=0;i<modeLoadingBlocks.Length;i++)
                {
                    if(modeLoadingBlocks[i]==null)continue;
                    bool on=i<active;
                    float idle=5f+Mathf.Abs(Mathf.Sin(time*4.2f+i*.71f))*5f;
                    float lit=30f+Mathf.Abs(Mathf.Sin(time*9f-i*.48f))*15f;
                    var rect=modeLoadingBlocks[i].rectTransform;
                    rect.sizeDelta=new Vector2(22f,on?lit:idle);
                    modeLoadingBlocks[i].color=on
                        ?(i%3==0?CyberTheme.Cyan:CyberTheme.Lime)
                        :new Color(.09f,.14f,.20f,.86f);
                }
            }
            if(modeLoadingCursor!=null)
                modeLoadingCursor.anchoredPosition=new Vector2(390f+value*820f,-474f);
            if(modeLoadingScan!=null)
                modeLoadingScan.anchoredPosition=new Vector2(0,-(180f+value*520f));
            if(modeLoadingStatus!=null)
                modeLoadingStatus.text="SYNCHRONIZING ROUTE DATA  /  "+Mathf.RoundToInt(value*28f).ToString("D2")+" OF 28";
            if(modeLoadingPercent!=null)modeLoadingPercent.text=(Mathf.RoundToInt(value*100f)).ToString("D3")+"%";
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

        public void BeginModeGate()
        {
            EndModeGate();
            modeGateRoot=Rect("Mode route gate",artboard,0,0,1600,900);
            modeGateShade=Box(modeGateRoot,"Mode route shade",0,0,1600,900,new Color(.01f,.02f,.04f,0));
            float[] widths={276f,214f,352f,246f,318f,194f};
            float[] heights={900f,780f,900f,720f,900f,820f};
            float[] offsets={0f,72f,-20f,104f,-10f,54f};
            modeGateBands=new CyberPanelGraphic[widths.Length];
            modeGateStartPositions=new Vector2[widths.Length];
            modeGateTargetPositions=new Vector2[widths.Length];
            modeGateDelays=new float[widths.Length];
            modeGateDurations=new float[widths.Length];
            float cursorX=0;
            for(int i=0;i<modeGateBands.Length;i++)
            {
                Color color=i%3==0?new Color(.035f,.09f,.14f,.98f):
                    i%3==1?new Color(.055f,.06f,.14f,.98f):new Color(.10f,.04f,.13f,.98f);
                Vector2 target=new Vector2(cursorX,-offsets[i]);
                Vector2 start=i%3==0?new Vector2(target.x-1850f,target.y):
                    i%3==1?new Vector2(target.x+1850f,target.y):
                    new Vector2(target.x,target.y-1080f);
                var band=Panel(modeGateRoot,"Route gate "+i,start.x,start.y,widths[i],heights[i],color,
                    i%3==0?CyberTheme.Cyan:i%3==1?CyberTheme.Purple:CyberTheme.Pink);
                band.cut=i%2==0?18f:30f;
                modeGateBands[i]=band;
                modeGateStartPositions[i]=start;
                modeGateTargetPositions[i]=target;
                modeGateDelays[i]=i*.072f;
                modeGateDurations[i]=.68f+(i%3)*.13f;
                cursorX+=widths[i];
            }
            modeGateRoot.SetAsLastSibling();
        }

        public void SetModeGateIn(float value)
        {
            if(modeGateBands==null)return;
            value=Mathf.Clamp01(value);
            for(int i=0;i<modeGateBands.Length;i++)
            {
                if(modeGateBands[i]==null)continue;
                float delay=modeGateDelays[i];
                float local=Mathf.Clamp01((value-delay)/Mathf.Max(.01f,modeGateDurations[i]*(1f-delay)));
                modeGateBands[i].rectTransform.anchoredPosition=Vector2.Lerp(
                    modeGateStartPositions[i],modeGateTargetPositions[i],Mathf.SmoothStep(0,1,local));
            }
            if(modeGateShade!=null)
                modeGateShade.color=new Color(.01f,.02f,.04f,Mathf.SmoothStep(0,1,value)*.96f);
        }

        public void SetModeGateOut(float value)
        {
            if(modeGateBands==null)return;
            value=Mathf.Clamp01(value);
            for(int i=0;i<modeGateBands.Length;i++)
            {
                if(modeGateBands[i]==null)continue;
                float delay=modeGateDelays[i]*.82f;
                float local=Mathf.Clamp01((value-delay)/Mathf.Max(.01f,modeGateDurations[i]*(1f-delay)));
                Vector2 target=modeGateTargetPositions[i];
                Vector2 exit=i%2==0?new Vector2(target.x,target.y-1120f):
                    new Vector2(target.x,target.y+1120f);
                modeGateBands[i].rectTransform.anchoredPosition=Vector2.Lerp(
                    target,exit,Mathf.SmoothStep(0,1,local));
            }
            if(modeGateShade!=null)
                modeGateShade.color=new Color(.01f,.02f,.04f,(1-Mathf.SmoothStep(0,1,value))*.96f);
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
            modeGateStartPositions=null;
            modeGateTargetPositions=null;
            modeGateDelays=null;
            modeGateDurations=null;
            modeGateShade=null;
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

        public void ShowSongModes(bool slideFromMain=false)
        {
            Begin("Mode select","02   /   TRACK ACCESS",false,.18f,slideFromMain,slideFromMain?900:0);
            Mono(page,"Mode eyebrow","[ CHOOSE AN ACCESS ROUTE ]",64,137,760,24,13,CyberTheme.Cyan);
            Label(page,"Mode heading","SELECT MODE",64,174,860,74,54,true);
            Mono(page,"Mode help","TWO WAYS TO ENTER THE SIGNAL ARCHIVE  /  YOUR STORY PROGRESS IS PRESERVED",68,250,980,22,13,CyberTheme.Muted);
            ActionButton(page,"Back to main","<  MAIN MENU",1292,156,244,49,owner.ReturnToMainFromModes);

            var story=Panel(page,"Story mode card",64,320,704,478,CyberTheme.Panel,CyberTheme.Pink);
            story.raycastTarget=true;
            Box(story.rectTransform,"Story card rail",0,0,704,6,CyberTheme.Pink);
            Mono(story.rectTransform,"Story index","ROUTE 01",34,28,250,24,13,CyberTheme.Pink);
            Label(story.rectTransform,"Story heading","STORY MODE",30,72,640,66,44,true);
            Mono(story.rectTransform,"Story subtitle","FOLLOW THE MAIN SIGNAL PATH",34,140,620,24,14,CyberTheme.Muted);
            Box(story.rectTransform,"Story route line",52,230,600,3,CyberTheme.Alpha(CyberTheme.Cyan,.55f));
            for(int i=0;i<3;i++)
            {
                float x=78+i*218;
                var node=Panel(story.rectTransform,"Story node "+i,x,202,54,54,
                    i==storyLevelIndex?CyberTheme.Cyan:CyberTheme.PanelLight,
                    i==storyLevelIndex?CyberTheme.Cyan:CyberTheme.Purple);
                node.rounded=true;node.cornerRadius=27;
                Label(node.rectTransform,"Story node label","0-"+(i+1),0,2,54,50,19,true,
                    i==storyLevelIndex?CyberTheme.Background:CyberTheme.Muted,TextAnchor.MiddleCenter);
                Mono(story.rectTransform,"Story node state "+(i+1),i==storyLevelIndex?"CURRENT":"LOCKED",x-28,266,110,20,10,
                    i==storyLevelIndex?CyberTheme.Cyan:CyberTheme.Muted,TextAnchor.MiddleCenter);
            }
            ActionButton(story.rectTransform,"Open story mode","ENTER STORY  >",34,382,636,64,owner.ShowStoryLevels,true,CyberTheme.Pink,20);

            var free=Panel(page,"Free mode card",832,320,704,478,CyberTheme.Panel,CyberTheme.Cyan);
            free.raycastTarget=true;
            Box(free.rectTransform,"Free card rail",0,0,704,6,CyberTheme.Cyan);
            Mono(free.rectTransform,"Free index","ROUTE 02",34,28,250,24,13,CyberTheme.Cyan);
            Label(free.rectTransform,"Free heading","FREE MODE",30,72,640,66,44,true);
            Mono(free.rectTransform,"Free subtitle","PLAY ANY AVAILABLE CHART",34,140,620,24,14,CyberTheme.Muted);
            for(int i=0;i<8;i++)Box(free.rectTransform,"Free grid "+i,56+(i%4)*154,220+(i/4)*54,112,2,
                i%3==0?CyberTheme.Alpha(CyberTheme.Pink,.48f):CyberTheme.Alpha(CyberTheme.Cyan,.35f));
            ActionButton(free.rectTransform,"Open free mode","ENTER FREE PLAY  >",34,382,636,64,owner.ShowSongs,true,CyberTheme.Cyan,20);
            if(slideFromMain)StartPageSlide(false);
        }
        public void ShowStoryLevels(int currentLevel)
        {
            storyLevelIndex=Mathf.Clamp(Mathf.Max(0,currentLevel),0,2);
            Begin("Story levels","03   /   STORY ROUTE",false);
            Mono(page,"Story eyebrow","[ MAIN SIGNAL / STORY CHAPTERS ]",64,137,760,24,13,CyberTheme.Pink);
            Label(page,"Story heading","STORY ROUTE",64,174,860,74,54,true);
            Mono(page,"Story status","CURRENT PROGRESS  /  0-"+(storyLevelIndex+1)+"  /  SELECT A CHAPTER",68,250,900,22,13,CyberTheme.Muted);
            ActionButton(page,"Back to modes","<  SELECT MODE",1292,156,244,49,owner.ShowSongModes);
            Box(page,"Story route rail",210,520,1180,3,CyberTheme.Alpha(CyberTheme.Cyan,.48f));
            string[] names={"FIRST LIGHT","SIGNAL LOST","THE LAST TRACE"};
            for(int i=0;i<3;i++)
            {
                float x=224+i*460;
                bool available=i==0;
                bool current=i==storyLevelIndex;
                bool active=available;
                int chapterIndex=i;
                var card=Panel(page,"Story chapter "+i,x,330,300,310,
                    active?new Color(.07f,.16f,.21f,.98f):new Color(.025f,.04f,.08f,.94f),
                    active?CyberTheme.Cyan:CyberTheme.Purple);
                Box(card.rectTransform,"Chapter rail",0,0,300,5,active?CyberTheme.Cyan:CyberTheme.Purple);
                Mono(card.rectTransform,"Chapter number","0-"+(i+1),24,26,150,28,16,active?CyberTheme.Cyan:CyberTheme.Muted);
                Label(card.rectTransform,"Chapter name",names[i],22,76,256,60,29,true,active?CyberTheme.Text:CyberTheme.Muted);
                string state=available?(current?"CURRENT / OPEN":"OPEN"):"LOCKED";
                Mono(card.rectTransform,"Chapter state",state,24,151,250,20,11,active?CyberTheme.Lime:CyberTheme.Muted);
                Box(card.rectTransform,"Chapter divider",24,188,252,1,CyberTheme.Alpha(active?CyberTheme.Cyan:CyberTheme.Purple,.45f));
                Mono(card.rectTransform,"Chapter note",available?"DEFAULT CHAPTER / READY TO ENTER":"COMPLETE THE PREVIOUS CHAPTER",24,205,250,42,12,CyberTheme.Muted);
                if(active)
                    ActionButton(card.rectTransform,"Open story chapter "+i,"ENTER 0-1  >",22,256,256,42,
                        ()=>owner.EnterStoryLevel(chapterIndex),true,CyberTheme.Cyan,15);
                else
                    Diamond(card.rectTransform,150,276,8,CyberTheme.Purple);
            }
            Mono(page,"Story footer","0-1 STORY ONLINE  /  SELECT THE CURRENT CHAPTER TO ENTER THE RECORDING ROOM",68,817,1200,22,12,CyberTheme.Muted);
        }
        public void ShowStoryIntro(string title,string chapter)
        {
            Begin("Story intro","00   /   STORY 0-1",false,.01f);
            Box(page,"Story intro background",0,0,1600,900,new Color(.01f,.025f,.055f,1f));
            Box(page,"Story intro rail",0,446,1600,3,CyberTheme.Cyan);
            Mono(page,"Story intro chapter",chapter,560,350,480,26,15,CyberTheme.Cyan,TextAnchor.MiddleCenter);
            var heading=StoryText(page,"Story intro title",title,300,390,1000,90,48,true,CyberTheme.Text,
                TextAnchor.MiddleCenter);
            Fit(heading,28,48);
            Mono(page,"Story intro status","RECORDING ARCHIVE  /  LOADING SCENE",560,500,480,22,12,CyberTheme.Muted,
                TextAnchor.MiddleCenter);
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

            Box(page,"Story vignette",0,0,1600,900,new Color(.01f,.02f,.04f,.08f));
            var topLayer=Rect("Story top layer",page,0,0,1600,92);
            Panel(topLayer,"Story top bar",0,0,1600,92,new Color(.015f,.028f,.06f,.94f),CyberTheme.Cyan);
            Box(topLayer,"Story top beam",0,0,1600,5,CyberTheme.Cyan);
            Mono(topLayer,"Story route","R/H  //  STORY ROUTE 01",48,24,600,24,13,CyberTheme.Cyan);
            var storyTitle=StoryText(topLayer,"Story title",story.title==null?"":story.title,48,48,640,30,24,true,CyberTheme.Text);
            Fit(storyTitle,15,24);
            Mono(topLayer,"Story node index","NODE  "+(Mathf.Clamp(GetStoryNodeIndex(story,node),0,999).ToString("D3")),1040,27,220,22,12,CyberTheme.Muted,TextAnchor.MiddleRight);
            Mono(topLayer,"Story variables","VARIABLES  "+(variables.Count==0?"NONE":FormatStoryVariables(variables)),880,52,460,22,11,CyberTheme.Muted,TextAnchor.MiddleRight);
            StoryActionButton(topLayer,"Skip story","SKIP STORY  >",1368,24,184,48,owner.SkipStoryToStage,true,
                CyberTheme.Cyan,15);

            bool choicePanel=node.kind==StoryRuntimeNodeKind.Choice;
            float panelY=choicePanel?570f:676f;
            float panelHeight=choicePanel?310f:214f;
            Color panelColor=choicePanel?new Color(.012f,.022f,.05f,.96f):new Color(.012f,.022f,.05f,.90f);
            var dialoguePanel=Panel(page,"Story dialogue panel",38,panelY,1524,panelHeight,panelColor,CyberTheme.Cyan);
            Box(dialoguePanel.rectTransform,"Dialogue panel rail",0,0,1524,4,CyberTheme.Cyan);
            if(node.kind==StoryRuntimeNodeKind.Dialogue)
            {
                dialoguePanel.raycastTarget=true;
                var advanceButton=dialoguePanel.gameObject.AddComponent<Button>();
                advanceButton.targetGraphic=dialoguePanel;
                advanceButton.onClick.AddListener(StoryContinue);
            }
            storyDialogueGroup=dialoguePanel.gameObject.AddComponent<CanvasGroup>();
            storyDialogueRect=dialoguePanel.rectTransform;
            storyDialogueTo=storyDialogueRect.anchoredPosition;
            storyDialogueFrom=storyDialogueTo+new Vector2(0,28);
            storyDialogueRect.anchoredPosition=sceneTransition?storyDialogueFrom:storyDialogueTo;
            storyDialogueGroup.alpha=sceneTransition?0:1;
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
                storyPortraitFrom=storyPortraitTo+new Vector2(0,22);
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
            DrawStoryBottomFade();
            dialoguePanel.rectTransform.SetAsLastSibling();
            topLayer.SetAsLastSibling();

            if(node.kind==StoryRuntimeNodeKind.Dialogue)
            {
                string speakerName=character==null?"旁白":character.displayName;
                var speaker=StoryText(dialoguePanel.rectTransform,"Story speaker",speakerName,30,18,800,32,22,true,
                    character==null?CyberTheme.Text:ParseStoryColor(character.colorHex));
                var body=StoryText(dialoguePanel.rectTransform,"Story dialogue","",
                    30,56,1464,142,22,false,new Color(.94f,.96f,1f));
                Fit(body,15,23);
                BeginStoryTyping(body,ResolveStoryText(node,variables));
            }
            else if(node.kind==StoryRuntimeNodeKind.Choice)
            {
                var prompt=StoryText(dialoguePanel.rectTransform,"Story question",node.text,28,15,1260,34,21,true,CyberTheme.Text);
                if(node.choices!=null)
                {
                    for(int i=0;i<node.choices.Length;i++)
                    {
                        int choiceIndex=i;
                        var choice=node.choices[i];
                        if(choice==null)continue;
                        string suffix="   ["+(string.IsNullOrEmpty(choice.variable)?"":choice.variable+" = ")+(choice.value??"")+"]";
                        StoryActionButton(dialoguePanel.rectTransform,"Story choice "+i,choice.text+suffix,26,54+i*61,1410,52,
                            ()=>owner.ChooseStoryOption(choiceIndex),false,new Color(.035f,.075f,.14f,.98f),17);
                    }
                }
                StoryText(dialoguePanel.rectTransform,"Story choice hint","CHOOSE AN OPTION TO CONTINUE",30,238,760,22,12,false,CyberTheme.Muted);
            }
            else if(node.kind==StoryRuntimeNodeKind.Stage)
            {
                dialoguePanel.gameObject.SetActive(false);
                DrawStoryCommand(story,node,"RHYTHM STAGE",
                    "LEVEL  "+node.levelId+"   /   "+(node.autoplay?"AUTOPLAY":"MANUAL"),
                    "ENTER STAGE  >",owner.EnterStoryStage);
            }
            else
            {
                dialoguePanel.gameObject.SetActive(false);
                DrawStoryCommand(story,node,"STORY COMPLETE",
                    string.IsNullOrWhiteSpace(node.text)?"THE RECORDING HAS ENDED.":ResolveStoryText(node,variables),
                    "RETURN TO CHAPTERS",owner.ReturnToStoryLevels);
            }
            storyTransitionStarted=sceneTransition?Time.unscaledTime:-1;
        }

        void DrawStoryBottomFade()
        {
            for(int i=0;i<8;i++)
            {
                float y=515+i*47;
                float alpha=.018f+i*.026f;
                Box(page,"Story bottom fade "+i,0,y,1600,49,new Color(.005f,.012f,.025f,alpha));
            }
        }

        void DrawStoryCommand(StoryRuntimeProjectData story,StoryRuntimeNodeData node,string tag,string body,
            string actionLabel,Action action)
        {
            var panel=Panel(page,"Story command",420,276,760,252,new Color(.012f,.024f,.055f,.94f),CyberTheme.Cyan);
            storyDialogueGroup=panel.gameObject.AddComponent<CanvasGroup>();
            storyDialogueRect=panel.rectTransform;
            storyDialogueTo=storyDialogueRect.anchoredPosition;
            storyDialogueFrom=storyDialogueTo+new Vector2(0,28);
            bool animate=node.kind==StoryRuntimeNodeKind.Background;
            storyDialogueRect.anchoredPosition=animate?storyDialogueFrom:storyDialogueTo;
            storyDialogueGroup.alpha=animate?0:1;
            Box(panel.rectTransform,"Story command rail",0,0,760,4,CyberTheme.Cyan);
            Mono(panel.rectTransform,"Story command tag",tag,34,25,690,24,13,CyberTheme.Cyan);
            StoryText(panel.rectTransform,"Story command body",body,34,68,692,80,24,true,CyberTheme.Text);
            StoryActionButton(panel.rectTransform,"Story command action",actionLabel,88,162,584,52,action,true,
                CyberTheme.Cyan,18);
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
            text.font=CyberTheme.Chinese;
            return text;
        }

        Button StoryActionButton(RectTransform parent,string name,string caption,float x,float y,float width,
            float height,Action callback,bool primary,Color? background,int fontSize)
        {
            var button=ActionButton(parent,name,caption,x,y,width,height,callback,primary,background,fontSize);
            foreach(var text in button.GetComponentsInChildren<Text>())text.font=CyberTheme.Chinese;
            return button;
        }

        public void ShowSongs(IReadOnlyList<SongEntry> songs,int selected,bool slideFromMain=false)
        {
            Begin("Song selection","03   /   MUSIC ARCHIVE",true,.18f,slideFromMain,slideFromMain?900:0);
            Label(page,"Library heading","SELECT TRACK",64,135,870,79,54,true);
            Mono(page,"Library count",songs.Count.ToString("D2")+" TRACK"+(songs.Count==1?"":"S")+"  /  BUNDLED JSON COLLECTION",67,214,760,24,13,CyberTheme.Muted);
            ActionButton(page,"Back to modes","<  SELECT MODE",1292,156,244,49,owner.ShowSongModes);
            // Real scrolling catalog. Rebuilding the detail view keeps the selected row visible.
            var viewport=Rect("Song list viewport",page,64,268,422,436);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color=CyberTheme.Alpha(CyberTheme.Panel,.2f);
            var content=Rect("Song rows",viewport,0,0,422,Math.Max(436,songs.Count*126));
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
            content.anchoredPosition=new Vector2(0,Mathf.Clamp(selected*126-126,0,Math.Max(0,songs.Count*126-436)));
            for(int i=0;i<songs.Count;i++)
            {
                int index=i;bool active=i==selected;
                var row=ActionButton(content,"Select song "+i,"",0,i*126,422,112,()=>owner.SelectSong(index),false,active?CyberTheme.PanelLight:CyberTheme.Panel);
                var p=(RectTransform)row.transform;
                Box(p,"Selected stripe",0,12,3,87,active?CyberTheme.Cyan:CyberTheme.Purple);
                Mono(p,"Track number",(i+1).ToString("D2"),21,21,52,42,24,active?CyberTheme.Cyan:CyberTheme.Muted);
                var name=Label(p,"Track title",songs[i].ShortTitle.ToUpperInvariant(),87,17,306,43,29,true);Fit(name,16,29);
                Mono(p,"Track metadata",FormatTime(songs[i].Duration)+"  /  "+songs[i].Bpm+" BPM",88,68,312,25,13,CyberTheme.Muted);
            }
            Mono(page,"Library footnote","[ LOCAL ARCHIVE ]\nADD JSON CHARTS TO EXPAND THE LIBRARY",67,741,427,67,12,CyberTheme.Muted);
            if(songs.Count==0)
            {
                Panel(page,"Empty archive",538,268,998,432,CyberTheme.Panel,CyberTheme.Purple);
                Label(page,"Empty library","NO SIGNAL FOUND",577,344,908,71,44,true);
                Label(page,"Empty help","Add a valid JSON chart to Resources/Charts.\nInvalid charts are reported in the Unity Console.",580,450,860,100,25,false,CyberTheme.Muted);
                return;
            }
            var entry=songs[selected];
            Panel(page,"Album sleeve",538,267,446,446,CyberTheme.Panel,CyberTheme.Purple);
            Box(page,"Album spine",538,294,6,391,CyberTheme.Purple);
            Mono(page,"Album edition","R/H  //  SPATIAL ARCHIVE",563,288,397,25,12,CyberTheme.Cyan);
            Artwork(page,558,321,406,321);
            var cover=Label(page,"Cover title",entry.ShortTitle.ToUpperInvariant(),564,647,390,42,30,true,CyberTheme.Cyan,TextAnchor.MiddleCenter);Fit(cover,17,30);
            Scanner(552,320,418,302);
            Mono(page,"Chart tag","CHART DATA   /   "+(selected+1).ToString("D3"),1024,270,490,22,13,CyberTheme.Lime);
            var title=Label(page,"Selected title",entry.ShortTitle.ToUpperInvariant(),1020,309,516,82,49,true);Fit(title,25,49);
            var author=Label(page,"Selected artist",string.IsNullOrWhiteSpace(entry.Chart.author)?"INDEPENDENT CHART":entry.Chart.author,1024,400,510,41,23,false,CyberTheme.Muted);Fit(author,14,23);
            Box(page,"Metadata rail",1024,464,512,2,CyberTheme.Purple);
            Metric("TEMPO",entry.Bpm,1024,490,243,92,CyberTheme.Cyan,"BPM");
            Metric("DURATION",FormatTime(entry.Duration),1290,490,246,92,CyberTheme.Text);
            Metric("NOTES",entry.Chart.notes.Length.ToString(),1024,605,243,91,CyberTheme.Text);
            Metric("MAX PATHS",entry.MaxPaths.ToString("D2"),1290,605,246,91,CyberTheme.Purple);
            ActionButton(page,"Watch autoplay","AUTOPLAY / PREVIEW",538,754,328,64,()=>owner.Play(true));
            ActionButton(page,"Play chart","PLAY CHART   /   MANUAL  >",890,754,646,64,()=>owner.Play(false),true);
            if(slideFromMain)StartPageSlide(false);
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
                new Vector2(12,9),new Vector2(68,48),()=>ClearPopup(ref returnPopup));
            var panel=Panel(returnPopup.Root,"Return confirmation",0,0,880,520,
                new Color(.018f,.026f,.075f,.98f),CyberTheme.Cyan);
            panel.raycastTarget=true;
            Box(panel.rectTransform,"Confirmation top rail",0,0,880,6,CyberTheme.Cyan);
            Mono(panel.rectTransform,"Confirmation tag","[ NAVIGATION / RETURN ]",46,34,650,24,13,CyberTheme.Cyan);
            Label(panel.rectTransform,"Confirmation heading","RETURN TO TITLE?",44,84,790,72,46,true);
            Label(panel.rectTransform,"Confirmation detail","Leave the MainUI and return to the title screen.\nYour current track selection will be kept.",48,176,780,72,22,false,CyberTheme.Muted);
            Box(panel.rectTransform,"Confirmation rail",48,277,784,1,CyberTheme.Alpha(CyberTheme.Purple,.48f));
            ActionButton(panel.rectTransform,"Cancel return","STAY",62,354,330,76,CloseReturnConfirmation,false,null,23);
            ActionButton(panel.rectTransform,"Confirm return","RETURN TO TITLE",488,354,330,76,owner.ConfirmReturnFromMain,true,null,23);
            Mono(panel.rectTransform,"Confirmation footer","SELECT STAY TO CONTINUE",48,458,780,20,11,CyberTheme.Muted,TextAnchor.MiddleCenter);
            StartPopup(returnPopup,false);
        }
        void CloseReturnConfirmation()
        {
            if(returnPopup!=null)StartPopup(returnPopup,true);
        }
        void ShowControls()
        {
            if(controlsPopup!=null)return;
            controlsPopup=CreatePopup(page,"Controls popup",418,145,764,616,
                new Vector2(1520,10),new Vector2(66,46),()=>ClearPopup(ref controlsPopup));
            var overlay=Panel(controlsPopup.Root,"Controls overlay",0,0,764,616,new Color(.012f,.018f,.052f,.98f),CyberTheme.Cyan);
            overlay.raycastTarget=true;
            Box(overlay.rectTransform,"Controls beam",0,0,764,6,CyberTheme.Cyan);
            Mono(overlay.rectTransform,"Controls tag","[ PILOT CONTROLS / LOCAL SESSION ]",34,28,520,23,13,CyberTheme.Cyan);
            Label(overlay.rectTransform,"Controls heading","HOW TO PLAY",31,64,680,60,42,true);
            Mono(overlay.rectTransform,"Controls intro","The rhythm enters on the spatial paths. Follow the signal, not the screen edges.",34,132,690,24,14,CyberTheme.Muted);
            ControlRow(overlay.rectTransform,"TAP","01","PRESS WHEN A NOTE REACHES THE RING",34,185,CyberTheme.Cyan);
            ControlRow(overlay.rectTransform,"DRAG","02","HOLD AND FOLLOW A DRAG NOTE",34,266,CyberTheme.Purple);
            ControlRow(overlay.rectTransform,"PAUSE","SPACE / ESC","OPEN THE PAUSE CONSOLE AT ANY TIME",34,347,CyberTheme.Pink);
            ControlRow(overlay.rectTransform,"PRACTICE","LEFT / RIGHT","SKIP FORWARD OR BACK IN THE CHART",34,428,CyberTheme.Lime);
            Mono(overlay.rectTransform,"Controls footnote","AUTOPLAY PREVIEW IS AVAILABLE FROM THE TRACK ARCHIVE.",34,526,690,20,12,CyberTheme.Muted);
            ActionButton(overlay.rectTransform,"Close controls","CLOSE",546,534,180,50,CloseControls,true,null,19);
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
                new Vector2(1458,568),new Vector2(98,98),()=>ClearPopup(ref backgroundPopup));
            var menu=Panel(backgroundPopup.Root,"Background selection",0,0,438,472,new Color(.018f,.025f,.075f,.97f),CyberTheme.Pink);
            menu.raycastTarget=true;
            Box(menu.rectTransform,"Menu top rail",0,0,438,6,CyberTheme.Pink);
            Mono(menu.rectTransform,"Menu tag","[ VISUAL STAGE ]",25,22,250,21,12,CyberTheme.Cyan);
            Label(menu.rectTransform,"Menu heading","CHANGE BACKGROUND",23,48,330,42,27,true);
            var closeBackground=ActionButton(menu.rectTransform,"Close background menu","X",372,20,42,42,CloseBackgroundMenu,false,new Color(.12f,.04f,.22f,.98f),17);
            RoundIcon(closeBackground,16);
            for(int i=0;i<homeBackgrounds.Length;i++)
            {
                int index=i;bool selected=i==homeBackgroundIndex;
                var button=ActionButton(menu.rectTransform,"Select background "+(i+1),"",23,104+i*122,392,104,()=>SelectHomeBackground(index),false,new Color(.04f,.06f,.13f,.94f));
                var buttonPanel=(CyberPanelGraphic)button.targetGraphic;buttonPanel.accent=selected?CyberTheme.Cyan:CyberTheme.Purple;
                RoundIcon(button,14);
                var thumbnail=Rect("Thumbnail",buttonPanel.rectTransform,8,8,190,88).gameObject.AddComponent<RawImage>();
                Texture2D texture=LoadHomeBackground(i);thumbnail.texture=texture;thumbnail.uvRect=texture!=null?CoverTexture(texture):new Rect(0,0,1,1);thumbnail.raycastTarget=false;
                Label(buttonPanel.rectTransform,"Stage label","0-"+(i+1),220,18,148,35,28,true,selected?CyberTheme.Cyan:CyberTheme.Text,TextAnchor.MiddleLeft);
                Mono(buttonPanel.rectTransform,"Stage state",selected?"CURRENT":"SELECT",220,58,148,18,10,selected?CyberTheme.Cyan:CyberTheme.Muted);
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
            homeBackgroundImage.uvRect=texture!=null?CoverTexture(texture):new Rect(0,0,1,1);
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
            homeBackgroundImageNext.uvRect=CoverTexture(texture);
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
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-homeBackgroundFadeStarted)/.72f));
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
            if(reveal>.995f&&titleBackdrop!=null)titleBackdrop.enabled=false;
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
            Fit(heading,12,17);
            var copy=Label(panel.rectTransform,"Notification detail",Excerpt(detail,52),72,44,340,34,13,false,CyberTheme.Muted);
            copy.lineSpacing=.9f;
            var item=new NotificationItem{Root=button.gameObject,Rect=rect,Group=group,Kind=kind,Target=target,
                ExpiresAt=Time.unscaledTime+8f,SpawnedAt=Time.unscaledTime,TargetPosition=new Vector2(24,-(88+notifications.Count*100)),OnClick=onClick};
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
                    item.Rect.anchoredPosition=Vector2.Lerp(item.TargetPosition,new Vector2(-460,item.TargetPosition.y),closeT);
                    item.Group.alpha=1-closeT;
                    if(closeT>=1f)
                    {
                        UnityEngine.Object.Destroy(item.Root);
                        notifications.RemoveAt(i);
                    }
                }
                else
                {
                    float enterT=Mathf.SmoothStep(0,1,Mathf.Clamp01((now-item.SpawnedAt)/.32f));
                    item.Rect.anchoredPosition=Vector2.Lerp(new Vector2(-460,item.TargetPosition.y),item.TargetPosition,enterT);
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
            ClearPopup(ref returnPopup);ClearPopup(ref controlsPopup);
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
            if(!buttons.TryGetValue(name,out var button)||!button.IsActive()||!button.interactable)return false;
            button.onClick.Invoke();return true;
        }
        public void UseCaptureCamera(Camera camera)
        {
            canvas.renderMode=camera==null?RenderMode.ScreenSpaceOverlay:RenderMode.ScreenSpaceCamera;
            canvas.worldCamera=camera;canvas.planeDistance=.5f;
            if(camera!=null){if(pageFade!=null)pageFade.alpha=1;SetTransitionShade(0);} // Deterministic screenshot, without waiting for entry fade.
            if(camera!=null && titleCtaGroup!=null){titleCtaGroup.alpha=1;titleCtaGroup.interactable=true;titleCtaGroup.blocksRaycasts=true;}
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
        static CyberPanelGraphic Panel(RectTransform parent,string name,float x,float y,float width,float height,Color color,Color accent)
        {
            var p=Rect(name,parent,x,y,width,height).gameObject.AddComponent<CyberPanelGraphic>();p.color=color;p.accent=accent;p.raycastTarget=false;return p;
        }
        static Text Label(RectTransform parent,string name,string value,float x,float y,float width,float height,int size,bool bold=false,Color? color=null,TextAnchor align=TextAnchor.UpperLeft)
        {
            var t=Rect(name,parent,x,y,width,height).gameObject.AddComponent<Text>();
            t.font=bold?CyberTheme.Bold:CyberTheme.Regular;t.fontSize=size;t.text=value;t.color=color??CyberTheme.Text;
            t.alignment=align;t.raycastTarget=false;t.supportRichText=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
        }
        static Text Mono(RectTransform parent,string name,string value,float x,float y,float width,float height,int size,Color color,TextAnchor align=TextAnchor.UpperLeft)
        {
            var t=Label(parent,name,value,x,y,width,height,size,false,color,align);t.font=CyberTheme.Mono;return t;
        }
        static void Fit(Text t,int min,int max){t.resizeTextForBestFit=true;t.resizeTextMinSize=min;t.resizeTextMaxSize=max;}
        Button ActionButton(RectTransform parent,string name,string caption,float x,float y,float width,float height,Action callback,bool primary=false,Color? background=null,int fontSize=23)
        {
            var image=Panel(parent,name,x,y,width,height,background??(primary?CyberTheme.Cyan:CyberTheme.Panel),primary?CyberTheme.Cyan:CyberTheme.Purple);image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(.77f,.84f,1);colors.pressedColor=new Color(.55f,.69f,.85f);colors.fadeDuration=.1f;button.colors=colors;
            button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(()=>callback());buttons[name]=button;
            if(caption.Length>0)Label(image.rectTransform,"Caption",caption,16,0,width-32,height,fontSize,true,primary?CyberTheme.Background:CyberTheme.Text,TextAnchor.MiddleCenter);
            return button;
        }
        static void RoundIcon(Button button,float radius)
        {
            var panel=button.targetGraphic as CyberPanelGraphic;
            if(panel==null)return;
            panel.rounded=true;
            panel.cornerRadius=radius;
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
        static string FormatTime(double time)=>((int)time/60).ToString("D2")+":"+((int)time%60).ToString("D2");
    }
}
