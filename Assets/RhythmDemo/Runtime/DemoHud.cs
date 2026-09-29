using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace GeometryRhythm
{
    /// <summary>Touch-first HUD. Only pause is interactive during play; all secondary actions
    /// live in a large paused sheet. Note geometry, projection and judgement callbacks are unchanged.</summary>
    public sealed class DemoHud
    {
        readonly Canvas canvas;
        readonly RectTransform root,modalBoard;
        readonly Text combo,score,accuracy,status,clock,mode,feedback,pauseLabel,soundLabel,overlayText,playMode;
        readonly Image fill;
        readonly GameObject overlay;
        double feedbackUntil;
        Rect lastSafe;
        Vector2 lastCanvas;
        public DemoHud(Transform parent,Action pause,Action restart,Action automatic,Action mute,Action songs=null)
        {
            var go=new GameObject("Gameplay HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.transform.SetParent(parent,false);canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            root=Board("Mobile HUD safe area",go.transform);
            if(UnityEngine.Object.FindObjectOfType<EventSystem>()==null)
                new GameObject("Input events",typeof(EventSystem),typeof(StandaloneInputModule)).transform.SetParent(parent,false);
            var header=Box("Top dock",new Vector2(0,1),Vector2.one,CyberTheme.Alpha(CyberTheme.Background,.94f));
            header.rectTransform.pivot=new Vector2(.5f,1);header.rectTransform.sizeDelta=new Vector2(0,130);
            pauseLabel=Button("Pause",new Vector2(92,-78),new Vector2(128,120),new Vector2(0,1),"II",pause,48);
            var track=Box("Song progress",new Vector2(.13f,1),new Vector2(.38f,1),CyberTheme.PanelLight);
            track.rectTransform.pivot=new Vector2(.5f,1);track.rectTransform.anchoredPosition=new Vector2(0,-57);track.rectTransform.sizeDelta=new Vector2(0,6);
            fill=Box("Played",Vector2.zero,Vector2.one,CyberTheme.Cyan,track.rectTransform);
            clock=Label("Time",new Vector2(213,-94),new Vector2(430,47),new Vector2(0,1),"00:00 / 01:04",28,TextAnchor.MiddleLeft,null,CyberTheme.Muted);
            combo=Label("Combo",new Vector2(0,-46),new Vector2(320,106),new Vector2(.5f,1),"0",73,TextAnchor.MiddleCenter,null,CyberTheme.Lime);
            Label("Combo caption",new Vector2(0,-105),new Vector2(275,45),new Vector2(.5f,1),"COMBO",26,TextAnchor.MiddleCenter,null,CyberTheme.Muted);
            score=Label("Score",new Vector2(-34,-43),new Vector2(426,86),Vector2.one,"0000000",57,TextAnchor.MiddleRight);
            accuracy=Label("Accuracy",new Vector2(-34,-103),new Vector2(410,49),Vector2.one,"100.00%",32,TextAnchor.MiddleRight,null,CyberTheme.Cyan);
            feedback=Label("Judgement",new Vector2(0,-173),new Vector2(350,65),new Vector2(.5f,1),"",41,TextAnchor.MiddleCenter,null,CyberTheme.Background);
            var footer=Box("Bottom status",Vector2.zero,new Vector2(1,0),CyberTheme.Alpha(CyberTheme.Background,.87f));
            footer.rectTransform.pivot=new Vector2(.5f,0);footer.rectTransform.sizeDelta=new Vector2(0,56);
            status=Label("Section",new Vector2(30,28),new Vector2(1025,47),Vector2.zero,"",28,TextAnchor.MiddleLeft,null,CyberTheme.Cyan);
            playMode=Label("Play mode",new Vector2(-30,28),new Vector2(460,47),new Vector2(1,0),"",28,TextAnchor.MiddleRight,null,CyberTheme.Muted);
            // The dimmer covers the whole display; only its controls are fitted to the safe area.
            var dim=Box("Pause overlay",Vector2.zero,Vector2.one,new Color(.01f,.02f,.04f,.85f),(RectTransform)canvas.transform);
            overlay=dim.gameObject;dim.raycastTarget=true;modalBoard=Board("Pause safe area",dim.transform);
            var pane=Panel("Pause sheet",Vector2.zero,new Vector2(1070,690),new Vector2(.5f,.5f),modalBoard,CyberTheme.Panel);
            overlayText=Label("State",new Vector2(0,252),new Vector2(976,117),new Vector2(.5f,.5f),"PAUSED",72,TextAnchor.MiddleCenter,pane.rectTransform);
            Label("Note legend",new Vector2(0,166),new Vector2(970,61),new Vector2(.5f,.5f),"BLUE: TAP  /  WHITE: DRAG  /  SLEEVE: ANYWHERE",27,TextAnchor.MiddleCenter,pane.rectTransform,CyberTheme.Muted);
            mode=Button("Mode",new Vector2(-256,61),new Vector2(462,112),new Vector2(.5f,.5f),"MODE: MANUAL",automatic,35,pane.rectTransform);
            soundLabel=Button("Audio",new Vector2(256,61),new Vector2(462,112),new Vector2(.5f,.5f),"SOUND ON",mute,35,pane.rectTransform);
            Button("Restart",new Vector2(-256,-80),new Vector2(462,112),new Vector2(.5f,.5f),"RETRY",restart,38,pane.rectTransform);
            if(songs!=null)Button("Song selection",new Vector2(256,-80),new Vector2(462,112),new Vector2(.5f,.5f),"SONGS",songs,38,pane.rectTransform);
            Button("Resume",new Vector2(0,-241),new Vector2(974,122),new Vector2(.5f,.5f),"RESUME  >",pause,43,pane.rectTransform,true);
            overlay.SetActive(false);RefreshLayout();
        }
        static RectTransform Board(string name,Transform parent)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(1600,900);return r;
        }
        void RefreshLayout()
        {
            var size=((RectTransform)canvas.transform).rect.size;var safe=MobileUiLayout.SafeArea;
            if(size==lastCanvas && safe==lastSafe)return;
            lastCanvas=size;lastSafe=safe;MobileUiLayout.Apply(root,canvas);MobileUiLayout.Apply(modalBoard,canvas);
        }
        Text Label(string name,Vector2 position,Vector2 size,Vector2 anchor,string value,int fontSize,TextAnchor alignment,RectTransform parent=null,Color? color=null)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Text));var r=obj.GetComponent<RectTransform>();r.SetParent(parent??root,false);
            r.anchorMin=r.anchorMax=anchor;r.pivot=new Vector2(.5f,.5f);
            if(alignment==TextAnchor.MiddleLeft)r.pivot=new Vector2(0,.5f);
            if(alignment==TextAnchor.MiddleRight)r.pivot=new Vector2(1,.5f);
            r.anchoredPosition=position;r.sizeDelta=size;
            var t=obj.GetComponent<Text>();t.font=CyberTheme.Bold;t.fontSize=fontSize;t.color=color??CyberTheme.Text;
            t.alignment=alignment;t.text=value;t.raycastTarget=false;t.supportRichText=false;return t;
        }
        Image Box(string name,Vector2 min,Vector2 max,Color color,RectTransform parent=null)
        {
            var r=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();r.SetParent(parent??root,false);
            r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
            var image=r.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        CyberPanelGraphic Panel(string name,Vector2 pos,Vector2 size,Vector2 anchor,RectTransform parent,Color color)
        {
            var r=new GameObject(name,typeof(RectTransform),typeof(CyberPanelGraphic)).GetComponent<RectTransform>();r.SetParent(parent??root,false);
            r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=pos;r.sizeDelta=size;
            var p=r.GetComponent<CyberPanelGraphic>();p.color=color;p.accent=CyberTheme.Cyan;p.raycastTarget=false;return p;
        }
        Text Button(string name,Vector2 position,Vector2 size,Vector2 anchor,string caption,Action action,int fontSize,RectTransform parent=null,bool primary=false)
        {
            var p=Panel(name,position,size,anchor,parent,primary?CyberTheme.Cyan:CyberTheme.PanelLight);p.raycastTarget=true;
            var b=p.gameObject.AddComponent<Button>();b.targetGraphic=p;b.navigation=new Navigation{mode=Navigation.Mode.None};b.onClick.AddListener(()=>action());
            var c=b.colors;c.highlightedColor=Color.white;c.pressedColor=new Color(.53f,.70f,.84f);c.fadeDuration=.08f;b.colors=c;
            return Label("Caption",Vector2.zero,size,new Vector2(.5f,.5f),caption,fontSize,TextAnchor.MiddleCenter,p.rectTransform,primary?CyberTheme.Background:CyberTheme.Text);
        }
        public void Judge(NoteResult result,double time)
        {
            feedback.text=result.ToString().ToUpperInvariant();feedback.color=result==NoteResult.Miss?new Color(.64f,.13f,.30f):new Color(.08f,.26f,.34f);feedbackUntil=time+.55;
        }
        public void SetVisible(bool visible)=>canvas.gameObject.SetActive(visible);
        public void ResetFeedback(){feedback.text="";feedbackUntil=-1;}
        public void UseCaptureCamera(Camera camera)
        {
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
        }
        public void Update(JudgementEngine engine,double time,double duration,SectionData section,bool paused,bool automatic,bool muted,bool capture)
        {
            RefreshLayout();
            combo.text=engine.Combo.ToString();score.text=engine.Score.ToString("D7");accuracy.text=engine.Accuracy.ToString("F2")+"%";
            fill.rectTransform.anchorMax=new Vector2((float)(time/duration),1);
            status.text=section.name.ToUpperInvariant()+"  /  "+section.placements.Length+" PATHS";
            playMode.text=automatic?"AUTOPLAY":"";
            clock.text=FormatTime(time)+" / "+FormatTime(duration);
            mode.text=automatic?"MODE: AUTO":"MODE: MANUAL";soundLabel.text=muted?"SOUND OFF":"SOUND ON";pauseLabel.text=paused?">":"II";
            if(time>feedbackUntil)feedback.text="";
            bool ended=time>=duration-.01;overlay.SetActive(!capture&&(paused||ended));
            overlayText.text=ended?"COMPLETE":"PAUSED";
        }
        static string FormatTime(double seconds)=>((int)seconds/60).ToString("D2")+":"+((int)seconds%60).ToString("D2");
    }
}
