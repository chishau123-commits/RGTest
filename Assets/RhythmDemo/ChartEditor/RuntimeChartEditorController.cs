using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public enum ChartEditMode { Scene, Stage, Paths, Notes, Camera, Effects, CameraMotion, Map }

    public sealed class ChartEditorHandle : MonoBehaviour
    {
        public ChartEditMode mode;
        public int index;
    }

    /// <summary>
    /// Standalone desktop chart-authoring MVP. It deliberately uses runtime APIs only: the
    /// same scene can run in the editor or be shipped as a Windows application.
    /// </summary>
    public sealed partial class RuntimeChartEditorController : MonoBehaviour
    {
        const float ToolbarHeight = 48;
        const float MinimumPanelWidth = 250;
        const float MaximumPanelWidth = 520;
        const float SceneDetailWidth = 350;
        readonly Stack<string> undo = new Stack<string>();
        readonly Stack<string> redo = new Stack<string>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        readonly Dictionary<string, string> numericEdits = new Dictionary<string, string>();

        ChartData chart;
        TempoMap tempo;
        SpatialDirector spatial;
        Camera sceneCamera;
        Camera evaluatorCamera;
        Transform cameraTargetMarker;
        Transform[] pathPlacementHandles;
        AudioSource audioSource;
        AudioClip generatedAudio;
        Transform visualRoot;
        Material routeMaterial, cameraMaterial, pathMaterial, pointMaterial, selectedMaterial;
        Material mapA, mapB, noteMaterial;
        GUIStyle titleStyle, smallStyle, statusStyle, buttonStyle, selectedButtonStyle;
        GUIStyle panelStyle, fieldStyle, toolbarStyle, headingStyle;
        GUIStyle modeButtonStyle, selectedModeButtonStyle;

        ChartEditMode mode;
        int selectedStagePoint;
        int selectedCameraKey;
        int selectedPath;
        int selectedSection;
        int selectedNote = -1;
        bool noteDrag;
        bool noteProtected;
        bool playing;
        bool chartCameraPreview;
        bool showMap = true;
        bool showSettings;
        bool showGuide;
        bool draggingHandle;
        bool inspectorResizing;
        float panelWidth = 330;
        float inspectorResizeStartX, inspectorResizeStartWidth;
        double songTime;
        float orbitYaw = 22;
        float orbitPitch = 18;
        float orbitDistance = 34;
        Vector3 orbitPivot = new Vector3(0, 2, 20);
        string filePath;
        string status = "Ready";
        double statusUntil;
        bool smokeMode;
        string smokeDirectory;
        float uiScale = 1.25f;

        double Duration => tempo == null ? 0 : tempo.SecondsAtBeat(chart.endBeat);
        float CurrentBeat => tempo == null ? 0 : (float)tempo.BeatAtSeconds(songTime);
        float ViewWidth => Screen.width / uiScale;
        float ViewHeight => Screen.height / uiScale;
        float PanelWidth => panelWidth;
        bool SceneDetailVisible => !chartCameraPreview && mode == ChartEditMode.Scene &&
            selectedSceneObject >= 0 && chart != null && chart.sceneObjects != null && selectedSceneObject < chart.sceneObjects.Length;
        float RightPanelWidth => SceneDetailVisible ? Mathf.Min(SceneDetailWidth, Mathf.Max(260, ViewWidth - PanelWidth - 280)) : 0;
        float ViewportRight => ViewWidth - RightPanelWidth;
        bool PointerInViewport(Vector2 p)
        {
            if (WorkspaceInputBlocked || chartCameraPreview) return false;
            p /= uiScale;
            return p.x > PanelWidth && p.x < ViewportRight && p.y > ToolbarHeight && p.y < ViewHeight - TimelineHeight;
        }

        void Awake()
        {
            // The authoring tool is a resizable desktop window. Never inherit the game's
            // fullscreen preference or silently switch the user's display mode.
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            uiScale = Mathf.Clamp(PlayerPrefs.GetFloat("ChartStudio.UiScale", 1.25f), 1, 1.75f);
            timelineHeight = PlayerPrefs.GetFloat("ChartStudio.TimelineHeight", 310);
            panelWidth = Mathf.Clamp(PlayerPrefs.GetFloat("ChartStudio.PanelWidth", 330), MinimumPanelWidth, MaximumPanelWidth);
            filePath = Path.Combine(Application.persistentDataPath, "geometry-chart-draft.json");
            string[] args = Environment.GetCommandLineArgs();
            int fileArgument = Array.IndexOf(args, "-chart");
            if (fileArgument >= 0 && fileArgument + 1 < args.Length) filePath = args[fileArgument + 1];
            int scaleArgument = Array.IndexOf(args, "-chartEditorUiScale");
            if (scaleArgument >= 0 && scaleArgument + 1 < args.Length &&
                float.TryParse(args[scaleArgument + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float requestedScale))
                uiScale = Mathf.Clamp(requestedScale, 1, 1.75f);
            showSettings = Array.IndexOf(args, "-chartEditorOpenSettings") >= 0;
            smokeMode = Array.IndexOf(args, "-chartEditorSmoke") >= 0;
            int captureArgument = Array.IndexOf(args, "-chartEditorCapture");
            smokeDirectory = captureArgument >= 0 && captureArgument + 1 < args.Length
                ? args[captureArgument + 1] : Path.Combine(Application.persistentDataPath, "ChartEditorSmoke");
            CreateCamerasAndLight();
            CreateMaterials();
            if (File.Exists(filePath)) LoadFile(); else NewChart();
            if (smokeMode) StartCoroutine(SmokeTest());
        }

        void CreateCamerasAndLight()
        {
            sceneCamera = GetComponentInChildren<Camera>();
            if (sceneCamera == null)
            {
                var cameraObject = new GameObject("Editor Scene Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(transform, false);
                sceneCamera = cameraObject.GetComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(.055f, .06f, .075f);
            sceneCamera.nearClipPlane = .08f;
            sceneCamera.farClipPlane = 1000;
            evaluatorCamera = new GameObject("Camera path evaluator", typeof(Camera)).GetComponent<Camera>();
            evaluatorCamera.enabled = false;
            evaluatorCamera.transform.SetParent(transform, false);
            var lightObject = new GameObject("Editor light", typeof(Light));
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(45, -30, 0);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.15f;
            RenderSettings.ambientLight = new Color(.48f, .5f, .56f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        Material MakeMaterial(string name, Color color, bool unlit = false)
        {
            Shader shader = Shader.Find(unlit ? "Sprites/Default" : "Standard");
            var material = new Material(shader) { name = name, color = color };
            if (!unlit)
            {
                material.SetFloat("_Glossiness", .08f);
                material.SetFloat("_Metallic", 0);
            }
            ownedMaterials.Add(material);
            return material;
        }
        void CreateMaterials()
        {
            routeMaterial = MakeMaterial("Stage route", new Color(.2f, .82f, 1), true);
            cameraMaterial = MakeMaterial("Camera path", new Color(1, .53f, .2f), true);
            pathMaterial = MakeMaterial("Note path", new Color(.52f, .92f, .72f), true);
            pointMaterial = MakeMaterial("Control point", new Color(.22f, .7f, .9f));
            selectedMaterial = MakeMaterial("Selected point", new Color(1, .72f, .18f));
            mapA = MakeMaterial("Map dark", new Color(.22f, .25f, .31f));
            mapB = MakeMaterial("Map light", new Color(.38f, .42f, .49f));
            noteMaterial = MakeMaterial("Note marker", new Color(.27f, .62f, 1));
        }

        void NewChart()
        {
            if (chartCameraPreview) ToggleCameraPreview();
            if (audioSource != null) SetPlaying(false);
            CancelTimelineGesture(); timelineStart = 0; timelineZoom = 1;
            chart = new ChartData
            {
                version = 1,
                title = "Untitled 3D Chart",
                author = Environment.UserName,
                ticksPerBeat = 480,
                endBeat = 128,
                approachSeconds = 3.4f,
                audioResource = "",
                audioOffsetSeconds = 0,
                tempos = new[] { new TempoData { tick = 0, bpm = 120 } },
                stagePath = new StagePathData
                {
                    unitsPerSecond = 5,
                    points = new[]
                    {
                        StagePoint(0, 0, 0), StagePoint(0, 0, 80), StagePoint(12, 3, 160),
                        StagePoint(-8, 7, 240), StagePoint(5, 2, 340), StagePoint(0, 0, 450)
                    }
                },
                map = new MapData(),
                paths = new[]
                {
                    new PathData { id = "p0" }, new PathData { id = "p1" }, new PathData { id = "p2" }
                },
                sections = new[]
                {
                    new SectionData { startBeat = 0, name = "INTRO", placements = new[]
                    {
                        Placement("p0", -4), Placement("p1", 0), Placement("p2", 4)
                    }}
                },
                cameraKeys = new[] { new CameraKey { beat = 0, distance = 25, height = 6, fov = 53 } },
                notes = new NoteData[0],
                sceneObjects = new SceneObjectData[0],
                effectClips = new EffectClipData[0],
                cameraMotionClips = new CameraMotionClipData[0]
            };
            selectedStagePoint = selectedCameraKey = selectedPath = selectedSection = 0;
            selectedNote = selectedSceneObject = selectedEffectClip = selectedMotionClip = -1; songTime = 0; undo.Clear(); redo.Clear();
            Rebuild("New chart created"); SetupAudio();
            savedChartJson = JsonUtility.ToJson(chart); needsSaveAs = true;
        }
        static StagePointData StagePoint(float x, float y, float z)
            => new StagePointData { x = x, y = y, z = z };
        static PathPlacement Placement(string id, float x)
            => new PathPlacement { pathId = id, x = x, y = 0, bend = 0, lift = 2 };

        void EnsureData()
        {
            if (chart.version == 0) chart.version = 1;
            if (chart.ticksPerBeat <= 0) chart.ticksPerBeat = 480;
            if (chart.endBeat <= 0) chart.endBeat = 128;
            if (chart.approachSeconds <= 0) chart.approachSeconds = 3.4f;
            if (chart.tempos == null || chart.tempos.Length == 0) chart.tempos = new[] { new TempoData { tick = 0, bpm = 120 } };
            if (chart.stagePath == null || chart.stagePath.points == null || chart.stagePath.points.Length < 2)
                chart.stagePath = new StagePathData { unitsPerSecond = 5, points = new[] { StagePoint(0, 0, 0), StagePoint(0, 0, 250) } };
            if (chart.stagePath.unitsPerSecond <= 0) chart.stagePath.unitsPerSecond = 5;
            if (chart.map == null) chart.map = new MapData();
            if (chart.paths == null || chart.paths.Length == 0) chart.paths = new[] { new PathData { id = "p0" } };
            if (chart.sections == null || chart.sections.Length == 0)
                chart.sections = new[] { new SectionData { startBeat = 0, name = "SECTION", placements = new[] { Placement(chart.paths[0].id, 0) } } };
            if (chart.cameraKeys == null || chart.cameraKeys.Length == 0) chart.cameraKeys = new[] { new CameraKey { beat = 0, distance = 25, height = 6, fov = 53 } };
            if (chart.notes == null) chart.notes = new NoteData[0];
            if (chart.sceneObjects == null) chart.sceneObjects = new SceneObjectData[0];
            if (chart.effectClips == null) chart.effectClips = new EffectClipData[0];
            if (chart.cameraMotionClips == null) chart.cameraMotionClips = new CameraMotionClipData[0];
            selectedStagePoint = Mathf.Clamp(selectedStagePoint, 0, chart.stagePath.points.Length - 1);
            selectedCameraKey = Mathf.Clamp(selectedCameraKey, 0, chart.cameraKeys.Length - 1);
            selectedPath = Mathf.Clamp(selectedPath, 0, chart.paths.Length - 1);
            selectedSection = Mathf.Clamp(selectedSection, 0, chart.sections.Length - 1);
            selectedNote = Mathf.Clamp(selectedNote, -1, chart.notes.Length - 1);
            selectedSceneObject = Mathf.Clamp(selectedSceneObject, -1, chart.sceneObjects.Length - 1);
            selectedEffectClip = Mathf.Clamp(selectedEffectClip, -1, chart.effectClips.Length - 1);
            selectedMotionClip = Mathf.Clamp(selectedMotionClip, -1, chart.cameraMotionClips.Length - 1);
        }

        void SetupAudio()
        {
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.Stop();
            if (generatedAudio != null) Destroy(generatedAudio);
            generatedAudio = DemoSoundtrack.Create((float)Math.Max(1, Duration));
            audioSource.clip = generatedAudio;
            audioSource.playOnAwake = false;
            audioSource.volume = .4f;
            BuildTimelineWaveform();
        }

        void Rebuild(string message = null)
        {
            EnsureData();
            tempo = new TempoMap(chart.tempos, chart.ticksPerBeat);
            spatial = new SpatialDirector(chart, tempo);
            songTime = Math.Max(0, Math.Min(Duration, songTime));
            RebuildVisuals();
            if (message != null) SetStatus(message);
        }

        void RebuildVisuals()
        {
            timelineCurveRevision++;
            authoredVisuals?.Dispose(); authoredVisuals = null;
            if (visualRoot != null) { visualRoot.gameObject.SetActive(false); Destroy(visualRoot.gameObject); }
            visualRoot = new GameObject("Chart editor visuals").transform;
            visualRoot.SetParent(transform, false);
            cameraTargetMarker = null;
            pathPlacementHandles = null;
            editorLivePaths = null; editorPlayheadLine = null; editorPlayheadVerticalLine = null;
            if (chartCameraPreview)
            {
                if (showMap) BuildMap();
                RefreshPreviewVisuals(); BuildAuthoredVisuals(); return;
            }
            BuildRoute();
            BuildStageHandles();
            BuildCameraPath();
            BuildNotePaths();
            BuildNotes();
            BuildEditorPlaybackVisuals();
            if (showMap) BuildMap();
            BuildAuthoredVisuals();
        }

        LineRenderer Line(string name, Material material, float width, Vector3[] points)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(visualRoot, false);
            var line = go.GetComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.widthMultiplier = width;
            line.positionCount = points.Length; line.SetPositions(points);
            line.numCornerVertices = 3; line.numCapVertices = 3;
            return line;
        }
        void BuildRoute()
        {
            float length = Mathf.Min(spatial.RouteLength, (float)Duration * spatial.UnitsPerSecond + 125);
            if (float.IsInfinity(length)) length = (float)Duration * spatial.UnitsPerSecond + 125;
            int count = Mathf.Clamp(Mathf.CeilToInt(length / 2), 32, 400);
            var points = new Vector3[count];
            for (int i = 0; i < count; i++) points[i] = spatial.RouteAt(length * i / (count - 1f));
            Line("Stage master spline", routeMaterial, .18f, points);
        }
        GameObject Handle(string name, Vector3 position, float scale, ChartEditMode handleMode, int index, bool selected)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name; go.transform.SetParent(visualRoot, false); go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<Renderer>().sharedMaterial = selected ? selectedMaterial : pointMaterial;
            var marker = go.AddComponent<ChartEditorHandle>(); marker.mode = handleMode; marker.index = index;
            return go;
        }
        void BuildStageHandles()
        {
            for (int i = 0; i < chart.stagePath.points.Length; i++)
            {
                var p = chart.stagePath.points[i];
                Handle("Stage point " + i, new Vector3(p.x, p.y, p.z), 1.25f, ChartEditMode.Stage, i,
                    mode == ChartEditMode.Stage && i == selectedStagePoint);
            }
        }
        void BuildCameraPath()
        {
            if (chart.cameraKeys.Length == 0) return;
            int count = Math.Max(2, chart.cameraKeys.Length * 12);
            var points = new Vector3[count];
            float end = chart.cameraKeys[chart.cameraKeys.Length - 1].beat;
            for (int i = 0; i < count; i++)
            {
                double time = tempo.SecondsAtBeat(end * i / (count - 1f));
                spatial.EvaluateCamera(evaluatorCamera, time); points[i] = evaluatorCamera.transform.position;
            }
            Line("Camera path", cameraMaterial, .045f, points);
            for (int i = 0; i < chart.cameraKeys.Length; i++)
            {
                spatial.EvaluateCamera(evaluatorCamera, tempo.SecondsAtBeat(chart.cameraKeys[i].beat));
                Handle("Camera key " + i, evaluatorCamera.transform.position, .75f, ChartEditMode.Camera, i,
                    mode == ChartEditMode.Camera && i == selectedCameraKey);
            }
            cameraTargetMarker = null;
            if (mode == ChartEditMode.Camera)
            {
                var target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                target.name = "Camera placement marker"; target.transform.SetParent(visualRoot, false);
                target.transform.position = PlacementMarkerPosition; target.transform.localScale = Vector3.one * .72f;
                target.GetComponent<Renderer>().sharedMaterial = selectedMaterial;
                Destroy(target.GetComponent<Collider>()); cameraTargetMarker = target.transform;
            }
        }
        void BuildNotePaths()
        {
            for (int path = 0; path < chart.paths.Length; path++)
            {
                if (chartCameraPreview && spatial.Visibility(chart.paths[path].id, songTime) <= .01f) continue;
                int count = chartCameraPreview ? 90 : Mathf.Clamp(Mathf.CeilToInt((float)Duration * spatial.UnitsPerSecond / 2) + 1, 90, 1500);
                var points = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    float fraction = i / (count - 1f);
                    points[i] = chartCameraPreview
                        ? spatial.Point(chart.paths[path].id, Mathf.Lerp(SpatialDirector.NearDepth - 2, SpatialDirector.FarDepth, fraction), songTime)
                        : spatial.Point(chart.paths[path].id, SpatialDirector.NearDepth, Duration * fraction);
                }
                Line("Note path " + chart.paths[path].id, !chartCameraPreview && path == selectedPath ? selectedMaterial : pathMaterial,
                    !chartCameraPreview && path == selectedPath ? .12f : .065f, points);
            }
            if (mode != ChartEditMode.Paths || chartCameraPreview) return;
            pathPlacementHandles = new Transform[chart.paths.Length];
            float distance = spatial.OffsetDistance(PlayheadTick);
            for (int i = 0; i < chart.paths.Length; i++)
            {
                var p = chart.paths[i];
                var offset = spatial.OffsetAtDistance(p.id, distance);
                pathPlacementHandles[i] = Handle("Path placement " + p.id, spatial.RoutePoint(distance, offset.x, offset.y), .85f,
                    ChartEditMode.Paths, i, selectedPath == i).transform;
            }
        }
        void RefreshEditorPlayheadVisuals()
        {
            // Update only moving overlays. The permanent paths, notes and map stay intact.
            RefreshEditorPlaybackVisuals();
            if (cameraTargetMarker != null) cameraTargetMarker.position = PlacementMarkerPosition;
            if (pathPlacementHandles == null) return;
            float distance = spatial.OffsetDistance(PlayheadTick);
            for (int i = 0; i < pathPlacementHandles.Length; i++)
            {
                if (pathPlacementHandles[i] == null) continue;
                Vector2 offset = spatial.OffsetAtDistance(chart.paths[i].id, distance);
                pathPlacementHandles[i].position = spatial.RoutePoint(distance, offset.x, offset.y);
            }
        }
        double NoteHitTime(NoteData note) => tempo.SecondsAtBeat(note.tick / (double)chart.ticksPerBeat);
        void EditorNotePose(NoteData note, out Vector3 position, out Quaternion rotation)
        {
            double hit = NoteHitTime(note);
            // The permanent authoring position is the same world-space hit position
            // reached in Preview, evaluated at THIS note's time, never at the playhead.
            spatial.NotePose(new RuntimeNote { Data = note, HitTime = hit }, hit, out position, out rotation);
        }
        void BuildNotes()
        {
            for (int i = 0; i < chart.notes.Length; i++)
            {
                double hit = NoteHitTime(chart.notes[i]);
                if (chartCameraPreview && (hit < songTime || hit > songTime + chart.approachSeconds)) continue;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = chart.notes[i].id; go.transform.SetParent(visualRoot, false);
                Vector3 position; Quaternion rotation;
                if (chartCameraPreview)
                    spatial.NotePose(new RuntimeNote { Data = chart.notes[i], HitTime = hit }, songTime, out position, out rotation);
                else
                {
                    EditorNotePose(chart.notes[i], out position, out rotation);
                    var marker = go.AddComponent<ChartEditorHandle>(); marker.mode = ChartEditMode.Notes; marker.index = i;
                }
                go.transform.SetPositionAndRotation(position, rotation * Quaternion.Euler(90, 0, 0));
                go.transform.localScale = new Vector3(.65f, .08f, .65f);
                go.GetComponent<Renderer>().sharedMaterial = chartCameraPreview ?
                    (chart.notes[i].protectedNote ? selectedMaterial : chart.notes[i].action == "drag" ? pathMaterial : noteMaterial) :
                    i == selectedNote ? selectedMaterial : noteMaterial;
                Destroy(go.GetComponent<Collider>());
            }
        }
        void BuildMap()
        {
            var random = new System.Random(chart.map.seed);
            float length = Mathf.Min(spatial.RouteLength, (float)Duration * spatial.UnitsPerSecond + 100);
            if (float.IsInfinity(length)) length = (float)Duration * spatial.UnitsPerSecond + 100;
            int chunks = Mathf.Clamp(Mathf.CeilToInt(length / 14 * chart.map.density), 0, 180);
            for (int i = 0; i < chunks; i++)
            {
                float distance = chunks <= 1 ? 0 : length * i / (chunks - 1f);
                int side = i % 2 == 0 ? -1 : 1;
                float lateral = side * (chart.map.corridorWidth + 5 + (float)random.NextDouble() * 18);
                float height = 4 + (float)random.NextDouble() * chart.map.heightVariation;
                var block = GameObject.CreatePrimitive(i % 5 == 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                block.name = "Generated map chunk " + i; block.transform.SetParent(visualRoot, false);
                block.transform.position = spatial.RoutePoint(distance, lateral, height * .5f - 3);
                block.transform.rotation = spatial.RouteRotationAt(distance) * Quaternion.Euler(
                    (float)random.NextDouble() * 15, (float)random.NextDouble() * 90, side * (5 + (float)random.NextDouble() * 20));
                block.transform.localScale = new Vector3(3 + (float)random.NextDouble() * 8, height, 3 + (float)random.NextDouble() * 10);
                block.GetComponent<Renderer>().sharedMaterial = i % 3 == 0 ? mapB : mapA;
                Destroy(block.GetComponent<Collider>());
            }
        }

        void Update()
        {
            if (tempo == null) return;
            UpdateViewportRect();
            ReadNavigationMode();
            ReadKeyboard();
            ReadLockedSceneSelection();
            if (playing)
            {
                songTime += Time.unscaledDeltaTime;
                if (songTime >= Duration) { songTime = Duration; SetPlaying(false); }
                if (playing && audioSource != null && !audioSource.isPlaying && songTime < audioSource.clip.length) StartAudioAtCurrentTime();
                if (chartCameraPreview) RefreshPreviewVisuals(); else RefreshEditorPlayheadVisuals();
            }
            UpdateTimelineEdgeScroll(Time.unscaledDeltaTime);
            if (chartCameraPreview) { spatial.EvaluateCamera(sceneCamera, songTime); CameraMotionEvaluator.Apply(chart, tempo, sceneCamera, songTime); }
            else { ReadSceneNavigation(); if (!flyMode) ApplyOrbitCamera(); }
            authoredVisuals?.Evaluate(songTime);
            if (cameraTargetMarker != null) cameraTargetMarker.position = PlacementMarkerPosition;
        }

        void ReadKeyboard()
        {
            if (textInputFocused || WorkspaceInputBlocked || Cursor.lockState == CursorLockMode.Locked || chartCameraPreview) return;
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (control && Input.GetKeyDown(KeyCode.Z)) Undo();
            if (control && Input.GetKeyDown(KeyCode.Y)) Redo();
            if (!flyMode && Input.GetKeyDown(KeyCode.Space)) SetPlaying(!playing);
            if (Input.GetKeyDown(KeyCode.F)) FocusSelection();
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetMode(ChartEditMode.Scene);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetMode(ChartEditMode.Stage);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetMode(ChartEditMode.Paths);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetMode(ChartEditMode.Notes);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SetMode(ChartEditMode.Camera);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SetMode(ChartEditMode.Effects);
            if (Input.GetKeyDown(KeyCode.Alpha7)) SetMode(ChartEditMode.CameraMotion);
            if (Input.GetKeyDown(KeyCode.Alpha8)) SetMode(ChartEditMode.Map);
            if (Input.GetKeyDown(KeyCode.Delete)) DeleteSelection();
        }
        void ReadSceneNavigation()
        {
            if (inspectorResizing || timelineResizing || timelineScrubbing || timelineDragItem != null) return;
            if (flyMode) { ReadFlight(); return; }
            if (draggingHandle) return;
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (!PointerInViewport(mouse)) return;
            if (Input.GetMouseButton(1))
            {
                orbitYaw += Input.GetAxis("Mouse X") * 3;
                orbitPitch = Mathf.Clamp(orbitPitch - Input.GetAxis("Mouse Y") * 3, -80, 85);
            }
            if (Input.GetMouseButton(2))
            {
                float scale = orbitDistance * .0025f;
                orbitPivot -= sceneCamera.transform.right * Input.GetAxis("Mouse X") * scale * 20;
                orbitPivot -= sceneCamera.transform.up * Input.GetAxis("Mouse Y") * scale * 20;
            }
        }
        void ApplyOrbitCamera()
        {
            Quaternion rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0);
            sceneCamera.transform.SetPositionAndRotation(orbitPivot - rotation * Vector3.forward * orbitDistance, rotation);
        }
        void ReadHandles(Event pointerEvent)
        {
            // Process queued pointer events rather than sampling buttons once per frame:
            // short clicks/drags must not disappear during a costly scene rebuild.
            if (pointerEvent.button != 0) return;
            if (pointerEvent.type == EventType.MouseUp)
            {
                if (draggingHandle) pointerEvent.Use();
                draggingHandle = false; return;
            }
            if (Cursor.lockState == CursorLockMode.Locked || playing || chartCameraPreview || inspectorResizing) return;
            Vector2 guiMouse = pointerEvent.mousePosition;
            Ray ray = sceneCamera.ScreenPointToRay(new Vector3(guiMouse.x, Screen.height - guiMouse.y, 0));
            if (draggingHandle && pointerEvent.type == EventType.MouseDrag)
            {
                UpdateGizmoDrag(ray, guiMouse); pointerEvent.Use(); return;
            }
            if (!PointerInViewport(guiMouse)) return;
            if (pointerEvent.type == EventType.MouseDown)
            {
                clearGuiFocus = true;
                int axis = HitGizmo(guiMouse / uiScale);
                if (axis >= 0) { BeginGizmoDrag(axis, ray, guiMouse); pointerEvent.Use(); return; }
                // A forgiving screen-space radius makes faraway control points selectable.
                ChartEditorHandle marker = PickHandle(guiMouse);
                if (marker != null)
                {
                    SelectHandle(marker); pointerEvent.Use();
                }
                else if (mode == ChartEditMode.Scene)
                {
                    SelectSceneObject(ray, true); pointerEvent.Use();
                }
                else if (mode == ChartEditMode.Stage && pointerEvent.shift)
                {
                    Plane ground = new Plane(Vector3.up, Vector3.zero);
                    if (ground.Raycast(ray, out float enter)) { AddStagePoint(ray.GetPoint(enter)); pointerEvent.Use(); }
                }
            }
        }
        void ReadLockedSceneSelection()
        {
            if (mode != ChartEditMode.Scene || !flyMode || Cursor.lockState != CursorLockMode.Locked ||
                WorkspaceInputBlocked || chartCameraPreview || playing || !Input.GetMouseButtonDown(0)) return;
            if (SelectSceneObject(sceneCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0)), false)) ReleaseMouse();
        }
        bool SelectSceneObject(Ray ray, bool clearWhenMiss)
        {
            if (authoredVisuals != null && authoredVisuals.TryPickSceneObject(ray, out SceneObjectData picked))
            {
                selectedSceneObject = Array.FindIndex(chart.sceneObjects, item => ReferenceEquals(item, picked) || item.id == picked.id);
                EnsureVisualLibrary(); int assetIndex = sceneLibrary.FindIndex(item => item.id == picked.assetId);
                if (assetIndex >= 0) selectedSceneAsset = assetIndex;
                draggingHandle = false; SetStatus("Selected scene object: " + picked.name); UpdateViewportRect(); return selectedSceneObject >= 0;
            }
            if (clearWhenMiss && !BackdropSelected()) { selectedSceneObject = -1; draggingHandle = false; UpdateViewportRect(); }
            return false;
        }
        /// <summary>A backdrop fills the frame and is excluded from picking on purpose, so clicking
        /// past it must not clear the selection: the inspector is the only way back to its toggle.</summary>
        bool BackdropSelected()
            => chart.sceneObjects != null && selectedSceneObject >= 0 && selectedSceneObject < chart.sceneObjects.Length
                && Backdrop.IsBackdrop(chart.sceneObjects[selectedSceneObject]);
        void SelectHandle(ChartEditorHandle marker)
        {
            mode = marker.mode;
            if (marker.mode == ChartEditMode.Stage) selectedStagePoint = marker.index;
            else if (marker.mode == ChartEditMode.Camera) selectedCameraKey = marker.index;
            else if (marker.mode == ChartEditMode.Paths) selectedPath = marker.index;
            else if (marker.mode == ChartEditMode.Notes)
            {
                selectedNote = marker.index;
                selectedPath = Mathf.Max(0, Array.FindIndex(chart.paths, p => p.id == chart.notes[selectedNote].pathId));
            }
            RebuildVisuals();
        }

        void SetPlaying(bool value)
        {
            if (value && songTime >= Duration) Seek(0);
            playing = value;
            if (!playing) audioSource.Pause(); else StartAudioAtCurrentTime();
            if (!chartCameraPreview && spatial != null) RefreshEditorPlayheadVisuals();
        }
        void StartAudioAtCurrentTime()
        {
            if (audioSource == null || audioSource.clip == null) return;
            float audioTime = (float)songTime + chart.audioOffsetSeconds;
            if (audioTime < 0 || audioTime >= audioSource.clip.length) return;
            audioSource.time = audioTime; audioSource.Play();
        }
        void Seek(double value, bool rebuildStaticVisuals = true)
        {
            draggingHandle = false;
            songTime = Math.Max(0, Math.Min(Duration, value));
            if (playing) StartAudioAtCurrentTime();
            if (rebuildStaticVisuals) RebuildVisuals();
            else if (chartCameraPreview) RefreshPreviewVisuals();
            else RefreshEditorPlayheadVisuals();
        }

        void RecordUndo()
        {
            undo.Push(JsonUtility.ToJson(chart)); redo.Clear();
            while (undo.Count > 60)
            {
                var values = undo.ToArray(); undo.Clear();
                for (int i = values.Length - 2; i >= 0; i--) undo.Push(values[i]);
            }
        }
        void Undo()
        {
            CancelTimelineGesture();
            draggingHandle = false;
            if (undo.Count == 0) return;
            redo.Push(JsonUtility.ToJson(chart)); chart = JsonUtility.FromJson<ChartData>(undo.Pop()); Rebuild("Undo");
        }
        void Redo()
        {
            CancelTimelineGesture();
            draggingHandle = false;
            if (redo.Count == 0) return;
            undo.Push(JsonUtility.ToJson(chart)); chart = JsonUtility.FromJson<ChartData>(redo.Pop()); Rebuild("Redo");
        }
        void Change(Action action, string message)
        {
            RecordUndo(); action(); Rebuild(message);
        }

        void AddStagePoint(Vector3 world)
        {
            Change(() =>
            {
                var list = new List<StagePointData>(chart.stagePath.points);
                int insert = Mathf.Clamp(selectedStagePoint + 1, 0, list.Count);
                list.Insert(insert, StagePoint(world.x, world.y, world.z));
                chart.stagePath.points = list.ToArray(); selectedStagePoint = insert;
            }, "Stage point added");
        }
        void AddCameraKey()
        {
            Change(() =>
            {
                var list = new List<CameraKey>(chart.cameraKeys);
                float snappedBeat = Mathf.Round(CurrentBeat * chart.ticksPerBeat) / (float)chart.ticksPerBeat;
                var previous = list.FindLast(k => k.beat <= snappedBeat);
                var key = new CameraKey { beat = snappedBeat, fov = sceneCamera.fieldOfView, usePathPose = true,
                    easing = previous == null ? SpatialDirector.CameraEaseInOut : previous.easing };
                CapturePose(key);
                int existing = list.FindIndex(k => Mathf.Abs(k.beat - snappedBeat) < .5f / chart.ticksPerBeat);
                if (existing >= 0) { key.easing = list[existing].easing; list[existing] = key; } else list.Add(key);
                list.Sort((a, b) => a.beat.CompareTo(b.beat));
                chart.cameraKeys = list.ToArray(); selectedCameraKey = Array.IndexOf(chart.cameraKeys, key);
            }, "Camera key placed at yellow marker (same-tick key replaced)");
        }
        void CaptureSelectedCamera()
        {
            Change(() => CapturePose(chart.cameraKeys[selectedCameraKey]), "Camera key moved to yellow marker");
        }
        void CapturePose(CameraKey key)
        {
            key.usePathPose = false; key.useWorldPose = true;
            key.worldPosition = PlacementMarkerPosition;
            key.worldTarget = key.worldPosition + sceneCamera.transform.forward * 20;
            key.fov = sceneCamera.fieldOfView;
        }
        void AddSection()
        {
            timelineSelection = TimelineKind.Section;
            Change(() =>
            {
                float snappedBeat = Mathf.Round(CurrentBeat * chart.ticksPerBeat) / (float)chart.ticksPerBeat;
                int existing = Array.FindIndex(chart.sections,
                    s => Mathf.Abs(s.startBeat - snappedBeat) < .5f / chart.ticksPerBeat);
                if (existing >= 0) { selectedSection = existing; return; }
                var source = chart.sections[selectedSection];
                var placements = JsonUtility.FromJson<SectionData>(JsonUtility.ToJson(source)).placements;
                var section = new SectionData { startBeat = snappedBeat, name = "SECTION " + (chart.sections.Length + 1), placements = placements };
                var list = new List<SectionData>(chart.sections); list.Add(section); list.Sort((a, b) => a.startBeat.CompareTo(b.startBeat));
                chart.sections = list.ToArray(); selectedSection = Array.IndexOf(chart.sections, section);
            }, "Path section added (existing same-tick section kept)");
        }
        void AddPath()
        {
            Change(() =>
            {
                int suffix = chart.paths.Length;
                string id; do { id = "p" + suffix++; } while (Array.Exists(chart.paths, p => p.id == id));
                var paths = new List<PathData>(chart.paths) { new PathData { id = id } }; chart.paths = paths.ToArray();
                foreach (var section in chart.sections)
                {
                    var placements = new List<PathPlacement>(section.placements) { Placement(id, (paths.Count - 1) * 3) };
                    section.placements = placements.ToArray();
                }
                selectedPath = chart.paths.Length - 1;
            }, "Note path added");
        }
        void AddNote()
        {
            Change(() =>
            {
                int tick = Mathf.RoundToInt(CurrentBeat * chart.ticksPerBeat);
                string id = "n" + Guid.NewGuid().ToString("N").Substring(0, 10);
                var list = new List<NoteData>(chart.notes)
                {
                    new NoteData { id = id, tick = tick, pathId = chart.paths[selectedPath].id,
                        action = noteDrag ? "drag" : "tap", protectedNote = noteProtected }
                };
                list.Sort((a, b) => a.tick != b.tick ? a.tick.CompareTo(b.tick) : string.CompareOrdinal(a.id, b.id));
                chart.notes = list.ToArray(); selectedNote = chart.notes.FindIndex(n => n.id == id);
            }, "Note added");
        }
        void DeleteSelection()
        {
            if (mode == ChartEditMode.Paths) { DeleteTimelineSelection(); return; }
            if (mode == ChartEditMode.Stage && chart.stagePath.points.Length > 2)
                Change(() => { var list = new List<StagePointData>(chart.stagePath.points); list.RemoveAt(selectedStagePoint); chart.stagePath.points = list.ToArray(); selectedStagePoint = Mathf.Max(0, selectedStagePoint - 1); }, "Stage point deleted");
            else if (mode == ChartEditMode.Camera && chart.cameraKeys.Length > 1 && selectedCameraKey > 0)
                Change(() => { var list = new List<CameraKey>(chart.cameraKeys); list.RemoveAt(selectedCameraKey); chart.cameraKeys = list.ToArray(); selectedCameraKey = Mathf.Max(0, selectedCameraKey - 1); }, "Camera key deleted");
            else if (mode == ChartEditMode.Notes && selectedNote >= 0 && selectedNote < chart.notes.Length)
                Change(() => { var list = new List<NoteData>(chart.notes); list.RemoveAt(selectedNote); chart.notes = list.ToArray(); selectedNote = -1; }, "Note deleted");
        }

        void Normalize()
        {
            foreach (var path in chart.paths)
                if (path.offsetKeys != null) Array.Sort(path.offsetKeys, (a, b) => a.tick.CompareTo(b.tick));
            Array.Sort(chart.tempos, (a, b) => a.tick.CompareTo(b.tick));
            Array.Sort(chart.sections, (a, b) => a.startBeat.CompareTo(b.startBeat));
            Array.Sort(chart.cameraKeys, (a, b) => a.beat.CompareTo(b.beat));
            Array.Sort(chart.notes, (a, b) => a.tick != b.tick ? a.tick.CompareTo(b.tick) : string.CompareOrdinal(a.id, b.id));
            Array.Sort(chart.effectClips, (a, b) => a.startTick.CompareTo(b.startTick));
            Array.Sort(chart.cameraMotionClips, (a, b) => a.startTick.CompareTo(b.startTick));
        }
        void SaveFile()
        {
            SaveCurrentChart();
        }
        void LoadFile()
        {
            if (!TryLoadChart(filePath) && chart == null) NewChart();
        }
        void ValidateChart()
        {
            try { ChartLoader.Parse(JsonUtility.ToJson(chart)); SetStatus("Validation passed: chart is playable"); }
            catch (Exception e) { SetStatus("Validation failed: " + e.Message); }
        }
        void SetStatus(string value) { status = value; statusUntil = Time.realtimeSinceStartupAsDouble + 8; }
        void SetMode(ChartEditMode value)
        {
            CancelTimelineGesture(); timelineTrackScroll = 0;
            draggingHandle = false; mode = value; RebuildVisuals();
        }

        void OnGUI()
        {
            BuildStyles();
            ReadWorkspaceKeys(Event.current);
            DismissFilesMenu(Event.current);
            ReadInspectorResize(Event.current);
            ReadTimelinePointer(Event.current);
            if (!CameraCurvePointerOwns(Event.current)) ReadHandles(Event.current);
            if (clearGuiFocus) { GUI.FocusControl(null); clearGuiFocus = false; }
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !WorkspaceInputBlocked;
            DrawGizmo();
            DrawCameraCurveControls();
            if (!chartCameraPreview) { DrawInspector(); DrawSceneObjectDetails(); }
            DrawTimeline();
            if (chartCameraPreview) DrawPreviewStatus(); else DrawNavigationStatus();
            if (Time.realtimeSinceStartupAsDouble < statusUntil)
                GUI.Label(new Rect((chartCameraPreview ? 0 : PanelWidth) + 18, ViewHeight - TimelineHeight - 42,
                    ViewWidth - (chartCameraPreview ? 0 : PanelWidth) - RightPanelWidth - 36, 32), status, statusStyle);
            GUI.enabled = previousEnabled && !WorkspaceModalOpen;
            DrawToolbar();
            GUI.enabled = previousEnabled;
            if (showFilesMenu) DrawFilesMenu();
            if (showSettings) DrawSettings();
            if (showGuide) DrawGuide();
            if (pendingFileAction != PendingFileAction.None) DrawUnsavedConfirmation();
            textInputFocused = GUI.GetNameOfFocusedControl().StartsWith("Edit:", StringComparison.Ordinal);
            GUI.matrix = previousMatrix;
        }
        void BuildStyles()
        {
            if (titleStyle != null) return;
            var buttonNormal = RoundedTexture(new Color(.13f, .15f, .19f), 8);
            var buttonHover = RoundedTexture(new Color(.19f, .22f, .28f), 8);
            var buttonActive = RoundedTexture(new Color(.10f, .12f, .16f), 8);
            var selectedNormal = RoundedTexture(new Color(.12f, .48f, .72f), 8);
            var selectedHover = RoundedTexture(new Color(.16f, .58f, .84f), 8);
            var panel = RoundedTexture(new Color(.045f, .052f, .067f, .97f), 10);
            var toolbar = RoundedTexture(new Color(.035f, .04f, .052f, .99f), 4);
            var field = RoundedTexture(new Color(.075f, .086f, .11f), 7);
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter, fixedHeight = 31,
                padding = new RectOffset(12, 12, 5, 5), margin = new RectOffset(3, 3, 2, 2),
                border = new RectOffset(8, 8, 8, 8)
            };
            buttonStyle.normal.background = buttonNormal; buttonStyle.hover.background = buttonHover; buttonStyle.active.background = buttonActive;
            buttonStyle.normal.textColor = new Color(.88f, .91f, .96f); buttonStyle.hover.textColor = Color.white; buttonStyle.active.textColor = Color.white;
            selectedButtonStyle = new GUIStyle(buttonStyle);
            selectedButtonStyle.normal.background = selectedNormal; selectedButtonStyle.hover.background = selectedHover;
            selectedButtonStyle.normal.textColor = Color.white;
            modeButtonStyle = new GUIStyle(buttonStyle) { padding = new RectOffset(5, 5, 5, 5) };
            selectedModeButtonStyle = new GUIStyle(selectedButtonStyle) { padding = new RectOffset(5, 5, 5, 5) };
            panelStyle = new GUIStyle(GUI.skin.box) { border = new RectOffset(10, 10, 10, 10) }; panelStyle.normal.background = panel;
            toolbarStyle = new GUIStyle(panelStyle) { border = new RectOffset(4, 4, 4, 4) }; toolbarStyle.normal.background = toolbar;
            fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 13, fixedHeight = 29, padding = new RectOffset(9, 9, 5, 5), border = new RectOffset(7, 7, 7, 7)
            };
            fieldStyle.normal.background = field; fieldStyle.focused.background = field;
            fieldStyle.normal.textColor = new Color(.9f, .93f, .98f); fieldStyle.focused.textColor = Color.white;
            GUI.skin.button = buttonStyle; GUI.skin.box = panelStyle; GUI.skin.textField = fieldStyle;
            GUI.skin.label.normal.textColor = new Color(.84f, .87f, .92f); GUI.skin.label.fontSize = 13;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(.94f, .96f, 1);
            headingStyle = new GUIStyle(titleStyle) { fontSize = 15 };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            smallStyle.normal.textColor = new Color(.6f, .66f, .75f);
            statusStyle = new GUIStyle(panelStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            statusStyle.normal.textColor = Color.white;
            BuildMinimalScrollStyles();
        }
        Texture2D RoundedTexture(Color color, int radius)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - .5f, x - (size - radius) + .5f, 0);
                float dy = Mathf.Max(radius - y - .5f, y - (size - radius) + .5f, 0);
                float alpha = 1 - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) - radius + 1);
                pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
            }
            texture.SetPixels(pixels); texture.Apply(); ownedTextures.Add(texture); return texture;
        }
        void DrawToolbar()
        {
            GUI.Box(new Rect(0, 0, ViewWidth, ToolbarHeight), "", toolbarStyle);
            GUILayout.BeginArea(new Rect(10, 7, ViewWidth - 20, 38)); GUILayout.BeginHorizontal();
            GUILayout.Label("CHART STUDIO", ViewWidth < 950 ? headingStyle : titleStyle, GUILayout.Width(ViewWidth < 950 ? 130 : 164));
            bool previous = GUI.enabled; GUI.enabled = previous && !chartCameraPreview && !showFilesMenu;
            ModeButton("Scene", ChartEditMode.Scene); ModeButton("Stage", ChartEditMode.Stage);
            ModeButton("Paths", ChartEditMode.Paths); ModeButton("Notes", ChartEditMode.Notes); ModeButton("Camera", ChartEditMode.Camera);
            ModeButton("Effects", ChartEditMode.Effects); ModeButton("Motion", ChartEditMode.CameraMotion); ModeButton("Map", ChartEditMode.Map);
            if (mode == ChartEditMode.Scene)
            {
                GUILayout.Space(5);
                SceneToolButton(new GUIContent("↔", "Move selected object"), SceneTransformTool.Move);
                SceneToolButton(new GUIContent("⟳", "Rotate selected object"), SceneTransformTool.Rotate);
            }
            GUI.enabled = previous;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Files", showFilesMenu ? selectedButtonStyle : buttonStyle, GUILayout.Width(60)))
            { showFilesMenu = !showFilesMenu; ReleaseMouse(); CancelTimelineGesture(); clearGuiFocus = true; }
            if (GUILayout.Button("Settings", GUILayout.Width(78))) { showFilesMenu = false; showSettings = true; showGuide = false; }
            if (GUILayout.Button("Save", selectedButtonStyle, GUILayout.Width(60))) { showFilesMenu = false; SaveFile(); }
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }
        void ModeButton(string label, ChartEditMode value)
        {
            if (GUILayout.Button(label, mode == value ? selectedModeButtonStyle : modeButtonStyle, GUILayout.Width(ViewWidth < 1200 ? 57 : 68))) SetMode(value);
        }
        void SceneToolButton(GUIContent icon, SceneTransformTool tool)
        {
            if (GUILayout.Button(icon, sceneTransformTool == tool ? selectedModeButtonStyle : modeButtonStyle, GUILayout.Width(35)))
            { sceneTransformTool = tool; draggingHandle = false; SetStatus(tool == SceneTransformTool.Move ? "Move tool" : "Rotate tool"); }
        }
        void DrawInspector()
        {
            GUI.Box(new Rect(0, ToolbarHeight, PanelWidth, ViewHeight - ToolbarHeight - TimelineHeight), "", panelStyle);
            GUILayout.BeginArea(new Rect(14, ToolbarHeight + 12, PanelWidth - 28, ViewHeight - ToolbarHeight - TimelineHeight - 24));
            inspectorScroll = GUILayout.BeginScrollView(inspectorScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.Width(PanelWidth - 52));
            GUILayout.Label(mode.ToString().ToUpperInvariant(), headingStyle);
            GUILayout.Label("Chart: " + chart.title + "\nBeat " + CurrentBeat.ToString("0.000") + "   Time " + songTime.ToString("0.000") + "s", smallStyle);
            GUILayout.Space(8);
            if (mode == ChartEditMode.Scene) DrawSceneInspector();
            else if (mode == ChartEditMode.Stage) DrawStageInspector();
            else if (mode == ChartEditMode.Camera) DrawCameraInspector();
            else if (mode == ChartEditMode.Paths) DrawPathInspector();
            else if (mode == ChartEditMode.Notes) DrawNoteInspector();
            else if (mode == ChartEditMode.Effects) DrawEffectInspector();
            else if (mode == ChartEditMode.CameraMotion) DrawCameraMotionInspector();
            else DrawMapInspector();
            GUILayout.FlexibleSpace();
            GUILayout.Label(needsSaveAs ? "Unsaved chart · Files > Save As" : "File: " + Path.GetFileName(filePath), smallStyle);
            GUILayout.BeginHorizontal();
            bool previous = GUI.enabled;
            GUI.enabled = previous && undo.Count > 0; if (GUILayout.Button("Undo  Ctrl+Z")) Undo();
            GUI.enabled = previous && redo.Count > 0; if (GUILayout.Button("Redo  Ctrl+Y")) Redo(); GUI.enabled = previous;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Validate chart")) ValidateChart();
            GUILayout.EndVertical(); GUILayout.EndScrollView();
            GUILayout.EndArea();
            Color saved = GUI.color; GUI.color = inspectorResizing ? new Color(.25f, .78f, 1) : new Color(.22f, .28f, .36f);
            GUI.DrawTexture(new Rect(PanelWidth - 2, ToolbarHeight, 4, ViewHeight - ToolbarHeight - TimelineHeight), Texture2D.whiteTexture);
            GUI.color = saved;
        }
        void DrawStageInspector()
        {
            GUILayout.Label("Master spline point " + selectedStagePoint + " / " + (chart.stagePath.points.Length - 1));
            var p = chart.stagePath.points[selectedStagePoint];
            bool changed = false;
            changed |= FloatField("X", ref p.x); changed |= FloatField("Y", ref p.y); changed |= FloatField("Z", ref p.z);
            changed |= FloatField("Roll", ref p.roll);
            if (changed) { spatial = new SpatialDirector(chart, tempo); RebuildVisuals(); }
            GUILayout.Space(6);
            if (GUILayout.Button("Insert point after selection"))
            {
                Vector3 here = new Vector3(p.x, p.y, p.z);
                if (selectedStagePoint + 1 < chart.stagePath.points.Length)
                {
                    var next = chart.stagePath.points[selectedStagePoint + 1];
                    here = (here + new Vector3(next.x, next.y, next.z)) * .5f;
                }
                else
                {
                    var previous = chart.stagePath.points[Mathf.Max(0, selectedStagePoint - 1)];
                    Vector3 direction = (here - new Vector3(previous.x, previous.y, previous.z)).normalized;
                    here += (direction.sqrMagnitude > .1f ? direction : Vector3.forward) * 36;
                }
                AddStagePoint(here);
            }
            bool wasEnabled = GUI.enabled; GUI.enabled = wasEnabled && chart.stagePath.points.Length > 2;
            if (GUILayout.Button("Delete selected point")) DeleteSelection(); GUI.enabled = wasEnabled;
            GUILayout.Space(10);
            if (GUILayout.Button("Focus selected point  [F]")) FocusSelection();
            GUILayout.Label("Select a sphere, then drag X / Y / Z arrows. Y changes height. The center square moves in the view plane. Shift+click adds a ground point.", smallStyle);
        }
        void DrawCameraInspector()
        {
            GUILayout.Label("Camera key " + selectedCameraKey + " / " + (chart.cameraKeys.Length - 1));
            var key = chart.cameraKeys[selectedCameraKey];
            GUILayout.Label("Beat " + key.beat.ToString("0.###") + (key.useWorldPose ? " · world pose" : key.usePathPose ? " · route pose" : " · legacy orbit"), smallStyle);
            if (spatial.UsesFixedCameraKeys) GUILayout.Label("Fixed endpoints / smooth rotation", smallStyle);
            if (GUILayout.Button("Add Key at yellow marker")) AddCameraKey();
            if (GUILayout.Button("Move selected key to marker")) CaptureSelectedCamera();
            if (GUILayout.Button("Focus selected key  [F]")) FocusSelection();
            bool previous = GUI.enabled; GUI.enabled = previous && chart.cameraKeys.Length > 1 && selectedCameraKey > 0;
            if (GUILayout.Button("Delete selected key")) DeleteSelection(); GUI.enabled = previous;
            key = chart.cameraKeys[selectedCameraKey];
            bool changed = FloatField("FOV", ref key.fov); changed |= FloatField("Roll", ref key.roll);
            key.fov = Mathf.Clamp(key.fov, 30, 85);
            if (key.useWorldPose)
            {
                GUILayout.Label("Position (world)", smallStyle);
                changed |= FloatField("X", ref key.worldPosition.x); changed |= FloatField("Y", ref key.worldPosition.y); changed |= FloatField("Z", ref key.worldPosition.z);
                GUILayout.Label("Look target (world)", smallStyle);
                changed |= FloatField("Target X", ref key.worldTarget.x); changed |= FloatField("Target Y", ref key.worldTarget.y); changed |= FloatField("Target Z", ref key.worldTarget.z);
            }
            else if (key.usePathPose)
            {
                GUILayout.Label("Position  forward / x / y", smallStyle);
                changed |= FloatField("Forward", ref key.positionForward); changed |= FloatField("X", ref key.positionX); changed |= FloatField("Y", ref key.positionY);
                GUILayout.Label("Look target  forward / x / y", smallStyle);
                changed |= FloatField("Target F", ref key.targetForward); changed |= FloatField("Target X", ref key.targetX); changed |= FloatField("Target Y", ref key.targetY);
            }
            if (changed) Rebuild();
            GUILayout.Space(8);
            if (selectedCameraKey + 1 < chart.cameraKeys.Length)
                GUILayout.Label("Outgoing curve to K" + (selectedCameraKey + 1) + ": " +
                    CameraCurveNames[Mathf.Max(0, Array.IndexOf(CameraCurveIds, SpatialDirector.CameraEasing(key)))], smallStyle);
            GUILayout.Label("Use the small button at the middle of each orange K-to-K line to choose Linear, Ease In, Ease Out, Ease In-Out, or Smoother.", smallStyle);
            GUILayout.Label("Yellow ball = New Key Position, fixed at the 3D viewport center. No automatic camera movement during editing playback or seeking. Add Key uses the yellow ball position.", smallStyle);
        }
        void DrawPathInspector()
        {
            GUILayout.Label("Note paths");
            for (int i = 0; i < chart.paths.Length; i++)
                if (GUILayout.Button(chart.paths[i].id, i == selectedPath ? selectedButtonStyle : buttonStyle)) { selectedPath = i; RebuildVisuals(); }
            if (GUILayout.Button("Add path")) AddPath();
            DrawOffsetInspector();
            GUILayout.Label("Layout sections are now clips in the bottom timeline.", smallStyle);
        }
        void DrawNoteInspector()
        {
            GUILayout.Label("All notes stay visible at their fixed hit positions in edit mode. Select a note and press F to focus it.", smallStyle);
            GUILayout.Label("Target path");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < chart.paths.Length; i++)
                if (GUILayout.Button(chart.paths[i].id, i == selectedPath ? selectedButtonStyle : buttonStyle)) selectedPath = i;
            GUILayout.EndHorizontal();
            GUILayout.Space(5); GUILayout.BeginHorizontal();
            if (GUILayout.Button("TAP", !noteDrag ? selectedButtonStyle : buttonStyle)) noteDrag = false;
            if (GUILayout.Button("DRAG", noteDrag ? selectedButtonStyle : buttonStyle)) noteDrag = true;
            GUILayout.EndHorizontal();
            if (GUILayout.Button(noteProtected ? "Protected · ON" : "Protected · OFF",
                noteProtected ? selectedButtonStyle : buttonStyle)) noteProtected = !noteProtected;
            if (GUILayout.Button("Add note at playhead", selectedButtonStyle, GUILayout.Height(36))) AddNote();
            GUILayout.Space(8);
            GUILayout.Label(chart.notes.Length + " notes total", smallStyle);
            int nearest = -1; int best = int.MaxValue; int currentTick = Mathf.RoundToInt(CurrentBeat * chart.ticksPerBeat);
            for (int i = 0; i < chart.notes.Length; i++)
            {
                int error = Math.Abs(chart.notes[i].tick - currentTick);
                if (error < best) { best = error; nearest = i; }
            }
            if (nearest >= 0)
            {
                var n = chart.notes[nearest];
                GUILayout.Label("Nearest: " + n.id + "\n" + n.action + " / " + n.pathId + " / tick " + n.tick, smallStyle);
                if (GUILayout.Button("Select nearest note")) { selectedNote = nearest; RebuildVisuals(); }
                bool previous = GUI.enabled; GUI.enabled = previous && selectedNote >= 0; if (GUILayout.Button("Delete selected note")) DeleteSelection(); GUI.enabled = previous;
            }
        }
        void DrawMapInspector()
        {
            GUILayout.Label("Deterministic abstract map", smallStyle);
            int seed = chart.map.seed;
            if (IntField("Seed", ref seed)) { chart.map.seed = seed; RebuildVisuals(); }
            Slider("Corridor width", ref chart.map.corridorWidth, 6, 45);
            Slider("Object density", ref chart.map.density, 0, 1.5f);
            Slider("Height variation", ref chart.map.heightVariation, 0, 36);
            if (GUILayout.Button(showMap ? "Map preview · ON" : "Map preview · OFF",
                showMap ? selectedButtonStyle : buttonStyle)) { showMap = !showMap; RebuildVisuals(); }
            if (GUILayout.Button("Regenerate preview")) RebuildVisuals();
            GUILayout.Space(8);
            GUILayout.Label("The seed makes the same chart generate the same geometry. The protected corridor around the master spline remains empty.", smallStyle);
        }
        void DrawSettings()
        {
            float width = 410, height = 326;
            Rect area = new Rect((ViewWidth - width) * .5f, (ViewHeight - height) * .5f, width, height);
            GUI.Box(new Rect(0, 0, ViewWidth, ViewHeight), "", toolbarStyle);
            GUI.Box(area, "", panelStyle);
            GUILayout.BeginArea(new Rect(area.x + 24, area.y + 20, width - 48, height - 40));
            GUILayout.Label("SETTINGS", titleStyle);
            GUILayout.Label("Interface scale", smallStyle);
            GUILayout.BeginHorizontal();
            ScaleButton("100%", 1); ScaleButton("125%", 1.25f); ScaleButton("150%", 1.5f); ScaleButton("175%", 1.75f);
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Window mode", smallStyle);
            GUILayout.Label("Resizable window · " + Screen.width + " × " + Screen.height + " px\nThe chart editor never forces fullscreen.", smallStyle);
            GUILayout.Space(12);
            if (GUILayout.Button("Validate current chart")) ValidateChart();
            if (GUILayout.Button("Charting guide")) { showSettings = false; showGuide = true; }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", selectedButtonStyle)) showSettings = false;
            GUILayout.EndArea();
        }
        void ScaleButton(string label, float value)
        {
            if (GUILayout.Button(label, Mathf.Abs(uiScale - value) < .01f ? selectedButtonStyle : buttonStyle))
            {
                uiScale = value; PlayerPrefs.SetFloat("ChartStudio.UiScale", value); PlayerPrefs.Save();
            }
        }
        void DrawGuide()
        {
            float width = Mathf.Min(720, ViewWidth - 80), height = Mathf.Min(520, ViewHeight - 70);
            Rect area = new Rect((ViewWidth - width) * .5f, (ViewHeight - height) * .5f, width, height);
            GUI.Box(new Rect(0, 0, ViewWidth, ViewHeight), "", toolbarStyle);
            GUI.Box(area, "", panelStyle);
            GUILayout.BeginArea(new Rect(area.x + 28, area.y + 22, width - 56, height - 44));
            GUILayout.Label("QUICK CHARTING GUIDE", titleStyle);
            GUILayout.Label("Caps Lock ON: fly with WASD, Space up, Shift down, Ctrl fast. RMB in viewport captures the mouse; Esc releases it. Caps OFF: RMB orbit, MMB pan. Wheel zoom is disabled.\n\n" +
                "1  SCENE   Place or import assets, click objects in the viewport, then use the top Move / Rotate icons and right-side inspector. Drag the left panel edge to resize it.\n\n" +
                "2  STAGE / PATHS / NOTES   Shape the route after the world exists, then author play paths and notes.\n\n" +
                "3  CAMERA  Build the base shot with world-space camera keys.\n\n" +
                "4  EFFECTS Add reusable screen, lighting, particle and object-effect clips on the timeline.\n\n" +
                "5  MOTION  Layer reusable shake, punch, orbit, roll and custom keyed motion over the base camera.\n\n" +
                "6  REVIEW  F5 starts a clean preview. Space pauses; Esc returns to editing. Ctrl + wheel zooms the timeline.", smallStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Start with Scene", selectedButtonStyle)) { showGuide = false; SetMode(ChartEditMode.Scene); }
            if (GUILayout.Button("Close")) showGuide = false;
            GUILayout.EndArea();
        }
        bool FloatField(string label, ref float value, bool recordUndo = true)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(82));
            string name = "Edit:" + mode + ":" + label;
            if (GUI.GetNameOfFocusedControl() != name || !numericEdits.ContainsKey(name))
                numericEdits[name] = value.ToString("0.###", CultureInfo.InvariantCulture);
            GUI.SetNextControlName(name);
            string previousText = numericEdits[name];
            string text = GUILayout.TextField(previousText, GUILayout.MinWidth(30));
            numericEdits[name] = text;
            GUILayout.EndHorizontal();
            // Display rounding must never mutate chart values or add undo entries.
            if (text == previousText) return false;
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed) && !Mathf.Approximately(parsed, value))
            { if (recordUndo) RecordUndo(); value = parsed; return true; }
            return false;
        }
        bool IntField(string label, ref int value)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(82)); GUI.SetNextControlName("Edit:" + label); string text = GUILayout.TextField(value.ToString()); GUILayout.EndHorizontal();
            if (int.TryParse(text, out int parsed) && parsed != value) { value = parsed; return true; } return false;
        }
        void Slider(string label, ref float value, float min, float max)
        {
            GUILayout.Label(label + "  " + value.ToString("0.0"));
            float next = GUILayout.HorizontalSlider(value, min, max);
            if (!Mathf.Approximately(next, value)) { value = next; RebuildVisuals(); }
        }

        void OnDestroy()
        {
            ReleaseMouse();
            authoredVisuals?.Dispose();
            if (generatedAudio != null) Destroy(generatedAudio);
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
            foreach (var texture in ownedTextures) if (texture != null) Destroy(texture);
        }

        IEnumerator SmokeTest()
        {
            Directory.CreateDirectory(smokeDirectory);
            // Wait past the standalone splash screen before capturing rendered UI.
            yield return new WaitForSecondsRealtime(3);
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            bool interactionsPassed = true;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chartEditorInteractionSmoke") >= 0)
            {
                interactionsPassed = RunInteractionChecks();
                showSettings = showGuide = false; showMap = true;
                selectedStagePoint = 2; SetMode(ChartEditMode.Stage); FocusSelection();
                yield return CaptureCheckScreenshot("stage-xyz.png");
                SetMode(ChartEditMode.Camera); FocusSelection();
                yield return CaptureCheckScreenshot("camera-marker.png");
                SetMode(ChartEditMode.Paths); Seek(tempo.SecondsAtBeat(32)); FocusSelection();
                yield return CaptureCheckScreenshot("path-xy.png");
                interactionsPassed &= ScreenshotHasContent(Path.Combine(smokeDirectory, "stage-xyz.png")) &&
                    ScreenshotHasContent(Path.Combine(smokeDirectory, "camera-marker.png")) &&
                    ScreenshotHasContent(Path.Combine(smokeDirectory, "path-xy.png"));
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chartEditorTimelineSmoke") >= 0)
            {
                interactionsPassed &= RunTimelineChecks();
                PrepareTimelinePreview();
                foreach (ChartEditMode view in new[] { ChartEditMode.Stage, ChartEditMode.Camera, ChartEditMode.Paths, ChartEditMode.Notes })
                {
                    SetMode(view); timelineTrackScroll = 0;
                    yield return CaptureCheckScreenshot("timeline-" + view.ToString().ToLowerInvariant() + ".png");
                    interactionsPassed &= ScreenshotHasContent(Path.Combine(smokeDirectory, "timeline-" + view.ToString().ToLowerInvariant() + ".png"));
                }
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chartEditorWorkspaceSmoke") >= 0)
            {
                interactionsPassed &= RunWorkspaceChecks();
                PrepareTimelinePreview(); SetMode(ChartEditMode.Paths);
                yield return CaptureCheckScreenshot("workspace-scrollbars.png");
                showFilesMenu = true;
                yield return CaptureCheckScreenshot("workspace-files.png");
                showFilesMenu = false;
                chart.cameraKeys = new[] { new CameraKey { beat = 0, distance = 25, height = 6, fov = 53 } };
                Rebuild(); SetPreviewMode(true); SetPlaying(false);
                yield return CaptureCheckScreenshot("workspace-preview.png");
                SetPreviewMode(false);
                interactionsPassed &= ScreenshotHasContent(Path.Combine(smokeDirectory, "workspace-scrollbars.png")) &&
                    ScreenshotHasContent(Path.Combine(smokeDirectory, "workspace-files.png")) &&
                    ScreenshotHasContent(Path.Combine(smokeDirectory, "workspace-preview.png"));
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chartEditorNoteSmoke") >= 0)
            {
                interactionsPassed &= RunStaticNoteChecks();
                PrepareTimelinePreview(); SetMode(ChartEditMode.Notes); selectedNote = 15; FocusSelection();
                Seek(0); yield return CaptureCheckScreenshot("static-notes-start.png");
                Seek(Duration); yield return CaptureCheckScreenshot("static-notes-end.png");
                interactionsPassed &= ScreenshotHasContent(Path.Combine(smokeDirectory, "static-notes-start.png")) &&
                    ScreenshotHasContent(Path.Combine(smokeDirectory, "static-notes-end.png"));
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chartEditorPlaybackSmoke") >= 0)
            {
                interactionsPassed &= RunEditingPlaybackChecks();
                PrepareTimelinePreview();
                chart.cameraKeys = new[] { new CameraKey { beat = 0, distance = 25, height = 6, fov = 53 } };
                selectedCameraKey = 0; Rebuild(); SetMode(ChartEditMode.Camera); Seek(12);
                orbitPivot = EditorPlayheadPosition(songTime); orbitYaw = 25; orbitPitch = 22; orbitDistance = 52; ApplyOrbitCamera();
                SetPlaying(true);
                yield return CaptureCheckScreenshot("editing-live-playback.png");
                SetPlaying(false);
                interactionsPassed &= ScreenshotHasContent(Path.Combine(smokeDirectory, "editing-live-playback.png"));
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chartEditorCameraTweenSmoke") >= 0)
            {
                interactionsPassed &= RunCameraTweenChecks();
                var input = CameraTweenInput();
                if (input != null)
                {
                    chart = input; selectedCameraKey = selectedStagePoint = selectedPath = selectedSection = 0;
                    selectedNote = -1; Rebuild(); SetMode(ChartEditMode.Camera);
                    Seek(tempo.SecondsAtBeat(chart.cameraKeys[1].beat * .5)); SetPreviewMode(true); SetPlaying(false);
                    yield return CaptureCheckScreenshot("camera-tween-midpoint.png");
                    Seek(tempo.SecondsAtBeat(chart.cameraKeys[1].beat * .888273));
                    spatial.EvaluateCamera(sceneCamera, songTime);
                    yield return CaptureCheckScreenshot("camera-tween-steep.png");
                    SetPreviewMode(false);
                    interactionsPassed &= ScreenshotHasContent(Path.Combine(smokeDirectory, "camera-tween-midpoint.png")) &&
                        ScreenshotHasContent(Path.Combine(smokeDirectory, "camera-tween-steep.png"));
                }
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chartEditorVisualSmoke") >= 0)
            {
                interactionsPassed &= RunVisualAuthoringChecks();
                EnsureVisualLibrary(); SetMode(ChartEditMode.Scene); AddSceneObject(sceneLibrary[1]);
                yield return CaptureCheckScreenshot("visual-scene.png");
                SetMode(ChartEditMode.Effects); AddEffectClip(effectLibrary[0]);
                yield return CaptureCheckScreenshot("visual-effects.png");
                SetMode(ChartEditMode.CameraMotion); AddMotionClip(motionLibrary[0]);
                yield return CaptureCheckScreenshot("visual-motion.png");
                interactionsPassed &= ScreenshotHasContent(Path.Combine(smokeDirectory, "visual-scene.png")) &&
                    ScreenshotHasContent(Path.Combine(smokeDirectory, "visual-effects.png")) &&
                    ScreenshotHasContent(Path.Combine(smokeDirectory, "visual-motion.png"));
            }
            yield return new WaitForEndOfFrame();
            string screenshot = Path.Combine(smokeDirectory, "chart-studio.png");
            if (File.Exists(screenshot)) File.Delete(screenshot);
            ScreenCapture.CaptureScreenshot(screenshot);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!File.Exists(screenshot) && Time.realtimeSinceStartup < deadline) yield return null;
            bool passed = interactionsPassed && chart != null && chart.stagePath != null && chart.stagePath.points.Length >= 2 &&
                spatial != null && visualRoot != null && sceneCamera != null && ScreenshotHasContent(screenshot);
            File.WriteAllText(Path.Combine(smokeDirectory, "chart-studio-smoke.txt"),
                "PASS=" + passed + "\nStagePoints=" + chart.stagePath.points.Length +
                "\nPaths=" + chart.paths.Length + "\nMapChildren=" + visualRoot.childCount +
                "\nScreenshot=" + screenshot + "\n");
            Debug.Log("CHART_STUDIO_SMOKE " + (passed ? "PASS" : "FAIL"));
            Application.Quit(passed ? 0 : 1);
        }
    }

    static class ChartEditorArrayExtensions
    {
        public static int FindIndex<T>(this T[] values, Predicate<T> predicate) => Array.FindIndex(values, predicate);
    }
}
