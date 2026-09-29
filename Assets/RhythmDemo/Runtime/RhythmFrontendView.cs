using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    /// <summary>Landscape, touch-first rhythm-game UI. All essential controls are at least
    /// 112 design units high. Menus and HUD share safe-area fitting, without moving the world.</summary>
    public sealed class RhythmFrontendView
    {
        readonly RhythmFrontend owner;
        readonly string gameTitle;
        readonly Canvas canvas;
        readonly RectTransform artboard;
        readonly Dictionary<string,Button> buttons=new Dictionary<string,Button>();
        RectTransform page;
        CanvasGroup pageFade;
        PagedSongCarousel carousel;
        float pageStarted;
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
            wash.GetComponent<Image>().color=CyberTheme.Background; // Blocks input from reaching the playfield.
            artboard=new GameObject("Mobile safe area / 1600 x 900",typeof(RectTransform)).GetComponent<RectTransform>();
            artboard.SetParent(obj.transform,false);artboard.anchorMin=artboard.anchorMax=new Vector2(.5f,.5f);
            artboard.sizeDelta=new Vector2(1600,900);
        }
        public void UpdateLayout()
        {
            if(!canvas.gameObject.activeSelf)return;
            if(pageFade!=null)pageFade.alpha=Mathf.Clamp01((Time.unscaledTime-pageStarted)/.18f);
            var size=((RectTransform)canvas.transform).rect.size;var safe=MobileUiLayout.SafeArea;
            if(lastSafe==safe && lastCanvas==size)return;
            lastSafe=safe;lastCanvas=size;MobileUiLayout.Apply(artboard,canvas);
        }
        void Begin(string name,string section)
        {
            canvas.gameObject.SetActive(true);buttons.Clear();carousel=null;
            if(page!=null){page.gameObject.SetActive(false);UnityEngine.Object.Destroy(page.gameObject);}
            page=Rect(name,artboard,0,0,1600,900);pageFade=page.gameObject.AddComponent<CanvasGroup>();
            pageStarted=Time.unscaledTime;pageFade.alpha=0;
            Rect("Neon edge frame",page,0,0,1600,900).gameObject.AddComponent<CyberFrameGraphic>().raycastTarget=false;
            Label(page,"Page heading",section,224,43,1050,88,56,true);
            Label(page,"Brand","G/R",1400,54,136,61,38,true,CyberTheme.Cyan,TextAnchor.MiddleRight);
            UpdateLayout();
        }
        void Back(string name,Action callback)=>ActionButton(page,name,"<",48,30,128,116,callback);
        public void ShowTitle()
        {
            Begin("Title page","");
            Label(page,"Tagline","A RHYTHM THROUGH SPACE",134,136,1312,68,36,true,CyberTheme.Cyan,TextAnchor.MiddleCenter);
            Artwork(page,1054,232,387,387);
            var shadow=Label(page,"Title chromatic shadow",gameTitle.ToUpperInvariant(),179,243,935,289,124,true,CyberTheme.Alpha(CyberTheme.Purple,.25f));
            shadow.lineSpacing=.85f;Fit(shadow,55,124);
            var title=Label(page,"Game title",gameTitle.ToUpperInvariant(),182,240,935,289,124,true);
            title.lineSpacing=.85f;Fit(title,55,124);
            Box(page,"Title underline",185,554,126,5,CyberTheme.Cyan);
            Label(page,"Title subtitle","FOLLOW THE LINES. FEEL THE RHYTHM.",183,585,920,71,33,false,CyberTheme.Muted);
            ActionButton(page,"Open songs","TAP TO START",480,709,640,128,owner.ShowSongs,true);
        }
        public void ShowSongs(IReadOnlyList<SongEntry> songs,int selected)
        {
            Begin("Song selection","SELECT MUSIC");Back("Back to title",owner.ShowTitle);
            if(songs.Count==0)
            {
                Artwork(page,182,219,408,408);
                Label(page,"Empty library","NO SONGS YET",746,293,754,106,62,true);
                Label(page,"Empty help","Your music collection will appear here.",750,425,744,110,36,false,CyberTheme.Muted);
                ActionButton(page,"Empty back","BACK",928,708,592,124,owner.ShowTitle,true);
                return;
            }
            var entry=songs[selected];
            // Covers are actual chart entries, never decorative fake tracks. The large masked
            // viewport supports finger drags; neighboring covers give a natural paging affordance.
            var viewport=Rect("Swipe song covers",page,72,180,720,498);
            viewport.gameObject.AddComponent<Image>().color=CyberTheme.Alpha(CyberTheme.Panel,.12f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Cover strip",viewport,0,0,720+(songs.Count-1)*588,498);
            carousel=viewport.gameObject.AddComponent<PagedSongCarousel>();carousel.viewport=viewport;carousel.content=content;
            carousel.PageCount=songs.Count;carousel.Stride=588;carousel.Selected=selected;carousel.horizontal=true;carousel.vertical=false;
            carousel.movementType=ScrollRect.MovementType.Clamped;carousel.inertia=false;carousel.scrollSensitivity=50;
            carousel.SelectPage(selected,false);carousel.SelectionChanged=owner.SelectSong;
            for(int i=0;i<songs.Count;i++)
            {
                int index=i;bool active=i==selected;
                var card=ActionButton(content,"Select song "+i,"",82+i*588,8,556,480,()=>owner.SelectSong(index),false,active?CyberTheme.PanelLight:CyberTheme.Panel);
                var p=(RectTransform)card.transform;
                Label(p,"Cover mark","G/R",26,20,450,52,30,true,CyberTheme.Cyan);
                // Adjacent albums reuse the style, but the title always identifies their real chart.
                Artwork(p,87,57,382,337);
                var cover=Label(p,"Cover title",songs[i].ShortTitle.ToUpperInvariant(),26,394,504,76,42,true,CyberTheme.Cyan,TextAnchor.MiddleCenter);Fit(cover,26,42);
            }
            var title=Label(page,"Selected title",entry.ShortTitle.ToUpperInvariant(),862,209,674,155,65,true);Fit(title,38,65);
            var author=Label(page,"Selected artist",string.IsNullOrWhiteSpace(entry.Chart.author)?"INDEPENDENT CHART":entry.Chart.author,866,375,665,73,34,false,CyberTheme.Muted);Fit(author,28,34);
            Box(page,"Detail accent",866,477,660,2,CyberTheme.Purple);
            Label(page,"Tempo",entry.Bpm+" BPM",866,508,310,67,40,true,CyberTheme.Cyan);
            Label(page,"Duration",FormatTime(entry.Duration),1234,508,292,67,40,true,CyberTheme.Text,TextAnchor.MiddleRight);
            Label(page,"Notes",entry.Chart.notes.Length+" NOTES",870,600,330,63,30,false,CyberTheme.Muted);
            ActionButton(page,"Watch autoplay","PREVIEW",1200,582,332,116,()=>owner.Play(true));
            Label(page,"Page count",(selected+1)+" / "+songs.Count,257,686,350,52,30,true,CyberTheme.Text,TextAnchor.MiddleCenter);
            if(songs.Count>1)
            {
                var prev=ActionButton(page,"Previous song","<",90,714,128,116,()=>owner.SelectSong(selected-1));
                var next=ActionButton(page,"Next song",">",646,714,128,116,()=>owner.SelectSong(selected+1));
                prev.interactable=selected>0;next.interactable=selected<songs.Count-1;
                Label(page,"Swipe hint","SWIPE TO SELECT",220,759,422,55,28,false,CyberTheme.Muted,TextAnchor.MiddleCenter);
            }
            else Label(page,"Single song hint","FIRST COLLECTION",128,757,610,55,28,false,CyberTheme.Muted,TextAnchor.MiddleCenter);
            ActionButton(page,"Play chart","PLAY  >",872,726,660,122,()=>owner.Play(false),true);
        }
        public void ShowResults(SongEntry song,PlayResult result)
        {
            Begin("Results page","TRACK COMPLETE");
            Label(page,"Result mode",result.Automatic?"AUTOPLAY PREVIEW":result.Practice?"PRACTICE":"RESULT",75,151,1130,60,30,true,CyberTheme.Cyan);
            var title=Label(page,"Result song",song.ShortTitle.ToUpperInvariant(),72,220,1038,103,58,true);Fit(title,32,58);
            Label(page,"Score caption","SCORE",77,347,900,51,30,false,CyberTheme.Muted);
            Label(page,"Final score",result.Score.ToString("D7"),68,387,1040,177,126,true);
            Label(page,"Grade",result.Grade,1136,187,387,205,142,true,CyberTheme.Lime,TextAnchor.MiddleCenter);
            Label(page,"Accuracy",result.Accuracy.ToString("F2")+"%",1127,411,410,87,57,true,CyberTheme.Cyan,TextAnchor.MiddleCenter);
            Label(page,"Accuracy caption","ACCURACY",1134,497,394,51,27,false,CyberTheme.Muted,TextAnchor.MiddleCenter);
            Metric("PERFECT",result.Perfect.ToString("D3"),72,578,346,CyberTheme.Cyan);
            Metric("GOOD",result.Good.ToString("D3"),441,578,346,CyberTheme.Lime);
            Metric("MISS",result.Miss.ToString("D3"),810,578,346,CyberTheme.Pink);
            Metric("MAX COMBO",result.MaxCombo.ToString(),1179,578,346,CyberTheme.Purple);
            ActionButton(page,"Back to songs","<  SONGS",72,737,408,116,owner.ShowSongs);
            Label(page,"Result disclaimer",result.Automatic?"PREVIEW · NOT A RECORD":result.Practice?"PRACTICE · NOT A RECORD":"RESULT NOT SAVED",501,771,568,59,27,false,CyberTheme.Muted,TextAnchor.MiddleCenter);
            ActionButton(page,"Retry","RETRY  >",1118,737,408,116,()=>owner.Play(result.Automatic),true);
        }
        public void ShowLoading(string title)
        {
            Begin("Loading page","GET READY");
            Artwork(page,130,206,492,448);
            Label(page,"Loading heading","FIND YOUR RHYTHM",738,271,807,111,59,true);
            var t=Label(page,"Loading track",title.ToUpperInvariant(),742,427,782,140,44,false,CyberTheme.Muted);Fit(t,32,44);
            ActionButton(page,"Cancel loading","CANCEL",80,734,400,120,owner.ShowSongs);
        }
        public void ShowError(string message)
        {
            var shade=Box(page,"Error blocker",0,0,1600,900,new Color(0,0,0,.75f));shade.raycastTarget=true;
            var p=Panel(page,"Load error",244,167,1112,597,CyberTheme.Panel,CyberTheme.Pink).rectTransform;
            Label(p,"Error heading","COULDN'T LOAD THIS TRACK",51,49,1010,99,53,true);
            var detail=Label(p,"Error detail",message,52,180,1008,176,34,false,CyberTheme.Muted);Fit(detail,26,34);
            ActionButton(p,"Dismiss error","BACK TO SONGS",241,417,630,120,owner.ShowSongs,true);
        }
        public void Hide()=>canvas.gameObject.SetActive(false);
        public bool Invoke(string name)
        {
            if(!buttons.TryGetValue(name,out var b)||!b.IsActive()||!b.interactable)return false;b.onClick.Invoke();return true;
        }
        public bool ValidateTouchTargets()
        {
            foreach(var b in buttons.Values){var r=(RectTransform)b.transform;if(r.rect.width<MobileUiLayout.TouchTarget||r.rect.height<MobileUiLayout.TouchTarget)return false;}
            return true;
        }
        public PagedSongCarousel Carousel=>carousel;
        public void UseCaptureCamera(Camera camera)
        {
            canvas.renderMode=camera==null?RenderMode.ScreenSpaceOverlay:RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
            if(camera!=null && pageFade!=null)pageFade.alpha=1;
        }
        void Metric(string name,string value,float x,float y,float width,Color color)
        {
            var p=Panel(page,name+" panel",x,y,width,130,CyberTheme.Panel,color).rectTransform;
            Label(p,name+" label",name,17,10,width-34,45,26,false,CyberTheme.Muted);
            Label(p,name+" value",value,15,46,width-30,79,53,true,color);
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
            if(caption.Length>0)Label(image.rectTransform,"Caption",caption,16,0,width-32,height,38,true,primary?CyberTheme.Background:CyberTheme.Text,TextAnchor.MiddleCenter);
            return button;
        }
        static void Artwork(RectTransform parent,float x,float y,float width,float height)
        {
            Rect("Signal core / original vector album art",parent,x,y,width,height).gameObject.AddComponent<MenuArtwork>().raycastTarget=false;
        }

        static string FormatTime(double time)=>((int)time/60).ToString("D2")+":"+((int)time%60).ToString("D2");
    }
}
