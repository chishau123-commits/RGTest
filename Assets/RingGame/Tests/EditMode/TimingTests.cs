using NUnit.Framework;
using System.Collections.Generic;
using RingGame.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RingGame.Tests
{
    public sealed class TimingTests
    {
        [Test]
        public void OriginalInputTimeDoesNotChangeWhenProcessingIsDelayed()
        {
            var bridge = new InputSongTimeAnchor(100, -0.5);
            double originalEventTime = 103.5;
            double immediate = bridge.ToSongSeconds(originalEventTime);
            // No processing-frame timestamp is an argument to this bridge.
            double afterAStalledFrame = bridge.ToSongSeconds(originalEventTime);
            Assert.That(immediate, Is.EqualTo(3).Within(1e-9));
            Assert.That(afterAStalledFrame, Is.EqualTo(immediate));
        }

        [Test]
        public void ScoreCalibrationDoesNotMoveTheSpatialPoseSelection()
        {
            var history = new PoseHistory();
            history.Add(Pose(1, 10.00));
            history.Add(Pose(2, 10.02));
            var bridge = new InputSongTimeAnchor(0, 0);
            double eventTime = 10.025;
            double calibratedScoreTime = bridge.ToSongSeconds(eventTime) - 0.08;
            Assert.That(calibratedScoreTime, Is.EqualTo(9.945).Within(1e-9));
            Assert.That(history.TrySelect(eventTime, 0.020, out var pose), Is.True);
            Assert.That(pose.FrameId, Is.EqualTo(1));
        }

        [Test]
        public void DisplayLatencyIsAppliedOnceToTheOriginalEventTimestamp()
        {
            var history = new PoseHistory();
            history.Add(Pose(1, 1.00));
            history.Add(Pose(2, 1.02));
            history.Add(Pose(3, 1.04));
            Assert.That(history.TrySelect(1.05, 0.020, out var pose), Is.True);
            Assert.That(pose.FrameId, Is.EqualTo(2));
        }

        [Test]
        public void MissingFutureAndExpiredPosesAreRejected()
        {
            var history = new PoseHistory(0.5);
            Assert.That(history.TrySelect(1, 0, out _), Is.False);
            history.Add(Pose(1, 1));
            Assert.That(history.TrySelect(0.999, 0, out _), Is.False);
            Assert.That(history.TrySelect(1.501, 0, out _), Is.False);
            history.Add(Pose(2, 1.51));
            Assert.That(history.TrySelect(1, 0, out _), Is.False);
            Assert.That(history.TrySelect(1.51, -0.01, out _), Is.False);
        }

        [Test]
        public void StoredAndReturnedPoseCopiesCannotBeChangedByTheCaller()
        {
            var history = new PoseHistory();
            var original = Pose(1, 1);
            history.Add(original);
            original.FrameId = 99;
            Assert.That(history.TrySelect(1, 0, out var selected), Is.True);
            Assert.That(selected.FrameId, Is.EqualTo(1));
            selected.FrameId = 77;
            history.TrySelect(1, 0, out var again);
            Assert.That(again.FrameId, Is.EqualTo(1));
        }

        [Test]
        public void HistoricalInverseUsesTheVisibleTransformAndRejectsOutsideViewport()
        {
            var pose = Pose(1, 1);
            pose.ChartToScreen = Matrix4x4.TRS(new Vector3(100, 50, 0),
                Quaternion.Euler(0, 0, 90), new Vector3(20, 20, 1));
            Vector3 displayed = pose.ChartToScreen.MultiplyPoint3x4(new Vector3(2, 1, 0));
            Assert.That(pose.TryScreenToChart(displayed, out var chartPosition), Is.True);
            Assert.That(chartPosition.x, Is.EqualTo(2).Within(1e-5));
            Assert.That(chartPosition.y, Is.EqualTo(1).Within(1e-5));
            Assert.That(pose.TryScreenToChart(new Vector2(-1, 100), out _), Is.False);
            pose.ChartToScreen = Matrix4x4.zero;
            Assert.That(pose.TryScreenToChart(new Vector2(10, 10), out _), Is.False);
        }

        private static PoseSnapshot Pose(long frame, double inputTime)
        {
            return new PoseSnapshot
            {
                FrameId = frame,
                SubmittedInputTime = inputTime,
                VisualSongSeconds = inputTime,
                Viewport = new Rect(0, 0, 1920, 1080),
                ChartToScreen = Matrix4x4.identity
            };
        }
    }

    public sealed class ContactCaptureTests
    {
        private GameObject audioObject;
        private Touchscreen touchscreen;
        private InputCollector collector;
        private readonly List<CapturedTouch> captured = new List<CapturedTouch>();

        [SetUp]
        public void SetUp()
        {
            audioObject = new GameObject("ContactCaptureTests audio");
            var clock = new SongClock(audioObject.AddComponent<AudioSource>());
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            collector = new InputCollector(clock);
            collector.Enable();
            captured.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            collector.Dispose();
            InputSystem.RemoveDevice(touchscreen);
            Object.DestroyImmediate(audioObject);
        }

        [Test]
        public void ReadyUiStillReceivesDownWhenSongIsNotRunning()
        {
            Send(1, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(100, 200));
            InputSystem.Update();
            collector.Drain(captured);
            Assert.That(captured.Count, Is.EqualTo(1));
            Assert.That(captured[0].EligibleForGameplay, Is.False);
            Assert.That(captured[0].ScreenPosition, Is.EqualTo(new Vector2(100, 200)));
        }

        [Test]
        public void ShortDownUpAndReusedPlatformIdProduceTwoDifferentContacts()
        {
            double startTime = SongClock.InputTimeNow;
            Send(42, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(100, 200), startTime);
            Send(42, UnityEngine.InputSystem.TouchPhase.Ended, new Vector2(100, 200), startTime + 0.001);
            Send(42, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(300, 400), startTime + 0.002);
            Send(42, UnityEngine.InputSystem.TouchPhase.Ended, new Vector2(300, 400), startTime + 0.003);
            InputSystem.Update();
            collector.Drain(captured);
            Assert.That(captured.Count, Is.EqualTo(2));
            Assert.That(captured[0].ContactId, Is.Not.EqualTo(captured[1].ContactId));
            Assert.That(captured[0].InputTime, Is.EqualTo(startTime).Within(1e-7));
            Assert.That(captured[1].InputTime, Is.EqualTo(startTime + 0.002).Within(1e-7));
            Assert.That(captured[0].ScreenPosition, Is.EqualTo(new Vector2(100, 200)));
            Assert.That(captured[1].ScreenPosition, Is.EqualTo(new Vector2(300, 400)));
        }

        [Test]
        public void MovingHeldAndEndingContactsDoNotCreateAnotherScoreEvent()
        {
            Send(7, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(100, 200));
            InputSystem.Update();
            collector.Drain(captured);
            Assert.That(captured.Count, Is.EqualTo(1));
            captured.Clear();
            Send(7, UnityEngine.InputSystem.TouchPhase.Moved, new Vector2(300, 400));
            Send(7, UnityEngine.InputSystem.TouchPhase.Stationary, new Vector2(300, 400));
            Send(7, UnityEngine.InputSystem.TouchPhase.Ended, new Vector2(300, 400));
            InputSystem.Update();
            collector.Drain(captured);
            Assert.That(captured, Is.Empty);
        }

        [Test]
        public void SyntheticStreamSupportsSixConcurrentContactsWithoutFourFingerCap()
        {
            // Synthetic capacity is deliberately distinguished from a real device hardware measurement.
            Assert.That(touchscreen.touches.Count, Is.GreaterThanOrEqualTo(6));
            for (int index = 0; index < 6; index++)
                Send(index + 1, UnityEngine.InputSystem.TouchPhase.Began, new Vector2(100 + index * 100, 200));
            InputSystem.Update();
            collector.Drain(captured);
            Assert.That(captured.Count, Is.EqualTo(6));
            Assert.That(collector.MaxConcurrentObserved, Is.EqualTo(6));
            Assert.That(collector.DeviceTouchSlots, Is.GreaterThanOrEqualTo(6));
        }

        private void Send(int touchId, UnityEngine.InputSystem.TouchPhase phase, Vector2 position, double time = -1)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = touchId,
                phase = phase,
                position = position
            }, time);
        }
    }
}
