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
        readonly ChartData chart;
        readonly TempoMap tempo;
        readonly StageSpline route;
        public SpatialDirector(ChartData chart, TempoMap tempo)
        { this.chart = chart; this.tempo = tempo; route = new StageSpline(chart.stagePath); }

        public static Vector3 Route(float distance)
            => StageSpline.LegacyPosition(distance);
        public static Quaternion RouteRotation(float distance)
            => StageSpline.LegacyRotation(distance);
        public float UnitsPerSecond => route.UnitsPerSecond;
        public float RouteLength => route.Length;
        public float DistanceAtTime(double time) => (float)time * route.UnitsPerSecond;
        public Vector3 RouteAt(float distance) => route.Position(distance);
        public Quaternion RouteRotationAt(float distance) => route.Rotation(distance);
        public Vector3 RoutePoint(float distance, float x, float y) => route.OffsetPoint(distance, x, y);
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
                float fadeIn = Mathf.Clamp01((float)(time - start + chart.approachSeconds) / .8f);
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
            var current = Placement(id, time, out var previous, out float blend);
            float q = Mathf.Clamp01((depth - NearDepth) / (FarDepth - NearDepth));
            float x = Mathf.Lerp(previous.x, current.x, blend);
            float y = Mathf.Lerp(previous.y, current.y, blend);
            float bend = Mathf.Lerp(previous.bend, current.bend, blend);
            float lift = Mathf.Lerp(previous.lift, current.lift, blend);
            float s = DistanceAtTime(time) + depth;
            Vector3 local = new Vector3(x * (1 - q * .6f) + Mathf.Sin(q * Mathf.PI * 1.35f) * bend,
                y * (1 - q * .4f) + Mathf.Sin(q * Mathf.PI) * lift, 0);
            return RouteAt(s) + RouteRotationAt(s) * local;
        }
        public float Depth(double hitTime, double time)
        {
            float q = (float)((hitTime - time) / chart.approachSeconds);
            // World-space progress, not accumulating deltaTime. Seek and low FPS stay in sync.
            return NearDepth + q * (FarDepth - NearDepth);
        }
        public void NotePose(RuntimeNote note, double time, out Vector3 position, out Quaternion rotation)
        {
            float depth = Depth(note.HitTime, time);
            position = Point(note.Data.pathId, depth, time);
            Vector3 tangent = (Point(note.Data.pathId, depth + .12f, time) - Point(note.Data.pathId, depth - .12f, time)).normalized;
            // A real world-space plane; never billboard to the camera. Gentle tilt exposes the rim.
            rotation = Quaternion.LookRotation(-tangent, RouteRotationAt(DistanceAtTime(time) + depth) * Vector3.up)
                * Quaternion.Euler(13, 0, 0);
            foreach (var path in chart.paths)
                if(path.id==note.Data.pathId) { rotation*=Quaternion.Euler(0,0,path.roll);break; }
        }
        public void EvaluateCamera(Camera camera, double time)
        {
            float beat = (float)tempo.BeatAtSeconds(time);
            int index = 0;
            while (index + 1 < chart.cameraKeys.Length && chart.cameraKeys[index + 1].beat <= beat) index++;
            var a = chart.cameraKeys[index];
            var b = chart.cameraKeys[Math.Min(index + 1, chart.cameraKeys.Length - 1)];
            float q = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a.beat, b.beat, beat));
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
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up)
                * Quaternion.Euler(0, 0, Mathf.Lerp(a.roll, b.roll, q));
            camera.fieldOfView = Mathf.Lerp(a.fov, b.fov, q);
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
