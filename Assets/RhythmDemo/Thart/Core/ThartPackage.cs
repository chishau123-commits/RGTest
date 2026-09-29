using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GeometryRhythm.Thart
{
    /// <summary>
    /// .thr 文件包：一个 ZIP，里面装着「谱面 + 触控原始数据 + 音频」。
    ///
    ///   chart.json   铺面（ChartData）
    ///   touch.json   平板录到的全屏触控原始数据（可选）
    ///   audio/xxx    原始音频文件（可选，保持原格式不再编码）
    ///   meta.json    包信息
    ///
    /// 这样一份 .thr 就是完整可复现的工程：换台电脑打开也能拿到同一首歌和同一份触控。
    /// </summary>
    public static class ThartPackage
    {
        public const string Extension = ".thr";
        public const int FormatVersion = 1;
        public const string FormatTag = "thart";

        public const string ChartEntry = "chart.json";
        public const string TouchEntry = "touch.json";
        public const string MetaEntry = "meta.json";
        public const string AudioDir = "audio/";

        [Serializable]
        public sealed class Meta
        {
            public string format = FormatTag;
            public int version = FormatVersion;
            public string title = "";
            public string author = "";
            public string created = "";
            public bool hasTouch;
            public bool hasAudio;
            public string audioName = "";
        }

        public sealed class Content
        {
            public ChartData Chart;
            public ThartTouchRecording Touch;
            public byte[] AudioBytes;
            public string AudioName = "";
            public Meta Info;
        }

        public static void Save(string path, ChartData chart, ThartTouchRecording touch,
            byte[] audioBytes, string audioName, string title)
        {
            if (chart == null) throw new ArgumentNullException("chart");

            bool hasAudio = audioBytes != null && audioBytes.Length > 0;
            string safeAudioName = hasAudio ? SanitizeFileName(audioName) : "";

            var meta = new Meta
            {
                format = FormatTag,
                version = FormatVersion,
                title = title ?? "",
                author = chart.author ?? "",
                created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                hasTouch = touch != null,
                hasAudio = hasAudio,
                audioName = safeAudioName
            };

            var entries = new List<ThartZip.Entry>
            {
                new ThartZip.Entry(ChartEntry, Utf8(JsonUtility.ToJson(chart, true))),
                new ThartZip.Entry(MetaEntry, Utf8(JsonUtility.ToJson(meta, true)))
            };

            if (touch != null)
                entries.Add(new ThartZip.Entry(TouchEntry, Utf8(JsonUtility.ToJson(touch, true))));

            if (hasAudio)
                entries.Add(new ThartZip.Entry(AudioDir + safeAudioName, audioBytes));

            ThartZip.Write(path, entries);
        }

        public static Content Load(string path)
        {
            var raw = ThartZip.Read(path);
            return FromEntries(raw);
        }

        public static Content FromEntries(Dictionary<string, byte[]> raw)
        {
            var content = new Content();

            byte[] chartBytes;
            if (raw.TryGetValue(ChartEntry, out chartBytes) && chartBytes != null && chartBytes.Length > 0)
            {
                // 这里刻意不用 ChartLoader.Parse：它按游戏运行时的严格规则校验，
                // 会把编辑过程中还没填完整的铺面直接判为非法。编辑器自己读自己的包，
                // 用宽松解析 + 后续修复，才不会出现「刚存的文件打不开」。
                content.Chart = JsonUtility.FromJson<ChartData>(Utf8Decode(chartBytes));
                if (content.Chart == null)
                    throw new InvalidDataException("chart.json 解析失败");
            }
            else
            {
                throw new InvalidDataException(".thr 里没有 chart.json");
            }

            byte[] touchBytes;
            if (raw.TryGetValue(TouchEntry, out touchBytes) && touchBytes != null && touchBytes.Length > 0)
                content.Touch = JsonUtility.FromJson<ThartTouchRecording>(Utf8Decode(touchBytes));

            foreach (var kv in raw)
            {
                if (kv.Key.StartsWith(AudioDir, StringComparison.OrdinalIgnoreCase) && kv.Value != null && kv.Value.Length > 0)
                {
                    content.AudioBytes = kv.Value;
                    content.AudioName = kv.Key.Substring(AudioDir.Length);
                    break;
                }
            }

            byte[] metaBytes;
            if (raw.TryGetValue(MetaEntry, out metaBytes) && metaBytes != null && metaBytes.Length > 0)
            {
                try { content.Info = JsonUtility.FromJson<Meta>(Utf8Decode(metaBytes)); }
                catch { }
            }

            if (content.AudioBytes != null && string.IsNullOrEmpty(content.AudioName))
                content.AudioName = "audio.wav";

            return content;
        }

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "audio.wav";

            string trimmed = Path.GetFileName(name);
            if (string.IsNullOrEmpty(trimmed)) return "audio.wav";

            var sb = new StringBuilder(trimmed.Length);
            foreach (char c in trimmed)
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);

            string result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "audio.wav" : result;
        }

        private static byte[] Utf8(string s)
        {
            return Encoding.UTF8.GetBytes(s ?? "");
        }

        private static string Utf8Decode(byte[] b)
        {
            return Encoding.UTF8.GetString(b);
        }
    }
}