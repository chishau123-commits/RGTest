using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        ChartData CameraTweenInput()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-chartEditorCameraTweenInput");
            return index < 0 || index + 1 >= args.Length ? null : JsonUtility.FromJson<ChartData>(File.ReadAllText(args[index + 1]));
        }
        static CameraKey TweenWorldKey(float beat, Vector3 position, Vector3 direction, float roll = 0)
            => new CameraKey { beat = beat, useWorldPose = true, worldPosition = position,
                worldTarget = position + direction.normalized * 20, roll = roll, fov = 53 };
        static float TweenRotationStep(Quaternion from, Quaternion to)
        {
            // Quaternion.Angle rounds tiny per-sample rotations to zero. Measure the
            // relative quaternion's vector part for meaningful high-rate diagnostics.
            Quaternion delta = Quaternion.Inverse(from) * to;
            double sine = Math.Sqrt((double)delta.x * delta.x + (double)delta.y * delta.y + (double)delta.z * delta.z);
            return (float)(2 * Math.Atan2(sine, Math.Abs(delta.w)) * 180 / Math.PI);
        }

        bool RunCameraTweenChecks()
        {
            string snapshot = JsonUtility.ToJson(chart), savedBaseline = savedChartJson;
            bool savedNeedsSaveAs = needsSaveAs;
            var report = new StringBuilder(); int checks = 0; bool passed = false;
            Action<bool, string> check = (value, name) =>
            {
                if (!value) throw new InvalidOperationException(name);
                checks++; report.AppendLine("PASS " + name);
            };
            try
            {
                NewChart(); SetMode(ChartEditMode.Camera); ReleaseMouse(); flyMode = false;
                showGuide = showSettings = showFilesMenu = false;
                GizmoPose(out var dragStart, out _);
                Vector3 grab = dragStart + Vector3.right * GizmoLength(dragStart) * .6f;
                BeginGizmoDrag(0, RayTo(grab), PixelGui(grab));
                check(spatial.UsesFixedCameraKeys, "Converting a key on gizmo press refreshes interpolation before any mouse movement");
                ApplyGizmoPosition(dragStart + Vector3.right * 2);
                spatial.EvaluateCamera(evaluatorCamera, 0);
                check(Vector3.Distance(evaluatorCamera.transform.position, dragStart + Vector3.right * 2) < .001f,
                    "Gizmo movement rebuilds fixed endpoint positions");
                draggingHandle = false; Undo();
                check(!spatial.UsesFixedCameraKeys, "Undo restores the original camera interpolation policy");
                var fixture = JsonUtility.FromJson<ChartData>(JsonUtility.ToJson(chart));
                fixture.tempos = new[] { new TempoData { tick = 0, bpm = 120 }, new TempoData { tick = 8 * 480, bpm = 180 } };
                fixture.cameraKeys = new[]
                {
                    new CameraKey { beat = 0, orbit = 15, height = 8, distance = 28, fov = 53 },
                    TweenWorldKey(16, new Vector3(12, 30, 100), new Vector3(-.01745f, -.99985f, -.00015f)),
                    new CameraKey { beat = 32, orbit = -20, height = 11, distance = 32, fov = 65 },
                    new CameraKey { beat = 48, usePathPose = true, positionForward = -12, positionX = 8, positionY = 12,
                        targetForward = 18, targetY = 2, fov = 57 },
                    TweenWorldKey(64, new Vector3(-10, 20, 180), Vector3.forward)
                };
                var fixtureTempo = new TempoMap(fixture.tempos, fixture.ticksPerBeat);
                var director = new SpatialDirector(fixture, fixtureTempo);
                check(director.UsesFixedCameraKeys, "One world key selects a consistent fixed-endpoint policy for the complete track");
                var positions = new Vector3[fixture.cameraKeys.Length];
                var rotations = new Quaternion[fixture.cameraKeys.Length];
                for (int i = 0; i < fixture.cameraKeys.Length; i++)
                {
                    var key = fixture.cameraKeys[i];
                    // Resolve legacy and route-local keys independently at their own
                    // times, never by interpolating against their mixed-type neighbor.
                    var single = JsonUtility.FromJson<ChartData>(JsonUtility.ToJson(fixture));
                    single.cameraKeys = new[] { key };
                    var endpoint = new SpatialDirector(single, fixtureTempo);
                    endpoint.EvaluateCamera(evaluatorCamera, fixtureTempo.SecondsAtBeat(key.beat));
                    positions[i] = evaluatorCamera.transform.position;
                    rotations[i] = key.useWorldPose
                        ? Quaternion.LookRotation(key.worldTarget - key.worldPosition, Vector3.up)
                        : evaluatorCamera.transform.rotation;
                    director.EvaluateCamera(evaluatorCamera, fixtureTempo.SecondsAtBeat(key.beat));
                    check(Vector3.Distance(evaluatorCamera.transform.position, positions[i]) < .001f &&
                        Quaternion.Angle(evaluatorCamera.transform.rotation, rotations[i]) < .1f,
                        "Mixed track reaches the independent fixed pose at key " + i);
                }
                string[] easingIds = { SpatialDirector.CameraEaseLinear, SpatialDirector.CameraEaseIn,
                    SpatialDirector.CameraEaseOut, SpatialDirector.CameraEaseInOut, SpatialDirector.CameraEaseSmoother };
                float[] easingQuarter = { .25f, .0625f, .4375f, .15625f, .103515625f };
                for (int easing = 0; easing < easingIds.Length; easing++)
                {
                    fixture.cameraKeys[0].easing = easingIds[easing];
                    director = new SpatialDirector(fixture, fixtureTempo);
                    float beat = Mathf.Lerp(fixture.cameraKeys[0].beat, fixture.cameraKeys[1].beat, .25f);
                    director.EvaluateCamera(evaluatorCamera, fixtureTempo.SecondsAtBeat(beat));
                    check(Vector3.Distance(evaluatorCamera.transform.position,
                        Vector3.Lerp(positions[0], positions[1], easingQuarter[easing])) < .003f,
                        "Camera segment evaluates " + easingIds[easing] + " easing");
                    var roundTrip = JsonUtility.FromJson<ChartData>(JsonUtility.ToJson(fixture));
                    check(roundTrip.cameraKeys[0].easing == easingIds[easing],
                        "Camera segment persists " + easingIds[easing] + " easing");
                }
                fixture.cameraKeys[0].easing = null;
                director = new SpatialDirector(fixture, fixtureTempo);
                check(SpatialDirector.CameraEasing(fixture.cameraKeys[0]) == SpatialDirector.CameraEaseInOut,
                    "Missing easing in an old chart retains the original smooth interpolation");
                for (int i = 0; i + 1 < positions.Length; i++)
                {
                    foreach (float fraction in new[] { .01f, .25f, .5f, .75f, .99f })
                    {
                        float beat = Mathf.Lerp(fixture.cameraKeys[i].beat, fixture.cameraKeys[i + 1].beat, fraction);
                        float eased = fraction * fraction * (3 - 2 * fraction);
                        director.EvaluateCamera(evaluatorCamera, fixtureTempo.SecondsAtBeat(beat));
                        check(Vector3.Distance(evaluatorCamera.transform.position, Vector3.Lerp(positions[i], positions[i + 1], eased)) < .003f,
                            "Fixed endpoint position / mixed pair " + i + " fraction " + fraction);
                        check(Quaternion.Angle(evaluatorCamera.transform.rotation, Quaternion.Slerp(rotations[i], rotations[i + 1], eased)) < .1f,
                            "Quaternion orientation / mixed pair " + i + " fraction " + fraction);
                        check(Math.Abs(evaluatorCamera.fieldOfView - Mathf.Lerp(fixture.cameraKeys[i].fov, fixture.cameraKeys[i + 1].fov, eased)) < .001f,
                            "FOV easing remains intact / pair " + i + " fraction " + fraction);
                    }
                }
                for (int i = 1; i < positions.Length; i++)
                {
                    double boundary = fixtureTempo.SecondsAtBeat(fixture.cameraKeys[i].beat);
                    director.EvaluateCamera(evaluatorCamera, boundary - .0001);
                    Vector3 before = evaluatorCamera.transform.position; Quaternion beforeRotation = evaluatorCamera.transform.rotation;
                    director.EvaluateCamera(evaluatorCamera, boundary + .0001);
                    check(Vector3.Distance(evaluatorCamera.transform.position, before) < .005f &&
                        Quaternion.Angle(evaluatorCamera.transform.rotation, beforeRotation) < .1f,
                        "No mixed-mode position/rotation jump at boundary " + i);
                }
                director.EvaluateCamera(evaluatorCamera, fixtureTempo.SecondsAtBeat(100));
                check(Vector3.Distance(evaluatorCamera.transform.position, positions[positions.Length - 1]) < .001f,
                    "After the final fixed key, the camera holds its endpoint");
                director.EvaluateCamera(evaluatorCamera, -1);
                check(Vector3.Distance(evaluatorCamera.transform.position, positions[0]) < .001f, "Before the first key, the endpoint is clamped");

                foreach (Vector3 destination in new[] { Vector3.up, Vector3.down, Vector3.back, new Vector3(0, -1, .00001f) })
                {
                    fixture.cameraKeys = new[] { TweenWorldKey(0, Vector3.zero, Vector3.forward),
                        TweenWorldKey(16, Vector3.right * 12, destination) };
                    director = new SpatialDirector(fixture, fixtureTempo);
                    director.EvaluateCamera(evaluatorCamera, 0); Quaternion last = evaluatorCamera.transform.rotation;
                    float largestStep = 0; bool finite = true;
                    for (int i = 1; i <= 240; i++)
                    {
                        director.EvaluateCamera(evaluatorCamera, fixtureTempo.SecondsAtBeat(16.0 * i / 240));
                        Quaternion now = evaluatorCamera.transform.rotation;
                        finite &= !float.IsNaN(now.x + now.y + now.z + now.w);
                        largestStep = Mathf.Max(largestStep, TweenRotationStep(last, now)); last = now;
                    }
                    check(finite && largestStep < 2, "Vertical/opposite look directions have continuous finite rotations: " + destination);
                    check(Vector3.Distance(evaluatorCamera.transform.forward, destination.normalized) < .001f,
                        "Vertical/opposite final direction is exact: " + destination);
                }
                fixture.cameraKeys = new[] { TweenWorldKey(0, Vector3.zero, Vector3.forward, 0),
                    TweenWorldKey(16, Vector3.right * 12, Vector3.forward, 360) };
                director = new SpatialDirector(fixture, fixtureTempo);
                director.EvaluateCamera(evaluatorCamera, fixtureTempo.SecondsAtBeat(8));
                check(Quaternion.Angle(evaluatorCamera.transform.rotation, Quaternion.Euler(0, 0, 180)) < .1f,
                    "Explicit authored roll retains a full turn instead of being collapsed by quaternion shortest-path");
                var legacy = JsonUtility.FromJson<ChartData>(JsonUtility.ToJson(fixture));
                legacy.cameraKeys = new[] { new CameraKey { beat = 0 } };
                director = new SpatialDirector(legacy, fixtureTempo); director.EvaluateCamera(evaluatorCamera, 0);
                Vector3 legacyStart = evaluatorCamera.transform.position; director.EvaluateCamera(evaluatorCamera, 10);
                check(!director.UsesFixedCameraKeys && Vector3.Distance(legacyStart, evaluatorCamera.transform.position) > 1,
                    "Pure legacy charts preserve their original moving-route camera");

                var input = CameraTweenInput();
                if (input != null)
                {
                    string inputBefore = JsonUtility.ToJson(input);
                    var inputTempo = new TempoMap(input.tempos, input.ticksPerBeat);
                    var inputDirector = new SpatialDirector(input, inputTempo);
                    check(input.cameraKeys.Length >= 2 && !input.cameraKeys[0].useWorldPose && input.cameraKeys[1].useWorldPose,
                        "Actual draft reproduces the legacy K0 / world K1 case");
                    var onlyK0 = JsonUtility.FromJson<ChartData>(inputBefore); onlyK0.cameraKeys = new[] { onlyK0.cameraKeys[0] };
                    var movingK0 = new SpatialDirector(onlyK0, inputTempo);
                    double startTime = inputTempo.SecondsAtBeat(input.cameraKeys[0].beat), endTime = inputTempo.SecondsAtBeat(input.cameraKeys[1].beat);
                    double midTime = inputTempo.SecondsAtBeat((input.cameraKeys[0].beat + input.cameraKeys[1].beat) * .5);
                    movingK0.EvaluateCamera(evaluatorCamera, startTime); Vector3 fixedStart = evaluatorCamera.transform.position;
                    movingK0.EvaluateCamera(evaluatorCamera, midTime); Vector3 formerlyDriftingStart = evaluatorCamera.transform.position;
                    Vector3 fixedEnd = input.cameraKeys[1].worldPosition, expectedMid = (fixedStart + fixedEnd) * .5f;
                    inputDirector.EvaluateCamera(evaluatorCamera, midTime);
                    float midpointError = Vector3.Distance(evaluatorCamera.transform.position, expectedMid);
                    check(midpointError < .001f, "Actual K0-K1 midpoint uses the two fixed endpoint positions");
                    report.AppendLine("ACTUAL midpoint error=" + midpointError.ToString("F6") +
                        " previous drift=" + (Vector3.Distance(formerlyDriftingStart, fixedStart) * .5f).ToString("F4"));
                    inputDirector.EvaluateCamera(evaluatorCamera, startTime); Quaternion last = evaluatorCamera.transform.rotation;
                    float maxStep = 0;
                    for (int i = 1; i <= 3000; i++)
                    {
                        inputDirector.EvaluateCamera(evaluatorCamera, startTime + (endTime - startTime) * i / 3000);
                        maxStep = Mathf.Max(maxStep, TweenRotationStep(last, evaluatorCamera.transform.rotation)); last = evaluatorCamera.transform.rotation;
                    }
                    check(maxStep > .001f && maxStep < .25f, "Actual steep K0-K1 segment rotates continuously without abrupt steps over 3000 samples");
                    report.AppendLine("ACTUAL maximum angular sample step=" + maxStep.ToString("F6") + " degrees");
                    Quaternion expectedEnd = Quaternion.LookRotation(input.cameraKeys[1].worldTarget - fixedEnd, Vector3.up)
                        * Quaternion.Euler(0, 0, input.cameraKeys[1].roll);
                    check(Vector3.Distance(evaluatorCamera.transform.position, fixedEnd) < .001f &&
                        Quaternion.Angle(evaluatorCamera.transform.rotation, expectedEnd) < .1f,
                        "Actual K1 retains its precise position and near-vertical authored direction");
                    inputDirector.EvaluateCamera(evaluatorCamera, midTime); Vector3 directPosition = evaluatorCamera.transform.position;
                    Quaternion directRotation = evaluatorCamera.transform.rotation;
                    inputDirector.EvaluateCamera(evaluatorCamera, endTime); inputDirector.EvaluateCamera(evaluatorCamera, startTime);
                    inputDirector.EvaluateCamera(evaluatorCamera, midTime);
                    check(Vector3.Distance(evaluatorCamera.transform.position, directPosition) < .0001f &&
                        Quaternion.Angle(evaluatorCamera.transform.rotation, directRotation) < .1f,
                        "Actual interpolation is deterministic when seeking backwards or skipping frames");
                    check(JsonUtility.ToJson(input) == inputBefore, "Actual draft data is never mutated by endpoint resolution or playback");

                    chart = input; Rebuild(); SetMode(ChartEditMode.Camera); Seek(midTime);
                    var pathLine = visualRoot.Find("Camera path").GetComponent<LineRenderer>();
                    bool lineMatches = true;
                    for (int i = 0; i < pathLine.positionCount; i++)
                    {
                        double t = inputTempo.SecondsAtBeat(input.cameraKeys[input.cameraKeys.Length - 1].beat * i / (pathLine.positionCount - 1.0));
                        inputDirector.EvaluateCamera(evaluatorCamera, t);
                        lineMatches &= Vector3.Distance(pathLine.GetPosition(i), evaluatorCamera.transform.position) < .003f;
                    }
                    check(lineMatches, "Actual editor path line uses the same fixed-endpoint evaluation as Preview");
                    Vector3 editorPosition = sceneCamera.transform.position, marker = PlacementMarkerPosition;
                    Quaternion editorRotation = sceneCamera.transform.rotation;
                    SetPlaying(true); songTime += .5; RefreshEditorPlayheadVisuals(); SetPlaying(false);
                    check(Vector3.Distance(sceneCamera.transform.position, editorPosition) < .001f &&
                        Quaternion.Angle(sceneCamera.transform.rotation, editorRotation) < .1f && Vector3.Distance(PlacementMarkerPosition, marker) < .001f,
                        "The tween fix does not restore editor or yellow-marker following");
                    SetPreviewMode(true); SetPlaying(false); inputDirector.EvaluateCamera(evaluatorCamera, songTime);
                    check(Vector3.Distance(sceneCamera.transform.position, evaluatorCamera.transform.position) < .001f &&
                        Quaternion.Angle(sceneCamera.transform.rotation, evaluatorCamera.transform.rotation) < .1f,
                        "Actual Preview uses the corrected shared camera tween");
                    SetPreviewMode(false);
                }
                passed = true;
            }
            catch (Exception error) { report.AppendLine("FAIL " + error); Debug.LogException(error); }
            finally
            {
                if (chartCameraPreview) SetPreviewMode(false);
                SetPlaying(false); CancelTimelineGesture(); ReleaseMouse();
                chart = JsonUtility.FromJson<ChartData>(snapshot); savedChartJson = savedBaseline; needsSaveAs = savedNeedsSaveAs;
                songTime = 0; selectedCameraKey = 0; selectedNote = -1; undo.Clear(); redo.Clear(); Rebuild(); SetupAudio();
            }
            report.AppendLine("RESULT=" + (passed ? "PASS" : "FAIL") + " CHECKS=" + checks);
            File.WriteAllText(Path.Combine(smokeDirectory, "camera-tween-checks.txt"), report.ToString());
            Debug.Log("CHART_STUDIO_CAMERA_TWEEN " + (passed ? "PASS" : "FAIL") + " " + checks); return passed;
        }
    }
}
