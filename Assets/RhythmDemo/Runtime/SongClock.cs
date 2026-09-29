using System;
using UnityEngine;

namespace GeometryRhythm
{
    /// <summary>Owns the audio/DSP anchors. Pausing an AudioSource alone does not stop DSP time.</summary>
    public sealed class SongClock
    {
        readonly AudioSource source;
        readonly double duration;
        readonly double audioOffset;
        double anchorDsp, anchorSong;
        public bool Paused { get; private set; }
        public double Time => Paused ? anchorSong : Math.Min(duration, anchorSong + Math.Max(0, AudioSettings.dspTime - anchorDsp));
        public SongClock(AudioSource source, double duration, double audioOffset)
        { this.source = source; this.duration = duration; this.audioOffset = audioOffset; }
        public void Seek(double seconds, bool paused)
        {
            anchorSong = Math.Max(0, Math.Min(duration, seconds));
            source.Stop(); Paused = paused;
            if (!paused) Schedule();
        }
        void Schedule()
        {
            anchorDsp = AudioSettings.dspTime + .12;
            if (source.clip == null) return;
            // audioOffset is the position in the audio clip corresponding to chart time zero.
            double audioTime = anchorSong + audioOffset;
            if (audioTime >= source.clip.length) return;
            source.timeSamples = Math.Min(source.clip.samples - 1,
                Math.Max(0, (int)(Math.Max(0, audioTime) * source.clip.frequency)));
            source.PlayScheduled(anchorDsp + Math.Max(0, -audioTime));
        }
        public void SetPaused(bool pause)
        {
            if (pause == Paused) return;
            if (pause) { anchorSong = Time; source.Stop(); Paused = true; }
            else { Paused = false; Schedule(); }
        }
    }

    public static class DemoSoundtrack
    {
        /// <summary>Original synthesized demo music: no downloaded or copyrighted song required.
        /// Generation happens once at load, never in the gameplay update loop.</summary>
        public static AudioClip Create(float duration)
        {
            const int rate = 24000;
            var samples = new float[Mathf.CeilToInt((duration + 1) * rate)];
            int[] bass = { 45, 41, 48, 43 };
            int[] melody = { 0, 7, 12, 16, 12, 7, 19, 16 };
            uint noise = 917;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)rate;
                int beat = (int)(t * 2), eighth = (int)(t * 4);
                double local = t % .5, pluck = t % .25;
                double b = 440 * Math.Pow(2, (bass[(beat / 8) % 4] - 69) / 12.0);
                double f = b * Math.Pow(2, (12 + melody[eighth % 8]) / 12.0);
                double kick = Math.Sin(2 * Math.PI * (47 * local + 6 * (1 - Math.Exp(-local * 30)))) * Math.Exp(-local * 22);
                double low = Math.Sin(2 * Math.PI * b * t) * (.6 + .4 * Math.Exp(-local * 6));
                double bell = (Math.Sin(2 * Math.PI * f * pluck) + .22 * Math.Sin(2 * Math.PI * f * 2 * pluck)) * Math.Exp(-pluck * 15);
                noise = noise * 1664525 + 1013904223;
                double hat = ((noise >> 8) / 8388608.0 - 1) * Math.Exp(-pluck * 85);
                double pad = Math.Sin(2 * Math.PI * b * 2 * t) * .035;
                double fade = Math.Min(1, t / 2) * Math.Min(1, Math.Max(0, duration - t) / 2);
                samples[i] = (float)((.2 * kick + .10 * low + .13 * bell + .035 * hat + pad) * fade);
            }
            var clip = AudioClip.Create("Geometry / original procedural demo", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
