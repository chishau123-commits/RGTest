using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using GeometryRhythm.Thart.Network;

namespace GeometryRhythm.Thart.TouchRecorder
{
    /// <summary>
    /// 平板端 - 接收电脑端推送的音频，本地落盘并解码播放
    /// 录制开始时在平板端播放，倒计时与音频同时开始
    /// </summary>
    public sealed partial class ThartTouchRecorder
    {
        private AudioClip songClip;
        private string songFilePath = "";
        private FileStream audioFileStream;
        private string pendingAudioPath = "";
        private int audioReceivedBytes;
        private string pendingAudioExt = "wav";
        private int pendingAudioTotal;
        private bool audioLoadingClip;
        private string queuedSongPath = "";

        /// <summary>音频是否已经可以在平板端播放</summary>
        private bool IsSongReady
        {
            get { return songClip != null; }
        }

        #region 接收

        private void HandleAudioBegin(string json)
        {
            pendingAudioExt = GetJsonString(json, "ext", "wav");
            pendingAudioTotal = (int)GetJsonFloat(json, "total", 0);
            CloseAudioStream();

            string ext = string.IsNullOrEmpty(pendingAudioExt) ? "wav" : pendingAudioExt;
            pendingAudioPath = Path.Combine(Application.persistentDataPath, "thart_song." + ext);
            audioReceivedBytes = 0;

            // 边收边写盘。以前是全塞进 List<byte>，20MB 的音频每收一片都要把已有内容
            // 整段复制一次（O(n²)），平板上会卡到把连接拖断 —— 这正是「连不上/收一半就掉」的主因。
            try
            {
                audioFileStream = new FileStream(pendingAudioPath, FileMode.Create, FileAccess.Write,
                    FileShare.None, 1 << 16);
            }
            catch (Exception e)
            {
                Debug.LogError("[ThartRec] 创建音频文件失败: " + e.Message);
                audioFileStream = null;
                audioStateText = "音频保存失败";
                statusText = "音频保存失败";
                ReplyAudioFailed();
                return;
            }

            audioStateText = "正在接收音频...";
            statusText = "正在接收音频...";
            Debug.Log("[ThartRec] 开始接收音频: ext=" + pendingAudioExt + " total=" + pendingAudioTotal);
        }

        private void HandleAudioChunk(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || audioFileStream == null) return;

            try
            {
                audioFileStream.Write(payload, 0, payload.Length);
                audioReceivedBytes += payload.Length;
            }
            catch (Exception e)
            {
                Debug.LogError("[ThartRec] 写入音频失败: " + e.Message);
                CloseAudioStream();
                audioStateText = "音频保存失败";
                statusText = "音频保存失败";
                ReplyAudioFailed();
            }
        }

        private void HandleAudioEnd()
        {
            CloseAudioStream();

            if (audioReceivedBytes <= 0 || string.IsNullOrEmpty(pendingAudioPath) || !File.Exists(pendingAudioPath))
            {
                audioStateText = "音频接收失败";
                statusText = "音频接收失败";
                ReplyAudioFailed();
                return;
            }

            // 没收全就交给解码器只会解出半首歌，宁可直接报失败让电脑端重推
            if (pendingAudioTotal > 0 && audioReceivedBytes < pendingAudioTotal)
            {
                Debug.LogError("[ThartRec] 音频不完整: " + audioReceivedBytes + "/" + pendingAudioTotal);
                audioStateText = "音频接收不完整";
                statusText = "音频接收不完整";
                ReplyAudioFailed();
                return;
            }

            Debug.Log("[ThartRec] 音频接收完成: " + audioReceivedBytes + " bytes -> " + pendingAudioPath);

            audioStateText = "正在解码音频...";
            if (audioLoadingClip)
            {
                // 正在解码上一首：先记下来，等它结束再解这一首。
                // 否则同时到达的第二首歌会被直接丢掉，永远处于「音频未就绪」。
                queuedSongPath = pendingAudioPath;
                return;
            }
            StartCoroutine(LoadSongRoutine(pendingAudioPath));
        }

        private void CloseAudioStream()
        {
            if (audioFileStream == null) return;
            try
            {
                audioFileStream.Flush();
                audioFileStream.Dispose();
            }
            catch { }
            audioFileStream = null;
        }

