using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeometryRhythm
{
    // Opt-in QA recording on the actual AudioListener, never active during normal play.
    public sealed class FrontendAudioRecorder : MonoBehaviour
    {
        readonly object gate=new object();float[] samples;int length,channelCount;
        public void Begin(){lock(gate){samples=new float[AudioSettings.outputSampleRate*60*2];length=0;}}
        void OnAudioFilterRead(float[] data,int channels)
        {
            lock(gate)
            {
                if(samples==null)return;
                channelCount=channels;int count=Math.Min(data.Length,samples.Length-length);
                Array.Copy(data,0,samples,length,count);length+=count;
            }
        }
        public bool Save(string path)
        {
            lock(gate)
            {
                float peak=0;double energy=0;
                for(int i=0;i<length;i++){peak=Math.Max(peak,Math.Abs(samples[i]));energy+=samples[i]*samples[i];}
                using(var w=new BinaryWriter(File.Create(path)))
                {
                    int ch=Math.Max(1,channelCount),rate=AudioSettings.outputSampleRate;
                    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+length*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    w.Write(16);w.Write((short)1);w.Write((short)ch);w.Write(rate);w.Write(rate*ch*2);w.Write((short)(ch*2));w.Write((short)16);
                    w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(length*2);
                    for(int i=0;i<length;i++)w.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i],-1,1)*32767));
                }
                File.WriteAllText(path+".txt","samples="+length+" channels="+channelCount+" peak="+peak+" rms="+Math.Sqrt(energy/Math.Max(1,length)));
                samples=null;return length>0&&peak>.005f&&peak<.99f;
            }
        }
    }
    public sealed partial class RhythmFrontend
    {
        IEnumerator FrontendAudioCapture(string[] args)
        {
            string directory=args[Array.IndexOf(args,"-frontendAudioCapture")+1];Directory.CreateDirectory(directory);
            float deadline=Time.realtimeSinceStartup+30;
            while(Page!=FrontendPage.Title&&Time.realtimeSinceStartup<deadline)yield return null;
            if(Page!=FrontendPage.Title){Debug.LogError("Audio capture startup timeout");Application.Quit(1);yield break;}
            yield return new WaitForSecondsRealtime(2);
            bool pass=uiAudio.LoadedCount==FrontendUiAudio.Cues.Length;
            var recorder=FindObjectOfType<AudioListener>().gameObject.AddComponent<FrontendAudioRecorder>();
            recorder.Begin();uiAudio.Muted=false;
            var events=new List<string>();double started=AudioSettings.dspTime;
            uiAudio.Played+=(cue,time)=>events.Add((time-started).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+cue);
            view.SetTitlePassword("1111");pass&=!view.SubmitTitleLogin();yield return new WaitForSecondsRealtime(.65f);
            view.SetTitlePassword("0000");pass&=view.SubmitTitleLogin();yield return new WaitForSecondsRealtime(5);
            pass&=Page==FrontendPage.Main;
            if(Array.IndexOf(args,"-withFrontendBgm")>=0&&menuBgm!=null)menuBgm.mute=false;
            PlayUiHover("Open diary");yield return new WaitForSecondsRealtime(.25f);
            pass&=view.Invoke("Open diary");yield return new WaitForSecondsRealtime(.8f);
            pass&=view.Invoke("Minimize diary");yield return new WaitForSecondsRealtime(.8f);
            pass&=view.Invoke("Restore diary");yield return new WaitForSecondsRealtime(.8f);
            pass&=view.Invoke("Open news");yield return new WaitForSecondsRealtime(.8f);
            PlayNotificationSound();yield return new WaitForSecondsRealtime(.9f);
            PlayUiSound("Like post 0");yield return new WaitForSecondsRealtime(.5f);
            PlayUiSound("Post comment");yield return new WaitForSecondsRealtime(.5f);
            PlayUiCue("save");yield return new WaitForSecondsRealtime(1);
            for(int i=0;i<12;i++){PlayKeyboardKeySound();yield return new WaitForSecondsRealtime(.055f);}
            pass&=view.Invoke("Restore main");yield return new WaitForSecondsRealtime(.8f);
            pass&=view.Invoke("Open start menu");yield return new WaitForSecondsRealtime(.55f);
            pass&=view.Invoke("Close start menu");yield return new WaitForSecondsRealtime(.55f);
            // Actual click path, then direct Enter handler path. Both must emit the same cue once.
            pass&=view.Invoke("Open songs");yield return new WaitForSecondsRealtime(5.6f);
            pass&=Page==FrontendPage.SongModes;
            pass&=uiAudio.PlayCounts["performance"]==1&&uiAudio.PlayCounts["ready"]==1;
            pass&=view.Invoke("Back to main");yield return new WaitForSecondsRealtime(2.7f);
            EnterMusic();EnterMusic();yield return new WaitForSecondsRealtime(5.6f);
            pass&=uiAudio.PlayCounts["performance"]==2&&uiAudio.PlayCounts["ready"]==2;
            // Dense ordinary clicks must not cut off a reserved important cue or clip the mix.
            PlayUiCue("performance");
            for(int i=0;i<24;i++){PlayUiSound("Test click");PlayUiHover("Test hover");PlayKeyboardKeySound();yield return new WaitForSecondsRealtime(.04f);}
            yield return new WaitForSecondsRealtime(1);
            pass&=recorder.Save(Path.Combine(directory,"unity-ui-mix.wav"));
            File.WriteAllLines(Path.Combine(directory,"events.csv"),events);
            File.WriteAllText(Path.Combine(directory,"result.txt"),"FRONTEND_AUDIO_PASS="+pass+" clips="+uiAudio.LoadedCount+" events="+events.Count);
            Application.Quit(pass?0:1);
        }
    }
}
