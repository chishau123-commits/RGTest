using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace GeometryRhythm
{
    // A video-backed environment, not reconstructed geometry. The camera's far plane
    // leaves real 3D gameplay paths and notes in front of the pre-rendered imagery.
    public sealed class VideoBgaRuntime : IDisposable
    {
        readonly GameObject root;
        readonly VideoPlayer player;
        bool seeking, active, disposed, desiredPlaying;
        double seekStarted, lastRequested = -1, seekReadyAfter = -1;
        public int SeekCount { get; private set; }
        public int CompletedSeekCount { get; private set; }
        public VideoBgaManifest Package { get; private set; }
        public string ManifestPath { get; private set; }
        public string Error { get; private set; }
        public bool IsLoaded => !disposed && player != null && player.isPrepared && Error == null;
        public bool IsSeeking => seeking;
        public double Time => player == null ? 0 : player.time;
        public long Frame => player == null ? -1 : player.frame;
        public Texture Texture => player == null ? null : player.texture;

        public VideoBgaRuntime(Transform parent, Camera camera)
        {
            root = new GameObject("Song-synchronised video BGA");
            root.transform.SetParent(parent, false);
            player = root.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.source = VideoSource.Url;
            player.renderMode = VideoRenderMode.APIOnly;
            player.targetCamera = camera;
            player.targetCameraAlpha = 0;
            player.aspectRatio = VideoAspectRatio.FitInside;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.isLooping = false;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            // Wmf can stall a backward seek if externalReferenceTime is advanced
            // while that asynchronous seek is pending. Free-run between explicit
            // song-clock corrections instead; never move a decoder's pending target.
            player.timeReference = VideoTimeReference.Freerun;
            player.errorReceived += (_, message) => { Error = message; seeking = false; };
            player.sendFrameReadyEvents = true;
            player.seekCompleted += _ =>
            {
                CompletedSeekCount++;
                // The decoder event precedes presentation on Wmf. Keep the request
                // in flight until its frame has had time to reach the render queue;
                // otherwise a moving song target submits another seek immediately.
                seekReadyAfter = UnityEngine.Time.realtimeSinceStartupAsDouble + .08;
            };
            player.frameReady += (_, __) => { if (!active) player.Pause(); };
        }

        public static VideoBgaManifest ReadManifest(string path)
        {
            var data = JsonUtility.FromJson<VideoBgaManifest>(File.ReadAllText(path));
            if (data == null || data.schemaVersion != 1 || data.kind != "video-bga")
                throw new InvalidDataException("Not a video-bga v1 manifest");
            if (data.width > 0 && data.height > 0) Require16By9(data.width, data.height);
            ResolveAsset(path, data.video);
            if (!string.IsNullOrEmpty(data.audio)) ResolveAsset(path, data.audio);
            return data;
        }

        public static void Require16By9(long width, long height)
        {
            if (width <= 0 || height <= 0 || width * 9 != height * 16)
                throw new InvalidDataException("Video must be exactly 16:9 (e.g. 1920x1080 or 2560x1440). No stretching or cropping is allowed.");
        }

        public static string ResolveAsset(string manifest, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
                throw new InvalidDataException("BGA assets must use package-relative paths");
            string directory = Path.GetDirectoryName(Path.GetFullPath(manifest)) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(directory, relative));
            if (!full.StartsWith(directory, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                throw new FileNotFoundException("BGA asset is missing or outside its package: " + relative);
            return full;
        }

        public IEnumerator Prepare(string manifest)
        {
            try
            {
                ManifestPath = Path.GetFullPath(manifest);
                Package = ReadManifest(ManifestPath);
                player.url = new Uri(ResolveAsset(ManifestPath, Package.video)).AbsoluteUri;
                player.Prepare();
            }
            catch (Exception e) { Error = e.Message; yield break; }
            float deadline = UnityEngine.Time.realtimeSinceStartup + 45;
            while (!disposed && !player.isPrepared && Error == null && UnityEngine.Time.realtimeSinceStartup < deadline)
                yield return null;
            if (disposed) yield break;
            if (!player.isPrepared || player.length <= 0 || player.width == 0)
            { Error = Error ?? "Video could not be decoded within 45 seconds"; yield break; }
            // Trust the decoder over user-authored metadata.
            Package.durationSeconds = player.length;
            Package.width = (int)player.width; Package.height = (int)player.height;
            try
            {
                Require16By9(player.width, player.height);
                if (player.pixelAspectRatioNumerator != player.pixelAspectRatioDenominator)
                    throw new InvalidDataException("Video must use square pixels for exact 16:9 anchor alignment");
            }
            catch (Exception e) { Error = e.Message; yield break; }
            Package.fps = player.frameRate;
            // Warm the decoder once while it has no render target. Subsequent seeks
            // can stay paused; starting playback during a seek makes Wmf use its old
            // presentation clock and discard frames at the new (earlier) position.
            player.Play();
            while (!disposed && player.frame < 0 && Error == null && UnityEngine.Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!disposed) player.Pause();
        }

        public void Activate()
        {
            active = true;
            player.renderMode = VideoRenderMode.CameraFarPlane;
            lastRequested = -1;
        }

        public void Evaluate(double seconds, bool playing)
        {
            if (!IsLoaded || !active) return;
            player.targetCameraAlpha = seconds < 0 ? 0 : 1;
            double end = Math.Max(0, player.length - 1 / Math.Max(1, player.frameRate));
            double target = Math.Max(0, Math.Min(end, seconds));
            bool run = playing && seconds >= 0 && seconds < end;
            desiredPlaying = run;
            // Coalesce scrubbing requests. Never fill the decoder with one seek per UI
            // frame; once a seek completes the newest song position wins.
            if (seeking && seekReadyAfter >= 0 && UnityEngine.Time.realtimeSinceStartupAsDouble >= seekReadyAfter)
            { seeking = false; seekReadyAfter = -1; }
            if (seeking && UnityEngine.Time.realtimeSinceStartupAsDouble - seekStarted > 5)
            { seeking = false; lastRequested = -1; }
            double tolerance = run ? .20 : .51 / Math.Max(1, player.frameRate);
            if (!seeking && (player.frame < 0 || Math.Abs(player.time - target) > tolerance) &&
                (run || Math.Abs(lastRequested - target) > .25 / Math.Max(1, player.frameRate)))
            {
                seeking = true; seekReadyAfter = -1; SeekCount++;
                seekStarted = UnityEngine.Time.realtimeSinceStartupAsDouble;
                lastRequested = target;
                player.Pause();
                player.frame = (long)Math.Round(target * player.frameRate);
                return;
            }
            if (seeking) return;
            if (run && !player.isPlaying) player.Play();
            else if (!run && player.isPlaying) player.Pause();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (player != null) { player.targetCameraAlpha = 0; player.Stop(); }
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
        }
    }
}
