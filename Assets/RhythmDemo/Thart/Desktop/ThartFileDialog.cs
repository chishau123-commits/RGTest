using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GeometryRhythm.Thart.Editor
{
    internal enum ThartDialogMode
    {
        Open,
        Save
    }

    /// <summary>
    /// Thart 内置文件浏览器（纯 IMGUI 实现，不依赖系统对话框）
    /// 同时支持「打开」和「保存」两种模式：保存模式下可以输入文件名并自动补扩展名。
    /// </summary>
    internal sealed class ThartFileDialog
    {
        private static readonly string[] AudioExtensions =
        {
            ".wav", ".ogg", ".mp3", ".aiff", ".aif", ".flac", ".m4a", ".aac"
        };

        public bool IsOpen { get; private set; }
        public string Title = "选择文件";
        public ThartDialogMode Mode = ThartDialogMode.Open;

        /// <summary>允许的文件扩展名（小写、含点）。为空则用音频扩展名。</summary>
        public string[] Extensions;

        /// <summary>保存模式下的默认文件名（不含扩展名）</summary>
        public string DefaultFileName = "";

        public Action<string> OnAccepted;

        private string currentDir = "";
        private string pathInput = "";
        private Vector2 listScroll;
        private readonly List<string> folders = new List<string>();
        private readonly List<string> files = new List<string>();
        private string selectedFile = "";
        private string saveName = "";
        private string error = "";
        private string notice = "";
        private bool listDirty = true;
        private bool overwriteArmed;

        public void Open(string startDir)
        {
            IsOpen = true;
            Mode = ThartDialogMode.Open;
            selectedFile = "";
            saveName = "";
            error = "";
            notice = "";
            overwriteArmed = false;
            Navigate(string.IsNullOrEmpty(startDir) ? GetDefaultDir() : startDir);
        }

        public void OpenForSave(string startDir, string defaultName, string[] extensions)
        {
            IsOpen = true;
            Mode = ThartDialogMode.Save;
            Extensions = extensions;
            selectedFile = "";
            saveName = string.IsNullOrEmpty(defaultName) ? "" : defaultName;
            error = "";
            notice = "";
            overwriteArmed = false;
            Navigate(string.IsNullOrEmpty(startDir) ? GetDefaultDir() : startDir);
        }

        public void Close()
        {
            IsOpen = false;
        }

        private static string GetDefaultDir()
        {
            try
            {
                string songs = ThartAudioLocator.FindSongsDirectory();
                if (!string.IsNullOrEmpty(songs)) return songs;
                return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            }
            catch
            {
                return "C:\\";
            }
        }

        private string[] EffectiveExtensions
        {
            get { return Extensions != null && Extensions.Length > 0 ? Extensions : AudioExtensions; }
        }

        private string DefaultExtension
        {
            get
            {
                var exts = EffectiveExtensions;
                return exts.Length > 0 ? exts[0] : "";
            }
        }

        private void Navigate(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;

            try
            {
                string full = Path.GetFullPath(dir);
                if (!Directory.Exists(full))
                {
                    error = "目录不存在: " + dir;
                    return;
                }
                currentDir = full;
                pathInput = full;
                error = "";
                notice = "";
                overwriteArmed = false;
                listDirty = true;
            }
            catch (Exception e)
            {
                error = "无法打开目录: " + e.Message;
            }
        }

        private void RefreshList()
        {
            listDirty = false;
            folders.Clear();
            files.Clear();

            try
            {
                foreach (var d in Directory.GetDirectories(currentDir))
                {
                    try
                    {
                        // 跳过系统隐藏目录，避免进不去还卡住
                        var attr = new DirectoryInfo(d).Attributes;
                        if ((attr & FileAttributes.System) != 0) continue;
                    }
                    catch { continue; }
                    folders.Add(d);
                }
                folders.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (var f in Directory.GetFiles(currentDir))
                {
                    if (HasAllowedExtension(f)) files.Add(f);
                }
                files.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                error = "读取目录失败: " + e.Message;
            }
        }

        private bool HasAllowedExtension(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return false;
            ext = ext.ToLowerInvariant();
            foreach (var a in EffectiveExtensions)
                if (string.Equals(a, ext, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>保存模式下把用户输入的名字补成完整路径</summary>
        private string BuildSavePath()
        {
            string name = (saveName ?? "").Trim();
            if (string.IsNullOrEmpty(name)) return "";

            // 用户自己带了扩展名就尊重它，否则补默认扩展名
            string ext = Path.GetExtension(name);
            if (string.IsNullOrEmpty(ext)) name += DefaultExtension;

            try { return Path.GetFullPath(Path.Combine(currentDir, name)); }
            catch { return Path.Combine(currentDir, name); }
        }

        /// <summary>
        /// 绘制对话框（传整个屏幕区域，内部自己居中）
        /// </summary>
        public void Draw(Rect screen)
        {
            if (!IsOpen) return;
            if (listDirty) RefreshList();

            // 半透明遮罩，同时吃掉底下的点击
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float w = Mathf.Min(940f, screen.width - 60f);
            float h = Mathf.Min(660f, screen.height - 60f);
            Rect panel = new Rect((screen.width - w) / 2f, (screen.height - h) / 2f, w, h);

            // 只在鼠标落在面板外时才铺这层「拦截按钮」。它必须画在面板内容之前，
            // 否则会盖住面板；但画在前面就会先一步抢走 hotControl，把面板里所有
            // 按钮的 MouseDown 全吃掉（表现为对话框完全点不动）。按位置区分，
            // 面板外的点击被吃掉、面板内的交给面板自己处理。
            if (!panel.Contains(Event.current.mousePosition) &&
                GUI.Button(screen, "", GUIStyle.none))
            {
                /* 阻止穿透 */
            }

            // 面板背景
            GUI.color = new Color(0.11f, 0.11f, 0.16f, 1f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.32f, 0.42f, 1f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(panel.x, panel.y, panel.width, panel.height));

            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label(Title, Styles.Heading);
            GUILayout.FlexibleSpace();
            GUILayout.Space(16);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // 路径栏
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            if (GUILayout.Button("↑ 上级", Styles.Button, GUILayout.Width(90), GUILayout.Height(32)))
            {
                var parent = Directory.GetParent(currentDir);
                if (parent != null) Navigate(parent.FullName);
            }
            GUILayout.Space(8);
            pathInput = GUILayout.TextField(pathInput, Styles.TextField, GUILayout.Height(32));
            GUILayout.Space(8);
            if (GUILayout.Button("转到", Styles.Button, GUILayout.Width(70), GUILayout.Height(32)))
                Navigate(pathInput);
            GUILayout.Space(16);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 快捷入口
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            DrawShortcut("桌面", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            DrawShortcut("文档", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            DrawShortcut("音乐", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            DrawShortcut("下载", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            string songsDir = ThartAudioLocator.FindSongsDirectory();
            if (!string.IsNullOrEmpty(songsDir)) DrawShortcut("工程 Songs", songsDir);
            DrawShortcut("C 盘", "C:\\");
            GUILayout.FlexibleSpace();
            GUILayout.Space(16);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            if (!string.IsNullOrEmpty(error))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(16);
                var old = GUI.color;
                GUI.color = new Color(1f, 0.5f, 0.5f);
                GUILayout.Label(error, Styles.SmallLabel);
                GUI.color = old;
                GUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(notice))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(16);
                var old = GUI.color;
                GUI.color = Styles.Warn;
                GUILayout.Label(notice, Styles.SmallLabel);
                GUI.color = old;
                GUILayout.EndHorizontal();
            }

            // 列表区
            float listHeight = panel.height - (Mode == ThartDialogMode.Save ? 268f : 210f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.BeginVertical(Styles.FileListBox, GUILayout.Width(panel.width - 32f), GUILayout.Height(listHeight));

            // 只在这段滚动视图里换成自定义滚动条皮肤，别的控件样式不受影响
            var originalSkin = GUI.skin;
            if (Styles.ScrollSkin != null) GUI.skin = Styles.ScrollSkin;
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
            GUILayout.Space(6);

            if (folders.Count == 0 && files.Count == 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label("（此目录没有子文件夹或匹配的文件）", Styles.SmallLabel);
                GUILayout.EndHorizontal();
            }

            foreach (var dir in folders)
            {
                string name = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(name)) name = dir;
                GUILayout.BeginHorizontal();
                GUILayout.Space(12);
                if (GUILayout.Button("[ 目录 ]  " + name, Styles.FolderRow, GUILayout.Height(30)))
                    Navigate(dir);
                GUILayout.FlexibleSpace();
                GUILayout.Space(12);
                GUILayout.EndHorizontal();
            }

            foreach (var file in files)
            {
                bool isSel = file == selectedFile;
                GUILayout.BeginHorizontal();
                GUILayout.Space(12);
                var old = GUI.color;
                if (isSel) GUI.color = new Color(0.35f, 0.75f, 1f);
                if (GUILayout.Button(Path.GetFileName(file), Styles.FileRow, GUILayout.Height(30)))
                {
                    selectedFile = file;
                    if (Mode == ThartDialogMode.Save)
                        saveName = Path.GetFileNameWithoutExtension(file);
                    error = "";
                    notice = "";
                    overwriteArmed = false;
                }
                GUI.color = old;
                GUILayout.FlexibleSpace();
                GUILayout.Space(12);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            GUILayout.EndScrollView();
            GUI.skin = originalSkin;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // 保存模式：文件名输入
            if (Mode == ThartDialogMode.Save)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(16);
                GUILayout.Label("文件名", Styles.SmallLabel, GUILayout.Width(60));
                GUILayout.Space(4);
                saveName = GUILayout.TextField(saveName ?? "", Styles.TextField, GUILayout.Height(32));
                GUILayout.Space(8);
                GUILayout.Label(DefaultExtension, Styles.SmallLabel, GUILayout.Width(52));
                GUILayout.Space(16);
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
            }

            // 底部
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);

            string shown;
            if (Mode == ThartDialogMode.Save)
            {
                string target = BuildSavePath();
                shown = string.IsNullOrEmpty(target) ? "未填写文件名" : target;
            }
            else
            {
                shown = string.IsNullOrEmpty(selectedFile) ? "未选择文件" : Path.GetFileName(selectedFile);
            }
            GUILayout.Label(shown, Styles.Label);
            GUILayout.FlexibleSpace();

            bool canAccept = Mode == ThartDialogMode.Save
                ? !string.IsNullOrEmpty(BuildSavePath())
                : !string.IsNullOrEmpty(selectedFile);

            GUI.enabled = canAccept;
            if (GUILayout.Button(Mode == ThartDialogMode.Save ? "保存" : "打开",
                Styles.BigButton, GUILayout.Width(120), GUILayout.Height(38)))
            {
                if (Mode == ThartDialogMode.Save)
                {
                    string target = BuildSavePath();
                    // 覆盖已有文件需要点两次，避免手滑
                    if (File.Exists(target) && !overwriteArmed)
                    {
                        overwriteArmed = true;
                        notice = "「" + Path.GetFileName(target) + "」已存在，再点一次「保存」覆盖";
                    }
                    else
                    {
                        Close();
                        OnAccepted?.Invoke(target);
                    }
                }
                else
                {
                    string picked = selectedFile;
                    Close();
                    OnAccepted?.Invoke(picked);
                }
            }
            GUI.enabled = true;

            GUILayout.Space(10);
            if (GUILayout.Button("取消", Styles.Button, GUILayout.Width(100), GUILayout.Height(38)))
                Close();

            GUILayout.Space(16);
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.EndArea();
        }

        private void DrawShortcut(string label, string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            if (GUILayout.Button(label, Styles.SmallButton, GUILayout.Width(88), GUILayout.Height(26)))
                Navigate(dir);
            GUILayout.Space(6);
        }
    }
}