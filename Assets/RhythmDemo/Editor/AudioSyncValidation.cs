using System;
using System.Reflection;
using UnityEngine;

namespace GeometryRhythm.Editor
{
    public static class AudioSyncValidation
    {
        public static void Run()
        {
            var go=new GameObject("Audio output timing regression");
            var source=go.AddComponent<AudioSource>();
            var clip=AudioClip.Create("Timing fixture",48000*8,1,48000,false);source.clip=clip;
            int checks=0;
            try
            {
                foreach(double delay in new[]{-.3,-.12,0,.1,.3})
                foreach(double offset in new[]{-.37,0,.125})
                foreach(double start in new[]{0,4})
                {
                    double now=10,scheduled=-1;
                    var clock=new SongClock(source,8,offset,delay);
                    // Drive the actual transport while replacing only wall time and
                    // the audio device's PlayScheduled boundary in the headless editor.
                    Set(clock,"readDsp",(Func<double>)(()=>now));
                    Set(clock,"playScheduled",(Action<double>)(value=>scheduled=value));
                    clock.Seek(start,false);
                    Check(scheduled>=now+.119999,"all calibration signs schedule in the future",ref checks);
                    Check(Math.Abs(clock.Time-start)<.000001,"seek holds until scheduled start",ref checks);
                    now=scheduled+delay+.75;
                    Check(Math.Abs(clock.Time+offset-(Math.Max(0,start+offset)+.75))<.000001,
                        "notes follow the audible clip, including delayed BGM onset",ref checks);
                    double paused=clock.Time;clock.SetPaused(true);now+=2;
                    Check(Math.Abs(clock.Time-paused)<.000001,"pause freezes calibrated song time",ref checks);
                    clock.SetOutputDelay(-delay);
                    Check(Math.Abs(clock.Time-paused)<.000001,"changing calibration while paused preserves progress",ref checks);
                    clock.SetPaused(false);
                    Check(scheduled>=now+.119999,"resume has scheduling lead time",ref checks);
                    now=scheduled-delay+.75;
                    Check(Math.Abs(clock.Time+offset-(Math.Max(0,paused+offset)+.75))<.000001,
                        "resume keeps audio and gameplay aligned with new calibration",ref checks);
                }
                Check(AudioSyncSettings.Sanitize(float.NaN)==0&&AudioSyncSettings.Sanitize(float.PositiveInfinity)==0,"invalid calibration is neutral",ref checks);
                Check(AudioSyncSettings.Sanitize(999)==300&&AudioSyncSettings.Sanitize(-999)==-300,"calibration bounds",ref checks);
                Debug.Log("AUDIO_SYNC_VALIDATION_SUCCESS "+checks+" checks");
            }
            finally {UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(clip);}
        }
        static void Set(SongClock clock,string field,object value)
            =>typeof(SongClock).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(clock,value);
        static void Check(bool condition,string message,ref int checks)
        {checks++;if(!condition)throw new Exception("Audio sync: "+message);}
    }
}
