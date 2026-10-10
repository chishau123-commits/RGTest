using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace GeometryRhythm
{
    public sealed partial class RhythmFrontend
    {
        IEnumerator MusicScreensCapture(string[] args)
        {
            motionDirectory=args[Array.IndexOf(args,"-musicScreensCapture")+1];
            Directory.CreateDirectory(motionDirectory);
            float deadline=Time.realtimeSinceStartup+30;
            while(Page!=FrontendPage.Title&&Time.realtimeSinceStartup<deadline)yield return null;
            if(Page!=FrontendPage.Title){Debug.LogError("Music capture startup timeout");Application.Quit(1);yield break;}
            yield return new WaitForSecondsRealtime(2);
            view.SetTitlePassword("0000");motionPassed&=view.SubmitTitleLogin();
            yield return new WaitForSecondsRealtime(5);
            motionCaptureStarted=Time.realtimeSinceStartup;
            Action<int> clickAlbum=index=>
            {
                Canvas.ForceUpdateCanvases();var shelf=view.AlbumShelf;
                var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=shelf.CardScreenPoint(index),button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
                motionPassed&=hits.Count>0&&hits[0].gameObject==shelf.gameObject;
                UnityEngine.EventSystems.ExecuteEvents.Execute(shelf.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            };
            FrontendAudioRecorder albumRecorder=null;
            var albumAudioEvents=new System.Collections.Generic.List<string>();
            if(Array.IndexOf(args,"-albumAudioCapture")>=0)
            {
                motionPassed&=MusicTrackCount==5&&SelectedMusicTrack==4&&MusicTrackAvailable(4)&&MusicDurationSeconds>100;
                albumRecorder=FindObjectOfType<AudioListener>().gameObject.AddComponent<FrontendAudioRecorder>();
                albumRecorder.Begin();uiAudio.Muted=false;menuBgm.mute=false;
                double audioStarted=AudioSettings.dspTime;
                uiAudio.Played+=(cue,time)=>albumAudioEvents.Add((time-audioStarted).ToString("F5",System.Globalization.CultureInfo.InvariantCulture)+","+cue);
                motionPassed&=view.Invoke("Open music player");yield return MotionSegment("default-music",1.2f);
                motionPassed&=MusicPlaybackSeconds>0&&menuBgm.isPlaying;
                Capture(motionDirectory,"default-music");
                motionPassed&=view.Invoke("Select music track 0");yield return MotionSegment("change-music",.6f);
                motionPassed&=SelectedMusicTrack==0;
                motionPassed&=view.Invoke("Select music track 4");yield return MotionSegment("restore-default-music",.6f);
                motionPassed&=SelectedMusicTrack==4;
                motionPassed&=view.Invoke("Close music player");yield return MotionSegment("close-music",.5f);
            }
            yield return MotionSegment("desktop",.5f);
            motionPassed&=view.Invoke("Open songs");
            yield return MotionSegment("enter-performance",5.6f);
            motionPassed&=Page==FrontendPage.SongModes;
            Capture(motionDirectory,"mode-select");
            if(Array.IndexOf(args,"-modeHoverCapture")>=0)
            {
                PixelMusicArtwork train=null,record=null;
                foreach(var art in FindObjectsOfType<PixelMusicArtwork>())
                    if(art.name=="Route artwork"){if(art.kind==1)train=art;else if(art.kind==2)record=art;}
                motionPassed&=train!=null&&record!=null;
                if(train!=null&&record!=null)
                {
                    train.PreviewPointer=record.PreviewPointer=new Vector2(-100,-100);
                    yield return MotionSegment("hover-idle",1);
                    var left=(RectTransform)train.transform.parent;
                    train.PreviewPointer=record.PreviewPointer=RectTransformUtility.WorldToScreenPoint(null,left.TransformPoint(left.rect.center));
                    yield return MotionSegment("train-city-parallax",3);
                    motionPassed&=train.MotionSpeed>.99f&&record.MotionSpeed==0&&train.Travel>40;
                    Capture(motionDirectory,"train-hover");
                    var right=(RectTransform)record.transform.parent;
                    // Caption area outside the artwork: the entire card must respond.
                    train.PreviewPointer=record.PreviewPointer=RectTransformUtility.WorldToScreenPoint(null,right.TransformPoint(new Vector2(340,-320)));
                    yield return MotionSegment("record-playing",3);
                    motionPassed&=record.MotionSpeed>.99f&&train.MotionSpeed==0&&record.Rotation>0;
                    Capture(motionDirectory,"record-hover");
                    train.PreviewPointer=record.PreviewPointer=new Vector2(-100,-100);
                    yield return MotionSegment("hover-coast-to-rest",1.3f);
                    float stopped=record.Rotation;
                    yield return MotionSegment("hover-stopped",.5f);
                    motionPassed&=train.MotionSpeed==0&&record.MotionSpeed==0&&record.Rotation==stopped;
                }
            }
            view.PreviewControlState("Open story mode",1);yield return MotionSegment("story-hover",.4f);
            motionPassed&=view.Invoke("Open story mode");yield return MotionSegment("story-select",2.4f);
            motionPassed&=Page==FrontendPage.StoryLevels;Capture(motionDirectory,"story-select");
            if(Array.IndexOf(args,"-albumShelfCapture")>=0)
            {
                motionPassed&=view.AlbumEntryReady&&!view.AlbumRouteVisible&&!UiTransitionLocked;
                view.AlbumShelf.OnScroll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){scrollDelta=new Vector2(0,-1)});
                yield return MotionSegment("archive-scroll-locked",1);
                motionPassed&=view.AlbumShelf.Index==1;
                motionPassed&=!view.AlbumDetailVisible;
                clickAlbum(1);yield return MotionSegment("archive-take-locked",1);
                motionPassed&=view.AlbumDetailVisible;
                view.ConfirmAlbum();motionPassed&=Page==FrontendPage.StoryLevels;
                Capture(motionDirectory,"story-locked");
                motionPassed&=view.PutAlbumBack();yield return MotionSegment("archive-put-back",.9f);
                motionPassed&=!view.AlbumDetailVisible&&!view.AlbumShelf.IsExtracting;
                motionPassed&=view.Invoke("Select chapter 2");yield return MotionSegment("archive-select-third",1);
                motionPassed&=view.AlbumShelf.Index==2;
                view.AlbumShelf.Step(-1);view.AlbumShelf.Step(-1);view.AlbumShelf.Step(1);view.AlbumShelf.Step(-1);
                yield return MotionSegment("archive-rapid-return",1.1f);
                motionPassed&=view.AlbumShelf.Index==0&&view.AlbumShelf.Settled;
                Capture(motionDirectory,"story-selected");
                clickAlbum(0);yield return MotionSegment("archive-take-story",1);
                motionPassed&=view.AlbumDetailVisible;Capture(motionDirectory,"story-taken");
                motionPassed&=view.PutAlbumBack();yield return MotionSegment("archive-put-story",.9f);
            }
            motionPassed&=view.Invoke("Back to modes");yield return MotionSegment("back-to-modes",1f);
            motionPassed&=view.Invoke("Open free mode");yield return MotionSegment("free-play",2.4f);
            motionPassed&=Page==FrontendPage.Songs;Capture(motionDirectory,"free-play");
            if(Array.IndexOf(args,"-albumShelfCapture")>=0)
            {
                var shelf=view.AlbumShelf;
                shelf.Step(-1);shelf.Step(1);
                if(songs.Count==1)motionPassed&=shelf.Index==0;
                clickAlbum(0);yield return MotionSegment("record-first-click",.5f);
                motionPassed&=!view.AlbumDetailVisible&&!shelf.IsExtracting;
                view.ConfirmAlbum();motionPassed&=view.PutAlbumBack()&&!shelf.IsExtracting;
                clickAlbum(0);yield return MotionSegment("record-take-out",1);
                motionPassed&=view.AlbumDetailVisible;Capture(motionDirectory,"free-taken");
                yield return MotionSegment("record-detail-hold",.5f);
                motionPassed&=view.PutAlbumBack();yield return MotionSegment("record-put-back",.9f);
            }
            if(songs.Count>0)
            {
                int target=Mathf.Min(1,songs.Count-1);
                motionPassed&=view.Invoke("Select song "+target);yield return MotionSegment("select-track",1f);
                motionPassed&=Selected==target;Capture(motionDirectory,"free-play-selected");
            }
            if(Array.IndexOf(args,"-albumShelfCapture")>=0&&songs.Count>0)
            {
                // In-memory stress fixture only; never added to the player's real library.
                var fixture=new System.Collections.Generic.List<SongEntry>();
                for(int i=0;i<12;i++)
                {
                    var entry=SongEntry.Parse(songs[0].Asset);entry.Chart.title="QA / Fixture "+(i+1);fixture.Add(entry);
                }
                view.ShowSongs(fixture,0);yield return MotionSegment("qa-many-entry",1.9f);
                var shelf=view.AlbumShelf;shelf.Selected=i=>view.ShowSongs(fixture,i);
                for(int i=0;i<20;i++)shelf.Step(1);
                yield return MotionSegment("qa-many-end",1.8f);
                motionPassed&=view.AlbumShelf==shelf&&shelf.Index==11&&shelf.Settled;
                shelf.OnBeginDrag(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=new Vector2(400,400)});
                shelf.OnDrag(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=new Vector2(600,400)});
                shelf.OnEndDrag(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=new Vector2(600,400)});
                yield return MotionSegment("qa-drag",1);
                motionPassed&=shelf.Index==10;
                view.ShowSongs(new System.Collections.Generic.List<SongEntry>(),0);
                yield return MotionSegment("qa-empty",1.9f);motionPassed&=view.AlbumShelf.Count==0;
                view.ShowSongs(songs,Selected);yield return MotionSegment("qa-real-library-restored",1.9f);
                motionPassed&=view.AlbumShelf.Count==songs.Count;
            }
            motionPassed&=view.Invoke("Back to modes");yield return MotionSegment("return-to-modes",1f);
            motionPassed&=view.Invoke("Back to main");yield return MotionSegment("return-desktop",2.7f);
            motionPassed&=Page==FrontendPage.Main;
            if(albumRecorder!=null)
            {
                motionPassed&=uiAudio.PlayCounts.TryGetValue("album-unfold",out int unfolds)&&unfolds==2;
                motionPassed&=uiAudio.PlayCounts.TryGetValue("album-settle",out int settles)&&settles==2;
                motionPassed&=albumRecorder.Save(Path.Combine(motionDirectory,"album-listener.wav"));
                File.WriteAllLines(Path.Combine(motionDirectory,"album-audio-events.csv"),albumAudioEvents);
            }
            File.WriteAllLines(Path.Combine(motionDirectory,"frames.csv"),motionFrames);
            File.WriteAllText(Path.Combine(motionDirectory,"result.txt"),"MUSIC_SCREENS_CAPTURE_PASS="+motionPassed+" frames="+motionFrame);
            Application.Quit(motionPassed?0:1);
        }
    }
}
