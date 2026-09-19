using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        bool RunTimelineChecks()
        {
            string snapshot = JsonUtility.ToJson(chart); float savedHeight = timelineHeight;
            var report = new StringBuilder(); int count = 0; bool passed = false;
            Action<bool, string> check = (condition, name) =>
            {
                if (!condition) throw new InvalidOperationException(name);
                count++; report.AppendLine("PASS " + name);
            };
            try
            {
                NewChart(); ReleaseMouse(); flyMode = false; showGuide = showSettings = false;
                timelineHeight = 310; timelineZoom = 1; timelineStart = 0; timelineTrackScroll = 0;
                check(ClampTimelineHeight(-100, 720) == 190, "Resize minimum remains usable");
                check(ClampTimelineHeight(900, 720) == 512, "Resize maximum preserves 3D viewport");
                check(ClampTimelineHeight(900, 420) <= 212, "Resize adapts to small windows and UI scaling");
                SetMode(ChartEditMode.Stage);
                check(TimelineRows().Count == 5, "Stage: audio, points and XYZ curves");
                var stage = new List<TimelineItem>(TimelineItems(new TimelineRow { kind = TimelineKind.Stage }));
                check(stage.Count == chart.stagePath.points.Length, "Every stage point has a timeline marker");
                check(stage[stage.Count - 1].time > Duration && TimelineDuration >= stage[stage.Count - 1].time,
                    "Stage post-song route tail remains selectable");
                for (int i = 0; i < stage.Count; i++)
                {
                    var p = chart.stagePath.points[i];
                    check(Vector3.Distance(spatial.RouteAt((float)stage[i].time * spatial.UnitsPerSecond), new Vector3(p.x, p.y, p.z)) < .003f,
                        "Stage arc-length marker matches control point " + i);
                }
                SetMode(ChartEditMode.Camera);
                check(TimelineRows().Count == 3, "Camera: audio, keys and FOV");
                Seek(tempo.SecondsAtBeat(16)); AddCameraKey(); Seek(tempo.SecondsAtBeat(48)); AddCameraKey();
                var keys = new List<TimelineItem>(TimelineItems(new TimelineRow { kind = TimelineKind.Camera }));
                BeginTimelineDrag(keys[1], keys[1].time);
                int before = undo.Count;
                MoveTimelineItem(keys[1], 20 * chart.ticksPerBeat); MoveTimelineItem(keys[1], 24 * chart.ticksPerBeat);
                check(chart.cameraKeys[1].beat == 24 && undo.Count == before + 1, "Camera continuous drag has one undo");
                CancelTimelineGesture(); Undo();
                check(chart.cameraKeys[1].beat == 16, "Camera timing undo");
                Redo(); check(chart.cameraKeys[1].beat == 24, "Camera timing redo");
                keys = new List<TimelineItem>(TimelineItems(new TimelineRow { kind = TimelineKind.Camera }));
                check(!MoveTimelineItem(keys[0], 8 * chart.ticksPerBeat) && chart.cameraKeys[0].beat == 0, "Camera key at zero cannot move");
                BeginTimelineDrag(keys[1], keys[1].time); MoveTimelineItem(keys[1], 60 * chart.ticksPerBeat);
                check(chart.cameraKeys[1].beat < chart.cameraKeys[2].beat, "Camera drag cannot cross next key");
                CancelTimelineGesture();
                SetMode(ChartEditMode.Paths);
                check(TimelineRows().Count == chart.paths.Length + 2, "Paths: layout clips and per-path offset lanes");
                Seek(tempo.SecondsAtBeat(32)); AddSection(); Seek(tempo.SecondsAtBeat(64)); AddSection();
                var sections = new List<TimelineItem>(TimelineItems(new TimelineRow { kind = TimelineKind.Section }));
                check(sections.Count == 3 && sections[0].end == sections[1].time && sections[1].end == sections[2].time,
                    "Layout clips cover contiguous section intervals");
                BeginTimelineDrag(sections[1], sections[1].time); MoveTimelineItem(sections[1], 40 * chart.ticksPerBeat);
                check(chart.sections[1].startBeat == 40 && chart.sections[2].startBeat == 64, "Clip left edge edits only its boundary");
                CancelTimelineGesture();
                check(!MoveTimelineItem(sections[0], 4 * chart.ticksPerBeat), "First layout section stays at zero");
                Seek(tempo.SecondsAtBeat(16)); EnsureOffsetKey(PlayheadTick); Seek(tempo.SecondsAtBeat(48)); EnsureOffsetKey(PlayheadTick);
                var offsets = new List<TimelineItem>(TimelineItems(new TimelineRow { kind = TimelineKind.Offset, path = 0 }));
                var offset = offsets.Find(k => ItemTick(k) == 16 * chart.ticksPerBeat);
                BeginTimelineDrag(offset, offset.time); MoveTimelineItem(offset, 20 * chart.ticksPerBeat);
                check(((PathOffsetKey)offset.data).tick == 20 * chart.ticksPerBeat && selectedPath == 0, "Offset key can be retimed on its lane");
                CancelTimelineGesture();
                SetMode(ChartEditMode.Notes);
                check(TimelineRows().Count == chart.paths.Length + 1, "Notes: separate lanes for each path");
                Seek(tempo.SecondsAtBeat(8)); AddNote(); Seek(tempo.SecondsAtBeat(16)); AddNote();
                var notes = new List<TimelineItem>(TimelineItems(new TimelineRow { kind = TimelineKind.Note, path = 0 }));
                string movedID = ((NoteData)notes[0].data).id;
                BeginTimelineDrag(notes[0], notes[0].time); MoveTimelineItem(notes[0], 32 * chart.ticksPerBeat);
                check(chart.notes[0].tick < chart.notes[1].tick && chart.notes[selectedNote].id == movedID,
                    "Note drag reorders time but preserves identity and selection");
                MoveTimelineItem(notes[0], int.MaxValue);
                check(chart.notes[selectedNote].tick < chart.endBeat * chart.ticksPerBeat, "Notes cannot move outside song");
                CancelTimelineGesture();
                timelineZoom = 1; timelineStart = 0; timelineTrackScroll = 0;
                int noteCount = chart.notes.Length;
                double clickTime = tempo.SecondsAtBeat(24);
                var noteRow = new TimelineRow { kind = TimelineKind.Note, path = 0 };
                ApplyTimelineClick(noteRow, null, clickTime, TimelineX(clickTime), 0);
                check(chart.notes.Length == noteCount + 1 && chart.notes[selectedNote].tick == 24 * chart.ticksPerBeat,
                    "Single left click on empty note lane adds a note");
                var addedNote = new List<TimelineItem>(TimelineItems(noteRow)).Find(item => ItemTick(item) == 24 * chart.ticksPerBeat);
                ApplyTimelineClick(noteRow, addedNote, addedNote.time, TimelineX(addedNote.time), 1);
                check(chart.notes.Length == noteCount, "Single right click on a note deletes it");
                timelineZoom = 4; timelineStart = 8;
                double time = timelineStart + TimelineSpan * .37;
                check(Math.Abs(TimelineTimeAt(TimelineX(time)) - time) < .0001, "Zoomed/panned timeline coordinates round-trip");
                timelineSnap = 2;
                check(SnapTimelineTick(tempo.SecondsAtBeat(4.31)) == Mathf.RoundToInt(4.25f * chart.ticksPerBeat), "Quarter-beat snapping");
                timelineSnap = 3;
                check(Math.Abs(SnapTimelineTick(tempo.SecondsAtBeat(4.31)) - 4.31 * chart.ticksPerBeat) <= .5, "Snap OFF retains tick precision");
                timelineZoom = 1; timelineStart = 0; timelineTrackScroll = 0;
                var canvas = TimelineCanvas;
                ReadTimelinePointer(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(TimelineX(12), canvas.y - 10) * uiScale });
                check(timelineScrubbing && Math.Abs(songTime - 12) < .001, "Ruler pointer event seeks playhead");
                ReadTimelinePointer(new Event { type = EventType.MouseDrag, button = 0, mousePosition = new Vector2(TimelineX(18), canvas.y - 10) * uiScale });
                check(Math.Abs(songTime - 18) < .001, "Playhead drag seeks continuously");
                ReadTimelinePointer(new Event { type = EventType.MouseUp, button = 0, mousePosition = Vector2.zero });
                check(!timelineScrubbing && timelineDragItem == null, "Release outside timeline clears drag ownership");
                check(!PointerInViewport(new Vector2(canvas.x + 20, canvas.y + 20) * uiScale), "Timeline cannot trigger 3D gizmo editing");
                timelineHeight = 220; UpdateViewportRect(); float oldBottom = sceneCamera.pixelRect.y;
                timelineHeight = 340; UpdateViewportRect();
                check(sceneCamera.pixelRect.y > oldBottom, "Resizing also resizes the actual camera viewport");
                check(timelineWaveform != null && Array.Exists(timelineWaveform, v => v > 0), "Waveform comes from the loaded audio clip");
                ChartLoader.Parse(JsonUtility.ToJson(chart)); check(true, "Edited timeline serializes to a playable chart");
                passed = true;
            }
            catch (Exception error) { report.AppendLine("FAIL " + error); Debug.LogException(error); }
            finally
            {
                CancelTimelineGesture(); chart = JsonUtility.FromJson<ChartData>(snapshot); timelineHeight = savedHeight;
                timelineZoom = 1; timelineStart = timelineTrackScroll = 0; timelineSnap = 2;
                undo.Clear(); redo.Clear(); songTime = 0; Rebuild(); UpdateViewportRect();
            }
            report.AppendLine("RESULT=" + (passed ? "PASS" : "FAIL") + " CHECKS=" + count);
            File.WriteAllText(Path.Combine(smokeDirectory, "timeline-checks.txt"), report.ToString());
            Debug.Log("CHART_STUDIO_TIMELINE " + (passed ? "PASS" : "FAIL") + " " + count);
            return passed;
        }
        void PrepareTimelinePreview()
        {
            NewChart(); timelineHeight = 330; timelineStart = 0; timelineZoom = 1;
            foreach (int beat in new[] { 16, 32, 64, 96 })
            {
                Seek(tempo.SecondsAtBeat(beat)); AddCameraKey();
                if (beat == 32 || beat == 64 || beat == 96) AddSection();
                for (int p = 0; p < chart.paths.Length; p++)
                {
                    selectedPath = p; var key = EnsureOffsetKey(PlayheadTick); key.x = (p - 1) * 4 + Mathf.Sin(beat) * 2; key.y = p;
                }
            }
            var notes = new List<NoteData>();
            for (int i = 0; i < 48; i++) notes.Add(new NoteData { id = "preview-" + i, pathId = chart.paths[i % 3].id,
                tick = (i * 2 + 4) * chart.ticksPerBeat, action = i % 4 == 0 ? "drag" : "tap", protectedNote = i % 9 == 0 });
            chart.notes = notes.ToArray(); selectedPath = 0; selectedStagePoint = 1; selectedCameraKey = 1; selectedSection = 1;
            selectedNote = -1; songTime = tempo.SecondsAtBeat(24); undo.Clear(); redo.Clear(); Rebuild("Timeline preview test data (not saved)");
            SetMode(ChartEditMode.Stage); FocusSelection();
        }
    }
}
