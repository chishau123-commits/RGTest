using System;
using UnityEngine;

namespace RingGame.Core
{
    // Authoring data stores musical ticks only. Seconds are derived during compilation.
    [Serializable]
    public sealed class ChartDocument
    {
        public string schemaVersion;
        public ChartTimebase timebase;
        public ChartSettings settings;
        public NoteDto[] notes;
        public PathDto[] paths;
        public CameraActionDto[] actions;
        public DecorationDto[] decorations;
    }

    [Serializable]
    public sealed class ChartTimebase
    {
        public int ppq;
        public long offsetUs;
        public TempoDto[] tempos;
    }

    [Serializable]
    public sealed class TempoDto
    {
        public long tick;
        public double bpm;
    }

    [Serializable]
    public sealed class ChartSettings
    {
        public string title;
        public int requiredTouches;
    }

    [Serializable]
    public struct ChartPoint
    {
        public float x;
        public float y;
        public ChartPoint(float x, float y) { this.x = x; this.y = y; }
        public Vector2 ToVector2() { return new Vector2(x, y); }
    }

    [Serializable]
    public sealed class NoteDto
    {
        public string id;
        public long tick;
        public long spawnTick;
        public string motion;
        public ChartPoint target;
        public float radius;
        public string pathId;
    }

    [Serializable]
    public sealed class PathDto
    {
        public string id;
        public string type;
        public ChartPoint start;
        public ChartPoint end;
    }

    [Serializable]
    public sealed class CameraActionDto
    {
        public string id;
        public string eventType;
        public long tick;
        public long durationTicks;
        public ChartPoint position;
        public float rotation;
        public float scale;
        public string ease;
    }

    [Serializable]
    public sealed class DecorationDto
    {
        public string id;
    }

    public enum NoteMotion { Shrink, Arrival }
    public enum CameraEase { Linear, Smooth }

    public sealed class CompiledNote
    {
        public string Id { get; private set; }
        public NoteMotion Motion { get; private set; }
        public double SpawnSeconds { get; private set; }
        public double HitSeconds { get; private set; }
        public Vector2 Target { get; private set; }
        public float Radius { get; private set; }
        public Vector2 PathStart { get; private set; }
        internal CompiledNote(string id, NoteMotion motion, double spawn, double hit,
            Vector2 target, float radius, Vector2 start)
        {
            Id = id; Motion = motion; SpawnSeconds = spawn; HitSeconds = hit;
            Target = target; Radius = radius; PathStart = start;
        }
    }

    public sealed class CompiledCameraAction
    {
        public string Id { get; private set; }
        public double StartSeconds { get; private set; }
        public double EndSeconds { get; private set; }
        public CameraPose From { get; private set; }
        public CameraPose To { get; private set; }
        public CameraEase Ease { get; private set; }
        internal CompiledCameraAction(string id, double start, double end,
            CameraPose from, CameraPose to, CameraEase ease)
        {
            Id = id; StartSeconds = start; EndSeconds = end;
            From = from; To = to; Ease = ease;
        }
    }

    public sealed class CompiledChart
    {
        public CompiledNote[] Notes { get; private set; }
        public CompiledCameraAction[] CameraActions { get; private set; }
        public TempoMap TimeMap { get; private set; }
        public string Title { get; private set; }
        public int RequiredTouches { get; private set; }
        internal CompiledChart(CompiledNote[] notes, CompiledCameraAction[] actions,
            TempoMap timeMap, string title, int requiredTouches)
        {
            Notes = notes; CameraActions = actions; TimeMap = timeMap;
            Title = title; RequiredTouches = requiredTouches;
        }
    }
}
