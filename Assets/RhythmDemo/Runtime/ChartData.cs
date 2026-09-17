using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeometryRhythm
{
    // JSON DTOs contain only values and stable IDs. The future standalone chart editor
    // can share this assembly without depending on UnityEditor or scene object references.
    [Serializable] public sealed class ChartData
    {
        public int version = 1;
        public string title;
        public string author;
        public int ticksPerBeat = 480;
        public float endBeat = 128;
        public float approachSeconds = 3.4f;
        public string audioResource = "";
        public float audioOffsetSeconds;
        public StagePathData stagePath;
        public MapData map;
        public TempoData[] tempos;
        public PathData[] paths;
        public SectionData[] sections;
        public CameraKey[] cameraKeys;
        public NoteData[] notes;
    }
    [Serializable] public sealed class StagePathData
    {
        public float unitsPerSecond = 5;
        public StagePointData[] points;
    }
    [Serializable] public sealed class StagePointData
    {
        public float x, y, z;
        public float roll;
    }
    [Serializable] public sealed class MapData
    {
        public int seed = 20260917;
        public float corridorWidth = 18;
        public float density = .55f;
        public float heightVariation = 16;
    }
    [Serializable] public sealed class TempoData { public int tick; public float bpm; }
    [Serializable] public sealed class PathData { public string id; public float roll; }
    [Serializable] public sealed class PathPlacement
    {
        public string pathId;
        public float x, y, bend, lift;
    }
    [Serializable] public sealed class SectionData
    {
        public float startBeat;
        public string name;
        public PathPlacement[] placements;
    }
    [Serializable] public sealed class CameraKey
    {
        public float beat;
        public float orbit;
        public float roll;
        public float distance = 25;
        public float height = 6;
        public float fov = 53;
        // Optional stage-local pose used by the desktop chart editor. Legacy charts leave
        // this false and continue to use orbit/distance/height without any migration.
        public bool usePathPose;
        public float positionForward = -7;
        public float positionX;
        public float positionY = 8;
        public float targetForward = 18;
        public float targetX;
        public float targetY = 2;
    }
    [Serializable] public sealed class NoteData
    {
        public string id;
        public int tick;
        public string pathId;
        public string action;
        public bool protectedNote;
    }

    /// <summary>Piecewise tempo map; all runtime judgment times use double seconds.</summary>
    public sealed class TempoMap
    {
        readonly TempoData[] changes;
        readonly int resolution;
        public TempoMap(TempoData[] changes, int resolution)
        { this.changes = changes; this.resolution = resolution; }
        public double SecondsAtBeat(double beat)
        {
            double tick = beat * resolution, time = 0;
            for (int i = 0; i < changes.Length; i++)
            {
                double end = i + 1 < changes.Length ? changes[i + 1].tick : tick;
                end = Math.Min(end, tick);
                if (end > changes[i].tick)
                    time += (end - changes[i].tick) / resolution * 60.0 / changes[i].bpm;
                if (end >= tick) break;
            }
            return time;
        }
        public double BeatAtSeconds(double seconds)
        {
            for (int i = 0; i < changes.Length; i++)
            {
                double length = i + 1 < changes.Length
                    ? (changes[i + 1].tick - changes[i].tick) / (double)resolution * 60 / changes[i].bpm
                    : double.PositiveInfinity;
                if (seconds <= length)
                    return changes[i].tick / (double)resolution + seconds * changes[i].bpm / 60;
                seconds -= length;
            }
            return 0;
        }
    }

    public static class ChartLoader
    {
        static void Require(bool condition, string reason)
        { if (!condition) throw new FormatException("Invalid chart: " + reason); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static ChartData Parse(string json)
        {
            var c = JsonUtility.FromJson<ChartData>(json);
            Require(c != null && c.version == 1, "unsupported or missing version");
            Require(c.ticksPerBeat > 0 && Finite(c.endBeat) && c.endBeat > 0, "invalid timeline");
            Require(Finite(c.approachSeconds) && c.approachSeconds >= 1 && c.approachSeconds <= 10,
                "approachSeconds must be in [1,10]");
            Require(Finite(c.audioOffsetSeconds), "invalid audio offset");
            // JsonUtility may instantiate a missing nested object. No points therefore means
            // a legacy chart; a non-empty custom path must still be structurally complete.
            if (c.stagePath != null && c.stagePath.points != null && c.stagePath.points.Length > 0)
            {
                Require(Finite(c.stagePath.unitsPerSecond) && c.stagePath.unitsPerSecond > 0 &&
                    c.stagePath.unitsPerSecond <= 100, "invalid stage speed");
                Require(c.stagePath.points != null && c.stagePath.points.Length >= 2, "stage path needs at least two points");
                foreach (var p in c.stagePath.points)
                    Require(Finite(p.x) && Finite(p.y) && Finite(p.z) && Finite(p.roll), "invalid stage point");
            }
            if (c.map != null)
                Require(Finite(c.map.corridorWidth) && c.map.corridorWidth >= 4 &&
                    Finite(c.map.density) && c.map.density >= 0 && c.map.density <= 2 &&
                    Finite(c.map.heightVariation) && c.map.heightVariation >= 0, "invalid map settings");
            Require(c.tempos != null && c.tempos.Length > 0 && c.tempos[0].tick == 0, "tempo must start at tick 0");
            for (int i = 0; i < c.tempos.Length; i++)
                Require(Finite(c.tempos[i].bpm) && c.tempos[i].bpm > 0 &&
                    (i == 0 || c.tempos[i].tick > c.tempos[i - 1].tick), "tempo order/BPM");
            if (c.stagePath != null && c.stagePath.points != null && c.stagePath.points.Length > 0)
            {
                var customRoute = new StageSpline(c.stagePath);
                double duration = new TempoMap(c.tempos, c.ticksPerBeat).SecondsAtBeat(c.endBeat);
                double requiredLength = duration * c.stagePath.unitsPerSecond + SpatialDirector.FarDepth;
                Require(customRoute.Length + .01 >= requiredLength,
                    "stage path is too short; extend it to at least " + Math.Ceiling(requiredLength) + " world units");
            }
            var paths = new HashSet<string>();
            Require(c.paths != null && c.paths.Length > 0, "missing paths");
            foreach (var p in c.paths)
                Require(!string.IsNullOrEmpty(p.id) && paths.Add(p.id) && Finite(p.roll), "duplicate/invalid path");
            Require(c.sections != null && c.sections.Length > 0 && c.sections[0].startBeat == 0, "missing section at zero");
            for (int i = 0; i < c.sections.Length; i++)
            {
                var s = c.sections[i];
                Require(Finite(s.startBeat) && s.startBeat < c.endBeat &&
                    (i == 0 || s.startBeat > c.sections[i - 1].startBeat), "section order");
                Require(s.placements != null && s.placements.Length > 0, "empty section");
                var used = new HashSet<string>();
                foreach (var p in s.placements)
                    Require(paths.Contains(p.pathId) && used.Add(p.pathId) && Finite(p.x) && Finite(p.y)
                        && Finite(p.bend) && Finite(p.lift), "invalid placement");
            }
            Require(c.cameraKeys != null && c.cameraKeys.Length > 0 && c.cameraKeys[0].beat == 0, "missing camera at zero");
            for (int i = 0; i < c.cameraKeys.Length; i++)
            {
                var k = c.cameraKeys[i];
                Require(Finite(k.beat) && Finite(k.orbit) && Finite(k.roll) && Finite(k.height) && Finite(k.distance)
                    && k.distance >= 12 && Finite(k.fov) && k.fov >= 30 && k.fov <= 85
                    && (i == 0 || k.beat > c.cameraKeys[i - 1].beat), "invalid camera key");
                if (k.usePathPose)
                    Require(Finite(k.positionForward) && Finite(k.positionX) && Finite(k.positionY) &&
                        Finite(k.targetForward) && Finite(k.targetX) && Finite(k.targetY), "invalid camera path pose");
            }
            Require(c.notes != null && c.notes.Length > 0, "missing notes");
            var ids = new HashSet<string>();
            foreach (var n in c.notes)
            {
                Require(!string.IsNullOrEmpty(n.id) && ids.Add(n.id), "duplicate note ID");
                Require(paths.Contains(n.pathId) && (n.action == "tap" || n.action == "drag"), "unknown path/action");
                float beat = n.tick / (float)c.ticksPerBeat;
                Require(beat >= 0 && beat < c.endBeat, "note outside song");
                int section = 0;
                while (section + 1 < c.sections.Length && c.sections[section + 1].startBeat <= beat) section++;
                Require(Array.Exists(c.sections[section].placements, p => p.pathId == n.pathId), "note on inactive path: " + n.id);
            }
            Array.Sort(c.notes, (a, b) => { int t = a.tick.CompareTo(b.tick); return t != 0 ? t : string.CompareOrdinal(a.id, b.id); });
            return c;
        }
    }
}
