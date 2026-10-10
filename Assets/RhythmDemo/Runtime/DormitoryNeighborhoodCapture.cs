using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace GeometryRhythm
{
    // Opt-in GPU capture also exercises repeat visits, two train passes, and exit cleanup.
    public sealed class DormitoryNeighborhoodCapture : MonoBehaviour
    {
        string directory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-neighborhoodCapture");
            if(index<0||index+1>=args.Length)return;
            var capture=new GameObject("Neighborhood render check").AddComponent<DormitoryNeighborhoodCapture>();
            capture.directory=Path.GetFullPath(args[index+1]);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(directory);yield return null;
            var stage=new GameObject("Neighborhood test stage").AddComponent<DormitoryDuskScene>();
            stage.Initialize();stage.enabled=false;
            bool railOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"-railAlignmentCapture")>=0;
            bool foregroundOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"-foregroundCapture")>=0;
            var railwayMask=stage.GetComponentInChildren<SpriteMask>();
            File.WriteAllBytes(Path.Combine(directory,"railway-mask.png"),railwayMask.sprite.texture.EncodeToPNG());
            var pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
            var preview=RenderTexture.GetTemporary(960,540,0);
            bool passed=stage.NeighborhoodActorCount==4&&!stage.TryAscend()&&stage.ActiveChaseActors==0&&
                stage.ChaseStartCount==0&&!stage.ChildrenAudioPlaying&&stage.ChildrenAudioLength>=4;
            float minDog=100,maxDog=-100;int idleTrainFrames=0,activeTrainFrames=0;
            float previousDog=-100,peakVoice=0,minPan=1,maxPan=-1;bool playingSeen=false;
            var trace=new System.Text.StringBuilder("frame,state,chaseStarts,chaseFinished,activeActors,dogX,voicePlaying,volume,pan\n");
            for(int i=0;i<1080;i++)
            {
                // Four simulation substeps for every 12fps capture frame.
                for(int j=0;j<4;j++)stage.Step(0,1f/48);
                if(stage.State==DormitoryDuskScene.TravelState.Descending)
                    passed&=stage.ActiveChaseActors==0&&stage.ChaseStartCount==0&&!stage.ChildrenAudioPlaying;
                else passed&=stage.ChaseStartCount==1;
                passed&=stage.DogPosition.x>=previousDog;
                previousDog=stage.DogPosition.x;
                peakVoice=Mathf.Max(peakVoice,stage.ChildrenAudioVolume);
                minPan=Mathf.Min(minPan,stage.ChildrenAudioPan);maxPan=Mathf.Max(maxPan,stage.ChildrenAudioPan);
                playingSeen|=stage.ChildrenAudioPlaying;
                if(stage.ChaseFinished)passed&=stage.ActiveChaseActors==0&&!stage.ChildrenAudioPlaying;
                trace.AppendLine(i+","+stage.State+","+stage.ChaseStartCount+","+stage.ChaseFinished+","+stage.ActiveChaseActors+
                    ","+stage.DogPosition.x+","+stage.ChildrenAudioPlaying+","+stage.ChildrenAudioVolume+","+stage.ChildrenAudioPan);
                minDog=Mathf.Min(minDog,stage.DogPosition.x);maxDog=Mathf.Max(maxDog,stage.DogPosition.x);
                if(stage.TrainPassing)activeTrainFrames++;else idleTrainFrames++;
                // Verify player movement and zoom while ambient behavior continues.
                if(!railOnly)
                {
                    if(i==350)stage.MoveTo(6.6f);
                    if(i==390)stage.ScrollZoom(3);
                    if(i==480)stage.ScrollZoom(-10);
                    if(i==530)stage.MoveTo(0);
                }
                if(railOnly&&i!=144&&i!=252&&i!=270&&i!=288&&i!=780&&i!=798&&i!=816)
                {yield return null;continue;}
                if(foregroundOnly&&i!=70&&i!=74&&i!=78&&i!=82&&i!=114)
                {yield return null;continue;}
                stage.RenderFrame();
                var previous=RenderTexture.active;Graphics.Blit(stage.Output,preview);RenderTexture.active=preview;
                pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();RenderTexture.active=previous;
                File.WriteAllBytes(Path.Combine(directory,i.ToString("D4")+".png"),pixels.EncodeToPNG());
                if(i==78||i==114)
                {
                    string edge=i==78?"left":"right";
                    var renderers=stage.GetComponentsInChildren<Renderer>(true);
                    foreach(var r in renderers)if(IsNeighbor(r.name))r.enabled=false;
                    SaveDiagnostic(stage,pixels,preview,"occlusion-background-"+edge);
                    foreach(var r in renderers)if(IsNeighbor(r.name))r.enabled=true;
                    foreach(var r in renderers)if(IsForeground(r.name))r.enabled=false;
                    SaveDiagnostic(stage,pixels,preview,"occlusion-revealed-"+edge);
                    foreach(var r in renderers)if(IsForeground(r.name))r.enabled=true;
                }
                if(i==144||i==270)
                {
                    foreach(var r in stage.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        if(r.name!="Four carriage sunset commuter train")continue;
                        r.enabled=false;SaveDiagnostic(stage,pixels,preview,"train-hidden-"+i);r.enabled=true;
                    }
                }
                yield return null;
            }
            passed&=minDog<-16&&maxDog>16&&stage.TrainPassCount>=2&&idleTrainFrames>200&&activeTrainFrames>200;
            passed&=stage.ChaseFinished&&stage.ChaseStartCount==1&&peakVoice>.4f&&minPan<-.7f&&maxPan>.7f&&playingSeen;
            passed&=stage.TryAscend()&&!stage.TryAscend();
            for(int i=0;i<240;i++)stage.Step(0,1f/60);
            passed&=stage.State==DormitoryDuskScene.TravelState.Finished;
            int passes=stage.TrainPassCount;Destroy(stage.gameObject);yield return null;
            var revisit=new GameObject("Neighborhood repeat visit").AddComponent<DormitoryDuskScene>();
            revisit.Initialize();revisit.enabled=false;
            passed&=revisit.NeighborhoodActorCount==4&&revisit.TrainPassCount==0&&revisit.ChaseStartCount==0&&
                revisit.ActiveChaseActors==0&&!revisit.ChildrenAudioPlaying;
            for(int i=0;i<480;i++)revisit.Step(0,1f/60);
            passed&=revisit.ChaseStartCount==1&&revisit.ActiveChaseActors==3&&revisit.ChildrenAudioPlaying;
            revisit.SetExitFade(0);revisit.Step(0,1f/60);passed&=revisit.ChildrenAudioVolume==0;
            Destroy(revisit.gameObject);Destroy(pixels);RenderTexture.ReleaseTemporary(preview);
            string result="NEIGHBORHOOD_CAPTURE_PASS="+passed+" actors=4 passes="+passes+" dogRange="+minDog+".."+maxDog+
                " idleTrainFrames="+idleTrainFrames+" activeTrainFrames="+activeTrainFrames+
                " oneShotChase/hiddenBeforeDescent/monotonicExit/revisit/fade checked voicePeak="+peakVoice+" pan="+minPan+".."+maxPan;
            File.WriteAllText(Path.Combine(directory,"chase-trace.csv"),trace.ToString());
            File.WriteAllText(Path.Combine(directory,"result.txt"),result);Debug.Log(result);Application.Quit(passed?0:1);
        }
        static bool IsNeighbor(string name)=>name.StartsWith("Child in ")||name.StartsWith("Playful courtyard dog")||name.StartsWith("Elder sitting");
        static bool IsForeground(string name)=>name.StartsWith("Painted foreground")||name=="Independent near foliage";
        void SaveDiagnostic(DormitoryDuskScene stage,Texture2D pixels,RenderTexture preview,string name)
        {
            stage.RenderFrame();var previous=RenderTexture.active;Graphics.Blit(stage.Output,preview);RenderTexture.active=preview;
            pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();RenderTexture.active=previous;
            File.WriteAllBytes(Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
        }
    }
}
