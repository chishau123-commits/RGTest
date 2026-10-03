using System;
using System.Collections.Generic;
using NUnit.Framework;
using RingGame.Core;
using UnityEngine;

namespace RingGame.Tests
{
    // These verify mathematical/runtime contracts, not physical display or touch latency on a device.
    public sealed class CoreTests
    {
        private const string ValidJson = "{\"schemaVersion\":\"prototype-ring-0\",\"timebase\":{\"ppq\":960,\"offsetUs\":0," +
            "\"tempos\":[{\"tick\":0,\"bpm\":120}]},\"settings\":{\"title\":\"Fixture\",\"requiredTouches\":1}," +
            "\"notes\":[{\"id\":\"n\",\"tick\":1920,\"spawnTick\":960,\"motion\":\"shrink\",\"target\":{\"x\":0,\"y\":0},\"radius\":5}]," +
            "\"paths\":[],\"actions\":[],\"decorations\":[]}";
        private static NoteDto Note(string id, float x = 0, float y = 0, float radius = 5)
        {
            return new NoteDto
            {
                id = id, tick = 1920, spawnTick = 960, motion = "shrink",
                target = new ChartPoint(x, y), radius = radius
            };
        }

        private static ChartDocument Document(params NoteDto[] notes)
        {
            return new ChartDocument
            {
                schemaVersion = ChartCompiler.SchemaVersion,
                timebase = new ChartTimebase
                {
                    ppq = ChartCompiler.Ppq, offsetUs = 0,
                    tempos = new[] { new TempoDto { tick = 0, bpm = 120 } }
                },
                settings = new ChartSettings { title = "Core contract test", requiredTouches = Math.Max(notes.Length, 1) },
                notes = notes, paths = new PathDto[0], actions = new CameraActionDto[0], decorations = new DecorationDto[0]
            };
        }

        [Test]
        public void TempoIntegrationCrossesChangesAndAppliesOffsetExactlyOnce()
        {
            var doc = Document(Note("n"));
            doc.timebase.offsetUs = 250000;
            doc.timebase.tempos = new[]
            {
                new TempoDto { tick = 0, bpm = 120 },
                new TempoDto { tick = 960, bpm = 60 },
                new TempoDto { tick = 2880, bpm = 240 }
            };
            var compiled = ChartCompiler.Compile(doc);
            Assert.That(compiled.TimeMap.TickToSeconds(0), Is.EqualTo(.25).Within(1e-10));
            Assert.That(compiled.TimeMap.TickToSeconds(960), Is.EqualTo(.75).Within(1e-10));
            Assert.That(compiled.TimeMap.TickToSeconds(1920), Is.EqualTo(1.75).Within(1e-10));
            Assert.That(compiled.TimeMap.TickToSeconds(3840), Is.EqualTo(3.0).Within(1e-10));
            Assert.That(compiled.Notes[0].SpawnSeconds, Is.EqualTo(.75).Within(1e-10));
            Assert.That(compiled.Notes[0].HitSeconds, Is.EqualTo(1.75).Within(1e-10));
        }

        [Test]
        public void ShrinkEdgesCoincideAtHitAndTailKeepsTarget()
        {
            var note = ChartCompiler.Compile(Document(Note("n", 12, -7))).Notes[0];
            var before = NoteEvaluator.Evaluate(note, .5);
            var hit = NoteEvaluator.Evaluate(note, 1);
            var tail = NoteEvaluator.Evaluate(note, 1.08);
            Assert.That(before.ApproachRadius, Is.EqualTo(note.Radius * 3));
            Assert.That(hit.ApproachRadius, Is.EqualTo(note.Radius));
            Assert.That(hit.MovingCenter, Is.EqualTo(note.Target));
            Assert.That(tail.Visible, Is.True);
            Assert.That(tail.ApproachRadius, Is.EqualTo(note.Radius));
            Assert.That(NoteEvaluator.Evaluate(note, 1.101).Visible, Is.False);
        }

