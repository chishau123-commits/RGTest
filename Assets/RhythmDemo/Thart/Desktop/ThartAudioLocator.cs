using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.Thart.Editor
{
    /// <summary>
    /// 定位 Thart 的歌曲目录
    /// 约定：优先使用「可执行文件所在目录/../Songs」，找不到就向上逐级查找名为 Songs 的目录
    /// </summary>
    internal static class ThartAudioLocator
    {
        /// <summary>
        /// 查找歌曲目录；找不到时可选择创建
        /// </summary>
        public static string FindSongsDirectory(bool createIfMissing = false)
        {
            var candidates = GetCandidates();
            foreach (var dir in candidates)
            {
                try
                {
                    if (Directory.Exists(dir)) return dir;
                }
                catch { }
            }

            if (createIfMissing && candidates.Count > 0)
            {
                try
                {
                    Directory.CreateDirectory(candidates[0]);
                    return candidates[0];
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// 列出歌曲目录下的音频文件
        /// </summary>
        public static List<string> ListSongs()
        {
            var result = new List<string>();
            string dir = FindSongsDirectory();
            if (string.IsNullOrEmpty(dir)) return result;

            try
            {
                foreach (var f in Directory.GetFiles(dir))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".wav" || ext == ".ogg" || ext == ".mp3" || ext == ".aiff" || ext == ".aif" || ext == ".flac" || ext == ".m4a")
                        result.Add(f);
                }
                result.Sort(System.StringComparer.OrdinalIgnoreCase);
            }
            catch { }

            return result;
        }

        private static List<string> GetCandidates()
        {
            var candidates = new List<string>();
            string baseDir = null;
            try { baseDir = Path.GetDirectoryName(Application.dataPath); } catch { }

            string cur = baseDir;
            for (int i = 0; i < 5 && !string.IsNullOrEmpty(cur); i++)
            {
                try { candidates.Add(Path.Combine(cur, "Songs")); } catch { }
                try { cur = Path.GetDirectoryName(cur); } catch { cur = null; }
            }

            // 最后兜底到当前工作目录
            try { candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "Songs")); } catch { }

            return candidates;
        }
    }
}
