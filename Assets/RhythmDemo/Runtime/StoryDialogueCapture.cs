using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeometryRhythm
{
    public sealed partial class RhythmFrontend
    {
        IEnumerator StoryDialogueCapture(string[] args)
        {
            motionDirectory=args[Array.IndexOf(args,"-storyDialogueCapture")+1];Directory.CreateDirectory(motionDirectory);
            float deadline=Time.realtimeSinceStartup+30;
            while(Page!=FrontendPage.Title&&Time.realtimeSinceStartup<deadline)yield return null;
            if(Page!=FrontendPage.Title){Debug.LogError("Story capture startup timeout");Application.Quit(1);yield break;}
            yield return new WaitForSecondsRealtime(2);
            view.SetTitlePassword("0000");motionPassed&=view.SubmitTitleLogin();
            yield return new WaitForSecondsRealtime(5);
            ShowStoryLevels();yield return new WaitForSecondsRealtime(1);
            motionCaptureStarted=Time.realtimeSinceStartup;
            EnterStoryLevel(0);yield return MotionSegment("chapter-entry",4.5f);
            Capture(motionDirectory,"dialogue-first");
            motionPassed&=view.Invoke("Story continue");yield return MotionSegment("speaker-change",3f);
            Capture(motionDirectory,"dialogue-second");
            int guard=0;
            while(storyCurrentNode.kind==StoryRuntimeNodeKind.Dialogue&&guard++<40)AdvanceStory();
            motionPassed&=storyCurrentNode.kind==StoryRuntimeNodeKind.Choice;
            yield return MotionSegment("choice-entry",1.2f);Capture(motionDirectory,"dialogue-choice");
            view.PreviewControlState("Story choice 0",1);yield return MotionSegment("choice-hover",.4f);
            Capture(motionDirectory,"dialogue-choice-hover");view.PreviewControlState("Story choice 0",0);
            motionPassed&=view.Invoke("Story choice 1");yield return MotionSegment("choice-response",2.8f);
            motionPassed&=storyVariables.Count>0;Capture(motionDirectory,"dialogue-response");
            guard=0;
            while(storyCurrentNode.kind!=StoryRuntimeNodeKind.Stage&&guard++<12)AdvanceStory();
            motionPassed&=storyCurrentNode.kind==StoryRuntimeNodeKind.Stage;
            yield return MotionSegment("stage-command",1f);Capture(motionDirectory,"dialogue-stage");
            var end=Array.Find(storyProject.nodes,n=>n!=null&&n.kind==StoryRuntimeNodeKind.End);
            view.ShowStory(storyProject,end,null,storyBackgroundId,storyVariables,storyCast,false);
            yield return MotionSegment("end-command",1f);Capture(motionDirectory,"dialogue-end");
            motionPassed&=view.Invoke("Story command action");yield return MotionSegment("return-chapters",1f);
            motionPassed&=Page==FrontendPage.StoryLevels;
            File.WriteAllLines(Path.Combine(motionDirectory,"frames.csv"),motionFrames);
            File.WriteAllText(Path.Combine(motionDirectory,"result.txt"),"STORY_DIALOGUE_CAPTURE_PASS="+motionPassed+" frames="+motionFrame);
            Application.Quit(motionPassed?0:1);
        }
    }
}
