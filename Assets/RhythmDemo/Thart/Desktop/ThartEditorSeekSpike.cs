using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Video;

namespace GeometryRhythm.Thart.Editor
{
    // Spike A from Docs/VideoBgaBranchingPlan.md 鎼?. It answers exactly one question:
    // when a branch has to switch to another segment of the same file, how long does
    // the picture take to get there, and does it flash black on the way?
    //
    // It drives the real VideoBgaRuntime the game uses rather than a stand-in, and it
    // measures seeking *while playing* as well as while paused, because a branch switch
    // happens mid-song. Numbers only: the pass/fail gates here are the ones that are
    // unambiguous (landed, no black frame, deterministic), and the verdict line carries
    // the judgement so it can be argued with.
    public sealed partial class ThartEditorController
    {
        sealed class SeekSample
        {
            public string Kind, Label;
            public double Target;
            public bool KeyframeAligned, Playing, Landed, TimedOut, SeekIssued;
            public double Milliseconds, FinalTime;
            public long FinalFrame, ExpectedFrame;
            public int Frames, BlackFrames, StaleFrames, DistinctFrames, SeeksIssued;
            public float MinLuminance, FinalLuminance, GoldenDelta;
            public bool GoldenCompared;
            public ulong Fingerprint;
            // Decoder-level probe only: how long until the codec acknowledged the seek, as
            // opposed to Milliseconds, which is when the target frame was actually ready.
            public double DecoderAckMilliseconds;
        }

        // A frame this dark is what the issue tracker calls "flickers to black". The BGA
        // under test is a bright garden scene, so anything near zero is a real blackout
        // rather than legitimate dark footage.
        const float SeekSpikeBlackLuminance = .05f;
        const double SeekSpikeLandTolerance = .12;
        const double SeekSpikeTimeout = 8;

        // Decoder-level probe state. The runtime coalesces seeks behind an 80 ms grace window
        // (VideoBgaRuntime.cs:55), which masks whatever the decoder actually costs, so this
        // probe drives a bare VideoPlayer with the same settings and no coalescing at all.
        VideoPlayer decoderProbe;
        double decoderSeekCompletedSeconds = -1, decoderReadySeconds = -1, decoderStartedSeconds = -1;
        long decoderExpectedFrame = -1, decoderReadyFrame = -1;

