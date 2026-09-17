using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        RectTransform page,scan;
        CanvasGroup pageFade;
        float pageStarted,lastAnimation;
        Rect lastSafe;
        Vector2 lastCanvas;

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
        }
        public void UpdateLayout()
        {
            if(!canvas.gameObject.activeSelf) return;
            // Tiny, non-flashing presentation animation. No per-frame artwork mesh rebuild.
            if(pageFade!=null) pageFade.alpha=Mathf.Clamp01((Time.unscaledTime-pageStarted)/.18f);
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
        float scanStart,scanHeight;
        void Begin(string name,string section)
        {
            canvas.gameObject.SetActive(true);buttons.Clear();scan=null;
            if(page!=null){page.gameObject.SetActive(false);UnityEngine.Object.Destroy(page.gameObject);}
            page=Rect(name,artboard,0,0,1600,900);pageFade=page.gameObject.AddComponent<CanvasGroup>();
            pageStarted=Time.unscaledTime;pageFade.alpha=0;
            var frame=Rect("Circuit frame",page,0,0,1600,900).gameObject.AddComponent<CyberFrameGraphic>();frame.raycastTarget=false;
            var logo=Panel(page,"Brand plate",48,35,95,60,CyberTheme.PanelLight,CyberTheme.Cyan);
            Label(logo.rectTransform,"Monogram","G/R",8,3,79,51,34,true,CyberTheme.Cyan,TextAnchor.MiddleCenter);
            var brand=Label(page,"Brand",gameTitle.Replace('\n',' ').ToUpperInvariant(),166,40,680,31,25,true);Fit(brand,14,25);
            Mono(page,"Subsystem","SPATIAL RHYTHM SYSTEM  /  LOCAL CLIENT",168,75,720,17,12,CyberTheme.Muted);
            Mono(page,"Page index",section,1020,45,500,29,15,CyberTheme.Text,TextAnchor.MiddleRight);
            Mono(page,"Connection","OFFLINE  /  STANDALONE",1100,77,420,17,11,CyberTheme.Cyan,TextAnchor.MiddleRight);
            Mono(page,"Footer","G/R   //   TRACE THE SIGNAL",48,861,610,22,12,CyberTheme.Muted);
            Mono(page,"Version","REAL-TIME 3D  /  PROTOTYPE 01",1030,851,490,22,12,CyberTheme.Muted,TextAnchor.MiddleRight);
            UpdateLayout();
        }
        public void ShowTitle()
        {
            Begin("Title page","01   /   SYSTEM ENTRY");
            Mono(page,"Eyebrow","[ AUDIO / VISUAL INTERFACE ]",80,185,740,28,17,CyberTheme.Cyan);
            // Offset outline-like shadow is deliberately static, not a flickering glitch.
            var shadow=Label(page,"Title chromatic shadow",gameTitle.ToUpperInvariant(),77,252,830,233,110,true,CyberTheme.Alpha(CyberTheme.Purple,.22f));
            shadow.lineSpacing=.83f;Fit(shadow,45,110);
            var title=Label(page,"Game title",gameTitle.ToUpperInvariant(),80,249,830,233,110,true);title.lineSpacing=.83f;Fit(title,45,110);
            Box(page,"Title underline",83,504,110,4,CyberTheme.Cyan);
            Box(page,"Title underline secondary",200,504,257,1,CyberTheme.Alpha(CyberTheme.Purple,.65f));
            Label(page,"Title description","DECODE THE RHYTHM.\nTRACE THE SIGNAL.",82,544,790,92,32,true,CyberTheme.Muted);
            Mono(page,"Capabilities","3D SPACE    /    MULTIPLE PATHS    /    ONE RHYTHM",84,655,820,25,14,CyberTheme.Cyan);
            ActionButton(page,"Open songs","ENTER MUSIC ARCHIVE   /   START",80,708,650,74,owner.ShowSongs,true);
            Mono(page,"Enter hint","[ ENTER ]   CONNECT TO YOUR NEXT TRACK",84,801,820,22,12,CyberTheme.Muted);
            Panel(page,"Core housing",938,189,574,526,CyberTheme.Alpha(CyberTheme.Panel,.5f),CyberTheme.Purple);
            Mono(page,"Core index","SIGNAL CORE  /  001",966,213,495,23,13,CyberTheme.Cyan);
            Artwork(page,978,248,494,432);
            Scanner(951,242,548,441);
            Mono(page,"Core caption","GEOMETRY  /  FIRST COLLECTION",960,744,535,23,15,CyberTheme.Text,TextAnchor.MiddleCenter);
            Mono(page,"Core microtype","SYNTHETIC AUDIO  +  SPATIAL MOTION",960,778,535,20,11,CyberTheme.Muted,TextAnchor.MiddleCenter);
        }
        public void ShowSongs(IReadOnlyList<SongEntry> songs,int selected)
        {
            Begin("Song selection","02   /   MUSIC ARCHIVE");
            Label(page,"Library heading","SELECT TRACK",64,135,870,79,54,true);
            Mono(page,"Library count",songs.Count.ToString("D2")+" TRACK"+(songs.Count==1?"":"S")+"  /  BUNDLED JSON COLLECTION",67,214,760,24,13,CyberTheme.Muted);
            ActionButton(page,"Back to title","<  SYSTEM ENTRY",1292,156,244,49,owner.ShowTitle);
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
            Mono(page,"Album edition","G/R  //  SPATIAL ARCHIVE",563,288,397,25,12,CyberTheme.Cyan);
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
        }
        public void ShowResults(SongEntry song,PlayResult result)
        {
            Begin("Results page","03   /   SESSION REPORT");
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
            ActionButton(page,"Back to songs","<  MUSIC ARCHIVE",64,761,395,58,owner.ShowSongs);
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
        public void Hide()=>canvas.gameObject.SetActive(false);
        public bool Invoke(string name)
        {
            if(!buttons.TryGetValue(name,out var button)||!button.IsActive()||!button.interactable)return false;
            button.onClick.Invoke();return true;
        }
        public void UseCaptureCamera(Camera camera)
        {
            canvas.renderMode=camera==null?RenderMode.ScreenSpaceOverlay:RenderMode.ScreenSpaceCamera;
            canvas.worldCamera=camera;canvas.planeDistance=.5f;
            if(camera!=null && pageFade!=null)pageFade.alpha=1; // Deterministic screenshot, without waiting for entry fade.
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
        Button ActionButton(RectTransform parent,string name,string caption,float x,float y,float width,float height,Action callback,bool primary=false,Color? background=null)
        {
            var image=Panel(parent,name,x,y,width,height,background??(primary?CyberTheme.Cyan:CyberTheme.Panel),primary?CyberTheme.Cyan:CyberTheme.Purple);image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(.77f,.84f,1);colors.pressedColor=new Color(.55f,.69f,.85f);colors.fadeDuration=.1f;button.colors=colors;
            button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(()=>callback());buttons[name]=button;
            if(caption.Length>0)Label(image.rectTransform,"Caption",caption,16,0,width-32,height,23,true,primary?CyberTheme.Background:CyberTheme.Text,TextAnchor.MiddleCenter);
            return button;
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
