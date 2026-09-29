using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        IEnumerator RunVideoSpaceChecks()
        {
            videoChecksPassed = true;
            var report = new List<string>();
            Action<bool, string> check = (ok, name) => { videoChecksPassed &= ok; report.Add((ok ? "PASS " : "FAIL ") + name); };
            float deadline = Time.realtimeSinceStartup + 35;
            while (videoLoading && Time.realtimeSinceStartup < deadline) yield return null;
            check(VideoSpaceEditing && videoBga?.IsLoaded == true && videoBga.Package.width == 2560 && videoBga.Package.height == 1440,
                "H3 2560x1440 package loads in video-space editing");
            if (!VideoSpaceEditing)
            { report.Add(status); File.WriteAllLines(Path.Combine(smokeDirectory, "video-space-checks.txt"), report); yield break; }
            SetPlaying(false);
            string snapshot = ChartSnapshotForUndo(), originalFile = filePath;
            check(mode == ChartEditMode.Paths && chart.videoSpace.schemaVersion == 2, "Opening a video chart starts directly in Path-only authoring");
            foreach (var workspace in new[] { ChartEditMode.Bga, ChartEditMode.Paths, ChartEditMode.Notes, ChartEditMode.Stage })
            {
                SetMode(workspace);
                var rows = TimelineRows();
                check(rows.FindAll(r => r.kind == TimelineKind.VideoZ).Count == 1 &&
                    rows.FindAll(r => r.kind == TimelineKind.ScreenAnchor).Count == chart.paths.Length &&
                    !rows.Exists(r => r.label.Contains("SCENE") || r.kind == TimelineKind.Stage),
                    workspace + ": Camera Z always accompanied by every Path X/Y row, no Scene row");
                if (workspace == ChartEditMode.Stage) check(mode == ChartEditMode.Paths, "Old Stage shortcut redirects to Paths");
            }
            SetMode(ChartEditMode.Bga);
            TimelineItem firstPathItem = null;
            foreach (var item in TimelineItems(new TimelineRow { kind = TimelineKind.ScreenAnchor, path = 1 })) { firstPathItem = item; break; }
            SelectTimelineItem(firstPathItem);
            check(mode == ChartEditMode.Paths && selectedPath == 1 && !editVideoCameraZ, "Path row opens that Path's percent inspector from BGA");
            foreach (var item in TimelineItems(new TimelineRow { kind = TimelineKind.VideoZ })) { SelectTimelineItem(item); break; }
            check(mode == ChartEditMode.Paths && editVideoCameraZ && TimelineRows().Exists(r => r.kind == TimelineKind.ScreenAnchor),
                "Camera Z editing stays in Paths without hiding anchor tracks");
            CheckLegacyPathMigration(snapshot, check);
            CheckFreeCameraRig(snapshot, check);
            SetMode(ChartEditMode.Paths); selectedPath = 0;
            SelectVideoInspector(true);
            check(timelineSelection == TimelineKind.VideoZ, "Camera inspector tab targets Z deletion, not Path anchors");
            SelectVideoInspector(false);
            check(timelineSelection == TimelineKind.ScreenAnchor, "Path inspector tab targets Path anchor deletion");
            check(!VideoChartSpace.Enabled(JsonUtility.FromJson<ChartData>("{\"version\":1}")), "Omitted videoSpace stays legacy, not a phantom empty track");
            int end = Mathf.RoundToInt(chart.endBeat * chart.ticksPerBeat);
            int tick4 = Mathf.RoundToInt((float)tempo.BeatAtSeconds(4) * chart.ticksPerBeat);
            int tick8 = Mathf.RoundToInt((float)tempo.BeatAtSeconds(8) * chart.ticksPerBeat);
            chart.videoSpace.cameraZKeys = new[] { new CameraZKey { tick = 0, z = 0 },
                new CameraZKey { tick = tick4, z = 30 }, new CameraZKey { tick = tick8, z = 80 }, new CameraZKey { tick = end, z = 150 } };
            Rebuild(); Seek(2);
            check(Mathf.Abs(sceneCamera.transform.position.z - 15) < .001f, "Camera Z is interpolated in seconds at 2s");
            Seek(6); check(Mathf.Abs(sceneCamera.transform.position.z - 55) < .001f, "Camera Z key segment changes at 4s");
            chart.videoSpace.cameraZKeys[0].easing = "easeIn"; Seek(2);
            check(Mathf.Abs(sceneCamera.transform.position.z - 7.5f) < .001f, "Outgoing Z easing is applied");
            chart.videoSpace.cameraZKeys[0].easing = "linear";
            // Check percent projection against Unity's camera, not just the inverse helper.
            float maxError = 0;
            foreach (double time in new[] { 0.0, 2.0, 6.0, 12.0, 19.0 })
            {
                Seek(time); UpdateViewportRect();
                foreach (Vector2 xy in new[] { Vector2.zero, new Vector2(100, 100), new Vector2(50, 50), new Vector2(27.25f, 76.5f), new Vector2(85, 15) })
                {
                    Vector3 world = VideoChartSpace.Unproject(xy.x, xy.y, spatial.DistanceAtTime(songTime));
                    Vector3 viewport = sceneCamera.WorldToViewportPoint(world);
                    maxError = Mathf.Max(maxError, Vector2.Distance(xy, new Vector2(viewport.x * 100, (1 - viewport.y) * 100)));
                }
                check(sceneCamera.transform.position.x == 0 && sceneCamera.transform.position.y == 0 &&
                    Quaternion.Angle(sceneCamera.transform.rotation, Quaternion.identity) < .001f, "No XY motion or rotation at " + time + "s");
            }
            check(maxError < .001f, "25 percent/world/camera round trips < 0.001 percent; error=" + maxError);
            float width = panelWidth, scale = uiScale, height = timelineHeight;
            foreach (float size in new[] { 1f, 1.25f, 1.75f })
            {
                uiScale = size; panelWidth = ClampInspectorPanelWidth(390); timelineHeight = 280; UpdateViewportRect();
                Rect r = sceneCamera.pixelRect;
                Vector2 xy = new Vector2(31.75f, 81.25f);
                Vector2 pixel = new Vector2(r.x + r.width * xy.x / 100, Screen.height - r.yMax + r.height * xy.y / 100);
                Vector3 world = VideoChartSpace.Unproject(xy.x, xy.y, spatial.DistanceAtTime(songTime));
                Vector3 projected = sceneCamera.WorldToScreenPoint(world);
                check(Vector2.Distance(VideoPercentAtPixel(pixel), xy) < .001f &&
                    Vector2.Distance(pixel, new Vector2(projected.x, Screen.height - projected.y)) < .5f,
                    "UI scale " + size + " / inspector resize: < half-pixel error and black bars excluded");
                check(Mathf.Abs(sceneCamera.aspect - 16f / 9) < .00001f, "16:9 projection at UI scale " + size);
            }
            panelWidth = width; uiScale = scale; timelineHeight = height; UpdateViewportRect();
            SetFlightMode(true); CaptureMouse(); orbitPivot += Vector3.one * 30; ApplyOrbitCamera();
            check(!flyMode && Cursor.lockState == CursorLockMode.None && sceneCamera.transform.position.x == 0 &&
                Quaternion.Angle(sceneCamera.transform.rotation, Quaternion.identity) < .001f, "Navigation cannot reframe the video");
            chart.videoBga.enabled = false; ReadNavigationMode();
            check(VideoCameraLocked, "Hiding video preserves coordinate system"); chart.videoBga.enabled = true;
            SetMode(ChartEditMode.Paths); Seek(4); AddScreenAnchor();
            var anchor = Array.Find(chart.paths[selectedPath].screenAnchors, k => k.tick == tick4);
            anchor.xPercent = 42; anchor.yPercent = 66; Rebuild();
            Vector3 projectedAnchor = sceneCamera.WorldToViewportPoint(spatial.VideoSpace.AnchorWorld(anchor));
            check(Mathf.Abs(projectedAnchor.x - .42f) < .00001f && Mathf.Abs(projectedAnchor.y - .34f) < .00001f, "Path anchor lands on exact video percentage at its tick");
            // The free camera rig must move the camera off the rail while keeping the
            // authored percentage exact at the anchor's own tick. Verified against the
            // real Unity camera, not just the inverse helper.
            int rigEnd = Mathf.RoundToInt(chart.endBeat * chart.ticksPerBeat);
            chart.videoSpace.cameraPoseKeys = new[] {
                new VideoCameraPoseKey { tick = 0, dx = 2.5f, dy = 1, dz = -3, yaw = 15, pitch = -6, roll = 4, fov = 50 },
                new VideoCameraPoseKey { tick = tick4, dx = -4, dy = 3, dz = 9, yaw = -20, pitch = 7, roll = -8, fov = 62 },
                new VideoCameraPoseKey { tick = rigEnd, fov = 53 } };
            Seek(4); Rebuild(); UpdateViewportRect();
            var rigPose = spatial.VideoSpace.Pose(spatial.VideoSpace.TimeAt(tick4));
            float rigError = 100;
            var rigAnchor = Array.Find(chart.paths[selectedPath].screenAnchors, k => k.tick == tick4);
            if (rigAnchor != null)
            {
                Vector3 viewport = sceneCamera.WorldToViewportPoint(spatial.VideoSpace.AnchorWorld(rigAnchor));
                rigError = Vector2.Distance(new Vector2(rigAnchor.xPercent, rigAnchor.yPercent),
                    new Vector2(viewport.x * 100, (1 - viewport.y) * 100));
            }
            check(rigAnchor != null && rigError < .002f && Quaternion.Angle(rigPose.rotation, Quaternion.identity) > 5 &&
                rigPose.position.sqrMagnitude > 1 && Mathf.Abs(rigPose.fov - 62) < .001f,
                "Offset, turned and re-lensed rig keeps the anchor on its authored percentage; error=" + rigError);
            chart.videoSpace.cameraPoseKeys = VideoChartSpace.DefaultCameraPose(rigEnd); Rebuild(); Seek(4);
            int initialAnchors = chart.paths[selectedPath].screenAnchors.Length;
            Seek(6); AddScreenAnchor(); Undo();
            check(chart.paths[selectedPath].screenAnchors.Length == initialAnchors, "Screen anchor creation supports Undo"); Redo();
            check(chart.paths[selectedPath].screenAnchors.Length == initialAnchors + 1, "Screen anchor creation supports Redo");
            SetMode(ChartEditMode.Paths); Seek(4);
            Vector2 handlePixel = WorldGui(VideoAnchorPosition(songTime)) * uiScale;
            ReadVideoSpaceHandles(new Event { type = EventType.MouseDown, button = 0, mousePosition = handlePixel });
            Rect videoRect = sceneCamera.pixelRect;
            Vector2 dragPixel = new Vector2(videoRect.x + videoRect.width * .28f, Screen.height - videoRect.yMax + videoRect.height * .78f);
            ReadVideoSpaceHandles(new Event { type = EventType.MouseDrag, button = 0, mousePosition = dragPixel });
            ReadVideoSpaceHandles(new Event { type = EventType.MouseUp, button = 0, mousePosition = dragPixel });
            var pathAnchor = Array.Find(chart.paths[selectedPath].screenAnchors, k => k.tick == tick4);
            check(pathAnchor != null && Mathf.Abs(pathAnchor.xPercent - 28) < .001f && Mathf.Abs(pathAnchor.yPercent - 78) < .001f,
                "Dragging the video cross edits percentages, not world XY");
            var row = new TimelineRow { kind = TimelineKind.ScreenAnchor, path = selectedPath };
            TimelineItem moved = null;
            foreach (var item in TimelineItems(row)) if (((ScreenAnchorData)item.data).tick == tick4) moved = item;
            int tick5 = Mathf.RoundToInt((float)tempo.BeatAtSeconds(5) * chart.ticksPerBeat);
            timelineDragUndo = false;
            check(moved != null && MoveTimelineItem(moved, tick5) && ((ScreenAnchorData)moved.data).tick == tick5, "Screen anchor timeline retiming");
            check(moved != null && Mathf.Abs(((ScreenAnchorData)moved.data).xPercent - 28) < .001f, "Retiming retains authored percentages");
            DeleteTimelineSelection();
            check(!Array.Exists(chart.paths[selectedPath].screenAnchors, k => k.tick == tick5), "Timeline deletes screen anchor"); Undo();
            SetMode(ChartEditMode.Paths); Seek(4);
            TimelineItem cameraItem = null;
            foreach (var item in TimelineItems(new TimelineRow { kind = TimelineKind.VideoZ })) if (item.index == 1) cameraItem = item;
            timelineDragUndo = false;
            check(MoveTimelineItem(cameraItem, tick5), "Camera Z timeline retiming");
            foreach (var item in TimelineItems(new TimelineRow { kind = TimelineKind.VideoZ }))
                if (item.index == 0 || item.index == chart.videoSpace.cameraZKeys.Length - 1)
                    check(!MoveTimelineItem(item, tick4), "Camera endpoint cannot leave boundary " + item.index);
            bool invalidAspect = false, invalidZ = false;
            try { VideoBgaRuntime.Require16By9(1920, 1200); } catch (InvalidDataException) { invalidAspect = true; }
            VideoBgaRuntime.Require16By9(2560, 1440);
            check(invalidAspect, "Non-16:9 rejected; H3 2560x1440 accepted");
            var invalid = JsonUtility.FromJson<ChartData>(JsonUtility.ToJson(chart)); invalid.videoSpace.cameraZKeys[1].z = -1;
            try { CheckEditableChart(invalid); } catch (InvalidDataException) { invalidZ = true; }
            check(invalidZ, "Backward camera Z rejected during load validation");
            invalid = JsonUtility.FromJson<ChartData>(JsonUtility.ToJson(chart)); invalid.paths[0].screenAnchors[0].xPercent = float.NaN;
            bool invalidPercent = false;
            try { CheckEditableChart(invalid); } catch (InvalidDataException) { invalidPercent = true; }
            check(invalidPercent, "Non-finite screen percentages rejected");
            var tempoFixture = JsonUtility.FromJson<ChartData>(JsonUtility.ToJson(chart));
            tempoFixture.tempos = new[] { new TempoData { tick = 0, bpm = 120 }, new TempoData { tick = 960, bpm = 60 } };
            tempoFixture.videoSpace.cameraZKeys = new[] { new CameraZKey { tick = 0, z = 0 }, new CameraZKey { tick = 1920, z = 60 } };
            var secondsSpace = new VideoChartSpace(tempoFixture, new TempoMap(tempoFixture.tempos, tempoFixture.ticksPerBeat));
            check(Mathf.Abs(secondsSpace.CameraZ(1.5) - 30) < .001f, "Z interpolation uses seconds across a BPM change, not linear beats");
            // Runtime uses the same projection even if a legacy camera key tries to rotate it.
            chart.cameraKeys[0].orbit = 120; chart.cameraKeys[0].roll = 70; Rebuild();
            spatial.EvaluateCamera(evaluatorCamera, songTime);
            check(Quaternion.Angle(evaluatorCamera.transform.rotation, Quaternion.identity) < .001f &&
                Vector3.Distance(evaluatorCamera.transform.position, sceneCamera.transform.position) < .001f, "Shared playback camera ignores legacy rotation tracks");
            SetMode(ChartEditMode.Notes); Seek(4); AddNote();
            check(PreviewGeometry && !chartCameraPreview && visualRoot.Find("Preview paths and notes") != null && editorLivePaths == null,
                "One editable preview renderer; no duplicate static/live paths");
            check(previewMotionRoot.Find("Video scene line") == null, "Preview contains no separate Scene line");
            var noteObject = previewMotionRoot.Find(chart.notes[selectedNote].id);
            check(noteObject != null && noteObject.GetComponent<ChartEditorHandle>() != null, "Final-render notes remain selectable in edit view");
            var runtimeNote = new RuntimeNote { Data = chart.notes[selectedNote], HitTime = NoteHitTime(chart.notes[selectedNote]) };
            var originalSpeed = new SpatialDirector(chart, tempo, 1);
            originalSpeed.NotePose(runtimeNote, 2, out var earlyNote, out _); originalSpeed.NotePose(runtimeNote, 4, out var hitNote, out _);
            check(Vector3.Distance(earlyNote, hitNote) < .00001f, "1x retains original fixed-world coordinates without extra Z-scroll");
            spatial.NotePose(runtimeNote, 4, out var fastHitNote, out _);
            check(Vector3.Distance(fastHitNote, hitNote) < .00001f, "Personal scroll speed retains the exact authored hit position");
            Rect beforeF5 = sceneCamera.pixelRect; ToggleCameraPreview();
            check(playing && !chartCameraPreview && sceneCamera.pixelRect == beforeF5, "F5 plays in place without read-only mode or viewport change");
            yield return new WaitForSecondsRealtime(.5f); SetPlaying(false);
            check(sceneCamera.transform.position.z > spatial.DistanceAtTime(4), "Playback advances camera along keyed Z");
            Seek(4); yield return WaitForVideoPosition(4);
            check(videoBga?.Texture != null && Math.Abs(videoBga.Time - 4) < .15, "Real H3 video seeks with chart timeline");
            string authoredSpace = JsonUtility.ToJson(chart.videoSpace), authoredPath = JsonUtility.ToJson(chart.paths[0]);
            string packagePath = Path.Combine(smokeDirectory, "roundtrip.grchart");
            check(TryExportChartPackage(packagePath), "Export portable package with new coordinate tracks");
            check(TryLoadChartOrPackage(packagePath), "Reimport portable package");
            check(authoredSpace == JsonUtility.ToJson(chart.videoSpace) && authoredPath == JsonUtility.ToJson(chart.paths[0]),
                "Package round-trip preserves exact Z, easing and percentages");
            check(File.Exists(ResolveMediaPath(chart.videoBga.packageManifest)), "Package carries local video manifest");
            deadline = Time.realtimeSinceStartup + 30;
            while (videoLoading && Time.realtimeSinceStartup < deadline) yield return null;
            statusUntil = 0;
            SetMode(ChartEditMode.Paths); Seek(5); videoInspector = VideoInspectorRail; yield return WaitForVideoPosition(5);
            yield return CaptureCheckScreenshot("video-path-camera-z.png");
            SetMode(ChartEditMode.Paths); yield return CaptureCheckScreenshot("video-path-anchors.png");
            SetMode(ChartEditMode.Bga); yield return CaptureCheckScreenshot("video-path-bga-tracks.png");
            chart = JsonUtility.FromJson<ChartData>(snapshot); filePath = originalFile; undo.Clear(); redo.Clear(); selectedNote = -1;
            savedChartJson = JsonUtility.ToJson(chart); needsSaveAs = true; Rebuild(); RefreshBgaBinding();
            check(TryExportChartPackage(Path.Combine(smokeDirectory, "Summer20_H3_PathOnly_20s.grchart")), "Clean Path-only starter package contains no test edits");
            report.Add("RESULT=" + (videoChecksPassed ? "PASS" : "FAIL") + " CHECKS=" + report.Count);
            File.WriteAllLines(Path.Combine(smokeDirectory, "video-space-checks.txt"), report);
            Debug.Log("VIDEO_SPACE_" + (videoChecksPassed ? "PASS" : "FAIL") + "\n" + string.Join("\n", report));
            SetMode(ChartEditMode.Paths); Seek(4); SetStatus("Path-only editing · Shared Camera Z + independent video-percent anchors");
        }

        // The free camera rig is deliberately additive: an absent or all-zero track
        // must reproduce the original axis-aligned camera exactly, while a keyed rig
        // still lands every percentage anchor on the judgement plane at its own tick.
        static void CheckFreeCameraRig(string snapshot, Action<bool, string> check)
        {
            var source = JsonUtility.FromJson<ChartData>(snapshot);
            int end = Mathf.RoundToInt(source.endBeat * source.ticksPerBeat);
            var plain = JsonUtility.FromJson<ChartData>(snapshot);
            plain.videoSpace.cameraPoseKeys = null;
            var clock = new TempoMap(plain.tempos, plain.ticksPerBeat);
            var plainSpace = new VideoChartSpace(plain, clock);
            var zero = JsonUtility.FromJson<ChartData>(snapshot);
            if (zero.videoSpace.cameraPoseKeys == null || zero.videoSpace.cameraPoseKeys.Length == 0)
                zero.videoSpace.cameraPoseKeys = VideoChartSpace.DefaultCameraPose(end);
            // An omitted FOV field reads back as zero, so exercise that sentinel too.
            foreach (var key in zero.videoSpace.cameraPoseKeys) { key.dx = key.dy = key.dz = key.yaw = key.pitch = key.roll = 0; key.fov = 0; }
            var zeroSpace = new VideoChartSpace(zero, clock);
            bool identityExact = true;
            foreach (double time in new[] { 0.0, 2.5, 7.5, 13.0, 19.0 })
            {
                var a = plainSpace.Pose(time); var b = zeroSpace.Pose(time);
                identityExact &= a.position == new Vector3(0, 0, plainSpace.CameraZ(time)) &&
                    Quaternion.Angle(a.rotation, Quaternion.identity) < .0001f &&
                    Mathf.Abs(a.fov - VideoChartSpace.FieldOfView) < .0001f;
                identityExact &= (a.position - b.position).sqrMagnitude < 1e-10f &&
                    Quaternion.Angle(a.rotation, b.rotation) < .0001f && Mathf.Abs(a.fov - b.fov) < 1e-6f;
            }
            check(identityExact, "No pose track, or an all-zero pose track, reproduces the exact axis-aligned camera");

            var moved = JsonUtility.FromJson<ChartData>(snapshot);
            moved.videoSpace.cameraPoseKeys = new[] {
                new VideoCameraPoseKey { tick = 0, dx = 3, dy = 1.5f, dz = -4, yaw = 18, pitch = -7, roll = 6, fov = 47, easing = "smoother" },
                new VideoCameraPoseKey { tick = end / 2, dx = -2.5f, dy = 4, dz = 12, yaw = -25, pitch = 5, roll = -9, fov = 60, easing = "easeInOut" },
                new VideoCameraPoseKey { tick = end, dx = 6, yaw = 40, fov = 0 } };
            VideoChartSpace.Validate(moved);
            var movedSpace = new VideoChartSpace(moved, clock);
            float anchorError = 0, depthError = 0;
            foreach (int tick in new[] { 0, end / 4, end / 2, end * 3 / 4, end })
            {
                var pose = movedSpace.Pose(movedSpace.TimeAt(tick));
                foreach (Vector2 xy in new[] { Vector2.zero, new Vector2(100, 100), new Vector2(50, 50),
                    new Vector2(27.25f, 76.5f), new Vector2(-20, 140) })
                {
                    var key = new ScreenAnchorData { tick = tick, xPercent = xy.x, yPercent = xy.y };
                    Vector3 world = movedSpace.AnchorWorld(key);
                    anchorError = Mathf.Max(anchorError, Vector2.Distance(xy, VideoChartSpace.Project(world, pose)));
                    depthError = Mathf.Max(depthError, Mathf.Abs(
                        Vector3.Dot(world - pose.position, pose.rotation * Vector3.forward) - SpatialDirector.NearDepth));
                }
            }
            check(anchorError < .002f && depthError < .0005f,
                "Offset, turned and eased rig lands every anchor on the judgement plane at its own tick; " + anchorError + " / " + depthError);

            // A camera that turns past 90 degrees stops advancing in world Z. Anchors
            // must still be interpolated by rail distance, or the track would fold.
            var turned = JsonUtility.FromJson<ChartData>(snapshot);
            turned.videoSpace.cameraPoseKeys = new[] { new VideoCameraPoseKey { tick = 0, yaw = 0 },
                new VideoCameraPoseKey { tick = end, yaw = 160 } };
            var turnedSpace = new VideoChartSpace(turned, clock);
            var turnedPath = turned.paths[0];
            float segmentError = 0;
            for (int i = 0; i < turnedPath.screenAnchors.Length; i++)
            {
                var anchor = turnedPath.screenAnchors[i];
                segmentError = Mathf.Max(segmentError, Vector3.Distance(turnedSpace.AnchorWorld(anchor),
                    turnedSpace.PathAtDistance(turnedPath, turnedSpace.AnchorDistance(anchor))));
            }
            check(turnedPath.screenAnchors.Length > 1 && segmentError < .0001f &&
                turnedSpace.Pose(turnedSpace.TimeAt(end)).rotation != Quaternion.identity,
                "Anchors interpolate by rail distance, so a camera turning past 90 degrees cannot fold the track; error=" + segmentError);

            var straightEnd = plainSpace.PathAtDistance(plain.paths[0],
                plainSpace.CameraZ(clock.SecondsAtBeat(plain.endBeat)) + SpatialDirector.NearDepth);
            var bentEnd = movedSpace.PathAtDistance(moved.paths[0],
                movedSpace.CameraZ(clock.SecondsAtBeat(moved.endBeat)) + SpatialDirector.NearDepth);
            check(Vector3.Distance(straightEnd, bentEnd) > 5,
                "A moved rig bends the far end of the track in world space; delta=" + Vector3.Distance(straightEnd, bentEnd));

            // The far visibility gate sits beyond the last anchor, so the rail must
            // extend past the authored range instead of clamping onto the endpoint.
            var railPath = plain.paths[0];
            var lastKey = railPath.screenAnchors[railPath.screenAnchors.Length - 1];
            float lastDistance = plainSpace.AnchorDistance(lastKey);
            Vector3 lastPoint = plainSpace.AnchorWorld(lastKey);
            Vector3 beyondPoint = plainSpace.PathAtDistance(railPath, lastDistance + 30);
            check(Mathf.Abs(beyondPoint.z - (lastDistance + 30)) < .0001f &&
                Mathf.Abs(beyondPoint.x - lastPoint.x) < .0001f && Mathf.Abs(beyondPoint.y - lastPoint.y) < .0001f,
                "The rail extends past the last anchor out to the far gate; z=" + beyondPoint.z + " want=" + (lastDistance + 30));
            var movedRail = moved.paths[0];
            var movedLastKey = movedRail.screenAnchors[movedRail.screenAnchors.Length - 1];
            Vector3 movedLastPoint = movedSpace.AnchorWorld(movedLastKey);
            Vector3 movedBeyond = movedSpace.PathAtDistance(movedRail, movedSpace.AnchorDistance(movedLastKey) + 30);
            check(Vector3.Distance(movedBeyond, movedLastPoint) > 20,
                "A posed rig extends its last anchor along its own rail instead of collapsing; delta=" + Vector3.Distance(movedBeyond, movedLastPoint));

            var director = new SpatialDirector(moved, clock);
            bool planeValid = true;
            foreach (double time in new[] { 1.0, 6.0, 12.0, 18.0 })
            {
                var pose = movedSpace.Pose(time);
                planeValid &= Vector3.Distance(director.JudgementCenter(time),
                    pose.position + pose.rotation * Vector3.forward * SpatialDirector.NearDepth) < .0001f;
                planeValid &= Vector3.Distance(director.CameraUp(time), pose.rotation * Vector3.up) < .0001f;
            }
            check(planeValid, "Judgement plane centre and screen up follow the authored rig");

            bool sentinelOk = true;
            var sentinel = JsonUtility.FromJson<ChartData>(snapshot);
            sentinel.videoSpace.cameraPoseKeys = new[] { new VideoCameraPoseKey { tick = 0, fov = 0 } };
            try { VideoChartSpace.Validate(sentinel); } catch (InvalidDataException) { sentinelOk = false; }
            check(sentinelOk && Mathf.Abs(new VideoChartSpace(sentinel, clock).Pose(0).fov - VideoChartSpace.FieldOfView) < .001f,
                "A hand-written pose key without an FOV field falls back to 53 degrees");

            bool badOrder = false, badValue = false, badFov = false, badTick = false;
            var bad = JsonUtility.FromJson<ChartData>(snapshot);
            bad.videoSpace.cameraPoseKeys = new[] { new VideoCameraPoseKey { tick = 1000 }, new VideoCameraPoseKey { tick = 500 } };
            try { VideoChartSpace.Validate(bad); } catch (InvalidDataException) { badOrder = true; }
            bad = JsonUtility.FromJson<ChartData>(snapshot);
            bad.videoSpace.cameraPoseKeys = new[] { new VideoCameraPoseKey { tick = 0, yaw = float.NaN } };
            try { VideoChartSpace.Validate(bad); } catch (InvalidDataException) { badValue = true; }
            bad = JsonUtility.FromJson<ChartData>(snapshot);
            bad.videoSpace.cameraPoseKeys = new[] { new VideoCameraPoseKey { tick = 0, fov = 12 } };
            try { VideoChartSpace.Validate(bad); } catch (InvalidDataException) { badFov = true; }
            bad = JsonUtility.FromJson<ChartData>(snapshot);
            bad.videoSpace.cameraPoseKeys = new[] { new VideoCameraPoseKey { tick = end + 1 } };
            try { VideoChartSpace.Validate(bad); } catch (InvalidDataException) { badTick = true; }
            check(badOrder && badValue && badFov && badTick,
                "Pose keys reject unordered ticks, non-finite angles, impossible FOV and out-of-song ticks");
        }

        static void CheckLegacyPathMigration(string snapshot, Action<bool, string> check)
        {
            var legacy = JsonUtility.FromJson<ChartData>(snapshot);
            int end = Mathf.RoundToInt(legacy.endBeat * legacy.ticksPerBeat);
            legacy.videoSpace.schemaVersion = 1;
            legacy.videoSpace.sceneAnchors = new[] {
                new ScreenAnchorData { tick = 0, xPercent = 10, yPercent = 30 },
                new ScreenAnchorData { tick = end / 3, xPercent = 90, yPercent = 60 },
                new ScreenAnchorData { tick = end, xPercent = 10, yPercent = 30 } };
            legacy.paths[0].screenAnchors = VideoChartSpace.DefaultAnchors(end, 90, 60);
            legacy.videoSpace.cameraZKeys[0].easing = "smoother";
            VideoChartSpace.Validate(legacy);
            var clock = new TempoMap(legacy.tempos, legacy.ticksPerBeat);
            var oldSpace = new VideoChartSpace(legacy, clock);
            var samples = new Vector3[legacy.paths.Length, 51];
            for (int p = 0; p < legacy.paths.Length; p++)
                for (int t = 0; t <= 50; t++)
                    samples[p, t] = oldSpace.PathAtDistance(legacy.paths[p], oldSpace.CameraZ(clock.SecondsAtBeat(legacy.endBeat) * t / 50) + SpatialDirector.NearDepth);
            VideoChartSpace.Initialize(legacy); VideoChartSpace.Validate(legacy);
            check(legacy.videoSpace.schemaVersion == 2 && legacy.videoSpace.sceneAnchors.Length == 0, "V1 Scene is folded into V2 independent Paths");
            var newSpace = new VideoChartSpace(legacy, clock); float error = 0;
            for (int p = 0; p < legacy.paths.Length; p++)
                for (int t = 0; t <= 50; t++)
                    error = Mathf.Max(error, Vector3.Distance(samples[p, t], newSpace.PathAtDistance(legacy.paths[p],
                        newSpace.CameraZ(clock.SecondsAtBeat(legacy.endBeat) * t / 50) + SpatialDirector.NearDepth)));
            check(error < .0001f, "Migration preserves complete curves across all Paths; error=" + error);
            check(Array.Exists(legacy.paths[0].screenAnchors, k => k.xPercent > 100), "Migration preserves off-frame geometry without clamping");
            string once = JsonUtility.ToJson(legacy); VideoChartSpace.Initialize(legacy);
            check(once == JsonUtility.ToJson(legacy), "Path-only migration is idempotent");
            Vector3 untouched = newSpace.PathAtDistance(legacy.paths[1], 30);
            legacy.paths[0].screenAnchors[0].xPercent += 5;
            check(Vector3.Distance(untouched, newSpace.PathAtDistance(legacy.paths[1], 30)) == 0, "Editing one Path cannot move another Path");
        }
    }
}
