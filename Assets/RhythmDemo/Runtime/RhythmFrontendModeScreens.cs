using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    public sealed partial class RhythmFrontendView
    {
        bool musicPixelPage;
        float musicEntranceDelay;
        CanvasGroup modeGateGroup;
        RectTransform modeGateMark;

        CyberPanelGraphic MusicPanel(RectTransform parent,string name,float x,float y,float w,float h,Color? fill=null)
        {
            var panel=Panel(parent,name,x,y,w,h,fill??DesktopWindowSurface,CyberTheme.Purple);
            panel.rounded=true;panel.cornerRadius=12;return panel;
        }
        Button MusicButton(RectTransform parent,string name,string caption,float x,float y,float w,float h,Action callback,bool primary=false)
        {
            var button=ActionButton(parent,name,caption,x,y,w,h,callback,primary,
                primary?CyberTheme.Purple:DesktopWindowAlt,18);
            RoundIcon(button,12);SetButtonCaptionColor(button,primary?Color.white:DesktopWindowText);return button;
        }
        PixelMusicArtwork MusicArt(RectTransform parent,string name,float x,float y,float w,float h,int kind,int edition=0)
        {
            var art=Rect(name,parent,x,y,w,h).gameObject.AddComponent<PixelMusicArtwork>();
            art.kind=kind;art.edition=edition;art.raycastTarget=false;
            return art;
        }
        void MusicReveal(string name,float delay,float distance=14)
        {Reveal(page,name,musicEntranceDelay+delay,distance);}
        void MusicShell(string name,string title,string english,string description,string backName,Action back,bool animate=true)
        {
            Begin(name,english,false,.22f);
            musicEntranceDelay=modeGateRoot!=null?.30f:0;
            MusicArt(page,"Music wallpaper",0,0,1600,900,0);
            var shadow=MusicPanel(page,"Music window shadow",52,56,1504,744,new Color(.36f,.28f,.58f,.13f));shadow.outline=false;
            MusicPanel(page,"Music window",48,48,1504,744);
            var bar=MusicPanel(page,"Music title bar",48,48,1504,50,DesktopWindowHeader);
            SignalLogo(bar.rectTransform,18,11,.26f,true);
            Label(bar.rectTransform,"Application title","RHYTHM HACKER  /  演奏终端",68,0,800,50,18,false,Color.white,TextAnchor.MiddleLeft);
            Mono(bar.rectTransform,"Application status","LOCAL SESSION   /   READY",1120,0,354,50,12,Color.white,TextAnchor.MiddleRight);
            Mono(page,"Music eyebrow",english,84,128,900,24,12,DesktopWindowMuted);
            Label(page,"Music heading",title,80,158,1030,58,36,true,DesktopWindowText);
            Label(page,"Music description",description,84,220,1110,30,18,false,DesktopWindowMuted);
            MusicButton(page,backName,backName=="Back to main"?"←  返回桌面":"←  选择模式",1300,156,212,48,back);
            var dock=CreateSoftDesktopDock("Music");
            SignalLogo(dock.rectTransform,20,15,.28f,true);
            Mono(dock.rectTransform,"Music dock identity","RHYTHM HACKER   /   "+english,64,0,1050,62,12,DesktopWindowText,TextAnchor.MiddleLeft);
            Mono(dock.rectTransform,"Music dock hint","ESC  返回    /    点击选择",1130,0,410,62,12,DesktopWindowMuted,TextAnchor.MiddleRight);
            if(animate)
            {
                MusicReveal("Music eyebrow",.02f,6);MusicReveal("Music heading",.05f,10);
                MusicReveal("Music description",.09f,10);
            }
        }
        public void ShowSongModes(bool slideFromMain=false)
        {
            MusicShell("Mode select","选择你的演奏方式","01  /  SELECT MODE","沿着故事继续，或从音乐中自由出发。","Back to main",owner.ReturnToMainFromModes);
            for(int i=0;i<2;i++)
            {
                bool story=i==0;float x=80+i*736;
                string name=story?"Story mode card":"Free mode card";
                var card=MusicPanel(page,name,x,280,704,464,story?new Color(.94f,.90f,.98f):new Color(.89f,.93f,.98f));
                var root=card.rectTransform;
                Mono(root,"Route number",story?"01   /   STORY MODE":"02   /   FREE MODE",28,22,560,24,12,DesktopWindowMuted);
                MusicArt(root,"Route artwork",24,60,656,180,story?1:2).BindHover(root,owner);
                Label(root,"Route title",story?"剧情模式":"自由模式",28,262,640,46,30,true,DesktopWindowText);
                Label(root,"Route description",story?"追随 Turi，进入每一个尚未结束的故事。":"选择一首音乐，让节奏回到你的手中。",30,320,634,36,18,false,DesktopWindowMuted);
                MusicButton(root,story?"Open story mode":"Open free mode",story?"继续故事   →":"自由选曲   →",28,384,648,54,
                    ()=>owner.EnterAlbumSelection(story),true);
                MusicReveal(name,.12f+i*.08f,20);
            }
        }
        public void ShowStoryLevels(int currentLevel) => ShowStoryAlbumArchive(currentLevel);
        public void ShowSongs(IReadOnlyList<SongEntry> songs,int selected,bool slideFromMain=false)
            => ShowSongAlbumArchive(songs,selected);
        public void ShowModeLoading()
        {
            Begin("Main to mode loading","CONNECTING",false,.12f);
            MusicArt(page,"Loading wallpaper",0,0,1600,900,0);
            var window=MusicPanel(page,"Mode loading panel",370,246,860,408);
            var root=window.rectTransform;
            var header=MusicPanel(root,"Loading title bar",0,0,860,46,DesktopWindowHeader);
            Mono(header.rectTransform,"Title","RHYTHM HACKER   /   正在进入演奏终端",24,0,810,46,12,Color.white,TextAnchor.MiddleLeft);
            SignalLogo(root,52,90,.8f,true);
            Label(root,"Mode loading heading","让节奏，接管这一刻",158,94,640,52,30,true,DesktopWindowText);
            modeLoadingStatus=Mono(root,"Mode loading status","正在连接演奏终端",160,158,630,24,12,DesktopWindowMuted);
            var track=MusicPanel(root,"Pixel bar back",48,244,764,40,DesktopWindowAlt);track.outline=false;
            modeLoadingBlocks=new Image[28];
            for(int i=0;i<28;i++)modeLoadingBlocks[i]=Box(root,"Pixel load "+i,60+i*26,256,20,16,new Color(.81f,.76f,.91f));
            modeLoadingPercent=Mono(root,"Mode loading percent","000%",630,314,178,30,18,DesktopWindowText,TextAnchor.MiddleRight);
            Mono(root,"Loading footnote","MUSIC IS WAITING FOR YOU",52,314,520,30,12,DesktopWindowMuted);
            Reveal(page,"Mode loading panel",.04f,12);
        }
        public void SetModeLoadingProgress(float value,float time)
        {
            value=Mathf.Clamp01(value);
            if(modeLoadingBlocks!=null)
                for(int i=0;i<modeLoadingBlocks.Length;i++)if(modeLoadingBlocks[i]!=null)
                    modeLoadingBlocks[i].color=i<Mathf.RoundToInt(value*28)?CyberTheme.Purple:new Color(.81f,.76f,.91f);
            if(modeLoadingStatus!=null)modeLoadingStatus.text=value<.44f?"正在连接演奏终端":value<.88f?"正在整理故事与音乐":"已就绪，即将进入";
            if(modeLoadingPercent!=null)modeLoadingPercent.text=Mathf.RoundToInt(value*100).ToString("D3")+"%";
        }
        public void BeginModeGate()
        {
            EndModeGate();
            modeGateRoot=Rect("Mode route gate",artboard,0,0,1600,900);
            modeGateShade=Box(modeGateRoot,"Mode route shade",0,0,1600,900,new Color(.79f,.76f,.92f,0));
            modeGateShade.raycastTarget=true;
            modeGateBands=new CyberPanelGraphic[8];
            for(int i=0;i<8;i++)
            {
                var band=Panel(modeGateRoot,"Route gate "+i,0,i*114,1600,116,
                    Color.Lerp(new Color(.86f,.82f,.96f),new Color(.74f,.69f,.88f),i/7f),CyberTheme.Purple);
                band.outline=false;band.cut=0;band.rectTransform.localScale=new Vector3(0,1,1);
                modeGateBands[i]=band;
            }
            modeGateMark=Rect("Route mark",modeGateRoot,676,420,248,62);
            SignalLogo(modeGateMark,0,0,.55f,true);
            var text=Mono(modeGateMark,"Route caption","RHYTHM\nHACKER",88,6,160,50,18,DesktopWindowText);
            text.font=CyberTheme.Pixel;
            modeGateGroup=modeGateMark.gameObject.AddComponent<CanvasGroup>();modeGateGroup.alpha=0;
            modeGateRoot.SetAsLastSibling();
        }
        public void SetModeGateIn(float value){DrawMusicGate(value,false);}
        public void SetModeGateOut(float value){DrawMusicGate(value,true);}
        void DrawMusicGate(float value,bool opening)
        {
            if(modeGateBands==null)return;
            float t=Mathf.Clamp01(value);
            for(int i=0;i<modeGateBands.Length;i++)
            {
                float delay=i*.025f;
                float p=DesktopMotion.Smooth(Mathf.Clamp01((t-delay)/(1-delay)));
                var rect=modeGateBands[i].rectTransform;
                rect.localScale=new Vector3(opening?1-p:p,1,1);
                rect.anchoredPosition=new Vector2(opening?1600*p:0,-i*114);
            }
            modeGateShade.color=new Color(.79f,.76f,.92f,(opening?1-t:t)*.35f);
            if(modeGateGroup!=null)modeGateGroup.alpha=opening?1-DesktopMotion.Enter(t):DesktopMotion.Smooth(Mathf.Clamp01((t-.48f)/.52f));
        }
    }
}
