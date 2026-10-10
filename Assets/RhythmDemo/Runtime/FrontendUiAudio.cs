using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeometryRhythm
{
    public sealed class FrontendUiAudio : MonoBehaviour
    {
        public static readonly string[] Cues={"hover","click","select","open","close","minimize","restore",
            "confirm","cancel","like","send","error","notification","save","login","logout","performance","ready","stage-start",
            "album-unfold","album-settle","key-0","key-1","key-2","key-3","key-4","key-5"};
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly Dictionary<string,float> last=new Dictionary<string,float>();
        readonly AudioSource[] voices=new AudioSource[8];
        readonly double[] ends=new double[8];
        public readonly Dictionary<string,int> PlayCounts=new Dictionary<string,int>();
        public event Action<string,double> Played;
        public string LastCue {get;private set;}
        public bool Muted {get;set;}
        public int LoadedCount => clips.Count;
        public void Configure(bool muted)
        {
            Muted=muted;
            foreach(string cue in Cues)
            {
                var clip=Resources.Load<AudioClip>("Audio/UI/"+cue);
                if(clip==null){Debug.LogError("Missing frontend cue: "+cue);continue;}
                clip.LoadAudioData();clips.Add(cue,clip);
            }
            for(int i=0;i<voices.Length;i++)
            {
                var voice=gameObject.AddComponent<AudioSource>();
                voice.playOnAwake=false;voice.loop=false;voice.spatialBlend=0;voice.dopplerLevel=0;
                voice.ignoreListenerPause=true;voice.volume=.72f;voice.priority=i>=6?48:144;
                voices[i]=voice;
            }
        }
        public bool Has(string cue)=>clips.ContainsKey(cue);
        public void Play(string cue)
        {
            if(string.IsNullOrEmpty(cue)||!clips.TryGetValue(cue,out var clip))return;
            bool key=cue.StartsWith("key-",StringComparison.Ordinal);
            bool important=cue.StartsWith("album-",StringComparison.Ordinal)||cue=="performance"||cue=="ready"||cue=="stage-start"||cue=="login"||cue=="logout"||cue=="save";
            string throttle=key?"keyboard":cue=="hover"?"hover":important?cue:cue=="notification"?cue:"interaction";
            float gap=key?.027f:cue=="hover"?.11f:important?.30f:cue=="notification"?.40f:.035f;
            float now=Time.unscaledTime;
            if(last.TryGetValue(throttle,out float previous)&&now-previous<gap)return;
            last[throttle]=now;
            // Separate lanes preserve important tails and bound rapid-click/typing polyphony.
            int first=key?4:important?6:cue=="notification"?7:0;
            int count=key?2:important||cue=="notification"?1:4;
            int slot=first;
            for(int i=first;i<first+count;i++)
            {
                if(!voices[i].isPlaying){slot=i;break;}
                if(ends[i]<ends[slot])slot=i;
            }
            var source=voices[slot];source.Stop();source.clip=clip;source.mute=Muted;source.pitch=1;
            source.Play();ends[slot]=AudioSettings.dspTime+clip.length;
            LastCue=cue;PlayCounts.TryGetValue(cue,out int played);PlayCounts[cue]=played+1;
            Played?.Invoke(cue,AudioSettings.dspTime);
        }
    }
}
