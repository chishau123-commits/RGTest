using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        // Keep the original 0..3 indices for existing editor interaction checks.
        static readonly int[] BeatSnapDivisors = { 1, 2, 4, 0, 8, 16, 3, 6, 12 };
        static readonly int[] BeatSnapOrder = { 0, 1, 2, 4, 5, 6, 7, 8, 3 };
        static readonly string[] BeatSnapLabels = { "1", "1/2", "1/4", "OFF", "1/8", "1/16", "1/3", "1/6", "1/12" };
        int BeatSnapDivisor => BeatSnapDivisors[Mathf.Clamp(timelineSnap, 0, BeatSnapDivisors.Length - 1)];
        string BeatSnapLabel => BeatSnapLabels[Mathf.Clamp(timelineSnap, 0, BeatSnapLabels.Length - 1)];
        NoteData SelectedTimingNote => selectedNote >= 0 && selectedNote < chart.notes.Length ? chart.notes[selectedNote] : null;
        string timingNoteId, timingBeatText;
        int timingNoteTick = -1;
        bool showTimingCalibration;
        TempoData timingTempo;
        float timingBpm, timingOffsetMs, timingOriginalBpm, timingOriginalOffset;

        void CycleBeatSnap()
        {
            int index = Array.IndexOf(BeatSnapOrder, timelineSnap);
            timelineSnap = BeatSnapOrder[(index + 1) % BeatSnapOrder.Length];
        }

        int SnapBeatTick(double beat)
        {
            int divisor = BeatSnapDivisor;
            // Quantize in beat space before converting to ticks. Integer division
            // of PPQ by the subdivision drifts when a chart uses unusual PPQ.
            if (divisor > 0) beat = Math.Round(beat * divisor, MidpointRounding.AwayFromZero) / divisor;
            return (int)Math.Round(Math.Max(0, beat) * chart.ticksPerBeat, MidpointRounding.AwayFromZero);
        }

        int ClampNoteTick(int tick, bool keepGrid)
        {
            int last = Math.Max(0, (int)Math.Ceiling(chart.endBeat * (double)chart.ticksPerBeat) - 1);
            if (tick <= last) return Math.Max(0, tick);
            if (keepGrid && BeatSnapDivisor > 0)
            {
                double lastStep = Math.Floor(last / (double)chart.ticksPerBeat * BeatSnapDivisor);
                return Math.Min(last, (int)Math.Round(lastStep / BeatSnapDivisor * chart.ticksPerBeat, MidpointRounding.AwayFromZero));
            }
            return last;
        }

        double TimelineSeekTime(double time)
        {
            // Notes use the same visible grid for ruler, audio scrubbing and add.
            // Other workspaces retain their continuous camera/anchor inspection.
            if (mode != ChartEditMode.Notes || chartCameraPreview || BeatSnapDivisor == 0) return time;
            return tempo.SecondsAtBeat(Math.Min(chart.endBeat, SnapTimelineTick(time) / (double)chart.ticksPerBeat));
        }

        int NoteAt(string path, int tick, NoteData except = null)
            => Array.FindIndex(chart.notes, n => !ReferenceEquals(n, except) && n.pathId == path && n.tick == tick);

        int NextStepTick(double beat, int direction, bool wholeBeat, bool singleTick = false)
        {
            int divisor = wholeBeat ? 1 : BeatSnapDivisor;
            if (singleTick || divisor == 0)
                return Math.Max(0, (int)Math.Round(beat * chart.ticksPerBeat, MidpointRounding.AwayFromZero) + direction);
            // A tick can represent a fraction only approximately. Treat a value
            // within half a tick of a grid point as that grid point before stepping.
            double step = beat * divisor, nearest = Math.Round(step);
            if (Math.Abs(step - nearest) <= divisor / (2.0 * chart.ticksPerBeat) + 1e-7) step = nearest;
            step = direction > 0 ? Math.Floor(step) + 1 : Math.Ceiling(step) - 1;
            return Math.Max(0, (int)Math.Round(step / divisor * chart.ticksPerBeat, MidpointRounding.AwayFromZero));
        }

        void StepPlayhead(int direction, bool wholeBeat)
        {
            SetPlaying(false);
            int tick = NextStepTick(tempo.BeatAtSeconds(songTime), direction, wholeBeat);
            Seek(tempo.SecondsAtBeat(Math.Min(chart.endBeat, tick / (double)chart.ticksPerBeat)));
            RevealBeatPlayhead();
        }

        void RevealBeatPlayhead()
        {
            if (songTime < timelineStart || songTime > timelineStart + TimelineSpan)
                timelineStart = (float)Math.Max(0, Math.Min(TimelineDuration - TimelineSpan, songTime - TimelineSpan * .5));
        }

        bool RetimeSelectedNote(int tick, bool keepGrid, string message)
        {
            var note = SelectedTimingNote;
            if (note == null) return false;
            tick = ClampNoteTick(tick, keepGrid);
            if (NoteAt(note.pathId, tick, note) >= 0)
            { SetStatus("Not moved: this path already has a note at that tick"); return false; }
            if (tick != note.tick)
                Change(() => {
                    note.tick = tick;
                    Array.Sort(chart.notes, (a, b) => a.tick != b.tick ? a.tick.CompareTo(b.tick) : string.CompareOrdinal(a.id, b.id));
                    selectedNote = Array.IndexOf(chart.notes, note);
                }, message);
            SetPlaying(false); timelineSelection = TimelineKind.Note;
            selectedPath = Array.FindIndex(chart.paths, p => p.id == note.pathId);
            Seek(tempo.SecondsAtBeat(note.tick / (double)chart.ticksPerBeat)); RevealBeatPlayhead();
            return true;
        }

        void SnapSelectedNote()
        {
            var note = SelectedTimingNote;
            if (note != null && BeatSnapDivisor > 0)
                RetimeSelectedNote(SnapBeatTick(note.tick / (double)chart.ticksPerBeat), true, "Selected note snapped to " + BeatSnapLabel + " beat");
        }

        void NudgeSelectedNote(int direction, bool singleTick = false)
        {
            var note = SelectedTimingNote;
            if (note == null) return;
            RetimeSelectedNote(NextStepTick(note.tick / (double)chart.ticksPerBeat, direction, false, singleTick), !singleTick,
                singleTick ? "Note moved by one tick" : "Note moved by one grid step");
        }

        bool ReadBeatEditingKeys(Event e)
        {
            if (mode != ChartEditMode.Notes || chartCameraPreview || flyMode || Cursor.lockState == CursorLockMode.Locked || e.control || e.command) return false;
            if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow)
            {
                int direction = e.keyCode == KeyCode.LeftArrow ? -1 : 1;
                if (e.alt) NudgeSelectedNote(direction, e.shift); else StepPlayhead(direction, e.shift);
            }
            else if (e.alt || e.shift) return false;
            else if (e.keyCode == KeyCode.N) AddNote();
            else if (e.keyCode == KeyCode.Q) SnapSelectedNote();
            else if (e.keyCode == KeyCode.M) ToggleMetronome();
            else if (VideoSpaceEditing && e.keyCode == KeyCode.LeftBracket) SetNoteReadSpeed(noteReadSpeed - .25f);
            else if (VideoSpaceEditing && e.keyCode == KeyCode.RightBracket) SetNoteReadSpeed(noteReadSpeed + .25f);
            else return false;
            e.Use(); return true;
        }

        void DrawBeatNoteInspector()
        {
            GUILayout.Label("Click a NOTES lane / N adds on the beat grid.", smallStyle);
            DrawNoteSpeedControls();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Snap " + BeatSnapLabel, selectedButtonStyle)) CycleBeatSnap();
            if (GUILayout.Button(metronomeEnabled ? "Click ON (M)" : "Click OFF (M)", metronomeEnabled ? selectedButtonStyle : buttonStyle)) ToggleMetronome();
            GUILayout.EndHorizontal();
            bool enabled = GUI.enabled;
            GUILayout.Space(4);
            var selected = SelectedTimingNote;
            if (selected != null)
            {
                double beat = selected.tick / (double)chart.ticksPerBeat;
                double time = tempo.SecondsAtBeat(beat);
                if (timingNoteId != selected.id || timingNoteTick != selected.tick)
                { timingNoteId = selected.id; timingNoteTick = selected.tick; timingBeatText = beat.ToString("0.#########", CultureInfo.InvariantCulture); }
                GUILayout.Label("Selected: " + selected.pathId + " / " + selected.action, headingStyle);
                GUILayout.Label("B " + beat.ToString("0.######") + "  /  tick " + selected.tick + "  /  " + FormatTimelineTime(time), smallStyle);
                GUILayout.BeginHorizontal(); GUILayout.Label("Beat", GUILayout.Width(34));
                GUI.SetNextControlName("Edit:ExactNoteBeat"); timingBeatText = GUILayout.TextField(timingBeatText, GUILayout.MinWidth(50));
                if (GUILayout.Button("Set exact", GUILayout.Width(85)))
                {
                    if (double.TryParse(timingBeatText, NumberStyles.Float, CultureInfo.InvariantCulture, out double requested) &&
                        !double.IsNaN(requested) && requested >= 0 && requested < chart.endBeat)
                        RetimeSelectedNote((int)Math.Round(requested * chart.ticksPerBeat, MidpointRounding.AwayFromZero), false, "Exact note beat applied");
                    else SetStatus("Enter a finite beat from 0 up to (not including) the chart end");
                }
                GUILayout.EndHorizontal();
                double snapMs = (tempo.SecondsAtBeat(ClampNoteTick(SnapBeatTick(beat), true) / (double)chart.ticksPerBeat) - time) * 1000;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("< Step", GUILayout.Width(64))) NudgeSelectedNote(-1);
                if (GUILayout.Button("Step >", GUILayout.Width(64))) NudgeSelectedNote(1);
                GUI.enabled = enabled && BeatSnapDivisor > 0;
                if (GUILayout.Button("Snap Q")) SnapSelectedNote();
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();
                GUILayout.Label("Snap correction: " + (BeatSnapDivisor > 0 ? snapMs.ToString("+0.0;-0.0;0.0") + " ms" : "OFF") + " / Set exact bypasses snap", smallStyle);
                if (GUILayout.Button("Delete selected")) DeleteSelection();
            }
            else GUILayout.Label(chart.notes.Length + " notes / click a note to edit its exact beat", smallStyle);
            if (GUILayout.Button("Select nearest on target path"))
            {
                int nearest = -1; double distance = double.PositiveInfinity;
                for (int i = 0; i < chart.notes.Length; i++)
                {
                    var note = chart.notes[i]; if (note.pathId != chart.paths[selectedPath].id) continue;
                    double gap = Math.Abs(tempo.SecondsAtBeat(note.tick / (double)chart.ticksPerBeat) - songTime);
                    if (gap < distance) { distance = gap; nearest = i; }
                }
                if (nearest >= 0) { selectedNote = nearest; RetimeSelectedNote(chart.notes[nearest].tick, false, "Selected"); }
            }
            GUILayout.Space(6); GUILayout.Label("New note / target path", headingStyle);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < chart.paths.Length; i++)
                if (GUILayout.Button(chart.paths[i].id, i == selectedPath ? selectedButtonStyle : buttonStyle)) selectedPath = i;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("TAP", !noteDrag ? selectedButtonStyle : buttonStyle)) noteDrag = false;
            if (GUILayout.Button("DRAG", noteDrag ? selectedButtonStyle : buttonStyle)) noteDrag = true;
            if (GUILayout.Button(noteProtected ? "PROTECT" : "Normal", noteProtected ? selectedButtonStyle : buttonStyle)) noteProtected = !noteProtected;
            GUILayout.EndHorizontal();
            int addTick = ClampNoteTick(SnapTimelineTick(songTime), true);
            GUI.enabled = enabled && CurrentBeat < chart.endBeat;
            if (GUILayout.Button("+ Note at B " + (addTick / (double)chart.ticksPerBeat).ToString("0.###") + "   N", selectedButtonStyle, GUILayout.Height(32))) AddNote();
            GUI.enabled = enabled;
            GUILayout.Space(6);
            DrawTimingCalibration();
            GUILayout.Label("Arrows: step playhead / Shift: whole beat\nAlt+arrows: move selected note\nAlt+Shift+arrows: one tick\nCtrl+wheel: zoom / Snap OFF: free timing", smallStyle);
        }

        int TempoIndexAtPlayhead()
        {
            int index = 0;
            double tick = tempo.BeatAtSeconds(songTime) * chart.ticksPerBeat;
            while (index + 1 < chart.tempos.Length && chart.tempos[index + 1].tick <= tick + .001) index++;
            return index;
        }

        bool ApplyTimingCalibration(int index, float bpm, float offsetSeconds)
        {
            if (index < 0 || index >= chart.tempos.Length || float.IsNaN(bpm) || float.IsInfinity(bpm) || bpm < 1 || bpm > 1000 || float.IsNaN(offsetSeconds) || float.IsInfinity(offsetSeconds)) return false;
            if (chart.tempos[index].bpm == bpm && chart.audioOffsetSeconds == offsetSeconds) return true;
            SetPlaying(false);
            Change(() => {
                float delta = offsetSeconds - chart.audioOffsetSeconds;
                chart.tempos[index].bpm = bpm; chart.audioOffsetSeconds = offsetSeconds;
                // Media time = chart time + audio offset, but video uses minus
                // video offset. Keep the existing A/V relationship when calibrating.
                if (chart.videoBga != null) chart.videoBga.timeOffsetSeconds -= delta;
            }, "Timing calibrated (undoable). Notes and anchors retain their ticks; media stays synchronized.");
            return true;
        }

        void DrawTimingCalibration()
        {
            var entry = chart.tempos[TempoIndexAtPlayhead()];
            if (GUILayout.Button((showTimingCalibration ? "-" : "+") + " Timing / " + entry.bpm.ToString("0.###") + " BPM")) showTimingCalibration = !showTimingCalibration;
            if (!showTimingCalibration) return;
            if (!ReferenceEquals(timingTempo, entry) || timingOriginalBpm != entry.bpm || timingOriginalOffset != chart.audioOffsetSeconds)
            {
                timingTempo = entry; timingBpm = timingOriginalBpm = entry.bpm;
                timingOriginalOffset = chart.audioOffsetSeconds; timingOffsetMs = timingOriginalOffset * 1000;
            }
            GUILayout.Label("Tempo segment starts at B " + (entry.tick / (double)chart.ticksPerBeat).ToString("0.###") + ". Applies to this existing segment, not a new tempo key.", smallStyle);
            FloatField("BPM", ref timingBpm, false);
            FloatField("Audio ms", ref timingOffsetMs, false);
            GUILayout.Label("Audio ms: audio position at chart beat 0. Positive skips the intro; negative delays audio. Video offset follows to keep A/V synchronized. BPM changes also retime path/camera keys. No changes until Apply.", smallStyle);
            if (GUILayout.Button("Apply timing calibration"))
                if (!ApplyTimingCalibration(TempoIndexAtPlayhead(), timingBpm, timingOffsetMs / 1000)) SetStatus("BPM must be 1..1000; audio offset must be finite");
        }

        float WaveformPeakAt(double chartStart, double chartEnd)
        {
            var clip = audioSource == null ? null : audioSource.clip;
            if (clip == null || timelineWaveform == null || clip.length <= 0) return -1;
            double start = chartStart + chart.audioOffsetSeconds, end = chartEnd + chart.audioOffsetSeconds;
            if (end < 0 || start >= clip.length) return -1;
            int a = Mathf.Clamp((int)Math.Floor(start / clip.length * timelineWaveform.Length), 0, timelineWaveform.Length - 1);
            int b = Mathf.Clamp((int)Math.Floor(end / clip.length * timelineWaveform.Length), a, timelineWaveform.Length - 1);
            float peak = 0;
            for (int i = a; i <= b; i++) peak = Mathf.Max(peak, timelineWaveform[i]);
            return peak;
        }

        struct BeatGridLine { public double beat; public float x; public bool whole, group; }
        readonly List<BeatGridLine> beatGrid = new List<BeatGridLine>();
        TempoMap beatGridTempo;
        double beatGridStart = -1, beatGridSpan;
        float beatGridWidth;
        int beatGridDivisor = -1;

        void BuildVisibleBeatGrid(Rect canvas)
        {
            if (beatGridTempo == tempo && beatGridStart == timelineStart && beatGridSpan == TimelineSpan && beatGridWidth == canvas.width && beatGridDivisor == BeatSnapDivisor) return;
            beatGridTempo = tempo; beatGridStart = timelineStart; beatGridSpan = TimelineSpan; beatGridWidth = canvas.width; beatGridDivisor = BeatSnapDivisor;
            beatGrid.Clear();
            double first = tempo.BeatAtSeconds(timelineStart), last = tempo.BeatAtSeconds(timelineStart + TimelineSpan);
            double pixelsPerBeat = canvas.width / Math.Max(.001, last - first);
            int divisor = Math.Max(1, BeatSnapDivisor);
            double step = pixelsPerBeat / divisor >= 7 ? 1.0 / divisor : 1;
            while (pixelsPerBeat * step < 7 || (last - first) / step > 4096) step *= 2;
            long begin = (long)Math.Ceiling((first - 1e-7) / step), end = (long)Math.Floor((last + 1e-7) / step);
            for (long i = begin; i <= end; i++)
            {
                double beat = i * step;
                int tick = (int)Math.Round(beat * chart.ticksPerBeat, MidpointRounding.AwayFromZero);
                beatGrid.Add(new BeatGridLine { beat = beat, x = TimelineX(tempo.SecondsAtBeat(tick / (double)chart.ticksPerBeat)) - canvas.x,
                    whole = Math.Abs(beat - Math.Round(beat)) < 1e-6, group = Math.Abs(beat / 4 - Math.Round(beat / 4)) < 1e-6 });
            }
        }

        void DrawBeatRuler(Rect canvas)
        {
            BuildVisibleBeatGrid(canvas);
            GUI.BeginGroup(new Rect(canvas.x, canvas.y - 30, canvas.width, 30));
            float lastLabel = -90;
            foreach (var line in beatGrid)
            {
                TimelineBox(new Rect(line.x, line.whole ? 0 : 22, 1, line.whole ? 30 : 8), new Color(.3f, .38f, .43f));
                if (line.x - lastLabel < 72 || (!line.whole && TimelineSpan > 2)) continue;
                GUI.Label(new Rect(line.x + 3, 0, 80, 14), "B " + line.beat.ToString("0.###"), timelineSmall);
                GUI.Label(new Rect(line.x + 3, 14, 80, 14), FormatTimelineTime(tempo.SecondsAtBeat(line.beat)), timelineSmall);
                lastLabel = line.x;
            }
            float head = TimelineX(songTime) - canvas.x;
            TimelineBox(new Rect(head - 4, 0, 8, 8), Color.white);
            TimelineBox(new Rect(head - .7f, 0, 1.4f, 30), Color.white);
            GUI.EndGroup();
        }

        void DrawBeatGrid(float y, Rect canvas)
        {
            BuildVisibleBeatGrid(canvas);
            foreach (var line in beatGrid)
                TimelineBox(new Rect(line.x, y, line.group ? 1.5f : 1, TimelineRowHeight), line.group ? new Color(.32f, .39f, .43f) :
                    line.whole ? new Color(.22f, .27f, .31f) : new Color(.14f, .18f, .21f));
        }

        void DrawNoteInsertionGuide(float y, Rect canvas)
        {
            Vector2 mouse = Event.current.mousePosition;
            if (playing || timelineDragItem != null || timelineScrubbing || WorkspaceInputBlocked || mouse.x < 0 || mouse.x >= canvas.width || mouse.y < y || mouse.y >= y + TimelineRowHeight) return;
            int tick = ClampNoteTick(SnapTimelineTick(TimelineTimeAt(mouse.x + canvas.x)), true);
            double beat = tick / (double)chart.ticksPerBeat;
            float x = TimelineX(tempo.SecondsAtBeat(beat)) - canvas.x;
            TimelineBox(new Rect(x, y + 2, 1, TimelineRowHeight - 4), new Color(1, .87f, .35f, .9f));
            TimelineBox(new Rect(x - 4, y + 14, 8, 16), new Color(1, .87f, .35f, .4f));
            GUI.Label(new Rect(Mathf.Clamp(x + 12, 2, canvas.width - 118), y, 116, 14), "B " + beat.ToString("0.###"), timelineSmall);
        }

        bool metronomeEnabled;
        AudioClip metronomeClip;
        readonly List<AudioSource> metronomeSources = new List<AudioSource>();
        int metronomeNextBeat, metronomeVoice, metronomeScheduledCount;
        double MetronomeDspTime(int beat) => transportDspStart + tempo.SecondsAtBeat(beat) - transportSongStart;

        void StopMetronome()
        {
            foreach (var source in metronomeSources) if (source != null) source.Stop();
        }

        void ResetMetronome()
        {
            StopMetronome();
            metronomeNextBeat = (int)Math.Ceiling(tempo.BeatAtSeconds(songTime) - 1e-7);
        }

        void ToggleMetronome()
        {
            metronomeEnabled = !metronomeEnabled; ResetMetronome();
            SetStatus(metronomeEnabled ? "Metronome ON: Space to listen (whole beats; accent every 4 beats)" : "Metronome OFF");
        }

        void EnsureMetronome()
        {
            if (metronomeClip != null) return;
            const int rate = 48000, samples = 1440;
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
                data[i] = (float)(Math.Sin(2 * Math.PI * 1400 * i / rate) * Math.Exp(-i / 200.0) * Math.Min(1, i / 24.0) * .65);
            metronomeClip = AudioClip.Create("Editor beat click (not exported)", samples, 1, rate, false); metronomeClip.SetData(data, 0);
            for (int i = 0; i < 8; i++)
            {
                var go = new GameObject("Editor metronome " + i); go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0;
                source.clip = metronomeClip; source.volume = .55f; metronomeSources.Add(source);
            }
        }

        void UpdateMetronome()
        {
            if (!playing || !metronomeEnabled) return;
            EnsureMetronome();
            double now = AudioSettings.dspTime;
            // Skip missed beats after a render stall, never play a burst of late clicks.
            double currentTime = transportSongStart + Math.Max(0, now - transportDspStart);
            metronomeNextBeat = Math.Max(metronomeNextBeat, (int)Math.Ceiling(tempo.BeatAtSeconds(currentTime) - 1e-7));
            for (int count = 0; count < 8 && metronomeNextBeat < chart.endBeat; count++)
            {
                double dsp = MetronomeDspTime(metronomeNextBeat);
                if (dsp > now + .12) break;
                if (dsp >= now + .002)
                {
                    var source = metronomeSources[metronomeVoice++ % metronomeSources.Count];
                    source.pitch = metronomeNextBeat % 4 == 0 ? 1.35f : 1;
                    source.PlayScheduled(dsp); metronomeScheduledCount++;
                }
                metronomeNextBeat++;
            }
        }
    }
}
