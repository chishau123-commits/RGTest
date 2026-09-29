using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace GeometryRhythm.Thart.Network
{
    /// <summary>
    /// 网络消息类型
    /// </summary>
    public enum ThartMessageType : byte
    {
        Hello = 1,        // 连接握手
        StartRecording = 2,   // 开始录制命令（服务器→客户端）
        StopRecording = 3,    // 停止录制命令（服务器→客户端）
        TouchSample = 4,      // 触控采样数据（客户端→服务器，实时）
        RecordingComplete = 5, // 录制完成，发送完整数据（客户端→服务器）
        Ping = 6,             // 心跳
        Pong = 7,             // 心跳响应
        Acknowledge = 8,      // 确认收到
        Error = 9,            // 错误信息
        RequestFullData = 10,  // 请求完整数据（服务器→客户端）
        SyncTime = 11,         // 时间同步
        AudioBegin = 12,       // 音频传输开始（服务器→客户端），payload 为 JSON 元信息
        AudioChunk = 13,       // 音频分片（服务器→客户端），payload 为原始文件字节
        AudioEnd = 14,         // 音频传输结束（服务器→客户端）
        AudioUnload = 15,      // 通知客户端清空已缓存音频（服务器→客户端）
        PrepareRecording = 16, // 预准备：客户端确认就绪后再正式开始（服务器→客户端）
        SessionAbort = 17,     // 取消当前会话（双向）
    }

    /// <summary>
    /// 网络消息基类
    /// </summary>
    public sealed class ThartNetworkMessage
    {
        public ThartMessageType Type;
        public byte[] Payload;
    }

    /// <summary>
    /// 录制状态
    /// </summary>
    public enum RecordingState
    {
        Idle,
        Recording,
        Stopping,
        Error
    }

    /// <summary>
    /// 连接状态
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }

    /// <summary>
    /// 网络工具类
    /// </summary>
    public static class ThartNetworkUtils
    {
        public const int DefaultPort = 28765; // Thart 的默认端口

        /// <summary>单条消息负载上限（防止长度字段损坏导致巨额分配）</summary>
        public const int MaxPayloadLength = 64 * 1024 * 1024;

        /// <summary>音频分片大小：256KB</summary>
        public const int AudioChunkSize = 256 * 1024;

        /// <summary>
        /// 获取本机局域网 IP 地址
        /// </summary>
        public static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        /// <summary>
        /// 序列化消息为字节数组
        /// </summary>
        public static byte[] SerializeMessage(ThartMessageType type, string jsonPayload)
        {
            byte[] payloadBytes = string.IsNullOrEmpty(jsonPayload) ? new byte[0] : Encoding.UTF8.GetBytes(jsonPayload);
            return SerializeMessage(type, payloadBytes);
        }

        /// <summary>
        /// 序列化消息为字节数组
        /// </summary>
        public static byte[] SerializeMessage(ThartMessageType type, byte[] payload)
        {
            int payloadLength = payload != null ? payload.Length : 0;
            byte[] result = new byte[5 + payloadLength];
            result[0] = (byte)type;
            BitConverter.GetBytes(payloadLength).CopyTo(result, 1);
            if (payload != null && payloadLength > 0)
                payload.CopyTo(result, 5);
            return result;
        }

        /// <summary>
        /// 直接把消息头写入目标数组（用于分片发送，避免重复分配）
        /// </summary>
        public static void WriteHeader(byte[] target, ThartMessageType type, int payloadLength)
        {
            target[0] = (byte)type;
            target[1] = (byte)(payloadLength & 0xFF);
            target[2] = (byte)((payloadLength >> 8) & 0xFF);
            target[3] = (byte)((payloadLength >> 16) & 0xFF);
            target[4] = (byte)((payloadLength >> 24) & 0xFF);
        }

        /// <summary>
        /// 尝试从字节流中解析一条完整消息
        /// 注意：这里必须避免把整个缓冲区复制一遍（音频传输可达几十 MB）
        /// </summary>
        public static bool TryParseMessage(List<byte> buffer, out ThartNetworkMessage message)
        {
            message = null;
            if (buffer.Count < 5) return false;

            // 直接按下标读长度，避免 ToArray() 带来的整包复制
            int payloadLength = buffer[1] | (buffer[2] << 8) | (buffer[3] << 16) | (buffer[4] << 24);
            if (payloadLength < 0 || payloadLength > MaxPayloadLength)
            {
                // 长度异常，直接丢弃缓冲区，避免协议错位后无限增长
                buffer.Clear();
                return false;
            }

            if (buffer.Count < 5 + payloadLength) return false;

            var type = (ThartMessageType)buffer[0];
            byte[] payload = new byte[payloadLength];
            if (payloadLength > 0)
                buffer.CopyTo(5, payload, 0, payloadLength);

            message = new ThartNetworkMessage { Type = type, Payload = payload };
            buffer.RemoveRange(0, 5 + payloadLength);
            return true;
        }

        /// <summary>
        /// 读取消息负载为字符串
        /// </summary>
        public static string GetPayloadString(ThartNetworkMessage msg)
        {
            if (msg.Payload == null || msg.Payload.Length == 0) return "";
            return Encoding.UTF8.GetString(msg.Payload);
        }
    }
}