        private IEnumerator LoadSongRoutine(string path)
        {
            audioLoadingClip = true;

            // 解码期间可能又推来一首新歌（queuedSongPath），循环处理掉，
            // 保证最后落地的永远是「最新收到的那一首」。
            while (!string.IsNullOrEmpty(path))
            {
                queuedSongPath = "";

                // 换歌前先停掉旧的
                if (audioSource != null) audioSource.Stop();
                if (songClip != null)
                {
                    Destroy(songClip);
                    songClip = null;
                }

                string uri = "file://" + path.Replace('\\', '/');
                AudioClip clip = null;

                using (var req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN))
                {
                    yield return req.SendWebRequest();
#if UNITY_2020_1_OR_NEWER
                    bool ok = req.result == UnityWebRequest.Result.Success;
#else
                    bool ok = !req.isNetworkError && !req.isHttpError;
#endif
                    if (ok)
                    {
                        clip = DownloadHandlerAudioClip.GetContent(req);
                    }
                    else
                    {
                        Debug.LogError("[ThartRec] 音频解码失败: " + req.error);
                    }
                }

                if (clip != null)
                {
                    songClip = clip;
                    songFilePath = path;
                    audioStateText = "音频已就绪 " + FormatDuration(clip.length);
                    statusText = "音频已就绪，等待开始录制";
                    Debug.Log("[ThartRec] 音频已就绪: " + clip.length.ToString("F2") + "s, " + clip.frequency + "Hz, " + clip.channels + "ch -> " + path);
                    ReplyAudioReady();
                }
                else
                {
                    audioStateText = "音频解码失败（请用 wav / ogg）";
                    statusText = "音频解码失败";
                    ReplyAudioFailed();
                }

                // 解码期间又来了新歌就继续解新的
                path = queuedSongPath;
            }

            audioLoadingClip = false;
        }

        private void UnloadSong()
        {
            if (audioSource != null) audioSource.Stop();
            if (songClip != null)
            {
                Destroy(songClip);
                songClip = null;
            }
            CloseAudioStream();
            queuedSongPath = "";
            songFilePath = "";
            audioStateText = "音频未加载";
        }

        private void ReplyAudioReady()
        {
            if (client != null && client.State == ConnectionState.Connected)
                client.SendMessage(ThartMessageType.Acknowledge, "{\"audio\":\"ok\"}");
        }

        private void ReplyAudioFailed()
        {
            if (client != null && client.State == ConnectionState.Connected)
                client.SendMessage(ThartMessageType.Acknowledge, "{\"audio\":\"fail\"}");
        }

        #endregion

        #region 播放

        /// <summary>
        /// 在指定 dsp 时刻开始播放歌曲（与录制时间原点对齐）
        /// </summary>
        private void StartSongPlayback(double dspTime)
        {
            if (audioSource == null) return;

            audioSource.Stop();

            if (songClip == null)
            {
                Debug.LogWarning("[ThartRec] 没有音频可播放");
                return;
            }

            audioSource.clip = songClip;
            audioSource.time = 0f;
            audioSource.PlayScheduled(dspTime);
            Debug.Log("[ThartRec] 开始播放音频 @ dsp=" + dspTime.ToString("F3") + " (now=" + AudioSettings.dspTime.ToString("F3") + ")");
        }

        private void StopSongPlayback()
        {
            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();
        }

        /// <summary>倒计时剩余秒数（已开始录制时才有意义）</summary>
        private float GetCountdownRemaining()
        {
            if (!inCountdown) return 0f;
            float elapsed = (float)(AudioSettings.dspTime - recordingStartTime);
            return Mathf.Max(0f, countdownSeconds - elapsed);
        }

        #endregion

        #region 极简 JSON 取值

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (total / 60).ToString("D2") + ":" + (total % 60).ToString("D2");
        }

        private static float GetJsonFloat(string json, string key, float fallback)
        {
            if (string.IsNullOrEmpty(json)) return fallback;
            string token = "\"" + key + "\"";
            int i = json.IndexOf(token, StringComparison.Ordinal);
            if (i < 0) return fallback;
            i = json.IndexOf(':', i + token.Length);
            if (i < 0) return fallback;
            i++;
            int end = i;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '+' || json[end] == '.' || json[end] == 'e' || json[end] == 'E'))
                end++;
            float v;
            if (float.TryParse(json.Substring(i, end - i), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;
            return fallback;
        }

        private static string GetJsonString(string json, string key, string fallback)
        {
            if (string.IsNullOrEmpty(json)) return fallback;
            string token = "\"" + key + "\"";
            int i = json.IndexOf(token, StringComparison.Ordinal);
            if (i < 0) return fallback;
            i = json.IndexOf(':', i + token.Length);
            if (i < 0) return fallback;
            i = json.IndexOf('"', i);
            if (i < 0) return fallback;
            int end = json.IndexOf('"', i + 1);
            if (end < 0) return fallback;
            return json.Substring(i + 1, end - i - 1);
        }

        #endregion
    }
}
