using System;
using System.Collections;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace GeometryRhythm
{
    // Opt-in QA records the actual Unity DSP output, including the source's low-pass filter.
    public sealed class DormitoryRailwayAudioCapture : MonoBehaviour
    {
        string directory;
        bool recording;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-railwayAudioCapture");
            if(index<0||index+1>=args.Length)return;
            var capture=new GameObject("Railway audio regression").AddComponent<DormitoryRailwayAudioCapture>();
            capture.directory=Path.GetFullPath(args[index+1]);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(directory);yield return null;
            var config=AudioSettings.GetConfiguration();config.speakerMode=AudioSpeakerMode.Stereo;
            bool passed=AudioSettings.Reset(config);
            var stage=new GameObject("Railway audio test stage").AddComponent<DormitoryDuskScene>();
            stage.Initialize();stage.enabled=false;
            AudioSource train=null;
            foreach(var source in stage.GetComponentsInChildren<AudioSource>())
                if(source.name=="Railway rolling sound / building occlusion")train=source;
            passed&=train!=null&&train.GetComponent<AudioLowPassFilter>()!=null&&
                stage.GetComponent<AudioLowPassFilter>()==null;
            Time.captureFramerate=30;
            recording=AudioRenderer.Start();passed&=recording;
            int sampleRate=AudioSettings.outputSampleRate,totalSamples=0,blockedFrames=0,openFrames=0;
            float maxJump=0,lastOcclusion=0,minPan=1,maxPan=-1;
            var trace=new StringBuilder("time,pass,playing,volume,occlusion,cutoff,pan\n");
            using(var stream=new FileStream(Path.Combine(directory,"railway-dsp.wav"),FileMode.Create))
            using(var writer=new BinaryWriter(stream))
            {
                for(int i=0;i<44;i++)writer.Write((byte)0);
                for(int frame=0;frame<84*30;frame++)
                {
                    stage.Step(0,1f/30);
                    // Capture only this source; scene BGM/voices keep their ordinary runtime settings.
                    foreach(var source in FindObjectsOfType<AudioSource>())source.mute=source!=train;
                    if(stage.TrainPassing)
                    {
                        maxJump=Mathf.Max(maxJump,Mathf.Abs(stage.RailwayOcclusion-lastOcclusion));
                        minPan=Mathf.Min(minPan,stage.RailwayAudioPan);maxPan=Mathf.Max(maxPan,stage.RailwayAudioPan);
                        if(stage.RailwayOcclusion>.98f)
                        {
                            blockedFrames++;passed&=stage.RailwayAudioPlaying&&stage.RailwayAudioVolume>.10f&&stage.RailwayCutoff<720;
                        }
                        if(stage.RailwayOcclusion<.02f&&stage.RailwayAudioVolume>.10f)
                        {openFrames++;passed&=stage.RailwayCutoff>6000;}
                    }
                    else passed&=!stage.RailwayAudioPlaying&&stage.RailwayAudioVolume==0;
                    lastOcclusion=stage.RailwayOcclusion;
                    trace.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0:F4},{1},{2},{3:F5},{4:F5},{5:F1},{6:F4}",(frame+1)/30f,stage.TrainPassCount,
                        stage.RailwayAudioPlaying,stage.RailwayAudioVolume,stage.RailwayOcclusion,stage.RailwayCutoff,stage.RailwayAudioPan));
                    yield return new WaitForEndOfFrame();
                    int count=AudioRenderer.GetSampleCountForCaptureFrame()*2;
                    using(var buffer=new NativeArray<float>(count,Allocator.Temp))
                    {
                        passed&=count>0&&AudioRenderer.Render(buffer);
                        for(int i=0;i<count;i++)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(buffer[i],-1,1)*32767));
                    }
                    totalSamples+=count;
                }
                stream.Position=0;
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+totalSamples*2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)2);
                writer.Write(sampleRate);writer.Write(sampleRate*4);writer.Write((short)4);writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(totalSamples*2);
            }
            AudioRenderer.Stop();recording=false;Time.captureFramerate=0;
            passed&=stage.TrainPassCount==2&&blockedFrames>60&&openFrames>60&&maxJump<.08f&&minPan<-.7f&&maxPan>.7f;
            // Repeat entry resets the sound; exit fade mutes even while a train is behind the building.
            Destroy(stage.gameObject);yield return null;passed&=train==null;
            var revisit=new GameObject("Railway repeat visit").AddComponent<DormitoryDuskScene>();
            revisit.Initialize();revisit.enabled=false;passed&=!revisit.RailwayAudioPlaying;
            for(int i=0;i<18*30;i++)revisit.Step(0,1f/30);
            passed&=revisit.RailwayAudioPlaying&&revisit.RailwayOcclusion>.98f;
            revisit.SetExitFade(0);revisit.Step(0,1f/30);passed&=revisit.RailwayAudioVolume==0;
            Destroy(revisit.gameObject);
            File.WriteAllText(Path.Combine(directory,"railway-trace.csv"),trace.ToString());
            string result="RAILWAY_AUDIO_PASS="+passed+" passes=2 blockedFrames="+blockedFrames+
                " openFrames="+openFrames+" maxOcclusionJump="+maxJump+" sampleRate="+sampleRate+
                " seconds="+(totalSamples/(2f*sampleRate))+" independentFilter/continuousOccludedSound/pan/revisit/exit checked";
            File.WriteAllText(Path.Combine(directory,"result.txt"),result);Debug.Log(result);Application.Quit(passed?0:1);
        }
        void OnDestroy()
        {
            if(recording)AudioRenderer.Stop();
            Time.captureFramerate=0;
        }
    }
}
