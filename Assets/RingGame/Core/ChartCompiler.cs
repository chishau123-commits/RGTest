using System;
using System.Collections.Generic;
using UnityEngine;

namespace RingGame.Core
{
    public sealed class ChartValidationException : Exception
    {
        public ChartValidationException(string message) : base(message) { }
    }

    public sealed class TempoMap
    {
        private readonly long[] ticks;
        private readonly double[] seconds;
        private readonly double[] bpms;

        internal TempoMap(TempoDto[] tempos, long offsetUs)
        {
            ticks = new long[tempos.Length];
            seconds = new double[tempos.Length];
            bpms = new double[tempos.Length];
            seconds[0] = offsetUs / 1000000.0;
            for (int i = 0; i < tempos.Length; i++)
            {
                ticks[i] = tempos[i].tick;
                bpms[i] = tempos[i].bpm;
                if (i > 0)
                    seconds[i] = seconds[i - 1] +
                        (ticks[i] - ticks[i - 1]) * (60.0 / ChartCompiler.Ppq / bpms[i - 1]);
                if (!ChartCompiler.IsFinite(seconds[i]))
                    throw new ChartValidationException("Tempo map produces a nonfinite song time.");
            }
        }

        public double TickToSeconds(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException("tick", "Ticks must be nonnegative.");
            int lower = 0;
            int upper = ticks.Length - 1;
            while (lower < upper)
            {
                int mid = (lower + upper + 1) / 2;
                if (ticks[mid] <= tick) lower = mid;
                else upper = mid - 1;
            }
            double result = seconds[lower] + (tick - ticks[lower]) * (60.0 / ChartCompiler.Ppq / bpms[lower]);
            if (!ChartCompiler.IsFinite(result))
                throw new ChartValidationException("Tempo conversion produces a nonfinite song time.");
            return result;
        }
    }

    public static class ChartCompiler
    {
        public const string SchemaVersion = "prototype-ring-0";
        public const int Ppq = 960;