        IEnumerator RunVideoSeekSpike(string source)
        {
            var report = new List<string>();
            var samples = new List<SeekSample>();
            bool passed = true;
            string[] args = Environment.GetCommandLineArgs();
            int repeats = Mathf.Clamp(SeekSpikeArgumentInt(args, "-thartSeekSpikeRepeats", 3), 1, 10);
            string framesDirectory = SeekSpikeArgument(args, "-thartSeekSpikeFrames");

            report.Add("VIDEO=" + source);
            report.Add("REPEATS=" + repeats + " FRAMES=" + (string.IsNullOrEmpty(framesDirectory) ? "(none)" : framesDirectory));

            yield return PrepareVideoBgaForProbe(source);
            if (videoBga == null || !videoBga.IsLoaded)
            {
                report.Add("FAIL video package did not load: " + videoBgaProbeError);
                WriteSeekSpike(report, samples, false, "NO_VIDEO");
                yield break;
            }

            double duration = videoBga.Package.durationSeconds;
            double fps = Math.Max(1, videoBga.Package.fps);
            report.Add(string.Format(CultureInfo.InvariantCulture,
                "DETAIL duration={0:0.000}s fps={1:0.000} frames={2} seeks={3}/{4}",
                duration, fps, videoBga.Frame < 0 ? -1 : (long)Math.Round(duration * fps),
                videoBga.SeekCount, videoBga.CompletedSeekCount));

            var texture = RenderTexture.GetTemporary(64, 36, 0, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(64, 36, TextureFormat.RGB24, false);
            try
            {
                // Warm the decoder before anything is timed: the first seek of a session
                // pays one-off costs that a branch switch never pays.
                double warm = duration * .5;
                yield return WaitForProbePosition(0);
                yield return WaitForProbePosition(warm);

                // Targets come from the encoder's real keyframe grid, handed in by ffprobe.
                // Guessing the grid from a round number is how you end up measuring a
                // keyframe while believing you measured a segment header: this package's
                // grid is ~10 frames, not the 15 the import command asks for, because the
                // seam chain's cross dissolves force keyframes of their own.
                var keyframes = LoadKeyframes(SeekSpikeArgument(args, "-thartSeekSpikeKeyframes"));
                report.Add("KEYFRAMES=" + keyframes.Count +
                    (keyframes.Count == 0 ? " (none supplied: alignment labels unavailable)" : string.Empty));
                var headers = PickKeyframeTargets(keyframes, duration, 3);
                report.Add("HEADERS=" + string.Join(", ", headers.ConvertAll(t => t.ToString("0.000", CultureInfo.InvariantCulture))));
                var inside = headers.ConvertAll(t => t + SeekSpikeInsideOffset(keyframes, t, duration));
                report.Add("OFFKEYFRAME=" + string.Join(", ", inside.ConvertAll(t => t.ToString("0.000", CultureInfo.InvariantCulture))));

                foreach (double target in headers)
                    yield return MeasureSeek("segment-header-forward", target, keyframes.Count > 0, false, repeats, samples, texture, readback, framesDirectory, fps);
                foreach (double target in inside)
                    yield return MeasureSeek("off-keyframe-forward", target, false, false, repeats, samples, texture, readback, framesDirectory, fps);
                // MeasureSeek parks in the far half when the target sits in the near half, so
                // these two are backward seeks by construction.
                for (int i = 0; i < headers.Count && i < 2; i++)
                    yield return MeasureSeek("backward", headers[i], keyframes.Count > 0, false, repeats, samples, texture, readback, framesDirectory, fps);
                // The decision-relevant case: switching arms mid-song, which is always a
                // backward seek in practice because the arm starts behind the playhead.
                for (int i = 0; i < headers.Count && i < 2; i++)
                    yield return MeasureSeek("playing-switch-backward", headers[i], keyframes.Count > 0, true, repeats, samples, texture, readback, framesDirectory, fps);
            }
            finally
            {
                
                RenderTexture.ReleaseTemporary(texture);
                Destroy(readback);
            }

            // --- gates -------------------------------------------------------------
            int blackTotal = 0, timedOut = 0, unlanded = 0, noSeek = 0;
            foreach (var s in samples)
            {
                blackTotal += s.BlackFrames;
                if (s.TimedOut) timedOut++;
                else if (!s.Landed) unlanded++;
                if (!s.SeekIssued) noSeek++;
            }
            passed &= blackTotal == 0;
            passed &= timedOut == 0 && unlanded == 0;

            // Determinism: the same paused target twice must decode to the same pixels.
            var byTarget = new Dictionary<string, HashSet<ulong>>();
            foreach (var s in samples)
            {
                if (s.Playing || !s.Landed || s.Fingerprint == 0) continue;
                string key = s.Target.ToString("0.000", CultureInfo.InvariantCulture);
                if (!byTarget.TryGetValue(key, out var set)) byTarget[key] = set = new HashSet<ulong>();
                set.Add(s.Fingerprint);
            }
            int nondeterministic = 0;
            foreach (var pair in byTarget) if (pair.Value.Count > 1) nondeterministic++;
            passed &= nondeterministic == 0;

            foreach (var s in samples)
            {
                report.Add(string.Format(CultureInfo.InvariantCulture,
                    "SAMPLE {0,-14} t={1,7:0.000} {2} {3} seek={4} ms={5,7:0.0} frames={6,3} black={7} stale={8} distinct={9} final={10:0.000} playerFrame={11} expected={12} minLum={13:0.000} finalLum={14:0.000}{15}{16}",
                    s.Kind, s.Target, s.KeyframeAligned ? "key  " : "off  ", s.Playing ? "play " : "pause",
                    s.SeekIssued ? "yes" : "NO ", s.Milliseconds, s.Frames, s.BlackFrames, s.StaleFrames, s.DistinctFrames,
                    s.FinalTime, s.FinalFrame, s.ExpectedFrame, s.MinLuminance, s.FinalLuminance,
                    s.GoldenCompared ? " goldenDelta=" + s.GoldenDelta.ToString("0.0000", CultureInfo.InvariantCulture) : string.Empty,
                    s.TimedOut ? " TIMEOUT" : s.Landed ? string.Empty : " NOT_LANDED"));
            }

            report.Add(string.Format(CultureInfo.InvariantCulture,
                "SUMMARY samples={0} blackFrames={1} timedOut={2} unlanded={3} noSeekIssued={4} nondeterministicTargets={5}",
                samples.Count, blackTotal, timedOut, unlanded, noSeek, nondeterministic));

            string verdict = SeekSpikeVerdict(samples, ref report);
            WriteSeekSpike(report, samples, passed, verdict);
        }

        IEnumerator MeasureSeek(string kind, double target, bool keyframeAligned, bool playing, int repeats,
            List<SeekSample> samples, RenderTexture texture, Texture2D readback, string framesDirectory, double fps)
        {
            for (int repeat = 0; repeat < repeats; repeat++)
            {
                var sample = new SeekSample
                {
                    Kind = kind, Label = kind + "#" + repeat, Target = target,
                    KeyframeAligned = keyframeAligned, Playing = playing,
                    ExpectedFrame = (long)Math.Round(target * fps)
                };

                // Park away from the target first. A switch between neighbouring segments
                // is a real seek, never a no-op, so the measurement must never be one either.
                
                probePlaying = false;
                double park = target > videoBga.Package.durationSeconds * .5
                    ? videoBga.Package.durationSeconds * .08
                    : videoBga.Package.durationSeconds * .92;
                yield return WaitForProbePosition(park);
                probePlaying = playing;
                if (playing) yield return new WaitForSecondsRealtime(.4f);

                float ignored;
                ulong before = SampleVideoTexture(texture, readback, out ignored);
                int seeksBefore = videoBga.SeekCount;
                probeTarget = target;
                double started = Time.realtimeSinceStartupAsDouble;

                float minLuminance = 1f, finalLuminance = 0;
                int frames = 0, blackFrames = 0, staleFrames = 0;
                ulong last = before;
                var distinct = new HashSet<ulong>();
                while (Time.realtimeSinceStartupAsDouble - started < SeekSpikeTimeout)
                {
                    DriveProbeVideo();
                    yield return new WaitForEndOfFrame();
                    frames++;
                    ulong fingerprint = SampleVideoTexture(texture, readback, out float mean);
                    finalLuminance = mean;
                    if (fingerprint != 0)
                    {
                        distinct.Add(fingerprint);
                        if (mean < minLuminance) minLuminance = mean;
                        if (mean < SeekSpikeBlackLuminance) blackFrames++;
                    }
                    if (fingerprint != 0 && fingerprint == last) staleFrames++;
                    last = fingerprint;
                    if (!videoBga.IsSeeking && Math.Abs(videoBga.Time - target) <= SeekSpikeLandTolerance)
                    {
                        sample.Landed = true;
                        break;
                    }
                }

                // 落定后再多采几帧，用它来代表「这个目标的画面」。正好在 seek 宣告完成的
                // 那一刻采样会撞上目标帧的呈现延迟——那是抖动，不是不确定性。
                if (sample.Landed)
                {
                    for (int settle = 0; settle < 3; settle++)
                    {
                        DriveProbeVideo();
                        yield return new WaitForEndOfFrame();
                        frames++;
                        ulong settlePrint = SampleVideoTexture(texture, readback, out float settleMean);
                        finalLuminance = settleMean;
                        if (settlePrint == 0) continue;
                        distinct.Add(settlePrint);
                        last = settlePrint;
                        if (settleMean < minLuminance) minLuminance = settleMean;
                        if (settleMean < SeekSpikeBlackLuminance) blackFrames++;
                    }
                }

                sample.Milliseconds = (Time.realtimeSinceStartupAsDouble - started) * 1000;
                sample.TimedOut = !sample.Landed;
                sample.Frames = frames;
                sample.BlackFrames = blackFrames;
                sample.StaleFrames = staleFrames;
                sample.DistinctFrames = distinct.Count;
                sample.MinLuminance = minLuminance;
                sample.FinalLuminance = finalLuminance;
                sample.FinalTime = videoBga.Time;
                sample.FinalFrame = videoBga.Frame;
                sample.Fingerprint = last;
                sample.SeeksIssued = videoBga.SeekCount - seeksBefore;
                sample.SeekIssued = sample.SeeksIssued > 0;

                if (!string.IsNullOrEmpty(framesDirectory))
                {
                    bool found;
                    float golden = GoldenFrameLuminance(framesDirectory, sample.ExpectedFrame, texture, readback, out found);
                    if (found)
                    {
                        sample.GoldenCompared = true;
                        sample.GoldenDelta = Math.Abs(golden - finalLuminance);
                    }
                }
                samples.Add(sample);
            }
        }

        // Grabbing the picture on the GPU side, the same way ChartEditorVideoBgaChecks does:
        // a 64x36 blit is enough to tell black from not-black and to fingerprint a frame.
        ulong SampleVideoTexture(RenderTexture texture, Texture2D readback, out float meanLuminance)
        {
            meanLuminance = 0;
            if (videoBga?.Texture == null) return 0;
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(videoBga.Texture, texture);
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readback.Apply();
                var raw = readback.GetRawTextureData<byte>();
                ulong hash = 1469598103934665603;
                double sum = 0;
                for (int i = 0; i + 2 < raw.Length; i += 3)
                {
                    hash ^= raw[i]; hash *= 1099511628211;
                    hash ^= raw[i + 1]; hash *= 1099511628211;
                    hash ^= raw[i + 2]; hash *= 1099511628211;
                    sum += .2126 * raw[i] + .7152 * raw[i + 1] + .0722 * raw[i + 2];
                }
                if (raw.Length >= 3) meanLuminance = (float)(sum / (raw.Length / 3) / 255.0);
                return hash;
            }
            finally { RenderTexture.active = previous; }
        }

