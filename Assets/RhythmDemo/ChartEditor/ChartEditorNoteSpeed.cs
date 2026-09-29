using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        float noteReadSpeed = NoteScrollSettings.Default;
        SpatialDirector CreateEditorSpatial() => new SpatialDirector(chart, tempo, noteReadSpeed);

        void SetNoteReadSpeed(float value, bool persist = true)
        {
            float next = NoteScrollSettings.Sanitize(value);
            if (Mathf.Abs(next - noteReadSpeed) < .0001f) return;
            noteReadSpeed = next;
            // Presentation-only: never touch ticks, BPM, offsets, authored geometry,
            // undo history or the running audio/video clock.
            spatial = CreateEditorSpatial();
            RebuildVisuals();
            if (persist && !smokeMode) NoteScrollSettings.Save(noteReadSpeed);
            SetStatus("Note scroll " + noteReadSpeed.ToString("0.##") + "x / from path end. Timing and camera unchanged.");
        }

        void DrawNoteSpeedControls()
        {
            if (!VideoSpaceEditing) return;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Scroll " + noteReadSpeed.ToString("0.##") + "x", GUILayout.MinWidth(78));
            if (GUILayout.Button("-", GUILayout.Width(28))) SetNoteReadSpeed(noteReadSpeed - .25f);
            if (GUILayout.Button("+", GUILayout.Width(28))) SetNoteReadSpeed(noteReadSpeed + .25f);
            if (GUILayout.Button("1x", GUILayout.Width(36))) SetNoteReadSpeed(1);
            GUILayout.EndHorizontal();
            GUILayout.Label("Quick speed / default 8x", smallStyle);
            GUILayout.BeginHorizontal();
            foreach (float preset in new[] { 4f, 6f, 8f, 10f, 12f })
                if (GUILayout.Button(preset.ToString("0") + "x")) SetNoteReadSpeed(preset);
            GUILayout.EndHorizontal();
            GUILayout.Label("Spawn: PATH END (locked)\nAhead here " + spatial.NoteLookaheadSeconds(songTime).ToString("0.00") + "s / [ ] speed", smallStyle);
        }
    }
}
