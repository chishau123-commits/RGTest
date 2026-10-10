using UnityEngine;

namespace GeometryRhythm
{
    public sealed class MonologuePage
    {
        public readonly string SpeakerName;
        public readonly string Text;
        public readonly Texture2D Portrait;
        public readonly Color Accent;

        public MonologuePage(string speakerName, string text, Texture2D portrait, Color accent)
        {
            SpeakerName = speakerName;
            Text = text;
            Portrait = portrait;
            Accent = accent;
        }
    }
}
