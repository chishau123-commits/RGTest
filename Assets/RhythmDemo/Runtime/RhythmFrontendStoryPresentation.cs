using System;
using UnityEngine;
using UnityEngine.UI;

namespace GeometryRhythm
{
    public sealed partial class RhythmFrontendView
    {
        Button storyContinueButton;
        Text storyReadingHint;

        void DrawStoryChrome(StoryRuntimeProjectData story,RectTransform topLayer,bool entering)
        {
            var bar=MusicPanel(topLayer,"Story top bar",32,24,990,58,new Color(.94f,.91f,.99f,.95f));
            SignalLogo(bar.rectTransform,18,14,.26f,true);
            Mono(bar.rectTransform,"Story route","STORY 0-1",70,10,180,18,12,DesktopWindowMuted);
            var title=StoryText(bar.rectTransform,"Story title",story.title??"",70,28,892,26,18,false,DesktopWindowText);
            Fit(title,14,18);
            var skip=MusicButton(topLayer,"Skip story","跳过剧情   →",1340,30,228,46,owner.SkipStoryToStage);
            if(entering)Reveal(page,"Story top layer",.04f,8);
        }

        CyberPanelGraphic CreateStoryDialogue(bool choice,int choices,bool entering)
        {
            float height=choice?Mathf.Clamp(190+choices*74,264,486):238;
            var panel=MusicPanel(page,"Story dialogue panel",48,862-height,1504,height,
                new Color(.965f,.95f,.995f,.98f));
            var header=MusicPanel(panel.rectTransform,"Dialogue title bar",0,0,1504,46,DesktopWindowHeader);
            Mono(header.rectTransform,"Dialogue mode",choice?"CHOICE   /   作出选择":"DIALOGUE   /   故事记录",1070,0,406,46,12,Color.white,TextAnchor.MiddleRight);
            storyDialogueGroup=panel.gameObject.AddComponent<CanvasGroup>();
            storyDialogueRect=panel.rectTransform;
            storyDialogueTo=storyDialogueRect.anchoredPosition;
            storyDialogueFrom=storyDialogueTo+Vector2.down*14;
            storyDialogueRect.anchoredPosition=entering?storyDialogueFrom:storyDialogueTo;
            storyDialogueGroup.alpha=entering?0:1;
            return panel;
        }

        void DrawStoryDialogueShadow()
        {
            var shadow=MusicPanel(page,"Story dialogue shadow",52,-storyDialogueTo.y+6,
                1504,storyDialogueRect.sizeDelta.y,new Color(.27f,.19f,.43f,.14f));
            shadow.outline=false;
            // The shadow follows the same short entrance as its surface, without darkening the scene.
            if(storyDialogueGroup.alpha<1)DesktopMotion.GetOrAdd<DesktopEntranceMotion>(shadow.gameObject).Play(0,.52f,14);
        }

        void DrawStoryLine(RectTransform root,string speakerName,string line)
        {
            StoryText(root,"Story speaker",speakerName,30,0,1000,46,24,false,Color.white,TextAnchor.MiddleLeft);
            var body=StoryText(root,"Story dialogue","",30,66,1444,112,24,false,DesktopWindowText);
            body.lineSpacing=1.16f;
            // Fit using the complete sentence, then freeze the size so typing never resizes the text.
            body.text=line;Fit(body,18,24);Canvas.ForceUpdateCanvases();
            var settings=body.GetGenerationSettings(body.rectTransform.rect.size);
            body.cachedTextGenerator.Populate(line,settings);
            int fitted=body.cachedTextGenerator.fontSizeUsedForBestFit;
            body.resizeTextForBestFit=false;body.fontSize=Mathf.Clamp(fitted,18,24);
            BeginStoryTyping(body,line);
            Box(root,"Dialogue footer rule",30,181,1444,1,new Color(.84f,.79f,.93f));
            storyReadingHint=Mono(root,"Story reading hint","双击显示全文   /   Enter 推进",32,196,1060,24,12,DesktopWindowMuted);
            storyContinueButton=MusicButton(root,"Story continue","继续   →",1260,192,214,34,StoryContinue);
            UpdateStoryReadingState();
            Reveal(root,"Story dialogue",0,5);
        }

        void UpdateStoryReadingState()
        {
            if(storyContinueButton==null)return;
            storyContinueButton.interactable=!storyTyping;
            string hint=storyTyping?"双击显示全文   /   Enter 推进":"点击对话框   /   Enter 继续";
            if(storyReadingHint!=null&&storyReadingHint.text!=hint)storyReadingHint.text=hint;
        }

        void DrawStoryChoices(RectTransform root,StoryRuntimeNodeData node)
        {
            StoryText(root,"Story speaker","你的选择",30,0,1000,46,24,false,Color.white,TextAnchor.MiddleLeft);
            var question=StoryText(root,"Story question",node.text??"",30,62,1444,52,24,false,DesktopWindowText);Fit(question,18,24);
            float available=root.sizeDelta.y-186;
            var viewport=Rect("Story choices viewport",root,20,126,1464,available);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color=Color.clear;
            int count=node.choices==null?0:node.choices.Length;
            var content=Rect("Story choices",viewport,0,0,1464,Mathf.Max(available,count*74));
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
            for(int i=0;i<count;i++)
            {
                int index=i;var choice=node.choices[i];if(choice==null)continue;
                var button=MusicButton(content,"Story choice "+i,choice.text??"",12,i*74+6,1440,60,()=>owner.ChooseStoryOption(index));
                var rect=(RectTransform)button.transform;
                rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition+=new Vector2(720,-30);
                var caption=button.transform.Find("Caption").GetComponent<Text>();
                caption.rectTransform.anchoredPosition=new Vector2(76,0);caption.rectTransform.sizeDelta=new Vector2(1330,60);
                caption.alignment=TextAnchor.MiddleLeft;Fit(caption,16,22);
                Mono((RectTransform)button.transform,"Choice number",(i+1).ToString("D2"),20,0,44,60,18,DesktopWindowMuted,TextAnchor.MiddleCenter);
                DesktopMotion.GetOrAdd<DesktopEntranceMotion>(button.gameObject).Play(.06f+Mathf.Min(i,4)*.06f,.42f,8);
            }
            Mono(root,"Story choice hint","选择一项，继续这段故事",32,root.sizeDelta.y-38,1420,24,12,DesktopWindowMuted);
        }

        void DrawStoryCommand(StoryRuntimeProjectData story,StoryRuntimeNodeData node,string tag,string body,
            string actionLabel,Action action)
        {
            var shadow=MusicPanel(page,"Story command shadow",424,306,760,302,new Color(.27f,.19f,.43f,.14f));shadow.outline=false;
            var panel=MusicPanel(page,"Story command",420,300,760,302);
            var header=MusicPanel(panel.rectTransform,"Command title bar",0,0,760,46,DesktopWindowHeader);
            Mono(header.rectTransform,"Story command tag",tag,28,0,704,46,18,Color.white,TextAnchor.MiddleLeft);
            var text=StoryText(panel.rectTransform,"Story command body",body,32,78,696,114,24,false,DesktopWindowText);Fit(text,18,24);
            MusicButton(panel.rectTransform,"Story command action",actionLabel,32,224,696,52,action,true);
            Reveal(page,"Story command shadow",0,12);Reveal(page,"Story command",0,12);
        }
    }
}
