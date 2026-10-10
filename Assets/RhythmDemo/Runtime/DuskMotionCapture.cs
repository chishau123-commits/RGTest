using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace GeometryRhythm
{
    // Opt-in render regression: captures actual consecutive GPU frames, including direction changes.
    public sealed class DuskMotionCapture : MonoBehaviour
    {
        string directory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-duskMotionCapture");
            if(index<0||index+1>=args.Length)return;
            var capture=new GameObject("Dusk motion regression").AddComponent<DuskMotionCapture>();
            capture.directory=Path.GetFullPath(args[index+1]);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(directory);
            yield return null;
            var stage=new GameObject("Motion test stage").AddComponent<DormitoryDuskScene>();
            stage.Initialize();stage.enabled=false;
            var pixels=new Texture2D(640,360,TextureFormat.RGB24,false);
            var preview=RenderTexture.GetTemporary(640,360,0);
            var trace=new System.Text.StringBuilder("frame,state,camera,door,music\n");
            // Entrance hold + descent, walk right/left, idle at stairs, then ascend.
            bool passed=!stage.TryAscend();
            for(int i=0;i<721;i++)
            {
                if(i==440)passed&=!stage.TryAscend();
                if(i==540)passed&=stage.TryAscend()&&!stage.TryAscend();
                stage.Step(i>=360&&i<440?1:i>=440&&i<520?-1:0,1f/60);
                stage.RenderFrame();
                trace.AppendLine(i+","+stage.State+","+stage.CameraOffset.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+","+stage.DoorOpen.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+stage.MusicPlaying);
                var previous=RenderTexture.active;Graphics.Blit(stage.Output,preview);RenderTexture.active=preview;
                pixels.ReadPixels(new Rect(0,0,640,360),0,0);pixels.Apply();RenderTexture.active=previous;
                File.WriteAllBytes(Path.Combine(directory,i.ToString("D4")+".png"),pixels.EncodeToPNG());
                yield return null;
            }
            Destroy(pixels);Destroy(stage.gameObject);
            RenderTexture.ReleaseTemporary(preview);File.WriteAllText(Path.Combine(directory,"trace.csv"),trace.ToString());
            passed&=stage.State==DormitoryDuskScene.TravelState.Finished;
            Debug.Log("DUSK_MOTION_CAPTURE_PASS="+passed+" 721 frames / descend, right, left, ascend");
            Application.Quit(passed?0:1);
        }
    }
}

