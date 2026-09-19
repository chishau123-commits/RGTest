using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        enum TimelineKind { Audio, Stage, X, Y, Z, Camera, Fov, Section, Offset, Note, Effect, Motion }
        sealed class TimelineRow
        {
            public TimelineKind kind;
            public string label;
            public int path = -1;
        }
        sealed class TimelineItem
        {
            public TimelineKind kind;
            public int index, path;
            public object data;
            public double time, end;
            public string label;
        }
        const float TimelineHeaderWidth = 164;
        const float TimelineRowHeight = 44;
        const float TimelineHeaderHeight = 112;
        const float TimelineFooterHeight = 30;
        float timelineHeight = 310, timelineZoom = 1, timelineStart, timelineTrackScroll;
        bool timelineResizing, timelineScrubbing, timelineDragUndo;
        float timelineResizeMouse, timelineResizeHeight;
        Vector2 timelineGestureMouse;
        double timelineGrabTime;
        int timelineOriginalTick, timelineSnap = 2;
        TimelineItem timelineDragItem;
        TimelineKind timelineSelection = TimelineKind.Section;
        float[] timelineWaveform;
        float timelineWavePeak;
        GUIStyle timelineLabel, timelineSmall, timelineButtonStyle, timelineActiveButtonStyle;
        sealed class TimelineCurveCache
        {
            public Texture2D texture;
            public int revision = -1;
            public double start = -1, span;
        }
        readonly Dictionary<TimelineKind, TimelineCurveCache> timelineCurves = new Dictionary<TimelineKind, TimelineCurveCache>();
        int timelineCurveRevision;

        float TimelineHeight => ClampTimelineHeight(timelineHeight, ViewHeight);
        float TimelineTop => ViewHeight - TimelineHeight;
        double TimelineDuration => mode == ChartEditMode.Stage
            ? Math.Max(Duration, spatial.RouteLength / spatial.UnitsPerSecond) : Duration;
        double TimelineSpan => Math.Max(.001, TimelineDuration / timelineZoom);
        Rect TimelineCanvas => new Rect(TimelineHeaderWidth, TimelineTop + TimelineHeaderHeight,
            Mathf.Max(40, ViewWidth - TimelineHeaderWidth - 18), Mathf.Max(20, TimelineHeight - TimelineHeaderHeight - TimelineFooterHeight));
        static float ClampTimelineHeight(float value, float viewHeight)
        {
            float maximum = Mathf.Max(142, viewHeight - ToolbarHeight - 160);
            return Mathf.Clamp(value, Mathf.Min(190, maximum), maximum);
        }
        void CancelTimelineGesture()
        { timelineResizing = timelineScrubbing = false; timelineDragItem = null; }
        double TimelineTimeAt(float x)
            => timelineStart + Mathf.Clamp01((x - TimelineCanvas.x) / TimelineCanvas.width) * TimelineSpan;
        float TimelineX(double time) => TimelineCanvas.x + (float)((time - timelineStart) / TimelineSpan) * TimelineCanvas.width;
        int SnapTimelineTick(double time)
        {
            double tick = tempo.BeatAtSeconds(Math.Max(0, time)) * chart.ticksPerBeat;
            int grid = timelineSnap == 3 ? 1 : Math.Max(1, chart.ticksPerBeat / (1 << timelineSnap));
            return (int)Math.Round(tick / grid, MidpointRounding.AwayFromZero) * grid;
        }
        void FollowTimelinePlayhead(float deltaTime)
        {
            double margin = TimelineSpan * Math.Min(.1, 64 / TimelineCanvas.width);
            double target = timelineStart;
            if (songTime > timelineStart + TimelineSpan - margin) target = songTime - TimelineSpan + margin;
            else if (songTime < timelineStart + margin) target = songTime - margin;
            target = Math.Max(0, Math.Min(TimelineDuration - TimelineSpan, target));
            // Follow continuously instead of jumping the head back to 15% of the view.
            float speed = Mathf.Max(1.5f, (float)TimelineSpan * .45f);
            timelineStart = Mathf.MoveTowards(timelineStart, (float)target, speed * Mathf.Clamp(deltaTime, 0, .05f));
        }
        void UpdateTimelineEdgeScroll(float deltaTime)
        {
            if (WorkspaceInputBlocked || timelineResizing) return;
            if (!timelineScrubbing && timelineDragItem == null)
            { if (playing) FollowTimelinePlayhead(deltaTime); return; }
            if (Cursor.lockState == CursorLockMode.Locked) return;
            Rect canvas = TimelineCanvas;
            if (timelineGestureMouse.y < canvas.y - 36 || timelineGestureMouse.y > canvas.yMax + 16) return;
            float edge = Mathf.Min(40, canvas.width * .15f);
            float direction = timelineGestureMouse.x < canvas.x + edge ?
                -Mathf.Clamp01((canvas.x + edge - timelineGestureMouse.x) / edge) :
                Mathf.Clamp01((timelineGestureMouse.x - (canvas.xMax - edge)) / edge);
            if (Mathf.Abs(direction) < .001f) return;
            float pixelsPerSecond = Mathf.Clamp(canvas.width * .45f, 120, 480);
            double delta = direction * pixelsPerSecond / canvas.width * TimelineSpan * Mathf.Clamp(deltaTime, 0, .05f);
            // Route tails may extend past the song, but a dragged playhead must not scroll
            // past its own end time. Other timeline gestures retain their content range.
            double maximum = Math.Max(0, Math.Min(TimelineDuration, timelineScrubbing ? Duration : TimelineDuration) - TimelineSpan);
            float next = (float)Math.Max(0, Math.Min(maximum, timelineStart + delta));
            if (Mathf.Approximately(next, timelineStart)) return;
            timelineStart = next;
            ApplyTimelinePointerTime(timelineGestureMouse.x);
        }
        void ApplyTimelinePointerTime(float x)
        {
            if (timelineScrubbing) Seek(TimelineTimeAt(x), false);
            else if (timelineDragItem != null)
            {
                double time = tempo.SecondsAtBeat(timelineOriginalTick / (double)chart.ticksPerBeat) + TimelineTimeAt(x) - timelineGrabTime;
                MoveTimelineItem(timelineDragItem, SnapTimelineTick(time));
            }
        }
        void ZoomTimeline(float factor)
        {
            double anchor = Math.Max(timelineStart, Math.Min(timelineStart + TimelineSpan, songTime));
            ZoomTimelineAt(factor, (anchor - timelineStart) / TimelineSpan);
        }
        void ZoomTimelineAt(float factor, double fraction)
        {
            fraction = Math.Max(0, Math.Min(1, fraction));
            double anchor = timelineStart + fraction * TimelineSpan;
            timelineZoom = Mathf.Clamp(timelineZoom * factor, 1, 64);
            timelineStart = (float)Math.Max(0, Math.Min(TimelineDuration - TimelineSpan, anchor - fraction * TimelineSpan));
        }
        List<TimelineRow> TimelineRows()
        {
            var rows = new List<TimelineRow> { new TimelineRow { kind = TimelineKind.Audio, label = "DEMO AUDIO" } };
            if (mode == ChartEditMode.Stage)
            {
                rows.Add(new TimelineRow { kind = TimelineKind.Stage, label = "ROUTE POINTS" });
                rows.Add(new TimelineRow { kind = TimelineKind.X, label = "POSITION X" });
                rows.Add(new TimelineRow { kind = TimelineKind.Y, label = "HEIGHT Y" });
                rows.Add(new TimelineRow { kind = TimelineKind.Z, label = "POSITION Z" });
            }
            else if (mode == ChartEditMode.Camera)
            {
                rows.Add(new TimelineRow { kind = TimelineKind.Camera, label = "CAMERA KEYS" });
                rows.Add(new TimelineRow { kind = TimelineKind.Fov, label = "FIELD OF VIEW" });
            }
            else if (mode == ChartEditMode.Paths)
            {
                rows.Add(new TimelineRow { kind = TimelineKind.Section, label = "LAYOUT SECTIONS" });
                for (int i = 0; i < chart.paths.Length; i++)
                    rows.Add(new TimelineRow { kind = TimelineKind.Offset, label = chart.paths[i].id + "  /  OFFSET", path = i });
            }
            else if (mode == ChartEditMode.Notes)
                for (int i = 0; i < chart.paths.Length; i++)
                    rows.Add(new TimelineRow { kind = TimelineKind.Note, label = chart.paths[i].id + "  /  NOTES", path = i });
            else if (mode == ChartEditMode.Effects)
                rows.Add(new TimelineRow { kind = TimelineKind.Effect, label = "VISUAL EFFECT CLIPS" });
            else if (mode == ChartEditMode.CameraMotion)
                rows.Add(new TimelineRow { kind = TimelineKind.Motion, label = "CAMERA MOTION CLIPS" });
            return rows;
        }
        IEnumerable<TimelineItem> TimelineItems(TimelineRow row)
        {
            if (row.kind == TimelineKind.Stage)
                for (int i = 0; i < chart.stagePath.points.Length; i++)
                    yield return new TimelineItem { kind = row.kind, data = chart.stagePath.points[i], index = i,
                        time = spatial.RouteControlPointDistance(i) / spatial.UnitsPerSecond, label = "P" + i };
            else if (row.kind == TimelineKind.Camera)
                for (int i = 0; i < chart.cameraKeys.Length; i++)
                    yield return new TimelineItem { kind = row.kind, data = chart.cameraKeys[i], index = i,
                        time = tempo.SecondsAtBeat(chart.cameraKeys[i].beat), label = "K" + i };
            else if (row.kind == TimelineKind.Section)
                for (int i = 0; i < chart.sections.Length; i++)
                    yield return new TimelineItem { kind = row.kind, data = chart.sections[i], index = i,
                        time = tempo.SecondsAtBeat(chart.sections[i].startBeat),
                        end = i + 1 < chart.sections.Length ? tempo.SecondsAtBeat(chart.sections[i + 1].startBeat) : Duration,
                        label = chart.sections[i].name };
            else if (row.kind == TimelineKind.Offset)
            {
                var keys = chart.paths[row.path].offsetKeys;
                if (keys != null) for (int i = 0; i < keys.Length; i++)
                    yield return new TimelineItem { kind = row.kind, data = keys[i], index = i, path = row.path,
                        time = tempo.SecondsAtBeat(keys[i].tick / (double)chart.ticksPerBeat),
                        label = "B" + (keys[i].tick / (float)chart.ticksPerBeat).ToString("0.##") };
            }
            else if (row.kind == TimelineKind.Note)
            {
                for (int i = 0; i < chart.notes.Length; i++)
                    if (chart.notes[i].pathId == chart.paths[row.path].id)
                        yield return new TimelineItem { kind = row.kind, data = chart.notes[i], index = i, path = row.path,
                            time = tempo.SecondsAtBeat(chart.notes[i].tick / (double)chart.ticksPerBeat),
                            label = chart.notes[i].action == "drag" ? "D" : "T" };
            }
            else if (row.kind == TimelineKind.Effect)
                for (int effectIndex = 0; effectIndex < chart.effectClips.Length; effectIndex++)
                {
                    var clip = chart.effectClips[effectIndex];
                    yield return new TimelineItem { kind = row.kind, data = clip, index = effectIndex,
                        time = tempo.SecondsAtBeat(clip.startTick / (double)chart.ticksPerBeat),
                        end = tempo.SecondsAtBeat((clip.startTick + clip.durationTicks) / (double)chart.ticksPerBeat), label = clip.name };
                }
            else if (row.kind == TimelineKind.Motion)
                for (int motionIndex = 0; motionIndex < chart.cameraMotionClips.Length; motionIndex++)
                {
                    var clip = chart.cameraMotionClips[motionIndex];
                    yield return new TimelineItem { kind = row.kind, data = clip, index = motionIndex,
                        time = tempo.SecondsAtBeat(clip.startTick / (double)chart.ticksPerBeat),
                        end = tempo.SecondsAtBeat((clip.startTick + clip.durationTicks) / (double)chart.ticksPerBeat), label = clip.name };
                }
        }
        Rect TimelineItemRect(TimelineItem item, float rowY)
        {
            float x = TimelineX(item.time);
            return item.kind == TimelineKind.Section || item.kind == TimelineKind.Effect || item.kind == TimelineKind.Motion ? new Rect(x, rowY + 6, Mathf.Max(3, TimelineX(item.end) - x), TimelineRowHeight - 12)
                : new Rect(x - 8, rowY + 12, 16, 20);
        }
        bool TimelineItemSelected(TimelineItem item)
        {
            if (item.kind == TimelineKind.Stage) return item.index == selectedStagePoint;
            if (item.kind == TimelineKind.Camera) return item.index == selectedCameraKey;
            if (item.kind == TimelineKind.Section) return item.index == selectedSection;
            if (item.kind == TimelineKind.Note) return item.index == selectedNote;
            if (item.kind == TimelineKind.Effect) return item.index == selectedEffectClip;
            if (item.kind == TimelineKind.Motion) return item.index == selectedMotionClip;
            return item.path == selectedPath && ((PathOffsetKey)item.data).tick == PlayheadTick;
        }
        void SelectTimelineItem(TimelineItem item)
        {
            timelineSelection = item.kind;
            if (item.kind == TimelineKind.Stage) selectedStagePoint = item.index;
            else if (item.kind == TimelineKind.Camera) selectedCameraKey = item.index;
            else if (item.kind == TimelineKind.Section) selectedSection = item.index;
            else if (item.kind == TimelineKind.Note) { selectedNote = item.index; selectedPath = item.path; }
            else if (item.kind == TimelineKind.Offset) selectedPath = item.path;
            else if (item.kind == TimelineKind.Effect) selectedEffectClip = item.index;
            else if (item.kind == TimelineKind.Motion) selectedMotionClip = item.index;
            Seek(item.time);
            if (item.kind == TimelineKind.Stage) FocusSelection();
        }
        void BeginTimelineDrag(TimelineItem item, double mouseTime)
        {
            if (item.kind == TimelineKind.Stage ||
                ((item.kind == TimelineKind.Camera || item.kind == TimelineKind.Section) && item.index == 0)) return;
            timelineDragItem = item; timelineGrabTime = mouseTime; timelineDragUndo = false;
            timelineOriginalTick = ItemTick(item);
        }
        int ItemTick(TimelineItem item)
        {
            if (item.data is CameraKey camera) return Mathf.RoundToInt(camera.beat * chart.ticksPerBeat);
            if (item.data is SectionData section) return Mathf.RoundToInt(section.startBeat * chart.ticksPerBeat);
            if (item.data is PathOffsetKey offset) return offset.tick;
            if (item.data is EffectClipData effect) return effect.startTick;
            if (item.data is CameraMotionClipData motion) return motion.startTick;
            return ((NoteData)item.data).tick;
        }
        bool MoveTimelineItem(TimelineItem item, int tick)
        {
            int minimum = 0, maximum = Mathf.CeilToInt(chart.endBeat * chart.ticksPerBeat);
            if (item.kind == TimelineKind.Note || item.kind == TimelineKind.Section) maximum--;
            if (item.data is EffectClipData effectLimit) maximum -= effectLimit.durationTicks;
            if (item.data is CameraMotionClipData motionLimit) maximum -= motionLimit.durationTicks;
            if (item.kind == TimelineKind.Camera || item.kind == TimelineKind.Section || item.kind == TimelineKind.Offset)
            {
                var row = new TimelineRow { kind = item.kind, path = item.path };
                var items = new List<TimelineItem>(TimelineItems(row));
                int index = items.FindIndex(k => ReferenceEquals(k.data, item.data));
                if (index < 0 || ((item.kind == TimelineKind.Camera || item.kind == TimelineKind.Section) && index == 0)) return false;
                if (index > 0) minimum = ItemTick(items[index - 1]) + 1;
                if (index + 1 < items.Count) maximum = ItemTick(items[index + 1]) - 1;
            }
            if (minimum > maximum) return false;
            tick = Mathf.Clamp(tick, minimum, maximum);
            if (ItemTick(item) == tick) return false;
            if (!timelineDragUndo) { RecordUndo(); timelineDragUndo = true; }
            if (item.data is CameraKey camera) camera.beat = tick / (float)chart.ticksPerBeat;
            else if (item.data is SectionData section) section.startBeat = tick / (float)chart.ticksPerBeat;
            else if (item.data is PathOffsetKey offset) offset.tick = tick;
            else if (item.data is EffectClipData effect) effect.startTick = tick;
            else if (item.data is CameraMotionClipData motion) motion.startTick = tick;
            else if (item.data is NoteData note)
            {
                note.tick = tick;
                Array.Sort(chart.notes, (a, b) => a.tick != b.tick ? a.tick.CompareTo(b.tick) : string.CompareOrdinal(a.id, b.id));
                selectedNote = Array.IndexOf(chart.notes, note);
            }
            if (item.data is EffectClipData movedEffect)
            { Array.Sort(chart.effectClips, (a, b) => a.startTick.CompareTo(b.startTick)); selectedEffectClip = Array.IndexOf(chart.effectClips, movedEffect); }
            if (item.data is CameraMotionClipData movedMotion)
            { Array.Sort(chart.cameraMotionClips, (a, b) => a.startTick.CompareTo(b.startTick)); selectedMotionClip = Array.IndexOf(chart.cameraMotionClips, movedMotion); }
            spatial = new SpatialDirector(chart, tempo); Seek(tempo.SecondsAtBeat(tick / (double)chart.ticksPerBeat));
            return true;
        }
        bool AddTimelineItem(TimelineRow row, double time)
        {
            if (row.kind != TimelineKind.Camera && row.kind != TimelineKind.Note && row.kind != TimelineKind.Offset &&
                row.kind != TimelineKind.Section && row.kind != TimelineKind.Effect && row.kind != TimelineKind.Motion) return false;
            Seek(tempo.SecondsAtBeat(SnapTimelineTick(time) / (double)chart.ticksPerBeat));
            if (row.path >= 0) selectedPath = row.path;
            if (row.kind == TimelineKind.Camera) AddCameraKey();
            else if (row.kind == TimelineKind.Note && CurrentBeat < chart.endBeat) AddNote();
            else if (row.kind == TimelineKind.Offset) Change(() => EnsureOffsetKey(PlayheadTick), "Offset key added from timeline");
            else if (row.kind == TimelineKind.Section && CurrentBeat < chart.endBeat) AddSection();
            else if (row.kind == TimelineKind.Effect) { EnsureVisualLibrary(); AddEffectClip(effectLibrary[selectedEffectAsset]); }
            else if (row.kind == TimelineKind.Motion) { EnsureVisualLibrary(); AddMotionClip(motionLibrary[selectedMotionAsset]); }
            else return false;
            return true;
        }
        void DeleteTimelineItem(TimelineItem item)
        {
            SelectTimelineItem(item);
            DeleteTimelineSelection();
        }
        bool ApplyTimelineClick(TimelineRow row, TimelineItem item, double time, float mouseX, int button)
        {
            if (button == 1)
            {
                if (item == null) return false;
                DeleteTimelineItem(item); return true;
            }
            if (button != 0) return false;
            if (item != null)
            {
                SelectTimelineItem(item);
                if (item.kind != TimelineKind.Section || Mathf.Abs(mouseX - TimelineX(item.time)) <= 10)
                    BeginTimelineDrag(item, time);
                return true;
            }
            Seek(time);
            if (row.path >= 0) selectedPath = row.path;
            if (!AddTimelineItem(row, songTime)) timelineScrubbing = true;
            return true;
        }
        void ReadTimelinePointer(Event e)
        {
            if (WorkspaceInputBlocked || Cursor.lockState == CursorLockMode.Locked) return;
            Vector2 mouse = e.mousePosition / uiScale;
            if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag || e.type == EventType.MouseMove)
                timelineGestureMouse = mouse;
            if (e.type == EventType.ScrollWheel && e.control && new Rect(0, TimelineTop, ViewWidth, TimelineHeight).Contains(mouse))
            {
                if (!timelineResizing && !timelineScrubbing && timelineDragItem == null)
                    ZoomTimelineAt(Mathf.Exp(-e.delta.y * .12f), (mouse.x - TimelineCanvas.x) / TimelineCanvas.width);
                e.Use(); return;
            }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (timelineResizing)
                { PlayerPrefs.SetFloat("ChartStudio.TimelineHeight", TimelineHeight); PlayerPrefs.Save(); }
                if (timelineResizing || timelineScrubbing || timelineDragItem != null) e.Use();
                CancelTimelineGesture(); return;
            }
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (timelineResizing)
                { timelineHeight = ClampTimelineHeight(timelineResizeHeight + timelineResizeMouse - mouse.y, ViewHeight); UpdateViewportRect(); e.Use(); return; }
                if (timelineScrubbing || timelineDragItem != null) { ApplyTimelinePointerTime(mouse.x); e.Use(); return; }
            }
            Rect canvas = TimelineCanvas;
            var rows = TimelineRows();
            if (e.type == EventType.ScrollWheel && mouse.y >= canvas.y && mouse.y <= canvas.yMax)
            {
                if (e.shift) timelineStart = (float)Math.Max(0, Math.Min(TimelineDuration - TimelineSpan, timelineStart + e.delta.y * TimelineSpan * .03));
                else timelineTrackScroll = Mathf.Clamp(timelineTrackScroll + e.delta.y * 18, 0, Mathf.Max(0, rows.Count * TimelineRowHeight - canvas.height));
                e.Use(); return;
            }
            if (e.type != EventType.MouseDown || (e.button != 0 && e.button != 1)) return;
            bool deleteClick = e.button == 1;
            if (deleteClick)
            {
                if (chartCameraPreview || mouse.x < canvas.x || mouse.x >= canvas.xMax || mouse.y < canvas.y || mouse.y > canvas.yMax) return;
                int deleteRowIndex = Mathf.FloorToInt((mouse.y - canvas.y + timelineTrackScroll) / TimelineRowHeight);
                if (deleteRowIndex < 0 || deleteRowIndex >= rows.Count) return;
                var deleteRow = rows[deleteRowIndex];
                float deleteRowY = canvas.y + deleteRowIndex * TimelineRowHeight - timelineTrackScroll;
                foreach (var item in TimelineItems(deleteRow))
                {
                    if (!TimelineItemRect(item, deleteRowY).Contains(mouse)) continue;
                    SetPlaying(false); clearGuiFocus = true;
                    ApplyTimelineClick(deleteRow, item, TimelineTimeAt(mouse.x), mouse.x, e.button); e.Use(); return;
                }
                e.Use(); return;
            }
            if (new Rect(0, TimelineTop - 4, ViewWidth, 12).Contains(mouse))
            {
                timelineResizing = true; timelineResizeHeight = TimelineHeight; timelineResizeMouse = mouse.y;
                draggingHandle = false; clearGuiFocus = true; e.Use(); return;
            }
            if (mouse.x >= canvas.x && mouse.x <= canvas.xMax && mouse.y >= canvas.y - 30 && mouse.y < canvas.y)
            { SetPlaying(false); timelineScrubbing = true; clearGuiFocus = true; Seek(TimelineTimeAt(mouse.x)); e.Use(); return; }
            if (mouse.y < canvas.y || mouse.y > canvas.yMax || mouse.x >= canvas.xMax) return;
            int rowIndex = Mathf.FloorToInt((mouse.y - canvas.y + timelineTrackScroll) / TimelineRowHeight);
            if (rowIndex < 0 || rowIndex >= rows.Count) return;
            var rowData = rows[rowIndex];
            if (chartCameraPreview)
            {
                if (mouse.x >= canvas.x) { SetPlaying(false); timelineScrubbing = true; Seek(TimelineTimeAt(mouse.x)); }
                clearGuiFocus = true; e.Use(); return;
            }
            if (mouse.x < canvas.x)
            {
                if (rowData.path >= 0) { selectedPath = rowData.path; RebuildVisuals(); }
                clearGuiFocus = true; e.Use(); return;
            }
            float rowY = canvas.y + rowIndex * TimelineRowHeight - timelineTrackScroll;
            SetPlaying(false); clearGuiFocus = true;
            foreach (var item in TimelineItems(rowData))
            {
                if (!TimelineItemRect(item, rowY).Contains(mouse)) continue;
                ApplyTimelineClick(rowData, item, TimelineTimeAt(mouse.x), mouse.x, e.button);
                e.Use(); return;
            }
            ApplyTimelineClick(rowData, null, TimelineTimeAt(mouse.x), mouse.x, e.button);
            e.Use();
        }
        void BuildTimelineWaveform()
        {
            timelineWaveform = new float[1024]; timelineWavePeak = .0001f;
            var clip = audioSource == null ? null : audioSource.clip;
            if (clip == null || clip.samples == 0) return;
            const int frames = 4096;
            var buffer = new float[frames * clip.channels];
            for (int start = 0; start < clip.samples; start += frames)
            {
                if (!clip.GetData(buffer, start)) break;
                int count = Math.Min(frames, clip.samples - start);
                for (int i = 0; i < count; i++)
                {
                    float peak = 0;
                    for (int c = 0; c < clip.channels; c++) peak = Mathf.Max(peak, Mathf.Abs(buffer[i * clip.channels + c]));
                    int bin = (int)((long)(start + i) * timelineWaveform.Length / clip.samples);
                    timelineWaveform[bin] = Mathf.Max(timelineWaveform[bin], peak); timelineWavePeak = Mathf.Max(timelineWavePeak, peak);
                }
            }
        }
        void TimelineBox(Rect rect, Color color)
        { Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous; }
        void TimelineButton(ref float x, float y, float width, string text, Action action, bool active = false, bool enabled = true)
        {
            bool previous = GUI.enabled; GUI.enabled = previous && enabled;
            if (GUI.Button(new Rect(x, y, width, 28), text, active ? timelineActiveButtonStyle : timelineButtonStyle)) action();
            GUI.enabled = previous; x += width + 5;
        }
        void DrawTimeline()
        {
            if (timelineLabel == null)
            {
                timelineLabel = new GUIStyle(smallStyle) { wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
                timelineSmall = new GUIStyle(timelineLabel) { fontSize = 10 };
                timelineButtonStyle = new GUIStyle(buttonStyle) { fontSize = 12, padding = new RectOffset(5, 5, 3, 3) };
                timelineActiveButtonStyle = new GUIStyle(selectedButtonStyle) { fontSize = 12, padding = new RectOffset(5, 5, 3, 3) };
            }
            var rows = TimelineRows(); Rect canvas = TimelineCanvas;
            timelineStart = (float)Math.Max(0, Math.Min(TimelineDuration - TimelineSpan, timelineStart));
            timelineTrackScroll = Mathf.Clamp(timelineTrackScroll, 0, Mathf.Max(0, rows.Count * TimelineRowHeight - canvas.height));
            TimelineBox(new Rect(0, TimelineTop, ViewWidth, TimelineHeight), new Color(.065f, .075f, .09f));
            bool hover = new Rect(0, TimelineTop - 4, ViewWidth, 12).Contains(Event.current.mousePosition);
            TimelineBox(new Rect(0, TimelineTop, ViewWidth, 6), timelineResizing || hover ? new Color(.2f, .7f, .85f) : new Color(.20f, .24f, .29f));
            TimelineBox(new Rect(ViewWidth * .5f - 24, TimelineTop + 2, 48, 2), new Color(.7f, .76f, .8f));
            float x = 10, y = TimelineTop + 10;
            TimelineButton(ref x, y, 62, playing ? "Pause" : "Play", () => SetPlaying(!playing), true);
            TimelineButton(ref x, y, 54, "- Beat", () => Seek(tempo.SecondsAtBeat(Math.Max(0, CurrentBeat - 1))));
            TimelineButton(ref x, y, 54, "+ Beat", () => Seek(tempo.SecondsAtBeat(Math.Min(chart.endBeat, CurrentBeat + 1))));
            TimelineButton(ref x, y, 132, chartCameraPreview ? "Exit Preview" : "Preview  F5", ToggleCameraPreview, chartCameraPreview);
            GUI.Label(new Rect(x + 4, y, 198, 28), FormatTimelineTime(songTime) + " / " + FormatTimelineTime(Duration) + "   B " + CurrentBeat.ToString("0.##"), timelineLabel);
            x = Mathf.Max(x + 202, ViewWidth - 258);
            TimelineButton(ref x, y, 90, new[] { "Snap 1", "Snap 1/2", "Snap 1/4", "Snap OFF" }[timelineSnap], () => timelineSnap = (timelineSnap + 1) % 4);
            TimelineButton(ref x, y, 30, "-", () => ZoomTimeline(.5f));
            TimelineButton(ref x, y, 30, "+", () => ZoomTimeline(2));
            TimelineButton(ref x, y, 42, "Fit", () => { timelineZoom = 1; timelineStart = 0; });
            GUI.Label(new Rect(x, y, 50, 28), timelineZoom.ToString("0.#") + "x", timelineLabel);
            DrawTimelineActions(TimelineTop + 44);
            GUI.Label(new Rect(12, TimelineTop + 81, TimelineHeaderWidth - 16, 24), mode.ToString().ToUpperInvariant() + " / TRACKS", timelineSmall);
            DrawTimelineRuler(canvas);
            // Header and canvas have separate clips so neither text nor keys can bleed into the other.
            GUI.BeginGroup(new Rect(0, canvas.y, TimelineHeaderWidth, canvas.height));
            for (int i = 0; i < rows.Count; i++)
            {
                float rowY = i * TimelineRowHeight - timelineTrackScroll;
                if (rowY + TimelineRowHeight < 0 || rowY > canvas.height) continue;
                bool selected = rows[i].path >= 0 && rows[i].path == selectedPath;
                TimelineBox(new Rect(0, rowY, TimelineHeaderWidth, TimelineRowHeight - 1), selected ? new Color(.09f, .20f, .24f) : new Color(.09f, .105f, .125f));
                GUI.Label(new Rect(12, rowY, TimelineHeaderWidth - 20, TimelineRowHeight), rows[i].label, timelineLabel);
            }
            GUI.EndGroup();
            GUI.BeginGroup(canvas);
            for (int i = 0; i < rows.Count; i++)
            {
                float rowY = i * TimelineRowHeight - timelineTrackScroll;
                if (rowY + TimelineRowHeight < 0 || rowY > canvas.height) continue;
                TimelineBox(new Rect(0, rowY, canvas.width, TimelineRowHeight - 1), i % 2 == 0 ? new Color(.095f, .11f, .13f) : new Color(.08f, .09f, .11f));
                DrawTimelineRow(rows[i], rowY, canvas);
            }
            float head = TimelineX(songTime) - canvas.x;
            if (head >= 0 && head <= canvas.width) TimelineBox(new Rect(head - .7f, 0, 1.4f, canvas.height), Color.white);
            float end = TimelineX(Duration) - canvas.x;
            if (end < canvas.width && end >= 0)
            {
                TimelineBox(new Rect(end, 0, 1, canvas.height), new Color(1, .55f, .3f));
                GUI.Label(new Rect(end + 5, canvas.height - 20, 170, 18), "SONG END / ROUTE TAIL", timelineSmall);
            }
            GUI.EndGroup();
            if (rows.Count * TimelineRowHeight > canvas.height)
                timelineTrackScroll = GUI.VerticalScrollbar(new Rect(ViewWidth - 15, canvas.y, 13, canvas.height), timelineTrackScroll, canvas.height, 0, rows.Count * TimelineRowHeight);
            timelineStart = GUI.HorizontalScrollbar(new Rect(canvas.x, ViewHeight - 24, canvas.width, 15), timelineStart, (float)TimelineSpan, 0, (float)TimelineDuration);
            GUI.Label(new Rect(10, ViewHeight - 27, TimelineHeaderWidth - 14, 22), "Ctrl + wheel: zoom", timelineSmall);
        }
        void DrawTimelineActions(float y)
        {
            float x = 10;
            if (chartCameraPreview)
            {
                TimelineButton(ref x, y, 90, "Restart", () => { Seek(0); SetPlaying(true); });
                GUI.Label(new Rect(x + 6, y, ViewWidth - x - 16, 28), "PREVIEW / READ ONLY  ·  Space: play / pause  ·  Esc: back to editing  ·  Demo audio", timelineLabel);
                return;
            }
            if (mode == ChartEditMode.Stage)
            {
                TimelineButton(ref x, y, 90, "Focus point", FocusSelection);
                TimelineButton(ref x, y, 92, "Delete point", DeleteSelection, false, chart.stagePath.points.Length > 2);
                GUI.Label(new Rect(x + 6, y, ViewWidth - x - 10, 28), "Click a route point to focus. Its time follows stage distance; reshape it in 3D.", timelineLabel);
            }
            else if (mode == ChartEditMode.Camera)
            {
                TimelineButton(ref x, y, 112, "+ Camera key", AddCameraKey);
                TimelineButton(ref x, y, 90, "Delete key", DeleteSelection, false, selectedCameraKey > 0);
                TimelineButton(ref x, y, 78, "Focus key", FocusSelection);
                GUI.Label(new Rect(x + 6, y, ViewWidth - x - 10, 28), "Left-click empty track to add; right-click a key to delete. Drag to retime. Beat 0 stays fixed.", timelineLabel);
            }
            else if (mode == ChartEditMode.Paths)
            {
                TimelineButton(ref x, y, 100, "+ Section", AddSection, false, CurrentBeat < chart.endBeat);
                TimelineButton(ref x, y, 102, "+ Offset key", () => Change(() => EnsureOffsetKey(PlayheadTick), "Offset key added"));
                bool canDelete = timelineSelection == TimelineKind.Section ? selectedSection > 0 :
                    Array.Exists(chart.paths[selectedPath].offsetKeys ?? new PathOffsetKey[0], k => k.tick == PlayheadTick);
                TimelineButton(ref x, y, 72, "Delete", DeleteTimelineSelection, false, canDelete);
                if (timelineSelection == TimelineKind.Section)
                {
                    GUI.Label(new Rect(x + 4, y, 58, 28), "Section", timelineLabel); x += 62;
                    GUI.SetNextControlName("Edit:TimelineSection");
                    var section = chart.sections[selectedSection];
                    string name = GUI.TextField(new Rect(x, y, 145, 28), section.name ?? "");
                    if (name != section.name) { RecordUndo(); section.name = name; }
                    x += 154;
                }
                GUI.Label(new Rect(x + 5, y, ViewWidth - x - 12, 28), "Left-click empty track to add; right-click an item to delete. Drag to retime.", timelineLabel);
            }
            else if (mode == ChartEditMode.Notes)
            {
                TimelineButton(ref x, y, 92, "+ Note", AddNote, false, CurrentBeat < chart.endBeat);
                TimelineButton(ref x, y, 90, "Delete note", DeleteSelection, false, selectedNote >= 0);
                TimelineButton(ref x, y, 98, "Focus note  F", FocusSelection, false, selectedNote >= 0 && selectedNote < chart.notes.Length);
                GUI.Label(new Rect(x + 6, y, ViewWidth - x - 10, 28), "Left-click empty track to add; right-click a note to delete. Drag to retime.", timelineLabel);
            }
            else if (mode == ChartEditMode.Effects)
            {
                TimelineButton(ref x, y, 104, "+ Effect clip", () => { EnsureVisualLibrary(); AddEffectClip(effectLibrary[selectedEffectAsset]); });
                TimelineButton(ref x, y, 78, "Delete", DeleteTimelineSelection, false, selectedEffectClip >= 0);
                GUI.Label(new Rect(x + 6, y, ViewWidth - x - 10, 28), "Left-click empty track to add; right-click a clip to delete. Drag to retime.", timelineLabel);
            }
            else if (mode == ChartEditMode.CameraMotion)
            {
                TimelineButton(ref x, y, 112, "+ Motion clip", () => { EnsureVisualLibrary(); AddMotionClip(motionLibrary[selectedMotionAsset]); });
                TimelineButton(ref x, y, 78, "Delete", DeleteTimelineSelection, false, selectedMotionClip >= 0);
                GUI.Label(new Rect(x + 6, y, ViewWidth - x - 10, 28), "Left-click empty track to add; right-click a clip to delete. F5 previews the final shot.", timelineLabel);
            }
            else GUI.Label(new Rect(14, y, ViewWidth - 28, 28), "Map preview  /  audio and playhead. Edit timed content in Stage, Camera, Paths or Notes.", timelineLabel);
        }
        void DeleteTimelineSelection()
        {
            if (mode == ChartEditMode.Effects) { if (selectedEffectClip >= 0) DeleteEffectClip(); return; }
            if (mode == ChartEditMode.CameraMotion) { if (selectedMotionClip >= 0) DeleteMotionClip(); return; }
            if (mode != ChartEditMode.Paths) { DeleteSelection(); return; }
            if (timelineSelection == TimelineKind.Section)
            {
                if (selectedSection <= 0 || chart.sections.Length <= 1) return;
                Change(() => { var list = new List<SectionData>(chart.sections); list.RemoveAt(selectedSection); chart.sections = list.ToArray(); selectedSection--; }, "Section removed");
            }
            else
            {
                var path = chart.paths[selectedPath]; if (path.offsetKeys == null) return;
                int tick = PlayheadTick;
                Change(() => { var list = new List<PathOffsetKey>(path.offsetKeys); list.RemoveAll(k => k.tick == tick); path.offsetKeys = list.ToArray(); }, "Offset key removed");
            }
        }
        static string FormatTimelineTime(double time) => ((int)time / 60).ToString("00") + ":" + (time % 60).ToString("00.00");
        double TimelineTickStep()
        {
            double target = TimelineSpan * 95 / TimelineCanvas.width;
            double power = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(.001, target))));
            return power * (target / power <= 1 ? 1 : target / power <= 2 ? 2 : target / power <= 5 ? 5 : 10);
        }
        void DrawTimelineRuler(Rect canvas)
        {
            GUI.BeginGroup(new Rect(canvas.x, canvas.y - 30, canvas.width, 30));
            double step = TimelineTickStep();
            for (double t = Math.Ceiling(timelineStart / step) * step; t <= timelineStart + TimelineSpan + .0001; t += step)
            {
                float x = TimelineX(t) - canvas.x;
                TimelineBox(new Rect(x, 0, 1, 30), new Color(.3f, .34f, .39f));
                GUI.Label(new Rect(x + 4, 0, 90, 14), FormatTimelineTime(t), timelineSmall);
                GUI.Label(new Rect(x + 4, 14, 90, 14), "B " + tempo.BeatAtSeconds(t).ToString("0.##"), timelineSmall);
            }
            float head = TimelineX(songTime) - canvas.x;
            TimelineBox(new Rect(head - 4, 0, 8, 8), Color.white);
            TimelineBox(new Rect(head - .7f, 0, 1.4f, 30), Color.white);
            GUI.EndGroup();
        }
        void DrawTimelineRow(TimelineRow row, float y, Rect canvas)
        {
            double step = TimelineTickStep();
            for (double t = Math.Ceiling(timelineStart / step) * step; t <= timelineStart + TimelineSpan; t += step)
                TimelineBox(new Rect(TimelineX(t) - canvas.x, y, 1, TimelineRowHeight), new Color(.15f, .17f, .2f));
            if (row.kind == TimelineKind.Audio)
            {
                if (timelineWaveform == null || audioSource.clip == null) return;
                float center = y + TimelineRowHeight * .5f;
                for (float x = 0; x < canvas.width; x += 2)
                {
                    double time = TimelineTimeAt(x + canvas.x);
                    if (time >= audioSource.clip.length) break;
                    int bin = Mathf.Clamp((int)(time / audioSource.clip.length * timelineWaveform.Length), 0, timelineWaveform.Length - 1);
                    float height = Mathf.Max(1, timelineWaveform[bin] / timelineWavePeak * 15);
                    TimelineBox(new Rect(x, center - height, 1.5f, height * 2), new Color(.2f, .66f, .69f));
                }
                return;
            }
            if (row.kind == TimelineKind.X || row.kind == TimelineKind.Y || row.kind == TimelineKind.Z || row.kind == TimelineKind.Fov)
            {
                DrawTimelineCurve(row.kind, y, canvas); return;
            }
            bool any = false;
            foreach (var item in TimelineItems(row))
            {
                Rect r = TimelineItemRect(item, y); r.x -= canvas.x;
                if (r.xMax < 0 || r.x > canvas.width) continue;
                any = true; bool selected = TimelineItemSelected(item);
                Color color = item.kind == TimelineKind.Camera ? new Color(.87f, .5f, .25f) :
                    item.kind == TimelineKind.Section ? new Color(.10f, .43f, .48f) :
                    item.kind == TimelineKind.Effect ? new Color(.68f, .25f, .86f) :
                    item.kind == TimelineKind.Motion ? new Color(.95f, .38f, .25f) : new Color(.23f, .65f, .79f);
                if (item.data is NoteData note) color = note.protectedNote ? new Color(.95f, .68f, .2f) :
                    note.action == "drag" ? new Color(.31f, .77f, .52f) : new Color(.23f, .65f, .9f);
                if (selected) TimelineBox(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(.85f, .92f, 1));
                TimelineBox(r, color);
                if (item.kind == TimelineKind.Section || item.kind == TimelineKind.Effect || item.kind == TimelineKind.Motion)
                {
                    TimelineBox(new Rect(r.x + 2, r.y + 3, 3, r.height - 6), new Color(.36f, .8f, .81f));
                    float textX = Mathf.Max(2, r.x + 9);
                    GUI.Label(new Rect(textX, r.y, Mathf.Max(0, r.xMax - textX - 4), r.height), item.label, timelineLabel);
                }
                else if (item.kind == TimelineKind.Note) GUI.Label(new Rect(r.x + 3, r.y, r.width, r.height), item.label, timelineSmall);
                else GUI.Label(new Rect(r.x + 18, r.y - 1, 56, 22), item.label, timelineSmall);
            }
            if (!any && row.kind == TimelineKind.Offset)
                GUI.Label(new Rect(14, y, canvas.width - 28, TimelineRowHeight), "Left-click to add an offset key at this beat", timelineSmall);
            if (!any && row.kind == TimelineKind.Note)
                GUI.Label(new Rect(14, y, canvas.width - 28, TimelineRowHeight), "Left-click to add a note", timelineSmall);
        }
        void DrawTimelineCurve(TimelineKind kind, float y, Rect canvas)
        {
            const int count = 511, height = 36;
            if (!timelineCurves.TryGetValue(kind, out var cache))
            {
                cache = new TimelineCurveCache { texture = new Texture2D(count + 1, height, TextureFormat.RGBA32, false) };
                cache.texture.wrapMode = TextureWrapMode.Clamp; cache.texture.filterMode = FilterMode.Bilinear;
                ownedTextures.Add(cache.texture); timelineCurves.Add(kind, cache);
            }
            if (cache.revision == timelineCurveRevision && cache.start == timelineStart && cache.span == TimelineSpan)
            { GUI.DrawTexture(new Rect(0, y + 4, canvas.width, height), cache.texture); return; }
            var values = new float[count + 1];
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            for (int i = 0; i <= count; i++)
            {
                double time = timelineStart + TimelineSpan * i / count;
                if (kind == TimelineKind.Fov) { spatial.EvaluateCamera(evaluatorCamera, time); values[i] = evaluatorCamera.fieldOfView; }
                else { var p = spatial.RouteAt((float)time * spatial.UnitsPerSecond); values[i] = kind == TimelineKind.X ? p.x : kind == TimelineKind.Y ? p.y : p.z; }
                min = Mathf.Min(min, values[i]); max = Mathf.Max(max, values[i]);
            }
            if (max - min < .001f) { min--; max++; }
            Color32 color = kind == TimelineKind.X ? new Color(.93f, .4f, .4f) : kind == TimelineKind.Y ? new Color(.4f, .85f, .48f) : new Color(.4f, .65f, .92f);
            var pixels = new Color32[(count + 1) * height];
            int previous = Mathf.RoundToInt(2 + Mathf.InverseLerp(min, max, values[0]) * (height - 5));
            for (int i = 0; i <= count; i++)
            {
                int current = Mathf.RoundToInt(2 + Mathf.InverseLerp(min, max, values[i]) * (height - 5));
                for (int row = Mathf.Min(previous, current); row <= Mathf.Max(previous, current) + 1; row++)
                    pixels[row * (count + 1) + i] = color;
                previous = current;
            }
            cache.texture.SetPixels32(pixels); cache.texture.Apply(false);
            cache.revision = timelineCurveRevision; cache.start = timelineStart; cache.span = TimelineSpan;
            // A cached texture avoids rotated GUI clipping errors and hundreds of draw calls.
            GUI.DrawTexture(new Rect(0, y + 4, canvas.width, height), cache.texture);
        }
    }
}
