using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public enum ChartEditMode { Stage, Camera, Paths, Notes, Map }

    public sealed class ChartEditorHandle : MonoBehaviour
    {
        public ChartEditMode mode;
        public int index;
    }

    /// <summary>
    /// Standalone desktop chart-authoring MVP. It deliberately uses runtime APIs only: the
    /// same scene can run in the editor or be shipped as a Windows application.
    /// </summary>
    public sealed class RuntimeChartEditorController : MonoBehaviour
    {
        const float PanelWidth = 330;
        const float ToolbarHeight = 48;
        const float TimelineHeight = 104;
        readonly Stack<string> undo = new Stack<string>();
        readonly Stack<string> redo = new Stack<string>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Texture2D> ownedTextures = new List<Texture2D>();

        ChartData chart;
        TempoMap tempo;
        SpatialDirector spatial;
        Camera sceneCamera;
        Camera evaluatorCamera;
        Transform cameraTargetMarker;
        AudioSource audioSource;
        AudioClip generatedAudio;
        Transform visualRoot;
        Material routeMaterial, cameraMaterial, pathMaterial, pointMaterial, selectedMaterial;
        Material mapA, mapB, noteMaterial;
        GUIStyle titleStyle, smallStyle, statusStyle, buttonStyle, selectedButtonStyle;
        GUIStyle panelStyle, fieldStyle, toolbarStyle, headingStyle;

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
        ChartEditorHandle activeHandle;
        Plane dragPlane;
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
        bool PointerInViewport(Vector2 p)
        {
            if (showSettings || showGuide) return false;
            p /= uiScale;
            return p.x > PanelWidth && p.y > ToolbarHeight && p.y < ViewHeight - TimelineHeight;
        }

        void Awake()
        {
            // The authoring tool is a resizable desktop window. Never inherit the game's
            // fullscreen preference or silently switch the user's display mode.
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            uiScale = Mathf.Clamp(PlayerPrefs.GetFloat("ChartStudio.UiScale", 1.25f), 1, 1.75f);
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
                notes = new NoteData[0]
            };
            selectedStagePoint = selectedCameraKey = selectedPath = selectedSection = 0;
            selectedNote = -1; songTime = 0; undo.Clear(); redo.Clear();
            SetupAudio(); Rebuild("New chart created");
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
            selectedStagePoint = Mathf.Clamp(selectedStagePoint, 0, chart.stagePath.points.Length - 1);
            selectedCameraKey = Mathf.Clamp(selectedCameraKey, 0, chart.cameraKeys.Length - 1);
            selectedPath = Mathf.Clamp(selectedPath, 0, chart.paths.Length - 1);
            selectedSection = Mathf.Clamp(selectedSection, 0, chart.sections.Length - 1);
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
            if (visualRoot != null) { visualRoot.gameObject.SetActive(false); Destroy(visualRoot.gameObject); }
            visualRoot = new GameObject("Chart editor visuals").transform;
            visualRoot.SetParent(transform, false);
            BuildRoute();
            BuildStageHandles();
            BuildCameraPath();
            BuildNotePaths();
            BuildNotes();
            if (showMap) BuildMap();
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
            Line("Camera path", cameraMaterial, .12f, points);
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
                target.name = "Camera look target"; target.transform.SetParent(visualRoot, false);
                target.transform.position = orbitPivot; target.transform.localScale = Vector3.one * .72f;
                target.GetComponent<Renderer>().sharedMaterial = selectedMaterial;
                Destroy(target.GetComponent<Collider>()); cameraTargetMarker = target.transform;
            }
        }
        void BuildNotePaths()
        {
            for (int path = 0; path < chart.paths.Length; path++)
            {
                const int count = 90;
                var points = new Vector3[count];
                for (int i = 0; i < count; i++)
                    points[i] = spatial.Point(chart.paths[path].id, Mathf.Lerp(SpatialDirector.NearDepth - 2, SpatialDirector.FarDepth, i / (count - 1f)), songTime);
                Line("Note path " + chart.paths[path].id, path == selectedPath ? selectedMaterial : pathMaterial,
                    path == selectedPath ? .12f : .065f, points);
            }
            if (mode != ChartEditMode.Paths) return;
            var section = chart.sections[selectedSection];
            float distance = spatial.DistanceAtTime(tempo.SecondsAtBeat(section.startBeat)) + SpatialDirector.NearDepth;
            for (int i = 0; i < section.placements.Length; i++)
            {
                var p = section.placements[i];
                Handle("Path placement " + p.pathId, spatial.RoutePoint(distance, p.x, p.y), .85f,
                    ChartEditMode.Paths, i, chart.paths[selectedPath].id == p.pathId);
            }
        }
        void BuildNotes()
        {
            double earliest = songTime - .15, latest = songTime + chart.approachSeconds;
            for (int i = 0; i < chart.notes.Length; i++)
            {
                double hit = tempo.SecondsAtBeat(chart.notes[i].tick / (double)chart.ticksPerBeat);
                if (hit < earliest || hit > latest) continue;
                float depth = SpatialDirector.NearDepth + (float)((hit - songTime) / chart.approachSeconds) *
                    (SpatialDirector.FarDepth - SpatialDirector.NearDepth);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = chart.notes[i].id; go.transform.SetParent(visualRoot, false);
                go.transform.position = spatial.Point(chart.notes[i].pathId, depth, songTime);
                go.transform.rotation = spatial.RouteRotationAt(spatial.DistanceAtTime(songTime) + depth) * Quaternion.Euler(90, 0, 0);
                go.transform.localScale = new Vector3(.65f, .08f, .65f);
                go.GetComponent<Renderer>().sharedMaterial = i == selectedNote ? selectedMaterial : noteMaterial;
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
            ReadKeyboard();
            if (playing)
            {
                songTime += Time.unscaledDeltaTime;
                if (songTime >= Duration) { songTime = Duration; SetPlaying(false); }
                if (audioSource != null && !audioSource.isPlaying && songTime < audioSource.clip.length) StartAudioAtCurrentTime();
                RebuildVisuals();
            }
            if (chartCameraPreview) spatial.EvaluateCamera(sceneCamera, songTime);
            else { ReadSceneNavigation(); ApplyOrbitCamera(); }
            if (cameraTargetMarker != null) cameraTargetMarker.position = orbitPivot;
            ReadHandles();
        }

        void ReadKeyboard()
        {
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (control && Input.GetKeyDown(KeyCode.S)) SaveFile();
            if (control && Input.GetKeyDown(KeyCode.Z)) Undo();
            if (control && Input.GetKeyDown(KeyCode.Y)) Redo();
            if (Input.GetKeyDown(KeyCode.Space)) SetPlaying(!playing);
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetMode(ChartEditMode.Stage);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetMode(ChartEditMode.Camera);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetMode(ChartEditMode.Paths);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetMode(ChartEditMode.Notes);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SetMode(ChartEditMode.Map);
            if (Input.GetKeyDown(KeyCode.Delete)) DeleteSelection();
        }
        void ReadSceneNavigation()
        {
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
            orbitDistance = Mathf.Clamp(orbitDistance * Mathf.Exp(-Input.mouseScrollDelta.y * .09f), 3, 220);
        }
        void ApplyOrbitCamera()
        {
            Quaternion rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0);
            sceneCamera.transform.SetPositionAndRotation(orbitPivot - rotation * Vector3.forward * orbitDistance, rotation);
        }
        void ReadHandles()
        {
            Vector2 guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (!PointerInViewport(guiMouse)) return;
            Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (Input.GetMouseButtonDown(0))
            {
                if (Physics.Raycast(ray, out var hit, 2000) && hit.collider.TryGetComponent(out ChartEditorHandle marker))
                {
                    SetMode(marker.mode); SelectHandle(marker);
                    RecordUndo(); activeHandle = marker; draggingHandle = marker.mode == ChartEditMode.Stage || marker.mode == ChartEditMode.Paths;
                    if (marker.mode == ChartEditMode.Stage)
                        dragPlane = new Plane(Vector3.up, marker.transform.position);
                    else
                    {
                        float d = spatial.DistanceAtTime(tempo.SecondsAtBeat(chart.sections[selectedSection].startBeat)) + SpatialDirector.NearDepth;
                        dragPlane = new Plane(spatial.RouteRotationAt(d) * Vector3.forward, spatial.RouteAt(d));
                    }
                }
                else if (mode == ChartEditMode.Stage && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                {
                    Plane ground = new Plane(Vector3.up, Vector3.zero);
                    if (ground.Raycast(ray, out float enter)) AddStagePoint(ray.GetPoint(enter));
                }
            }
            if (draggingHandle && Input.GetMouseButton(0) && activeHandle != null && dragPlane.Raycast(ray, out float distance))
            {
                Vector3 point = ray.GetPoint(distance);
                if (activeHandle.mode == ChartEditMode.Stage)
                {
                    var p = chart.stagePath.points[activeHandle.index]; p.x = point.x; p.y = point.y; p.z = point.z;
                }
                else
                {
                    var section = chart.sections[selectedSection];
                    if (activeHandle.index < section.placements.Length)
                    {
                        var placement = section.placements[activeHandle.index];
                        float d = spatial.DistanceAtTime(tempo.SecondsAtBeat(section.startBeat)) + SpatialDirector.NearDepth;
                        Vector3 local = Quaternion.Inverse(spatial.RouteRotationAt(d)) * (point - spatial.RouteAt(d));
                        placement.x = local.x; placement.y = local.y;
                    }
                }
                tempo = new TempoMap(chart.tempos, chart.ticksPerBeat); spatial = new SpatialDirector(chart, tempo);
                RebuildVisuals();
            }
            if (Input.GetMouseButtonUp(0)) { draggingHandle = false; activeHandle = null; }
        }
        void SelectHandle(ChartEditorHandle marker)
        {
            if (marker.mode == ChartEditMode.Stage) selectedStagePoint = marker.index;
            else if (marker.mode == ChartEditMode.Camera) selectedCameraKey = marker.index;
            else if (marker.mode == ChartEditMode.Paths)
            {
                var placement = chart.sections[selectedSection].placements[marker.index];
                selectedPath = Array.FindIndex(chart.paths, p => p.id == placement.pathId);
                if (selectedPath < 0) selectedPath = 0;
            }
            RebuildVisuals();
        }

        void SetPlaying(bool value)
        {
            playing = value;
            if (!playing) audioSource.Pause(); else StartAudioAtCurrentTime();
        }
        void StartAudioAtCurrentTime()
        {
            if (audioSource == null || audioSource.clip == null) return;
            float audioTime = (float)songTime + chart.audioOffsetSeconds;
            if (audioTime < 0 || audioTime >= audioSource.clip.length) return;
            audioSource.time = audioTime; audioSource.Play();
        }
        void Seek(double value)
        {
            songTime = Math.Max(0, Math.Min(Duration, value));
            if (playing) StartAudioAtCurrentTime();
            RebuildVisuals();
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
            if (undo.Count == 0) return;
            redo.Push(JsonUtility.ToJson(chart)); chart = JsonUtility.FromJson<ChartData>(undo.Pop()); Rebuild("Undo");
        }
        void Redo()
        {
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
                var key = new CameraKey { beat = snappedBeat, fov = sceneCamera.fieldOfView, usePathPose = true };
                CapturePose(key);
                int existing = list.FindIndex(k => Mathf.Abs(k.beat - snappedBeat) < .5f / chart.ticksPerBeat);
                if (existing >= 0) list[existing] = key; else list.Add(key);
                list.Sort((a, b) => a.beat.CompareTo(b.beat));
                chart.cameraKeys = list.ToArray(); selectedCameraKey = Array.IndexOf(chart.cameraKeys, key);
            }, "Camera key captured (same-tick key replaced)");
        }
        void CaptureSelectedCamera()
        {
            Change(() => CapturePose(chart.cameraKeys[selectedCameraKey]), "Camera key updated from viewport");
        }
        void CapturePose(CameraKey key)
        {
            float baseDistance = spatial.DistanceAtTime(tempo.SecondsAtBeat(key.beat));
            Quaternion inverse = Quaternion.Inverse(spatial.RouteRotationAt(baseDistance));
            Vector3 position = inverse * (sceneCamera.transform.position - spatial.RouteAt(baseDistance));
            Vector3 target = inverse * (orbitPivot - spatial.RouteAt(baseDistance));
            key.usePathPose = true;
            key.positionX = position.x; key.positionY = position.y; key.positionForward = position.z;
            key.targetX = target.x; key.targetY = target.y; key.targetForward = target.z;
            key.fov = sceneCamera.fieldOfView;
        }
        void AddSection()
        {
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
            if (mode == ChartEditMode.Stage && chart.stagePath.points.Length > 2)
                Change(() => { var list = new List<StagePointData>(chart.stagePath.points); list.RemoveAt(selectedStagePoint); chart.stagePath.points = list.ToArray(); selectedStagePoint = Mathf.Max(0, selectedStagePoint - 1); }, "Stage point deleted");
            else if (mode == ChartEditMode.Camera && chart.cameraKeys.Length > 1 && selectedCameraKey > 0)
                Change(() => { var list = new List<CameraKey>(chart.cameraKeys); list.RemoveAt(selectedCameraKey); chart.cameraKeys = list.ToArray(); selectedCameraKey = Mathf.Max(0, selectedCameraKey - 1); }, "Camera key deleted");
            else if (mode == ChartEditMode.Notes && selectedNote >= 0 && selectedNote < chart.notes.Length)
                Change(() => { var list = new List<NoteData>(chart.notes); list.RemoveAt(selectedNote); chart.notes = list.ToArray(); selectedNote = -1; }, "Note deleted");
        }

        void Normalize()
        {
            Array.Sort(chart.tempos, (a, b) => a.tick.CompareTo(b.tick));
            Array.Sort(chart.sections, (a, b) => a.startBeat.CompareTo(b.startBeat));
            Array.Sort(chart.cameraKeys, (a, b) => a.beat.CompareTo(b.beat));
            Array.Sort(chart.notes, (a, b) => a.tick != b.tick ? a.tick.CompareTo(b.tick) : string.CompareOrdinal(a.id, b.id));
        }
        void SaveFile()
        {
            try
            {
                Normalize(); string json = JsonUtility.ToJson(chart, true);
                string directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(filePath, json);
                try { ChartLoader.Parse(json); SetStatus("Saved and gameplay validation passed"); }
                catch (Exception e) { SetStatus("Draft saved; validation: " + e.Message); }
            }
            catch (Exception e) { SetStatus("Save failed: " + e.Message); }
        }
        void LoadFile()
        {
            try
            {
                chart = JsonUtility.FromJson<ChartData>(File.ReadAllText(filePath));
                EnsureData(); undo.Clear(); redo.Clear(); songTime = 0; SetupAudio(); Rebuild("Loaded " + filePath);
            }
            catch (Exception e) { SetStatus("Load failed: " + e.Message); if (chart == null) NewChart(); }
        }
        void ValidateChart()
        {
            try { ChartLoader.Parse(JsonUtility.ToJson(chart)); SetStatus("Validation passed: chart is playable"); }
            catch (Exception e) { SetStatus("Validation failed: " + e.Message); }
        }
        void SetStatus(string value) { status = value; statusUntil = Time.realtimeSinceStartupAsDouble + 8; }
        void SetMode(ChartEditMode value) { mode = value; RebuildVisuals(); }

        void OnGUI()
        {
            BuildStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            DrawToolbar();
            DrawInspector();
            DrawTimeline();
            GUI.Label(new Rect(PanelWidth + 14, ToolbarHeight + 10, 480, 28),
                chartCameraPreview ? "CHART CAMERA PREVIEW" : "RMB orbit · MMB pan · Wheel zoom · Shift+click adds stage point", smallStyle);
            if (Time.realtimeSinceStartupAsDouble < statusUntil)
                GUI.Label(new Rect(PanelWidth + 18, ViewHeight - TimelineHeight - 42, ViewWidth - PanelWidth - 36, 32), status, statusStyle);
            if (showSettings) DrawSettings();
            if (showGuide) DrawGuide();
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
            GUILayout.Label("CHART STUDIO", titleStyle, GUILayout.Width(164));
            ModeButton("1 Stage", ChartEditMode.Stage); ModeButton("2 Camera", ChartEditMode.Camera);
            ModeButton("3 Paths", ChartEditMode.Paths); ModeButton("4 Notes", ChartEditMode.Notes); ModeButton("5 Map", ChartEditMode.Map);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("New", GUILayout.Width(58))) NewChart();
            if (GUILayout.Button("Load", GUILayout.Width(58))) LoadFile();
            if (GUILayout.Button("Save", selectedButtonStyle, GUILayout.Width(58))) SaveFile();
            if (GUILayout.Button("Guide", GUILayout.Width(62))) { showGuide = true; showSettings = false; }
            if (GUILayout.Button("Settings", GUILayout.Width(78))) { showSettings = true; showGuide = false; }
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }
        void ModeButton(string label, ChartEditMode value)
        {
            if (GUILayout.Button(label, mode == value ? selectedButtonStyle : buttonStyle, GUILayout.Width(74))) SetMode(value);
        }
        void DrawInspector()
        {
            GUI.Box(new Rect(0, ToolbarHeight, PanelWidth, ViewHeight - ToolbarHeight - TimelineHeight), "", panelStyle);
            GUILayout.BeginArea(new Rect(14, ToolbarHeight + 12, PanelWidth - 28, ViewHeight - ToolbarHeight - TimelineHeight - 24));
            GUILayout.Label(mode.ToString().ToUpperInvariant(), headingStyle);
            GUILayout.Label("Chart: " + chart.title + "\nBeat " + CurrentBeat.ToString("0.000") + "   Time " + songTime.ToString("0.000") + "s", smallStyle);
            GUILayout.Space(8);
            if (mode == ChartEditMode.Stage) DrawStageInspector();
            else if (mode == ChartEditMode.Camera) DrawCameraInspector();
            else if (mode == ChartEditMode.Paths) DrawPathInspector();
            else if (mode == ChartEditMode.Notes) DrawNoteInspector();
            else DrawMapInspector();
            GUILayout.FlexibleSpace();
            GUILayout.Label("File", smallStyle); filePath = GUILayout.TextField(filePath);
            GUILayout.BeginHorizontal();
            GUI.enabled = undo.Count > 0; if (GUILayout.Button("Undo  Ctrl+Z")) Undo();
            GUI.enabled = redo.Count > 0; if (GUILayout.Button("Redo  Ctrl+Y")) Redo(); GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Validate chart")) ValidateChart();
            GUILayout.EndArea();
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
            GUI.enabled = chart.stagePath.points.Length > 2;
            if (GUILayout.Button("Delete selected point")) DeleteSelection(); GUI.enabled = true;
            GUILayout.Space(10);
            GUILayout.Label("Shift+click the ground to draw. Drag a sphere to move it.", smallStyle);
        }
        void DrawCameraInspector()
        {
            GUILayout.Label("Camera key " + selectedCameraKey + " / " + (chart.cameraKeys.Length - 1));
            var key = chart.cameraKeys[selectedCameraKey];
            GUILayout.Label("Beat " + key.beat.ToString("0.###") + (key.usePathPose ? " · drawn pose" : " · legacy orbit"), smallStyle);
            FloatField("FOV", ref key.fov); FloatField("Roll", ref key.roll);
            if (key.usePathPose)
            {
                GUILayout.Label("Position  forward / x / y", smallStyle);
                FloatField("Forward", ref key.positionForward); FloatField("X", ref key.positionX); FloatField("Y", ref key.positionY);
                GUILayout.Label("Look target  forward / x / y", smallStyle);
                FloatField("Forward", ref key.targetForward); FloatField("X", ref key.targetX); FloatField("Y", ref key.targetY);
            }
            if (GUILayout.Button("Add key at playhead from viewport")) AddCameraKey();
            if (GUILayout.Button("Overwrite key from viewport")) CaptureSelectedCamera();
            GUI.enabled = chart.cameraKeys.Length > 1 && selectedCameraKey > 0;
            if (GUILayout.Button("Delete selected key")) DeleteSelection(); GUI.enabled = true;
            GUILayout.Space(8);
            GUILayout.Label("Frame the shot with RMB/MMB, set the orange pivot as the look target, then capture.", smallStyle);
        }
        void DrawPathInspector()
        {
            GUILayout.Label("Path sections");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < chart.sections.Length; i++)
                if (GUILayout.Button(i.ToString(), i == selectedSection ? selectedButtonStyle : buttonStyle, GUILayout.Width(40))) { selectedSection = i; RebuildVisuals(); }
            GUILayout.EndHorizontal();
            GUILayout.Label(chart.sections[selectedSection].name + " · beat " + chart.sections[selectedSection].startBeat.ToString("0.###"), smallStyle);
            if (GUILayout.Button("Add section at playhead")) AddSection();
            GUILayout.Space(8); GUILayout.Label("Note paths");
            for (int i = 0; i < chart.paths.Length; i++)
                if (GUILayout.Button(chart.paths[i].id, i == selectedPath ? selectedButtonStyle : buttonStyle)) { selectedPath = i; RebuildVisuals(); }
            if (GUILayout.Button("Add path")) AddPath();
            var placement = Array.Find(chart.sections[selectedSection].placements, x => x.pathId == chart.paths[selectedPath].id);
            if (placement != null)
            {
                GUILayout.Space(6); GUILayout.Label("Selected placement");
                bool changed = false;
                changed |= FloatField("X", ref placement.x); changed |= FloatField("Y", ref placement.y);
                changed |= FloatField("Bend", ref placement.bend); changed |= FloatField("Lift", ref placement.lift);
                if (changed) RebuildVisuals();
            }
            GUILayout.Label("Drag the section spheres in the 3D cross-section.", smallStyle);
        }
        void DrawNoteInspector()
        {
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
                GUI.enabled = selectedNote >= 0; if (GUILayout.Button("Delete selected note")) DeleteSelection(); GUI.enabled = true;
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
        void DrawTimeline()
        {
            GUI.Box(new Rect(0, ViewHeight - TimelineHeight, ViewWidth, TimelineHeight), "", toolbarStyle);
            GUILayout.BeginArea(new Rect(14, ViewHeight - TimelineHeight + 10, ViewWidth - 28, TimelineHeight - 16));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(playing ? "Pause" : "Play", selectedButtonStyle, GUILayout.Width(64))) SetPlaying(!playing);
            if (GUILayout.Button("−1 beat", GUILayout.Width(78))) Seek(tempo.SecondsAtBeat(Math.Max(0, CurrentBeat - 1)));
            if (GUILayout.Button("+1 beat", GUILayout.Width(78))) Seek(tempo.SecondsAtBeat(Math.Min(chart.endBeat, CurrentBeat + 1)));
            if (GUILayout.Button(chartCameraPreview ? "Camera preview · ON" : "Camera preview · OFF",
                chartCameraPreview ? selectedButtonStyle : buttonStyle, GUILayout.Width(160))) chartCameraPreview = !chartCameraPreview;
            GUILayout.Label(songTime.ToString("0.000") + " / " + Duration.ToString("0.000") + " sec    Beat " + CurrentBeat.ToString("0.000"));
            GUILayout.EndHorizontal();
            float next = GUILayout.HorizontalSlider((float)songTime, 0, (float)Math.Max(.001, Duration), GUILayout.Height(28));
            if (Math.Abs(next - songTime) > .0001) Seek(next);
            GUILayout.EndArea();
        }
        void DrawSettings()
        {
            float width = 410, height = 286;
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
            GUILayout.Label("1  STAGE   Draw the master route first. Shift-click the ground to add points; drag the blue spheres to shape the world.\n\n" +
                "2  MAP     Choose a seed, corridor width and density. Keep the play corridor readable; use generated geometry as a blockout.\n\n" +
                "3  CAMERA  Move the viewport with RMB/MMB, put the orange pivot on the subject, move the playhead, then capture a camera key.\n\n" +
                "4  PATHS   Add layout sections at musical phrases. Select a path and drag its sphere in the cross-section; Bend/Lift shape the approach.\n\n" +
                "5  NOTES   Select a path and TAP/DRAG type, move to a beat, then add the note. Protected notes accept input anywhere.\n\n" +
                "6  REVIEW  Enable Camera preview, play through the section, run Validate, then Save. Start sparse and add density only after camera and paths are readable.", smallStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Start with Stage", selectedButtonStyle)) { showGuide = false; SetMode(ChartEditMode.Stage); }
            if (GUILayout.Button("Close")) showGuide = false;
            GUILayout.EndArea();
        }
        bool FloatField(string label, ref float value)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(82));
            string text = GUILayout.TextField(value.ToString("0.###", CultureInfo.InvariantCulture));
            GUILayout.EndHorizontal();
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && !Mathf.Approximately(parsed, value))
            { value = parsed; return true; }
            return false;
        }
        bool IntField(string label, ref int value)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(82)); string text = GUILayout.TextField(value.ToString()); GUILayout.EndHorizontal();
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
            if (generatedAudio != null) Destroy(generatedAudio);
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
            foreach (var texture in ownedTextures) if (texture != null) Destroy(texture);
        }

        IEnumerator SmokeTest()
        {
            Directory.CreateDirectory(smokeDirectory);
            yield return null;
            yield return new WaitForEndOfFrame();
            string screenshot = Path.Combine(smokeDirectory, "chart-studio.png");
            if (File.Exists(screenshot)) File.Delete(screenshot);
            ScreenCapture.CaptureScreenshot(screenshot);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!File.Exists(screenshot) && Time.realtimeSinceStartup < deadline) yield return null;
            bool passed = chart != null && chart.stagePath != null && chart.stagePath.points.Length >= 2 &&
                spatial != null && visualRoot != null && sceneCamera != null && File.Exists(screenshot);
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