        [Test]
        public void ArrivalDiskInterpolatesToReceivingRingAndNeverAutoJudges()
        {
            var dto = Note("n", 20, 10);
            dto.motion = "arrival";
            dto.pathId = "p";
            var doc = Document(dto);
            doc.paths = new[] { new PathDto { id = "p", type = "linear", start = new ChartPoint(-20, 10), end = dto.target } };
            var chart = ChartCompiler.Compile(doc);
            var note = chart.Notes[0];
            Assert.That(NoteEvaluator.Evaluate(note, .75).MovingCenter, Is.EqualTo(new Vector2(0, 10)));
            Assert.That(NoteEvaluator.Evaluate(note, 1).MovingCenter, Is.EqualTo(note.Target));
            Assert.That(NoteEvaluator.Evaluate(note, 1).MovingRadius, Is.EqualTo(note.Radius));
            var judge = new JudgeEngine(chart);
            Assert.That(judge.GetState("n"), Is.EqualTo(NoteState.Pending));
            Assert.That(judge.ProcessBatch(new[] { new JudgeContact(1, .75, new Vector2(0, 10)) }, 0), Is.Empty);
            Assert.That(judge.GetState("n"), Is.EqualTo(NoteState.Pending));
        }

        [Test]
        public void CameraEvaluationIsAbsoluteAndSupportsSeekAcrossActions()
        {
            var doc = Document(Note("n"));
            doc.actions = new[]
            {
                new CameraActionDto { id = "c1", eventType = "MoveCamera", tick = 960, durationTicks = 960,
                    position = new ChartPoint(10, 20), rotation = 90, scale = 2, ease = "linear" },
                new CameraActionDto { id = "c2", eventType = "MoveCamera", tick = 2880, durationTicks = 960,
                    position = new ChartPoint(-10, 0), rotation = 180, scale = .5f, ease = "smooth" }
            };
            var chart = ChartCompiler.Compile(doc);
            var first = CameraEvaluator.Evaluate(chart, .75);
            CameraEvaluator.Evaluate(chart, 100);
            CameraEvaluator.Evaluate(chart, -.5);
            var again = CameraEvaluator.Evaluate(chart, .75);
            Assert.That(first.Position, Is.EqualTo(new Vector2(5, 10)));
            Assert.That(first.RotationDegrees, Is.EqualTo(45));
            Assert.That(first.Scale, Is.EqualTo(1.5));
            Assert.That(again.Position, Is.EqualTo(first.Position));
            Assert.That(again.RotationDegrees, Is.EqualTo(first.RotationDegrees));
            Assert.That(again.Scale, Is.EqualTo(first.Scale));
            Assert.That(CameraEvaluator.Evaluate(chart, 1.25).Position, Is.EqualTo(new Vector2(10, 20)));
            Assert.That(CameraEvaluator.Evaluate(chart, 1.75).Position, Is.EqualTo(new Vector2(0, 10)));
        }

        [TestCase(0f, 1f)]
        [TestCase(90f, .3f)]
        [TestCase(-35f, 2.5f)]
        [TestCase(720f, 1.7f)]
        public void CameraSimilarityTransformHasCorrectInverse(float rotation, float scale)
        {
            var pose = new CameraPose(new Vector2(-7, 13), rotation, scale);
            var point = new Vector2(24, -15);
            var restored = pose.InverseTransformPoint(pose.TransformPoint(point));
            Assert.That(Vector2.Distance(restored, point), Is.LessThan(.00002));
            Assert.That(Vector2.Distance(pose.TransformPoint(point), pose.TransformPoint(point + Vector2.right)),
                Is.EqualTo(scale).Within(.00002));
        }

        [TestCase(-.035, JudgeGrade.Perfect)]
        [TestCase(.035, JudgeGrade.Perfect)]
        [TestCase(-.035001, JudgeGrade.Great)]
        [TestCase(.035001, JudgeGrade.Great)]
        [TestCase(-.070, JudgeGrade.Great)]
        [TestCase(.070, JudgeGrade.Great)]
        [TestCase(-.070001, JudgeGrade.Good)]
        [TestCase(.070001, JudgeGrade.Good)]
        [TestCase(-.100, JudgeGrade.Good)]
        [TestCase(.100, JudgeGrade.Good)]
        [TestCase(-.100001, JudgeGrade.Miss)]
        [TestCase(.100001, JudgeGrade.Miss)]
        public void TimingWindowsAreSymmetricAndInclusive(double error, JudgeGrade expected)
        {
            Assert.That(JudgeEngine.GradeForError(error), Is.EqualTo(expected));
            var engine = new JudgeEngine(ChartCompiler.Compile(Document(Note("n"))));
            var result = engine.ProcessBatch(new[] { new JudgeContact(1, 1 + error, Vector2.zero) }, 0);
            if (expected == JudgeGrade.Miss) Assert.That(result, Is.Empty);
            else Assert.That(result[0].Grade, Is.EqualTo(expected));
        }

