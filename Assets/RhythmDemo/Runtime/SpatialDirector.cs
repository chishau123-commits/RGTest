using System;
using UnityEngine;

namespace GeometryRhythm
{
    /// <summary>All world poses are pure functions of chart time, including camera keyframes.
    /// This is shared by gameplay, seek/preview and the future chart editor.</summary>
    public sealed class SpatialDirector
    {
        public const float NearDepth = 7;
        public const float FarDepth = 100;
        public const string CameraEaseLinear = "linear";
        public const string CameraEaseIn = "easeIn";
        public const string CameraEaseOut = "easeOut";
        public const string CameraEaseInOut = "easeInOut";
        public const string CameraEaseSmoother = "smoother";
        readonly ChartData chart;
        readonly TempoMap tempo;
        readonly StageSpline route;
        public readonly VideoChartSpace VideoSpace;
        public float NoteSpeedMultiplier { get; private set; }
        public float NoteSpawnPercent { get; private set; } = NoteSpawnSettings.Default;
        // A chart without a video space owns a fixed world route, so its approach window can
        // only be read as a flight time. The shared reading speed scales that time; the
        // recommended speed returns the authored window exactly, and the clamp keeps the
        // extreme ends of the preference from flooding or emptying the screen.
        const float MinimumApproachSeconds = .35f, MaximumApproachSeconds = 12;
        public float ApproachSeconds => VideoSpace != null ? chart.approachSeconds
            : Mathf.Clamp(chart.approachSeconds * NoteScrollSettings.Default / NoteSpeedMultiplier,
                MinimumApproachSeconds, MaximumApproachSeconds);
        /// <summary>Personal reading speed. It never touches ticks, BPM, offsets or geometry,
        /// only how much of the path is in flight.</summary>
        public void SetNoteSpeed(float speed)
        {
            float sanitized = NoteScrollSettings.Sanitize(speed);
            if (Mathf.Abs(sanitized - NoteSpeedMultiplier) < .0001f) return;
            NoteSpeedMultiplier = sanitized;
        }
        /// <summary>Shorten the visible path without changing flight speed or hit timing.</summary>
        public void SetNoteSpawnPosition(float percent) => NoteSpawnPercent = NoteSpawnSettings.Sanitize(percent);
        // Notes enter at the drawn endpoint. Geometry still samples the full authored
        // path, so moving the endpoint cannot move a note already in flight.
        public float NoteSpawnDepth => NearDepth + (FarDepth - NearDepth) * NoteSpawnPercent / 100;
        public float VisiblePathFarDepth => NoteSpawnDepth;
        public double NoteLookaheadSeconds(double time)
        {
            if (VideoSpace == null) return ApproachSeconds * NoteSpawnPercent / 100;
            float targetZ = VideoSpace.CameraZ(time) + (NoteSpawnDepth - NearDepth) / NoteSpeedMultiplier;
            return Math.Max(0, VideoSpace.TimeAtCameraZ(targetZ) - time);
        }
        readonly Vector3[] fixedCameraPositions;
        readonly Quaternion[] fixedCameraRotations;
        public bool UsesFixedCameraKeys => fixedCameraPositions != null;
        public static bool IsCameraEasing(string value)
            => string.IsNullOrEmpty(value) || value == CameraEaseLinear || value == CameraEaseIn ||
                value == CameraEaseOut || value == CameraEaseInOut || value == CameraEaseSmoother;
        public static string CameraEasing(CameraKey key)
            => key == null || string.IsNullOrEmpty(key.easing) ? CameraEaseInOut : key.easing;
        public static float CameraInterpolation(CameraKey key, float value)
        {
            float t = Mathf.Clamp01(value);
            switch (CameraEasing(key))
            {
                case CameraEaseLinear: return t;
                case CameraEaseIn: return t * t;
                case CameraEaseOut: return 1 - (1 - t) * (1 - t);
                case CameraEaseSmoother: return t * t * t * (t * (t * 6 - 15) + 10);
                default: return t * t * (3 - 2 * t);
            }
        }
        public SpatialDirector(ChartData chart, TempoMap tempo, float videoNoteSpeed = 1)
        {
            this.chart = chart; this.tempo = tempo; route = new StageSpline(chart.stagePath);
            // Both chart families share one reading speed. A no-video chart scales its
            // authored approach window (see ApproachSeconds); a video chart scales the
            // distance still in flight. At the recommended speed both are unchanged.
            NoteSpeedMultiplier = NoteScrollSettings.Sanitize(videoNoteSpeed);
            if (VideoChartSpace.Enabled(chart))
            {
                VideoSpace = new VideoChartSpace(chart, tempo);
                return;
            }
            // A track authored with world-space keys has fixed endpoints throughout.
            // Resolve legacy/path keys at THEIR own times too; switching interpolation
            // modes per pair would otherwise introduce jumps at mixed-type boundaries.
            // Pure legacy/path tracks keep their original moving-route semantics.
            if (chart.cameraKeys == null || !Array.Exists(chart.cameraKeys, key => key.useWorldPose)) return;
            fixedCameraPositions = new Vector3[chart.cameraKeys.Length];
            fixedCameraRotations = new Quaternion[chart.cameraKeys.Length];
            for (int i = 0; i < chart.cameraKeys.Length; i++)
            {
                var key = chart.cameraKeys[i];
                float distance = DistanceAtTime(tempo.SecondsAtBeat(key.beat));
                WorldPose(key, distance, out var position, out var target);
                Vector3 direction = target - position;
                if (direction.sqrMagnitude < .000001f) direction = RouteRotationAt(distance) * Vector3.forward;
                direction.Normalize();
                // Pick an endpoint frame only once. Near-vertical (e.g. 89 degrees)
                // still has a valid world-up frame; use a fallback only at the pole.
                Vector3 up = Vector3.Cross(Vector3.up, direction).sqrMagnitude < .00000001f
                    ? Vector3.forward : Vector3.up;
                fixedCameraPositions[i] = position;
                fixedCameraRotations[i] = Quaternion.LookRotation(direction, up);
            }
        }

