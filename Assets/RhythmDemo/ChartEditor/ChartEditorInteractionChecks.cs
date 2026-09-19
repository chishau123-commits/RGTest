using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        // Opt-in player regression suite. Exercises the same drag/capture/undo methods
        // as input, including rebuilding (destroying) the visual handles mid-drag.
        bool RunInteractionChecks()
        {
            string snapshot = JsonUtility.ToJson(chart);
            var report = new StringBuilder(); int checks = 0;
            Action<bool, string> check = (condition, name) =>
            {
                if (!condition) throw new InvalidOperationException(name);
                report.AppendLine("PASS " + name); checks++;
            };
            bool passed = false;
            try
            {
                ReleaseMouse(); flyMode = false; chartCameraPreview = false; showSettings = showGuide = false;
                NewChart(); UpdateViewportRect();
                for (int axis = 0; axis < 3; axis++)
                {
                    SetMode(ChartEditMode.Stage); selectedStagePoint = 2; FocusSelection();
                    GizmoPose(out var start, out _);
                    Vector3 grab = start + Axis(axis) * GizmoLength(start) * .6f;
                    check(HitGizmo(WorldGui(grab)) == axis, "Visible axis matches hit-test " + axis);
                    BeginGizmoDrag(axis, RayTo(grab), PixelGui(grab));
                    int undoCount = undo.Count;
                    UpdateGizmoDrag(RayTo(grab + Axis(axis) * 2), PixelGui(grab + Axis(axis) * 2));
                    UpdateGizmoDrag(RayTo(grab + Axis(axis) * 4), PixelGui(grab + Axis(axis) * 4));
                    GizmoPose(out var end, out _);
                    check(Vector3.Distance(end, start + Axis(axis) * 4) < .02f, "Stage axis " + axis + " survives two rebuilds");
                    check(undo.Count == undoCount, "One undo snapshot for entire drag " + axis);
                    draggingHandle = false; Undo(); GizmoPose(out var restored, out _);
                    check(Vector3.Distance(restored, start) < .001f, "Stage undo " + axis);
                    Redo(); GizmoPose(out restored, out _);
                    check(Vector3.Distance(restored, end) < .001f, "Stage redo " + axis);
                    Undo();
                }
                Vector3 viewPosition = sceneCamera.transform.position;
                Quaternion viewRotation = sceneCamera.transform.rotation;
                SetFlightMode(true); ReleaseMouse();
                check(Vector3.Distance(viewPosition, sceneCamera.transform.position) < .001f, "Caps ON preserves camera position");
                check(Vector3.Distance(FlightMovement(90, 0, 1, 0), Vector3.right) < .001f, "Flight WASD follows yaw");
                check(FlightVerticalInput(true, false) == 1 && FlightMovement(50, 0, 0, 1) == Vector3.up,
                    "Flight Space uses world up");
                check(FlightVerticalInput(false, true) == -1 && FlightMovement(50, 0, 0, -1) == Vector3.down,
                    "Flight Shift uses world down");
                check(FlightSpeedMultiplier(false) == 1 && FlightSpeedMultiplier(true) == 3,
                    "Flight Ctrl enables acceleration");
                check(FlightMovement(0, 1, 1, 1).magnitude <= 1.001f, "Diagonal flight speed is normalized");
                SetFlightMode(false); ApplyOrbitCamera();
                check(Vector3.Distance(viewPosition, sceneCamera.transform.position) < .001f && Quaternion.Angle(viewRotation, sceneCamera.transform.rotation) < .01f,
                    "Caps OFF preserves position and rotation");

                SetMode(ChartEditMode.Camera); Seek(tempo.SecondsAtBeat(32));
                orbitPivot = spatial.RoutePoint(spatial.DistanceAtTime(songTime), 3, 8); ApplyOrbitCamera();
                Vector3 yellow = PlacementMarkerPosition; AddCameraKey();
                var cameraKey = chart.cameraKeys[selectedCameraKey];
                spatial.EvaluateCamera(evaluatorCamera, songTime);
                check(Vector3.Distance(cameraKey.worldPosition, yellow) < .0001f, "Add Key uses yellow ball, not viewport camera");
                check(Vector3.Distance(evaluatorCamera.transform.position, yellow) < .0001f, "Curved-route camera playback matches marker exactly");
                check(Vector3.Distance(yellow, sceneCamera.transform.position) > 1, "Marker and viewport are distinct");
                int cameraCount = chart.cameraKeys.Length; AddCameraKey();
                check(chart.cameraKeys.Length == cameraCount, "Same-tick camera key replaces without duplication");
                SetFlightMode(true); ReleaseMouse(); yellow = PlacementMarkerPosition; CaptureSelectedCamera();
                spatial.EvaluateCamera(evaluatorCamera, songTime);
                check(Vector3.Distance(yellow, evaluatorCamera.transform.position) < .0001f, "Flying capture uses the displayed placement marker");
                SetFlightMode(false);

                SetMode(ChartEditMode.Paths); selectedPath = 0; Seek(tempo.SecondsAtBeat(32)); FocusSelection();
                GizmoPose(out var pathStart, out var frame);
                BeginGizmoDrag(3, RayTo(pathStart), PixelGui(pathStart));
                Vector3 movement = frame * new Vector3(3, 4, 0);
                UpdateGizmoDrag(RayTo(pathStart + movement), PixelGui(pathStart + movement));
                UpdateGizmoDrag(RayTo(pathStart + movement * 2), PixelGui(pathStart + movement * 2));
                GizmoPose(out var pathEnd, out _); draggingHandle = false;
                check(Vector3.Distance(pathEnd, pathStart + movement * 2) < .02f, "XY plane drag survives rebuild and stays perpendicular to route");
                check(Mathf.Abs(Vector3.Dot(pathEnd - pathStart, frame * Vector3.forward)) < .01f, "Offset drag has no route-forward component");
                var keys = chart.paths[0].offsetKeys;
                var key32 = Array.Find(keys, k => k.tick == 32 * chart.ticksPerBeat);
                check(key32 != null && keys.Length == 3, "Editing middle beat seeds boundaries plus independent key");
                check(Mathf.Abs(keys[0].x + 4) < .001f && Mathf.Abs(keys[keys.Length - 1].x + 4) < .001f,
                    "Editing offset does not translate other keys");
                float savedX = key32.x, savedY = key32.y;
                Seek(tempo.SecondsAtBeat(64)); var key64 = EnsureOffsetKey(PlayheadTick); key64.x = -6; key64.y = -2;
                Rebuild();
                check(key32.x == savedX && key32.y == savedY, "Later offset edits preserve earlier key");
                float da = spatial.OffsetDistance(32 * chart.ticksPerBeat), db = spatial.OffsetDistance(64 * chart.ticksPerBeat);
                Vector2 midpoint = spatial.OffsetAtDistance("p0", (da + db) * .5f);
                check(Vector2.Distance(midpoint, new Vector2((savedX - 6) * .5f, (savedY - 2) * .5f)) < .001f, "Offsets smoothly interpolate by route distance");
                Seek(tempo.SecondsAtBeat(32));
                check(Vector3.Distance(spatial.Point("p0", SpatialDirector.NearDepth, songTime), pathEnd) < .02f,
                    "Gameplay note path and editor offset handle coincide");
                AddNote(); string json = JsonUtility.ToJson(chart); var roundTrip = ChartLoader.Parse(json);
                var restoredSpatial = new SpatialDirector(roundTrip, new TempoMap(roundTrip.tempos, roundTrip.ticksPerBeat));
                check(Vector3.Distance(restoredSpatial.Point("p0", SpatialDirector.NearDepth, songTime), pathEnd) < .02f,
                    "Save/load preserves placement track and gameplay geometry");
                restoredSpatial.EvaluateCamera(evaluatorCamera, songTime);
                check(Vector3.Distance(evaluatorCamera.transform.position, chart.cameraKeys[selectedCameraKey].worldPosition) < .001f,
                    "Save/load preserves exact camera key position");
                roundTrip.paths[0].offsetKeys[1].tick = roundTrip.paths[0].offsetKeys[0].tick;
                bool invalidRejected = false;
                try { ChartLoader.Parse(JsonUtility.ToJson(roundTrip)); } catch (FormatException) { invalidRejected = true; }
                check(invalidRejected, "Duplicate offset ticks rejected by loader");
                passed = true;
            }
            catch (Exception e) { report.AppendLine("FAIL " + e); Debug.LogException(e); }
            finally
            {
                chart = JsonUtility.FromJson<ChartData>(snapshot); undo.Clear(); redo.Clear(); draggingHandle = false;
                flyMode = false; ReleaseMouse(); songTime = 0; Rebuild();
            }
            report.AppendLine("RESULT=" + (passed ? "PASS" : "FAIL") + " CHECKS=" + checks);
            File.WriteAllText(Path.Combine(smokeDirectory, "interaction-checks.txt"), report.ToString());
            Debug.Log("CHART_STUDIO_INTERACTIONS " + (passed ? "PASS" : "FAIL") + " " + checks);
            return passed;
        }
        Ray RayTo(Vector3 world) => sceneCamera.ScreenPointToRay(sceneCamera.WorldToScreenPoint(world));
        Vector2 PixelGui(Vector3 world) => WorldGui(world) * uiScale;
        IEnumerator CaptureCheckScreenshot(string name)
        {
            yield return null; yield return new WaitForEndOfFrame();
            string path = Path.Combine(smokeDirectory, name); ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
        }
        bool ScreenshotHasContent(string path)
        {
            if (!File.Exists(path)) return false;
            var texture = new Texture2D(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) return false;
                int visible = 0;
                for (int y = 0; y < texture.height; y += 32)
                    for (int x = 0; x < texture.width; x += 32)
                    {
                        Color color = texture.GetPixel(x, y);
                        if (color.r + color.g + color.b > .3f) visible++;
                    }
                return visible > 20;
            }
            finally { Destroy(texture); }
        }
    }
}
