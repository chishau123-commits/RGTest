using System;
using System.Collections.Generic;
using UnityEngine;

namespace RingGame.Core
{
    public enum NoteState { Pending, Hit, Miss }
    public enum JudgeGrade { Perfect, Great, Good, Miss }

    public struct JudgeContact
    {
        public long ContactId;
        public double RawSongSeconds;
        // Already inverse-projected through the pose chosen for this contact's original input timestamp.
        public Vector2 ChartPosition;
        public JudgeContact(long contactId, double rawSongSeconds, Vector2 chartPosition)
        { ContactId = contactId; RawSongSeconds = rawSongSeconds; ChartPosition = chartPosition; }
    }

    public struct JudgeResult
    {
        public string NoteId;
        public long ContactId;
        public JudgeGrade Grade;
        public double ErrorSeconds;
        public float SpatialDistance;
    }

    public sealed class JudgeEngine
    {
        public const double PerfectWindowSeconds = 0.035;
        public const double GreatWindowSeconds = 0.070;
        public const double GoodWindowSeconds = 0.100;
        private const double TimeTolerance = 0.000000001;
        private readonly CompiledNote[] notes;
        private readonly NoteState[] states;
        private readonly Dictionary<string, int> noteIndices;
        private readonly HashSet<long> processedContacts = new HashSet<long>();

        public JudgeEngine(CompiledChart chart)
        {
            if (chart == null) throw new ArgumentNullException("chart");
            notes = chart.Notes;
            states = new NoteState[notes.Length];
            noteIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < notes.Length; i++) noteIndices.Add(notes[i].Id, i);
        }

        public NoteState GetState(string noteId)
        {
            int index;
            if (noteId == null || !noteIndices.TryGetValue(noteId, out index))
                throw new ArgumentException("Unknown note id.", "noteId");
            return states[index];
        }

        // Call once for a batch of original Began events BEFORE Expire for the processing frame.
        // A Began is consumed even when it misses; a held finger may never be retried as a new tap.
        public List<JudgeResult> ProcessBatch(IList<JudgeContact> contacts, double calibrationSeconds)
        {
            if (contacts == null) throw new ArgumentNullException("contacts");
            if (!ChartCompiler.IsFinite(calibrationSeconds)) throw new ArgumentOutOfRangeException("calibrationSeconds");
            var unique = new List<JudgeContact>();
            var batchIds = new HashSet<long>();
            for (int i = 0; i < contacts.Count; i++)
            {
                var contact = contacts[i];
                if (contact.ContactId < 0 || !ChartCompiler.IsFinite(contact.RawSongSeconds) ||
                    !ChartCompiler.IsFinite(contact.ChartPosition) ||
                    !ChartCompiler.IsFinite(contact.RawSongSeconds + calibrationSeconds))
                    throw new ArgumentException("Contacts require a nonnegative id and finite time/position.", "contacts");
                if (!processedContacts.Contains(contact.ContactId) && batchIds.Add(contact.ContactId)) unique.Add(contact);
            }
            unique.Sort((left, right) =>
            {
                int time = left.RawSongSeconds.CompareTo(right.RawSongSeconds);
                return time != 0 ? time : left.ContactId.CompareTo(right.ContactId);
            });
            var edges = new List<int>[unique.Count];
            for (int i = 0; i < unique.Count; i++)
            {
                var contact = unique[i];
                double judgeTime = contact.RawSongSeconds + calibrationSeconds;
                edges[i] = new List<int>();
                for (int j = 0; j < notes.Length; j++)
                {
                    if (states[j] != NoteState.Pending) continue;
                    double error = Math.Abs(judgeTime - notes[j].HitSeconds);
                    if (error > GoodWindowSeconds + TimeTolerance) continue;
                    if (DistanceSquared(contact.ChartPosition, notes[j].Target) > (double)notes[j].Radius * notes[j].Radius) continue;
                    edges[i].Add(j);
                }
                edges[i].Sort((left, right) => CompareCandidate(left, right, contact, judgeTime));
            }
            var noteByContact = OptimalAssignment(unique, edges, calibrationSeconds);
            var results = new List<JudgeResult>();
            for (int i = 0; i < unique.Count; i++)
            {
                processedContacts.Add(unique[i].ContactId);
                int noteIndex = noteByContact[i];
                if (noteIndex < 0) continue;
                states[noteIndex] = NoteState.Hit;
                double error = unique[i].RawSongSeconds + calibrationSeconds - notes[noteIndex].HitSeconds;
                results.Add(new JudgeResult
                {
                    NoteId = notes[noteIndex].Id, ContactId = unique[i].ContactId,
                    Grade = GradeForError(error), ErrorSeconds = error,
                    SpatialDistance = (float)Math.Sqrt(DistanceSquared(unique[i].ChartPosition, notes[noteIndex].Target))
                });
            }
            return results;
        }

