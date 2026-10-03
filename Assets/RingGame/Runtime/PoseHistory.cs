using System;
using System.Collections.Generic;
using UnityEngine;

namespace RingGame.Runtime
{
    [Serializable]
    public sealed class PoseSnapshot
    {
        public long FrameId;
        public double SubmittedInputTime;
        public double VisualSongSeconds;
        public Rect Viewport;
        public Matrix4x4 ChartToScreen;

        public bool TryScreenToChart(Vector2 screenPosition, out Vector2 chartPosition)
        {
            chartPosition = default;
            if (!Viewport.Contains(screenPosition) || Math.Abs(ChartToScreen.determinant) < 1e-10f)
                return false;
            var projected = ChartToScreen.inverse.MultiplyPoint3x4(screenPosition);
            if (float.IsNaN(projected.x) || float.IsInfinity(projected.x) ||
                float.IsNaN(projected.y) || float.IsInfinity(projected.y)) return false;
            chartPosition = new Vector2(projected.x, projected.y);
            return true;
        }

        internal PoseSnapshot Copy()
        {
            return new PoseSnapshot
            {
                FrameId = FrameId,
                SubmittedInputTime = SubmittedInputTime,
                VisualSongSeconds = VisualSongSeconds,
                Viewport = Viewport,
                ChartToScreen = ChartToScreen
            };
        }
    }

    /// <summary>
    /// Retains CPU render-submission poses. Selection estimates the displayed frame using a
    /// separately configured latency; it does not claim to measure actual display presentation.
    /// No pose is invented for a missing, future or expired history interval.
    /// </summary>
    public sealed class PoseHistory
    {
        private readonly List<PoseSnapshot> snapshots = new List<PoseSnapshot>(64);
        private readonly double historySeconds;

        public PoseHistory(double historySeconds = 0.5)
        {
            if (double.IsNaN(historySeconds) || double.IsInfinity(historySeconds) || historySeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(historySeconds));
            this.historySeconds = historySeconds;
        }

        public int Count => snapshots.Count;
        public void Clear() => snapshots.Clear();

        public void Add(PoseSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!Finite(snapshot.SubmittedInputTime) || !Finite(snapshot.VisualSongSeconds))
                throw new ArgumentException("Pose timestamps must be finite.", nameof(snapshot));
            if (snapshot.Viewport.width <= 0 || snapshot.Viewport.height <= 0)
                throw new ArgumentException("Pose viewport must have positive dimensions.", nameof(snapshot));
            if (snapshots.Count > 0 && snapshot.SubmittedInputTime < snapshots[snapshots.Count - 1].SubmittedInputTime)
                throw new ArgumentException("Pose submissions must be chronological.", nameof(snapshot));
            snapshots.Add(snapshot.Copy());
            double cutoff = snapshot.SubmittedInputTime - historySeconds;
            int removeCount = 0;
            while (removeCount < snapshots.Count && snapshots[removeCount].SubmittedInputTime < cutoff)
                removeCount++;
            if (removeCount > 0) snapshots.RemoveRange(0, removeCount);
        }

        public bool TrySelect(double inputEventTime, double displayLatencySeconds, out PoseSnapshot snapshot)
        {
            snapshot = null;
            if (snapshots.Count == 0 || !Finite(inputEventTime) || !Finite(displayLatencySeconds) ||
                displayLatencySeconds < 0) return false;
            double estimatedSubmissionTime = inputEventTime - displayLatencySeconds;
            if (estimatedSubmissionTime < snapshots[0].SubmittedInputTime ||
                estimatedSubmissionTime - snapshots[snapshots.Count - 1].SubmittedInputTime > historySeconds)
                return false;
            for (int i = snapshots.Count - 1; i >= 0; i--)
            {
                if (snapshots[i].SubmittedInputTime > estimatedSubmissionTime) continue;
                snapshot = snapshots[i].Copy();
                return true;
            }
            return false;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