        public static Vector3 Route(float distance)
            => StageSpline.LegacyPosition(distance);
        public static Quaternion RouteRotation(float distance)
            => StageSpline.LegacyRotation(distance);
        public float UnitsPerSecond => route.UnitsPerSecond;
        public float RouteLength => route.Length;
        public float RouteControlPointDistance(int index) => route.ControlPointDistance(index);
        public float DistanceAtTime(double time) => VideoSpace != null ? VideoSpace.CameraZ(time) : (float)time * route.UnitsPerSecond;
        public Vector3 RouteAt(float distance) => VideoSpace != null ? VideoSpace.RailAtDistance(distance) : route.Position(distance);
        public Quaternion RouteRotationAt(float distance) => VideoSpace != null ? VideoSpace.RailRotationAtDistance(distance) : route.Rotation(distance);
        public Vector3 RoutePoint(float distance, float x, float y) => route.OffsetPoint(distance, x, y);
        /// <summary>Centre of the judgement plane the player reads at this time. In
        /// video space that is one NearDepth step along the camera's own forward
        /// axis, so an offset or turned camera carries the plane with it.</summary>
        public Vector3 JudgementCenter(double time)
        {
            if (VideoSpace != null)
            {
                var pose = VideoSpace.Pose(time);
                return pose.position + pose.rotation * new Vector3(0, 0, NearDepth);
            }
            return RouteAt(DistanceAtTime(time) + NearDepth);
        }
        /// <summary>Up axis of the screen at this time. Video notes and judgement
        /// rings roll with the authored camera; legacy charts keep the stage frame.</summary>
        public Vector3 CameraUp(double time)
            => VideoSpace != null ? VideoSpace.Pose(time).rotation * Vector3.up
                : RouteRotationAt(DistanceAtTime(time) + NearDepth) * Vector3.up;
        public int SectionIndex(double time)
        {
            double beat = tempo.BeatAtSeconds(time);
            int index = 0;
            while (index + 1 < chart.sections.Length && chart.sections[index + 1].startBeat <= beat) index++;
            return index;
        }
        public SectionData Section(double time) => chart.sections[SectionIndex(time)];
        static PathPlacement Find(SectionData section, string id)
        {
            foreach (var p in section.placements) if (p.pathId == id) return p;
            return null;
        }
        public float Visibility(string id, double time)
        {
            float value = 0;
            for (int i = 0; i < chart.sections.Length; i++)
            {
                if (Find(chart.sections[i], id) == null) continue;
                double start = tempo.SecondsAtBeat(chart.sections[i].startBeat);
                double end = i + 1 < chart.sections.Length ? tempo.SecondsAtBeat(chart.sections[i + 1].startBeat)
                    : tempo.SecondsAtBeat(chart.endBeat);
                float fadeIn = VideoSpace == null ? Mathf.Clamp01((float)(time - start + NoteLookaheadSeconds(time)) / .8f)
                    : Mathf.Clamp01((NoteSpawnDepth - Depth(start, time)) / 4);
                float fadeOut = Mathf.Clamp01((float)(end + .5 - time) / .7f);
                value = Mathf.Max(value, fadeIn * fadeOut);
            }
            return value;
        }
        PathPlacement Placement(string id, double time, out PathPlacement previous, out float mix)
        {
            int index = SectionIndex(time);
            var current = Find(chart.sections[index], id);
            previous = index > 0 ? Find(chart.sections[index - 1], id) : current;
            // Previews may contain notes for a path that starts in the next section.
            if (current == null)
            {
                if (index + 1 < chart.sections.Length) current = Find(chart.sections[index + 1], id);
                current = current ?? previous;
            }
            if (current == null) current = new PathPlacement { pathId = id };
            previous = previous ?? current;
            mix = Mathf.SmoothStep(0, 1, (float)(time - tempo.SecondsAtBeat(chart.sections[index].startBeat)) / 1.25f);
            return current;
        }
        public Vector3 Point(string id, float depth, double time)
        {
            float s = DistanceAtTime(time) + depth;
            var path = Array.Find(chart.paths, p => p.id == id);
            if (VideoSpace != null) return VideoSpace.PathAtDistance(path, s);
            if (path != null && path.offsetKeys != null && path.offsetKeys.Length > 0)
            {
                Vector2 offset = OffsetAtDistance(id, s);
                return RoutePoint(s, offset.x, offset.y);
            }
            var current = Placement(id, time, out var previous, out float blend);
            float q = Mathf.Clamp01((depth - NearDepth) / (FarDepth - NearDepth));
            float x = Mathf.Lerp(previous.x, current.x, blend);
            float y = Mathf.Lerp(previous.y, current.y, blend);
            float bend = Mathf.Lerp(previous.bend, current.bend, blend);
            float lift = Mathf.Lerp(previous.lift, current.lift, blend);
            Vector3 local = new Vector3(x * (1 - q * .6f) + Mathf.Sin(q * Mathf.PI * 1.35f) * bend,
                y * (1 - q * .4f) + Mathf.Sin(q * Mathf.PI) * lift, 0);
            return RouteAt(s) + RouteRotationAt(s) * local;
        }
        public Vector2 OffsetAtDistance(string id, float distance)
        {
            var path = Array.Find(chart.paths, p => p.id == id);
            var keys = path == null ? null : path.offsetKeys;
            double time = Math.Max(0, (distance - NearDepth) / UnitsPerSecond);
            if (keys == null || keys.Length == 0)
            {
                var current = Placement(id, time, out var previous, out float blend);
                return Vector2.Lerp(new Vector2(previous.x, previous.y), new Vector2(current.x, current.y), blend);
            }
            int index = 0;
            while (index + 1 < keys.Length && OffsetDistance(keys[index + 1].tick) <= distance) index++;
            var a = keys[index]; var b = keys[Math.Min(index + 1, keys.Length - 1)];
            float mix = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(OffsetDistance(a.tick), OffsetDistance(b.tick), distance));
            return Vector2.Lerp(new Vector2(a.x, a.y), new Vector2(b.x, b.y), mix);
        }
        public float OffsetDistance(int tick)
            => DistanceAtTime(tempo.SecondsAtBeat(tick / (double)chart.ticksPerBeat)) + NearDepth;
        public float Depth(double hitTime, double time)
        {
            // 1x reproduces the original fixed-world note exactly. A personal
            // read-speed multiplier stretches only the distance BEFORE/AFTER its
            // hit plane. Hit time, anchor XY/Z, path geometry and camera are unchanged.
            // Sample the existing path at this depth, so accelerated notes stay on it.
            if (VideoSpace != null) return NearDepth + (VideoSpace.CameraZ(hitTime) - VideoSpace.CameraZ(time)) * NoteSpeedMultiplier;
            float q = (float)((hitTime - time) / ApproachSeconds);
            // World-space progress, not accumulating deltaTime. Seek and low FPS stay in sync.
            return NearDepth + q * (FarDepth - NearDepth);
        }
        public bool NoteInView(double hitTime, double time, double pastWindow = 0)
        {
            double delta = hitTime - time;
            if (delta < -pastWindow - 1e-7) return false;
            return Depth(hitTime, time) <= NoteSpawnDepth + .0001f;
        }
        public void NotePose(RuntimeNote note, double time, out Vector3 position, out Quaternion rotation)
        {
            PathPose(note.Data.pathId, Depth(note.HitTime,time), time, out position, out rotation);
        }
        /// <summary>The touch target stays on the timing markers throughout the judgement
        /// window, independently of reading speed and the flying note's depth.</summary>
        public void JudgementPose(string pathId, double time, out Vector3 position, out Quaternion rotation)
            => PathPose(pathId, NearDepth, time, out position, out rotation);

