using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        const string DefaultVideoManifest = "BGA/Video20/manifest.json";
        VideoBgaRuntime videoBga, pendingVideoBga;
        string videoStatus = "Import a finished video or a video-bga manifest";
        string visualBinding;
        string audioBinding;
        bool videoImporting, videoLoading, realAudioLoaded;
        int videoRevision, audioRevision, mediaExitCode;
        string mediaError;
        Process mediaProcess;
        UnityWebRequest audioRequest;
        bool VideoBgaEnabled => chart?.videoBga != null && chart.videoBga.enabled && !string.IsNullOrWhiteSpace(chart.videoBga.packageManifest);
        bool HasVideoPackage => !string.IsNullOrWhiteSpace(chart?.videoBga?.packageManifest);
        // Hiding the image must not unlock its gameplay projection.
        bool VideoCameraLocked => HasVideoPackage;
        float VideoFrameAspect => VideoChartSpace.Aspect;

        void ApplyLockedVideoCamera()
        {
            if (!VideoCameraLocked || sceneCamera == null) return;
            if (spatial?.VideoSpace != null)
            {
                spatial.VideoSpace.ApplyCamera(sceneCamera, songTime);
                flyMode = false; ReleaseMouse(); return;
            }
            // A repeatable front view, shared by editing and F5. Neither the stage
            // spline nor legacy camera keys may reframe a pre-rendered video.
            sceneCamera.transform.SetPositionAndRotation(new Vector3(0, 8, -7),
                Quaternion.LookRotation(new Vector3(0, -6, 25), Vector3.up));
            sceneCamera.orthographic = false;
            sceneCamera.fieldOfView = 53;
            flyMode = false;
            ReleaseMouse();
        }
        bool LegacyBgaFollowing => !VideoBgaEnabled && chart?.blenderBga != null && chart.blenderBga.enabled && chart.blenderBga.followCamera;

        string ResolveMediaPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            string besideChart = string.IsNullOrEmpty(filePath) ? null : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(filePath)), path);
            if (besideChart != null && File.Exists(besideChart)) return Path.GetFullPath(besideChart);
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }

        string CurrentVisualBinding => (VideoBgaEnabled ? "video:" + ResolveMediaPath(chart.videoBga.packageManifest)
            : "legacy:" + chart?.blenderBga?.enabled + ":" + chart?.blenderBga?.packageManifest);
        string CurrentAudioBinding => ResolveMediaPath(chart?.audioFile) + ":" +
            (HasVideoPackage ? ResolveMediaPath(chart.videoBga.packageManifest) : "");

        string ChartSnapshotForUndo()
        {
            var snapshot = JsonUtility.FromJson<ChartData>(JsonUtility.ToJson(chart));
            if (!string.IsNullOrWhiteSpace(snapshot.videoBga?.packageManifest))
                snapshot.videoBga.packageManifest = ResolveMediaPath(snapshot.videoBga.packageManifest);
            if (!string.IsNullOrWhiteSpace(snapshot.audioFile)) snapshot.audioFile = ResolveMediaPath(snapshot.audioFile);
            return JsonUtility.ToJson(snapshot);
        }

        void RefreshBgaBinding()
        {
            if (visualBinding != CurrentVisualBinding) ReloadVisualBga();
        }

        void ReloadVisualBga()
        {
            visualBinding = CurrentVisualBinding;
            ++videoRevision;
            pendingVideoBga?.Dispose(); pendingVideoBga = null;
            videoBga?.Dispose(); videoBga = null;
            videoLoading = false;
            ReloadBlenderBga();
            if (VideoBgaEnabled) StartCoroutine(LoadVideoBga(ResolveMediaPath(chart.videoBga.packageManifest), videoRevision));
            if (audioBinding != CurrentAudioBinding) SetupAudio();
        }

        IEnumerator LoadVideoBga(string manifest, int revision)
        {
            videoLoading = true; videoStatus = "Preparing video…";
            var candidate = new VideoBgaRuntime(transform, sceneCamera);
            pendingVideoBga = candidate;
            yield return candidate.Prepare(manifest);
            if (revision != videoRevision) { candidate.Dispose(); yield break; }
            pendingVideoBga = null; videoLoading = false;
            if (!candidate.IsLoaded) { videoStatus = "Video failed: " + candidate.Error; candidate.Dispose(); SetStatus(videoStatus); yield break; }
            videoBga = candidate; videoBga.Activate();
            videoBga.Evaluate(songTime - chart.videoBga.timeOffsetSeconds, playing);
            videoStatus = "Ready · song-clock playback and frame scrubbing";
        }

        void DrawVideoImportButton(bool toolbar)
        {
            bool enabled = GUI.enabled; GUI.enabled = enabled && !videoImporting && !videoLoading;
            string label = videoImporting ? "Importing…" : videoLoading ? "Loading…" : "Import Video";
            bool clicked = toolbar ? GUILayout.Button(new GUIContent(label, "One-click video BGA import"), selectedButtonStyle, GUILayout.Width(132))
                : GUILayout.Button(label, selectedButtonStyle, GUILayout.Height(36));
            GUI.enabled = enabled;
            if (!clicked) return;
            ReleaseMouse(); CancelTimelineGesture(); SetPlaying(false); showFilesMenu = false;
            string source = ChooseVisualAsset("Import video BGA", "Video or BGA manifest\0*.mp4;*.mov;*.mkv;*.webm;*.avi;*.json\0\0");
            if (!string.IsNullOrEmpty(source)) StartCoroutine(ImportVideoBga(source));
        }

        void DrawVideoBgaInspector()
        {
            DrawVideoImportButton(false);
            GUILayout.Label("VIDEO BGA · 3D gameplay overlay", headingStyle);
            GUILayout.Label(videoStatus, smallStyle);
            if (videoBga?.Package != null)
            {
                var p = videoBga.Package;
                GUILayout.Label(p.title ?? "Video BGA", smallStyle);
                GUILayout.Label($"{p.width} × {p.height} · {p.fps:0.##} fps · {p.durationSeconds:0.00} s", smallStyle);
                GUILayout.Label($"Video time {videoBga.Time:0.00} s · frame {videoBga.Frame}", smallStyle);
            }
            GUILayout.Space(8);
            if (chart.videoBga != null)
            {
                bool enabled = chart.videoBga.enabled;
                if (GUILayout.Button(enabled ? "BGA · VISIBLE" : "BGA · HIDDEN", enabled ? selectedButtonStyle : buttonStyle))
                { RecordUndo(); chart.videoBga.enabled = !enabled; }
                float offset = chart.videoBga.timeOffsetSeconds;
                if (FloatField("Video offset (s)", ref offset)) chart.videoBga.timeOffsetSeconds = offset;
            }
            if (GUILayout.Button("Import / replace song"))
            {
                SetPlaying(false);
                string source = ChooseVisualAsset("Import song", "Audio\0*.wav;*.ogg;*.mp3\0\0");
                if (!string.IsNullOrEmpty(source)) { RecordUndo(); chart.audioFile = Path.GetFullPath(source); SetupAudio(); }
            }
            GUILayout.Label(realAudioLoaded ? "Song audio loaded" : "No external song loaded · demo audio only for empty charts", smallStyle);
            if (GUILayout.Button("Reload media") && !videoImporting) { ReloadVisualBga(); SetupAudio(); }
            GUILayout.Space(8);
            GUILayout.Label("16:9 video / unified edit + playback view. Shared Camera Z keys move forward; every Path has independent video X/Y percent anchors (top-left origin). Camera Z and all Path tracks are always on the timeline. Space / F5 plays in place.\n\nFiles > Export .grchart includes media and all coordinate tracks.", smallStyle);
            if (!VideoBgaEnabled && chart.blenderBga?.enabled == true)
                GUILayout.Label("Legacy Blender package retained for this chart. Import a video to switch workflows.", smallStyle);
        }

        string FindFfmpeg()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            foreach (string path in new[] { Path.Combine(root, "BGA/Tools/ffmpeg.exe"), Path.Combine(root, "BGA/tools/bin/ffmpeg.exe") })
                if (File.Exists(path)) return path;
            throw new FileNotFoundException("The bundled BGA/Tools/ffmpeg.exe is missing");
        }
        static string QuoteMedia(string path) => "\"" + path.Replace("\"", "") + "\"";

        IEnumerator RunMediaCommand(string executable, string arguments)
        {
            mediaExitCode = -1; mediaError = "";
            var errors = new StringBuilder();
            try
            {
                mediaProcess = new Process { StartInfo = new ProcessStartInfo(executable, "-hide_banner -loglevel error -nostdin " + arguments)
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true } };
                mediaProcess.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (errors) { if (errors.Length < 8000) errors.AppendLine(e.Data); } };
                mediaProcess.Start(); mediaProcess.BeginErrorReadLine();
            }
            catch (Exception e) { mediaError = e.Message; mediaProcess?.Dispose(); mediaProcess = null; yield break; }
            while (!mediaProcess.HasExited) yield return null;
            mediaProcess.WaitForExit(); mediaExitCode = mediaProcess.ExitCode;
            lock (errors) mediaError = errors.ToString();
            mediaProcess.Dispose(); mediaProcess = null;
        }

        IEnumerator ImportVideoBga(string source)
        {
            if (videoImporting) yield break;
            videoImporting = true; var importingChart = chart;
            VideoBgaRuntime candidate = null;
            string manifest = null, ffmpeg = null, output = null;
            try
            {
                source = Path.GetFullPath(source);
                if (!File.Exists(source)) throw new FileNotFoundException(source);
                if (Path.GetExtension(source).Equals(".json", StringComparison.OrdinalIgnoreCase))
                { VideoBgaRuntime.ReadManifest(source); manifest = source; }
                else
                {
                    ffmpeg = FindFfmpeg();
                    output = Path.Combine(Application.persistentDataPath, "VideoBga", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(output);
                }
            }
            catch (Exception e) { videoStatus = "Import failed: " + e.Message; videoImporting = false; SetStatus(videoStatus); yield break; }
            if (output != null)
            {
                videoStatus = "Converting video · H.264, 60 fps, short seek interval…";
                yield return RunMediaCommand(ffmpeg, "-i " + QuoteMedia(source) + " -map 0:v:0 -an -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2:out_color_matrix=bt709,fps=60:round=up:eof_action=pass\" -c:v libx264 -profile:v high -level 5.1 -bf 0 -preset fast -crf 18 -pix_fmt yuv420p -color_primaries bt709 -color_trc bt709 -colorspace bt709 -g 15 -keyint_min 15 -sc_threshold 0 -bsf:v h264_metadata=colour_primaries=1:transfer_characteristics=1:matrix_coefficients=1 -movflags +faststart " + QuoteMedia(Path.Combine(output, "video.mp4")));
                if (mediaExitCode != 0) { videoStatus = "Video conversion failed: " + mediaError; videoImporting = false; SetStatus(videoStatus); yield break; }
                videoStatus = "Reading soundtrack…";
                yield return RunMediaCommand(ffmpeg, "-i " + QuoteMedia(source) + " -map 0:a:0? -vn -ac 2 -ar 48000 -c:a pcm_s16le " + QuoteMedia(Path.Combine(output, "audio.wav")));
                bool hasAudio = mediaExitCode == 0 && File.Exists(Path.Combine(output, "audio.wav"));
                videoStatus = "Checking video package…";
                var digest = Task.Run(() => HashMedia(source));
                while (!digest.IsCompleted) yield return null;
                try
                {
                    manifest = Path.Combine(output, "manifest.json");
                    File.WriteAllText(manifest, JsonUtility.ToJson(new VideoBgaManifest { title = Path.GetFileNameWithoutExtension(source),
                        audio = hasAudio ? "audio.wav" : "", sourceSha256 = digest.GetAwaiter().GetResult() }, true));
                }
                catch (Exception e) { videoStatus = "Packaging failed: " + e.Message; videoImporting = false; SetStatus(videoStatus); yield break; }
            }
            candidate = new VideoBgaRuntime(transform, sceneCamera);
            pendingVideoBga = candidate;
            yield return candidate.Prepare(manifest);
            if (!candidate.IsLoaded || !ReferenceEquals(chart, importingChart))
            {
                videoStatus = !candidate.IsLoaded ? "Import failed: " + candidate.Error : "Chart changed; imported package was not attached";
                candidate.Dispose(); if (pendingVideoBga == candidate) pendingVideoBga = null;
                videoImporting = false; SetStatus(videoStatus); yield break;
            }
            try { if (output != null) File.WriteAllText(manifest, JsonUtility.ToJson(candidate.Package, true)); }
            catch (Exception e) { candidate.Dispose(); pendingVideoBga = null; videoImporting = false; SetStatus("Package write failed: " + e.Message); yield break; }
            RecordUndo(); SetPlaying(false);
            chart.videoBga = new VideoBgaData { enabled = true, packageManifest = manifest };
            chart.blenderBga.enabled = false; chart.blenderBga.followCamera = false;
            // A silent video must never erase the chart's independently selected song.
            if (!string.IsNullOrEmpty(candidate.Package.audio)) chart.audioFile = VideoBgaRuntime.ResolveAsset(manifest, candidate.Package.audio);
            if ((chart.notes == null || chart.notes.Length == 0) && tempo != null && !VideoChartSpace.Enabled(chart))
            {
                float videoEndBeat = (float)tempo.BeatAtSeconds(candidate.Package.durationSeconds);
                chart.endBeat = Mathf.Ceil(videoEndBeat * chart.ticksPerBeat) / chart.ticksPerBeat;
            }
            ++videoRevision; pendingVideoBga = null;
            ++bgaLoadRevision; blenderBga?.Dispose(); blenderBga = null; videoBga?.Dispose();
            videoBga = candidate; videoBga.Activate(); visualBinding = CurrentVisualBinding;
            videoLoading = videoImporting = false;
            Rebuild(); SetupAudio(); SetMode(ChartEditMode.Bga);
            videoStatus = "Imported · paths, notes and tempo preserved"; SetStatus(videoStatus);
        }

        static string HashMedia(string path)
        {
            using (var input = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        void LoadSongAudio()
        {
            audioBinding = CurrentAudioBinding;
            ++audioRevision; audioRequest?.Abort();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.Stop(); audioSource.clip = null; realAudioLoaded = false;
            if (generatedAudio != null) Destroy(generatedAudio);
            generatedAudio = null; audioSource.playOnAwake = false; audioSource.volume = .65f;
            string source = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(chart.audioFile)) source = ResolveMediaPath(chart.audioFile);
                else if (HasVideoPackage)
                {
                    string manifest = ResolveMediaPath(chart.videoBga.packageManifest);
                    var package = VideoBgaRuntime.ReadManifest(manifest);
                    if (!string.IsNullOrWhiteSpace(package.audio)) source = VideoBgaRuntime.ResolveAsset(manifest, package.audio);
                }
            }
            catch (Exception e) { SetStatus("Audio unavailable: " + e.Message); BuildTimelineWaveform(); return; }
            if (source != null) StartCoroutine(LoadSongFile(source, audioRevision));
            else if (!HasVideoPackage)
            {
                generatedAudio = DemoSoundtrack.Create((float)Math.Max(1, Duration)); audioSource.clip = generatedAudio;
            }
            BuildTimelineWaveform();
        }

        IEnumerator LoadSongFile(string path, int revision)
        {
            if (!File.Exists(path)) { SetStatus("Song file not found: " + Path.GetFileName(path)); yield break; }
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var type = extension == ".wav" ? AudioType.WAV : extension == ".ogg" ? AudioType.OGGVORBIS : AudioType.MPEG;
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type))
            {
                audioRequest = request;
                yield return request.SendWebRequest();
                if (revision != audioRevision) yield break;
                audioRequest = null;
                if (request.result != UnityWebRequest.Result.Success) { SetStatus("Song decode failed: " + request.error); yield break; }
                generatedAudio = DownloadHandlerAudioClip.GetContent(request);
                audioSource.clip = generatedAudio; realAudioLoaded = generatedAudio != null;
                BuildTimelineWaveform();
                if (playing) StartAudioAtCurrentTime();
            }
        }

        // Save a clone so a failed disk write cannot corrupt the currently open paths.
        ChartData PortableChartForSave(string chartPath)
        {
            var copy = JsonUtility.FromJson<ChartData>(JsonUtility.ToJson(chart));
            string assetRelative = Path.GetFileNameWithoutExtension(chartPath) + ".assets";
            string root = Path.Combine(Path.GetDirectoryName(chartPath), assetRelative);
            string bundledBgaAudioSource = null, bundledBgaAudioRelative = null;
            if (!string.IsNullOrWhiteSpace(copy.videoBga?.packageManifest))
            {
                string manifest = ResolveMediaPath(copy.videoBga.packageManifest);
                var package = VideoBgaRuntime.ReadManifest(manifest);
                string video = VideoBgaRuntime.ResolveAsset(manifest, package.video);
                string videoHash = HashMedia(video);
                string sub = "BGA/" + videoHash.Substring(0, 20) + "-" + HashMedia(manifest).Substring(0, 12);
                string target = Path.Combine(root, sub); Directory.CreateDirectory(target);
                string videoName = "video" + Path.GetExtension(video);
                CopyMediaOnce(video, Path.Combine(target, videoName));
                if (!string.IsNullOrWhiteSpace(package.audio))
                {
                    bundledBgaAudioSource = VideoBgaRuntime.ResolveAsset(manifest, package.audio);
                    package.audio = "audio" + Path.GetExtension(package.audio);
                    CopyMediaOnce(bundledBgaAudioSource, Path.Combine(target, package.audio));
                    bundledBgaAudioRelative = assetRelative + "/" + sub + "/" + package.audio;
                }
                package.video = videoName;
                string targetManifest = Path.Combine(target, "manifest.json");
                if (!File.Exists(targetManifest)) File.WriteAllText(targetManifest, JsonUtility.ToJson(package, true));
                copy.videoBga.packageManifest = assetRelative + "/" + sub + "/manifest.json";
            }
            if (!string.IsNullOrWhiteSpace(copy.audioFile))
            {
                string song = ResolveMediaPath(copy.audioFile);
                if (!string.IsNullOrEmpty(bundledBgaAudioSource) && Path.GetFullPath(song).Equals(Path.GetFullPath(bundledBgaAudioSource), StringComparison.OrdinalIgnoreCase))
                    copy.audioFile = bundledBgaAudioRelative;
                else
                {
                    string name = "Audio/" + HashMedia(song).Substring(0, 20) + Path.GetExtension(song);
                    Directory.CreateDirectory(Path.Combine(root, "Audio")); CopyMediaOnce(song, Path.Combine(root, name));
                    copy.audioFile = assetRelative + "/" + name;
                }
            }
            return copy;
        }
        static void CopyMediaOnce(string source, string target)
        {
            if (Path.GetFullPath(source).Equals(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) return;
            if (File.Exists(target))
            {
                if (HashMedia(source) != HashMedia(target)) throw new IOException("Existing bundled media differs: " + target);
            }
            else File.Copy(source, target);
        }
    }
}
