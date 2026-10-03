using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace RingGame.Runtime
{
    /// <summary>A clock bridge captured once per playback epoch. Score calibration is applied later.</summary>
    public readonly struct InputSongTimeAnchor
    {
        public readonly double InputTime;
        public readonly double SongTime;

        public InputSongTimeAnchor(double inputTime, double songTime)
        {
            InputTime = inputTime;
            SongTime = songTime;
        }

        public double ToSongSeconds(double eventInputTime)
        {
            return SongTime + eventInputTime - InputTime;
        }
    }

    /// <summary>
    /// Scheduled DSP playback is the musical clock. Input events retain their original timestamp,
    /// rather than acquiring the Update frame's time. A bridge assumes equal clock rates within
    /// one short song; physical input, display and audio delays still require device measurement.
    /// </summary>
    public sealed class SongClock
    {
        private readonly AudioSource source;
        private readonly double scheduleLeadSeconds;
        private AudioClip clip;
        private double scheduledDspTime;
        private double songAtScheduledStart;
        private double pausedSongTime;
        private int pausedSample;
        private bool running;
        private bool holdDuringResumeLead;
        private InputSongTimeAnchor inputAnchor;

        public SongClock(AudioSource source, double scheduleLeadSeconds = 0.5)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!IsFinite(scheduleLeadSeconds) || scheduleLeadSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(scheduleLeadSeconds));
            this.source = source;
            this.source.playOnAwake = false;
            this.scheduleLeadSeconds = scheduleLeadSeconds;
        }

        public int Epoch { get; private set; }
        public bool IsRunning => running;
        public static double InputTimeNow => InputState.currentTime;
        public double ScheduledDspTime => scheduledDspTime;
        public int PausedSample => pausedSample;

        public double NowSeconds
        {
            get
            {
                if (!running) return pausedSongTime;
                double value = songAtScheduledStart + AudioSettings.dspTime - scheduledDspTime;
                return holdDuringResumeLead ? Math.Max(songAtScheduledStart, value) : value;
            }
        }

        public void Begin(AudioClip audioClip)
        {
            if (audioClip == null) throw new ArgumentNullException(nameof(audioClip));
            clip = audioClip;
            source.Stop();
            source.clip = clip;
            source.loop = false;
            source.pitch = 1f;
            source.timeSamples = 0;
            pausedSample = 0;
            pausedSongTime = -scheduleLeadSeconds;
            songAtScheduledStart = 0;
            holdDuringResumeLead = false;
            Schedule(scheduleLeadSeconds);
        }

        public void Pause()
        {
            if (!running) return;
            double songTime = NowSeconds;
            source.Pause();
            if (songTime >= clip.length)
            {
                // Natural audio completion resets timeSamples on some backends. Preserve DSP time
                // while the final calibrated judgement tail finishes, rather than jumping to zero.
                pausedSample = clip.samples - 1;
                pausedSongTime = songTime;
                running = false;
                Epoch++;
                return;
            }
            // Save the actual audio sample position, not a rounded frame timestamp.
            pausedSample = Mathf.Clamp(source.timeSamples, 0, Math.Max(0, clip.samples - 1));
            pausedSongTime = songTime < 0 ? songTime : (double)pausedSample / clip.frequency;
            running = false;
            Epoch++;
        }

        public void Resume()
        {
            if (running || clip == null) return;
            source.Stop();
            source.clip = clip;
            source.timeSamples = pausedSample;
            if (pausedSongTime < 0)
            {
                // Pausing the initial count-in preserves its remaining duration.
                songAtScheduledStart = 0;
                holdDuringResumeLead = false;
                Schedule(-pausedSongTime);
            }
            else
            {
                songAtScheduledStart = pausedSongTime >= clip.length ? pausedSongTime : (double)pausedSample / clip.frequency;
                holdDuringResumeLead = true;
                Schedule(scheduleLeadSeconds);
            }
        }

        public void Restart()
        {
            if (clip != null) Begin(clip);
        }

        public double ToSongSeconds(double eventInputTime)
        {
            double result = inputAnchor.ToSongSeconds(eventInputTime);
            return holdDuringResumeLead ? Math.Max(songAtScheduledStart, result) : result;
        }

        /// <summary>Do not reinterpret events from before a restart or the paused resume lead.</summary>
        public bool CanCaptureAt(double eventInputTime)
        {
            if (!running || !IsFinite(eventInputTime) || eventInputTime < inputAnchor.InputTime)
                return false;
            return !holdDuringResumeLead || inputAnchor.ToSongSeconds(eventInputTime) >= songAtScheduledStart;
        }

        private void Schedule(double leadSeconds)
        {
            // Pair input time with the midpoint of two adjacent DSP reads.
            double dspBefore = AudioSettings.dspTime;
            double inputNow = InputState.currentTime;
            double dspAfter = AudioSettings.dspTime;
            double pairedDsp = (dspBefore + dspAfter) * 0.5;
            scheduledDspTime = dspAfter + leadSeconds;
            inputAnchor = new InputSongTimeAnchor(inputNow,
                songAtScheduledStart + pairedDsp - scheduledDspTime);
            source.PlayScheduled(scheduledDspTime);
            running = true;
            Epoch++;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    [Serializable]
    public struct CapturedTouch
    {
        public long ContactId;
        public int FingerId;
        public double InputTime;
        public double RawSongSeconds;
        public Vector2 ScreenPosition;
        public int Epoch;
        public bool EligibleForGameplay;
        public string Source;
    }

    /// <summary>One immutable copy per contact Began. Move, hold and Ended never enter the score queue.</summary>
    public sealed class InputCollector : IDisposable
    {
        private readonly SongClock clock;
        private readonly Queue<CapturedTouch> pending = new Queue<CapturedTouch>();
        private readonly HashSet<long> activeFingers = new HashSet<long>();
        private bool enabled;
        private long nextContactId;

        public InputCollector(SongClock clock)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            RefreshDeviceSlots();
        }

        public int MaxConcurrentObserved { get; private set; }
        // This is the Input System stream capacity, not a measured physical finger limit.
        public int DeviceTouchSlots { get; private set; }
        public int PendingCount => pending.Count;

        public void Enable()
        {
            if (enabled) return;
            EnhancedTouchSupport.Enable();
            EnhancedTouch.onFingerDown += OnFingerDown;
            EnhancedTouch.onFingerUp += OnFingerUp;
            InputSystem.onDeviceChange += OnDeviceChange;
            enabled = true;
            RefreshDeviceSlots();
        }

        public void Disable()
        {
            if (!enabled) return;
            EnhancedTouch.onFingerDown -= OnFingerDown;
            EnhancedTouch.onFingerUp -= OnFingerUp;
            InputSystem.onDeviceChange -= OnDeviceChange;
            EnhancedTouchSupport.Disable();
            enabled = false;
            activeFingers.Clear();
            Clear();
        }

        /// <summary>Append captured events and empty the queue; the caller owns its destination list.</summary>
        public void Drain(List<CapturedTouch> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            while (pending.Count > 0) destination.Add(pending.Dequeue());
        }

        public void Clear() => pending.Clear();

        /// <summary>Desktop debug entry point. The caller must not also simulate the same mouse as touch.</summary>
        public bool CapturePointerDown(int fingerId, double inputTime, Vector2 position, string source = "mouse")
        {
            bool eligible = clock.CanCaptureAt(inputTime);
            pending.Enqueue(new CapturedTouch
            {
                ContactId = ++nextContactId,
                FingerId = fingerId,
                InputTime = inputTime,
                RawSongSeconds = eligible ? clock.ToSongSeconds(inputTime) : clock.NowSeconds,
                ScreenPosition = position,
                Epoch = clock.Epoch,
                EligibleForGameplay = eligible,
                Source = source ?? "unknown"
            });
            return true;
        }

        public void Dispose() => Disable();

        private void OnFingerDown(Finger finger)
        {
            activeFingers.Add(FingerKey(finger));
            MaxConcurrentObserved = Math.Max(MaxConcurrentObserved, activeFingers.Count);
            var touch = finger.currentTouch;
            if (!touch.valid) return;
            // Copy now: EnhancedTouch records refer to a reusable native history buffer.
            CapturePointerDown(finger.index, touch.startTime, touch.startScreenPosition, "touch");
        }

        private void OnFingerUp(Finger finger) => activeFingers.Remove(FingerKey(finger));

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Touchscreen)) return;
            RefreshDeviceSlots();
            if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
                activeFingers.RemoveWhere(key => (int)(key >> 32) == device.deviceId);
        }

        private void RefreshDeviceSlots()
        {
            int slots = 0;
            foreach (var device in InputSystem.devices)
                if (device is Touchscreen screen) slots += screen.touches.Count;
            DeviceTouchSlots = slots;
        }

        private static long FingerKey(Finger finger)
        {
            return ((long)finger.screen.deviceId << 32) | (uint)finger.index;
        }
    }
}
