using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        bool noteSpeedChecksPassed;

        IEnumerator RunNoteSpeedChecks()
        {
            noteSpeedChecksPassed = true;
            var report = new List<string>();
            Action<bool, string> check = (ok, name) => { noteSpeedChecksPassed &= ok; report.Add((ok ? "PASS " : "FAIL ") + name); };
            float deadline = Time.realtimeSinceStartup + 35;
            while (videoLoading && Time.realtimeSinceStartup < deadline) yield return null;
            check(VideoSpaceEditing && videoBga?.IsLoaded == true, "H3 package opens in fixed-video workspace");
            if (!VideoSpaceEditing)
            { File.WriteAllLines(Path.Combine(smokeDirectory, "note-speed-checks.txt"), report); yield break; }
            SetPlaying(false);
            string original = ChartSnapshotForUndo(), originalFile = filePath;
            float originalSpeed = noteReadSpeed;
            try
            {
                check(NoteScrollSettings.Default == 8 && Math.Abs(NoteScrollSettings.Load() - noteReadSpeed) < .00001,
                    "Editor loads personal speed; upgraded default is 8x");
                check(spatial.NoteSpawnDepth == SpatialDirector.FarDepth && spatial.NoteSpawnDepth == spatial.VisiblePathFarDepth,
                    "Spawn is locked to the drawn path endpoint; there is no independent mid-track gate");
                check(NoteScrollSettings.Sanitize(float.NaN) == 8 && NoteScrollSettings.Sanitize(float.PositiveInfinity) == 8 &&
                    NoteScrollSettings.Sanitize(-1) == .5f && NoteScrollSettings.Sanitize(1000) == NoteScrollSettings.Maximum,
                    "Invalid and extreme speeds are safely bounded");
                check(NoteScrollSettings.ResolveSavedSpeed(3, 0) == 8 && NoteScrollSettings.ResolveSavedSpeed(6, 0) == 8 &&
                    NoteScrollSettings.ResolveSavedSpeed(float.NaN, 0) == 8 && NoteScrollSettings.ResolveSavedSpeed(12, 0) == 12,
                    "Existing slow preferences upgrade to 8x, not just fresh installations; faster choices are retained");
                check(NoteScrollSettings.ResolveSavedSpeed(1, NoteScrollSettings.ReadabilityVersion) == 1 &&
                    NoteScrollSettings.ResolveSavedSpeed(6, NoteScrollSettings.ReadabilityVersion) == 6 &&
                    NoteScrollSettings.ResolveSavedSpeed(12, NoteScrollSettings.ReadabilityVersion) == 12,
                    "After a new personal choice, slower and faster speeds remain respected on reload");
                chart.notes = new NoteData[0]; selectedNote = -1;
                SetMode(ChartEditMode.Notes); Seek(8); Rebuild();
                string unchanged = JsonUtility.ToJson(chart);
                int undoCount = undo.Count;
                double unchangedTime = songTime;
                Vector3 cameraBefore = sceneCamera.transform.position;
                Quaternion rotationBefore = sceneCamera.transform.rotation;
                Matrix4x4 projectionBefore = sceneCamera.projectionMatrix;
                SetNoteReadSpeed(3, false);
                check(unchanged == JsonUtility.ToJson(chart) && songTime == unchangedTime && undoCount == undo.Count,
                    "Changing read speed never mutates chart, ticks, BPM, media offsets, time or undo stack");
                check(sceneCamera.transform.position == cameraBefore && sceneCamera.transform.rotation == rotationBefore && sceneCamera.projectionMatrix == projectionBefore,
                    "Camera pose, Z and projection are unchanged at a fixed time");
                check(Math.Abs(spatial.NoteLookaheadSeconds(8) - 93.0 / 15) < .00001,
                    "Full depth-100 path at 3x provides 6.2 seconds of flight on H3");
                var slow = new SpatialDirector(chart, tempo, 1);
                var fast = new SpatialDirector(chart, tempo, 3);
                double hit = 10, now = 9.7;
                check(Math.Abs((fast.Depth(hit, now) - SpatialDirector.NearDepth) / (slow.Depth(hit, now) - SpatialDirector.NearDepth) - 3) < .0001,
                    "Camera-relative approach distance is tripled, not just delayed note spawning");
                float slowStep = slow.Depth(hit, now) - slow.Depth(hit, now + .05);
                float fastStep = fast.Depth(hit, now) - fast.Depth(hit, now + .05);
                check(Math.Abs(fastStep / slowStep - 3) < .0001, "Actual approaching velocity is tripled on the linear Z track");
                foreach (float multiplier in new[] { .5f, 1f, 2f, 3f, 4f, 6f, 8f, 10f, 12f, 16f })
                {
                    var view = new SpatialDirector(chart, tempo, multiplier);
                    bool hitsMatch = true, pathsMatch = true;
                    foreach (var path in chart.paths)
                    {
                        var note = new RuntimeNote { Data = new NoteData { pathId = path.id, tick = 30 * 480, action = "tap" }, HitTime = hit };
                        slow.NotePose(note, hit, out var baseHit, out var baseRotation);
                        view.NotePose(note, hit, out var speedHit, out var speedRotation);
                        hitsMatch &= Vector3.Distance(baseHit, speedHit) < .00001f && Quaternion.Angle(baseRotation, speedRotation) < .001f;
                        view.NotePose(note, now, out var travelling, out _);
                        var expected = view.VideoSpace.PathAtDistance(path, view.VideoSpace.CameraZ(now) + view.Depth(hit, now));
                        pathsMatch &= Vector3.Distance(expected, travelling) < .00001f;
                        pathsMatch &= Vector3.Distance(view.Point(path.id, 14, now), slow.Point(path.id, 14, now)) < .00001f;
                    }
                    check(hitsMatch && pathsMatch, multiplier + "x: notes stay on original paths and hit every path at the same position/orientation");
                    double visibilityHit = 18, entryTime = visibilityHit - (view.NoteSpawnDepth - SpatialDirector.NearDepth) / (5 * multiplier);
                    bool entryCorrect = entryTime >= 0 ? view.NoteInView(visibilityHit, entryTime) && !view.NoteInView(visibilityHit, entryTime - .001)
                        : view.NoteInView(visibilityHit, 0);
                    check(entryCorrect && view.NoteInView(hit, hit) && !view.NoteInView(hit, hit + .001) && view.NoteInView(hit, hit + .05, JudgementEngine.GoodWindow),
                        multiplier + "x: distance-gated visibility and runtime late-judgement allowance are correct");
                }
                check(fast.NoteInView(12, 8) && fast.NoteInView(14, 8) && !fast.NoteInView(14.3, 8),
                    "Notes beyond the old depth-60 gate remain visible through the complete far path");
                foreach (var path in chart.paths)
                {
                    const double entry = 5.8;
                    var note = new RuntimeNote { Data = new NoteData { pathId = path.id }, HitTime = 12 };
                    fast.NotePose(note, entry, out var firstPosition, out _);
                    check(Vector3.Distance(firstPosition, fast.Point(path.id, fast.VisiblePathFarDepth, entry)) < .0001f &&
                        fast.NoteInView(12, entry) && !fast.NoteInView(12, entry - .001),
                        path.id + ": first visible note center coincides with far endpoint, with nothing visible before entry");
                }
                check(Math.Abs(fast.NoteLookaheadSeconds(19) - 1) < .00001 && fast.NoteLookaheadSeconds(20) == 0,
                    "Lookahead is bounded by chart end without inventing later camera motion");
                var legacy = JsonUtility.FromJson<ChartData>(unchanged); legacy.videoSpace = null;
                // A chart without a video space always carries a real window; the H3 source used
                // here is a video chart whose approachSeconds field is not authored.
                legacy.approachSeconds = 3.4f;
                var legacyAuthored = new SpatialDirector(legacy, tempo, NoteScrollSettings.Default);
                var legacyFaster = new SpatialDirector(legacy, tempo, NoteScrollSettings.Default * 2);
                // A chart without a video space owns a fixed world route, so its shared reading
                // speed scales the authored approach window instead of a camera-relative
                // distance. The recommended speed must be bit-identical, and a doubled speed
                // doubles the approach velocity without moving the hit anchor.
                double authoredDepth = SpatialDirector.NearDepth +
                    (hit - now) / legacy.approachSeconds * (SpatialDirector.FarDepth - SpatialDirector.NearDepth);
                float authoredStep = legacyAuthored.Depth(hit, now) - legacyAuthored.Depth(hit, now + .05);
                float fasterStep = legacyFaster.Depth(hit, now) - legacyFaster.Depth(hit, now + .05);
                check(Math.Abs(legacyAuthored.Depth(hit, now) - authoredDepth) < .001f &&
                    Math.Abs(legacyAuthored.ApproachSeconds - legacy.approachSeconds) < .0001f &&
                    Math.Abs(fasterStep / authoredStep - 2) < .0001f &&
                    Vector3.Distance(legacyAuthored.Point(legacy.paths[0].id, SpatialDirector.NearDepth, hit),
                        legacyFaster.Point(legacy.paths[0].id, SpatialDirector.NearDepth, hit)) < .00001f,
                    "Legacy non-video motion keeps its authored window at the recommended speed and scales from there");
                var curved = JsonUtility.FromJson<ChartData>(unchanged);
                int end = Mathf.RoundToInt(curved.endBeat * curved.ticksPerBeat);
                curved.videoSpace.cameraZKeys = new[] { new CameraZKey { tick = 0, z = 0, easing = "smoother" },
                    new CameraZKey { tick = end / 2, z = 45, easing = "easeInOut" }, new CameraZKey { tick = end, z = 100 } };
                curved.paths[0].screenAnchors = new[] { new ScreenAnchorData { tick = 0, xPercent = 25, yPercent = 65 },
                    new ScreenAnchorData { tick = end / 2, xPercent = 70, yPercent = 45 }, new ScreenAnchorData { tick = end, xPercent = 30, yPercent = 80 } };
                var curvedFast = new SpatialDirector(curved, tempo, NoteScrollSettings.Default);
                bool curvedValid = true;
                foreach (double time in new[] { 1.0, 4.0, 9.0, 13.0, 18.0 })
                {
                    double hitTime = time + .3;
                    var note = new RuntimeNote { Data = new NoteData { pathId = curved.paths[0].id }, HitTime = hitTime };
                    curvedFast.NotePose(note, time, out var pos, out _);
                    var onPath = curvedFast.VideoSpace.PathAtDistance(curved.paths[0], pos.z);
                    curvedFast.NotePose(note, hitTime, out var target, out _);
                    var expectedTarget = curvedFast.Point(note.Data.pathId, SpatialDirector.NearDepth, hitTime);
                    curvedValid &= Vector3.Distance(pos, onPath) < .00001f && Vector3.Distance(target, expectedTarget) < .00001f;
                    curvedFast.NotePose(note, time, out var repeated, out _);
                    curvedValid &= repeated == pos;
                }
                check(curvedValid, "Accelerated curved paths / eased Z are deterministic under seek and keep exact hit anchors");
                bool inversionValid = true, easedSpawnValid = true;
                foreach (double time in new[] { .5, 2.0, 7.0, 9.0, 10.0, 11.0, 15.0, 18.0, 19.0 })
                    inversionValid &= Math.Abs(curvedFast.VideoSpace.TimeAtCameraZ(curvedFast.VideoSpace.CameraZ(time)) - time) < .003;
                foreach (double futureHit in new[] { 9.0, 12.0, 17.0, 19.0 })
                {
                    float startZ = curvedFast.VideoSpace.CameraZ(futureHit) - (curvedFast.VisiblePathFarDepth - SpatialDirector.NearDepth) / curvedFast.NoteSpeedMultiplier;
                    if (startZ <= 0) continue;
                    double entry = curvedFast.VideoSpace.TimeAtCameraZ(startZ);
                    easedSpawnValid &= Math.Abs(curvedFast.Depth(futureHit, entry) - curvedFast.VisiblePathFarDepth) < .001 &&
                        !curvedFast.NoteInView(futureHit, entry - .01) && curvedFast.NoteInView(futureHit, entry + .01);
                }
                check(inversionValid, "Z-to-time inversion respects all eased segments and their boundaries");
                check(easedSpawnValid, "Eased camera still spawns notes at the exact far distance, without time-window clipping");
                var tempoVariant = JsonUtility.FromJson<ChartData>(unchanged);
                tempoVariant.tempos = new[] { new TempoData { tick = 0, bpm = 120 }, new TempoData { tick = 12 * 480, bpm = 180 } };
                var tempoVariantClock = new TempoMap(tempoVariant.tempos, tempoVariant.ticksPerBeat);
                var tempoVariantSpace = new VideoChartSpace(tempoVariant, tempoVariantClock);
                check(Math.Abs(tempoVariantSpace.TimeAtCameraZ(50) - tempoVariantClock.SecondsAtBeat(tempoVariant.endBeat) * .5) < .00001,
                    "Inverse Z uses seconds across tempo changes, not a linear beat interpolation");
                int slowCount = 0, fastCount = 0;
                var notes = new List<NoteData>();
                for (int tick = 20 * chart.ticksPerBeat; tick < chart.endBeat * chart.ticksPerBeat; tick += chart.ticksPerBeat / 4)
                {
                    var note = new NoteData { id = "speed-test-" + tick, tick = tick, pathId = chart.paths[0].id, action = "tap" };
                    notes.Add(note); double noteTime = NoteHitTime(note);
                    if (slow.NoteInView(noteTime, 8)) slowCount++;
                    if (fast.NoteInView(noteTime, 8)) fastCount++;
                }
                check(slowCount == 144 && fastCount == 75, "Full-path visibility keeps correct density at 1x / 3x, bounded by chart end (" + slowCount + " -> " + fastCount + ")");
                chart.notes = notes.ToArray(); Rebuild(); selectedNote = -1; SetMode(ChartEditMode.Notes); Seek(8);
                SetNoteReadSpeed(1, false); timelineZoom = 4; timelineStart = 7; timelineTrackScroll = 0; inspectorScroll = Vector2.zero;
                yield return WaitForVideoPosition(8); statusUntil = 0;
                yield return CaptureCheckScreenshot("note-speed-1x.png");
                SetNoteReadSpeed(3, false); statusUntil = 0;
                yield return CaptureCheckScreenshot("note-speed-3x.png");
                int rendered = 0;
                foreach (Transform child in previewMotionRoot) if (child.name.StartsWith("speed-test-", StringComparison.Ordinal)) rendered++;
                check(rendered == fastCount, "Actual editor renderer shows every future note inside the complete path");
                string chartBeforeFasterSpeed = JsonUtility.ToJson(chart);
                int undoBeforeFasterSpeed = undo.Count;
                Vector3 cameraBeforeFasterSpeed = sceneCamera.transform.position;
                Matrix4x4 projectionBeforeFasterSpeed = sceneCamera.projectionMatrix;
                SetNoteReadSpeed(8, false); statusUntil = 0;
                check(Math.Abs(spatial.NoteLookaheadSeconds(8) - 2.325) < .00001 &&
                    Math.Abs(spatial.NoteLookaheadSeconds(8) / fast.NoteLookaheadSeconds(8) - 3.0 / 8) < .00001,
                    "8x shortens full-path flight from 6.2s to 2.325s, so spawning is later without moving the start point");
                int lessCrowded = 0;
                foreach (Transform child in previewMotionRoot) if (child.name.StartsWith("speed-test-", StringComparison.Ordinal)) lessCrowded++;
                check(lessCrowded == 28 && rendered == 75,
                    "Same dense chart/time: actual rendered count falls from 75 at 3x to 28 at 8x");
                var faster = new SpatialDirector(chart, tempo, 8);
                check(!faster.NoteInView(12, 6) && fast.NoteInView(12, 6) &&
                    Math.Abs((faster.Depth(12, 11) - faster.Depth(12, 11.05)) /
                        (fast.Depth(12, 11) - fast.Depth(12, 11.05)) - 8.0 / 3) < .0001,
                    "Later entry is paired with 8/3 times the previous flight velocity, not a middle-of-path cutoff");
                check(JsonUtility.ToJson(chart) == chartBeforeFasterSpeed && undo.Count == undoBeforeFasterSpeed && songTime == 8 &&
                    sceneCamera.transform.position == cameraBeforeFasterSpeed && sceneCamera.projectionMatrix == projectionBeforeFasterSpeed,
                    "Density reduction preserves every note, tick, camera key, anchor, media offset and undo entry");
                yield return CaptureCheckScreenshot("note-speed-8x.png");
                foreach (float rate in new[] { 3f, 8f, 16f })
                {
                    SetNoteReadSpeed(rate, false);
                    yield return RunFarEndFlightChecks((ok, name) => check(ok, rate + "x: " + name));
                }
                SetNoteReadSpeed(8, false);
                string chartBeforeSpeed = JsonUtility.ToJson(chart);
                ReadWorkspaceKeys(new Event { type = EventType.KeyDown, keyCode = KeyCode.RightBracket });
                check(noteReadSpeed == 8.25f && spatial.NoteSpeedMultiplier == 8.25f, "] shortcut raises speed above the old 6x limit by 0.25x");
                ReadWorkspaceKeys(new Event { type = EventType.KeyDown, keyCode = KeyCode.LeftBracket });
                check(noteReadSpeed == 8 && spatial.NoteSpeedMultiplier == 8, "[ shortcut lowers speed by 0.25x");
                SetPlaying(true);
                double dspBefore = transportDspStart, songBefore = transportSongStart;
                SetNoteReadSpeed(12, false);
                check(playing && transportDspStart == dspBefore && transportSongStart == songBefore,
                    "Speed can change during playback without restarting audio/video clock");
                yield return new WaitForSecondsRealtime(.3f); SetPlaying(false);
                check(songTime > 8.2 && JsonUtility.ToJson(chart) == chartBeforeSpeed, "Playback advances normally and speed remains presentation-only");
                SetNoteReadSpeed(8, false); Seek(8);
                // The same factory must be used after note retiming, undo, and reimport.
                selectedNote = 0;
                var first = new List<TimelineItem>(TimelineItems(new TimelineRow { kind = TimelineKind.Note, path = 0 }))[0];
                BeginTimelineDrag(first, first.time); MoveTimelineItem(first, ItemTick(first) + 10); CancelTimelineGesture();
                check(spatial.NoteSpeedMultiplier == 8 && spatial.NoteSpawnDepth == spatial.VisiblePathFarDepth, "Retime rebuild retains speed and far-end spawning");
                Undo(); check(spatial.NoteSpeedMultiplier == 8 && spatial.NoteSpawnDepth == spatial.VisiblePathFarDepth, "Undo rebuild retains speed and far-end spawning");
                string chartBeforePackage = JsonUtility.ToJson(chart);
                string packagePath = Path.Combine(smokeDirectory, "NoteSpeed-roundtrip.grchart");
                check(TryExportChartPackage(packagePath) && TryLoadChartOrPackage(packagePath), "Package still exports/reopens without new required assets");
                var expectedChart = JsonUtility.FromJson<ChartData>(chartBeforePackage);
                check(spatial.NoteSpeedMultiplier == 8 && spatial.NoteSpawnDepth == spatial.VisiblePathFarDepth && chart.notes.Length == expectedChart.notes.Length &&
                    Array.TrueForAll(chart.notes, n => Array.Exists(expectedChart.notes, e => e.id == n.id && e.tick == n.tick)),
                    "Reimport retains personal speed, far-end spawning and exact authored note ticks");
                check(!JsonUtility.ToJson(chart).Contains("noteReadSpeed") && !JsonUtility.ToJson(chart).Contains("NoteSpeedMultiplier") &&
                    !JsonUtility.ToJson(chart).Contains("noteSpawnDepth"), "Personal speed is not baked into chart and no new package fields/assets are needed");
            }
            finally
            {
                SetPlaying(false); CancelTimelineGesture(); noteReadSpeed = originalSpeed;
                chart = JsonUtility.FromJson<ChartData>(original); filePath = originalFile; selectedNote = -1; undo.Clear(); redo.Clear();
                savedChartJson = JsonUtility.ToJson(chart); needsSaveAs = true;
                Rebuild(); RefreshBgaBinding(); SetMode(ChartEditMode.Notes); Seek(0);
            }
            report.Add("RESULT=" + (noteSpeedChecksPassed ? "PASS" : "FAIL") + " CHECKS=" + report.Count);
            File.WriteAllLines(Path.Combine(smokeDirectory, "note-speed-checks.txt"), report);
            Debug.Log("NOTE_SPEED_" + (noteSpeedChecksPassed ? "PASS" : "FAIL") + "\n" + string.Join("\n", report));
        }

        IEnumerator RunFarEndFlightChecks(Action<bool, string> check)
        {
            var denseNotes = chart.notes;
            double velocity = 5 * noteReadSpeed;
            double entryTime = 12 - (spatial.VisiblePathFarDepth - SpatialDirector.NearDepth) / velocity;
            string screenshotPrefix = "note-flight-" + noteReadSpeed.ToString("0") + "x-";
            try
            {
                var flightNotes = new List<NoteData>();
                foreach (var path in chart.paths)
                    flightNotes.Add(new NoteData { id = "flight-" + path.id, pathId = path.id, tick = 36 * chart.ticksPerBeat, action = "tap" });
                chart.notes = flightNotes.ToArray(); Rebuild();
                Seek(entryTime - .001);
                bool hidden = true;
                foreach (var note in flightNotes) hidden &= previewMotionRoot.Find(note.id) == null;
                check(hidden, "Actual renderer hides notes strictly beyond the far endpoint");
                Seek(entryTime);
                bool endpoint = true, hitMatch = true;
                foreach (var note in flightNotes)
                {
                    var rendered = previewMotionRoot.Find(note.id);
                    var line = previewMotionRoot.Find("Note path " + note.pathId)?.GetComponent<LineRenderer>();
                    endpoint &= rendered != null && line != null &&
                        Vector3.Distance(rendered.position, line.GetPosition(line.positionCount - 1)) < .0001f;
                    if (rendered != null && line != null)
                        endpoint &= Vector2.Distance(sceneCamera.WorldToViewportPoint(rendered.position),
                            sceneCamera.WorldToViewportPoint(line.GetPosition(line.positionCount - 1))) < .00001f;
                }
                check(endpoint, "Rendered note centers match actual line endpoints in BOTH world and video-screen coordinates on all paths");
                yield return WaitForVideoPosition(entryTime); statusUntil = 0;
                yield return CaptureCheckScreenshot(screenshotPrefix + "01-far-end.png");
                double midway = (entryTime + 12) * .5;
                Seek(midway); yield return WaitForVideoPosition(midway);
                yield return CaptureCheckScreenshot(screenshotPrefix + "02-approach.png");
                Seek(12); yield return WaitForVideoPosition(12);
                foreach (var note in flightNotes)
                {
                    var rendered = previewMotionRoot.Find(note.id);
                    hitMatch &= rendered != null && Vector3.Distance(rendered.position,
                        spatial.Point(note.pathId, SpatialDirector.NearDepth, 12)) < .0001f;
                }
                check(hitMatch, "Full-length flight still arrives at the exact authored hit positions/times on all paths");
                yield return CaptureCheckScreenshot(screenshotPrefix + "03-hit.png");

                // Exercise real DSP-clock playback, not only static seeks: entry
                // must occur on the first frame crossing the far plane, and every
                // following visible frame must stay on its continuous trajectory.
                Seek(entryTime - .2); yield return WaitForVideoPosition(entryTime - .2); SetPlaying(true);
                double previousTime = songTime;
                bool appeared = false, continuous = true, firstAtFarEnd = true, reachesNear = false;
                int frames = 0; float previousDepth = spatial.VisiblePathFarDepth;
                float timeout = Time.realtimeSinceStartup + 15;
                while (songTime < 12.1 && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                    foreach (var note in flightNotes)
                    {
                        var rendered = previewMotionRoot.Find(note.id);
                        bool expected = spatial.NoteInView(12, songTime, .0001);
                        continuous &= (rendered != null) == expected;
                        if (rendered != null)
                        {
                            spatial.NotePose(new RuntimeNote { Data = note, HitTime = 12 }, songTime, out var position, out _);
                            continuous &= Vector3.Distance(rendered.position, position) < .0001f;
                        }
                    }
                    var first = previewMotionRoot.Find(flightNotes[0].id);
                    if (first != null)
                    {
                        float depth = first.position.z - sceneCamera.transform.position.z;
                        if (!appeared)
                        {
                            double frameTravel = velocity * Math.Max(0, songTime - previousTime);
                            firstAtFarEnd &= previousTime < entryTime && songTime >= entryTime &&
                                spatial.VisiblePathFarDepth - depth <= frameTravel + .001;
                            appeared = true;
                        }
                        continuous &= depth <= previousDepth + .001f;
                        previousDepth = depth; frames++;
                        reachesNear |= depth <= SpatialDirector.NearDepth + 1;
                    }
                    else if (appeared && previousTime <= 12 && songTime >= 12)
                    {
                        // At high speeds a frame may cross the whole last unit.
                        // Judge the final step using its actual elapsed time.
                        reachesNear |= previousDepth - SpatialDirector.NearDepth <= velocity * (songTime - previousTime) + .001;
                    }
                    previousTime = songTime;
                }
                SetPlaying(false);
                check(appeared && firstAtFarEnd, "Live playback first visible frame crosses the far endpoint, never the old middle cutoff");
                check(continuous && reachesNear && frames > 10 && songTime >= 12.1,
                    "Live playback remains visible and continuous from far end to judgement (" + frames + " sampled frames)");
            }
            finally
            {
                SetPlaying(false); chart.notes = denseNotes; Rebuild(); Seek(8);
            }
        }
    }
}