        [Test]
        public void NegativeInputCalibrationDoesNotPrematurelyMissAndIsAppliedOnce()
        {
            var engine = new JudgeEngine(ChartCompiler.Compile(Document(Note("n"))));
            const double calibration = -.04;
            const double processingSongNow = 1.12;
            Assert.That(engine.Expire(processingSongNow + calibration), Is.Empty);
            var results = engine.ProcessBatch(new[] { new JudgeContact(1, 1.04, Vector2.zero) }, calibration);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Grade, Is.EqualTo(JudgeGrade.Perfect));
            Assert.That(results[0].ErrorSeconds, Is.EqualTo(0).Within(1e-10));
        }

        [Test]
        public void QueuedInputsAreJudgedByOriginalTimeBeforeFrameTimeout()
        {
            var engine = new JudgeEngine(ChartCompiler.Compile(Document(Note("n"))));
            var results = engine.ProcessBatch(new[] { new JudgeContact(1, 1.09, Vector2.zero) }, 0);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(engine.Expire(1.3), Is.Empty);
            Assert.That(engine.GetState("n"), Is.EqualTo(NoteState.Hit));
        }

        [Test]
        public void ExpirationIsStrictlyAfterInclusiveTailAndOccursOnce()
        {
            var engine = new JudgeEngine(ChartCompiler.Compile(Document(Note("n"))));
            Assert.That(engine.Expire(1.1), Is.Empty);
            Assert.That(engine.Expire(1.10001).Count, Is.EqualTo(1));
            Assert.That(engine.Expire(10), Is.Empty);
            Assert.That(engine.GetState("n"), Is.EqualTo(NoteState.Miss));
            Assert.That(engine.ProcessBatch(new[] { new JudgeContact(1, 1, Vector2.zero) }, 0), Is.Empty);
        }

