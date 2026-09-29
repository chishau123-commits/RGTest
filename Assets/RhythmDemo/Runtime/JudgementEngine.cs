using System;
using UnityEngine;

namespace GeometryRhythm
{
    public enum NoteResult { Pending, Perfect, Good, Miss, Skipped }
    public sealed class RuntimeNote
    {
        public NoteData Data;
        public double HitTime;
        public NoteResult Result;
        public NoteVisual View;
    }

    /// <summary>Logical scoring has no dependency on physics, camera movement or GameObject lifetime.</summary>
    public sealed class JudgementEngine
    {
        public const double PerfectWindow = .060;
        public const double GoodWindow = .150;
        public readonly RuntimeNote[] Notes;
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Judged { get; private set; }
        public int Misses { get; private set; }
        public int Perfects { get; private set; }
        public int Goods { get; private set; }
        public int Eligible { get; private set; }
        public double Earned { get; private set; }
        public int Score => Eligible == 0 ? 0 : (int)Math.Round(1000000 * Earned / Eligible);
        public double Accuracy => Judged == 0 ? 100 : 100 * Earned / Judged;
        public Action<RuntimeNote, NoteResult> OnJudged;

        public JudgementEngine(ChartData chart, TempoMap tempo)
        {
            Notes = new RuntimeNote[chart.notes.Length];
            for (int i = 0; i < Notes.Length; i++)
                Notes[i] = new RuntimeNote { Data = chart.notes[i], HitTime = tempo.SecondsAtBeat(chart.notes[i].tick / (double)chart.ticksPerBeat) };
            Reset(0);
        }
        // Seeking starts a practice segment. Skipped notes never become invisible misses.
        public void Reset(double time)
        {
            Combo = MaxCombo = Judged = Misses = Perfects = Goods = 0;
            Earned = 0; Eligible = 0;
            foreach (var n in Notes)
            {
                n.Result = n.HitTime < time - GoodWindow ? NoteResult.Skipped : NoteResult.Pending;
                if (n.Result == NoteResult.Pending) Eligible++;
            }
        }
        void Resolve(RuntimeNote n, NoteResult result)
        {
            if (n.Result != NoteResult.Pending) return;
            n.Result = result; Judged++;
            if (result == NoteResult.Miss) { Misses++; Combo = 0; }
            else
            {
                Combo++; MaxCombo = Math.Max(MaxCombo, Combo);
                if (result == NoteResult.Perfect) { Perfects++; Earned += 1; }
                else { Goods++; Earned += .65; }
            }
            OnJudged?.Invoke(n, result);
        }
        /// <summary>One down event consumes at most one Tap. A directly touched local Tap
        /// takes priority over a protected Tap, then absolute time error and chart order break ties.</summary>
        public RuntimeNote Tap(double time, Func<RuntimeNote, bool> inside)
        {
            RuntimeNote best = null;
            double error = double.MaxValue;
            foreach (var n in Notes)
            {
                double e = Math.Abs(n.HitTime - time);
                if (n.Result != NoteResult.Pending || n.Data.action != "tap" || e > GoodWindow) continue;
                if (!n.Data.protectedNote && !inside(n)) continue;
                bool preferLocal = best != null && best.Data.protectedNote && !n.Data.protectedNote;
                if (best == null || preferLocal || (best.Data.protectedNote == n.Data.protectedNote && e < error))
                { best = n; error = e; }
            }
            if (best != null) Resolve(best, error <= PerfectWindow ? NoteResult.Perfect : NoteResult.Good);
            return best;
        }
        // Drag means contact, not a mandatory swipe or a fresh press. Early contact must
        // remain until HitTime. Multiple protected Drags may share the same held contact.
        public void Drag(double time, Func<RuntimeNote, bool> inside, bool hasContact)
        {
            if (!hasContact) return;
            foreach (var n in Notes)
            {
                double error = time - n.HitTime;
                if (n.Result != NoteResult.Pending || n.Data.action != "drag" || error < 0 || error > GoodWindow) continue;
                if (n.Data.protectedNote || inside(n))
                    Resolve(n, error <= PerfectWindow ? NoteResult.Perfect : NoteResult.Good);
            }
        }
        public void Advance(double time, bool autoPlay)
        {
            foreach (var n in Notes)
            {
                if (n.Result != NoteResult.Pending) continue;
                if (autoPlay && time >= n.HitTime) Resolve(n, NoteResult.Perfect);
                else if (time > n.HitTime + GoodWindow) Resolve(n, NoteResult.Miss);
            }
        }
    }

    /// <summary>Projects the actual tilted disc to a convex polygon. The hollow centre is
    /// tappable too. Padding is screen-space forgiveness, not a billboard visual.</summary>
    public static class NoteProjection
    {
        public static bool Contains(Camera camera, Transform disc, float radius, Vector2 point, Vector2[] polygon, float padding)
        {
            Vector3 centre = camera.WorldToScreenPoint(disc.position);
            if (centre.z <= camera.nearClipPlane) return false;
            for (int i = 0; i < polygon.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / polygon.Length;
                Vector3 p = camera.WorldToScreenPoint(disc.TransformPoint(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius));
                if (p.z <= camera.nearClipPlane) return false;
                polygon[i] = p;
            }
            bool positive = false, negative = false;
            float distanceSq = float.MaxValue;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                Vector2 edge = b - a, v = point - a;
                float cross = edge.x * v.y - edge.y * v.x;
                positive |= cross > .001f; negative |= cross < -.001f;
                Vector2 nearest = a + edge * Mathf.Clamp01(Vector2.Dot(v, edge) / Mathf.Max(edge.sqrMagnitude, .0001f));
                distanceSq = Mathf.Min(distanceSq, (point - nearest).sqrMagnitude);
            }
            return !(positive && negative) || distanceSq <= padding * padding;
        }
    }
}