        // judgeNow must be songNow + input calibration. Raw songNow would expire early under negative calibration.
        public List<JudgeResult> Expire(double judgeNow)
        {
            if (!ChartCompiler.IsFinite(judgeNow)) throw new ArgumentOutOfRangeException("judgeNow");
            var results = new List<JudgeResult>();
            for (int i = 0; i < notes.Length; i++)
            {
                if (states[i] != NoteState.Pending || judgeNow <= notes[i].HitSeconds + GoodWindowSeconds + TimeTolerance)
                    continue;
                states[i] = NoteState.Miss;
                results.Add(new JudgeResult
                {
                    NoteId = notes[i].Id, ContactId = -1, Grade = JudgeGrade.Miss,
                    ErrorSeconds = judgeNow - notes[i].HitSeconds, SpatialDistance = float.NaN
                });
            }
            return results;
        }

        public static JudgeGrade GradeForError(double errorSeconds)
        {
            if (!ChartCompiler.IsFinite(errorSeconds)) throw new ArgumentOutOfRangeException("errorSeconds");
            double absolute = Math.Abs(errorSeconds);
            if (absolute <= PerfectWindowSeconds + TimeTolerance) return JudgeGrade.Perfect;
            if (absolute <= GreatWindowSeconds + TimeTolerance) return JudgeGrade.Great;
            if (absolute <= GoodWindowSeconds + TimeTolerance) return JudgeGrade.Good;
            return JudgeGrade.Miss;
        }

        private int CompareCandidate(int left, int right, JudgeContact contact, double judgeTime)
        {
            int time = Math.Abs(judgeTime - notes[left].HitSeconds).CompareTo(Math.Abs(judgeTime - notes[right].HitSeconds));
            if (time != 0) return time;
            int distance = DistanceSquared(contact.ChartPosition, notes[left].Target).CompareTo(
                DistanceSquared(contact.ChartPosition, notes[right].Target));
            return distance != 0 ? distance : StringComparer.Ordinal.Compare(notes[left].Id, notes[right].Id);
        }

        // Successive shortest residual paths maximize count first. Their lexicographic costs then minimize
        // summed |timing error|, summed distance/radius, and stable contact/id ranks in that order.
        private int[] OptimalAssignment(List<JudgeContact> contacts, List<int>[] candidates, double calibration)
        {
            var result = new int[contacts.Count];
            for (int i = 0; i < result.Length; i++) result[i] = -1;
            if (contacts.Count == 0) return result;
            var activeNotes = new List<int>();
            var seen = new HashSet<int>();
            foreach (var contactCandidates in candidates)
                foreach (int note in contactCandidates)
                    if (seen.Add(note)) activeNotes.Add(note);
            if (activeNotes.Count == 0) return result;
            activeNotes.Sort((left, right) => StringComparer.Ordinal.Compare(notes[left].Id, notes[right].Id));
            var rankByNote = new Dictionary<int, int>();
            for (int i = 0; i < activeNotes.Count; i++) rankByNote.Add(activeNotes[i], i);

            const int source = 0;
            int noteStart = 1 + contacts.Count;
            int sink = noteStart + activeNotes.Count;
            var graph = new List<FlowEdge>[sink + 1];
            for (int i = 0; i < graph.Length; i++) graph[i] = new List<FlowEdge>();
            var assignments = new List<AssignmentEdge>();
            for (int contact = 0; contact < contacts.Count; contact++)
            {
                AddEdge(graph, source, contact + 1, new FlowCost());
                foreach (int note in candidates[contact])
                {
                    int rank = rankByNote[note];
                    long stable = contact * ((long)activeNotes.Count + 1) * (contacts.Count + 1L) +
                        rank * (contacts.Count - (long)contact);
                    var cost = new FlowCost
                    {
                        Time = Math.Abs(contacts[contact].RawSongSeconds + calibration - notes[note].HitSeconds),
                        Spatial = Math.Sqrt(DistanceSquared(contacts[contact].ChartPosition, notes[note].Target)) / notes[note].Radius,
                        Stable = stable
                    };
                    var edge = AddEdge(graph, contact + 1, noteStart + rank, cost);
                    assignments.Add(new AssignmentEdge { Contact = contact, Note = note, Edge = edge });
                }
            }
            for (int rank = 0; rank < activeNotes.Count; rank++)
                AddEdge(graph, noteStart + rank, sink, new FlowCost());

            while (AugmentShortestPath(graph, source, sink)) { }
            foreach (var assignment in assignments)
                if (assignment.Edge.Capacity == 0) result[assignment.Contact] = assignment.Note;
            return result;
        }