        [Test]
        public void MatchingMaximizesChordInsteadOfLosingRestrictedContactToGreedyChoice()
        {
            // Contact 1 can reach both notes and locally prefers a; contact 2 can reach only a.
            var engine = new JudgeEngine(ChartCompiler.Compile(Document(Note("a", 0, 0, 5), Note("b", 6, 0, 5))));
            var results = engine.ProcessBatch(new[]
            {
                new JudgeContact(1, 1, new Vector2(2, 0)),
                new JudgeContact(2, 1, new Vector2(-4, 0))
            }, 0);
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results.Find(result => result.ContactId == 1).NoteId, Is.EqualTo("b"));
            Assert.That(results.Find(result => result.ContactId == 2).NoteId, Is.EqualTo("a"));
        }

        [Test]
        public void EqualCardinalityMatchingMinimizesTotalTimingError()
        {
            var a = Note("a"); a.tick = 10000; a.spawnTick = 9000;
            var b = Note("b"); b.tick = 10160; b.spawnTick = 9000;
            var doc = Document(a, b); doc.timebase.tempos[0].bpm = 125;
            var engine = new JudgeEngine(ChartCompiler.Compile(doc));
            var results = engine.ProcessBatch(new[]
            {
                new JudgeContact(1, 5, Vector2.zero), new JudgeContact(2, 5.010, Vector2.zero)
            }, 0);
            Assert.That(results.Count, Is.EqualTo(2));
            var first = results.Find(result => result.ContactId == 1);
            var second = results.Find(result => result.ContactId == 2);
            Assert.That(first.NoteId, Is.EqualTo("a"));
            Assert.That(first.Grade, Is.EqualTo(JudgeGrade.Perfect));
            Assert.That(second.NoteId, Is.EqualTo("b"));
            Assert.That(second.Grade, Is.EqualTo(JudgeGrade.Great));
        }

        [Test]
        public void EqualTimingMatchingMinimizesNormalizedSpatialDistanceThenUsesStableIds()
        {
            var chart = ChartCompiler.Compile(Document(Note("a", 0, 0, 10), Note("b", 8, 0, 10)));
            var spatial = new JudgeEngine(chart).ProcessBatch(new[]
            {
                new JudgeContact(1, 1, new Vector2(1, 0)), new JudgeContact(2, 1, new Vector2(7, 0))
            }, 0);
            Assert.That(spatial.Find(result => result.ContactId == 1).NoteId, Is.EqualTo("a"));
            Assert.That(spatial.Find(result => result.ContactId == 2).NoteId, Is.EqualTo("b"));
            var tieChart = ChartCompiler.Compile(Document(Note("a"), Note("b")));
            var tied = new JudgeEngine(tieChart).ProcessBatch(new[]
            {
                new JudgeContact(2, 1, Vector2.zero), new JudgeContact(1, 1, Vector2.zero)
            }, 0);
            Assert.That(tied.Find(result => result.ContactId == 1).NoteId, Is.EqualTo("a"));
            Assert.That(tied.Find(result => result.ContactId == 2).NoteId, Is.EqualTo("b"));
        }

        [Test]
        public void BatchReorderingPreservesStableAssignment()
        {
            var chart = ChartCompiler.Compile(Document(Note("a", 0, 0, 5), Note("b", 6, 0, 5)));
            var first = new JudgeContact(1, 1, new Vector2(2, 0));
            var second = new JudgeContact(2, 1, new Vector2(-4, 0));
            var normal = new JudgeEngine(chart).ProcessBatch(new[] { first, second }, 0);
            var reversed = new JudgeEngine(chart).ProcessBatch(new[] { second, first }, 0);
            Assert.That(reversed.Count, Is.EqualTo(normal.Count));
            for (int i = 0; i < normal.Count; i++)
            {
                Assert.That(reversed[i].ContactId, Is.EqualTo(normal[i].ContactId));
                Assert.That(reversed[i].NoteId, Is.EqualTo(normal[i].NoteId));
            }
        }

        [Test]
        public void ContactsAndNotesAreConsumedAtMostOnceEvenAfterAnUnsuccessfulBegin()
        {
            var later = Note("later", 30);
            later.tick = 3840; later.spawnTick = 2880;
            var engine = new JudgeEngine(ChartCompiler.Compile(Document(Note("n"), later)));
            Assert.That(engine.ProcessBatch(new[] { new JudgeContact(1, .5, Vector2.zero) }, 0), Is.Empty);
            Assert.That(engine.ProcessBatch(new[] { new JudgeContact(1, 1, Vector2.zero) }, 0), Is.Empty);
            var contacts = new[] { new JudgeContact(2, 1, Vector2.zero), new JudgeContact(2, 1, Vector2.zero) };
            Assert.That(engine.ProcessBatch(contacts, 0).Count, Is.EqualTo(1));
            Assert.That(engine.ProcessBatch(new[] { new JudgeContact(3, 1, Vector2.zero) }, 0), Is.Empty);
            Assert.That(engine.ProcessBatch(new[] { new JudgeContact(2, 2, new Vector2(30, 0)) }, 0), Is.Empty);
            Assert.That(engine.GetState("later"), Is.EqualTo(NoteState.Pending));
        }

        [Test]
        public void OneContactCannotConsumeTwoOverlappingNotes()
        {
            var engine = new JudgeEngine(ChartCompiler.Compile(Document(Note("a"), Note("b"))));
            Assert.That(engine.ProcessBatch(new[] { new JudgeContact(1, 1, Vector2.zero) }, 0).Count, Is.EqualTo(1));
            Assert.That(engine.GetState("a") == NoteState.Hit ^ engine.GetState("b") == NoteState.Hit, Is.True);
        }

        [Test]
        public void MoreThanFourSimultaneousTouchesHaveNoArtificialCoreLimit()
        {
            var notes = new NoteDto[10];
            var contacts = new List<JudgeContact>();
            for (int i = 0; i < 10; i++)
            {
                notes[i] = Note("n" + i, -63 + 14 * i);
                contacts.Add(new JudgeContact(i, 1, notes[i].target.ToVector2()));
            }
            var chart = ChartCompiler.Compile(Document(notes));
            Assert.That(chart.RequiredTouches, Is.EqualTo(10));
            Assert.That(new JudgeEngine(chart).ProcessBatch(contacts, 0).Count, Is.EqualTo(10));
        }

        [Test]
        public void SpatialBoundaryIsInclusiveAndTargetsRemainInChartSpace()
        {
            var chart = ChartCompiler.Compile(Document(Note("n", 10, 5, 4)));
            var pose = new CameraPose(new Vector2(-20, 10), 70, 1.3f);
            var displayedTarget = pose.TransformPoint(new Vector2(10, 5));
            var restored = pose.InverseTransformPoint(displayedTarget);
            Assert.That(new JudgeEngine(chart).ProcessBatch(new[] { new JudgeContact(1, 1, restored) }, 0).Count, Is.EqualTo(1));
            Assert.That(new JudgeEngine(chart).ProcessBatch(new[] { new JudgeContact(1, 1, new Vector2(14, 5)) }, 0).Count, Is.EqualTo(1));
            Assert.That(new JudgeEngine(chart).ProcessBatch(new[] { new JudgeContact(1, 1, new Vector2(14.001f, 5)) }, 0), Is.Empty);
        }

        [Test]
        public void CompilerRejectsUnsupportedVersionMotionActionEaseAndDecoration()
        {
            var doc = Document(Note("n"));
            doc.schemaVersion = "v1";
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            doc.schemaVersion = ChartCompiler.SchemaVersion;
            doc.notes[0].motion = "hold";
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            doc.notes[0].motion = "shrink";
            doc.actions = new[] { new CameraActionDto { id = "c", eventType = "Shake", tick = 0, scale = 1, ease = "linear" } };
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            doc.actions[0].eventType = "MoveCamera";
            doc.actions[0].ease = "bounce";
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            doc.actions[0].ease = "linear";
            doc.decorations = new[] { new DecorationDto { id = "d" } };
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
        }

        [Test]
        public void CompilerRejectsMissingDataDuplicateIdsInvalidTempoAndUnderstatedChords()
        {
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(null));
            var doc = Document(Note("n"));
            doc.paths = null;
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            doc.paths = new PathDto[0];
            doc.timebase.tempos[0].tick = 1;
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            doc.timebase.tempos[0].tick = 0;
            doc.timebase.tempos[0].bpm = 0;
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            doc.timebase.tempos[0].bpm = 120;
            doc.notes = new[] { Note("n"), Note("n") };
            doc.settings.requiredTouches = 2;
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            doc.notes[1].id = "m";
            doc.settings.requiredTouches = 1;
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
        }

        [Test]
        public void CompilerRejectsBadPathReferenceWrongEndAndZeroLength()
        {
            var note = Note("n"); note.motion = "arrival"; note.pathId = "p";
            var doc = Document(note);
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            doc.paths = new[] { new PathDto { id = "p", type = "linear", start = new ChartPoint(-10, 0), end = new ChartPoint(1, 0) } };
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            doc.paths[0].end = new ChartPoint(0, 0);
            doc.paths[0].start = new ChartPoint(0, 0);
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
        }

        [Test]
        public void CompilerRejectsNoninvertibleNonfiniteAndOverlappingCameras()
        {
            var doc = Document(Note("n"));
            var camera = new CameraActionDto { id = "c", eventType = "MoveCamera", tick = 0, durationTicks = 1920,
                position = new ChartPoint(0, 0), scale = 0, ease = "linear" };
            doc.actions = new[] { camera };
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            camera.scale = float.NaN;
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            camera.scale = 1;
            doc.actions = new[] { camera, new CameraActionDto { id = "c2", eventType = "MoveCamera", tick = 960,
                durationTicks = 960, scale = 1, ease = "linear" } };
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
            doc.actions[1].tick = 0;
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
        }

        [Test]
        public void InvalidInputFailsBeforeConsumingAnyContacts()
        {
            var engine = new JudgeEngine(ChartCompiler.Compile(Document(Note("n"))));
            Assert.Throws<ArgumentException>(() => engine.ProcessBatch(new[]
            {
                new JudgeContact(1, 1, Vector2.zero), new JudgeContact(2, double.NaN, Vector2.zero)
            }, 0));
            Assert.That(engine.ProcessBatch(new[] { new JudgeContact(1, 1, Vector2.zero) }, 0).Count, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.Expire(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => JudgeEngine.GradeForError(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CameraPose(Vector2.zero, 0, 0));
        }

        [Test]
        public void CompilerRejectsTempoTimesThatOverflowDespiteFiniteBpm()
        {
            var doc = Document(Note("n"));
            doc.timebase.tempos[0].bpm = double.Epsilon;
            Assert.Throws<ChartValidationException>(() => ChartCompiler.Compile(doc));
        }

        [Test]
        public void JsonParserAcceptsValidDocumentAndPreservesLegalZeroCoordinates()
        {
            var doc = ChartJson.Parse(ValidJson);
            Assert.That(doc.notes[0].target.ToVector2(), Is.EqualTo(Vector2.zero));
            Assert.That(ChartCompiler.Compile(doc).Notes[0].HitSeconds, Is.EqualTo(1));
        }

        [TestCase("\"target\":{\"x\":0,\"y\":0},", "$.notes[0].target")]
        [TestCase("\"tick\":1920,", "$.notes[0].tick")]
        [TestCase("\"offsetUs\":0,", "$.timebase.offsetUs")]
        public void JsonParserRejectsMissingValueFieldsWithTheirPaths(string missing, string path)
        {
            var exception = Assert.Throws<ChartValidationException>(() => ChartJson.Parse(ValidJson.Replace(missing, "")));
            StringAssert.Contains(path, exception.Message);
        }

        [Test]
        public void JsonParserRejectsUnknownAndDuplicateProperties()
        {
            var unknown = ValidJson.Replace("\"x\":0", "\"hiddenFlag\":true,\"x\":0");
            StringAssert.Contains("$.notes[0].target.hiddenFlag",
                Assert.Throws<ChartValidationException>(() => ChartJson.Parse(unknown)).Message);
            var duplicate = ValidJson.Replace("\"id\":\"n\"", "\"id\":\"n\",\"id\":\"other\"");
            StringAssert.Contains("notes[0].id", Assert.Throws<ChartValidationException>(() => ChartJson.Parse(duplicate)).Message);
        }

        [TestCase("\"paths\":[]", "\"paths\":null", "$.paths")]
        [TestCase("\"ppq\":960", "\"ppq\":\"960\"", "$.timebase.ppq")]
        [TestCase("\"radius\":5", "\"radius\":\"5\"", "$.notes[0].radius")]
        [TestCase("\"tick\":1920", "\"tick\":1920.0", "$.notes[0].tick")]
        [TestCase("\"target\":{\"x\":0,\"y\":0}", "\"target\":[]", "$.notes[0].target")]
        [TestCase("\"offsetUs\":0", "\"offsetUs\":9223372036854775808", "$.timebase.offsetUs")]
        [TestCase("\"radius\":5", "\"radius\":1e39", "$.notes[0].radius")]
        [TestCase("\"bpm\":120", "\"bpm\":1e999", "$.timebase.tempos[0].bpm")]
        public void JsonParserRejectsWrongTypesNullAndNumericOverflow(string source, string replacement, string path)
        {
            var exception = Assert.Throws<ChartValidationException>(() => ChartJson.Parse(ValidJson.Replace(source, replacement)));
            StringAssert.Contains(path, exception.Message);
        }

        [TestCase("{\"notes\":")]
        [TestCase("{}{}")]
        [TestCase("{'schemaVersion':'prototype-ring-0'}")]
        [TestCase("{schemaVersion:\"prototype-ring-0\"}")]
        [TestCase("{\"schemaVersion\":\"prototype-ring-0\",}")]
        [TestCase("{\"x\":01}")]
        [TestCase("{\"x\":NaN}")]
        [TestCase("{/*comment*/}")]
        public void JsonParserRejectsMalformedJsonAndJavaScriptExtensions(string json)
        {
            Assert.Throws<ChartValidationException>(() => ChartJson.Parse(json));
        }

        [Test]
        public void JsonParserEnforcesSizeDepthAndItemLimitsBeforeCompilation()
        {
            Assert.Throws<ChartValidationException>(() => ChartJson.Parse(new string(' ', ChartJson.MaximumUtf8Bytes) + "{}"));
            var deep = new string('[', ChartJson.MaximumDepth + 1) + "0" + new string(']', ChartJson.MaximumDepth + 1);
            Assert.Throws<ChartValidationException>(() => ChartJson.Parse(deep));
            var entries = new string[ChartJson.MaximumActions + 1];
            for (int i = 0; i < entries.Length; i++) entries[i] = "{}";
            var manyActions = ValidJson.Replace("\"actions\":[]", "\"actions\":[" + string.Join(",", entries) + "]");
            StringAssert.Contains("$.actions", Assert.Throws<ChartValidationException>(() => ChartJson.Parse(manyActions)).Message);
        }
    }
}
