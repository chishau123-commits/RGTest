using UnityEngine;

namespace GeometryRhythm
{
    /// <summary>Device calibration, separate from the chart's authored music offset.
    /// Positive values delay both notes and judgement to follow late audio output.</summary>
    public static class AudioSyncSettings
    {
        public const string PreferenceKey="GeometryRhythm.AudioSyncMilliseconds";
        public const float Step=5,Minimum=-300,Maximum=300;
        public static float Sanitize(float value)
            =>float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp(value,Minimum,Maximum);
        public static float Load()=>Sanitize(PlayerPrefs.GetFloat(PreferenceKey,0));
        public static void Save(float value)
        {PlayerPrefs.SetFloat(PreferenceKey,Sanitize(value));PlayerPrefs.Save();}
    }
}
