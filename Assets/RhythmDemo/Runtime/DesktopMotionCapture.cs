using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace GeometryRhythm
{
    public sealed partial class RhythmFrontend
    {
        readonly List<string> motionFrames=new List<string>();
        string motionDirectory;int motionFrame;bool motionPassed=true;
        float motionCaptureStarted;
        IEnumerator MotionSegment(string label,float duration)
        {
            Debug.Log("MOTION_CHECK "+label+" PASS="+motionPassed+" page="+Page);
            float start=Time.realtimeSinceStartup,next=start;
            while(Time.realtimeSinceStartup-start<duration)
            {
                yield return null;
                if(Time.realtimeSinceStartup<next)continue;
                float time=Time.realtimeSinceStartup;
                next=time+1f/24;
                motionPassed&=view.DesktopGeometryValid();
                string name="frame-"+(motionFrame++).ToString("D5");
                Capture(motionDirectory,name,preserveTransition:true);
                motionFrames.Add(name+".png,"+(time-motionCaptureStarted).ToString("F5",CultureInfo.InvariantCulture)+","+label);
            }
        }
        IEnumerator DesktopMotionCapture(string[] args)
        {
            int index=Array.IndexOf(args,"-desktopMotionCapture");
            motionDirectory=args[index+1];Directory.CreateDirectory(motionDirectory);
            float deadline=Time.realtimeSinceStartup+30;
            while(Page!=FrontendPage.Title&&Time.realtimeSinceStartup<deadline)yield return null;
            if(Page!=FrontendPage.Title){Debug.LogError("DESKTOP_MOTION_CAPTURE startup timeout");Application.Quit(1);yield break;}
            view.ShowTitle();motionCaptureStarted=Time.realtimeSinceStartup;
            yield return MotionSegment("login-entry",2.1f);
            view.PreviewControlState("Enter game",1);yield return MotionSegment("login-hover",.35f);
            view.PreviewControlState("Enter game",2);yield return MotionSegment("login-press",.2f);
            view.PreviewControlState("Enter game",0);
            view.SetTitlePassword("1111");motionPassed&=!view.SubmitTitleLogin();
            yield return MotionSegment("password-error",1.25f);
            view.SetTitlePassword("0000");motionPassed&=view.SubmitTitleLogin();
            yield return MotionSegment("login-to-desktop",5.2f);
            motionPassed&=Page==FrontendPage.Main;
            motionPassed&=view.Invoke("Open diary");yield return MotionSegment("diary-open",.85f);
            motionPassed&=view.Invoke("Minimize diary");yield return MotionSegment("diary-minimize-reverse",.12f);
            Vector3 before=view.DesktopPose(FrontendPage.Diary);
            motionPassed&=view.Invoke("Open diary");
            view.UpdateLayout();
            motionPassed&=Vector3.Distance(before,view.DesktopPose(FrontendPage.Diary))<.1f;
            yield return MotionSegment("diary-reverse-restore",.65f);
            motionPassed&=!view.DiaryWindowMinimized;
            motionPassed&=view.Invoke("Minimize diary");yield return MotionSegment("diary-minimize",.8f);
            motionPassed&=view.DiaryWindowMinimized;
            motionPassed&=view.Invoke("Restore diary");yield return MotionSegment("diary-restore",.65f);
            motionPassed&=view.Invoke("Open friends");yield return MotionSegment("friends-open",.85f);
            motionPassed&=view.Invoke("Minimize friends");yield return MotionSegment("friends-minimize",.75f);
            motionPassed&=view.FriendsWindowMinimized;
            motionPassed&=view.Invoke("Open news");yield return MotionSegment("news-open",.85f);
            motionPassed&=view.Invoke("Open news post 0");yield return MotionSegment("news-detail-open",.8f);
            motionPassed&=view.Invoke("Close detail");yield return MotionSegment("news-detail-close",.8f);
            motionPassed&=view.Invoke("Minimize news");yield return MotionSegment("news-minimize",.75f);
            motionPassed&=view.NewsWindowMinimized;
            motionPassed&=view.Invoke("Open music player");yield return MotionSegment("music-open",.85f);
            motionPassed&=view.Invoke("Close music player");yield return MotionSegment("music-close",.85f);
            motionPassed&=view.Invoke("Restore main");yield return MotionSegment("main-focus",.75f);
            motionPassed&=view.PreviewWindowDrag(FrontendPage.Main,new Vector2(-60,-20));
            Vector3 dragged=view.DesktopPose(FrontendPage.Main);
            yield return MotionSegment("main-drag",.4f);
            motionPassed&=view.Invoke("Restore main");yield return MotionSegment("main-minimize",.75f);
            motionPassed&=view.MainWindowMinimized;
            motionPassed&=view.Invoke("Restore main");yield return MotionSegment("main-restore",.75f);
            motionPassed&=Vector3.Distance(dragged,view.DesktopPose(FrontendPage.Main))<.1f;
            motionPassed&=view.Invoke("Open start menu");yield return MotionSegment("start-open",.65f);
            motionPassed&=view.Invoke("Close start menu");yield return MotionSegment("start-close",.6f);
            motionPassed&=view.Invoke("Open start menu");yield return MotionSegment("start-again",.55f);
            motionPassed&=view.Invoke("Open background menu");yield return MotionSegment("background-open",.7f);
            motionPassed&=view.Invoke("Select background 2");yield return MotionSegment("background-select",1f);
            motionPassed&=view.Invoke("Open start menu");yield return MotionSegment("start-settings",.55f);
            motionPassed&=view.Invoke("Open settings");yield return MotionSegment("controls-open",.7f);
            motionPassed&=view.Invoke("Close controls");yield return MotionSegment("controls-close",.65f);
            motionPassed&=view.Invoke("Open start menu");yield return MotionSegment("start-return",.55f);
            motionPassed&=view.Invoke("Return to title");yield return MotionSegment("return-dialog",.7f);
            motionPassed&=view.Invoke("Cancel return");yield return MotionSegment("return-cancel",.7f);
            File.WriteAllLines(Path.Combine(motionDirectory,"frames.csv"),motionFrames);
            File.WriteAllText(Path.Combine(motionDirectory,"result.txt"),"DESKTOP_MOTION_CAPTURE_PASS="+motionPassed+" frames="+motionFrame);
            Debug.Log("DESKTOP_MOTION_CAPTURE_PASS="+motionPassed);
            Application.Quit(motionPassed?0:1);
        }
    }
}