        private sealed class FlowEdge
        {
            public int To;
            public int Reverse;
            public int Capacity;
            public FlowCost Cost;
        }

        private struct AssignmentEdge
        {
            public int Contact;
            public int Note;
            public FlowEdge Edge;
        }

        private struct FlowCost
        {
            public double Time;
            public double Spatial;
            public long Stable;
            public FlowCost Add(FlowCost other)
            {
                return new FlowCost { Time = Time + other.Time, Spatial = Spatial + other.Spatial, Stable = Stable + other.Stable };
            }
            public FlowCost Negated()
            {
                return new FlowCost { Time = -Time, Spatial = -Spatial, Stable = -Stable };
            }
            public bool LessThan(FlowCost other)
            {
                const double tolerance = 1e-12;
                if (Math.Abs(Time - other.Time) > tolerance) return Time < other.Time;
                if (Math.Abs(Spatial - other.Spatial) > tolerance) return Spatial < other.Spatial;
                return Stable < other.Stable;
            }
        }

        private static FlowEdge AddEdge(List<FlowEdge>[] graph, int from, int to, FlowCost cost)
        {
            var forward = new FlowEdge { To = to, Reverse = graph[to].Count, Capacity = 1, Cost = cost };
            var reverse = new FlowEdge { To = from, Reverse = graph[from].Count, Capacity = 0, Cost = cost.Negated() };
            graph[from].Add(forward);
            graph[to].Add(reverse);
            return forward;
        }

        private static bool AugmentShortestPath(List<FlowEdge>[] graph, int source, int sink)
        {
            var distances = new FlowCost[graph.Length];
            var reachable = new bool[graph.Length];
            var previousNode = new int[graph.Length];
            var previousEdge = new int[graph.Length];
            reachable[source] = true;
            // Reverse edges can have negative cost, so Bellman-Ford is used instead of greedy/Dijkstra.
            for (int iteration = 0; iteration < graph.Length - 1; iteration++)
            {
                bool changed = false;
                for (int node = 0; node < graph.Length; node++)
                {
                    if (!reachable[node]) continue;
                    for (int edgeIndex = 0; edgeIndex < graph[node].Count; edgeIndex++)
                    {
                        var edge = graph[node][edgeIndex];
                        if (edge.Capacity == 0) continue;
                        var candidate = distances[node].Add(edge.Cost);
                        if (reachable[edge.To] && !candidate.LessThan(distances[edge.To])) continue;
                        reachable[edge.To] = true;
                        distances[edge.To] = candidate;
                        previousNode[edge.To] = node;
                        previousEdge[edge.To] = edgeIndex;
                        changed = true;
                    }
                }
                if (!changed) break;
            }
            if (!reachable[sink]) return false;
            int current = sink;
            while (current != source)
            {
                int previous = previousNode[current];
                var edge = graph[previous][previousEdge[current]];
                edge.Capacity--;
                graph[current][edge.Reverse].Capacity++;
                current = previous;
            }
            return true;
        }

        private static double DistanceSquared(Vector2 left, Vector2 right)
        {
            double x = (double)left.x - right.x;
            double y = (double)left.y - right.y;
            return x * x + y * y;
        }
    }
}
