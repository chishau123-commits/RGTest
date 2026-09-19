using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        bool RunEditingPlaybackChecks()
        {
            string snapshot = JsonUtility.ToJson(chart), savedBaseline = savedChartJson;
            bool savedNeedsSaveAs = needsSaveAs; float savedScale = uiScale, savedTimelineHeight = timelineHeight;
            var report = new StringBuilder(); int checks = 0; bool passed = false;
            Action<bool, string> check = (value, name) =>
            {
                if (!value) throw new InvalidOperationException(name);
                checks++; report.AppendLine("PASS " + name);
            };
            Action<string> checkCentered = name =>
            {
                Vector3 screen = sceneCamera.WorldToScreenPoint(PlacementMarkerPosition);
                Vector3 viewport = sceneCamera.WorldToViewportPoint(PlacementMarkerPosition);
                // Fractional UI scaling can rasterize the pixel rect on half-pixels;
                // assert exact normalized centering and at most one physical pixel.
                check(Math.Abs(viewport.x - .5f) < .0001f && Math.Abs(viewport.y - .5f) < .0001f &&
                    Vector2.Distance(new Vector2(screen.x, screen.y), sceneCamera.pixelRect.center) < 1 && screen.z > 0,
                    "Yellow marker is centered in the actual 3D viewport: " + name + " viewport=" + viewport + " screen=" + screen + " rect=" + sceneCamera.pixelRect);
                if (cameraTargetMarker != null)
                    check(Vector3.Distance(cameraTargetMarker.position, PlacementMarkerPosition) < .001f,
                        "Visible yellow sphere matches the placement position: " + name);
            };
            try
            {
                NewChart(); SetMode(ChartEditMode.Camera); ReleaseMouse(); flyMode = false;
                showGuide = showSettings = showFilesMenu = false;
                foreach (float scale in new[] { 1f, 1.25f, 1.75f })
                {
                    uiScale = scale; timelineZoom = 4; timelineStart = 8;
                    Rect canvas = TimelineCanvas;
                    Vector2 right = new Vector2(canvas.xMax - 1, canvas.y - 12);
                    ReadTimelinePointer(new Event { type = EventType.MouseDown, button = 0, mousePosition = right * scale });
                    double beforeTime = songTime; UpdateTimelineEdgeScroll(1f / 60);
                    check(timelineStart > 8 && timelineStart < 9 && songTime > beforeTime, "Smooth right-edge scrub at UI scale " + scale);
                    float before = timelineStart; UpdateTimelineEdgeScroll(1f / 60);
                    check(timelineStart > before, "Stationary held pointer continues scrolling at " + scale);
                    ReadTimelinePointer(new Event { type = EventType.MouseDrag, button = 0,
                        mousePosition = new Vector2(canvas.x + 1, right.y) * scale });
                    before = timelineStart; UpdateTimelineEdgeScroll(1f / 60);
                    check(timelineStart < before, "Left-edge scrub at " + scale);
                    ReadTimelinePointer(new Event { type = EventType.MouseDrag, button = 0,
                        mousePosition = new Vector2(canvas.center.x, right.y) * scale });
                    before = timelineStart; UpdateTimelineEdgeScroll(1f / 60);
                    check(timelineStart == before, "Leaving edge stops scrolling at " + scale);
                    ReadTimelinePointer(new Event { type = EventType.MouseUp, button = 0, mousePosition = right * scale });
                    timelineGestureMouse = right; UpdateTimelineEdgeScroll(1f / 60);
                    check(timelineStart == before && !timelineScrubbing, "Release stops edge scrolling at " + scale);
                }
                uiScale = savedScale; timelineScrubbing = true; timelineStart = 8;
                timelineGestureMouse = new Vector2(TimelineCanvas.xMax, TimelineCanvas.y - 10);
                for (int i = 0; i < 30; i++) UpdateTimelineEdgeScroll(1f / 30);
                float at30fps = timelineStart; timelineStart = 8;
                for (int i = 0; i < 60; i++) UpdateTimelineEdgeScroll(1f / 60);
                check(Math.Abs(timelineStart - at30fps) < .001, "Edge scrolling is frame-rate independent");
                timelineStart = (float)(Duration - TimelineSpan); UpdateTimelineEdgeScroll(1f / 60);
                check(Math.Abs(timelineStart - (Duration - TimelineSpan)) < .001, "Right edge clamps at song end");
                timelineStart = 0; timelineGestureMouse.x = TimelineCanvas.x; UpdateTimelineEdgeScroll(1f / 60);
                check(timelineStart == 0, "Left edge clamps at zero");
                timelineStart = 8; showFilesMenu = true; UpdateTimelineEdgeScroll(1f / 60);
                check(timelineStart == 8, "Menu blocks edge gestures"); showFilesMenu = false;
                timelineGestureMouse.y = ToolbarHeight; UpdateTimelineEdgeScroll(1f / 60);
                check(timelineStart == 8, "Pointer outside timeline does not edge-scroll");
                CancelTimelineGesture(); Seek(30); timelineStart = 0; SetPlaying(true); UpdateTimelineEdgeScroll(1f / 60);
                check(timelineStart > 0 && timelineStart < 1, "Playback follows smoothly to the right without jumping");
                timelineStart = 40; UpdateTimelineEdgeScroll(1f / 60);
                check(timelineStart < 40 && timelineStart > 39, "Playback follows smoothly to the left");
                timelineStart = 0; Cursor.lockState = CursorLockMode.Locked; UpdateTimelineEdgeScroll(1f / 60);
                check(timelineStart > 0, "Playback follow remains active while creative flight captures the mouse"); ReleaseMouse();
                timelineZoom = 64; timelineStart = 8; songTime = 8.9;
                for (int i = 0; i < 120; i++) { songTime += 1.0 / 60; UpdateTimelineEdgeScroll(1f / 60); }
                check(songTime <= timelineStart + TimelineSpan + .001, "Playback follow keeps up at maximum zoom");
                SetPlaying(false); SetMode(ChartEditMode.Stage); timelineZoom = 4;
                timelineStart = (float)(Duration - TimelineSpan); timelineScrubbing = true;
                timelineGestureMouse = new Vector2(TimelineCanvas.xMax, TimelineCanvas.y - 10); UpdateTimelineEdgeScroll(1f / 60);
                check(Math.Abs(timelineStart - (Duration - TimelineSpan)) < .001, "Stage route tail cannot pull scrubbing past song end");
                CancelTimelineGesture();

                NewChart(); SetMode(ChartEditMode.Notes); Seek(4); AddNote(); Seek(40); AddNote();
                timelineZoom = 4; timelineStart = 0;
                var noteItem = new List<TimelineItem>(TimelineItems(new TimelineRow { kind = TimelineKind.Note, path = 0 }))[0];
                int originalTick = ((NoteData)noteItem.data).tick, beforeUndo = undo.Count;
                BeginTimelineDrag(noteItem, noteItem.time);
                timelineGestureMouse = new Vector2(TimelineCanvas.xMax, TimelineCanvas.y + TimelineRowHeight * 1.5f);
                ApplyTimelinePointerTime(timelineGestureMouse.x);
                for (int i = 0; i < 6; i++) UpdateTimelineEdgeScroll(.05f);
                check(tempo.SecondsAtBeat(((NoteData)noteItem.data).tick / (double)chart.ticksPerBeat) > 16,
                    "Dragged note can move beyond the original visible window");
                check(undo.Count == beforeUndo + 1, "Edge drag retains one undo entry");
                CancelTimelineGesture(); Undo(); check(chart.notes[0].tick == originalTick, "Undo restores edge-dragged note");

                NewChart(); SetMode(ChartEditMode.Camera);
                chart.tempos = new[] { new TempoData { tick = 0, bpm = 120 }, new TempoData { tick = 32 * 480, bpm = 180 } };
                chart.cameraKeys = new[]
                {
                    new CameraKey { beat = 0, distance = 25, height = 6, fov = 53 },
                    new CameraKey { beat = 32, useWorldPose = true, worldPosition = new Vector3(200, 50, -500), worldTarget = Vector3.zero, fov = 53 },
                    new CameraKey { beat = 64, usePathPose = true, positionForward = -9, positionX = 30, positionY = 12, targetForward = 18, targetY = 2, fov = 53 }
                };
                chart.paths[0].offsetKeys = new[] { new PathOffsetKey { tick = 0, x = -4 }, new PathOffsetKey { tick = 96 * 480, x = 4, y = 3 } };
                chart.notes = new[] { new NoteData { id = "no-follow-fixed-note", tick = 40 * 480, pathId = "p0", action = "tap" } };
                orbitDistance = 47; orbitYaw = 25; orbitPitch = 22; orbitPivot = new Vector3(9, 12, 30);
                ApplyOrbitCamera(); Rebuild();
                Vector3 viewPosition = sceneCamera.transform.position, viewPivot = orbitPivot, marker = PlacementMarkerPosition;
                Quaternion viewRotation = sceneCamera.transform.rotation;
                Action<string> checkNoMovement = name =>
                {
                    check(Vector3.Distance(sceneCamera.transform.position, viewPosition) < .001f &&
                        Vector3.Distance(orbitPivot, viewPivot) < .001f &&
                        Quaternion.Angle(sceneCamera.transform.rotation, viewRotation) < .05f &&
                        Vector3.Distance(PlacementMarkerPosition, marker) < .001f,
                        "No automatic editor camera or marker movement: " + name);
                    checkCentered(name);
                };
                foreach (ChartEditMode view in Enum.GetValues(typeof(ChartEditMode)))
                {
                    SetMode(view);
                    foreach (double time in new[] { 0, 12, tempo.SecondsAtBeat(32), tempo.SecondsAtBeat(64), Duration })
                    {
                        Seek(time); checkNoMovement(view + " seek " + time);
                        SetPlaying(true);
                        songTime = Math.Min(Duration, songTime + .25); RefreshEditorPlayheadVisuals();
                        checkNoMovement(view + " play " + time);
                        SetPlaying(false); checkNoMovement(view + " pause " + time);
                    }
                }
                SetMode(ChartEditMode.Camera);
                foreach (float scale in new[] { 1f, 1.25f, 1.75f })
                {
                    uiScale = scale;
                    foreach (float height in new[] { 190f, 330f, 450f })
                    {
                        timelineHeight = height; UpdateViewportRect(); RefreshEditorPlayheadVisuals();
                        checkCentered("UI " + scale + " timeline height " + height);
                    }
                }
                uiScale = savedScale; timelineHeight = savedTimelineHeight; UpdateViewportRect();
                SetFlightMode(true); ReleaseMouse(); RefreshEditorPlayheadVisuals(); checkNoMovement("Caps ON");
                SetFlightMode(false); ApplyOrbitCamera(); RefreshEditorPlayheadVisuals(); checkNoMovement("Caps OFF");

                orbitYaw += 35; orbitPitch = 27; orbitPivot += new Vector3(2, 3, 4); ApplyOrbitCamera();
                RefreshEditorPlayheadVisuals(); checkCentered("manual orbit/pan");
                check(Vector3.Distance(sceneCamera.transform.position, viewPosition) > 1, "Manual orbit/pan still moves the editor");
                SetFlightMode(true); ReleaseMouse();
                sceneCamera.transform.position += new Vector3(3, 4, 5);
                sceneCamera.transform.rotation = Quaternion.Euler(12, 48, 0);
                RefreshEditorPlayheadVisuals(); checkCentered("manual flight/look");
                SetFlightMode(false); ApplyOrbitCamera(); RefreshEditorPlayheadVisuals(); checkCentered("return to orbit");
                viewPosition = sceneCamera.transform.position; viewRotation = sceneCamera.transform.rotation;
                viewPivot = orbitPivot; marker = PlacementMarkerPosition;

                Seek(tempo.SecondsAtBeat(31.9)); SetPlaying(true);
                songTime = tempo.SecondsAtBeat(32.1); RefreshEditorPlayheadVisuals(); checkNoMovement("crossing a distant world camera key");
                songTime = tempo.SecondsAtBeat(64.1); RefreshEditorPlayheadVisuals(); checkNoMovement("crossing a route-local camera key");
                chart.cameraKeys[1].worldPosition += new Vector3(100, 100, 100); Rebuild();
                checkNoMovement("editing authored camera positions during playback");

                Seek(4); SetPlaying(true);
                Transform oldRoot = visualRoot, fixedNote = visualRoot.Find("no-follow-fixed-note");
                Vector3 notePosition = fixedNote.position, staticPathPoint = visualRoot.Find("Note path p0").GetComponent<LineRenderer>().GetPosition(0);
                Vector3 livePoint = editorLivePaths[0].GetPosition(0);
                songTime += 1; RefreshEditorPlayheadVisuals(); checkNoMovement("live overlay refresh");
                check(visualRoot == oldRoot && visualRoot.Find("no-follow-fixed-note") == fixedNote &&
                    fixedNote.position == notePosition &&
                    visualRoot.Find("Note path p0").GetComponent<LineRenderer>().GetPosition(0) == staticPathPoint,
                    "Permanent notes and paths are neither moved nor rebuilt during playback");
                check(Vector3.Distance(editorLivePaths[0].GetPosition(0), livePoint) > 1 &&
                    Vector3.Distance(editorLivePaths[0].GetPosition(0), spatial.Point("p0", SpatialDirector.NearDepth - 2, songTime)) < .001f,
                    "Purple paths still show the current time without moving the editor");
                check(editorLivePathMaterial.color != pathMaterial.color && editorLivePathMaterial.color != selectedMaterial.color,
                    "Live paths retain a distinct color");
                Vector3 crossCenter = (editorPlayheadLine.GetPosition(0) + editorPlayheadLine.GetPosition(1)) * .5f;
                Vector3 verticalCenter = (editorPlayheadVerticalLine.GetPosition(0) + editorPlayheadVerticalLine.GetPosition(1)) * .5f;
                check(Vector3.Distance(crossCenter, EditorPlayheadPosition(songTime)) < .001f &&
                    Vector3.Distance(verticalCenter, crossCenter) < .001f &&
                    Mathf.Abs(Vector3.Dot((editorPlayheadLine.GetPosition(1) - editorPlayheadLine.GetPosition(0)).normalized,
                        sceneCamera.transform.right)) > .999f &&
                    Mathf.Abs(Vector3.Dot((editorPlayheadVerticalLine.GetPosition(1) - editorPlayheadVerticalLine.GetPosition(0)).normalized,
                        sceneCamera.transform.up)) > .999f,
                    "The stable two-stroke 3D playhead indicator updates without moving the editor");

                Seek(tempo.SecondsAtBeat(48)); AddCameraKey(); checkNoMovement("Add Key while playing");
                check(Vector3.Distance(chart.cameraKeys[selectedCameraKey].worldPosition, marker) < .001f,
                    "Playing Add Key saves the centered yellow ball, not a follow position");
                SetPlaying(false); Seek(tempo.SecondsAtBeat(52)); AddCameraKey(); checkNoMovement("Add Key while paused");
                check(Vector3.Distance(chart.cameraKeys[selectedCameraKey].worldPosition, marker) < .001f,
                    "Paused Add Key saves the same centered placement point");
                Seek(Duration - .1); SetPlaying(true); songTime = Duration; SetPlaying(false);
                checkNoMovement("automatic song-end pause");
                SetPlaying(true); check(songTime == 0, "Restart at song end still resets only the time");
                checkNoMovement("restart at beginning"); SetPlaying(false);

                SetPreviewMode(true); SetPlaying(false);
                check(editorLivePaths == null && editorPlayheadLine == null && editorPlayheadVerticalLine == null && cameraTargetMarker == null,
                    "Preview still hides editing overlays and the yellow marker");
                spatial.EvaluateCamera(evaluatorCamera, songTime);
                check(Vector3.Distance(sceneCamera.transform.position, evaluatorCamera.transform.position) < .001f,
                    "Preview retains its authored gameplay camera behavior");
                SetPreviewMode(false);
                check(editorLivePaths != null && visualRoot.Find("no-follow-fixed-note") != null,
                    "Leaving Preview restores editing overlays and fixed notes");
                checkNoMovement("return from Preview");
                passed = true;
            }
            catch (Exception error) { report.AppendLine("FAIL " + error); Debug.LogException(error); }
            finally
            {
                if (chartCameraPreview) SetPreviewMode(false);
                SetPlaying(false); CancelTimelineGesture(); ReleaseMouse(); showFilesMenu = false; uiScale = savedScale; timelineHeight = savedTimelineHeight;
                chart = JsonUtility.FromJson<ChartData>(snapshot); savedChartJson = savedBaseline; needsSaveAs = savedNeedsSaveAs;
                songTime = 0; timelineZoom = 1; timelineStart = 0;
                undo.Clear(); redo.Clear(); Rebuild(); SetupAudio(); UpdateViewportRect();
            }
            report.AppendLine("RESULT=" + (passed ? "PASS" : "FAIL") + " CHECKS=" + checks);
            File.WriteAllText(Path.Combine(smokeDirectory, "editing-playback-checks.txt"), report.ToString());
            Debug.Log("CHART_STUDIO_EDITING_PLAYBACK " + (passed ? "PASS" : "FAIL") + " " + checks); return passed;
        }
    }
}
