using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        enum PendingFileAction { None, New, Load }
        PendingFileAction pendingFileAction;
        bool showFilesMenu, fileDialogOpen, needsSaveAs;
        GUIStyle minimalVerticalThumb;
        string savedChartJson;
        bool WorkspaceModalOpen => showSettings || showGuide || fileDialogOpen || pendingFileAction != PendingFileAction.None;
        bool WorkspaceInputBlocked => WorkspaceModalOpen || showFilesMenu;
        bool HasUnsavedChanges => chart != null && JsonUtility.ToJson(chart) != savedChartJson;
        Rect FilesMenuRect => new Rect(ViewWidth - 226, ToolbarHeight + 2, 170, 116);
        Transform previewMotionRoot;
        Vector3 previewEditorPosition, previewEditorPivot;
        Quaternion previewEditorRotation;
        float previewEditorFov, previewEditorYaw, previewEditorPitch, previewEditorDistance;

        void BuildMinimalScrollStyles()
        {
            // Keep a 12 px grab area, but draw only a 6 px thumb. No bevels or arrows.
            GUI.skin.verticalScrollbar = MinimalScrollStyle(true, false);
            GUI.skin.verticalScrollbarThumb = MinimalScrollStyle(true, true);
            minimalVerticalThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.horizontalScrollbar = MinimalScrollStyle(false, false);
            GUI.skin.horizontalScrollbarThumb = MinimalScrollStyle(false, true);
            GUI.skin.verticalScrollbarUpButton = new GUIStyle();
            GUI.skin.verticalScrollbarDownButton = new GUIStyle();
            GUI.skin.horizontalScrollbarLeftButton = new GUIStyle();
            GUI.skin.horizontalScrollbarRightButton = new GUIStyle();
            GUI.skin.horizontalSlider = MinimalScrollStyle(false, false);
            GUI.skin.horizontalSlider.margin = new RectOffset(0, 0, 4, 4);
            GUI.skin.horizontalSliderThumb = new GUIStyle { fixedWidth = 12, fixedHeight = 12, border = new RectOffset(8, 8, 8, 8) };
            GUI.skin.horizontalSliderThumb.normal.background = RoundedTexture(new Color(.42f, .50f, .59f), 16);
            GUI.skin.horizontalSliderThumb.hover.background = RoundedTexture(new Color(.50f, .60f, .69f), 16);
            GUI.skin.horizontalSliderThumb.active.background = RoundedTexture(new Color(.25f, .66f, .81f), 16);
        }
        GUIStyle MinimalScrollStyle(bool vertical, bool thumb)
        {
            var style = new GUIStyle
            {
                fixedWidth = vertical ? 12 : 0, fixedHeight = vertical ? 0 : 12,
                border = vertical ? new RectOffset(0, 0, 6, 6) : new RectOffset(6, 6, 0, 0)
            };
            style.normal.background = ScrollTexture(thumb ? new Color(.30f, .35f, .42f) : new Color(.08f, .095f, .12f), vertical);
            style.hover.background = thumb ? ScrollTexture(new Color(.42f, .50f, .59f), vertical) : style.normal.background;
            style.active.background = thumb ? ScrollTexture(new Color(.25f, .66f, .81f), vertical) : style.normal.background;
            return style;
        }
        Texture2D ScrollTexture(Color color, bool vertical)
        {
            // Transparent gutters are baked into the texture; IMGUI ignores negative overflow.
            int width = vertical ? 12 : 24, height = vertical ? 24 : 12;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float across = (vertical ? x : y) + .5f - 6;
                float along = (vertical ? y : x) + .5f;
                float cap = Mathf.Max(3 - along, along - 21, 0);
                float alpha = Mathf.Clamp01(3 - Mathf.Sqrt(across * across + cap * cap));
                pixels[y * width + x] = new Color(color.r, color.g, color.b, color.a * alpha);
            }
            texture.SetPixels(pixels); texture.Apply(); ownedTextures.Add(texture); return texture;
        }

        void ReadWorkspaceKeys(Event e)
        {
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Escape)
            {
                if (pendingFileAction != PendingFileAction.None) pendingFileAction = PendingFileAction.None;
                else if (showSettings || showGuide) showSettings = showGuide = false;
                else if (showFilesMenu) showFilesMenu = false;
                else if (chartCameraPreview) SetPreviewMode(false);
                else ReleaseMouse();
                e.Use(); return;
            }
            if (WorkspaceInputBlocked || textInputFocused) return;
            if (e.keyCode == KeyCode.F5) { ToggleCameraPreview(); e.Use(); }
            else if (chartCameraPreview && e.keyCode == KeyCode.Space) { SetPlaying(!playing); e.Use(); }
            else if (e.control && e.keyCode == KeyCode.S && Cursor.lockState != CursorLockMode.Locked)
            { if (e.shift) SaveChartAs(); else SaveFile(); e.Use(); }
        }
        void DismissFilesMenu(Event e)
        {
            if (!showFilesMenu || e.type != EventType.MouseDown) return;
            Vector2 mouse = e.mousePosition / uiScale;
            if (FilesMenuRect.Contains(mouse) || new Rect(ViewWidth - 226, 0, 70, ToolbarHeight).Contains(mouse)) return;
            showFilesMenu = false;
            // Dismissing a dropdown must not also move a control point or the playhead.
            if (mouse.y >= ToolbarHeight) e.Use();
        }
        void DrawFilesMenu()
        {
            Rect area = FilesMenuRect;
            GUI.Box(area, "", panelStyle);
            if (GUI.Button(new Rect(area.x + 7, area.y + 7, area.width - 14, 31), "New")) RequestFileAction(PendingFileAction.New);
            if (GUI.Button(new Rect(area.x + 7, area.y + 42, area.width - 14, 31), "Load")) RequestFileAction(PendingFileAction.Load);
            if (GUI.Button(new Rect(area.x + 7, area.y + 77, area.width - 14, 31), "Save As"))
            { showFilesMenu = false; SaveChartAs(); }
        }
        void RequestFileAction(PendingFileAction action)
        {
            showFilesMenu = false; ReleaseMouse(); CancelTimelineGesture(); SetPlaying(false);
            if (HasUnsavedChanges) pendingFileAction = action;
            else RunFileAction(action);
        }
        void RunFileAction(PendingFileAction action)
        {
            pendingFileAction = PendingFileAction.None;
            if (action == PendingFileAction.New) NewChart();
            else if (action == PendingFileAction.Load)
            {
                string path = ChooseChartFile(false);
                if (!string.IsNullOrEmpty(path)) TryLoadChart(path);
            }
        }
        void DrawUnsavedConfirmation()
        {
            Rect area = new Rect((ViewWidth - 470) * .5f, (ViewHeight - 182) * .5f, 470, 182);
            GUI.Box(new Rect(0, 0, ViewWidth, ViewHeight), "", toolbarStyle);
            GUI.Box(area, "", panelStyle);
            GUI.Label(new Rect(area.x + 22, area.y + 18, 430, 32), "Unsaved changes", titleStyle);
            GUI.Label(new Rect(area.x + 22, area.y + 56, 420, 50),
                "Save the current chart before continuing?\nCancel keeps your current work open.", smallStyle);
            if (GUI.Button(new Rect(area.x + 20, area.y + 123, 154, 32), "Save & Continue", selectedButtonStyle))
            {
                var next = pendingFileAction;
                if (SaveCurrentChart()) RunFileAction(next);
            }
            if (GUI.Button(new Rect(area.x + 183, area.y + 123, 126, 32), "Discard changes")) RunFileAction(pendingFileAction);
            if (GUI.Button(new Rect(area.x + 318, area.y + 123, 130, 32), "Cancel")) pendingFileAction = PendingFileAction.None;
        }
        bool SaveCurrentChart() => needsSaveAs ? SaveChartAs() : TrySaveChart(filePath);
        bool SaveChartAs()
        {
            string path = ChooseChartFile(true);
            return !string.IsNullOrEmpty(path) && TrySaveChart(path);
        }
        bool TrySaveChart(string path)
        {
            try
            {
                path = Path.GetFullPath(path);
                Normalize(); string json = JsonUtility.ToJson(chart, true);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json, new UTF8Encoding(false));
                filePath = path; needsSaveAs = false; savedChartJson = JsonUtility.ToJson(chart);
                try { ChartLoader.Parse(json); SetStatus("Saved " + Path.GetFileName(path) + " · gameplay validation passed"); }
                catch (Exception e) { SetStatus("Draft saved; validation: " + e.Message); }
                return true;
            }
            catch (Exception e) { SetStatus("Save failed: " + e.Message); return false; }
        }
        bool TryLoadChart(string path)
        {
            // Parse and evaluate an independent candidate before replacing the open chart.
            ChartData previous = chart;
            int previousStage = selectedStagePoint, previousCamera = selectedCameraKey, previousPath = selectedPath, previousSection = selectedSection;
            try
            {
                var candidate = JsonUtility.FromJson<ChartData>(File.ReadAllText(path));
                if (candidate == null || candidate.paths == null || candidate.paths.Length == 0)
                    throw new InvalidDataException("Not a chart JSON file");
                CheckEditableChart(candidate);
                chart = candidate;
                EnsureData(); Normalize();
                var candidateTempo = new TempoMap(chart.tempos, chart.ticksPerBeat);
                var candidateSpatial = new SpatialDirector(chart, candidateTempo);
                candidateSpatial.EvaluateCamera(evaluatorCamera, 0);
                foreach (var p in chart.paths) candidateSpatial.Point(p.id, SpatialDirector.NearDepth, 0);
                chart = previous;
                if (chartCameraPreview) SetPreviewMode(false);
                if (audioSource != null) SetPlaying(false);
                chart = candidate; CancelTimelineGesture(); timelineStart = 0; timelineZoom = 1;
                selectedStagePoint = selectedCameraKey = selectedPath = selectedSection = 0; selectedNote = -1;
                undo.Clear(); redo.Clear(); songTime = 0; Rebuild(); SetupAudio();
                filePath = Path.GetFullPath(path); needsSaveAs = false; savedChartJson = JsonUtility.ToJson(chart);
                SetStatus("Loaded " + Path.GetFileName(filePath)); return true;
            }
            catch (Exception e)
            {
                chart = previous; selectedStagePoint = previousStage; selectedCameraKey = previousCamera;
                selectedPath = previousPath; selectedSection = previousSection;
                SetStatus("Load failed: " + e.Message); return false;
            }
        }
        static void CheckEditableChart(ChartData candidate)
        {
            // Drafts can have no notes or an unfinished route. Reject corrupt structures,
            // but leave the stricter gameplay checks to Validate, as with draft saving.
            if (candidate.stagePath?.points != null && Array.Exists(candidate.stagePath.points, p => p == null))
                throw new InvalidDataException("Invalid stage point");
            if (candidate.paths != null && Array.Exists(candidate.paths, p => p == null || string.IsNullOrEmpty(p.id) ||
                (p.offsetKeys != null && Array.Exists(p.offsetKeys, k => k == null)))) throw new InvalidDataException("Invalid path");
            if (candidate.tempos != null && Array.Exists(candidate.tempos, t => t == null || t.bpm <= 0 || float.IsNaN(t.bpm) || float.IsInfinity(t.bpm)))
                throw new InvalidDataException("Invalid tempo");
            if (candidate.cameraKeys != null && Array.Exists(candidate.cameraKeys, k => k == null)) throw new InvalidDataException("Invalid camera key");
            if (candidate.sections != null && Array.Exists(candidate.sections, s => s == null || s.placements == null || Array.Exists(s.placements, p => p == null)))
                throw new InvalidDataException("Invalid layout section");
            if (candidate.notes != null && Array.Exists(candidate.notes, n => n == null)) throw new InvalidDataException("Invalid note");
            if (candidate.sceneObjects != null && Array.Exists(candidate.sceneObjects, o => o == null)) throw new InvalidDataException("Invalid scene object");
            if (candidate.effectClips != null && Array.Exists(candidate.effectClips, e => e == null)) throw new InvalidDataException("Invalid effect clip");
            if (candidate.cameraMotionClips != null && Array.Exists(candidate.cameraMotionClips, m => m == null ||
                (m.keys != null && Array.Exists(m.keys, k => k == null)))) throw new InvalidDataException("Invalid camera motion clip");
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        sealed class ChartFileDialog
        {
            public int size = Marshal.SizeOf(typeof(ChartFileDialog));
            public IntPtr owner, instance;
            public string filter = "Chart files (*.json)\0*.json\0All files (*.*)\0*.*\0\0";
            public IntPtr customFilter;
            public int maxCustomFilter, filterIndex = 1;
            public IntPtr file;
            public int maxFile = 4096;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public string initialDirectory, title;
            public int flags;
            public short fileOffset, extensionOffset;
            public string defaultExtension = "json";
            public IntPtr customData, hook, templateName, reserved;
            public int reservedValue, flagsEx;
        }
        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetOpenFileNameW")]
        [return: MarshalAs(UnmanagedType.Bool)] static extern bool OpenChartDialog([In, Out] ChartFileDialog dialog);
        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetSaveFileNameW")]
        [return: MarshalAs(UnmanagedType.Bool)] static extern bool SaveChartDialog([In, Out] ChartFileDialog dialog);
        [DllImport("comdlg32.dll")] static extern int CommDlgExtendedError();
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
#endif
        string ChooseChartFile(bool save)
        {
            ReleaseMouse(); CancelTimelineGesture(); SetPlaying(false); clearGuiFocus = true;
            fileDialogOpen = true;
            try
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                var dialog = new ChartFileDialog
                {
                    owner = GetActiveWindow(), title = save ? "Save chart as" : "Load chart",
                    initialDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath)),
                    flags = 0x00080000 | 0x00000008 | 0x00000800 | (save ? 0x00000002 : 0x00001000)
                };
                dialog.file = Marshal.AllocHGlobal(dialog.maxFile * 2);
                try
                {
                    var buffer = new char[dialog.maxFile];
                    string name = save ? (needsSaveAs ? "Untitled.json" : Path.GetFileName(filePath)) : "";
                    name.CopyTo(0, buffer, 0, Math.Min(name.Length, buffer.Length - 1));
                    Marshal.Copy(buffer, 0, dialog.file, buffer.Length);
                    bool accepted = save ? SaveChartDialog(dialog) : OpenChartDialog(dialog);
                    if (accepted) return Marshal.PtrToStringUni(dialog.file);
                    int error = CommDlgExtendedError();
                    if (error != 0) SetStatus("File dialog failed (" + error + ")");
                }
                finally { Marshal.FreeHGlobal(dialog.file); }
