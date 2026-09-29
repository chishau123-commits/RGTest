using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeometryRhythm
{
    [Serializable] public sealed class VideoChartSpaceData
    {
        // Zero is the unconfigured / omitted DTO sentinel for Unity JsonUtility.
        public int schemaVersion;
        public CameraZKey[] cameraZKeys;
        // V1 compatibility only. V2 paths are independent and have no scene line.
        public ScreenAnchorData[] sceneAnchors;
        // Optional free camera rig. Missing, or present with every offset and head
        // angle at zero, reproduces the original axis-aligned camera exactly, so
        // every existing chart keeps its precise framing without migration.
        public VideoCameraPoseKey[] cameraPoseKeys;
    }
    [Serializable] public sealed class CameraZKey
    {
        public int tick;
        public float z;
        public string easing = "linear";
    }
    // The camera rail: how far the song has travelled. It stays a separate track
    // from the camera rig so note scroll speed and camera motion never fight each
    // other. Non-increasing Z is already rejected, so the rail order is total.
    // Percent anchors are placed against the pose below, at the anchor's own tick.
    [Serializable] public sealed class VideoCameraPoseKey
    {
        public int tick;
        // Metres the camera is pushed off the rail it would otherwise follow:
        // +x right, +y up, +z further into the song. Zero is the omitted sentinel.
        public float dx, dy, dz;
        // Head angles in degrees, applied in pitch (X) / yaw (Y) / roll (Z) order.
        // A yawed or pitched key also steers where later anchors sit in the world,
        // which is what turns "the camera slides" into "the path bends".
        public float yaw, pitch, roll;
        // Vertical field of view. Zero (an omitted field) means the default 53.
        public float fov = VideoChartSpace.FieldOfView;
        // Outgoing interpolation from this key to the next.
        public string easing = "linear";
    }
    // Percent of the VIDEO rectangle, origin at top left. At this key's time its
    // world point lies on the judgement plane (not on the camera plane).
    [Serializable] public sealed class ScreenAnchorData
    {
        public int tick;
        public float xPercent = 50, yPercent = 70;
    }

    public sealed class VideoChartSpace
    {
        public const float Aspect = 16f / 9, FieldOfView = 53;
        // A resolved camera frame. The chart's own coordinates stay the original
        // straight rail; this is the rigid rig hung around it.
        public struct CameraPose
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fov;
        }
        readonly ChartData chart;
        readonly TempoMap tempo;
        // JsonUtility can materialize an omitted nested DTO as an all-zero object.
        public static bool Enabled(ChartData data) => data?.videoSpace != null &&
            (data.videoSpace.schemaVersion != 0 || (data.videoSpace.cameraZKeys?.Length ?? 0) > 0 || (data.videoSpace.sceneAnchors?.Length ?? 0) > 0);
        public VideoChartSpace(ChartData data, TempoMap clock) { chart = data; tempo = clock; }
        public double TimeAt(int tick) => tempo.SecondsAtBeat(tick / (double)chart.ticksPerBeat);
        public static float Ease(string easing, float t)
        {
            t = Mathf.Clamp01(t);
            switch (easing)
            {
                case "easeIn": return t * t;
                case "easeOut": return 1 - (1 - t) * (1 - t);
                case "easeInOut": return t * t * (3 - 2 * t);
                case "smoother": return t * t * t * (t * (t * 6 - 15) + 10);
                default: return t;
            }
        }
        public float CameraZ(double time)
        {
            var keys = chart.videoSpace.cameraZKeys;
            int i = 0;
            while (i + 1 < keys.Length && TimeAt(keys[i + 1].tick) <= time) i++;
            if (i == keys.Length - 1) return keys[i].z;
            var a = keys[i]; var b = keys[i + 1];
            float t = (float)((time - TimeAt(a.tick)) / (TimeAt(b.tick) - TimeAt(a.tick)));
            return Mathf.Lerp(a.z, b.z, Ease(a.easing, t));
        }
        public double TimeAtCameraZ(float z)
        {
            var keys = chart.videoSpace.cameraZKeys;
            if (z <= keys[0].z) return TimeAt(keys[0].tick);
            int i = 0;
            while (i + 1 < keys.Length && keys[i + 1].z <= z) i++;
            if (i == keys.Length - 1) return TimeAt(keys[i].tick);
            var a = keys[i]; var b = keys[i + 1];
            float target = Mathf.InverseLerp(a.z, b.z, z), low = 0, high = 1;
            // All supported easing functions are monotone. Invert in seconds,
            // including keys which straddle a tempo change; do not linearize Z.
            for (int iteration = 0; iteration < 24; iteration++)
            {
                float middle = (low + high) * .5f;
                if (Ease(a.easing, middle) < target) low = middle; else high = middle;
            }
            return TimeAt(a.tick) + (TimeAt(b.tick) - TimeAt(a.tick)) * ((low + high) * .5);
        }
        public bool HasCameraPose => chart.videoSpace.cameraPoseKeys != null && chart.videoSpace.cameraPoseKeys.Length > 0;
        // An omitted FOV reads back as zero through JsonUtility; treat out-of-range
        // values as "unset" rather than letting a zero FOV collapse the projection.
        public static float KeyFieldOfView(VideoCameraPoseKey key)
            => key == null || key.fov < 30 || key.fov > 85 ? FieldOfView : key.fov;
        // An omitted easing string reads back as null; treat it as the linear default
        // rather than letting the trigger lookups fall through to an unknown value.
        public static string PoseEasing(VideoCameraPoseKey key)
            => key == null || string.IsNullOrEmpty(key.easing) ? SpatialDirector.CameraEaseLinear : key.easing;
        /// <summary>The rig at this song time: rail position + authored offset, head
        /// rotation, and FOV. With no pose keys this is the original
        /// (0,0,CameraZ) / identity / 53 camera, bit for bit.</summary>
        public CameraPose Pose(double time)
        {
            var pose = new CameraPose
            {
                position = new Vector3(0, 0, CameraZ(time)),
                rotation = Quaternion.identity,
                fov = FieldOfView
            };
            var keys = chart.videoSpace.cameraPoseKeys;
            if (keys == null || keys.Length == 0) return pose;
            int i = 0;
            while (i + 1 < keys.Length && TimeAt(keys[i + 1].tick) <= time) i++;
            var a = keys[i];
            // Past the last key the rig holds its final values; validation forbids
            // duplicate ticks, so the span can only reach zero at that clamp.
            var b = i + 1 < keys.Length ? keys[i + 1] : a;
            double span = TimeAt(b.tick) - TimeAt(a.tick);
            float t = span <= 0 ? 0 : Mathf.Clamp01((float)((time - TimeAt(a.tick)) / span));
            float mix = Ease(a.easing, t);
            pose.position += Vector3.Lerp(new Vector3(a.dx, a.dy, a.dz), new Vector3(b.dx, b.dy, b.dz), mix);
            pose.rotation = Quaternion.Euler(Mathf.Lerp(a.pitch, b.pitch, mix),
                Mathf.Lerp(a.yaw, b.yaw, mix), Mathf.Lerp(a.roll, b.roll, mix));
            pose.fov = Mathf.Lerp(KeyFieldOfView(a), KeyFieldOfView(b), mix);
            return pose;
        }
        /// <summary>The rig at the moment the rail reaches this distance.</summary>
        public CameraPose PoseAtDistance(float distance) => Pose(TimeAtCameraZ(distance));
        // V1 only. A legacy scene line is a world-space curve indexed by Z.
        public Vector3 SceneAtZ(float z) => chart.videoSpace.schemaVersion >= 2 ? new Vector3(0, 0, z) : Interpolate(chart.videoSpace.sceneAnchors, z, false);
        /// <summary>Where the camera is when the rail reads this distance. Version 1
        /// charts keep their scene line so their framing is untouched.</summary>
        public Vector3 RailAtDistance(float distance) => chart.videoSpace.schemaVersion >= 2
            ? PoseAtDistance(distance).position : SceneAtZ(distance);
        public Quaternion RailRotationAtDistance(float distance) => chart.videoSpace.schemaVersion >= 2
            ? PoseAtDistance(distance).rotation : Quaternion.identity;
        public void ApplyCamera(Camera camera, double time)
        {
            var pose = Pose(time);
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            camera.orthographic = false; camera.fieldOfView = pose.fov;
            camera.aspect = Aspect;
            camera.projectionMatrix = Matrix4x4.Perspective(pose.fov, Aspect, camera.nearClipPlane, camera.farClipPlane);
        }
        /// <summary>Screen percentage to a point in front of the camera, in camera
        /// local axes. `depth` is distance along the camera's own forward axis.</summary>
        public static Vector3 LocalPoint(float xPercent, float yPercent, float fov, float depth = SpatialDirector.NearDepth)
        {
            float halfHeight = depth * Mathf.Tan(fov * Mathf.Deg2Rad * .5f);
            return new Vector3((xPercent / 100 - .5f) * 2 * halfHeight * Aspect,
                (.5f - yPercent / 100) * 2 * halfHeight, depth);
        }
        /// <summary>Legacy axis-aligned helper: camera at (0,0,cameraZ), looking +Z.</summary>
        public static Vector3 Unproject(float xPercent, float yPercent, float cameraZ, float depth = SpatialDirector.NearDepth)
            => new Vector3(0, 0, cameraZ) + LocalPoint(xPercent, yPercent, FieldOfView, depth);
        /// <summary>Screen percentage to world space through an authored camera rig.</summary>
        public static Vector3 Unproject(float xPercent, float yPercent, CameraPose pose, float depth = SpatialDirector.NearDepth)
            => pose.position + pose.rotation * LocalPoint(xPercent, yPercent, pose.fov, depth);
        /// <summary>Legacy axis-aligned helper: world point to screen percentage.</summary>
        public static Vector2 Project(Vector3 world, float cameraZ)
        {
            float halfHeight = (world.z - cameraZ) * Mathf.Tan(FieldOfView * Mathf.Deg2Rad * .5f);
            return new Vector2((world.x / (2 * halfHeight * Aspect) + .5f) * 100,
                (.5f - world.y / (2 * halfHeight)) * 100);
        }
        /// <summary>World point to screen percentage through an authored camera rig.</summary>
        public static Vector2 Project(Vector3 world, CameraPose pose)
        {
            Vector3 local = Quaternion.Inverse(pose.rotation) * (world - pose.position);
            float halfHeight = local.z * Mathf.Tan(pose.fov * Mathf.Deg2Rad * .5f);
            return new Vector2((local.x / (2 * halfHeight * Aspect) + .5f) * 100,
                (.5f - local.y / (2 * halfHeight)) * 100);
        }
        /// <summary>Where this anchor sits in the world. Resolved through the rig at
        /// the anchor's own tick, so its authored percentage is exact at that moment.</summary>
        public Vector3 AnchorWorld(ScreenAnchorData key)
            => Unproject(key.xPercent, key.yPercent, Pose(TimeAt(key.tick)));
        /// <summary>Rail distance this anchor occupies. Monotone in tick, which is
        /// what keeps a turned or offset camera from breaking path interpolation.</summary>
        public float AnchorDistance(ScreenAnchorData key)
            => CameraZ(TimeAt(key.tick)) + SpatialDirector.NearDepth;
        public Vector3 PathAtDistance(PathData path, float distance) => chart.videoSpace.schemaVersion >= 2
            ? Interpolate(path.screenAnchors, distance, false) : SceneAtZ(distance) + Interpolate(path.screenAnchors, distance, true);
        Vector3 Interpolate(ScreenAnchorData[] keys, float distance, bool relative)
        {
            // Anchors are ordered and parameterised by rail distance, never by world
            // Z: an offset or rotated camera is free to move the curve anywhere.
            int i = 0;
            while (i + 1 < keys.Length && AnchorDistance(keys[i + 1]) <= distance) i++;
            int next = Mathf.Min(i + 1, keys.Length - 1);
            float from = AnchorDistance(keys[i]), to = AnchorDistance(keys[next]);
            Vector3 a = AnchorWorld(keys[i]), b = AnchorWorld(keys[next]);
            if (relative)
            {
                a -= SceneAtZ(from); b -= SceneAtZ(to);
                var offset = Vector3.Lerp(a, b, Mathf.InverseLerp(from, to, distance)); offset.z = 0;
                return offset;
            }
            // The rail continues past the authored range. Notes are visible from a far
            // gate that normally sits beyond the last anchor, so clamping onto the
            // endpoint here would stop the track dead and pull every entry frame off
            // the far end.
            if (distance <= from) return ExtendRail(keys[i], distance);
            if (distance >= to) return ExtendRail(keys[next], distance);
            return Vector3.Lerp(a, b, Mathf.InverseLerp(from, to, distance));
        }
        // Continue one authored anchor down its own rail: keep the percentages it was
        // authored with and move only along its depth axis. With the legacy
        // axis-aligned camera this is exactly "keep X/Y, set Z to the query distance".
        Vector3 ExtendRail(ScreenAnchorData key, float distance)
        {
            double time = TimeAt(key.tick);
            var pose = Pose(time);
            Vector3 local = LocalPoint(key.xPercent, key.yPercent, pose.fov);
            local.z = distance - CameraZ(time);
            return pose.position + pose.rotation * local;
        }
        public static ScreenAnchorData[] DefaultAnchors(int endTick, float x = 50, float y = 70)
            => new[] { new ScreenAnchorData { tick = 0, xPercent = x, yPercent = y },
                new ScreenAnchorData { tick = endTick, xPercent = x, yPercent = y } };
        public static VideoCameraPoseKey[] DefaultCameraPose(int endTick)
            => new[] { new VideoCameraPoseKey { tick = 0, fov = FieldOfView },
                new VideoCameraPoseKey { tick = endTick, fov = FieldOfView } };
        public static void Initialize(ChartData chart)
        {
            int end = Mathf.Max(1, Mathf.RoundToInt(chart.endBeat * chart.ticksPerBeat));
            if (!Enabled(chart))
            {
                var clock = new TempoMap(chart.tempos, chart.ticksPerBeat);
                chart.videoSpace = new VideoChartSpaceData {
                    schemaVersion = 2,
                    cameraZKeys = new[] { new CameraZKey { tick = 0, z = 0 },
                        new CameraZKey { tick = end, z = (float)clock.SecondsAtBeat(chart.endBeat) * 5 } },
                    sceneAnchors = new ScreenAnchorData[0] };
            }
            for (int i = 0; i < chart.paths.Length; i++)
                if (chart.paths[i].screenAnchors == null || chart.paths[i].screenAnchors.Length == 0)
                    chart.paths[i].screenAnchors = DefaultAnchors(end, Mathf.Clamp(50 + (i - (chart.paths.Length - 1) * .5f) * 16, 5, 95));
            // Seed an all-zero rig so the track is visible and ready to key. Every
            // value is the identity, so the framing is still exactly the old one.
            if (chart.videoSpace.cameraPoseKeys == null || chart.videoSpace.cameraPoseKeys.Length == 0)
                chart.videoSpace.cameraPoseKeys = DefaultCameraPose(end);
            if (chart.videoSpace.schemaVersion == 1) MergeLegacySceneIntoPaths(chart);
        }
        static void MergeLegacySceneIntoPaths(ChartData chart)
        {
            var space = new VideoChartSpace(chart, new TempoMap(chart.tempos, chart.ticksPerBeat));
            var merged = new ScreenAnchorData[chart.paths.Length][];
            // V1 adds two piecewise-linear functions of Z. Sampling the UNION of
            // their breakpoints preserves the complete curve, not just path endpoints.
            // Build all tracks before committing, so an error never leaves half a migration.
            for (int i = 0; i < chart.paths.Length; i++)
            {
                var ticks = new SortedSet<int>();
                foreach (var key in chart.videoSpace.sceneAnchors) ticks.Add(key.tick);
                foreach (var key in chart.paths[i].screenAnchors) ticks.Add(key.tick);
                var anchors = new List<ScreenAnchorData>();
                foreach (int tick in ticks)
                {
                    float cameraZ = space.CameraZ(space.TimeAt(tick));
                    Vector2 xy = Project(space.PathAtDistance(chart.paths[i], cameraZ + SpatialDirector.NearDepth), cameraZ);
                    anchors.Add(new ScreenAnchorData { tick = tick, xPercent = xy.x, yPercent = xy.y });
                }
                merged[i] = anchors.ToArray();
            }
            for (int i = 0; i < chart.paths.Length; i++) chart.paths[i].screenAnchors = merged[i];
            chart.videoSpace.sceneAnchors = new ScreenAnchorData[0];
            chart.videoSpace.schemaVersion = 2;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static void Validate(ChartData chart)
        {
            if (!Enabled(chart)) return;
            int end = Mathf.RoundToInt(chart.endBeat * chart.ticksPerBeat);
            var data = chart.videoSpace; var keys = data.cameraZKeys;
            if ((data.schemaVersion != 1 && data.schemaVersion != 2) || keys == null || keys.Length < 2)
                throw new InvalidDataException("Video space requires v1/v2 and at least two camera Z keys");
            for (int i = 0; i < keys.Length; i++)
                if (keys[i] == null || !Finite(keys[i].z) || keys[i].tick < 0 || keys[i].tick > end ||
                    !SpatialDirector.IsCameraEasing(keys[i].easing) || (i > 0 && (keys[i].tick <= keys[i - 1].tick || keys[i].z <= keys[i - 1].z)))
                    throw new InvalidDataException("Camera Z keys must have increasing times and Z values");
            if (keys[0].tick != 0 || keys[keys.Length - 1].tick != end)
                throw new InvalidDataException("Camera Z keys must cover beat 0 through chart end");
            if (data.cameraPoseKeys != null) for (int i = 0; i < data.cameraPoseKeys.Length; i++)
            {
                var key = data.cameraPoseKeys[i];
                // fov is the one field whose omitted sentinel (zero) is meaningful;
                // KeyFieldOfView resolves it to the default. Everything else must be
                // finite and ordered, and every head angle is free to exceed 180.
                if (key == null || !Finite(key.dx) || !Finite(key.dy) || !Finite(key.dz) ||
                    !Finite(key.yaw) || !Finite(key.pitch) || !Finite(key.roll) ||
                    !Finite(key.fov) || (key.fov != 0 && (key.fov < 30 || key.fov > 85)) ||
                    !SpatialDirector.IsCameraEasing(key.easing) || key.tick < 0 || key.tick > end ||
                    (i > 0 && key.tick <= data.cameraPoseKeys[i - 1].tick))
                    throw new InvalidDataException("Camera pose keys require ordered ticks, finite offsets and head angles, and FOV 30-85");
            }
            if (data.schemaVersion == 1) ValidateAnchors(data.sceneAnchors, end, true);
            if (chart.paths != null) foreach (var path in chart.paths) ValidateAnchors(path.screenAnchors, end, data.schemaVersion == 1);
        }
        static void ValidateAnchors(ScreenAnchorData[] keys, int end, bool insideFrame)
        {
            if (keys == null || keys.Length < 1) throw new InvalidDataException("Missing screen anchor track");
            for (int i = 0; i < keys.Length; i++)
                if (keys[i] == null || keys[i].tick < 0 || keys[i].tick > end || !Finite(keys[i].xPercent) || !Finite(keys[i].yPercent) ||
                    (insideFrame && (keys[i].xPercent < 0 || keys[i].xPercent > 100 || keys[i].yPercent < 0 || keys[i].yPercent > 100)) ||
                    (i > 0 && keys[i].tick <= keys[i - 1].tick))
                    throw new InvalidDataException("Screen anchors require ordered ticks and finite video X/Y percentages");
        }
    }
}
