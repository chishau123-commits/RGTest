using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        bool videoChecksPassed;
        IEnumerator RunLockedVideoCameraChecks()
        {
            videoChecksPassed = true;
            var report = new List<string>();
            Action<bool, string> check = (ok, name) => { videoChecksPassed &= ok; report.Add((ok ? "PASS " : "FAIL ") + name); };
            float deadline = Time.realtimeSinceStartup + 30;
            while (videoLoading && Time.realtimeSinceStartup < deadline) yield return null;
            check(VideoCameraLocked && videoBga?.IsLoaded == true, "Real video package loads with camera lock");
            SetPlaying(false); ApplyLockedVideoCamera(); UpdateViewportRect();
            string snapshot = JsonUtility.ToJson(chart);
            Vector3 position = sceneCamera.transform.position;
            Quaternion rotation = sceneCamera.transform.rotation;
            float fov = sceneCamera.fieldOfView;
            Vector3 probe = new Vector3(3, 1, 16);
            Vector3 projected = sceneCamera.WorldToViewportPoint(probe);
            Matrix4x4 projection = sceneCamera.projectionMatrix;
            int originalPixelWidth = sceneCamera.pixelWidth, originalPixelHeight = sceneCamera.pixelHeight;
            Action<string> checkView = name =>
            {
                Vector3 actualProjection = sceneCamera.WorldToViewportPoint(probe);
                float matrixDelta = 0;
                for (int i = 0; i < 16; i++) matrixDelta = Mathf.Max(matrixDelta, Mathf.Abs(projection[i] - sceneCamera.projectionMatrix[i]));
                // Viewport rasterization rounds to whole pixels at each UI size.
                // Require an identical perspective matrix and < half a pixel drift.
                float pixelWidth = Mathf.Min(originalPixelWidth, sceneCamera.pixelWidth);
                float pixelHeight = Mathf.Min(originalPixelHeight, sceneCamera.pixelHeight);
                bool stable = Vector3.Distance(position, sceneCamera.transform.position) < .0001f &&
                    Quaternion.Angle(rotation, sceneCamera.transform.rotation) < .001f &&
                    Mathf.Abs(fov - sceneCamera.fieldOfView) < .0001f &&
                    matrixDelta < .000001f && Mathf.Abs(projected.x - actualProjection.x) * pixelWidth < .5f &&
                    Mathf.Abs(projected.y - actualProjection.y) * pixelHeight < .5f && Mathf.Abs(projected.z - actualProjection.z) < .0001f;
                check(stable, name);
                if (!stable) report.Add($"DETAIL position={sceneCamera.transform.position:F6} rotation={sceneCamera.transform.rotation:F6} fov={sceneCamera.fieldOfView:F6} matrixDelta={matrixDelta:F8} projected={actualProjection:F6} baseline={projected:F6}");
            };

            SetFlightMode(true); CaptureMouse(); ReadNavigationMode(); ReadFlight(); ReadSceneNavigation(); FocusSelection();
            check(!flyMode && Cursor.lockState == CursorLockMode.None, "Flight, mouse capture and focus are disabled");
            orbitYaw += 45; orbitPitch += 20; orbitPivot += Vector3.one * 50; orbitDistance = 80; ApplyOrbitCamera();
            checkView("Orbit and pan state cannot change the video projection");
            foreach (var editMode in new[] { ChartEditMode.Bga, ChartEditMode.Stage, ChartEditMode.Paths, ChartEditMode.Notes })
            {
                SetMode(editMode);
                foreach (double time in new[] { 0.0, 4.0, 11.0, 19.0 })
                {
                    Seek(Math.Min(time, Duration)); yield return null;
                    FocusSelection(); checkView(editMode + " keeps camera fixed at " + time + "s");
                }
            }
            chart.cameraKeys[0].fov = 85;
            chart.cameraKeys[0].useWorldPose = true;
            chart.cameraKeys[0].worldPosition = new Vector3(200, 80, -100);
            chart.cameraKeys[0].worldTarget = new Vector3(150, 0, 300);
            chart.stagePath.points[1].x += 2;
            Rebuild(); checkView("Stage edits and old camera keys cannot reframe video charts");
            chart.videoBga.enabled = false; ReadNavigationMode(); SetFlightMode(true);
            check(VideoCameraLocked && !flyMode, "Hiding BGA keeps camera locked");
            chart.videoBga.enabled = true;

            chart = JsonUtility.FromJson<ChartData>(snapshot); Rebuild();
            SetMode(ChartEditMode.Paths); Seek(1);
            GizmoPose(out var origin, out var frame);
            BeginGizmoDrag(3, RayTo(origin), PixelGui(origin));
            Vector3 moved = origin + frame * new Vector3(1, .5f, 0);
            UpdateGizmoDrag(RayTo(moved), PixelGui(moved)); draggingHandle = false;
            GizmoPose(out var actual, out _);
            check(Vector3.Distance(actual, moved) < .01f, "Path XY handles remain editable with locked camera");
            checkView("Dragging a path changes geometry only");
            int count = chart.notes.Length;
            SetMode(ChartEditMode.Notes); AddNote();
            check(chart.notes.Length == count + 1 && selectedNote >= 0, "Note placement remains available");
            checkView("Adding a note keeps the camera fixed");

            float originalWidth = panelWidth, originalScale = uiScale;
            foreach (float scale in new[] { 1f, 1.5f })
            {
                uiScale = scale; panelWidth = ClampInspectorPanelWidth(originalWidth + 40); UpdateViewportRect();
                checkView("Panel resize / UI scale " + scale + " preserves image-relative projection");
                check(Mathf.Abs(sceneCamera.aspect - VideoFrameAspect) < .0001f, "Viewport retains video aspect at scale " + scale);
            }
            uiScale = originalScale; panelWidth = originalWidth; UpdateViewportRect();
            Seek(2); SetPreviewMode(true); checkView("F5 enters the same camera view");
            yield return new WaitForSecondsRealtime(.5f);
            checkView("Playback does not animate the camera");
            SetPlaying(false); Seek(12); yield return null; checkView("Preview seeking keeps the camera fixed");
            SetPreviewMode(false); checkView("Leaving preview restores the same projection");
            SetMode(ChartEditMode.Paths); Seek(4);
            yield return WaitForVideoPosition(4);
            check(videoBga?.Texture != null && Math.Abs(videoBga.Time - 4) < .15, "Video still decodes and seeks under the locked view");
            report.Add("RESULT=" + (videoChecksPassed ? "PASS" : "FAIL") + " CHECKS=" + report.Count);
            File.WriteAllLines(Path.Combine(smokeDirectory, "locked-video-camera-checks.txt"), report);
            Debug.Log("LOCKED_VIDEO_CAMERA_" + (videoChecksPassed ? "PASS" : "FAIL") + "\n" + string.Join("\n", report));
            chart = JsonUtility.FromJson<ChartData>(snapshot); undo.Clear(); redo.Clear(); selectedNote = -1;
            Rebuild(); SetStatus("Video view locked · Ready to chart");
            yield return CaptureCheckScreenshot("locked-video-paths.png");
        }

        IEnumerator RunVideoBgaChecks(string source)
        {
            videoChecksPassed = true;
            var report = new List<string>();
            Action<bool, string> check = (ok, name) => { videoChecksPassed &= ok; report.Add((ok ? "PASS " : "FAIL ") + name); };
            string pathsBefore = JsonUtility.ToJson(chart.stagePath);
            int noteCount = chart.notes.Length, pathCount = chart.paths.Length;
            yield return ImportVideoBga(source);
            check(videoBga != null && videoBga.IsLoaded, "One-click import prepares a decoded video");
            check(chart.notes.Length == noteCount && chart.paths.Length == pathCount && JsonUtility.ToJson(chart.stagePath) == pathsBefore,
                "Import preserves gameplay notes, paths and stage");
            if (videoBga == null || !videoBga.IsLoaded)
            {
                report.Add(videoStatus); File.WriteAllLines(Path.Combine(smokeDirectory, "video-bga-checks.txt"), report); yield break;
            }
            float deadline = Time.realtimeSinceStartup + 15;
            while (!realAudioLoaded && Time.realtimeSinceStartup < deadline) yield return null;
            check(realAudioLoaded, "External song is decoded (not demo audio)");
            SetPlaying(false); SetMode(ChartEditMode.Bga);
            double target = Math.Min(4, videoBga.Package.durationSeconds * .5);
            Seek(.25); yield return WaitForVideoPosition(.25);
            ulong first = VideoTextureFingerprint();
            yield return CaptureCheckScreenshot("video-start.png");
            Seek(target); yield return WaitForVideoPosition(target);
            ulong second = VideoTextureFingerprint();
            report.Add($"DETAIL seek target={target:0.000} time={videoBga.Time:0.000} frame={videoBga.Frame} pixels={first}/{second}");
            check(first != 0 && second != 0 && first != second, "Scrubbing changes decoded pixels");
            check(Math.Abs(videoBga.Time - target) < .12, "Paused seek reaches requested song time");
            yield return CaptureCheckScreenshot("video-seek.png");
            Seek(.4); Seek(1); Seek(.7); yield return WaitForVideoPosition(.7);
            report.Add($"DETAIL rapid seek time={videoBga.Time:0.000} frame={videoBga.Frame}");
            check(Math.Abs(videoBga.Time - .7) < .12, "Rapid backward / forward seeks keep newest target");
            SetPlaying(true); yield return new WaitForSecondsRealtime(1.5f);
            check(Math.Abs(videoBga.Time - songTime) < .25, "Video follows song clock during playback");
            check(realAudioLoaded && Math.Abs(audioSource.time - chart.audioOffsetSeconds - songTime) < .1, "Music is the timing authority");
            SetPlaying(false);
            if (videoBga.Package.durationSeconds > 44)
            {
                var scenePixels = new HashSet<ulong>();
                bool scenesSeek = true;
                foreach (double time in new double[] { .3, 6.3, 12.3, 18.3, 24.3, 30.3, 36.3, 42.3 })
                {
                    Seek(time); yield return WaitForVideoPosition(time);
                    scenesSeek &= Math.Abs(videoBga.Time - time) < .12;
                    scenePixels.Add(VideoTextureFingerprint());
                }
                check(scenesSeek && scenePixels.Count == 8, "All eight BGA scenes decode and seek to different images");
                Seek(23.5); SetPlaying(true); yield return new WaitForSecondsRealtime(2);
                report.Add($"DETAIL cross cut video={videoBga.Time:0.000} song={songTime:0.000} audio={audioSource.time:0.000} seeking={videoBga.IsSeeking} playing={playing} seeks={videoBga.SeekCount}/{videoBga.CompletedSeekCount}");
                check(Math.Abs(videoBga.Time - songTime) < .25, "Music/video remain aligned across a scene cut");
                SetPlaying(false);
                Seek(Duration - .4); SetPlaying(true); yield return new WaitForSecondsRealtime(.8f);
                report.Add($"DETAIL end video={videoBga.Time:0.000} song={songTime:0.000} duration={Duration:0.000} audio={audioSource.time:0.000} audioPlaying={audioSource.isPlaying} playing={playing}");
                check(!playing && Math.Abs(songTime - Duration) < .05, "Playback stops at the chart end");
                yield return WaitForVideoPosition(videoBga.Package.durationSeconds - 1 / videoBga.Package.fps);
                check(videoBga.Frame >= 1348, "BGA holds its final decoded frame");
                yield return CaptureCheckScreenshot("video-end.png");
            }
            chart.videoBga.timeOffsetSeconds = 1;
            Seek(2); yield return WaitForVideoPosition(1);
            check(Math.Abs(videoBga.Time - 1) < .12, "Video time offset is independent from song clock");
            chart.videoBga.timeOffsetSeconds = 0;
            chart.videoBga.enabled = false; yield return null;
            check(realAudioLoaded && audioSource.clip != null, "Hiding video preserves the song");
            chart.videoBga.enabled = true; yield return null;
            deadline = Time.realtimeSinceStartup + 20;
            while ((videoLoading || !realAudioLoaded) && Time.realtimeSinceStartup < deadline) yield return null;
            check(videoBga?.IsLoaded == true, "Showing video restores decoded BGA");
            string saved = Path.Combine(smokeDirectory, "portable-original", "test-chart.json");
            check(TrySaveChart(saved), "Save writes chart and bundled assets");
            var savedData = JsonUtility.FromJson<ChartData>(File.ReadAllText(saved));
            check(!Path.IsPathRooted(savedData.videoBga.packageManifest) && !Path.IsPathRooted(savedData.audioFile), "Saved media references are relative");
            string relocated = Path.Combine(smokeDirectory, "portable-relocated");
            Directory.CreateDirectory(relocated);
            string sourceDirectory = Path.GetDirectoryName(saved);
            foreach (string path in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string to = Path.Combine(relocated, path.Substring(sourceDirectory.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(to)); File.Copy(path, to, true);
            }
            check(TryLoadChart(Path.Combine(relocated, "test-chart.json")), "Relocated chart opens without original source path");
            deadline = Time.realtimeSinceStartup + 20;
            while ((videoLoading || !realAudioLoaded) && Time.realtimeSinceStartup < deadline) yield return null;
            check(videoBga?.IsLoaded == true && realAudioLoaded, "Relocated media decodes successfully");
            string originalManifest = chart.videoBga.packageManifest;
            yield return ImportVideoBga(Path.Combine(smokeDirectory, "missing-video.mp4"));
            check(chart.videoBga.packageManifest == originalManifest && videoBga.IsLoaded, "Failed import retains current BGA");
            SetMode(ChartEditMode.Paths); Seek(1); yield return WaitForVideoPosition(1);
            yield return CaptureCheckScreenshot("video-with-3d-paths.png");
            check(chart.paths.Length == pathCount, "3D path editing remains available");
            File.WriteAllLines(Path.Combine(smokeDirectory, "video-bga-checks.txt"), report);
            Debug.Log("VIDEO_BGA_CHECKS_" + (videoChecksPassed ? "PASS" : "FAIL") + "\n" + string.Join("\n", report));
        }

        IEnumerator WaitForVideoPosition(double target)
        {
            float deadline = Time.realtimeSinceStartup + 8;
            do { yield return null; }
            while (videoBga != null && (videoBga.IsSeeking || Math.Abs(videoBga.Time - target) > .1) && Time.realtimeSinceStartup < deadline);
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
        }

        ulong VideoTextureFingerprint()
        {
            if (videoBga?.Texture == null) return 0;
            var target = RenderTexture.GetTemporary(64, 36, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var pixels = new Texture2D(64, 36, TextureFormat.RGB24, false);
            try
            {
                Graphics.Blit(videoBga.Texture, target); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 64, 36), 0, 0); pixels.Apply();
                ulong hash = 1469598103934665603;
                foreach (byte b in pixels.GetRawTextureData<byte>()) { hash ^= b; hash *= 1099511628211; }
                return hash;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Destroy(pixels); }
        }
    }
}