        public static CompiledChart Compile(ChartDocument document)
        {
            Require(document != null, "Chart document is missing.");
            Require(document.schemaVersion == SchemaVersion, "Unsupported schemaVersion; expected " + SchemaVersion + ".");
            Require(document.timebase != null, "timebase is missing.");
            Require(document.timebase.ppq == Ppq, "timebase.ppq must be 960.");
            var tempos = document.timebase.tempos;
            Require(tempos != null && tempos.Length > 0, "At least one tempo is required.");
            for (int i = 0; i < tempos.Length; i++)
            {
                Require(tempos[i] != null, "Tempo entry is missing.");
                Require(tempos[i].tick >= 0, "Tempo tick must be nonnegative.");
                Require(IsFinite(tempos[i].bpm) && tempos[i].bpm > 0, "Tempo bpm must be finite and positive.");
                Require(i == 0 ? tempos[i].tick == 0 : tempos[i].tick > tempos[i - 1].tick,
                    "Tempos must begin at tick 0 and use strictly increasing ticks.");
            }
            Require(document.settings != null, "settings is missing.");
            Require(!string.IsNullOrWhiteSpace(document.settings.title), "settings.title is required.");
            Require(document.settings.requiredTouches > 0, "requiredTouches must be positive.");
            Require(document.notes != null && document.notes.Length > 0, "At least one note is required.");
            Require(document.paths != null, "paths array is missing (use an empty array when unused).");
            Require(document.actions != null, "actions array is missing (use an empty array when unused).");
            Require(document.decorations != null, "decorations array is missing (use an empty array when unused).");
            Require(document.decorations.Length == 0, "Decorations are not supported in prototype-ring-0.");

            var map = new TempoMap(tempos, document.timebase.offsetUs);
            var allIds = new HashSet<string>(StringComparer.Ordinal);
            var paths = new Dictionary<string, PathDto>(StringComparer.Ordinal);
            foreach (var path in document.paths)
            {
                Require(path != null, "Path entry is missing.");
                RegisterId(path.id, allIds);
                Require(path.type == "linear", "Unsupported path type for " + path.id + ".");
                Require(IsFinite(path.start) && IsFinite(path.end), "Path points must be finite: " + path.id + ".");
                Require((path.end.ToVector2() - path.start.ToVector2()).sqrMagnitude > 0.00000001f,
                    "Linear path must have different start/end points: " + path.id + ".");
                paths.Add(path.id, path);
            }

            var compiledNotes = new List<CompiledNote>();
            var chordCounts = new Dictionary<long, int>();
            foreach (var note in document.notes)
            {
                Require(note != null, "Note entry is missing.");
                RegisterId(note.id, allIds);
                Require(note.spawnTick >= 0 && note.tick > note.spawnTick,
                    "A note must have 0 <= spawnTick < tick: " + note.id + ".");
                Require(IsFinite(note.target), "Note target must be finite: " + note.id + ".");
                Require(IsFinite(note.radius) && note.radius > 0, "Note radius must be finite and positive: " + note.id + ".");
                NoteMotion motion;
                Vector2 start = note.target.ToVector2();
                if (note.motion == "shrink")
                {
                    motion = NoteMotion.Shrink;
                    Require(string.IsNullOrEmpty(note.pathId), "A shrink note cannot reference a path: " + note.id + ".");
                }
                else if (note.motion == "arrival")
                {
                    motion = NoteMotion.Arrival;
                    PathDto path;
                    Require(!string.IsNullOrEmpty(note.pathId) && paths.TryGetValue(note.pathId, out path),
                        "Arrival note pathId does not resolve: " + note.id + ".");
                    path = paths[note.pathId];
                    Require((path.end.ToVector2() - note.target.ToVector2()).sqrMagnitude <= 0.00000001f,
                        "Arrival path end must equal the receiving target: " + note.id + ".");
                    start = path.start.ToVector2();
                }
                else throw new ChartValidationException("Unsupported note motion: " + note.motion + ".");
                double spawnSeconds = map.TickToSeconds(note.spawnTick);
                double hitSeconds = map.TickToSeconds(note.tick);
                Require(hitSeconds > spawnSeconds, "Note times cannot be represented distinctly: " + note.id + ".");
                compiledNotes.Add(new CompiledNote(note.id, motion, spawnSeconds,
                    hitSeconds, note.target.ToVector2(), note.radius, start));
                int count;
                chordCounts.TryGetValue(note.tick, out count);
                count++;
                Require(count <= document.settings.requiredTouches,
                    "requiredTouches understates the simultaneous chord at tick " + note.tick + ".");
                chordCounts[note.tick] = count;
            }
            compiledNotes.Sort((left, right) =>
            {
                int time = left.HitSeconds.CompareTo(right.HitSeconds);
                return time != 0 ? time : StringComparer.Ordinal.Compare(left.Id, right.Id);
            });

            var sourceActions = new List<CameraActionDto>(document.actions);
            foreach (var action in sourceActions)
            {
                Require(action != null, "Action entry is missing.");
                RegisterId(action.id, allIds);
                Require(action.eventType == "MoveCamera", "Unsupported eventType: " + action.eventType + ".");
                Require(action.tick >= 0 && action.durationTicks >= 0, "Camera tick/durationTicks must be nonnegative.");
                Require(action.durationTicks <= long.MaxValue - action.tick, "Camera tick + durationTicks overflows.");
                Require(IsFinite(action.position) && IsFinite(action.rotation), "Camera position/rotation must be finite.");
                Require(IsFinite(action.scale) && action.scale > 0, "Camera scale must be finite and positive.");
                Require(action.ease == "linear" || action.ease == "smooth", "Unsupported camera ease: " + action.ease + ".");
            }
            sourceActions.Sort((left, right) => left.tick.CompareTo(right.tick));
            var compiledActions = new List<CompiledCameraAction>();
            var previous = CameraPose.Identity;
            long previousEnd = -1;
            long previousStart = -1;
            foreach (var action in sourceActions)
            {
                Require(action.tick >= previousEnd && action.tick > previousStart,
                    "Camera actions must not overlap or share a start tick.");
                var target = new CameraPose(action.position.ToVector2(), action.rotation, action.scale);
                double startSeconds = map.TickToSeconds(action.tick);
                double endSeconds = map.TickToSeconds(action.tick + action.durationTicks);
                Require(action.durationTicks == 0 || endSeconds > startSeconds,
                    "Camera times cannot be represented distinctly: " + action.id + ".");
                compiledActions.Add(new CompiledCameraAction(action.id, startSeconds,
                    endSeconds, previous, target,
                    action.ease == "linear" ? CameraEase.Linear : CameraEase.Smooth));
                previous = target;
                previousStart = action.tick;
                previousEnd = action.tick + action.durationTicks;
            }
            return new CompiledChart(compiledNotes.ToArray(), compiledActions.ToArray(), map,
                document.settings.title, document.settings.requiredTouches);
        }

        private static void RegisterId(string id, HashSet<string> ids)
        {
            Require(!string.IsNullOrWhiteSpace(id), "Every note/path/action requires a stable nonempty id.");
            Require(ids.Add(id), "Duplicate id: " + id + ".");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new ChartValidationException(message);
        }
        internal static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        internal static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        internal static bool IsFinite(ChartPoint point) { return IsFinite(point.x) && IsFinite(point.y); }
        internal static bool IsFinite(Vector2 point) { return IsFinite(point.x) && IsFinite(point.y); }
    }
}
