using UnityEngine;

namespace GeometryRhythm
{
    /// <summary>Personal reading speed, not authored timing or camera data.</summary>
    public static class NoteScrollSettings
    {
        public const string PreferenceKey = "GeometryRhythm.VideoNoteScrollSpeed";
        public const string ReadabilityPreferenceKey = "GeometryRhythm.VideoNoteReadabilityVersion";
        public const int ReadabilityVersion = 1;
        public const float Default = 8, Minimum = .5f, Maximum = 48;
        // Touch UI steps a whole multiplier; the desktop editor keeps its finer 0.25 steps.
        public const float Step = 1;
        public static float Sanitize(float value)
            => float.IsNaN(value) || float.IsInfinity(value) ? Default : Mathf.Clamp(value, Minimum, Maximum);
        // Upgrade old 3x-6x preferences as well as the default. Otherwise an
        // existing installation would silently keep the long, crowded approach.
        // Once the user adjusts speed in this version, respect even slower choices.
        public static float ResolveSavedSpeed(float value, int readabilityVersion)
            => readabilityVersion < ReadabilityVersion ? Mathf.Max(Default, Sanitize(value)) : Sanitize(value);
        public static float Load() => ResolveSavedSpeed(PlayerPrefs.GetFloat(PreferenceKey, Default),
            PlayerPrefs.GetInt(ReadabilityPreferenceKey, 0));
        public static void Save(float value)
        {
            PlayerPrefs.SetFloat(PreferenceKey, Sanitize(value));
            PlayerPrefs.SetInt(ReadabilityPreferenceKey, ReadabilityVersion);
            PlayerPrefs.Save();
        }
    }
}
