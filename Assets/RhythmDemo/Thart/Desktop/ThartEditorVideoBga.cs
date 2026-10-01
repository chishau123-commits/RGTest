using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GeometryRhythm.Thart.Editor
{
    /// <summary>
    /// 视频 BGA 在 Thart 里的通路。
    ///
    /// 为什么不能像旧制谱器那样直接把视频贴到相机远平面：Thart 的屏幕绘制权完全归 OnGUI，
    /// 而它在画任何东西之前先执行 Styles.DrawBackdrop 把整屏铺成不透明底色，远平面视频会被
    /// 那层盖住。所以这里用 VideoBgaRuntime 的「贴图」模式（ActivateAsTexture），由
    /// DrawVideoBga 把 Texture 画进主视图矩形——和 3D 预览通路（ThartEditorPreview）同一套做法。
    ///
    /// 命令行（复用 CommandLineArgument 约定）：
    ///   -thartCapture &lt;dir&gt;         报告目录，默认 persistentDataPath/ThartSmoke
    ///   -thartBga &lt;manifest.json&gt;   把一个 video-bga 包挂成编辑器背景
    ///   -thartSeekSpike &lt;manifest&gt;  跑运行时级 seek 量测后退出
    ///   -thartSeekSpikeDecoder &lt;m&gt; 跑解码器级 seek 量测后退出
    /// 后两者附加 -thartSeekSpikeKeyframes/-thartSeekSpikeRepeats/-thartSeekSpikeFrames。
    /// </summary>
    public sealed partial class ThartEditorController
    {
        VideoBgaRuntime videoBga;
        string videoBgaProbeError;
        string thartCaptureDirectory;
        bool thartSeekSpikePassed;
        bool probeRunning;
        double probeTarget;
        bool probePlaying;
        /// <summary>BGA 文件所在目录：保存 .thr 时从这里打进包。来自包内时指向展开缓存。</summary>
        string bgaSourceDirectory;

        /// <summary>Start() 里调用：先处理量测开关，其次是把 BGA 挂成编辑器背景。</summary>
        private void LoadCommandLineVideoBga()
        {
            thartCaptureDirectory = CommandLineArgument("-thartCapture");
            if (string.IsNullOrEmpty(thartCaptureDirectory))
                thartCaptureDirectory = Path.Combine(Application.persistentDataPath, "ThartSmoke");

            string decoder = CommandLineArgument("-thartSeekSpikeDecoder");
            if (!string.IsNullOrEmpty(decoder))
            {
                probeRunning = true;
                StartCoroutine(RunProbeThenQuit(RunVideoSeekDecoderSpike(decoder)));
                return;
            }

            string runtime = CommandLineArgument("-thartSeekSpike");
            if (!string.IsNullOrEmpty(runtime))
            {
                probeRunning = true;
                StartCoroutine(RunProbeThenQuit(RunVideoSeekSpike(runtime)));
                return;
            }

            string packSmoke = CommandLineArgument("-thartBgaPackSmoke");
            if (!string.IsNullOrEmpty(packSmoke))
            {
                probeRunning = true;
                StartCoroutine(RunProbeThenQuit(RunBgaPackageSmoke(packSmoke)));
                return;
            }

            string manifest = CommandLineArgument("-thartBga");
            if (!string.IsNullOrEmpty(manifest)) { AttachBgaFromManifest(manifest); return; }

            // 没有命令行参数时，看当前谱面自己绑了什么
            string fromChart = ChartVideoBgaManifest();
            if (!string.IsNullOrEmpty(fromChart)) StartCoroutine(BindVideoBga(fromChart));
        }

        /// <summary>把一份 BGA 包挂到当前谱面上：谱面只记包内相对路径，保存时再把文件打进包。</summary>
        private void AttachBgaFromManifest(string manifestPath)
        {
            string full = Path.GetFullPath(manifestPath);
            bgaSourceDirectory = Path.GetDirectoryName(full);
            if (chart != null)
            {
                if (chart.videoBga == null) chart.videoBga = new VideoBgaData();
                chart.videoBga.packageManifest = ThartPackage.BgaManifestEntry;
                chart.videoBga.enabled = true;
            }
            StartCoroutine(BindVideoBga(full));
        }

        private IEnumerator RunProbeThenQuit(IEnumerator probe)
        {
            yield return StartCoroutine(probe);
            yield return new WaitForEndOfFrame();
            Application.Quit(thartSeekSpikePassed ? 0 : 1);
        }

        /// <summary>谱面里绑定的 BGA（Thart 目前只认绝对/当前目录可解析的路径；包内相对路径待 .thr 槽位落地）。</summary>
        private string ChartVideoBgaManifest()
        {
            if (chart?.videoBga == null || !chart.videoBga.enabled) return null;
            string manifest = chart.videoBga.packageManifest;
            if (string.IsNullOrEmpty(manifest)) return null;
            // 包内相对路径（bga/manifest.json）只能在打开 .thr 时还原，这里不猜。
            if (manifest.StartsWith(ThartPackage.BgaDir, StringComparison.OrdinalIgnoreCase)) return null;
            if (Path.IsPathRooted(manifest)) return File.Exists(manifest) ? manifest : null;
            if (!string.IsNullOrEmpty(chartFilePath))
            {
                string beside = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(chartFilePath) ?? "", manifest));
                if (File.Exists(beside)) return beside;
            }
            return null;
        }

        private IEnumerator BindVideoBga(string manifest)
        {
            videoBgaProbeError = null;
            if (videoBga != null) { videoBga.Dispose(); videoBga = null; }

            var candidate = new VideoBgaRuntime(transform, Camera.main);
            yield return candidate.Prepare(manifest);
            if (!candidate.IsLoaded)
            {
                videoBgaProbeError = candidate.Error ?? "video package did not load";
                candidate.Dispose();
                yield break;
            }
            candidate.ActivateAsTexture();
            videoBga = candidate;
        }

        /// <summary>量测用：与 BindVideoBga 同一路径，失败原因记进 videoBgaProbeError。</summary>
        private IEnumerator PrepareVideoBgaForProbe(string manifest)
        {
            yield return BindVideoBga(manifest);
        }

        /// <summary>每帧：把 BGA 定位到当前歌曲时间。量测期间由探针自己驱动，避免两边抢 seek。</summary>
        private void UpdateVideoBga()
        {
            if (probeRunning || videoBga == null || !videoBga.IsLoaded) return;
            double offset = chart?.videoBga != null ? chart.videoBga.timeOffsetSeconds : 0;
            videoBga.Evaluate(songTime - offset, playing);
        }

        private void DisposeVideoBga()
        {
            if (videoBga != null) { videoBga.Dispose(); videoBga = null; }
        }

        // ---- 探针宿主（量测代码在 ThartEditorSeekSpike.cs 分片里）----

        private void DriveProbeVideo()
        {
            if (videoBga != null && videoBga.IsLoaded) videoBga.Evaluate(probeTarget, probePlaying);
        }

        private IEnumerator WaitForProbePosition(double target, double tolerance = .1)
        {
            probeTarget = target;
            float deadline = Time.realtimeSinceStartup + 8;
            do { DriveProbeVideo(); yield return null; }
            while (videoBga != null && (videoBga.IsSeeking || Math.Abs(videoBga.Time - target) > tolerance) &&
                   Time.realtimeSinceStartup < deadline);
            DriveProbeVideo(); yield return new WaitForEndOfFrame();
            DriveProbeVideo(); yield return new WaitForEndOfFrame();
        }

        private bool HasVideoBgaTexture => videoBga != null && videoBga.IsLoaded && videoBga.Texture != null;

        /// <summary>由 DrawMainView 调用，把 BGA 画在主视图的 16:9 矩形里、编辑器面板之下。</summary>
        private void DrawVideoBga(Rect field)
        {
            if (!HasVideoBgaTexture) return;
            GUI.DrawTexture(field, videoBga.Texture, ScaleMode.StretchToFill, false);
        }

        // ---- .thr 包内的 BGA 槽位 ----

        /// <summary>要写进 .thr 的 bga/ 条目：只带 manifest 点名的文件，不顺手把目录里别的东西装进去。</summary>
        private IList<ThartZip.Entry> CollectBgaEntries()
        {
            if (string.IsNullOrEmpty(bgaSourceDirectory) || !Directory.Exists(bgaSourceDirectory)) return null;
            string manifest = Path.Combine(bgaSourceDirectory, "manifest.json");
            if (!File.Exists(manifest)) return null;

            VideoBgaManifest package;
            try { package = VideoBgaRuntime.ReadManifest(manifest); }
            catch (Exception e) { videoBgaProbeError = "BGA manifest 读不了: " + e.Message; return null; }

            var entries = new List<ThartZip.Entry>
            {
                new ThartZip.Entry(ThartPackage.BgaManifestEntry, File.ReadAllBytes(manifest))
            };
            string video = VideoBgaRuntime.ResolveAsset(manifest, package.video);
            entries.Add(new ThartZip.Entry(ThartPackage.BgaDir + Path.GetFileName(video), File.ReadAllBytes(video)));
            if (!string.IsNullOrEmpty(package.audio))
            {
                string audio = VideoBgaRuntime.ResolveAsset(manifest, package.audio);
                entries.Add(new ThartZip.Entry(ThartPackage.BgaDir + Path.GetFileName(audio), File.ReadAllBytes(audio)));
            }
            return entries;
        }

        /// <summary>把包内 bga/ 展开到缓存目录再走常规绑定；缓存目录同时成为下次保存的源。</summary>
        private void MaterializeBgaFromPackage(Dictionary<string, byte[]> bgaEntries)
        {
            if (bgaEntries == null || bgaEntries.Count == 0) { DisposeVideoBga(); bgaSourceDirectory = null; return; }

            byte[] manifestBytes = null;
            string manifestEntry = null;
            foreach (var kv in bgaEntries)
            {
                if (!kv.Key.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase)) continue;
                manifestBytes = kv.Value; manifestEntry = kv.Key; break;
            }
            if (manifestBytes == null)
            {
                videoBgaProbeError = "包内 bga/ 里没有 manifest.json";
                return;
            }

            string cacheDir = Path.Combine(Application.persistentDataPath, "ThartBgaCache",
                StableKey(manifestEntry, manifestBytes, bgaEntries));
            try
            {
                Directory.CreateDirectory(cacheDir);
                foreach (var kv in bgaEntries)
                {
                    string name = kv.Key.StartsWith(ThartPackage.BgaDir, StringComparison.OrdinalIgnoreCase)
                        ? kv.Key.Substring(ThartPackage.BgaDir.Length) : kv.Key;
                    if (name.Length == 0) continue;
                    File.WriteAllBytes(Path.Combine(cacheDir, Path.GetFileName(name)), kv.Value);
                }
            }
            catch (Exception e)
            {
                videoBgaProbeError = "展开包内 BGA 失败: " + e.Message;
                return;
            }

            // 缓存目录就是文件源：这样用户「打开 → 另存」时 BGA 不会从包里掉出去。
            bgaSourceDirectory = cacheDir;
            StartCoroutine(BindVideoBga(Path.Combine(cacheDir, "manifest.json")));
        }

        /// <summary>等两帧再让 ScreenCapture 落盘（它写的是本帧结束时的画面，含 OnGUI）。</summary>
        private IEnumerator CaptureView(string directory, string fileName, Action<bool, string> check, string label)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            if (File.Exists(path)) File.Delete(path);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            // ScreenCapture 是异步落盘的：只等固定帧数会读到一个 0 字节的文件。
            float deadline = Time.realtimeSinceStartup + 5;
            long bytes = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForEndOfFrame();
                bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
                if (bytes > 10000) break;
            }
            check(bytes > 10000, label + "（" + bytes + " 字节）");
        }

        /// <summary>包版本的稳定标识：同样的内容命中同一个缓存目录，免得每次打开都重写 100 MB。</summary>
        private static string StableKey(string manifestEntry, byte[] manifestBytes, Dictionary<string, byte[]> entries)
        {
            ulong hash = 1469598103934665603;
            foreach (byte b in manifestBytes) { hash ^= b; hash *= 1099511628211; }
            var names = new List<string>(entries.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (string name in names)
            {
                foreach (byte b in Encoding.UTF8.GetBytes(name)) { hash ^= b; hash *= 1099511628211; }
                hash ^= (ulong)entries[name].Length; hash *= 1099511628211;
            }
            return hash.ToString("x16");
        }

        /// <summary>
        /// 往返自检：挂一份外部 BGA 包 → 存成 .thr → 重新读包 → 从包内还原并确认还能解码。
        /// 「.thr 能装 BGA」如果不这样验一次，就只是编译通过而已。
        /// </summary>
        private IEnumerator RunBgaPackageSmoke(string manifestPath)
        {
            var report = new List<string>();
            bool passed = true;
            Action<bool, string> check = (ok, name) => { passed &= ok; report.Add((ok ? "PASS " : "FAIL ") + name); };
            Directory.CreateDirectory(thartCaptureDirectory);
            string thrPath = Path.Combine(thartCaptureDirectory, "bga-roundtrip.thr");
            if (File.Exists(thrPath)) File.Delete(thrPath);

            report.Add("SOURCE=" + manifestPath);
            long sourceVideoBytes = 0;
            try
            {
                var package = VideoBgaRuntime.ReadManifest(manifestPath);
                sourceVideoBytes = new FileInfo(VideoBgaRuntime.ResolveAsset(manifestPath, package.video)).Length;
            }
            catch (Exception e) { report.Add("DETAIL 源包读不了: " + e.Message); }

            AttachBgaFromManifest(manifestPath);
            float deadline = Time.realtimeSinceStartup + 60;
            while ((videoBga == null || !videoBga.IsLoaded) && Time.realtimeSinceStartup < deadline) yield return null;
            check(videoBga != null && videoBga.IsLoaded, "源 BGA 包能加载并解码");
            check(chart != null && chart.videoBga != null &&
                  string.Equals(chart.videoBga.packageManifest, ThartPackage.BgaManifestEntry, StringComparison.OrdinalIgnoreCase),
                "谱面记的是包内相对路径 " + ThartPackage.BgaManifestEntry);

            // 视觉证据：这一行 DrawVideoBga 到底有没有把画面画进主视图。
            yield return CaptureView(thartCaptureDirectory, "bga-main-view.png", check, "挂载后主视图截图有内容");

            var entries = CollectBgaEntries();
            check(entries != null && entries.Count >= 2, "收集到 bga/ 条目（manifest + 视频）");

            string title = string.IsNullOrEmpty(chart.title) ? "Thart Chart" : chart.title;
            ThartPackage.Save(thrPath, chart, currentRecording, audioRawBytes, audioFileName, title, entries);
            check(File.Exists(thrPath), "写出 .thr");
            report.Add("DETAIL .thr 大小 = " + (File.Exists(thrPath) ? new FileInfo(thrPath).Length : 0) + " 字节");

            ThartPackage.Content content = null;
            try { content = ThartPackage.Load(thrPath); }
            catch (Exception e) { report.Add("DETAIL 读包失败: " + e.Message); }
            check(content != null && content.Chart != null, "重新读回 .thr");
            check(content != null && content.Info != null && content.Info.hasBga, "meta 标记 hasBga");
            check(content != null && content.BgaEntries != null && content.BgaEntries.Count >= 2, "包内有 bga/ 条目");

            if (content != null && content.BgaEntries != null)
            {
                long videoBytes = 0;
                foreach (var kv in content.BgaEntries)
                    if (kv.Key.EndsWith("video.mp4", StringComparison.OrdinalIgnoreCase)) videoBytes = kv.Value.Length;
                check(sourceVideoBytes > 0 && videoBytes == sourceVideoBytes,
                    "包内视频字节数与源一致（" + videoBytes + " vs " + sourceVideoBytes + "）");
            }

            DisposeVideoBga();
            check(videoBga == null, "先卸载，确认后面是从包内还原的");
            MaterializeBgaFromPackage(content != null ? content.BgaEntries : null);
            deadline = Time.realtimeSinceStartup + 60;
            while ((videoBga == null || !videoBga.IsLoaded) && Time.realtimeSinceStartup < deadline) yield return null;
            check(videoBga != null && videoBga.IsLoaded, "从包内还原后 BGA 仍能解码");
            yield return CaptureView(thartCaptureDirectory, "bga-restored-view.png", check, "还原后主视图截图有内容");

            report.Add("RESULT=" + (passed ? "PASS" : "FAIL") + " CHECKS=" + report.Count);
            File.WriteAllLines(Path.Combine(thartCaptureDirectory, "bga-package-checks.txt"), report);
            Debug.Log("THART_BGA_PACKAGE_" + (passed ? "PASS" : "FAIL") + "\n" + string.Join("\n", report));
            thartSeekSpikePassed = passed;
        }
    }
}
