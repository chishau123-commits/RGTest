using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace GeometryRhythm
{
    /// <summary>
    /// 游玩运行时的视频 BGA 通路。
    ///
    /// 和制谱器（Thart）的关键差别：游玩端有真实的 3D 世界相机（<see cref="demoCamera"/>），
    /// 所以直接用 <see cref="VideoBgaRuntime.Activate"/> 把视频画到相机远平面——音符、路径、
    /// 雾都在它前面，正是「BGA 是环境」的形态。Thart 那边屏幕绘制权归 OnGUI 且先整屏铺不透明
    /// backdrop，远平面会被盖住，才必须走贴图模式（ActivateAsTexture）。
    ///
    /// 绑定来自谱面的 <c>chart.videoBga</c>：包内相对路径按谱面文件所在目录解析；从
    /// Resources 载入的谱面没有可解析的目录，会明确报错而不是静默不显示。
    /// </summary>
    public sealed partial class RhythmDemoController
    {
        VideoBgaRuntime videoBga;
        string videoBgaStatus = "no videoBga binding";
        string lastChartPath;
        bool videoBgaProbe;
        float lastCaptureCenterGray;

        private void LoadVideoBga(string chartPath)
        {
            lastChartPath = chartPath;
            DisposeVideoBga();

            var data = Chart != null ? Chart.videoBga : null;
            if (data == null || !data.enabled || string.IsNullOrEmpty(data.packageManifest))
            {
                videoBgaStatus = "no videoBga binding";
                return;
            }

            string manifest = ResolveVideoBgaManifest(data.packageManifest);
            if (manifest == null)
            {
                videoBgaStatus = "videoBga manifest not found: " + data.packageManifest;
                Debug.LogWarning("BGA: " + videoBgaStatus);
                return;
            }

            videoBgaStatus = "loading " + manifest;
            StartCoroutine(LoadVideoBgaRoutine(manifest));
        }

        private string ResolveVideoBgaManifest(string relative)
        {
            if (Path.IsPathRooted(relative)) return File.Exists(relative) ? relative : null;
            if (string.IsNullOrEmpty(lastChartPath)) return null;
            string basis = Path.GetDirectoryName(Path.GetFullPath(lastChartPath));
            if (string.IsNullOrEmpty(basis)) return null;
            string candidate = Path.GetFullPath(Path.Combine(basis, relative));
            return File.Exists(candidate) ? candidate : null;
        }

        private IEnumerator LoadVideoBgaRoutine(string manifest)
        {
            var candidate = new VideoBgaRuntime(transform, demoCamera);
            yield return candidate.Prepare(manifest);
            if (!candidate.IsLoaded)
            {
                videoBgaStatus = "videoBga failed: " + candidate.Error;
                Debug.LogWarning("BGA: " + videoBgaStatus);
                candidate.Dispose();
                yield break;
            }

            candidate.Activate();
            videoBga = candidate;
            videoBgaStatus = string.Format(CultureInfo.InvariantCulture,
                "loaded {0} {1}x{2} {3:0.###}fps {4:0.000}s",
                Path.GetFileName(manifest), candidate.Package.width, candidate.Package.height,
                candidate.Package.fps, candidate.Package.durationSeconds);
        }

        /// <summary>每帧挂钩。探针在跑时让位，避免两边抢 seek。</summary>
        private void UpdateVideoBga(double time, bool playing)
        {
            if (videoBgaProbe) return;
            DriveVideoBga(time, playing);
        }

        private void DriveVideoBga(double time, bool playing)
        {
            if (videoBga == null || !videoBga.IsLoaded) return;
            double offset = Chart != null && Chart.videoBga != null ? Chart.videoBga.timeOffsetSeconds : 0;
            videoBga.Evaluate(time - offset, playing);
        }

        private void DisposeVideoBga()
        {
            if (videoBga == null) return;
            videoBga.Dispose();
            videoBga = null;
        }

        #region 冒烟：-demoBgaSmoke

        private IEnumerator RunBgaSmoke(string[] args)
        {
            videoBgaProbe = true;
            var report = new List<string>();
            bool passed = true;
            Action<bool, string> check = (ok, name) => { passed &= ok; report.Add((ok ? "PASS " : "FAIL ") + name); };
            string directory = Argument(args, "-demoCapture") ?? Path.Combine(Application.persistentDataPath, "DemoCaptures");
            Directory.CreateDirectory(directory);

            report.Add("CHART=" + (string.IsNullOrEmpty(lastChartPath) ? "(none)" : lastChartPath));
            float deadline = Time.realtimeSinceStartup + 60;
            while ((videoBga == null || !videoBga.IsLoaded) && Time.realtimeSinceStartup < deadline) yield return null;
            check(videoBga != null && videoBga.IsLoaded, "chart.videoBga 指向的包能加载并解码");
            report.Add("STATUS=" + videoBgaStatus);

            if (videoBga != null && videoBga.IsLoaded)
            {
                report.Add(string.Format(CultureInfo.InvariantCulture, "VIDEO={0}x{1} fps={2:0.###} duration={3:0.000}s",
                    videoBga.Package.width, videoBga.Package.height, videoBga.Package.fps, videoBga.Package.durationSeconds));

                var texture = RenderTexture.GetTemporary(64, 36, 0, RenderTextureFormat.ARGB32);
                var readback = new Texture2D(64, 36, TextureFormat.RGB24, false);
                try
                {
                    double duration = videoBga.Package.durationSeconds;
                    var fingerprints = new HashSet<ulong>();
                    int black = 0, samples = 0;
                    foreach (double fraction in new[] { .1, .3, .5, .7, .9 })
                    {
                        double target = duration * fraction;
                        yield return SeekVideoBga(target);
                        ulong fingerprint = SampleBgaTexture(texture, readback, out float mean);
                        samples++;
                        if (mean < .05f) black++;
                        if (fingerprint != 0) fingerprints.Add(fingerprint);
                        report.Add(string.Format(CultureInfo.InvariantCulture,
                            "SAMPLE t={0,7:0.000} time={1,7:0.000} frame={2,5} mean={3:0.000} fingerprint={4}",
                            target, videoBga.Time, videoBga.Frame, mean, fingerprint));
                    }
                    check(black == 0, "五个采样点都没有黑帧（black=" + black + "）");
                    check(fingerprints.Count == samples, "五个采样点画面各不相同（distinct=" + fingerprints.Count + "/" + samples + "）");
                }
                finally
                {
                    RenderTexture.ReleaseTemporary(texture);
                    Destroy(readback);
                }

                // 两个时刻取自视频里色相差别最大的两段（这段 BGA 是橙 → 青），
                // 两张图并排就是「Note 颜色跟着视频走」的证据。
                yield return CaptureCameraFrame(directory, "bga-palette-early.png", Math.Min(Duration, 4.0));
                float earlyGray = lastCaptureCenterGray;
                Color earlyTap = PaletteTap;
                yield return CaptureCameraFrame(directory, "bga-palette-late.png", Math.Min(Duration, 12.0));
                float lateGray = lastCaptureCenterGray;
                Color lateTap = PaletteTap;

                check(earlyGray > .05f && lateGray > .05f, "两张色板截图画面中心都不是黑的（" +
                    earlyGray.ToString("0.000", CultureInfo.InvariantCulture) + " / " +
                    lateGray.ToString("0.000", CultureInfo.InvariantCulture) + "）");
                report.Add(string.Format(CultureInfo.InvariantCulture,
                    "SHOT earlyTap=({0:0.000},{1:0.000},{2:0.000}) lateTap=({3:0.000},{4:0.000},{5:0.000})",
                    earlyTap.r, earlyTap.g, earlyTap.b, lateTap.r, lateTap.g, lateTap.b));
            }

            // ---- 验收清单第 1 条：BGA 与音轨在同一次播放内偏移 ≤ 1 帧 ----
            // 用帧号判而不是秒：VideoBgaRuntime 是按 player.frame 定位的，帧号才是它的原生单位。
            if (videoBga != null && videoBga.IsLoaded)
            {
                double probe = Math.Min(Duration, 20.0);
                Seek(probe, true);
                yield return SeekVideoBga(probe);
                double offset = Chart.videoBga != null ? Chart.videoBga.timeOffsetSeconds : 0;
                long expected = (long)Math.Round((probe - offset) * videoBga.Package.fps);
                long actual = videoBga.Frame;
                check(Math.Abs(actual - expected) <= 1, "BGA 与歌曲时钟对齐在 1 帧内（frame " + actual + " vs " + expected + "）");
                report.Add(string.Format(CultureInfo.InvariantCulture,
                    "SYNC t={0:0.000} offset={1:0.000} expectedFrame={2} actualFrame={3}", probe, offset, expected, actual));
            }

            // ---- 色板权威（阶段 1）：Note 的颜色必须跟着视频走 ----
            check(PaletteActive, "谱面带 paletteKeys，色板已激活");
            if (PaletteActive)
            {
                var keys = Chart.paletteKeys;
                ApplyPalette(0);
                Color tapAtStart = library.Tap.color;
                // 第二个键的秒数由 tempo 图算出来，不写死 8 秒——这样验的是「tick→秒→取键」整条路。
                double secondSeconds = tempo.SecondsAtBeat(keys[1].tick / (double)Chart.ticksPerBeat);
                ApplyPalette(secondSeconds);
                Color tapAtSecond = library.Tap.color;

                report.Add(string.Format(CultureInfo.InvariantCulture,
                    "PALETTE keys={0} tap@{1:0.000}s=({2:0.000},{3:0.000},{4:0.000}) tap@{5:0.000}s=({6:0.000},{7:0.000},{8:0.000})",
                    keys.Length, 0.0, tapAtStart.r, tapAtStart.g, tapAtStart.b,
                    secondSeconds, tapAtSecond.r, tapAtSecond.g, tapAtSecond.b));

                check(Approximately(tapAtStart, keys[0].tap), "色板已推到 Note 材质上（t=0 对齐 keys[0].tap）");
                check(Approximately(tapAtSecond, keys[1].tap), "按 tempo 算出的第二个键时刻对齐 keys[1].tap");
                check(!Approximately(tapAtStart, tapAtSecond), "两个时间点的 Note 主色不同（跟着视频变）");

                // 可读性的对照物是运行时自己的世界底色（浅色暖雾），不是视频：
                // Note 画在世界几何体之上，背景亮不亮与视频亮不亮是两件事。
                float worldLuma = Luminance(RenderSettings.fogColor);
                float gap = worldLuma - Luminance(tapAtStart);
                check(gap >= .35f, "Note 主色与世界底色有足够亮度差（worldLuma=" +
                    worldLuma.ToString("0.000", CultureInfo.InvariantCulture) + " gap=" + gap.ToString("0.000", CultureInfo.InvariantCulture) + "）");
            }

            // ---- 判定驱动的特效（阶段 1）：effectClips 的 target=="judgement" ----
            bool hasJudgementClip = false;
            if (Chart.effectClips != null)
                foreach (var clip in Chart.effectClips) if (clip.target == "judgement") hasJudgementClip = true;
            check(hasJudgementClip, "谱面里有 target==\"judgement\" 的特效");
            if (hasJudgementClip && authoredVisuals != null)
            {
                double missTime = Math.Min(Duration, 12.0);
                // 先把时钟也定位到同一时刻：NotifyJudgement 记的是 clock.Time，两处用不同的
                // 时间基准会算出负的「距上次判定」。
                Seek(missTime, true);
                // 走真实链路：Engine 判 Miss → OnJudged → NotifyJudgement → Evaluate。
                Engine.Reset(0); Engine.Advance(missTime, false);
                authoredVisuals.Evaluate(clock.Time + .05);
                float missAlpha = authoredVisuals.LastJudgementEffectAlpha;
                check(missAlpha > 0f, "漏掉音符会触发判定特效（alpha=" + missAlpha.ToString("0.000", CultureInfo.InvariantCulture) + "）");

                double perfectTime = missTime + 5.0;
                Seek(perfectTime, true);
                Engine.Reset(0); Engine.Advance(perfectTime, true);
                authoredVisuals.Evaluate(clock.Time + .05);
                float perfectAlpha = authoredVisuals.LastJudgementEffectAlpha;
                check(perfectAlpha == 0f, "Perfect 不会触发 result==\"miss\" 的特效（过滤器生效）");
            }

            report.Add("RESULT=" + (passed ? "PASS" : "FAIL") + " CHECKS=" + report.Count);
            File.WriteAllLines(Path.Combine(directory, "bga-checks.txt"), report);
            Debug.Log("GEOMETRY_BGA_SMOKE " + (passed ? "PASS" : "FAIL") + "\n" + string.Join("\n", report));
            Application.Quit(passed ? 0 : 1);
        }

        /// <summary>只负责把相机渲到离屏目标并落盘。调用者先自己定位好歌曲时间与 BGA。</summary>
        private void RenderCameraToFile(string directory, string fileName, int width, int height)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            Canvas.ForceUpdateCanvases();
            var shotTarget = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            demoCamera.targetTexture = shotTarget;
            GameViewport.Apply(demoCamera);
            demoCamera.Render();
            RenderTexture.active = shotTarget;
            var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();
            lastCaptureCenterGray = screenshot.GetPixel(width / 2, height / 2).grayscale;
            File.WriteAllBytes(path, screenshot.EncodeToPNG());
            demoCamera.targetTexture = null;
            RenderTexture.active = previous;
            ApplyPlayfieldViewport();
            RenderTexture.ReleaseTemporary(shotTarget);
            Destroy(screenshot);
        }

        /// <summary>把真实相机渲到离屏目标并落盘。返回后 lastCaptureCenterGray 是本张图的中心灰度。</summary>
        private IEnumerator CaptureCameraFrame(string directory, string fileName, double songTime)
        {
            Seek(songTime, true);
            yield return SeekVideoBga(songTime);
            EvaluateVisuals(songTime);
            yield return null;
            RenderCameraToFile(directory, fileName, 1600, 900);
        }

        private static double ParseClipArgument(string[] args, string key, double fallback)
        {
            string value = Argument(args, key);
            return !string.IsNullOrEmpty(value) &&
                   double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed : fallback;
        }

        /// <summary>
        /// 抓一段连续帧做成动图：帧步进（每帧先定位再渲染），不是实时播放。
        /// 这样即使窗口被遮挡也能稳定出图——隐藏的 Windows player 不保证呈现 backbuffer。
        /// 代价是每帧一次视频 seek，所以抓 100 多帧要十几秒。
        /// </summary>
        private IEnumerator RunBgaClip(string[] args)
        {
            videoBgaProbe = true;
            string directory = Argument(args, "-demoCapture") ?? Path.Combine(Application.persistentDataPath, "DemoCaptures");
            Directory.CreateDirectory(directory);
            double start = ParseClipArgument(args, "-demoClipStart", 4.0);
            double seconds = ParseClipArgument(args, "-demoClipSeconds", 8.0);
            int fps = Mathf.Clamp((int)ParseClipArgument(args, "-demoClipFps", 15.0), 1, 60);
            int width = Mathf.Clamp((int)ParseClipArgument(args, "-demoClipWidth", 800.0), 64, 1920);
            int height = width * 9 / 16;

            float deadline = Time.realtimeSinceStartup + 60;
            while ((videoBga == null || !videoBga.IsLoaded) && Time.realtimeSinceStartup < deadline) yield return null;
            if (videoBga == null || !videoBga.IsLoaded)
            {
                Debug.LogWarning("GEOMETRY_BGA_CLIP FAILED: " + videoBgaStatus);
                Application.Quit(1);
                yield break;
            }

            int frames = Mathf.Max(1, Mathf.RoundToInt((float)(seconds * fps)));
            for (int i = 0; i < frames; i++)
            {
                double time = start + i / (double)fps;
                Seek(time, true);                       // 时钟与引擎定位到这一帧
                yield return SeekVideoBga(time);        // 视频定位到同一帧（BGA 有自己的定位容差）
                EvaluateVisuals(time);                  // 音符按这个时刻摆放
                yield return null;
                RenderCameraToFile(directory, string.Format(CultureInfo.InvariantCulture, "clip-{0:D4}.png", i), width, height);
            }

            string summary = string.Format(CultureInfo.InvariantCulture,
                "GEOMETRY_BGA_CLIP frames={0} start={1:0.000}s seconds={2:0.000}s fps={3} size={4}x{5}",
                frames, start, seconds, fps, width, height);
            File.WriteAllText(Path.Combine(directory, "bga-clip.txt"), summary + "\n");
            Debug.Log(summary);
            Application.Quit(0);
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < .02f;
        }

        private static float Luminance(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;

        private IEnumerator SeekVideoBga(double target)
        {
            float deadline = Time.realtimeSinceStartup + 8;
            do
            {
                DriveVideoBga(target, false);
                yield return null;
            }
            while ((videoBga.IsSeeking || Math.Abs(videoBga.Time - target) > .12) && Time.realtimeSinceStartup < deadline);
            // 落定后再多推几帧，让呈现追上解码器报告的帧
            for (int i = 0; i < 3; i++) { DriveVideoBga(target, false); yield return new WaitForEndOfFrame(); }
        }

        private ulong SampleBgaTexture(RenderTexture texture, Texture2D readback, out float meanLuminance)
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

        #endregion
    }
}