#else
                SetStatus("The desktop file picker requires Windows.");
#endif
                return null;
            }
            catch (Exception error) { SetStatus("File dialog failed: " + error.Message); return null; }
            finally { fileDialogOpen = false; }
        }

        void SetPreviewMode(bool enabled)
        {
            if (chartCameraPreview == enabled) return;
            draggingHandle = false; ReleaseMouse(); CancelTimelineGesture(); clearGuiFocus = true;
            SetPlaying(false);
            if (enabled)
            {
                previewEditorPosition = sceneCamera.transform.position; previewEditorRotation = sceneCamera.transform.rotation;
                previewEditorFov = sceneCamera.fieldOfView; previewEditorPivot = orbitPivot;
                previewEditorYaw = orbitYaw; previewEditorPitch = orbitPitch; previewEditorDistance = orbitDistance;
                chartCameraPreview = true;
                if (songTime >= Duration) songTime = 0;
                UpdateViewportRect(); RebuildVisuals(); spatial.EvaluateCamera(sceneCamera, songTime);
                SetPlaying(true); SetStatus("Preview started at playhead · Esc returns to editing");
            }
            else
            {
                chartCameraPreview = false;
                sceneCamera.transform.SetPositionAndRotation(previewEditorPosition, previewEditorRotation);
                sceneCamera.fieldOfView = previewEditorFov; orbitPivot = previewEditorPivot;
                orbitYaw = previewEditorYaw; orbitPitch = previewEditorPitch; orbitDistance = previewEditorDistance;
                UpdateViewportRect(); RebuildVisuals(); SetStatus("Back to editing · view restored");
            }
        }
        void RefreshPreviewVisuals()
        {
            if (previewMotionRoot != null) { previewMotionRoot.gameObject.SetActive(false); Destroy(previewMotionRoot.gameObject); }
            previewMotionRoot = new GameObject("Preview paths and notes").transform;
            previewMotionRoot.SetParent(visualRoot, false);
            Transform root = visualRoot; visualRoot = previewMotionRoot;
            try
            {
                BuildNotePaths(); BuildNotes();
                foreach (var path in chart.paths)
                {
                    if (spatial.Visibility(path.id, songTime) <= .01f) continue;
                    Vector3 center = spatial.Point(path.id, SpatialDirector.NearDepth, songTime);
                    Vector3 tangent = spatial.Point(path.id, SpatialDirector.NearDepth + .12f, songTime) - center;
                    Quaternion frame = Quaternion.LookRotation(tangent, spatial.RouteRotationAt(spatial.DistanceAtTime(songTime) + SpatialDirector.NearDepth) * Vector3.up);
                    var ring = new Vector3[33];
                    for (int i = 0; i < ring.Length; i++)
                    {
                        float angle = i * Mathf.PI * 2 / (ring.Length - 1);
                        ring[i] = center + frame * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .58f;
                    }
                    Line("Hit plane " + path.id, pathMaterial, .055f, ring);
                }
            }
            finally { visualRoot = root; }
        }
        void DrawPreviewStatus()
        {
            GUI.Label(new Rect(18, ToolbarHeight + 12, ViewWidth - 36, 28),
                "PREVIEW  /  " + (playing ? "PLAYING" : songTime >= Duration ? "ENDED" : "PAUSED"), headingStyle);
            GUI.Label(new Rect(18, ToolbarHeight + 40, ViewWidth - 36, 32),
                "Camera + paths + notes · Visual preview, no scoring · Space play / pause · Esc exit", smallStyle);
        }
    }
}
