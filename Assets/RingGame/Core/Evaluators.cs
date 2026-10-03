using System;
using UnityEngine;

namespace RingGame.Core
{
    // This is a gameplay similarity transform, rather than a perspective camera.
    // The same pose must be used for rendering and contact inverse projection.
    public struct CameraPose
    {
        public Vector2 Position { get; private set; }
        public float RotationDegrees { get; private set; }
        public float Scale { get; private set; }
        public static CameraPose Identity { get { return new CameraPose(Vector2.zero, 0, 1); } }

        public CameraPose(Vector2 position, float rotationDegrees, float scale)
        {
            if (!ChartCompiler.IsFinite(position) || !ChartCompiler.IsFinite(rotationDegrees) ||
                !ChartCompiler.IsFinite(scale) || scale <= 0)
                throw new ArgumentOutOfRangeException("scale", "Camera pose requires finite values and positive scale.");
            Position = position; RotationDegrees = rotationDegrees; Scale = scale;
        }
        public Vector2 TransformPoint(Vector2 chartPoint)
        {
            float angle = RotationDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            return Position + new Vector2(cosine * chartPoint.x - sine * chartPoint.y,
                sine * chartPoint.x + cosine * chartPoint.y) * Scale;
        }
        public Vector2 InverseTransformPoint(Vector2 viewPoint)
        {
            Vector2 translated = (viewPoint - Position) / Scale;
            float angle = RotationDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            return new Vector2(cosine * translated.x + sine * translated.y,
                -sine * translated.x + cosine * translated.y);
        }
    }

    public static class CameraEvaluator
    {
        public static CameraPose Evaluate(CompiledChart chart, double songSeconds)
        {
            if (chart == null) throw new ArgumentNullException("chart");
            if (!ChartCompiler.IsFinite(songSeconds)) throw new ArgumentOutOfRangeException("songSeconds");
            var pose = CameraPose.Identity;
            foreach (var action in chart.CameraActions)
            {
                if (songSeconds < action.StartSeconds) return pose;
                if (songSeconds >= action.EndSeconds)
                {
                    pose = action.To;
                    continue;
                }
                float progress = (float)((songSeconds - action.StartSeconds) /
                    (action.EndSeconds - action.StartSeconds));
                if (action.Ease == CameraEase.Smooth) progress = progress * progress * (3 - 2 * progress);
                return new CameraPose(Vector2.LerpUnclamped(action.From.Position, action.To.Position, progress),
                    Mathf.LerpUnclamped(action.From.RotationDegrees, action.To.RotationDegrees, progress),
                    Mathf.LerpUnclamped(action.From.Scale, action.To.Scale, progress));
            }
            return pose;
        }
    }

    public struct NoteVisual
    {
        public bool Visible;
        public float Progress;
        public Vector2 Target;
        public Vector2 MovingCenter;
        public float MovingRadius;
        public float ApproachRadius;
    }

    public static class NoteEvaluator
    {
        public static NoteVisual Evaluate(CompiledNote note, double songSeconds)
        {
            if (note == null) throw new ArgumentNullException("note");
            if (!ChartCompiler.IsFinite(songSeconds)) throw new ArgumentOutOfRangeException("songSeconds");
            float progress = Mathf.Clamp01((float)((songSeconds - note.SpawnSeconds) /
                (note.HitSeconds - note.SpawnSeconds)));
            return new NoteVisual
            {
                Visible = songSeconds >= note.SpawnSeconds && songSeconds <= note.HitSeconds + JudgeEngine.GoodWindowSeconds,
                Progress = progress,
                Target = note.Target,
                MovingCenter = note.Motion == NoteMotion.Arrival ?
                    Vector2.LerpUnclamped(note.PathStart, note.Target, progress) : note.Target,
                MovingRadius = note.Radius,
                ApproachRadius = note.Motion == NoteMotion.Shrink ? note.Radius * (3 - 2 * progress) : note.Radius
            };
        }
    }
}
