using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.Thart
{
    /// <summary>
    /// Thart 铺面导入导出工具
    /// 支持与现有 Geometry Chart Studio 格式互操作
    /// </summary>
    public static class ThartChartIO
    {
        /// <summary>
        /// 将 Thart 录制数据导入为标准 ChartData
        /// </summary>
        public static ChartData ImportFromRecording(
            ThartTouchRecording recording,
            TouchToNoteConfig config,
            float bpm = 120f,
            string title = "Untitled",
            int gridDivisor = 0)
        {
            var chart = new ChartData
            {
                version = 1,
                title = title,
                author = "Thart",
                ticksPerBeat = 480,
                approachSeconds = 3.4f,
                tempos = new[] { new TempoData { tick = 0, bpm = bpm } }
            };

            // 计算拍数
            double duration = recording.durationSeconds;
            double beats = duration * bpm / 60.0;
            chart.endBeat = (float)Math.Ceiling(beats) + 4;

            // 关键：铺面没有固定轨道。先按触控位置做转换，得到自由的列布局。
            TouchNoteLayout layout;
            var noteEvents = ThartChartBuilder.ConvertTouchToNotes(recording, config, out layout);

            chart.paths = layout.paths;
            chart.sections = new[]
            {
                new SectionData
                {
                    startBeat = 0,
                    name = "Section 1",
                    placements = layout.placements
                }
            };

            // 相机
            chart.cameraKeys = new[]
            {
                new CameraKey
                {
                    beat = 0,
                    distance = 25,
                    height = 6,
                    fov = 53,
                    easing = "smooth"
                }
            };

            // 转换音符（pathIndex 已经由布局按真实位置分配好）
            var tempoMap = new TempoMap(chart.tempos, chart.ticksPerBeat);
            chart.notes = ThartChartBuilder.ConvertToChartNotes(
                noteEvents, tempoMap, chart.ticksPerBeat, layout.pathIds, gridDivisor);

            return chart;
        }

        /// <summary>
        /// 导出为 JSON 谱面文件
        /// </summary>
        public static void ExportChartJson(ChartData chart, string path)
        {
            string json = JsonUtility.ToJson(chart, true);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// 从 JSON 谱面文件加载
        /// </summary>
        public static ChartData ImportChartJson(string path)
        {
            string json = File.ReadAllText(path);
            return ChartLoader.Parse(json);
        }

        /// <summary>
        /// 保存触控录制数据为 JSON
        /// </summary>
        public static void SaveRecording(ThartTouchRecording recording, string path)
        {
            string json = JsonUtility.ToJson(recording, true);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// 加载触控录制数据
        /// </summary>
        public static ThartTouchRecording LoadRecording(string path)
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<ThartTouchRecording>(json);
        }

        /// <summary>
        /// 导出为 .grchart 谱面包（ZIP 格式，兼容现有格式）
        /// 注意：完整的 grchart 封装需要视频等资源，这里只导出 JSON
        /// </summary>
        public static void ExportGrchartJson(ChartData chart, string path)
        {
            ExportChartJson(chart, path);
        }
    }
}