        // Diagnostic only: does the frame we are showing have the brightness of the
        // reference frame extracted from the same encode? Both sides go through the same
        // blit and readback, so the comparison is apples to apples.
        float GoldenFrameLuminance(string directory, long frame, RenderTexture texture, Texture2D readback, out bool found)
        {
            found = false;
            try
            {
                string path = Path.Combine(directory, "f" + frame.ToString("D5", CultureInfo.InvariantCulture) + ".png");
                if (!File.Exists(path)) return 0;
                var golden = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!golden.LoadImage(File.ReadAllBytes(path))) return 0;
                    var previous = RenderTexture.active;
                    try
                    {
                        Graphics.Blit(golden, texture);
                        RenderTexture.active = texture;
                        readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                        readback.Apply();
                        var raw = readback.GetRawTextureData<byte>();
                        double sum = 0;
                        for (int i = 0; i + 2 < raw.Length; i += 3) sum += .2126 * raw[i] + .7152 * raw[i + 1] + .0722 * raw[i + 2];
                        found = raw.Length >= 3;
                        return found ? (float)(sum / (raw.Length / 3) / 255.0) : 0;
                    }
                    finally { RenderTexture.active = previous; }
                }
                finally { Destroy(golden); }
            }
            catch (Exception e) { Debug.LogWarning("BGA_SEEK_SPIKE golden frame unavailable: " + e.Message); return 0; }
        }

        string SeekSpikeVerdict(List<SeekSample> samples, ref List<string> report)
        {
            double headerMedian = SeekSpikeMedian(samples, s => s.Kind == "segment-header-forward" && s.Landed && s.SeekIssued);
            double headerWorst = SeekSpikeWorst(samples, s => s.Kind == "segment-header-forward" && s.Landed && s.SeekIssued);
            double offMedian = SeekSpikeMedian(samples, s => s.Kind == "off-keyframe-forward" && s.Landed && s.SeekIssued);
            double backwardMedian = SeekSpikeMedian(samples, s => s.Kind == "backward" && s.Landed && s.SeekIssued);
            double playingMedian = SeekSpikeMedian(samples, s => s.Kind == "playing-switch-backward" && s.Landed && s.SeekIssued);
            int black = 0, failures = 0;
            foreach (var s in samples) { black += s.BlackFrames; if (s.TimedOut || !s.Landed) failures++; }

            report.Add(string.Format(CultureInfo.InvariantCulture,
                "STATS segmentHeaderMedian={0:0.0}ms segmentHeaderWorst={1:0.0}ms offKeyframeMedian={2:0.0}ms backwardMedian={3:0.0}ms playingSwitchMedian={4:0.0}ms blackFrames={5} failedSeeks={6}",
                headerMedian, headerWorst, offMedian, backwardMedian, playingMedian, black, failures));

            string verdict;
            if (black > 0) verdict = "NEEDS_FALLBACK_BLACK_FLASH";
            else if (failures > 0) verdict = "NEEDS_FALLBACK_SEEK_DID_NOT_LAND";
            else if (headerMedian <= 150 && headerWorst <= 400) verdict = "SAME_FILE_SEEK_OK";
            else verdict = "REVIEW";
            report.Add("VERDICT=" + verdict +
                " (rule: no black frame, every seek lands, segment-header median <=150ms and worst <=400ms)");
            return verdict;
        }

        static double SeekSpikeMedian(List<SeekSample> samples, Func<SeekSample, bool> pick)
            => SeekSpikeMedian(samples, pick, s => s.Milliseconds);

        static double SeekSpikeMedian(List<SeekSample> samples, Func<SeekSample, bool> pick, Func<SeekSample, double> value)
        {
            var values = new List<double>();
            foreach (var s in samples) if (pick(s)) values.Add(value(s));
            if (values.Count == 0) return -1;
            values.Sort();
            return values[values.Count / 2];
        }

        static double SeekSpikeWorst(List<SeekSample> samples, Func<SeekSample, bool> pick)
            => SeekSpikeWorst(samples, pick, s => s.Milliseconds);

        static double SeekSpikeWorst(List<SeekSample> samples, Func<SeekSample, bool> pick, Func<SeekSample, double> value)
        {
            double worst = -1;
            foreach (var s in samples) if (pick(s) && value(s) > worst) worst = value(s);
            return worst;
        }

        void WriteSeekSpike(List<string> report, List<SeekSample> samples, bool passed, string verdict)
        {
            report.Add("RESULT=" + (passed ? "PASS" : "FAIL") + " CHECKS=" + samples.Count);
            Directory.CreateDirectory(thartCaptureDirectory);
            File.WriteAllLines(Path.Combine(thartCaptureDirectory, "seek-spike-checks.txt"), report);

            var json = new StringBuilder();
            json.Append("{\n  \"verdict\": \"").Append(verdict).Append("\",\n  \"passed\": ").Append(passed ? "true" : "false").Append(",\n  \"platform\": \"")
                .Append(Application.platform).Append("\",\n  \"samples\": [\n");
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                json.Append("    { \"kind\": \"").Append(s.Kind).Append("\", \"target\": ").Append(s.Target.ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(", \"keyframeAligned\": ").Append(s.KeyframeAligned ? "true" : "false")
                    .Append(", \"playing\": ").Append(s.Playing ? "true" : "false")
                    .Append(", \"landed\": ").Append(s.Landed ? "true" : "false")
                    .Append(", \"seekIssued\": ").Append(s.SeekIssued ? "true" : "false")
                    .Append(", \"milliseconds\": ").Append(s.Milliseconds.ToString("0.0", CultureInfo.InvariantCulture))
                    .Append(", \"frames\": ").Append(s.Frames)
                    .Append(", \"blackFrames\": ").Append(s.BlackFrames)
                    .Append(", \"staleFrames\": ").Append(s.StaleFrames)
                    .Append(", \"distinctFrames\": ").Append(s.DistinctFrames)
                    .Append(", \"finalTime\": ").Append(s.FinalTime.ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(", \"finalFrame\": ").Append(s.FinalFrame)
                    .Append(", \"expectedFrame\": ").Append(s.ExpectedFrame)
                    .Append(", \"minLuminance\": ").Append(s.MinLuminance.ToString("0.0000", CultureInfo.InvariantCulture))
                    .Append(", \"goldenDelta\": ").Append(s.GoldenCompared ? s.GoldenDelta.ToString("0.0000", CultureInfo.InvariantCulture) : "null")
                    .Append(" }").Append(i + 1 < samples.Count ? "," : "").Append("\n");
            }
            json.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(thartCaptureDirectory, "seek-spike.json"), json.ToString());

            Debug.Log("BGA_SEEK_SPIKE_" + (passed ? "PASS" : "FAIL") + " VERDICT=" + verdict + "\n" + string.Join("\n", report));
            thartSeekSpikePassed = passed;
        }

        // Spike A, second pass. Same question as RunVideoSeekSpike, asked at the decoder instead
        // of through the runtime's coalescing wrapper: set player.frame directly, then time the
        // seekCompleted acknowledgement and the frameReady for the exact target frame.
        //
        // It runs as its own process rather than alongside the runtime cases so only one decoder
        // is resident while the numbers are taken.
        IEnumerator RunVideoSeekDecoderSpike(string source)
        {
            var report = new List<string>();
            var samples = new List<SeekSample>();
            string[] args = Environment.GetCommandLineArgs();
            int repeats = Mathf.Clamp(SeekSpikeArgumentInt(args, "-thartSeekSpikeRepeats", 3), 1, 10);
            report.Add("DECODER_PROBE=" + source);

            string url;
            try
            {
                var package = VideoBgaRuntime.ReadManifest(source);
                url = new Uri(VideoBgaRuntime.ResolveAsset(source, package.video)).AbsoluteUri;
            }
            catch (Exception e)
            {
                report.Add("FAIL package could not be read: " + e.Message);
                WriteSeekSpike(report, samples, false, "NO_VIDEO");
                yield break;
            }

            
            var root = new GameObject("BGA decoder seek probe");
            root.transform.SetParent(transform, false);
            decoderProbe = root.AddComponent<VideoPlayer>();
            decoderProbe.playOnAwake = false;
            decoderProbe.source = VideoSource.Url;
            decoderProbe.url = url;
            decoderProbe.renderMode = VideoRenderMode.APIOnly;
            decoderProbe.audioOutputMode = VideoAudioOutputMode.None;
            decoderProbe.isLooping = false;
            decoderProbe.waitForFirstFrame = true;
            decoderProbe.skipOnDrop = true;
            decoderProbe.timeReference = VideoTimeReference.Freerun;
            // The manual warns this costs CPU, but this probe exists to time exactly this event.
            decoderProbe.sendFrameReadyEvents = true;
            decoderProbe.seekCompleted += _ =>
            {
                if (decoderSeekCompletedSeconds < 0) decoderSeekCompletedSeconds = Time.realtimeSinceStartupAsDouble;
            };
            decoderProbe.frameReady += (_, frame) =>
            {
                if (frame == decoderExpectedFrame && decoderReadySeconds < 0)
                {
                    decoderReadySeconds = Time.realtimeSinceStartupAsDouble;
                    decoderReadyFrame = frame;
                }
            };
            try
            {
                decoderProbe.Prepare();
                float deadline = Time.realtimeSinceStartup + 45;
                while (!decoderProbe.isPrepared && Time.realtimeSinceStartup < deadline) yield return null;
                if (!decoderProbe.isPrepared)
                {
                    report.Add("FAIL the probe decoder never prepared");
                    WriteSeekSpike(report, samples, false, "NO_VIDEO");
                    yield break;
                }

                double fps = Math.Max(1, decoderProbe.frameRate);
                long frameCount = (long)decoderProbe.frameCount;
                report.Add(string.Format(CultureInfo.InvariantCulture,
                    "DECODER length={0:0.000}s fps={1:0.000} frames={2}", decoderProbe.length, fps, frameCount));

                var keyframes = LoadKeyframes(SeekSpikeArgument(args, "-thartSeekSpikeKeyframes"));
                report.Add("KEYFRAMES=" + keyframes.Count);
                var headers = PickKeyframeTargets(keyframes, decoderProbe.length, 3);
                var inside = headers.ConvertAll(t => t + SeekSpikeInsideOffset(keyframes, t, decoderProbe.length));
                report.Add("HEADERS=" + string.Join(", ", headers.ConvertAll(t => t.ToString("0.000", CultureInfo.InvariantCulture))));
                report.Add("OFFKEYFRAME=" + string.Join(", ", inside.ConvertAll(t => t.ToString("0.000", CultureInfo.InvariantCulture))));

                // Warm the decoder once so the first timed seek is not paying start-up costs.
                yield return DecoderSeekTo((long)(frameCount * .5));
                yield return DecoderSeekTo((long)(frameCount * .5));

                foreach (double target in headers)
                    yield return MeasureDecoderSeek("decoder-segment-header", target, fps, frameCount, repeats, samples, report);
                foreach (double target in inside)
                    yield return MeasureDecoderSeek("decoder-off-keyframe", target, fps, frameCount, repeats, samples, report);
            }
            finally
            {
                if (decoderProbe != null) decoderProbe.Stop();
                Destroy(root);
                decoderProbe = null;
            }

            int missing = 0;
            foreach (var s in samples) if (!s.Landed) missing++;
            bool passed = missing == 0;
            report.Add(string.Format(CultureInfo.InvariantCulture,
                "DECODER_STATS seekCompletedMedian={0:0.0}ms seekCompletedWorst={1:0.0}ms frameReadyMedian={2:0.0}ms frameReadyWorst={3:0.0}ms missing={4}",
                SeekSpikeMedian(samples, s => s.DecoderAckMilliseconds >= 0, s => s.DecoderAckMilliseconds),
                SeekSpikeWorst(samples, s => s.DecoderAckMilliseconds >= 0, s => s.DecoderAckMilliseconds),
                SeekSpikeMedian(samples, s => s.Landed),
                SeekSpikeWorst(samples, s => s.Landed), missing));
            string verdict = passed ? "DECODER_PROBE_COMPLETE" : "DECODER_PROBE_INCOMPLETE";
            report.Add("VERDICT=" + verdict);
            WriteSeekSpike(report, samples, passed, verdict);
        }

        IEnumerator MeasureDecoderSeek(string kind, double target, double fps, long frameCount, int repeats,
            List<SeekSample> samples, List<string> report)
        {
            for (int repeat = 0; repeat < repeats; repeat++)
            {
                var sample = new SeekSample
                {
                    Kind = kind, Label = kind + "#" + repeat, Target = target,
                    ExpectedFrame = (long)Math.Round(target * fps)
                };
                // Park in the far half so the measured seek always has real distance to cover.
                long park = sample.ExpectedFrame > frameCount / 2 ? (long)(frameCount * .08) : (long)(frameCount * .92);
                yield return DecoderSeekTo(park);
                yield return DecoderSeekTo(sample.ExpectedFrame);

                sample.DecoderAckMilliseconds = decoderSeekCompletedSeconds < 0
                    ? -1 : (decoderSeekCompletedSeconds - decoderStartedSeconds) * 1000;
                sample.Milliseconds = decoderReadySeconds < 0
                    ? -1 : (decoderReadySeconds - decoderStartedSeconds) * 1000;
                sample.Landed = decoderReadySeconds >= 0;
                sample.TimedOut = !sample.Landed;
                sample.SeekIssued = decoderSeekCompletedSeconds >= 0;
                sample.FinalFrame = decoderReadyFrame;
                samples.Add(sample);

                report.Add(string.Format(CultureInfo.InvariantCulture,
                    "DECODER_SAMPLE {0,-24} t={1,7:0.000} frame={2,5} ack={3,7:0.0}ms ready={4,7:0.0}ms landed={5}",
                    sample.Kind, sample.Target, sample.ExpectedFrame, sample.DecoderAckMilliseconds, sample.Milliseconds, sample.Landed));
            }
        }

        // Busy-waits a single seek at the decoder level. Nothing coalesces it and nothing keeps
        // showing the old frame on purpose, so these are the raw numbers the wrapper was hiding.
        IEnumerator DecoderSeekTo(long frame)
        {
            decoderSeekCompletedSeconds = -1;
            decoderReadySeconds = -1;
            decoderReadyFrame = -1;
            decoderExpectedFrame = frame;
            decoderProbe.Pause();
            decoderStartedSeconds = Time.realtimeSinceStartupAsDouble;
            decoderProbe.frame = frame;
            while (decoderReadySeconds < 0 && Time.realtimeSinceStartupAsDouble - decoderStartedSeconds < 8)
                yield return new WaitForEndOfFrame();
        }

        static string SeekSpikeArgument(string[] args, string flag)
        {
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        static List<double> LoadKeyframes(string path)
        {
            var keyframes = new List<double>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return keyframes;
            foreach (string line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) keyframes.Add(value);
            }
            keyframes.Sort();
            return keyframes;
        }

        // Three headers spread across the piece, snapped onto real keyframes when the grid
        // is known. Without a grid these are only round numbers, and the report says so
        // rather than calling them segment headers.
        static List<double> PickKeyframeTargets(List<double> keyframes, double duration, int count)
        {
            var targets = new List<double>();
            if (keyframes.Count >= count)
            {
                var used = new HashSet<int>();
                for (int i = 0; i < count; i++)
                {
                    int index = Mathf.Clamp(Mathf.RoundToInt((i + 1f) * (keyframes.Count - 1) / (count + 1f)), 0, keyframes.Count - 1);
                    while (used.Contains(index) && index + 1 < keyframes.Count) index++;
                    while (used.Contains(index) && index - 1 >= 0) index--;
                    used.Add(index);
                    targets.Add(keyframes[index]);
                }
                return targets;
            }
            for (int i = 0; i < count; i++) targets.Add(duration * (i + 1.0) / (count + 1.0));
            return targets;
        }

        // Land well inside the group of pictures rather than one frame past its header, so
        // the gap between the two cases is decode work and not a rounding artefact.
        static double SeekSpikeInsideOffset(List<double> keyframes, double target, double duration)
        {
            const double fallback = .1;
            if (keyframes.Count == 0) return fallback;
            int index = keyframes.IndexOf(target);
            double next = index >= 0 && index + 1 < keyframes.Count ? keyframes[index + 1] : -1;
            if (next <= target)
            {
                double previous = index > 0 ? keyframes[index - 1] : 0;
                return Math.Min(fallback, Math.Max(.05, target - previous) * .5);
            }
            double offset = (next - target) * .4;
            return target + offset >= duration ? fallback : offset;
        }

        static int SeekSpikeArgumentInt(string[] args, string flag, int fallback)
        {
            string value = SeekSpikeArgument(args, flag);
            return value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
        }
    }
}
