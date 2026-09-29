using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace GeometryRhythm.Thart.Network
{
    /// <summary>
    /// Thart 客户端（运行在平板端，发送触控数据到电脑）
    /// </summary>
    public sealed class ThartClient : IDisposable
    {
        private TcpClient client;
        private NetworkStream stream;
        private List<byte> receiveBuffer = new List<byte>();
        private Thread receiveThread;
        private volatile bool running;

        public string ServerAddress { get; private set; }
        public int ServerPort { get; private set; }
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public string ErrorMessage { get; private set; }

        // 事件回调（在后台线程触发）
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<ThartNetworkMessage> OnMessageReceived;
        public event Action<string> OnError;

        /// <summary>
        /// 连接到服务端
        /// </summary>
        public bool Connect(string address, int port = 0)
        {
            try
            {
                ServerAddress = address;
                ServerPort = port <= 0 ? ThartNetworkUtils.DefaultPort : port;
                State = ConnectionState.Connecting;

                client = new TcpClient();
                var connectResult = client.BeginConnect(address, ServerPort, null, null);
                bool success = connectResult.AsyncWaitHandle.WaitOne(3000); // 3秒超时

                if (!success || !client.Connected)
                {
                    // 超时/失败时必须把 socket 收掉，否则每重试一次就漏一个句柄，
                    // 重试几十次后系统资源耗尽，连新连接都建不起来。
                    try { client.Close(); } catch { }
                    client = null;
                    State = ConnectionState.Error;
                    ErrorMessage = "Connection timeout";
                    OnError?.Invoke("Connection timeout");
                    return false;
                }

                client.EndConnect(connectResult);
                // 触控采样是小包，Nagle 攒包会让平板点了半天电脑端才收到
                try { client.NoDelay = true; } catch { }
                stream = client.GetStream();
                running = true;
                State = ConnectionState.Connected;

                receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                receiveThread.Start();

                // 发送握手消息
                SendMessage(ThartMessageType.Hello, "{\"device\":\"tablet\"}");

                OnConnected?.Invoke();
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
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            running = false;
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
            State = ConnectionState.Disconnected;
            OnDisconnected?.Invoke();
        }

        public void Dispose()
        {
            Disconnect();
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        public bool SendMessage(ThartMessageType type, string jsonPayload)
        {
            if (State != ConnectionState.Connected || stream == null) return false;

            try
            {
                byte[] data = ThartNetworkUtils.SerializeMessage(type, jsonPayload);
                stream.Write(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                State = ConnectionState.Error;
                OnError?.Invoke(ex.Message);
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// 发送消息（原始字节负载）
        /// </summary>
        public bool SendMessage(ThartMessageType type, byte[] payload)
        {
            if (State != ConnectionState.Connected || stream == null) return false;

            try
            {
                byte[] data = ThartNetworkUtils.SerializeMessage(type, payload);
                stream.Write(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                State = ConnectionState.Error;
                OnError?.Invoke(ex.Message);
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// 发送触控采样数据（实时）
        /// </summary>
        public bool SendTouchSample(TouchSample sample)
        {
            string json = JsonUtility.ToJson(sample);
            return SendMessage(ThartMessageType.TouchSample, json);
        }

        /// <summary>
        /// 发送完整录制数据
        /// </summary>
        public bool SendFullRecording(ThartTouchRecording recording)
        {
            string json = JsonUtility.ToJson(recording);
            return SendMessage(ThartMessageType.RecordingComplete, json);
        }

        private void ReceiveLoop()
        {
            var buffer = new byte[64 * 1024];

            try { client.ReceiveBufferSize = 256 * 1024; } catch { }

            while (running && client != null && client.Connected)
            {
                try
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0) break;

                    lock (receiveBuffer)
                    {
                        receiveBuffer.AddRange(new ArraySegment<byte>(buffer, 0, bytesRead));

                        while (ThartNetworkUtils.TryParseMessage(receiveBuffer, out var message))
                        {
                            OnMessageReceived?.Invoke(message);
                        }
                    }
                }
                catch
                {
                    break;
                }
            }

            if (running)
            {
                State = ConnectionState.Disconnected;
                OnDisconnected?.Invoke();
            }
        }
    }
}
