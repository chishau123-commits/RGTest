using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GeometryRhythm.Editor
{
    // Run in the isolated validation project. A Player build cannot reproduce Editor fake-null wrappers.
    [InitializeOnLoad]
    public static class DesktopMotionEditorValidation
    {
        const string Active="RhythmDesktopEditorValidation";
        static RhythmFrontend frontend;
        static RhythmFrontendView view;
        static readonly List<Action> steps=new List<Action>();
        static int step,errors,clicks;
        static double next,deadline;
        static string resultPath;
        static bool finishing;

        static DesktopMotionEditorValidation()
        {
            if(!SessionState.GetBool(Active,false))return;
            Application.logMessageReceived+=Log;
            EditorApplication.playModeStateChanged+=State;
        }
        public static void Run()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-editorSmokeResult");
            if(index<0)throw new ArgumentException("-editorSmokeResult is required");
            SessionState.SetString(Active+"Result",args[index+1]);
            SessionState.SetBool(Active,true);
            EditorSceneManager.OpenScene(DemoBuildTools.ScenePath);
            EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string stack,LogType type)
        {
            if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)errors++;
        }
        static void State(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode)return;
            resultPath=SessionState.GetString(Active+"Result","");
            deadline=EditorApplication.timeSinceStartup+240;
            EditorApplication.update+=Tick;
        }
        static void Check(bool value,string message)
        {if(!value)throw new Exception(message);}
        static void ClickAlbum(int index)
        {
            Canvas.ForceUpdateCanvases();var shelf=view.AlbumShelf;
            var pointer=new PointerEventData(EventSystem.current){position=shelf.CardScreenPoint(index),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&hits[0].gameObject==shelf.gameObject,"Album geometry picking blocked");
            ExecuteEvents.Execute(shelf.gameObject,pointer,ExecuteEvents.pointerClickHandler);clicks++;
        }
        static void Components()
        {
            var fresh=new GameObject("Fresh entrance regression",typeof(RectTransform));
            try
            {
                var entrance=DesktopMotion.GetOrAdd<DesktopEntranceMotion>(fresh);
                entrance.Play(0,.2f);
                Check(fresh.TryGetComponent<CanvasGroup>(out var group)&&group!=null,"Missing entrance CanvasGroup");
                Check(DesktopMotion.GetOrAdd<DesktopEntranceMotion>(fresh)==entrance,"Duplicate entrance component");
                Check(fresh.GetComponents<CanvasGroup>().Length==1,"Duplicate CanvasGroup");
                entrance.Play(0,.2f); // Replaying on an existing component must also work.
            }
            finally{UnityEngine.Object.DestroyImmediate(fresh);}
            var plain=new GameObject("Destroyed component regression",typeof(RectTransform));
            try
            {
                var first=DesktopMotion.GetOrAdd<CanvasGroup>(plain);
                UnityEngine.Object.DestroyImmediate(first);
                var second=DesktopMotion.GetOrAdd<CanvasGroup>(plain);
                Check(second!=null&&second!=first,"Destroyed component must be recreated");
            }
            finally{UnityEngine.Object.DestroyImmediate(plain);}
        }
        static void Click(string name)
        {
            Canvas.ForceUpdateCanvases();
            Button button=null;
            foreach(var candidate in UnityEngine.Object.FindObjectsOfType<Button>())
                if(candidate.name==name&&candidate.gameObject.activeInHierarchy){button=candidate;break;}
            Check(button!=null&&button.IsInteractable(),"Button unavailable: "+name);
            var rect=(RectTransform)button.transform;
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0,"No UI raycast hit: "+name);
            var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Check(handler==button.gameObject,"UI blocked: "+name+" by "+hits[0].gameObject.name);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            clicks++;Debug.Log("EDITOR_UI_CLICK "+name);
        }
        static void Setup()
        {
            steps.Add(Components);
            steps.Add(()=>{view.SetTitlePassword("1111");Click("Enter game");Check(frontend.Page==FrontendPage.Title,"Invalid login accepted");});
            steps.Add(()=>{view.SetTitlePassword("0000");Click("Enter game");next+=5;});
            steps.Add(()=>{
                Check(frontend.Page==FrontendPage.Main,"Login did not enter MainUI");
                var bank=UnityEngine.Object.FindObjectOfType<FrontendUiAudio>();
                Check(bank!=null&&bank.LoadedCount==FrontendUiAudio.Cues.Length,"UI sound palette incomplete");
                var pointer=new PointerEventData(EventSystem.current);
                var diary=GameObject.Find("Open diary");
                bank.PlayCounts.TryGetValue("hover",out int before);
                for(int i=0;i<20;i++)ExecuteEvents.Execute(diary,pointer,ExecuteEvents.pointerEnterHandler);
                bank.PlayCounts.TryGetValue("hover",out int after);
                Check(after-before==1,"Hover audio is missing or stacking");
                ExecuteEvents.Execute(diary,pointer,ExecuteEvents.pointerExitHandler);
                Click("Open diary");
            });
            steps.Add(()=>{Check(frontend.Page==FrontendPage.Diary,"Diary not opened");Click("Open diary entry 1");});
            steps.Add(()=>Click("Minimize diary"));
            steps.Add(()=>{Check(view.DiaryWindowMinimized,"Diary did not minimize");Click("Restore diary");});
            steps.Add(()=>{Check(!view.DiaryWindowMinimized,"Diary did not restore");Click("Open friends");});
            steps.Add(()=>{Check(frontend.Page==FrontendPage.Friends,"Friends not opened");Click("Minimize friends");});
            steps.Add(()=>{Check(view.FriendsWindowMinimized,"Friends did not minimize");Click("Restore friends");});
            steps.Add(()=>{Check(!view.FriendsWindowMinimized,"Friends did not restore");Click("Open news");});
            steps.Add(()=>{Check(frontend.Page==FrontendPage.News,"News not opened");Click("Open news post 0");});
            steps.Add(()=>Click("Close detail"));
            steps.Add(()=>Click("Minimize news"));
            steps.Add(()=>{Check(view.NewsWindowMinimized,"News did not minimize");Click("Open music player");});
            steps.Add(()=>{Check(frontend.Page==FrontendPage.MusicPlayer,"Music not opened");Check(frontend.MusicTrackCount==5&&frontend.MusicTrackAvailable(4),"Gymnopedie track missing");Click("Select music track "+frontend.SelectedMusicTrack);});
            steps.Add(()=>Click("Minimize music player"));
            steps.Add(()=>{Check(view.MusicWindowMinimized,"Music did not minimize");Click("Restore musicplayer");});
            steps.Add(()=>Click("Close music player"));
            steps.Add(()=>Click("Restore main"));
            steps.Add(()=>Click("Restore main"));
            steps.Add(()=>{Check(view.MainWindowMinimized,"Main did not minimize");Click("Restore main");});
            steps.Add(()=>{Check(!view.MainWindowMinimized,"Main did not restore");Click("Open start menu");});
            steps.Add(()=>Click("Open background menu"));
            steps.Add(()=>Click("Select background "+(view.HomeBackgroundIndex+1)));
            steps.Add(()=>Click("Open start menu"));
            steps.Add(()=>Click("Open settings"));
            steps.Add(()=>Click("Close controls"));
            steps.Add(()=>Click("Open start menu"));
            steps.Add(()=>Click("Return to title"));
            steps.Add(()=>Click("Cancel return"));
            steps.Add(()=>{Click("Open songs");next+=5;});
            steps.Add(()=>{Check(frontend.Page==FrontendPage.SongModes,"Mode selection did not open");Click("Open story mode");Check(frontend.UiTransitionLocked&&view.AlbumRouteVisible,"Album route did not start");next+=1.7;});
            steps.Add(()=>{Check(view.AlbumEntryReady&&!view.AlbumRouteVisible&&!frontend.UiTransitionLocked,"Album route did not release its input gate");Click("Select chapter 1");});
            steps.Add(()=>{
                Check(view.AlbumShelf.Index==1&&!view.AlbumDetailVisible,"Selection exposed details before extraction");
                ClickAlbum(1);
            });
            steps.Add(()=>{
                Check(view.AlbumDetailVisible,"Second click did not take the record out");
                Check(!GameObject.Find("Open story chapter 1").GetComponent<Button>().IsInteractable(),"Locked chapter can be entered");
                view.ConfirmAlbum();Check(frontend.Page==FrontendPage.StoryLevels,"Locked chapter entered with Enter");
                Click("Put album back");
            });
            steps.Add(()=>{Check(!view.AlbumDetailVisible&&!view.AlbumShelf.IsExtracting,"Record did not return to shelf");Click("Select chapter 2");});
            steps.Add(()=>{ClickAlbum(0);Check(view.AlbumShelf.Index==0&&!view.AlbumDetailVisible,"First click did not just select chapter");});
            steps.Add(()=>ClickAlbum(0));
            steps.Add(()=>{
                Check(frontend.Page==FrontendPage.StoryLevels,"Story selection did not open");
                foreach(var b in UnityEngine.Object.FindObjectsOfType<Button>())
                    if(b.name=="Open story chapter 1"||b.name=="Open story chapter 2")Check(!b.IsInteractable(),"Locked chapter enabled");
                Click("Open story chapter 0");next+=3;
            });
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-storyDialogueSmoke")>=0)AddStoryDialogueSteps();
            else steps.Add(()=>{Check(frontend.Page==FrontendPage.StoryPlayer,"Chapter did not start");frontend.ReturnToStoryLevels();});
            steps.Add(()=>Click("Back to modes"));
            steps.Add(()=>{Click("Open free mode");Check(frontend.UiTransitionLocked&&view.AlbumRouteVisible,"Free album route did not start");next+=1.7;});
            steps.Add(()=>{
                Check(frontend.Page==FrontendPage.Songs,"Free play did not open");
                Check(GameObject.Find("Archive detail")==null,"Obsolete sidebar still visible");
                Check(!view.AlbumDetailVisible,"Free library showed details on entry");
                ClickAlbum(frontend.Selected);
                Check(!view.AlbumShelf.IsExtracting,"First click extracted default selection");
            });
            steps.Add(()=>ClickAlbum(frontend.Selected));
            steps.Add(()=>{
                Check(view.AlbumDetailVisible,"Free detail not revealed after second click");
                var song=frontend.Songs[frontend.Selected];
                Check(GameObject.Find("Archive selected title").GetComponent<Text>().text==song.ShortTitle,"Selected title missing");
                foreach(var group in GameObject.Find("Archive selected title").GetComponentsInParent<CanvasGroup>())Check(group.alpha>.99f,"Extracted metadata stayed transparent");
                string metrics=GameObject.Find("Archive metric B").GetComponent<Text>().text;
                Check(metrics.Contains(song.Chart.notes.Length+" 音符")&&metrics.Contains("最多 "+song.MaxPaths+" 轨道"),"Note or lane information missing");
            });
            steps.Add(()=>{Check(frontend.Selected==Mathf.Min(1,frontend.Songs.Count-1),"Song selection failed");Click("Watch autoplay");next+=2;});
            steps.Add(()=>{
                Check(frontend.Page==FrontendPage.Playing,"Autoplay did not start");
                var session=(RhythmDemoController)typeof(RhythmFrontend).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(frontend);
                Check(session.autoPlay,"Autoplay flag lost");session.ReturnToSongs();
                Check(frontend.Page==FrontendPage.Songs,"Free play retained the story return destination");
                next+=1;
            });
            steps.Add(()=>view.ConfirmAlbum());
            steps.Add(()=>Click("Play chart"));
            steps.Add(()=>{
                Check(frontend.Page==FrontendPage.Playing,"Manual play did not start");
                var session=(RhythmDemoController)typeof(RhythmFrontend).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(frontend);
                Check(!session.autoPlay,"Manual play flag lost");session.ReturnToSongs();
            });
            steps.Add(()=>Click("Back to modes"));
            steps.Add(()=>{Click("Back to main");next+=2;});
            steps.Add(()=>{Check(frontend.Page==FrontendPage.Main,"Main did not rebuild");Click("Open start menu");});
            steps.Add(()=>Click("Return to title"));
            steps.Add(()=>Click("Confirm return"));
            steps.Add(()=>{Check(frontend.Page==FrontendPage.Title,"Title did not rebuild");next+=1;});
            steps.Add(()=>{view.SetTitlePassword("0000");Click("Enter game");next+=5;});
            steps.Add(()=>{Check(frontend.Page==FrontendPage.Main,"Repeated login failed");Click("Open diary");});
            steps.Add(()=>{
                Check(view.DesktopGeometryValid(),"Window geometry invalid");Check(errors==0,"Editor logged "+errors+" errors");
                var bank=UnityEngine.Object.FindObjectOfType<FrontendUiAudio>();
                Check(bank.PlayCounts["login"]==2,"Login sound duplicated or missing");
                Check(bank.PlayCounts["performance"]==1&&bank.PlayCounts["ready"]==1,"Performance transition cues duplicated or missing");
                Check(bank.PlayCounts["logout"]==1,"Logout cue missing");
                Check(bank.PlayCounts["album-unfold"]==2&&bank.PlayCounts["album-settle"]==2,"Album entry cues duplicated or missing");
            });
        }
        static StoryRuntimeNodeData StoryNode => (StoryRuntimeNodeData)typeof(RhythmFrontend)
            .GetField("storyCurrentNode",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(frontend);
        static void AddStoryDialogueSteps()
        {
            steps.Add(()=>{Check(frontend.Page==FrontendPage.StoryPlayer,"Chapter did not start");next+=3;});
            for(int i=0;i<10;i++)
            {
                int index=i;
                steps.Add(()=>{
                    Check(StoryNode.kind==StoryRuntimeNodeKind.Dialogue,"Expected dialogue "+index);
                    var panel=GameObject.Find("Story dialogue panel").GetComponent<CyberPanelGraphic>();
                    Check(panel.canvasRenderer.GetColor().a>.99f,"Dialogue surface retained disabled transparency");
                    string previous=StoryNode.id;
                    Click(index%2==0?"Story continue":"Story dialogue panel");
                    Check(StoryNode.id!=previous,"Dialogue click did not advance");next+=3;
                });
            }
            steps.Add(()=>{
                Check(StoryNode.kind==StoryRuntimeNodeKind.Choice,"Expected story choice");
                foreach(var text in UnityEngine.Object.FindObjectsOfType<Text>())
                    Check(!text.text.Contains("VARIABLES  ")&&!text.text.Contains(" = "),"Debug variables leaked into story UI");
                Click("Story choice 0");next+=3;
            });
            steps.Add(()=>{
                Check(StoryNode.id=="choice-response","Choice response missing");
                var vars=(Dictionary<string,string>)typeof(RhythmFrontend).GetField("storyVariables",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(frontend);
                Check(vars.Count>0,"Choice variable was not written");
                Click("Story continue");next+=3;
            });
            for(int i=0;i<3;i++)steps.Add(()=>{Click("Story continue");next+=3;});
            steps.Add(()=>{Check(StoryNode.kind==StoryRuntimeNodeKind.Stage,"Stage command not reached");Click("Story command action");next+=3;});
            steps.Add(()=>{
                Check(frontend.Page==FrontendPage.Playing,"Story stage did not start");
                var session=(RhythmDemoController)typeof(RhythmFrontend).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(frontend);
                session.ReturnToSongs();
                Check(frontend.Page==FrontendPage.StoryLevels,"Story stage return failed");
            });
        }
        static void Tick()
        {
            if(finishing)return;
            try
            {
                double now=EditorApplication.timeSinceStartup;
                Check(now<deadline,"Editor Play-mode validation timed out");
                Check(errors==0,"Editor runtime errors: "+errors);
                if(now<next)return;
                if(frontend==null)
                {
                    frontend=UnityEngine.Object.FindObjectOfType<RhythmFrontend>();
                    if(frontend==null||frontend.Page!=FrontendPage.Title){frontend=null;return;}
                    view=(RhythmFrontendView)typeof(RhythmFrontend).GetField("view",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(frontend);
                    Setup();next=now+2;return;
                }
                if(view.DiaryPromptActive){view.AdvanceMonologue();next=now+.25;return;}
                if(step==steps.Count){Finish(true,"All Editor raycast interactions passed");return;}
                next=now+1.15;steps[step++]();
            }
            catch(Exception ex){Finish(false,"Step "+step+": "+ex);}
        }
        static void Finish(bool passed,string detail)
        {
            finishing=true;EditorApplication.update-=Tick;
            SessionState.SetBool(Active,false);
            string result="DESKTOP_EDITOR_SMOKE_PASS="+passed+" clicks="+clicks+" errors="+errors+"\n"+detail;
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));File.WriteAllText(resultPath,result);
            Debug.Log(result);EditorApplication.Exit(passed?0:1);
        }
    }
}
