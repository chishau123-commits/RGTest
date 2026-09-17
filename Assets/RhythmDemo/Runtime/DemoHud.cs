using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace GeometryRhythm
{
    /// <summary>Presentation-only HUD. Decorative graphics do not intercept gameplay touches.
    /// The playfield, all judgement callbacks and the note colors/shapes stay unchanged.</summary>
    public sealed class DemoHud
    {
        readonly RectTransform root;
        readonly Text combo,score,accuracy,status,clock,mode,feedback,pauseLabel,legend,soundLabel,overlayText;
        readonly Image fill;
        readonly GameObject overlay;
        double feedbackUntil;
        public DemoHud(Transform parent,Action pause,Action restart,Action automatic,Action mute,Action songs=null)
        {
            var go=new GameObject("Gameplay HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.transform.SetParent(parent,false);root=go.GetComponent<RectTransform>();
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            if(UnityEngine.Object.FindObjectOfType<EventSystem>()==null)
                new GameObject("Input events",typeof(EventSystem),typeof(StandaloneInputModule)).transform.SetParent(parent,false);
            // Edge-only framing: no fullscreen tint, bloom, blur or scanner over incoming notes.
            var header=Box("Instrument header",new Vector2(0,1),Vector2.one,CyberTheme.Alpha(CyberTheme.Background,.96f));
            header.rectTransform.pivot=new Vector2(.5f,1);header.rectTransform.sizeDelta=new Vector2(0,90);
            FixedBox("Cyan top rail",new Vector2(0,-2),new Vector2(690,3),new Vector2(0,1),CyberTheme.Cyan);
            FixedBox("Violet top rail",new Vector2(0,-2),new Vector2(690,3),Vector2.one,CyberTheme.Purple);
            pauseLabel=Button("Pause",new Vector2(54,-44),new Vector2(64,58),new Vector2(0,1),"II",pause,30);
            Label("System label",new Vector2(110,-24),new Vector2(400,24),new Vector2(0,1),"G/R  //  SIGNAL IN PROGRESS",14,TextAnchor.MiddleLeft,null,CyberTheme.Cyan,true);
            var track=Box("Song progress",new Vector2(.072f,1),new Vector2(.405f,1),CyberTheme.PanelLight);
            track.rectTransform.pivot=new Vector2(.5f,1);track.rectTransform.anchoredPosition=new Vector2(0,-57);track.rectTransform.sizeDelta=new Vector2(0,4);
            fill=Box("Played",Vector2.zero,Vector2.one,CyberTheme.Cyan,track.rectTransform);
            combo=Label("Combo",new Vector2(0,-35),new Vector2(270,78),new Vector2(.5f,1),"0",54,TextAnchor.MiddleCenter,null,CyberTheme.Lime);
            Label("Combo caption",new Vector2(0,-74),new Vector2(240,21),new Vector2(.5f,1),"C O M B O",12,TextAnchor.MiddleCenter,null,CyberTheme.Muted,true);
            Label("Score caption",new Vector2(-270,-19),new Vector2(255,24),Vector2.one,"SCORE",11,TextAnchor.MiddleRight,null,CyberTheme.Muted,true);
            score=Label("Score",new Vector2(-270,-53),new Vector2(255,55),Vector2.one,"0000000",41,TextAnchor.MiddleRight);
            Label("Accuracy caption",new Vector2(-31,-19),new Vector2(199,24),Vector2.one,"ACCURACY",11,TextAnchor.MiddleRight,null,CyberTheme.Muted,true);
            accuracy=Label("Accuracy",new Vector2(-31,-53),new Vector2(205,55),Vector2.one,"100.00%",41,TextAnchor.MiddleRight,null,CyberTheme.Cyan);
            feedback=Label("Judgement",new Vector2(0,-129),new Vector2(270,40),new Vector2(.5f,1),"",27,TextAnchor.MiddleCenter,null,CyberTheme.Background);
            var footer=Box("Status dock",Vector2.zero,new Vector2(1,0),CyberTheme.Alpha(CyberTheme.Background,.96f));
            footer.rectTransform.pivot=new Vector2(.5f,0);footer.rectTransform.sizeDelta=new Vector2(0,64);
            FixedBox("Dock accent",new Vector2(0,64),new Vector2(1020,2),Vector2.zero,CyberTheme.Purple);
            status=Label("Section",new Vector2(30,44),new Vector2(920,26),Vector2.zero,"GEOMETRY / DEMO",18,TextAnchor.MiddleLeft,null,CyberTheme.Cyan);
            clock=Label("Clock",new Vector2(30,20),new Vector2(1000,23),Vector2.zero,"",12,TextAnchor.MiddleLeft,null,CyberTheme.Muted,true);
            mode=Button("Mode",new Vector2(-350,32),new Vector2(146,40),new Vector2(1,0),"AUTO / VIEW",automatic,17);
            soundLabel=Button("Audio",new Vector2(-199,32),new Vector2(135,40),new Vector2(1,0),"SOUND ON",mute,17);
            Button("Restart",new Vector2(-66,32),new Vector2(116,40),new Vector2(1,0),"RESTART",restart,17);
            legend=Label("Legend",new Vector2(0,83),new Vector2(770,25),new Vector2(.5f,0),"BLUE: TAP   /   WHITE: DRAG   /   OUTER SLEEVE: ANYWHERE",13,TextAnchor.MiddleCenter,null,CyberTheme.Background,true);
            var dim=Box("Pause overlay",Vector2.zero,Vector2.one,new Color(.01f,.02f,.04f,.82f));overlay=dim.gameObject;
            // Only the paused modal scrim intercepts input. It is inactive during play.
            dim.raycastTarget=true;
            var pane=Panel("Pause console",new Vector2(0,0),new Vector2(760,334),new Vector2(.5f,.5f),dim.rectTransform,CyberTheme.Panel);
            Label("Pause index",new Vector2(0,131),new Vector2(690,29),new Vector2(.5f,.5f),"[ PLAYBACK CONTROL ]",14,TextAnchor.MiddleCenter,pane.rectTransform,CyberTheme.Purple,true);
            overlayText=Label("State",new Vector2(0,31),new Vector2(702,147),new Vector2(.5f,.5f),"PAUSED",41,TextAnchor.MiddleCenter,pane.rectTransform,CyberTheme.Text);
            Button("Resume",new Vector2(-168,-103),new Vector2(308,59),new Vector2(.5f,.5f),"RESUME SIGNAL  >",pause,24,pane.rectTransform,true);
            if(songs!=null)Button("Song selection",new Vector2(168,-103),new Vector2(308,59),new Vector2(.5f,.5f),"<  MUSIC ARCHIVE",songs,24,pane.rectTransform);
            overlay.SetActive(false);
        }
        Text Label(string name,Vector2 position,Vector2 size,Vector2 anchor,string value,int fontSize,TextAnchor alignment,RectTransform parent=null,Color? color=null,bool mono=false)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Text));var r=obj.GetComponent<RectTransform>();r.SetParent(parent??root,false);
            r.anchorMin=r.anchorMax=anchor;r.pivot=new Vector2(.5f,.5f);
            if(alignment==TextAnchor.MiddleLeft)r.pivot=new Vector2(0,.5f);
            if(alignment==TextAnchor.MiddleRight)r.pivot=new Vector2(1,.5f);
            r.anchoredPosition=position;r.sizeDelta=size;
            var t=obj.GetComponent<Text>();t.font=mono?CyberTheme.Mono:CyberTheme.Bold;t.fontSize=fontSize;t.color=color??CyberTheme.Text;
            t.alignment=alignment;t.text=value;t.raycastTarget=false;t.supportRichText=false;return t;
        }
        Image Box(string name,Vector2 min,Vector2 max,Color color,RectTransform parent=null)
        {
            var o=new GameObject(name,typeof(RectTransform),typeof(Image));var r=o.GetComponent<RectTransform>();r.SetParent(parent??root,false);
            r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
            var image=o.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        void FixedBox(string name,Vector2 pos,Vector2 size,Vector2 anchor,Color color)
        {
            var b=Box(name,anchor,anchor,color);b.rectTransform.anchoredPosition=pos;b.rectTransform.sizeDelta=size;
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
            var c=b.colors;c.highlightedColor=new Color(.72f,.84f,1);c.pressedColor=new Color(.5f,.6f,.8f);c.fadeDuration=.1f;b.colors=c;
            return Label("Caption",Vector2.zero,size,new Vector2(.5f,.5f),caption,fontSize,TextAnchor.MiddleCenter,p.rectTransform,primary?CyberTheme.Background:CyberTheme.Text);
        }
        public void Judge(NoteResult result,double time)
        {
            feedback.text=result.ToString().ToUpperInvariant();
            feedback.color=result==NoteResult.Miss?new Color(.64f,.13f,.30f):new Color(.08f,.26f,.34f);
            feedbackUntil=time+.55;
        }
        public void SetVisible(bool visible)=>root.gameObject.SetActive(visible);
        public void ResetFeedback(){feedback.text="";feedbackUntil=-1;}
        public void UseCaptureCamera(Camera camera)
        {
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
        }
        public void Update(JudgementEngine engine,double time,double duration,SectionData section,bool paused,bool automatic,bool muted,bool capture)
        {
            combo.text=engine.Combo.ToString();score.text=engine.Score.ToString("D7");accuracy.text=engine.Accuracy.ToString("F2")+"%";
            fill.rectTransform.anchorMax=new Vector2((float)(time/duration),1);
            status.text="GEOMETRY  /  "+section.name+"  /  "+section.placements.Length.ToString("D2")+" PATHS";
            clock.text=FormatTime(time)+" / "+FormatTime(duration)+"   "+(automatic?"AUTOPLAY PREVIEW":"MANUAL PLAY")+"   SPACE: PAUSE / A: MODE";
            mode.text=automatic?"AUTO / VIEW":"MANUAL / PLAY";soundLabel.text=muted?"SOUND OFF":"SOUND ON";pauseLabel.text=paused?">":"II";
            if(time>feedbackUntil)feedback.text="";
            bool ended=time>=duration-.01;overlay.SetActive(!capture&&(paused||ended));
            overlayText.text=ended?"COMPLETE\n"+engine.Perfects+" PERFECT  /  "+engine.Goods+" GOOD  /  "+engine.Misses+" MISS":"PAUSED\nPress SPACE to resume";
            legend.color=CyberTheme.Alpha(CyberTheme.Background,time<7?.85f:.48f);
        }
        static string FormatTime(double seconds)=>((int)seconds/60).ToString("D2")+":"+((int)seconds%60).ToString("D2");
    }
}
