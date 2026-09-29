using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace GeometryRhythm.Thart.Editor
{
    /// <summary>
    /// Thart 电脑端 - 音频导入与推送到平板
    /// </summary>
    public sealed partial class ThartEditorController
    {
        // 当前歌曲
        private string audioFilePath = "";
        private string audioFileName = "";
        private byte[] audioRawBytes;
        private long audioFileBytes;
        private bool audioLoading;

        // 推送到平板的状态
        private int audioEpoch;              // 每次换歌 +1
        private int pushedEpoch = -1;        // 已成功推送的歌曲版本
        private int clientGeneration;        // 每有新设备连上 +1
        private int pushedGeneration = -1;   // 推送成功时的设备代次
        private volatile bool audioPushing;
        private volatile float audioPushProgress;
        private volatile string audioPushError = "";
        private bool startRecordingAfterPush;

        private ThartFileDialog fileDialog;
        private Vector2 songListScroll;

        private ThartFileDialog FileDialog
        {
            get { return fileDialog ?? (fileDialog = new ThartFileDialog()); }
        }

        /// <summary>当前是否已导入音频</summary>
        public bool HasAudio
        {
            get { return currentAudio != null; }
        }

        /// <summary>平板是否已经拿到当前这首歌（必须同时匹配歌曲版本与设备代次）</summary>
        private bool IsAudioPushedToTablet
        {
            get { return audioEpoch > 0 && pushedEpoch == audioEpoch && pushedGeneration == clientGeneration; }
        }

        #region 导入

        /// <summary>
        /// 打开内置文件浏览器选择音频
        /// </summary>
        public void OpenAudioPicker()
        {
            string start = ThartAudioLocator.FindSongsDirectory(true);
            var dlg = FileDialog;
            dlg.Title = "选择音频文件（wav / ogg / mp3）";
            // 必须清掉上一次的过滤（比如打开过 .thr 谱面包），否则这里会只列出 .thr
            dlg.Extensions = null;
            dlg.OnAccepted = path => StartCoroutine(LoadAudioRoutine(path));
            dlg.Open(start);
        }

        /// <summary>
        /// 启动时自动载入上次用过的歌曲；没有记录就取 Songs 目录里的第一首
        /// </summary>
        public void AutoLoadLastSong()
        {
            string last = "";
            try { last = PlayerPrefs.GetString("thart.lastSong", ""); } catch { }

            if (!string.IsNullOrEmpty(last) && File.Exists(last))
            {
                StartCoroutine(LoadAudioRoutine(last));
                return;
            }

            var songs = ThartAudioLocator.ListSongs();
            if (songs.Count > 0)
            {
                Debug.Log("[Thart] 自动载入 Songs 第一首: " + songs[0]);
                StartCoroutine(LoadAudioRoutine(songs[0]));
            }
            else
            {
                Debug.Log("[Thart] Songs 目录里没有音频文件");
            }
        }

        /// <summary>
        /// 直接从路径导入音频
        /// </summary>
        public void ImportAudio(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            StartCoroutine(LoadAudioRoutine(path));
        }

        private IEnumerator LoadAudioRoutine(string path)
        {
            if (!File.Exists(path))
            {
                UpdateStatus("音频文件不存在: " + path);
                yield break;
            }

            audioLoading = true;
            UpdateStatus("正在载入音频: " + Path.GetFileName(path));

            // 读取原始字节（后续要原样推给平板）
            byte[] raw = null;
            try
            {
                raw = File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                audioLoading = false;
                UpdateStatus("读取音频失败: " + e.Message);
                yield break;
            }

            AudioClip clip = null;
            yield return LoadClipRoutine(path, c => clip = c);

            audioLoading = false;

            if (clip == null)
            {
                UpdateStatus("音频解码失败，请确认格式（推荐 wav / ogg）");
                yield break;
            }

            // 上一首歌的音源先停掉
            if (audioSource != null) audioSource.Stop();
            playing = false;
            songTime = 0;

            currentAudio = clip;
            audioFilePath = path;
            audioFileName = Path.GetFileName(path);
            audioRawBytes = raw;
            audioFileBytes = raw.Length;

            // 记住这首歌，下次启动自动载入
            try
            {
                PlayerPrefs.SetString("thart.lastSong", path);
                PlayerPrefs.Save();
            }
            catch { }

            // 换歌后需要重新推送给平板
            audioEpoch++;
            pushedEpoch = -1;
            audioPushError = "";

            // 谱面时长跟着音频走
            if (chart != null)
            {
                float beats = clip.length * (chart.tempos != null && chart.tempos.Length > 0 ? chart.tempos[0].bpm : 120f) / 60f;
                chart.endBeat = Mathf.Max(chart.endBeat, Mathf.Ceil(beats) + 4);
            }

            UpdateStatus("已导入音频: " + audioFileName + "（" + FormatDuration(clip.length) + "，"
                + (raw.Length / 1024f / 1024f).ToString("F1") + " MB）");

            // 平板在线的话马上推过去
            if (server != null && server.ClientCount > 0)
                StartPushAudioToTablet(false);
        }

        private IEnumerator LoadClipRoutine(string path, Action<AudioClip> onDone)
        {
            string uri = ToFileUri(path);
            AudioClip clip = null;
            using (var req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN))
            {
                yield return req.SendWebRequest();
#if UNITY_2020_1_OR_NEWER
                bool ok = req.result == UnityWebRequest.Result.Success;
#else
                bool ok = !req.isNetworkError && !req.isHttpError;
#endif
                if (!ok)
                {
                    Debug.LogWarning("[Thart] 音频载入失败: " + req.error + " -> " + path);
                    onDone?.Invoke(null);
                    yield break;
                }
                clip = DownloadHandlerAudioClip.GetContent(req);
            }
            onDone?.Invoke(clip);
        }

        private static string ToFileUri(string path)
        {
            string full = Path.GetFullPath(path).Replace('\\', '/');
            if (!full.StartsWith("/")) full = "/" + full;
            return "file://" + full;
        }

        #endregion

        #region 推送到平板

        /// <summary>
        /// 把当前音频推给平板
        /// </summary>
        /// <param name="startRecordingWhenDone">推完是否立刻开始录制</param>
        public void StartPushAudioToTablet(bool startRecordingWhenDone)
        {
            if (audioRawBytes == null || audioRawBytes.Length == 0)
            {
                UpdateStatus("还没有导入音频，无法推送");
                return;
            }
            if (server == null || server.ClientCount == 0)
            {
                UpdateStatus("没有已连接的平板，无法推送音频");
                if (startRecordingWhenDone) startRecordingAfterPush = false;
                return;
            }
            if (audioPushing)
            {
                if (startRecordingWhenDone) startRecordingAfterPush = true;
                return;
            }

            pendingAudioPayload = audioRawBytes;
            pendingAudioName = audioFileName;
            pendingAudioEpoch = audioEpoch;
            pendingAudioGeneration = clientGeneration;
            startRecordingAfterPush = startRecordingWhenDone;
            audioPushError = "";
            audioPushProgress = 0f;
            audioPushing = true;

            UpdateStatus("正在推送音频到平板...");
            var t = new Thread(AudioPushThread) { IsBackground = true };
            t.Start();
        }

        // 传给后台线程的数据
        private byte[] pendingAudioPayload;
        private string pendingAudioName;
        private int pendingAudioEpoch;
        private int pendingAudioGeneration;

        private void AudioPushThread()
        {
            bool ok = false;
            try
            {
                Debug.Log("[Thart] 开始推送音频: " + pendingAudioName + " (" + pendingAudioPayload.Length + " bytes)");
                ok = server.BroadcastAudio(pendingAudioPayload, pendingAudioName, p => audioPushProgress = p);
            }
            catch (Exception e)
            {
                audioPushError = e.Message;
            }

            audioPushing = false;

            if (ok)
            {
                pushedEpoch = pendingAudioEpoch;
                pushedGeneration = pendingAudioGeneration;
            }
            else if (string.IsNullOrEmpty(audioPushError))
                audioPushError = "推送中断";

            Debug.Log("[Thart] 音频推送结束: ok=" + ok + " err=" + audioPushError);

            EnqueueMainThread(() =>
            {
                if (ok)
                {
                    UpdateStatus("音频已推送到平板: " + pendingAudioName);
                    if (startRecordingAfterPush)
                    {
                        startRecordingAfterPush = false;
                        ArmAndStart();
                    }
                    else if (!IsAudioPushedToTablet)
                    {
                        // 推送过程中又有新设备连上，补推一次
                        OnTabletReadyForAudio();
                    }
                }
                else
                {
                    UpdateStatus("音频推送失败: " + audioPushError);
                    startRecordingAfterPush = false;
                }
            });
        }

        /// <summary>平板重连后自动补推</summary>
        private void OnTabletReadyForAudio()
        {
            if (audioEpoch > 0 && !IsAudioPushedToTablet && !audioPushing)
                StartPushAudioToTablet(false);
        }

        #endregion

        #region 小工具

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (total / 60).ToString("D2") + ":" + (total % 60).ToString("D2");
        }

        /// <summary>把播放位置换算成谱面里的秒数（用于时间轴）</summary>
        private double GetSongLengthSeconds()
        {
            return currentAudio != null ? currentAudio.length : GetDuration();
        }

        #endregion
    }
}
