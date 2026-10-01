using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.Thart.Editor
{
    /// <summary>
    /// Thart 电脑端 - 谱面包（.thr）的新建 / 打开 / 保存 / 另存为
    ///
    /// 一份 .thr 里装着「谱面 + 触控原始数据 + 音频」，所以换台电脑打开也能
    /// 拿到同一首歌和同一份触控，而不是只拿到一个音符列表。
    /// </summary>
    public sealed partial class ThartEditorController
    {
        /// <summary>谱面包存放目录：可执行文件同级的 Charts/</summary>
        private static string ChartsDirectory()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Charts"));
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        #region 新建

        public void NewChart()
        {
            PushUndo();

            LoadOrCreateChart();
            DisposeVideoBga();
            bgaSourceDirectory = null;

            currentRecording = null;
            convertedNotes.Clear();
            convertedLayout = null;
            chartFilePath = "";
            ClearNoteSelection();
            selectedPathIndex = 0;

            StopPlayback();
            songTime = 0;

            UpdateStatus("已新建空白谱面（音频保持不变）");
        }

        #endregion

        #region 打开

        public void OpenChartPackage()
        {
            string dir = ChartsDirectory();

            var dlg = FileDialog;
            dlg.Title = "打开 .thr 谱面包";
            // 不设 Extensions 的话，文件浏览器会退回音频扩展名过滤，.thr 根本列不出来
            dlg.Extensions = new[] { ThartPackage.Extension };
            dlg.OnAccepted = LoadChartPackage;
            dlg.Open(dir);
        }

        private void LoadChartPackage(string path)
        {
            try
            {
                var content = ThartPackage.Load(path);

                PushUndo();

                chart = content.Chart;
                currentRecording = content.Touch;
                convertedNotes.Clear();
                convertedLayout = null;
                ClearNoteSelection();
                selectedPathIndex = 0;
                chartFilePath = path;

                StopPlayback();
                songTime = 0;

                // 老包/空包可能没有列定义，补一份最简布局，否则时间轴与编辑面板是空的
                EnsureMinimumPaths(chart.paths != null ? chart.paths.Length : 0);
                RebuildTempoMap();

                // 包里有触控数据就重算一次，让编辑面板的列与转换结果和录制时一致
                if (currentRecording != null && currentRecording.samples != null &&
                    currentRecording.samples.Length > 0)
                {
                    convertedNotes = ThartChartBuilder.ConvertTouchToNotes(
                        currentRecording, conversionConfig, out convertedLayout);
                }

                if (content.AudioBytes != null && content.AudioBytes.Length > 0)
                {
                    // 音频原始字节已经拿回来了：直接记下，保证「另存为」不会再丢音频
                    audioFileName = content.AudioName;
                    audioRawBytes = content.AudioBytes;
                    audioFileBytes = content.AudioBytes.Length;
                    audioEpoch++;
                    pushedEpoch = -1;
                    audioPushError = "";
                    StartCoroutine(LoadPackageAudioRoutine(content.AudioBytes, content.AudioName));
                }

                // 包内有 BGA 槽位就展开再绑定；老包没有就卸载，免得画面里还留着上一张谱面的背景。
                // 谱面若把 BGA 记成包外路径（早期手工写的绝对路径），也在这里兜一下。
                bool hasBga = content.BgaEntries != null && content.BgaEntries.Count > 0;
                if (hasBga)
                {
                    MaterializeBgaFromPackage(content.BgaEntries);
                }
                else
                {
                    DisposeVideoBga();
                    bgaSourceDirectory = null;
                    string external = ChartVideoBgaManifest();
                    if (!string.IsNullOrEmpty(external)) StartCoroutine(BindVideoBga(external));
                }

                UpdateStatus("已打开: " + Path.GetFileName(path)
                    + (content.AudioBytes != null ? "（含音频）" : "")
                    + (content.Touch != null ? "（含触控）" : "")
                    + (hasBga ? "（含BGA）" : ""));
            }
            catch (Exception e)
            {
                UpdateStatus("打开失败: " + e.Message);
            }
        }

        /// <summary>
        /// 把包里的音频字节落成文件再交给常规载入流程。
        /// 复用 LoadAudioRoutine 是为了让 currentAudio / audioFileName / 推送版本号
        /// 一次性全部对齐，不必在这里再抄一遍。
        /// </summary>
        private IEnumerator LoadPackageAudioRoutine(byte[] bytes, string name)
        {
            string cacheDir = Path.Combine(Application.persistentDataPath, "ThartAudioCache");
            string path;
            try
            {
                if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
                path = Path.Combine(cacheDir, ThartPackage.SanitizeFileName(name));
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception e)
            {
                UpdateStatus("还原谱面包音频失败: " + e.Message);
                yield break;
            }

            yield return StartCoroutine(LoadAudioRoutine(path));
        }

        #endregion

        #region 保存

        public void SaveChartPackage()
        {
            if (chart == null) return;

            // 没有路径、或路径不是 .thr（例如以前存过 .json），都当作「另存为」
            if (string.IsNullOrEmpty(chartFilePath) ||
                !chartFilePath.EndsWith(ThartPackage.Extension, StringComparison.OrdinalIgnoreCase))
            {
                SaveChartPackageAs();
                return;
            }

            SavePackageTo(chartFilePath);
        }

        public void SaveChartPackageAs()
        {
            if (chart == null) return;

            string dir = ChartsDirectory();
            string baseName = string.IsNullOrEmpty(chart.title) ? "thart_chart" : chart.title;
            baseName = ThartPackage.SanitizeFileName(baseName);
            // SanitizeFileName 不保证扩展名被去掉（标题里可能自带），这里再削一次，
            // 否则会存成 xxx.thr.thr
            if (baseName.EndsWith(ThartPackage.Extension, StringComparison.OrdinalIgnoreCase))
                baseName = baseName.Substring(0, baseName.Length - ThartPackage.Extension.Length);

            var dlg = FileDialog;
            dlg.Title = "另存为 .thr 谱面包";
            dlg.OnAccepted = SavePackageTo;
            dlg.OpenForSave(dir, baseName, new[] { ThartPackage.Extension });
        }

        private void SavePackageTo(string path)
        {
            if (chart == null || string.IsNullOrEmpty(path)) return;

            try
            {
                string title = string.IsNullOrEmpty(chart.title) ? "Thart Chart" : chart.title;
                var bga = CollectBgaEntries();
                ThartPackage.Save(path, chart, currentRecording, audioRawBytes, audioFileName, title, bga);
                chartFilePath = path;
                UpdateStatus("已保存: " + Path.GetFileName(path)
                    + "（" + (currentRecording != null ? "含触控 " : "")
                    + (audioRawBytes != null && audioRawBytes.Length > 0 ? "含音频" : "无音频")
                    + (bga != null ? " 含BGA" : "")
                    + "）");
            }
            catch (Exception e)
            {
                UpdateStatus("保存失败: " + e.Message);
            }
        }

        #endregion
    }
}