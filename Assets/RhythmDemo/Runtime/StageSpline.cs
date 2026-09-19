using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeometryRhythm
{
    /// <summary>
    /// Arc-length sampled Catmull-Rom stage path with a parallel-transport frame. The
    /// deterministic fallback is the original demo route, so version-1 charts keep working.
    /// </summary>
    public sealed class StageSpline
    {
        struct Sample
        {
            public float distance;
            public Vector3 position;
            public Quaternion rotation;
        }

        readonly StagePathData data;
        readonly Sample[] samples;
        const int Subdivisions = 32;
        public bool IsCustom => samples != null;
        public float UnitsPerSecond => data == null ? 5 : data.unitsPerSecond;
        public float Length => samples == null ? float.PositiveInfinity : samples[samples.Length - 1].distance;

        public StageSpline(StagePathData data)
        {
            this.data = data;
            if (data == null || data.points == null || data.points.Length < 2) return;

            int segmentCount = data.points.Length - 1;
            const int subdivisions = Subdivisions;
            var positions = new List<Vector3>(segmentCount * subdivisions + 1);
            var rolls = new List<float>(segmentCount * subdivisions + 1);
            for (int segment = 0; segment < segmentCount; segment++)
            {
                for (int step = 0; step < subdivisions; step++)
                {
                    float t = step / (float)subdivisions;
                    positions.Add(EvaluateSegment(segment, t));
                    rolls.Add(Mathf.Lerp(data.points[segment].roll, data.points[segment + 1].roll, t));
                }
            }
            positions.Add(Point(data.points[data.points.Length - 1]));
            rolls.Add(data.points[data.points.Length - 1].roll);

            samples = new Sample[positions.Count];
            float distance = 0;
            Vector3 firstTangent = SafeTangent(positions, 0);
            Vector3 right = Vector3.Cross(Vector3.up, firstTangent).normalized;
            if (right.sqrMagnitude < .001f) right = Vector3.right;
            Vector3 up = Vector3.Cross(firstTangent, right).normalized;
            Vector3 previousTangent = firstTangent;
            for (int i = 0; i < positions.Count; i++)
            {
                if (i > 0) distance += Vector3.Distance(positions[i - 1], positions[i]);
                Vector3 tangent = SafeTangent(positions, i);
                if (i > 0)
                {
                    Quaternion transport = Quaternion.FromToRotation(previousTangent, tangent);
                    right = transport * right;
                    up = transport * up;
                }
                Quaternion baseRotation = Quaternion.LookRotation(tangent, up);
                samples[i] = new Sample
                {
                    distance = distance,
                    position = positions[i],
                    rotation = baseRotation * Quaternion.AngleAxis(rolls[i], Vector3.forward)
                };
                previousTangent = tangent;
            }
        }

        static Vector3 Point(StagePointData p) => new Vector3(p.x, p.y, p.z);
        Vector3 EvaluateSegment(int segment, float t)
        {
            var p = data.points;
            Vector3 a = Point(p[Mathf.Max(0, segment - 1)]);
            Vector3 b = Point(p[segment]);
            Vector3 c = Point(p[segment + 1]);
            Vector3 d = Point(p[Mathf.Min(p.Length - 1, segment + 2)]);
            float t2 = t * t, t3 = t2 * t;
            return .5f * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
        }
        static Vector3 SafeTangent(List<Vector3> values, int index)
        {
            Vector3 value = values[Mathf.Min(values.Count - 1, index + 1)] - values[Mathf.Max(0, index - 1)];
            return value.sqrMagnitude < .000001f ? Vector3.forward : value.normalized;
        }
        int LowerSample(float distance)
        {
            int low = 0, high = samples.Length - 1;
            while (low + 1 < high)
            {
                int middle = (low + high) / 2;
                if (samples[middle].distance <= distance) low = middle; else high = middle;
            }
            return low;
        }
        void Evaluate(float distance, out Vector3 position, out Quaternion rotation)
        {
            if (samples == null)
            {
                position = LegacyPosition(distance);
                rotation = LegacyRotation(distance);
                return;
            }
            distance = Mathf.Clamp(distance, 0, Length);
            int a = LowerSample(distance), b = Mathf.Min(a + 1, samples.Length - 1);
            float mix = Mathf.InverseLerp(samples[a].distance, samples[b].distance, distance);
            position = Vector3.Lerp(samples[a].position, samples[b].position, mix);
            rotation = Quaternion.Slerp(samples[a].rotation, samples[b].rotation, mix);
        }
        public Vector3 Position(float distance) { Evaluate(distance, out var p, out _); return p; }
        public float ControlPointDistance(int index)
            => samples == null ? 0 : samples[Mathf.Clamp(index * Subdivisions, 0, samples.Length - 1)].distance;
        public Quaternion Rotation(float distance) { Evaluate(distance, out _, out var r); return r; }
        public Vector3 OffsetPoint(float distance, float x, float y)
        {
            Evaluate(distance, out var p, out var r);
            return p + r * new Vector3(x, y, 0);
        }
        public static Vector3 LegacyPosition(float distance)
            => new Vector3(10 * Mathf.Sin(distance / 62) + 4 * Mathf.Sin(distance / 29),
                1.8f * Mathf.Sin(distance / 47), distance);
        public static Quaternion LegacyRotation(float distance)
            => Quaternion.LookRotation((LegacyPosition(distance + .1f) - LegacyPosition(distance - .1f)).normalized, Vector3.up);
    }
}
