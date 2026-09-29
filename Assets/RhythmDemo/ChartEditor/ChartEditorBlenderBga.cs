using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.ChartEditor
{
    public sealed partial class RuntimeChartEditorController
    {
        const string DefaultBgaManifest = "BGA/exports/Firefly_the_Summer_Opening_v03/manifest.json";
        BlenderBgaRuntime blenderBga;
        string bgaStatus = "No Blender BGA loaded";
        bool bgaExporting;
        bool bgaLoading;
        int bgaLoadRevision;

        void EnsureBlenderBgaData()
        {
            if (chart.blenderBga == null)
                chart.blenderBga = new BlenderBgaData { enabled = false, followCamera = false };
        }

        async void ReloadBlenderBga()
        {
            EnsureBlenderBgaData();
            int revision = ++bgaLoadRevision;
            bgaLoading = false;
            blenderBga?.Dispose();
            if (VideoBgaEnabled) { blenderBga = null; return; }
            blenderBga = new BlenderBgaRuntime(transform);
            if (!chart.blenderBga.enabled) { bgaStatus = "Blender BGA disabled"; return; }
            string manifest = ResolveBgaPath(chart.blenderBga.packageManifest, true);
            if (string.IsNullOrEmpty(manifest) || !File.Exists(manifest))
            {
                bgaStatus = "BGA package not found · import a .blend file"; return;
            }
            bgaLoading = true;
            bool loaded = await blenderBga.LoadAsync(manifest, message =>
            {
                if (revision == bgaLoadRevision) bgaStatus = message;
            });
            if (revision != bgaLoadRevision) return;
            bgaLoading = false;
            if (loaded)
            {
                chart.blenderBga.packageManifest = MakePortableBgaPath(manifest);
                chart.blenderBga.sourceSha256 = blenderBga.Package.sourceSha256;
                if (generatedAudio == null || generatedAudio.length + .1f < Duration) SetupAudio();
                blenderBga.Evaluate(songTime - chart.blenderBga.timeOffsetSeconds, sceneCamera,
                    chart.blenderBga.followCamera || chartCameraPreview);
            }
            SetStatus(loaded ? "Blender 3D BGA loaded" : bgaStatus);
        }

        string ResolveBgaPath(string path, bool manifest)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            string relative = path.Replace('/', Path.DirectorySeparatorChar);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string exeRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string chartRoot = string.IsNullOrEmpty(filePath) ? null : Path.GetDirectoryName(Path.GetFullPath(filePath));
            string[] candidates =
            {
                chartRoot == null ? null : Path.Combine(chartRoot, relative),
                Path.Combine(projectRoot, relative),
                Path.Combine(exeRoot, relative),
                Path.Combine(exeRoot, "BGA", "Current", manifest ? "manifest.json" : Path.GetFileName(relative))
            };
            foreach (string candidate in candidates) if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate)) return Path.GetFullPath(candidate);
            return candidates[0] ?? candidates[1];
        }

        string MakePortableBgaPath(string manifest)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.GetFullPath(manifest);
            if (full.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return full.Substring(projectRoot.Length + 1).Replace('\\', '/');
            return full;
        }

        void DrawBlenderImportButton(bool toolbar)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && !bgaExporting && !bgaLoading;
            string label = bgaExporting ? "Exporting…" : bgaLoading ? "Loading…" : "Import Blender";
            var content = new GUIContent(label, "Select a .blend project; export and load its 3D BGA automatically");
            bool clicked = toolbar
                ? GUILayout.Button(content, selectedButtonStyle, GUILayout.Width(132))
                : GUILayout.Button(content, selectedButtonStyle, GUILayout.Height(36));
            GUI.enabled = previous;
            if (clicked) ImportBlenderProject();
        }

        void ImportBlenderProject()
        {
            if (bgaExporting || bgaLoading) return;
            ReleaseMouse(); CancelTimelineGesture(); SetPlaying(false);
            showFilesMenu = false; clearGuiFocus = true;
            string blend = ChooseVisualAsset("Import Blender project", "Blender project (*.blend)\0*.blend\0\0");
            if (string.IsNullOrEmpty(blend)) return;
            if (!File.Exists(blend) || !string.Equals(Path.GetExtension(blend), ".blend", StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Select an existing .blend project"); return;
            }
            if (chartCameraPreview) SetPreviewMode(false);
            SetMode(ChartEditMode.Bga); inspectorScroll = Vector2.zero;
            StartCoroutine(ExportBlenderProject(blend));
        }

        void DrawBgaInspector()
        {
            EnsureBlenderBgaData();
            DrawBlenderImportButton(false);
            GUILayout.Label("Blender is the visual source of truth", smallStyle);
            GUILayout.Label(bgaStatus, smallStyle);
            if (blenderBga?.Package != null)
            {
                var package = blenderBga.Package;
                GUILayout.Space(5);
                GUILayout.Label(package.sceneName, headingStyle);
                GUILayout.Label(package.objectCount + " objects · " + package.materialCount + " materials · " + package.cameraCount + " cameras\n" +
                    package.durationSeconds.ToString("0.0") + " s · Blender " + package.blenderVersion, smallStyle);
            }
            GUILayout.Space(9);
            bool previous = GUI.enabled; GUI.enabled = previous && !bgaExporting && !bgaLoading;
            if (GUILayout.Button("Reload exported 3D package")) ReloadBlenderBga();
            GUI.enabled = previous;
            GUILayout.Space(7);
            bool follow = chart.blenderBga.followCamera;
            if (GUILayout.Button(follow ? "BGA camera · FOLLOWING" : "BGA camera · FREE INSPECTION",
                follow ? selectedButtonStyle : buttonStyle))
            {
                RecordUndo(); chart.blenderBga.followCamera = !follow;
                if (!chart.blenderBga.followCamera)
                {
                    orbitPivot = sceneCamera.transform.position + sceneCamera.transform.forward * orbitDistance;
                    ReadViewAngles(); navigationInitialized = true;
                    SetStatus("Free inspection; BGA animation keeps playing");
                }
            }
            float offset = chart.blenderBga.timeOffsetSeconds;
            if (FloatField("Time offset", ref offset)) chart.blenderBga.timeOffsetSeconds = offset;
            GUILayout.Space(8);
            GUILayout.Label("Visuals, effects and camera cuts are read-only here. Edit them in Blender, then re-export. The chart editor only overlays route, paths and notes.", smallStyle);
        }

        IEnumerator ExportBlenderProject(string blendPath)
        {
            bgaExporting = true;
            var importingChart = chart;
            string blender = FindBlenderExecutable();
            string exporter = FindBgaExporter();
            if (blender == null || exporter == null)
            {
                bgaStatus = blender == null ? "Blender executable not found" : "BGA exporter script not found";
                SetStatus(bgaStatus);
                bgaExporting = false; yield break;
            }
            // Keep earlier imports intact, including packages referenced by Undo or other charts.
            string output = Path.Combine(Application.persistentDataPath, "GeometryChartStudio", "BGA",
                Path.GetFileNameWithoutExtension(blendPath), Guid.NewGuid().ToString("N"));
            bgaStatus = "Blender is exporting the 3D project…";
            SetStatus(bgaStatus);
            var info = new ProcessStartInfo
            {
                FileName = blender,
                Arguments = "-b \"" + blendPath + "\" --python-exit-code 1 --python \"" + exporter + "\" -- \"" + output + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process process = null;
            try { Directory.CreateDirectory(output); process = Process.Start(info); }
            catch (Exception e)
            {
                bgaStatus = "Could not start Blender: " + e.Message; SetStatus(bgaStatus); bgaExporting = false; yield break;
            }
            while (!process.HasExited) yield return null;
            try
            {
                if (process.ExitCode != 0) throw new InvalidOperationException("Blender export failed with exit code " + process.ExitCode);
                string manifest = Path.Combine(output, "manifest.json");
                if (!File.Exists(manifest)) throw new FileNotFoundException("Blender did not produce manifest.json");
                if (!ReferenceEquals(chart, importingChart))
                {
                    SetStatus("Blender export completed; chart changed, so BGA was not replaced");
                    yield break;
                }
                RecordUndo(); EnsureBlenderBgaData();
                chart.blenderBga.sourceBlend = Path.GetFullPath(blendPath);
                chart.blenderBga.packageManifest = Path.GetFullPath(manifest);
                chart.blenderBga.enabled = true;
                chart.blenderBga.followCamera = true;
                ReloadBlenderBga();
            }
            catch (Exception e) { bgaStatus = "Blender export failed: " + e.Message; SetStatus(bgaStatus); }
            finally { process?.Dispose(); bgaExporting = false; }
        }

        string FindBlenderExecutable()
        {
            string fromEnvironment = Environment.GetEnvironmentVariable("BLENDER_EXE");
            string[] candidates = { fromEnvironment, @"D:\Programs\Blender\blender.exe", @"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" };
            foreach (string candidate in candidates) if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate)) return candidate;
            return null;
        }

        string FindBgaExporter()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] candidates =
            {
                Path.Combine(projectRoot, "BGA", "tools", "export_unity_bga.py"),
                Path.Combine(projectRoot, "BGA", "Tools", "export_unity_bga.py")
            };
            foreach (string candidate in candidates) if (File.Exists(candidate)) return candidate;
            return null;
        }
    }
}
