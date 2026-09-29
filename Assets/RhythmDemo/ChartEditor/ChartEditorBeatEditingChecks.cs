using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        bool beatChecksPassed;

        IEnumerator RunBeatEditingChecks()
        {
            beatChecksPassed = true;
            var report = new List<string>();
            Action<bool, string> check = (ok, name) => { beatChecksPassed &= ok; report.Add((ok ? "PASS " : "FAIL ") + name); };
            float deadline = Time.realtimeSinceStartup + 40;
            while ((videoLoading || !realAudioLoaded) && Time.realtimeSinceStartup < deadline) yield return null;
            check(VideoSpaceEditing && videoBga?.IsLoaded == true && realAudioLoaded, "Real H3 video and separate soundtrack load");
            if (!VideoSpaceEditing || !realAudioLoaded)
            { File.WriteAllLines(Path.Combine(smokeDirectory, "beat-editing-checks.txt"), report); yield break; }
            SetPlaying(false);
            string original = ChartSnapshotForUndo(), originalFile = filePath;
            float originalHeight = timelineHeight;
            try
            {
                chart.notes = new NoteData[0]; undo.Clear(); redo.Clear(); selectedNote = -1;
                SetMode(ChartEditMode.Notes); selectedPath = 0; timelineSnap = 2;
                Rebuild();
                var rows = TimelineRows();
                check(rows[0].kind == TimelineKind.Audio && rows[1].kind == TimelineKind.Note, "Notes immediately follow waveform");
                check(rows.FindAll(r => r.kind == TimelineKind.Note).Count == chart.paths.Length &&
                    rows.FindAll(r => r.kind == TimelineKind.ScreenAnchor).Count == chart.paths.Length && rows.Exists(r => r.kind == TimelineKind.VideoZ),
                    "All Note, Path XY and Camera Z tracks remain available");
                check(timelineWaveform.Length >= 19000, "20s waveform uses millisecond peak bins, not 1024-bin thumbnail");
                int resolution = chart.ticksPerBeat;
                for (int i = 0; i < BeatSnapDivisors.Length; i++)
                {
                    timelineSnap = i;
                    int divisor = BeatSnapDivisors[i];
                    double beat = 4.314;
                    double expectedBeat = divisor == 0 ? beat : Math.Round(beat * divisor, MidpointRounding.AwayFromZero) / divisor;
                    int expected = (int)Math.Round(expectedBeat * resolution, MidpointRounding.AwayFromZero);
                    check(SnapTimelineTick(tempo.SecondsAtBeat(beat)) == expected, "Snap " + BeatSnapLabel + " uses exact musical grid");
                }
                chart.ticksPerBeat = 100; timelineSnap = 6;
                check(SnapBeatTick(100.333333) == 10033 && SnapBeatTick(101) == 10100, "Triplets do not accumulate integer-PPQ drift");
                check(NextStepTick(100.33, 1, false) == 10067 && NextStepTick(100.33, -1, false) == 10000,
                    "Triplet stepping tolerates unavoidable tick rounding");
                chart.ticksPerBeat = resolution; timelineSnap = 2;

                Seek(tempo.SecondsAtBeat(4.31)); AddNote();
                check(SelectedTimingNote.tick == 2040 && Math.Abs(CurrentBeat - 4.25) < .00001, "Toolbar / inspector Add Note snaps playhead and note to quarter beat");
                string noteId = SelectedTimingNote.id;
                int undoCount = undo.Count; AddNote();
                check(chart.notes.Length == 1 && SelectedTimingNote.id == noteId && undo.Count == undoCount, "Duplicate add selects existing note without mutation or undo entry");
                Undo(); check(chart.notes.Length == 0, "Undo snapped insertion");
                Redo(); check(chart.notes.Length == 1 && chart.notes[0].tick == 2040, "Redo retains exact tick");
                selectedNote = 0;
                Seek(tempo.SecondsAtBeat(4.69));
                ReadWorkspaceKeys(new Event { type = EventType.KeyDown, keyCode = KeyCode.N });
                check(SelectedTimingNote.tick == 2280 && chart.notes.Length == 2, "N inserts at the same snapped beat as mouse / toolbar");
                ReadWorkspaceKeys(new Event { type = EventType.KeyDown, keyCode = KeyCode.LeftArrow, modifiers = EventModifiers.Alt });
                check(SelectedTimingNote.tick == 2160, "Alt+left retimes selected note by one grid step");
                RetimeSelectedNote(2117, false, "test exact timing");
                check(SelectedTimingNote.tick == 2117, "Exact timing bypasses snap intentionally");
                SnapSelectedNote();
                check(SelectedTimingNote.tick == 2040 + 120, "Q rounds selected off-grid note to nearest free grid point");
                check(!RetimeSelectedNote(2040, true, "collision") && SelectedTimingNote.tick == 2160, "Retime refuses same-path same-tick collision");
                NudgeSelectedNote(1, true);
                check(SelectedTimingNote.tick == 2161, "Single-tick fine nudge bypasses coarse grid");
                ReadWorkspaceKeys(new Event { type = EventType.KeyDown, keyCode = KeyCode.Q });
                check(SelectedTimingNote.tick == 2160, "Q shortcut quantizes selected note");
                selectedPath = 1; Seek(tempo.SecondsAtBeat(4.25)); AddNote();
                check(chart.notes.Length == 3 && SelectedTimingNote.tick == 2040 && SelectedTimingNote.pathId == chart.paths[1].id,
                    "Simultaneous notes on different paths remain allowed");
                selectedPath = 0; Seek(tempo.SecondsAtBeat(chart.endBeat) - .001); AddNote();
                check(SelectedTimingNote.tick < chart.endBeat * resolution && SelectedTimingNote.tick % 120 == 0,
                    "Near-end insertion stays on last valid beat grid, never end-minus-one tick");
                int countAtEnd = chart.notes.Length; Seek(Duration); AddNote();
                check(chart.notes.Length == countAtEnd, "Add at end remains disabled");

                timelineZoom = 4; timelineStart = 0;
                var canvas = TimelineCanvas;
                double rawTime = tempo.SecondsAtBeat(5.31);
                ReadTimelinePointer(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(TimelineX(rawTime), canvas.y - 10) * uiScale });
                check(timelineScrubbing && Math.Abs(CurrentBeat - 5.25) < .00001, "Ruler pointer snaps to same beat grid");
                ReadTimelinePointer(new Event { type = EventType.MouseDrag, button = 0, mousePosition = new Vector2(TimelineX(tempo.SecondsAtBeat(6.33)), canvas.y - 10) * uiScale });
                check(Math.Abs(CurrentBeat - 6.25) < .00001, "Ruler drag remains beat-snapped");
                CancelTimelineGesture();
                timelineSnap = 3;
                check(Math.Abs(TimelineSeekTime(rawTime) - rawTime) < 1e-9, "Snap OFF allows free waveform / ruler timing");
                timelineSnap = 2;
                Seek(rawTime); StepPlayhead(1, false);
                check(Math.Abs(CurrentBeat - 5.5) < .00001, "Step from off-grid seeks next grid point, not arbitrary time plus step");
                StepPlayhead(-1, true);
                check(Math.Abs(CurrentBeat - 5) < .00001, "Whole-beat stepping lands on integer beat");
                var noteRow = new TimelineRow { kind = TimelineKind.Note, path = 0 };
                ApplyTimelineClick(noteRow, null, tempo.SecondsAtBeat(6.31), TimelineX(tempo.SecondsAtBeat(6.31)), 0);
                check(SelectedTimingNote.tick == 3000, "Empty note-lane click inserts on beat 6.25");
                var dragged = new List<TimelineItem>(TimelineItems(noteRow)).Find(item => ItemTick(item) == 3000);
                undoCount = undo.Count;
                BeginTimelineDrag(dragged, dragged.time + .01);
                ApplyTimelinePointerTime(TimelineX(tempo.SecondsAtBeat(7.31) + .01));
                ApplyTimelinePointerTime(TimelineX(tempo.SecondsAtBeat(8.29) + .01));
                check(SelectedTimingNote.tick == 3960 && undo.Count == undoCount + 1, "Continuous pointer drag snaps, retains grab offset and makes one undo");
                CancelTimelineGesture(); Undo();
                check(Array.Exists(chart.notes, n => n.pathId == chart.paths[0].id && n.tick == 3000), "Drag undo restores starting beat");
                Redo();

                // Waveform offset and peak aggregation are independent of signal content.
                float[] wave = timelineWaveform; float offset = chart.audioOffsetSeconds;
                timelineWaveform = new float[100]; timelineWaveform[10] = .75f; timelineWaveform[12] = 1;
                double clipLength = audioSource.clip.length;
                chart.audioOffsetSeconds = (float)(clipLength * .105);
                check(Math.Abs(WaveformPeakAt(0, .001) - .75f) < .0001, "Waveform follows positive audio offset used by playback");
                chart.audioOffsetSeconds = -.5f;
                check(WaveformPeakAt(0, .01) < 0, "Negative audio offset displays leading silence");
                chart.audioOffsetSeconds = 0;
                check(WaveformPeakAt(clipLength * .10, clipLength * .13) == 1, "Zoomed-out waveform preserves peaks across the whole pixel interval");
                timelineWaveform = wave; chart.audioOffsetSeconds = offset;

                string timingBefore = JsonUtility.ToJson(chart);
                string pathsBefore = JsonUtility.ToJson(chart.paths[0]), cameraBefore = JsonUtility.ToJson(chart.videoSpace);
                float avBefore = chart.videoBga.timeOffsetSeconds + chart.audioOffsetSeconds;
                check(ApplyTimingCalibration(0, 174, .125f), "Explicit BPM / offset calibration applies");
                check(chart.tempos[0].bpm == 174 && chart.audioOffsetSeconds == .125f &&
                    Math.Abs(chart.videoBga.timeOffsetSeconds + chart.audioOffsetSeconds - avBefore) < .000001, "Calibration preserves relative audio / video alignment");
                check(JsonUtility.ToJson(chart.paths[0]) == pathsBefore && JsonUtility.ToJson(chart.videoSpace) == cameraBefore,
                    "Calibration does not rewrite Path XY, Camera Z or their key ticks");
                Undo(); check(JsonUtility.ToJson(chart) == timingBefore, "BPM and both media offsets undo together exactly");
                check(!ApplyTimingCalibration(0, 0, 0) && !ApplyTimingCalibration(0, float.NaN, 0) && !ApplyTimingCalibration(0, 180, float.PositiveInfinity),
                    "Invalid timing input cannot mutate chart");

                var savedTempos = chart.tempos;
                chart.tempos = new[] { new TempoData { tick = 0, bpm = 120 }, new TempoData { tick = 1920, bpm = 180 } }; Rebuild();
                check(SnapTimelineTick(tempo.SecondsAtBeat(4.31)) == 2040 && Math.Abs(tempo.SecondsAtBeat(4.25) - 2.083333333) < 1e-6,
                    "Beat snapping honors variable-tempo segments");
                timelineStart = 0; timelineZoom = 2;
                BuildVisibleBeatGrid(TimelineCanvas);
                var lineAt4 = beatGrid.Find(l => Math.Abs(l.beat - 4) < .000001);
                check(Math.Abs(lineAt4.x - (TimelineX(2) - TimelineCanvas.x)) < .01, "Visible beat grid uses tempo mapping, not uniformly spaced seconds");
                chart.tempos = savedTempos; Rebuild();

                Seek(tempo.SecondsAtBeat(8));
                metronomeEnabled = true; int clicks = metronomeScheduledCount;
                SetPlaying(true);
                check(Math.Abs(MetronomeDspTime(9) - transportDspStart - 1.0 / 3) < .000001, "Metronome schedules against shared audio DSP transport");
                yield return new WaitForSecondsRealtime(.85f);
                check(metronomeScheduledCount >= clicks + 2 && songTime > tempo.SecondsAtBeat(10), "Playback schedules beat clicks while song/video advance");
                Seek(tempo.SecondsAtBeat(20));
                check(metronomeNextBeat == 20 && Math.Abs(MetronomeDspTime(20) - transportDspStart) < .000001, "Playback seek clears scheduled clicks and resets metronome phase");
                SetPlaying(false); yield return null;
                check(metronomeSources.TrueForAll(source => !source.isPlaying), "Pause stops all scheduled click voices");
                metronomeEnabled = false;

                ChartLoader.Parse(JsonUtility.ToJson(chart)); check(true, "Edited chart validates with existing package schema");
                string authoredNotes = JsonUtility.ToJson(chart);
                string package = Path.Combine(smokeDirectory, "BeatEditing-roundtrip.grchart");
                check(TryExportChartPackage(package) && TryLoadChartOrPackage(package), "Portable package exports and reopens with beat-aligned notes");
                var expectedChart = JsonUtility.FromJson<ChartData>(authoredNotes);
                check(chart.notes.Length == expectedChart.notes.Length && Array.TrueForAll(chart.notes,
                    n => Array.Exists(expectedChart.notes, e => e.id == n.id && e.tick == n.tick && e.pathId == n.pathId)), "Package preserves exact note ticks, IDs and paths");
                deadline = Time.realtimeSinceStartup + 35;
                while (videoLoading && Time.realtimeSinceStartup < deadline) yield return null;
                SetMode(ChartEditMode.Notes); timelineSnap = 2; timelineZoom = 4; timelineStart = 0; timelineTrackScroll = 0;
                selectedNote = Array.FindIndex(chart.notes, n => n.pathId == chart.paths[0].id && n.tick == 3960);
                selectedPath = 0; Seek(2.75); statusUntil = 0;
                yield return WaitForVideoPosition(2.75);
                yield return CaptureCheckScreenshot("beat-editing-notes.png");
                showTimingCalibration = true; inspectorScroll = new Vector2(0, 1000);
                yield return CaptureCheckScreenshot("beat-editing-calibration.png");
                showTimingCalibration = false;
                check(sceneCamera.transform.rotation == Quaternion.identity && Math.Abs(sceneCamera.fieldOfView - 53) < .001 &&
                    sceneCamera.transform.position.x == 0 && sceneCamera.transform.position.y == 0, "Beat editing keeps fixed video-camera pose and FOV");
            }
            finally
            {
                SetPlaying(false); metronomeEnabled = false; CancelTimelineGesture();
                chart = JsonUtility.FromJson<ChartData>(original); filePath = originalFile; undo.Clear(); redo.Clear(); selectedNote = -1;
                savedChartJson = JsonUtility.ToJson(chart); needsSaveAs = true;
                timelineHeight = originalHeight; timelineSnap = 2; timelineZoom = 1; timelineStart = 0; inspectorScroll = Vector2.zero;
                Rebuild(); RefreshBgaBinding(); SetMode(ChartEditMode.Notes); Seek(0);
            }
            report.Add("RESULT=" + (beatChecksPassed ? "PASS" : "FAIL") + " CHECKS=" + report.Count);
            File.WriteAllLines(Path.Combine(smokeDirectory, "beat-editing-checks.txt"), report);
            Debug.Log("BEAT_EDITING_" + (beatChecksPassed ? "PASS" : "FAIL") + "\n" + string.Join("\n", report));
        }
    }
}
