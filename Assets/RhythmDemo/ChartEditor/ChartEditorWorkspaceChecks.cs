using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        bool RunWorkspaceChecks()
        {
            string snapshot = JsonUtility.ToJson(chart), previousFile = filePath, previousSaved = savedChartJson;
            bool previousNeedsSave = needsSaveAs;
            var report = new StringBuilder(); int checks = 0; bool passed = false;
            Action<bool, string> check = (value, name) =>
            {
                if (!value) throw new InvalidOperationException(name);
                checks++; report.AppendLine("PASS " + name);
            };
            try
            {
                NewChart(); SetMode(ChartEditMode.Notes); ReleaseMouse(); flyMode = false;
                showFilesMenu = showSettings = showGuide = false;
                pendingFileAction = PendingFileAction.None;
                check(minimalVerticalThumb.normal.background != null && minimalVerticalThumb.normal.background.GetPixel(1, 12).a == 0 &&
                    minimalVerticalThumb.normal.background.GetPixel(5, 12).a > .9f && minimalVerticalThumb.fixedWidth == 12,
                    "Minimal scrollbar has a 6 px drawing within a 12 px grab area");
                foreach (float scale in new[] { 1f, 1.25f, 1.75f })
                {
                    float previousScale = uiScale; uiScale = scale;
                    timelineZoom = 2; timelineStart = 8;
                    float x = TimelineCanvas.x + TimelineCanvas.width * .4f;
                    double anchor = TimelineTimeAt(x); float zoom = timelineZoom;
                    var wheel = new Event { type = EventType.ScrollWheel, modifiers = EventModifiers.Control,
                        delta = new Vector2(0, -2), mousePosition = new Vector2(x, TimelineTop + 24) * uiScale };
                    ReadTimelinePointer(wheel);
                    check(timelineZoom > zoom && wheel.type == EventType.Used, "Ctrl wheel zoom over timeline toolbar at scale " + scale);
                    check(Math.Abs(TimelineTimeAt(x) - anchor) < .00002, "Zoom keeps cursor time anchored at scale " + scale);
                    wheel = new Event { type = EventType.ScrollWheel, modifiers = EventModifiers.Control,
                        delta = new Vector2(0, 2), mousePosition = new Vector2(x, TimelineCanvas.y + 10) * uiScale };
                    ReadTimelinePointer(wheel);
                    check(Mathf.Abs(timelineZoom - zoom) < .00001, "Reverse wheel restores zoom at scale " + scale);
                    uiScale = previousScale;
                }
                ZoomTimelineAt(10000, .5); check(timelineZoom == 64, "Zoom clamps to 64x");
                ZoomTimelineAt(.00001f, .5); check(timelineZoom == 1 && timelineStart == 0, "Zoom out clamps to Fit");
                float beforeZoom = timelineZoom;
                ReadTimelinePointer(new Event { type = EventType.ScrollWheel, modifiers = EventModifiers.Control, delta = Vector2.down,
                    mousePosition = new Vector2(PanelWidth + 40, ToolbarHeight + 20) * uiScale });
                check(timelineZoom == beforeZoom, "Ctrl wheel in viewport never zooms timeline");
                showFilesMenu = true;
                var click = new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(TimelineCanvas.x + 50, TimelineCanvas.y - 10) * uiScale };
                double oldTime = songTime; ReadTimelinePointer(click);
                check(songTime == oldTime && !PointerInViewport(new Vector2(PanelWidth + 30, ToolbarHeight + 30) * uiScale), "Files menu blocks timeline and scene input");
                DismissFilesMenu(click); ReadTimelinePointer(click);
                check(!showFilesMenu && click.type == EventType.Used && songTime == oldTime, "Outside click closes menu without leaking to timeline");
                chart.title = "Workspace QA draft";
                RequestFileAction(PendingFileAction.New);
                check(pendingFileAction == PendingFileAction.New && chart.title == "Workspace QA draft", "New protects unsaved work");
                ReadWorkspaceKeys(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                check(pendingFileAction == PendingFileAction.None && chart.title == "Workspace QA draft", "Escape cancels pending replacement");

                string testDirectory = Path.Combine(smokeDirectory, "files-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(testDirectory);
                string first = Path.Combine(testDirectory, "draft.json"), second = Path.Combine(testDirectory, "副本 copy.json");
                check(TrySaveChart(first) && !needsSaveAs && !HasUnsavedChanges, "Save establishes current file and clean baseline");
                string original = File.ReadAllText(first); chart.title = "QA copy";
                check(TrySaveChart(second) && filePath == Path.GetFullPath(second) && File.ReadAllText(first) == original, "Save As changes destination without modifying original");
                chart.title = "QA updated copy";
                check(SaveCurrentChart() && File.ReadAllText(first) == original && File.ReadAllText(second).Contains("QA updated copy"), "Save targets the new file after Save As");
                string originalPath = filePath, originalJson = JsonUtility.ToJson(chart);
                check(!TrySaveChart(testDirectory) && filePath == originalPath, "Failed save preserves current destination");
                string invalid = Path.Combine(testDirectory, "invalid.json"); File.WriteAllText(invalid, "{broken");
                check(!TryLoadChart(invalid) && filePath == originalPath && JsonUtility.ToJson(chart) == originalJson, "Invalid JSON does not replace current chart");
                File.WriteAllText(invalid, "{}");
                check(!TryLoadChart(invalid) && JsonUtility.ToJson(chart) == originalJson, "Unrelated JSON rejected");
                check(TryLoadChart(first) && chart.title == "Workspace QA draft" && !HasUnsavedChanges, "Load restores a saved draft with no notes");

                Seek(2); AddNote(); Seek(1); FocusSelection();
                Vector3 position = sceneCamera.transform.position; Quaternion rotation = sceneCamera.transform.rotation;
                float fov = sceneCamera.fieldOfView; string previewSnapshot = JsonUtility.ToJson(chart); int undoCount = undo.Count;
                SetPreviewMode(true);
                check(chartCameraPreview && playing && Math.Abs(songTime - 1) < .001, "Preview auto-plays from current playhead");
                check(sceneCamera.pixelRect.x == 0 && sceneCamera.pixelRect.width == Screen.width, "Preview expands over inspector without fullscreen");
                check(visualRoot.GetComponentsInChildren<ChartEditorHandle>().Length == 0 && cameraTargetMarker == null && visualRoot.Find("Stage master spline") == null,
                    "Preview hides editing handles, stage guide and yellow marker");
                spatial.EvaluateCamera(evaluatorCamera, songTime);
                check(Vector3.Distance(sceneCamera.transform.position, evaluatorCamera.transform.position) < .001f &&
                    Quaternion.Angle(sceneCamera.transform.rotation, evaluatorCamera.transform.rotation) < .001f, "Preview uses shared gameplay camera evaluation");
                Transform mapChunk = visualRoot.Find("Generated map chunk 0"); RefreshPreviewVisuals();
                check(mapChunk != null && visualRoot.Find("Generated map chunk 0") == mapChunk, "Preview playback retains static map geometry");
                ReadWorkspaceKeys(new Event { type = EventType.KeyDown, keyCode = KeyCode.Space });
                check(!playing, "Space pauses preview");
                var canvas = TimelineCanvas;
                ReadTimelinePointer(new Event { type = EventType.MouseDown, button = 0, clickCount = 1,
                    mousePosition = new Vector2(TimelineX(6), canvas.y + TimelineRowHeight * 1.5f) * uiScale });
                check(timelineDragItem == null && JsonUtility.ToJson(chart) == previewSnapshot && undo.Count == undoCount,
                    "Preview timeline scrubs without adding or retiming notes");
                ReadTimelinePointer(new Event { type = EventType.MouseUp, button = 0 });
                ReadWorkspaceKeys(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                check(!chartCameraPreview && !playing, "Escape exits preview and pauses");
                check(Vector3.Distance(position, sceneCamera.transform.position) < .001f && Quaternion.Angle(rotation, sceneCamera.transform.rotation) < .001f &&
                    sceneCamera.fieldOfView == fov, "Exit restores exact editor position, rotation and FOV");
                check(sceneCamera.pixelRect.x > 0 && visualRoot.GetComponentsInChildren<ChartEditorHandle>().Length > 0, "Exit restores inspector viewport and handles");
                Seek(Duration); SetPreviewMode(true);
                check(songTime == 0 && playing, "Preview at song end restarts from zero");
                SetPreviewMode(false); passed = true;
            }
            catch (Exception error) { report.AppendLine("FAIL " + error); Debug.LogException(error); }
            finally
            {
                if (chartCameraPreview) SetPreviewMode(false);
                showFilesMenu = false; pendingFileAction = PendingFileAction.None; SetPlaying(false); CancelTimelineGesture();
                chart = JsonUtility.FromJson<ChartData>(snapshot); filePath = previousFile; savedChartJson = previousSaved; needsSaveAs = previousNeedsSave;
                songTime = 0; undo.Clear(); redo.Clear(); Rebuild(); SetupAudio(); UpdateViewportRect();
            }
            report.AppendLine("RESULT=" + (passed ? "PASS" : "FAIL") + " CHECKS=" + checks);
            File.WriteAllText(Path.Combine(smokeDirectory, "workspace-checks.txt"), report.ToString());
            Debug.Log("CHART_STUDIO_WORKSPACE " + (passed ? "PASS" : "FAIL") + " " + checks); return passed;
        }
    }
}
