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
    /// Thart 服务端（运行在电脑端，接收平板的触控数据）
    /// </summary>
    public sealed class ThartServer : IDisposable
    {
        private TcpListener listener;
        private readonly List<TcpClient> clients = new List<TcpClient>();
        private readonly Dictionary<TcpClient, List<byte>> receiveBuffers = new Dictionary<TcpClient, List<byte>>();
        private Thread listenThread;
        private volatile bool running;

        // 发送锁：音频推送线程与主线程可能同时写同一个 socket，必须串行化
        private readonly object sendLock = new object();

        public int Port { get; private set; }
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public string ErrorMessage { get; private set; }

        // 事件回调（在后台线程触发，需要转到主线程处理）
        public event Action<TcpClient> OnClientConnected;
        public event Action<TcpClient> OnClientDisconnected;
        public event Action<TcpClient, ThartNetworkMessage> OnMessageReceived;
        public event Action<string> OnError;

        /// <summary>
        /// 启动服务端
        /// </summary>
        public bool Start(int port = 0)
        {
            try
            {
                Port = port <= 0 ? ThartNetworkUtils.DefaultPort : port;
                listener = new TcpListener(IPAddress.Any, Port);
                listener.Start();
                running = true;
                State = ConnectionState.Connected;

                listenThread = new Thread(AcceptLoop) { IsBackground = true };
                listenThread.Start();

                return true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                State = ConnectionState.Error;
                OnError?.Invoke(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 停止服务端
        /// </summary>
        public void Stop()
        {
            running = false;
            try
            {
                lock (clients)
                {
                    foreach (var client in clients)
                    {
                        try { client.Close(); } catch { }
                    }
                    clients.Clear();
                }
                listener?.Stop();
            }
            catch { }
            State = ConnectionState.Disconnected;
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// 向所有已连接客户端发送消息
        /// </summary>
        public void BroadcastMessage(ThartMessageType type, string jsonPayload)
        {
            byte[] data = ThartNetworkUtils.SerializeMessage(type, jsonPayload);
            BroadcastRaw(data);
        }

        /// <summary>
        /// 向所有已连接客户端发送原始数据
        /// </summary>
        public void BroadcastRaw(byte[] data)
        {
            BroadcastRaw(data, data.Length);
        }

        /// <summary>
        /// 向所有已连接客户端发送原始数据（可指定有效长度）
        /// </summary>
        public void BroadcastRaw(byte[] data, int length)
        {
            lock (sendLock)
            {
                lock (clients)
                {
                    foreach (var client in clients)
                    {
                        try
                        {
                            client.GetStream().Write(data, 0, length);
                        }
                        catch
                        {
                            // 发送失败的客户端会在接收循环中被清理
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 向所有客户端分片推送音频文件字节
        /// </summary>
        /// <param name="data">音频原始文件字节</param>
        /// <param name="fileName">文件名（含扩展名，客户端据此选择解码器）</param>
        /// <param name="onProgress">进度回调 0~1（在调用线程触发）</param>
        public bool BroadcastAudio(byte[] data, string fileName, Action<float> onProgress)
        {
            if (data == null || data.Length == 0) return false;

            string meta = "{\"name\":\"" + EscapeJson(fileName) + "\",\"ext\":\"" + EscapeJson(GetExtension(fileName)) + "\",\"total\":" + data.Length + "}";
            BroadcastMessage(ThartMessageType.AudioBegin, meta);

            const int chunkSize = ThartNetworkUtils.AudioChunkSize;
            byte[] chunkBuffer = new byte[5 + chunkSize];
            int offset = 0;

            try
            {
                while (offset < data.Length && running)
                {
                    int size = Math.Min(chunkSize, data.Length - offset);
                    ThartNetworkUtils.WriteHeader(chunkBuffer, ThartMessageType.AudioChunk, size);
                    Buffer.BlockCopy(data, offset, chunkBuffer, 5, size);
                    BroadcastRaw(chunkBuffer, 5 + size);
                    offset += size;
                    onProgress?.Invoke(offset / (float)data.Length);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke("Audio broadcast failed: " + ex.Message);
                return false;
            }

            BroadcastMessage(ThartMessageType.AudioEnd, "{\"total\":" + data.Length + "}");
            onProgress?.Invoke(1f);
            return true;
        }

        public void NotifyAudioUnloaded()
        {
            BroadcastMessage(ThartMessageType.AudioUnload, "");
        }

        private static string GetExtension(string fileName)
        {
            int dot = fileName.LastIndexOf('.');
            return dot >= 0 ? fileName.Substring(dot + 1).ToLowerInvariant() : "wav";
        }

        private static string EscapeJson(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>
        /// 向指定客户端发送消息
        /// </summary>
        public bool SendMessage(TcpClient client, ThartMessageType type, string jsonPayload)
        {
            try
            {
                byte[] data = ThartNetworkUtils.SerializeMessage(type, jsonPayload);
                lock (sendLock)
                {
                    client.GetStream().Write(data, 0, data.Length);
                }
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke("Send failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取已连接客户端数量
        /// </summary>
        public int ClientCount
        {
            get
            {
                lock (clients) return clients.Count;
            }
        }

        private void AcceptLoop()
        {
            while (running)
            {
                try
                {
                    var client = listener.AcceptTcpClient();
                    // 触控采样很密集，Nagle 会把小包攒起来再发，表现为「点了半天电脑端才收到」
                    try { client.NoDelay = true; } catch { }
                    lock (clients)
                    {
                        clients.Add(client);
                    }
                    // receiveBuffers 在接收线程里是用 lock(receiveBuffers) 保护的，
                    // 这里必须用同一把锁写入，否则字典会被并发读写破坏。
                    lock (receiveBuffers)
                    {
                        receiveBuffers[client] = new List<byte>();
                    }

                    OnClientConnected?.Invoke(client);

                    // 为每个客户端启动接收线程
                    var receiveThread = new Thread(() => ReceiveLoop(client)) { IsBackground = true };
                    receiveThread.Start();
                }
                catch (SocketException)
                {
                    // 只有 Stop() 关闭监听时才算正常退出。
                    // 运行中偶发的 SocketException（例如系统短暂资源紧张）不能结束整个
                    // 接受循环，否则服务端会从此再也不接受任何连接，表现为「平板一直连不上」。
                    if (!running) break;
                    Thread.Sleep(200);
                }
                catch (Exception ex)
                {
                    if (!running) break;
                    OnError?.Invoke("Accept error: " + ex.Message);
                    Thread.Sleep(200);
                }
            }
        }

        private void ReceiveLoop(TcpClient client)
        {
            var stream = client.GetStream();
            var buffer = new byte[64 * 1024];

            try { client.ReceiveBufferSize = 256 * 1024; } catch { }

            while (running && client.Connected)
            {
                try
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0) break;

                    List<byte> clientBuffer;
                    lock (receiveBuffers)
                    {
                        if (!receiveBuffers.TryGetValue(client, out clientBuffer))
                        {
                            clientBuffer = new List<byte>();
                            receiveBuffers[client] = clientBuffer;
                        }
                    }

                    lock (clientBuffer)
                    {
                        clientBuffer.AddRange(new ArraySegment<byte>(buffer, 0, bytesRead));

                        // 尝试解析完整消息
                        while (ThartNetworkUtils.TryParseMessage(clientBuffer, out var message))
                        {
                            OnMessageReceived?.Invoke(client, message);
                        }
                    }
                }
                catch
                {
                    break;
                }
            }

            // 客户端断开
            lock (clients)
            {
                clients.Remove(client);
            }
            lock (receiveBuffers)
            {
                receiveBuffers.Remove(client);
            }
            try { client.Close(); } catch { }
            OnClientDisconnected?.Invoke(client);
        }
    }
}
