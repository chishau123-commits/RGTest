using System;
using UnityEngine;

namespace GeometryRhythm
{
    public sealed partial class RhythmFrontend
    {
        FrontendUiAudio uiAudio;
        int keyboardKeyVariant;
        readonly System.Random keyboardRandom=new System.Random();
        void SetupUiSounds()
        {
            var root=new GameObject("Frontend sound palette");root.transform.SetParent(transform,false);
            uiAudio=root.AddComponent<FrontendUiAudio>();uiAudio.Configure(smoke);
        }
        public void PlayUiCue(string cue){if(uiAudio!=null)uiAudio.Play(cue);}
        public void PlayNotificationSound()=>PlayUiCue("notification");
        public void PlayKeyboardKeySound()
        {
            keyboardKeyVariant=(keyboardKeyVariant+1+keyboardRandom.Next(5))%6;
            PlayUiCue("key-"+keyboardKeyVariant);
        }
        public void PlayUiHover(string name)
        {
            if(!UiTransitionLocked)PlayUiCue("hover");
        }
        public void PlayUiSound(string name)
        {
            if(name=="Open story mode"||name=="Open free mode")return;
            name=name??"";
            // These cues belong to successful actions, shared by mouse, Enter and automatic saves.
            if(name=="Open songs"||name=="Enter game"||name=="Save diary"||name=="Confirm return"||
                name=="Back to main"||name=="Play chart"||name=="Watch autoplay")return;
            if(name=="Story command action"&&storyCurrentNode!=null&&storyCurrentNode.kind==StoryRuntimeNodeKind.Stage)return;
            string stateCue=view.DesktopStateSound(name);
            if(stateCue!=null){PlayUiCue(stateCue);return;}
            string cue;
            if(name.StartsWith("Minimize",StringComparison.Ordinal))cue="minimize";
            else if(name.StartsWith("Restore",StringComparison.Ordinal))
            {
                bool minimized=name=="Restore main"?view.MainWindowMinimized:
                    name=="Restore diary"?view.DiaryWindowMinimized:name=="Restore friends"?view.FriendsWindowMinimized:
                    name=="Restore news"?view.NewsWindowMinimized:name=="Restore musicplayer"?view.MusicWindowMinimized:false;
                cue=minimized?"restore":"minimize";
            }
            else if(name.StartsWith("Like post",StringComparison.Ordinal))cue="like";
            else if(name.StartsWith("Post comment",StringComparison.Ordinal)||name.StartsWith("Send",StringComparison.Ordinal))cue="send";
            else if(name.StartsWith("Cancel",StringComparison.Ordinal))cue="cancel";
            else if(name.StartsWith("Close",StringComparison.Ordinal)||name.StartsWith("Back",StringComparison.Ordinal))cue="close";
            else if(name.StartsWith("Select",StringComparison.Ordinal))cue="select";
            else if(name.StartsWith("Accept",StringComparison.Ordinal)||name.StartsWith("Confirm",StringComparison.Ordinal)||name.StartsWith("Story choice",StringComparison.Ordinal))cue="confirm";
            else if(name.StartsWith("Return",StringComparison.Ordinal))cue="open";
            else if(name.StartsWith("Open",StringComparison.Ordinal))cue="open";
            else cue="click";
            PlayUiCue(cue);
        }
    }
    public sealed partial class RhythmFrontendView
    {
        public string DesktopStateSound(string name)
        {
            if(name=="Open start menu")return startMenuPopup==null?"open":"close";
            if(!name.StartsWith("Restore",StringComparison.Ordinal))return null;
            FrontendPage target=name=="Restore main"?FrontendPage.Main:name=="Restore diary"?FrontendPage.Diary:
                name=="Restore friends"?FrontendPage.Friends:name=="Restore news"?FrontendPage.News:FrontendPage.MusicPlayer;
            bool minimized=target==FrontendPage.Main?MainWindowMinimized:target==FrontendPage.Diary?DiaryWindowMinimized:
                target==FrontendPage.Friends?FriendsWindowMinimized:target==FrontendPage.News?NewsWindowMinimized:MusicWindowMinimized;
            return minimized?"restore":owner.Page==target&&FrontmostWindow==target?"minimize":"select";
        }
    }
}
