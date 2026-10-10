using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    public sealed partial class RhythmFrontendView
    {
        static readonly Color ArchiveBackground=new Color(.94f,.91f,.99f);
        static readonly Color ArchivePanel=new Color(.965f,.95f,.995f);
        static readonly Color ArchiveText=new Color(.20f,.12f,.42f);
        static readonly Color ArchiveMuted=new Color(.47f,.39f,.68f);
        PixelAlbumShelf albumShelf;
        IReadOnlyList<SongEntry> albumSongs;
        bool albumStory;
        int albumIndex;
        RectTransform albumInformation,albumListContent,albumDetails,albumBrowse;
        bool albumSelectionArmed;
        public bool AlbumDetailVisible => albumDetails!=null&&albumDetails.gameObject.activeSelf&&albumShelf.DetailReady;
        ScrollRect albumScroll;
        Text albumTitle,albumSubtitle,albumStatus,albumCounter,albumMetricA,albumMetricB,albumHint;
        Button albumConfirm,albumPreview,albumPrevious,albumNext;
        readonly List<Button> albumRows=new List<Button>();
        readonly string[] chapterTitles={"第一束光","信号遗失","最后的踪迹"};
        public PixelAlbumShelf AlbumShelf => albumShelf;
        public bool AlbumEntryReady => albumShelf!=null&&albumShelf.Depth>=.999f;
        CyberPanelGraphic ArchivePanelAt(RectTransform parent,string name,float x,float y,float w,float h,Color? fill=null)
        {
            var panel=MusicPanel(parent,name,x,y,w,h,fill??ArchivePanel);panel.accent=CyberTheme.Purple;return panel;
        }
        Button ArchiveButton(RectTransform parent,string name,string caption,float x,float y,float w,float h,Action action,bool primary=false)
        {
            var b=MusicButton(parent,name,caption,x,y,w,h,action,primary);
            b.targetGraphic.color=primary?CyberTheme.Purple:DesktopWindowAlt;
            SetButtonCaptionColor(b,primary?Color.white:ArchiveText);return b;
        }
        void BuildAlbumSelection(bool story,int count,int selected)
        {
            albumStory=story;albumIndex=selected;albumRows.Clear();albumSelectionArmed=false;
            Begin(story?"Story levels":"Song selection",story?"STORY ARCHIVE":"MUSIC ARCHIVE",false,.12f);
            Box(page,"Archive background",0,0,1600,900,ArchiveBackground);
            var scene=Rect("Album perspective viewport",page,0,142,1600,612);
            scene.gameObject.AddComponent<RectMask2D>();
            if(albumRouteShelf!=null)
            {
                albumShelf=albumRouteShelf;albumRouteShelf=null;
                albumShelf.rectTransform.SetParent(scene,false);
                albumShelf.rectTransform.anchoredPosition=Vector2.zero;albumShelf.rectTransform.sizeDelta=new Vector2(1600,612);
                DesktopMotion.GetOrAdd<CanvasGroup>(albumShelf.gameObject).alpha=1;
                albumShelf.ReleaseEntry();
            }
            else
            {
                albumShelf=Rect("Album shelf / perspective boxes",scene,0,0,1600,612).gameObject.AddComponent<PixelAlbumShelf>();
                albumShelf.Configure(count,selected,story);
            }
            albumShelf.CanInteract=()=>!owner.UiTransitionLocked;
            albumShelf.Selected=index=>
            {
                if(index!=albumIndex)owner.PlayUiCue("select");
                ChooseAlbum(index);
            };
            albumShelf.Clicked=PickAlbum;
            albumShelf.ExtractionSettled=OnAlbumExtractionSettled;
            SignalLogo(page,56,49,.40f,true);
            Label(page,"Archive app title","RHYTHM HACKER",116,44,630,40,28,true,ArchiveText);
            Mono(page,"Archive app status","MUSIC ARCHIVE   /   演奏终端",118,89,700,22,12,ArchiveMuted);
            Mono(page,"Archive eyebrow",story?"02  /  STORY ARCHIVE":"03  /  FREE PLAY",56,144,800,24,12,ArchiveMuted);
            ArchiveButton(page,"Back to modes","←  选择模式",1310,56,224,42,owner.ShowSongModes);
            Box(page,"Archive header rule",56,123,1480,1,new Color(.80f,.75f,.91f));

            albumBrowse=Rect("Album browse controls",page,0,0,1600,900);
            albumDetails=Rect("Taken album details",page,0,0,1600,900);
            Box(albumDetails,"Album detail paper",56,610,1480,192,ArchiveBackground);
            albumStatus=Mono(albumDetails,"Archive status","",82,612,1020,24,12,ArchiveMuted);
            albumInformation=Rect("Archive information",albumDetails,80,640,1030,160);
            albumTitle=Label(albumInformation,"Archive selected title","",0,0,1030,48,34,true,ArchiveText);Fit(albumTitle,20,34);
            albumSubtitle=Label(albumInformation,"Archive selected subtitle","",2,53,1030,36,18,false,ArchiveMuted);Fit(albumSubtitle,14,18);
            albumMetricA=Mono(albumInformation,"Archive metric A","",2,99,370,28,18,ArchiveText);
            albumMetricB=Mono(albumInformation,"Archive metric B","",390,99,640,28,18,ArchiveText);
            albumConfirm=ArchiveButton(albumDetails,story?"Open story chapter "+selected:"Play chart",story?"进入章节   ↗":"开始演奏   ↗",1150,646,362,48,ConfirmAlbum,true);
            albumPreview=story?null:ArchiveButton(albumDetails,"Watch autoplay","自动演奏 / 预览",1150,708,362,36,()=>{if(AlbumDetailVisible&&!owner.UiTransitionLocked)owner.Play(true);});
            if(story)Label(albumDetails,"Story archive note","未开放的档案可查看，暂不能进入。",1150,708,362,50,14,false,ArchiveMuted);
            ArchiveButton(albumDetails,"Put album back","↓  放回唱片",1290,158,234,42,()=>PutAlbumBack());
            albumDetails.gameObject.SetActive(false);
            albumCounter=Mono(albumBrowse,"Collection counter","",1080,750,190,40,22,ArchiveText,TextAnchor.MiddleCenter);
            albumPrevious=ArchiveButton(albumBrowse,"Select previous album","←",1300,750,64,40,()=>albumShelf.Step(-1));
            albumNext=ArchiveButton(albumBrowse,"Select next album","→",1450,750,64,40,()=>albumShelf.Step(1));

            // A compact indexed ruler replaces the large row of song cards.
            var viewport=Rect("Archive list viewport",albumBrowse,56,750,944,48);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;
            albumListContent=Rect("Archive list",viewport,0,0,Mathf.Max(944,count*104),48);
            albumScroll=viewport.gameObject.AddComponent<ScrollRect>();albumScroll.viewport=viewport;albumScroll.content=albumListContent;
            albumScroll.horizontal=true;albumScroll.vertical=false;albumScroll.movementType=ScrollRect.MovementType.Clamped;albumScroll.scrollSensitivity=40;
            for(int i=0;i<count;i++)
            {
                int index=i;
                var row=ArchiveButton(albumListContent,story?"Select chapter "+i:"Select song "+i,(i+1).ToString("D2"),i*104,4,88,40,()=>ChooseAlbum(index));
                albumRows.Add(row);
            }
            albumHint=Mono(albumBrowse,"Collection hint",count>0?"点击选中唱片  ·  再次点击取出  /  方向键切换  /  Enter 取出":"暂无曲目",56,710,944,24,12,ArchiveMuted);
            var dock=ArchivePanelAt(page,"Archive dock",18,826,1564,62,new Color(.86f,.82f,.95f));dock.cornerRadius=20;
            SignalLogo(dock.rectTransform,20,15,.28f,true);
            Mono(dock.rectTransform,"Archive dock identity",story?"RHYTHM HACKER   /   STORY COLLECTION":"RHYTHM HACKER   /   MUSIC COLLECTION",64,0,900,62,12,ArchiveMuted,TextAnchor.MiddleLeft);
            Mono(dock.rectTransform,"Archive dock help","点击选中 · 再次点击取出   /   ESC 放回或返回",922,0,612,62,12,ArchiveMuted,TextAnchor.MiddleRight);
            RefreshAlbumInformation(selected,false);
        }
        RectTransform albumRouteRoot,albumRouteCard,albumRouteArt;
        CanvasGroup albumRouteGroup,albumRouteShelfGroup,albumRouteArtGroup;
        PixelAlbumShelf albumRouteShelf;
        readonly List<CanvasGroup> albumRouteLabels=new List<CanvasGroup>();
        Vector2 albumRouteOrigin;
        public bool AlbumRouteVisible => albumRouteRoot!=null;
        public void BeginAlbumRoute(bool story)
        {
            EndAlbumRoute();
            albumRouteRoot=Rect("Mode to album transition",artboard,0,0,1600,900);
            var blocker=Box(albumRouteRoot,"Album route input shield",0,0,1600,900,Color.clear);blocker.raycastTarget=true;
            albumRouteCard=(RectTransform)page.Find(story?"Story mode card":"Free mode card");
            albumRouteCard.SetParent(albumRouteRoot,false);
            var entrance=albumRouteCard.GetComponent<DesktopEntranceMotion>();if(entrance!=null)entrance.enabled=false;
            albumRouteCard.localScale=Vector3.one;
            albumRouteOrigin=albumRouteCard.anchoredPosition;
            albumRouteGroup=DesktopMotion.GetOrAdd<CanvasGroup>(albumRouteCard.gameObject);albumRouteGroup.alpha=1;
            albumRouteArt=(RectTransform)albumRouteCard.Find("Route artwork");
            albumRouteLabels.Clear();
            foreach(Transform child in albumRouteCard)
                if(child!=albumRouteArt)albumRouteLabels.Add(DesktopMotion.GetOrAdd<CanvasGroup>(child.gameObject));
            albumRouteArtGroup=DesktopMotion.GetOrAdd<CanvasGroup>(albumRouteArt.gameObject);
            albumRouteShelf=Rect("Album shelf / perspective boxes",albumRouteRoot,albumRouteOrigin.x+24,-albumRouteOrigin.y+60,656,180).gameObject.AddComponent<PixelAlbumShelf>();
            albumRouteShelf.Configure(story?3:owner.Songs.Count,story?owner.StoryLevelIndex:owner.Selected,story);
            albumRouteShelf.HoldFlat();
            albumRouteShelfGroup=DesktopMotion.GetOrAdd<CanvasGroup>(albumRouteShelf.gameObject);albumRouteShelfGroup.alpha=0;
            albumRouteRoot.SetAsLastSibling();
        }
        public void SetAlbumRoute(float progress,bool reveal)
        {
            if(albumRouteRoot==null)return;
            albumRouteRoot.SetAsLastSibling();
            float t=DesktopMotion.Smooth(progress);
            if(reveal){albumRouteGroup.alpha=0;return;}
            SetPageAlpha(1-t);
            albumRouteCard.anchoredPosition=Vector2.Lerp(albumRouteOrigin,new Vector2(0,-142),t);
            albumRouteCard.sizeDelta=Vector2.Lerp(new Vector2(704,464),new Vector2(1600,612),t);
            albumRouteArt.anchoredPosition=Vector2.Lerp(new Vector2(24,-60),Vector2.zero,t);
            albumRouteArt.sizeDelta=Vector2.Lerp(new Vector2(656,180),new Vector2(1600,612),t);
            if(albumRouteShelf!=null)
            {
                albumRouteShelf.rectTransform.anchoredPosition=albumRouteCard.anchoredPosition+albumRouteArt.anchoredPosition;
                albumRouteShelf.rectTransform.sizeDelta=albumRouteArt.sizeDelta;
                float morph=DesktopMotion.Smooth(Mathf.InverseLerp(.12f,.85f,progress));
                albumRouteShelfGroup.alpha=morph;albumRouteArtGroup.alpha=1-morph;
            }
            foreach(var group in albumRouteLabels)group.alpha=1-DesktopMotion.Enter(Mathf.Clamp01(progress/.65f));
        }
        public void RevealAlbumInformation()
        {
            DesktopMotion.GetOrAdd<DesktopEntranceMotion>(albumBrowse.gameObject).Play(.20f,.5f,8);
        }
        void PickAlbum(int index)
        {
            if(!AlbumEntryReady||owner.UiTransitionLocked||albumShelf.IsExtracting)return;
            if(index==albumIndex&&albumSelectionArmed){TakeAlbumOut();return;}
            owner.PlayUiCue("select");ChooseAlbum(index);
        }
        void TakeAlbumOut()
        {
            if(!AlbumEntryReady||owner.UiTransitionLocked||albumShelf.Count==0||albumShelf.IsExtracting)return;
            albumSelectionArmed=true;albumBrowse.gameObject.SetActive(false);
            albumShelf.SetExtracted(true);owner.PlayUiCue("open");
        }
        void OnAlbumExtractionSettled(bool taken)
        {
            if(albumDetails==null||albumBrowse==null)return;
            albumDetails.gameObject.SetActive(taken);albumBrowse.gameObject.SetActive(!taken);
            if(taken)DesktopMotion.GetOrAdd<DesktopEntranceMotion>(albumDetails.gameObject).Play(0,.24f,8);
        }
        public bool PutAlbumBack()
        {
            if(albumShelf==null||!albumShelf.IsExtracting)return false;
            albumDetails.gameObject.SetActive(false);albumShelf.SetExtracted(false);
            owner.PlayUiCue("close");return true;
        }
        public void EndAlbumRoute()
        {
            if(albumRouteRoot!=null){albumRouteRoot.gameObject.SetActive(false);UnityEngine.Object.Destroy(albumRouteRoot.gameObject);}
            albumRouteRoot=null;albumRouteCard=null;albumRouteArt=null;albumRouteGroup=null;albumRouteShelf=null;albumRouteLabels.Clear();
        }
        void ChooseAlbum(int index)
        {
            if(!AlbumEntryReady||owner.UiTransitionLocked||albumShelf.IsExtracting)return;
            albumSelectionArmed=true;
            if(albumStory)RefreshAlbumInformation(index,true);else owner.SelectSong(index);
        }
        public void ConfirmAlbum()
        {
            if(!AlbumEntryReady||owner.UiTransitionLocked)return;
            if(!albumShelf.IsExtracting){TakeAlbumOut();return;}
            if(!AlbumDetailVisible)return;
            if(albumStory){if(albumIndex==0)owner.EnterStoryLevel(albumIndex);}
            else if(albumSongs!=null&&albumSongs.Count>0)owner.Play(false);
        }
        void RefreshAlbumInformation(int index,bool animate)
        {
            int count=albumStory?chapterTitles.Length:albumSongs.Count;
            albumIndex=Mathf.Clamp(index,0,Mathf.Max(0,count-1));albumShelf.Select(albumIndex);
            albumCounter.text=count==0?"00 / 00":(albumIndex+1).ToString("D2")+" / "+count.ToString("D2");
            albumPrevious.interactable=count>1&&albumIndex>0;albumNext.interactable=count>1&&albumIndex<count-1;
            if(albumStory)
            {
                bool available=albumIndex==0;
                albumStatus.text="CHAPTER 0-"+(albumIndex+1)+"   /   "+(available?"已开放":"尚未开放");
                albumTitle.text=chapterTitles[albumIndex];
                albumSubtitle.text=available?"进入录音室，开启第一段故事。":"这段信号尚未接通，故事仍在前方。";
                albumMetricA.text="剧情档案";albumMetricB.text=available?"TURI  /  0-1":"LOCKED";
                buttons.Remove(albumConfirm.name);albumConfirm.name="Open story chapter "+albumIndex;buttons[albumConfirm.name]=albumConfirm;
                albumConfirm.interactable=available;
                foreach(var t in albumConfirm.GetComponentsInChildren<Text>())t.text=available?"进入章节   →":"章节尚未开放";
            }
            else if(count>0)
            {
                var song=albumSongs[albumIndex];albumStatus.text="NOW SELECTED   /   "+(albumIndex+1).ToString("D2");
                albumTitle.text=song.ShortTitle;albumSubtitle.text=string.IsNullOrWhiteSpace(song.Chart.author)?"独立谱面":song.Chart.author;
                albumMetricA.text=song.Bpm+" BPM  /  "+FormatTime(song.Duration);albumMetricB.text=song.Chart.notes.Length+" 音符    /    最多 "+song.MaxPaths+" 轨道";
            }
            else
            {
                albumStatus.text="COLLECTION EMPTY";albumTitle.text="暂时没有可用的曲目";albumSubtitle.text="音乐收藏为空，请稍后再来。";
                albumMetricA.text="";albumMetricB.text="";albumConfirm.interactable=false;albumPreview.interactable=false;albumHint.text="暂无曲目";
            }
            for(int i=0;i<albumRows.Count;i++)
                albumRows[i].targetGraphic.color=i==albumIndex?new Color(.82f,.75f,.95f):DesktopWindowAlt;
            // Scroll only the list, preserving the camera and the existing shelf instance.
            float x=Mathf.Clamp(albumIndex*104-416,0,Mathf.Max(0,count*104-944));
            albumScroll.StopMovement();albumListContent.anchoredPosition=new Vector2(-x,0);
            if(animate&&AlbumDetailVisible)DesktopMotion.GetOrAdd<DesktopEntranceMotion>(albumInformation.gameObject).Play(0,.28f,8);
        }
        void ShowStoryAlbumArchive(int currentLevel)
        {
            storyLevelIndex=Mathf.Clamp(currentLevel,0,2);
            BuildAlbumSelection(true,chapterTitles.Length,storyLevelIndex);
        }
        void ShowSongAlbumArchive(IReadOnlyList<SongEntry> songs,int selected)
        {
            albumSongs=songs;
            if(page!=null&&page.name=="Song selection"&&albumShelf!=null&&albumShelf.Count==songs.Count)
            {RefreshAlbumInformation(selected,true);return;}
            BuildAlbumSelection(false,songs.Count,Mathf.Clamp(selected,0,Mathf.Max(0,songs.Count-1)));
        }
    }
    public sealed partial class RhythmFrontend
    {
        bool enteringAlbum;
        public void EnterAlbumSelection(bool story)
        {
            if(Page!=FrontendPage.SongModes||UiTransitionLocked)return;
            enteringAlbum=true;PlayUiCue("album-unfold");StartCoroutine(EnterAlbumSelectionSequence(story));
        }
        System.Collections.IEnumerator EnterAlbumSelectionSequence(bool story)
        {
            try
            {
                view.BeginAlbumRoute(story);
                float elapsed=0;
                while(elapsed<.46f)
                {
                    elapsed+=Time.unscaledDeltaTime;view.SetAlbumRoute(Mathf.Clamp01(elapsed/.46f),false);yield return null;
                }
                if(story)ShowStoryLevels();else ShowSongs();
                view.RevealAlbumInformation();
                elapsed=0;
                while(elapsed<.42f)
                {
                    elapsed+=Time.unscaledDeltaTime;view.SetAlbumRoute(Mathf.Clamp01(elapsed/.42f),true);yield return null;
                }
                view.EndAlbumRoute();
                while(!view.AlbumEntryReady)yield return null;
                PlayUiCue("album-settle");
            }
            finally{view.EndAlbumRoute();enteringAlbum=false;}
        }
    }

}