        void PathPose(string pathId, float depth, double time, out Vector3 position, out Quaternion rotation)
        {
            position = Point(pathId, depth, time);
            Vector3 tangent = (Point(pathId, depth + .12f, time) - Point(pathId, depth - .12f, time)).normalized;
            // A real world-space plane; never billboard to the camera. Gentle tilt exposes the rim.
            // Video space takes its up axis from the authored rig, so a rolled
            // camera rolls its notes with the screen instead of the world.
            Quaternion upFrame = VideoSpace != null ? VideoSpace.Pose(time).rotation
                : RouteRotationAt(DistanceAtTime(time) + depth);
            rotation = Quaternion.LookRotation(-tangent, upFrame * Vector3.up)
                * Quaternion.Euler(13, 0, 0);
            foreach (var path in chart.paths)
                if(path.id==pathId) { rotation*=Quaternion.Euler(0,0,path.roll);break; }
        }
        public void EvaluateCamera(Camera camera, double time)
        {
            if (VideoSpace != null) { VideoSpace.ApplyCamera(camera, time); return; }
            float beat = (float)tempo.BeatAtSeconds(time);
            int index = 0;
            while (index + 1 < chart.cameraKeys.Length && chart.cameraKeys[index + 1].beat <= beat) index++;
            var a = chart.cameraKeys[index];
            var b = chart.cameraKeys[Math.Min(index + 1, chart.cameraKeys.Length - 1)];
            float q = CameraInterpolation(a, Mathf.InverseLerp(a.beat, b.beat, beat));
            if (UsesFixedCameraKeys)
            {
                int next = Math.Min(index + 1, fixedCameraPositions.Length - 1);
                camera.transform.SetPositionAndRotation(
                    Vector3.Lerp(fixedCameraPositions[index], fixedCameraPositions[next], q),
                    Quaternion.Slerp(fixedCameraRotations[index], fixedCameraRotations[next], q)
                        * Quaternion.Euler(0, 0, Mathf.Lerp(a.roll, b.roll, q)));
                camera.fieldOfView = Mathf.Lerp(a.fov, b.fov, q);
                return;
            }
            float orbit = Mathf.Lerp(a.orbit, b.orbit, q);
            float distance = Mathf.Lerp(a.distance, b.distance, q);
            float height = Mathf.Lerp(a.height, b.height, q);
            float s = DistanceAtTime(time);
            Vector3 target;
            if (a.usePathPose || b.usePathPose)
            {
                PoseValues(a, out var apf, out var apx, out var apy, out var atf, out var atx, out var aty);
                PoseValues(b, out var bpf, out var bpx, out var bpy, out var btf, out var btx, out var bty);
                camera.transform.position = RoutePoint(s + Mathf.Lerp(apf, bpf, q), Mathf.Lerp(apx, bpx, q), Mathf.Lerp(apy, bpy, q));
                target = RoutePoint(s + Mathf.Lerp(atf, btf, q), Mathf.Lerp(atx, btx, q), Mathf.Lerp(aty, bty, q));
            }
            else
            {
                Quaternion frame = RouteRotationAt(s + 15);
                target = RouteAt(s + 18) + Vector3.up * 2;
                Vector3 offset = Quaternion.Euler(0, orbit, 0) * new Vector3(0, height, -distance);
                camera.transform.position = target + frame * offset;
            }
            Vector3 direction = target - camera.transform.position;
            if (direction.sqrMagnitude < .000001f) direction = RouteRotationAt(s) * Vector3.forward;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > .999f ? Vector3.forward : Vector3.up;
            camera.transform.rotation = Quaternion.LookRotation(direction, up)
                * Quaternion.Euler(0, 0, Mathf.Lerp(a.roll, b.roll, q));
            camera.fieldOfView = Mathf.Lerp(a.fov, b.fov, q);
        }
        void WorldPose(CameraKey key, float distance, out Vector3 position, out Vector3 target)
        {
            if (key.useWorldPose) { position = key.worldPosition; target = key.worldTarget; return; }
            if (key.usePathPose)
            {
                position = RoutePoint(distance + key.positionForward, key.positionX, key.positionY);
                target = RoutePoint(distance + key.targetForward, key.targetX, key.targetY);
                return;
            }
            target = RouteAt(distance + 18) + Vector3.up * 2;
            position = target + RouteRotationAt(distance + 15) *
                (Quaternion.Euler(0, key.orbit, 0) * new Vector3(0, key.height, -key.distance));
        }
        static void PoseValues(CameraKey key, out float pf, out float px, out float py,
            out float tf, out float tx, out float ty)
        {
            if (key.usePathPose)
            {
                pf = key.positionForward; px = key.positionX; py = key.positionY;
                tf = key.targetForward; tx = key.targetX; ty = key.targetY;
                return;
            }
            float radians = key.orbit * Mathf.Deg2Rad;
            pf = 18 - Mathf.Cos(radians) * key.distance;
            px = Mathf.Sin(radians) * key.distance;
            py = 2 + key.height;
            tf = 18; tx = 0; ty = 2;
        }
    }
}
