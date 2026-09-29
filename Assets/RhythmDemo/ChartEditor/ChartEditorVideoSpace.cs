using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        bool VideoSpaceEditing => HasVideoPackage && VideoChartSpace.Enabled(chart);
        bool PreviewGeometry => chartCameraPreview || VideoSpaceEditing;
        // 0 = this Path's percent anchors, 1 = camera rail (note travel), 2 = camera rig.
        const int VideoInspectorAnchors = 0, VideoInspectorRail = 1, VideoInspectorPose = 2;
        int videoInspector;
        int selectedCameraZ, selectedCameraPose;
        bool editVideoCameraZ => videoInspector == VideoInspectorRail;
        int VideoAuthoringTick => Mathf.Clamp(PlayheadTick, 0, Mathf.RoundToInt(chart.endBeat * chart.ticksPerBeat));
        ScreenAnchorData draggedScreenAnchor;
        ScreenAnchorData[] ScreenKeys() => chart.paths[selectedPath].screenAnchors;
        void SetScreenKeys(ScreenAnchorData[] keys) => chart.paths[selectedPath].screenAnchors = keys;
        double VideoKeyTime(int tick) => tempo.SecondsAtBeat(tick / (double)chart.ticksPerBeat);
        Vector3 VideoAnchorPosition(double time)
        {
            float distance = spatial.DistanceAtTime(time) + SpatialDirector.NearDepth;
            return spatial.VideoSpace.PathAtDistance(chart.paths[selectedPath], distance);
        }
        ScreenAnchorData EnsureScreenAnchor(int tick)
        {
            tick = Mathf.Clamp(tick, 0, Mathf.RoundToInt(chart.endBeat * chart.ticksPerBeat));
            var keys = new List<ScreenAnchorData>(ScreenKeys());
            var found = keys.Find(k => k.tick == tick);
            if (found != null) return found;
            double time = VideoKeyTime(tick);
            Vector2 xy = VideoChartSpace.Project(VideoAnchorPosition(time), spatial.VideoSpace.Pose(time));
            var key = new ScreenAnchorData { tick = tick, xPercent = xy.x, yPercent = xy.y };
            keys.Add(key); keys.Sort((a, b) => a.tick.CompareTo(b.tick)); SetScreenKeys(keys.ToArray()); return key;
        }
        void AddScreenAnchor()
        {
            Seek(VideoKeyTime(VideoAuthoringTick));
            Change(() => EnsureScreenAnchor(PlayheadTick), "Path anchor stored at this beat");
            timelineSelection = TimelineKind.ScreenAnchor; videoInspector = VideoInspectorAnchors;
        }
        CameraZKey EnsureCameraZKey(int tick)
        {
            tick = Mathf.Clamp(tick, 0, Mathf.RoundToInt(chart.endBeat * chart.ticksPerBeat));
            var keys = new List<CameraZKey>(chart.videoSpace.cameraZKeys);
            var key = keys.Find(k => k.tick == tick);
            if (key == null)
            {
                key = new CameraZKey { tick = tick, z = spatial.DistanceAtTime(VideoKeyTime(tick)) };
                keys.Add(key); keys.Sort((a, b) => a.tick.CompareTo(b.tick)); chart.videoSpace.cameraZKeys = keys.ToArray();
            }
            selectedCameraZ = keys.IndexOf(key); return key;
        }
        void AddCameraZKey()
        {
            Seek(VideoKeyTime(VideoAuthoringTick));
            Change(() => EnsureCameraZKey(PlayheadTick), "Camera rail key stored");
            timelineSelection = TimelineKind.VideoZ;
            videoInspector = VideoInspectorRail;
        }
        VideoCameraPoseKey EnsureCameraPoseKey(int tick)
        {
            tick = Mathf.Clamp(tick, 0, Mathf.RoundToInt(chart.endBeat * chart.ticksPerBeat));
            var keys = new List<VideoCameraPoseKey>(chart.videoSpace.cameraPoseKeys ?? new VideoCameraPoseKey[0]);
            var key = keys.Find(k => k.tick == tick);
            if (key == null)
            {
                // Seed from the rig already in force here, so inserting a key can
                // never move the camera: the author only changes what they asked for.
                double time = VideoKeyTime(tick);
                var pose = spatial.VideoSpace.Pose(time);
                Vector3 offset = pose.position - new Vector3(0, 0, spatial.DistanceAtTime(time));
                Vector3 angles = pose.rotation.eulerAngles;
                key = new VideoCameraPoseKey
                {
                    tick = tick,
                    dx = offset.x, dy = offset.y, dz = offset.z,
                    pitch = Mathf.DeltaAngle(0, angles.x),
                    yaw = Mathf.DeltaAngle(0, angles.y),
                    roll = Mathf.DeltaAngle(0, angles.z),
                    fov = pose.fov
                };
                keys.Add(key); keys.Sort((a, b) => a.tick.CompareTo(b.tick)); chart.videoSpace.cameraPoseKeys = keys.ToArray();
                // A key inserted inside an eased span inherits that span's curve.
                int index = keys.IndexOf(key);
                if (index > 0) key.easing = VideoChartSpace.PoseEasing(keys[index - 1]);
            }
            selectedCameraPose = keys.IndexOf(key); return key;
        }
        void AddCameraPoseKey()
        {
            Seek(VideoKeyTime(VideoAuthoringTick));
            Change(() => EnsureCameraPoseKey(PlayheadTick), "Camera pose key stored");
            timelineSelection = TimelineKind.VideoPose;
            videoInspector = VideoInspectorPose;
        }
        void DrawVideoPathInspector()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Path anchors", videoInspector == VideoInspectorAnchors ? selectedButtonStyle : buttonStyle))
                SelectVideoInspector(VideoInspectorAnchors);
            if (GUILayout.Button("Camera rail", videoInspector == VideoInspectorRail ? selectedButtonStyle : buttonStyle))
                SelectVideoInspector(VideoInspectorRail);
            if (GUILayout.Button("Camera pose", videoInspector == VideoInspectorPose ? selectedButtonStyle : buttonStyle))
                SelectVideoInspector(VideoInspectorPose);
            GUILayout.EndHorizontal();
            if (videoInspector == VideoInspectorRail) { DrawVideoCameraInspector(); return; }
            if (videoInspector == VideoInspectorPose) { DrawVideoCameraPoseInspector(); return; }
            for (int i = 0; i < chart.paths.Length; i++)
                if (GUILayout.Button(chart.paths[i].id, i == selectedPath ? selectedButtonStyle : buttonStyle))
                { selectedPath = i; timelineSelection = TimelineKind.ScreenAnchor; RebuildVisuals(); }
            if (GUILayout.Button("Add path")) AddPath();
            DrawScreenAnchorInspector();
        }
        void SelectVideoInspector(int tab)
        {
            videoInspector = tab;
            timelineSelection = tab == VideoInspectorRail ? TimelineKind.VideoZ
                : tab == VideoInspectorPose ? TimelineKind.VideoPose : TimelineKind.ScreenAnchor;
            draggingHandle = false; draggedScreenAnchor = null;
        }
        void SelectVideoInspector(bool cameraRail) => SelectVideoInspector(cameraRail ? VideoInspectorRail : VideoInspectorAnchors);
        // One-line readout of where the rig is right now, so an author can tell at a
        // glance whether the camera is still on the straight rail or has been moved.
        string CameraPoseSummary()
        {
            if (spatial?.VideoSpace == null) return "no video space";
            var pose = spatial.VideoSpace.Pose(songTime);
            Vector3 offset = pose.position - new Vector3(0, 0, spatial.DistanceAtTime(songTime));
            Vector3 euler = pose.rotation.eulerAngles;
            float yaw = Mathf.DeltaAngle(0, euler.y), pitch = Mathf.DeltaAngle(0, euler.x), roll = Mathf.DeltaAngle(0, euler.z);
            bool onRail = offset.sqrMagnitude < .0001f && Mathf.Abs(yaw) < .05f && Mathf.Abs(pitch) < .05f && Mathf.Abs(roll) < .05f;
            if (onRail) return "camera ON RAIL";
            return $"camera {offset.magnitude:0.##}m off rail · yaw {yaw:0.#}° · pitch {pitch:0.#}° · roll {roll:0.#}° · FOV {pose.fov:0.#}°";
        }
        void DrawVideoCameraInspector()
        {
            GUILayout.Label("CAMERA RAIL / NOTE TRAVEL", headingStyle);
            GUILayout.Label("X = 0   Y = 0   this track only sets how fast notes travel", smallStyle);
            var keys = chart.videoSpace.cameraZKeys;
            float z = spatial.DistanceAtTime(songTime);
            if (FloatField("Rail distance", ref z))
            {
                var key = EnsureCameraZKey(PlayheadTick);
                keys = chart.videoSpace.cameraZKeys;
                float minimum = selectedCameraZ > 0 ? keys[selectedCameraZ - 1].z + .001f : -100000;
                float maximum = selectedCameraZ + 1 < keys.Length ? keys[selectedCameraZ + 1].z - .001f : 100000;
                key.z = Mathf.Clamp(z, minimum, maximum);
                timelineSelection = TimelineKind.VideoZ; Rebuild(); Seek(VideoKeyTime(key.tick));
            }
            if (GUILayout.Button("+ Rail key at playhead")) AddCameraZKey();
            var current = Array.Find(chart.videoSpace.cameraZKeys, k => k.tick == PlayheadTick);
            if (current != null)
            {
                string[] ids = { "linear", "easeIn", "easeOut", "easeInOut", "smoother" };
                string[] labels = { "Linear", "Ease In", "Ease Out", "Smooth", "Smoother" };
                int index = Mathf.Max(0, Array.IndexOf(ids, current.easing));
                if (GUILayout.Button("To next key: " + labels[index]))
                    Change(() => current.easing = ids[(index + 1) % ids.Length], "Camera rail interpolation changed");
            }
            GUILayout.Label("Distance must increase. It is the odometer the notes and the path " +
                "anchors are measured along, and it keeps the camera's framing out of note timing.\n\n" +
                "Camera framing now lives in the Camera pose tab: offset and head angles.", smallStyle);
        }
        void DrawVideoCameraPoseInspector()
        {
            GUILayout.Label("CAMERA POSE / RIG", headingStyle);
            var keys = chart.videoSpace.cameraPoseKeys;
            var existing = keys == null ? null : Array.Find(keys, k => k.tick == PlayheadTick);
            var pose = spatial.VideoSpace.Pose(songTime);
            Vector3 offset, angles; float fov;
            if (existing != null)
            {
                // An existing key shows exactly what is stored, so editing one field
                // can never silently rewrite another through angle wrapping.
                offset = new Vector3(existing.dx, existing.dy, existing.dz);
                angles = new Vector3(existing.pitch, existing.yaw, existing.roll);
                fov = VideoChartSpace.KeyFieldOfView(existing);
            }
            else
            {
                offset = pose.position - new Vector3(0, 0, spatial.DistanceAtTime(songTime));
                Vector3 raw = pose.rotation.eulerAngles;
                angles = new Vector3(Mathf.DeltaAngle(0, raw.x), Mathf.DeltaAngle(0, raw.y), Mathf.DeltaAngle(0, raw.z));
                fov = pose.fov;
            }
            bool changed = FloatField("Offset X", ref offset.x);
            changed |= FloatField("Offset Y", ref offset.y);
            changed |= FloatField("Offset Z", ref offset.z);
            changed |= FloatField("Yaw", ref angles.y);
            changed |= FloatField("Pitch", ref angles.x);
            changed |= FloatField("Roll", ref angles.z);
            changed |= FloatField("FOV", ref fov);
            if (changed)
            {
                var key = EnsureCameraPoseKey(PlayheadTick);
                key.dx = offset.x; key.dy = offset.y; key.dz = offset.z;
                key.pitch = angles.x; key.yaw = angles.y; key.roll = angles.z;
                key.fov = Mathf.Clamp(fov, 30, 85);
                timelineSelection = TimelineKind.VideoPose; Rebuild(); Seek(VideoKeyTime(key.tick));
                return;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Pose key here")) AddCameraPoseKey();
            bool previous = GUI.enabled;
            GUI.enabled = previous && keys != null && keys.Length > 1 && existing != null;
            if (GUILayout.Button("Delete pose key")) DeleteCameraPoseKey();
            GUI.enabled = previous;
            GUILayout.EndHorizontal();
            GUI.enabled = previous && existing != null;
            if (GUILayout.Button("Reset this key to the straight rail"))
            {
                var key = existing;
                Change(() => { key.dx = key.dy = key.dz = 0; key.yaw = key.pitch = key.roll = 0; key.fov = VideoChartSpace.FieldOfView; },
                    "Camera pose key reset to the straight rail");
            }
            GUI.enabled = previous;
            if (existing != null)
            {
                string[] ids = { "linear", "easeIn", "easeOut", "easeInOut", "smoother" };
                string[] labels = { "Linear", "Ease In", "Ease Out", "Smooth", "Smoother" };
                int index = Mathf.Max(0, Array.IndexOf(ids, VideoChartSpace.PoseEasing(existing)));
                if (GUILayout.Button("To next key: " + labels[index]))
                    Change(() => existing.easing = ids[(index + 1) % ids.Length], "Camera pose interpolation changed");
            }
            GUILayout.Label("Offset pushes the camera off the rail: +X right, +Y up, +Z deeper into " +
                "the song. Yaw turns the head left/right, pitch looks up/down, roll tilts the frame.\n\n" +
                "The offset is stored as a percentage anchor in the path's own frame, so a turned " +
                "camera bends the track instead of shearing it.\n\n" +
                "A flat video cannot rotate with the camera: large yaw/pitch will detach the 3D " +
                "foreground from the picture. Small angles, dolly and lateral offsets blend best.", smallStyle);
        }
        void DeleteCameraPoseKey()
        {
            var keys = chart.videoSpace.cameraPoseKeys;
            if (keys == null || keys.Length <= 1) return;
            int tick = PlayheadTick;
            Change(() => { var list = new List<VideoCameraPoseKey>(keys); list.RemoveAll(k => k.tick == tick); chart.videoSpace.cameraPoseKeys = list.ToArray(); },
                "Camera pose key deleted");
        }
        void DrawScreenAnchorInspector()
        {
            GUILayout.Label(chart.paths[selectedPath].id + " / VIDEO X% Y%", headingStyle);
            float beat = CurrentBeat;
            if (FloatField("Anchor beat", ref beat, false)) Seek(tempo.SecondsAtBeat(Mathf.Clamp(beat, 0, chart.endBeat)));
            Vector2 xy = VideoChartSpace.Project(VideoAnchorPosition(songTime), spatial.VideoSpace.Pose(songTime));
            bool changed = FloatField("X (%)", ref xy.x); changed |= FloatField("Y (%)", ref xy.y);
            if (changed)
            {
                var key = EnsureScreenAnchor(PlayheadTick);
                key.xPercent = xy.x; key.yPercent = xy.y;
                Rebuild(); Seek(VideoKeyTime(key.tick));
                timelineSelection = TimelineKind.ScreenAnchor;
            }
            if (GUILayout.Button("+ Path anchor here")) AddScreenAnchor();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous")) SeekScreenAnchor(-1);
            if (GUILayout.Button("Next")) SeekScreenAnchor(1);
            GUILayout.EndHorizontal();
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && ScreenKeys().Length > 1 && Array.Exists(ScreenKeys(), k => k.tick == PlayheadTick);
            if (GUILayout.Button("Delete path anchor")) DeleteScreenAnchor();
            GUI.enabled = enabled;
            GUILayout.Label("(0,0) top-left / (100,100) bottom-right. Black bars excluded. Drag the cross; coordinates match at the anchor's time. Values outside 0–100 place a point off-frame.", smallStyle);
        }
        void SeekScreenAnchor(int direction)
        {
            int target = direction > 0 ? int.MaxValue : int.MinValue;
            foreach (var key in ScreenKeys())
                if (direction > 0 && key.tick > PlayheadTick) target = Math.Min(target, key.tick);
                else if (direction < 0 && key.tick < PlayheadTick) target = Math.Max(target, key.tick);
            if (target != int.MaxValue && target != int.MinValue) Seek(VideoKeyTime(target));
        }
        void DeleteScreenAnchor()
        {
            if (ScreenKeys().Length <= 1) return;
            int tick = PlayheadTick;
            Change(() => { var keys = new List<ScreenAnchorData>(ScreenKeys()); keys.RemoveAll(k => k.tick == tick); SetScreenKeys(keys.ToArray()); }, "Path anchor deleted");
        }
        bool DeleteVideoTimelineSelection()
        {
            if (!VideoSpaceEditing) return false;
            if (timelineSelection == TimelineKind.VideoZ)
            {
                var keys = chart.videoSpace.cameraZKeys;
                int index = Array.FindIndex(keys, k => k.tick == PlayheadTick);
                if (index > 0 && index < keys.Length - 1)
                    Change(() => { var list = new List<CameraZKey>(keys); list.RemoveAt(index); chart.videoSpace.cameraZKeys = list.ToArray(); }, "Camera rail key deleted");
                return true;
            }
            if (timelineSelection == TimelineKind.VideoPose) { DeleteCameraPoseKey(); return true; }
            if (timelineSelection == TimelineKind.ScreenAnchor) { DeleteScreenAnchor(); return true; }
            return false;
        }
        Rect VideoGuiRect
        {
            get { Rect r = sceneCamera.pixelRect; return new Rect(r.x / uiScale, (Screen.height - r.yMax) / uiScale, r.width / uiScale, r.height / uiScale); }
        }
        Vector2 VideoPercentAtPixel(Vector2 pixel)
        {
            Rect r = sceneCamera.pixelRect;
            return new Vector2(Mathf.Clamp01((pixel.x - r.x) / r.width) * 100,
                Mathf.Clamp01((pixel.y - (Screen.height - r.yMax)) / r.height) * 100);
        }
        void DrawVideoSpaceHandles()
        {
            if (playing || mode != ChartEditMode.Paths || videoInspector != VideoInspectorAnchors) return;
            Vector2 center = WorldGui(VideoAnchorPosition(songTime));
            Rect frame = VideoGuiRect;
            if (center.x < frame.x || center.x > frame.xMax || center.y < frame.y || center.y > frame.yMax) return;
            GuiLine(center - Vector2.right * 8, center + Vector2.right * 8, Color.yellow, 2);
            GuiLine(center - Vector2.up * 8, center + Vector2.up * 8, Color.yellow, 2);
            Vector2 xy = VideoPercentAtPixel(center * uiScale);
            GUI.Label(new Rect(Mathf.Clamp(center.x + 12, frame.x, frame.xMax - 120), Mathf.Clamp(center.y + 8, frame.y, frame.yMax - 24), 120, 22),
                $"{xy.x:0.##}%, {xy.y:0.##}%", smallStyle);
        }
        void ReadVideoSpaceHandles(Event e)
        {
            if (e.button != 0) return;
            if (e.type == EventType.MouseUp)
            { if (draggingHandle) e.Use(); draggingHandle = false; draggedScreenAnchor = null; return; }
            if (WorkspaceInputBlocked || playing || inspectorResizing || timelineResizing) return;
            if (draggingHandle && draggedScreenAnchor != null && e.type == EventType.MouseDrag)
            {
                Vector2 xy = VideoPercentAtPixel(e.mousePosition);
                draggedScreenAnchor.xPercent = xy.x; draggedScreenAnchor.yPercent = xy.y;
                RebuildVisuals(); e.Use(); return;
            }
            if (e.type != EventType.MouseDown || !PointerInViewport(e.mousePosition)) return;
            if (mode == ChartEditMode.Paths && videoInspector == VideoInspectorAnchors)
            {
                Vector2 center = WorldGui(VideoAnchorPosition(songTime)) * uiScale;
                if (Vector2.Distance(center, e.mousePosition) <= 20 * uiScale || e.shift)
                {
                    Seek(VideoKeyTime(VideoAuthoringTick)); RecordUndo();
                    draggedScreenAnchor = EnsureScreenAnchor(PlayheadTick);
                    if (e.shift) { Vector2 xy = VideoPercentAtPixel(e.mousePosition); draggedScreenAnchor.xPercent = xy.x; draggedScreenAnchor.yPercent = xy.y; }
                    draggingHandle = true; clearGuiFocus = true;
                    timelineSelection = TimelineKind.ScreenAnchor;
                    RebuildVisuals(); e.Use();
                }
            }
            else if (mode == ChartEditMode.Notes)
            { var handle = PickHandle(e.mousePosition); if (handle != null) { SelectHandle(handle); e.Use(); } }
        }
    }
}
