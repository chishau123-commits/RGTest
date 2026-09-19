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
        // Authored visuals are optional so every v1 chart remains readable. Instances copy
        // the reusable-library values into the chart; playback never depends on a user's
        // local preset library being present.
        public SceneObjectData[] sceneObjects;
        public EffectClipData[] effectClips;
        public CameraMotionClipData[] cameraMotionClips;
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
    [Serializable] public sealed class PathData
    {
        public string id;
        public float roll;
        // Optional spatial offset track. Tick maps to a cross-section on the stage route.
        // Absent tracks retain the original section/bend/lift behaviour.
        public PathOffsetKey[] offsetKeys;
    }
    [Serializable] public sealed class PathOffsetKey { public int tick; public float x, y; }
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
        // Outgoing interpolation from this key to the next. Missing values keep the
        // original smoothstep behaviour for existing version-1 charts.
        public string easing;
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
        // New editor captures are explicit world positions, exactly at the placement marker.
        public bool useWorldPose;
        public Vector3 worldPosition;
        public Vector3 worldTarget;
    }
    [Serializable] public sealed class NoteData
    {
        public string id;
        public int tick;
        public string pathId;
        public string action;
        public bool protectedNote;
    }

    [Serializable] public sealed class SceneObjectData
    {
        public string id;
        public string name;
        public string assetId;
        // cube, sphere, cylinder, plane, obj, image or video
        public string kind = "cube";
        public string sourcePath;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
        public Color color = Color.white;
        // none, float, rotate, pulse or pendulum
        public string animation = "none";
        public float animationSpeed = 1;
        public float animationAmount = 1;
    }

    [Serializable] public sealed class EffectClipData
    {
        public string id;
        public string name;
        public string presetId;
        public int startTick;
        public int durationTicks = 960;
        // flash, color, fog, light, scenePulse, shockwave, particles, speedLines or glitch
        public string kind = "flash";
        // screen, scene, judgement or a SceneObjectData id
        public string target = "screen";
        public Color color = Color.white;
        public float intensity = 1;
        public float frequency = 1;
        public int seed;
        public string easing = "smooth";
    }

    [Serializable] public sealed class CameraMotionKeyData
    {
        // Normalized time in the reusable clip, in [0,1].
        public float time;
        public Vector3 position;
        public Vector3 rotation;
        public float fov;
    }

    [Serializable] public sealed class CameraMotionClipData
    {
        public string id;
        public string name;
        public string presetId;
        public int startTick;
        public int durationTicks = 960;
        // keys is fully user-authored; kind adds an optional procedural layer.
        public string kind = "custom";
        public float intensity = 1;
        public float frequency = 2;
        public int seed;
        public string easing = "smooth";
        public CameraMotionKeyData[] keys;
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
            {
                Require(!string.IsNullOrEmpty(p.id) && paths.Add(p.id) && Finite(p.roll), "duplicate/invalid path");
                if (p.offsetKeys == null) continue;
                for (int i = 0; i < p.offsetKeys.Length; i++)
                {
                    var k = p.offsetKeys[i];
                    Require(k != null && k.tick >= 0 && k.tick <= c.endBeat * c.ticksPerBeat &&
                        Finite(k.x) && Finite(k.y) && (i == 0 || k.tick > p.offsetKeys[i - 1].tick), "invalid path offset key");
                }
            }
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
                Require(SpatialDirector.IsCameraEasing(k.easing), "unknown camera easing");
                if (k.usePathPose)
                    Require(Finite(k.positionForward) && Finite(k.positionX) && Finite(k.positionY) &&
                        Finite(k.targetForward) && Finite(k.targetX) && Finite(k.targetY), "invalid camera path pose");
                if (k.useWorldPose)
                    Require(Finite(k.worldPosition.x) && Finite(k.worldPosition.y) && Finite(k.worldPosition.z) &&
                        Finite(k.worldTarget.x) && Finite(k.worldTarget.y) && Finite(k.worldTarget.z) &&
                        (k.worldTarget - k.worldPosition).sqrMagnitude > .000001f, "invalid camera world pose");
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
            if (c.sceneObjects != null)
            {
                var sceneIds = new HashSet<string>();
                foreach (var o in c.sceneObjects)
                    Require(o != null && !string.IsNullOrEmpty(o.id) && sceneIds.Add(o.id) &&
                        Finite(o.position.x) && Finite(o.position.y) && Finite(o.position.z) &&
                        Finite(o.rotation.x) && Finite(o.rotation.y) && Finite(o.rotation.z) &&
                        Finite(o.scale.x) && Finite(o.scale.y) && Finite(o.scale.z) &&
                        Finite(o.animationSpeed) && Finite(o.animationAmount), "invalid scene object");
            }
            if (c.effectClips != null) foreach (var e in c.effectClips)
                Require(e != null && !string.IsNullOrEmpty(e.id) && e.startTick >= 0 && e.durationTicks > 0 &&
                    e.startTick + e.durationTicks <= c.endBeat * c.ticksPerBeat && Finite(e.intensity) && Finite(e.frequency), "invalid effect clip");
            if (c.cameraMotionClips != null) foreach (var m in c.cameraMotionClips)
            {
                Require(m != null && !string.IsNullOrEmpty(m.id) && m.startTick >= 0 && m.durationTicks > 0 &&
                    m.startTick + m.durationTicks <= c.endBeat * c.ticksPerBeat && Finite(m.intensity) && Finite(m.frequency), "invalid camera motion clip");
                if (m.keys != null) foreach (var k in m.keys)
                    Require(k != null && Finite(k.time) && k.time >= 0 && k.time <= 1 && Finite(k.position.x) && Finite(k.position.y) &&
                        Finite(k.position.z) && Finite(k.rotation.x) && Finite(k.rotation.y) && Finite(k.rotation.z) && Finite(k.fov), "invalid camera motion key");
            }
            Array.Sort(c.notes, (a, b) => { int t = a.tick.CompareTo(b.tick); return t != 0 ? t : string.CompareOrdinal(a.id, b.id); });
            return c;
        }
    }
}
