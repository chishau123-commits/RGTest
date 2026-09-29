using UnityEngine;

namespace GeometryRhythm
{
    /// <summary>Visible travel distance as a percentage of the full path.</summary>
    public static class NoteSpawnSettings
    {
        public const string PreferenceKey = "GeometryRhythm.NoteSpawnPercent";
        public const float Default = 100, Minimum = 20, Maximum = 100, Step = 5;
        public static float Sanitize(float value)
            => float.IsNaN(value) || float.IsInfinity(value) ? Default : Mathf.Clamp(value, Minimum, Maximum);
        public static float Load() => Sanitize(PlayerPrefs.GetFloat(PreferenceKey, Default));
        public static void Save(float value)
        {
            PlayerPrefs.SetFloat(PreferenceKey, Sanitize(value));
            PlayerPrefs.Save();
        }
    }
}
