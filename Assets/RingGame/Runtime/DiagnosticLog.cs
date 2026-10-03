using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace RingGame.Runtime
{
    /// <summary>Buffered JSONL in app-private storage; no external-storage Android permission required.</summary>
    public sealed class DiagnosticLog : IDisposable
    {
        private readonly object sync = new object();
        private StreamWriter writer;

        public DiagnosticLog(string configurationJson = "{}", int deviceTouchSlots = 0)
        {
            string sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) +
                "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            FilePath = Path.Combine(Application.persistentDataPath, "diagnostics", "session_" + sessionId + ".jsonl");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                writer = new StreamWriter(new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.Read, 65536), new UTF8Encoding(false));
                AudioSettings.GetDSPBufferSize(out int bufferLength, out int bufferCount);
                Record("session", JsonUtility.ToJson(new SessionHeader
                {
                    sessionId = sessionId,
                    unityVersion = Application.unityVersion,
                    appVersion = Application.version,
                    platform = Application.platform.ToString(),
                    operatingSystem = SystemInfo.operatingSystem,
                    deviceModel = SystemInfo.deviceModel,
                    graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                    width = Screen.width,
                    height = Screen.height,
                    outputSampleRate = AudioSettings.outputSampleRate,
                    dspBufferLength = bufferLength,
                    dspBufferCount = bufferCount,
                    inputStreamTouchSlots = deviceTouchSlots,
                    presentationEvidence = "CPU submission history; display latency is configured, not physically measured",
                    touchCapacityEvidence = "Input System slots; maximum simultaneous hardware contacts must be manually measured"
                }));
                Record("configuration", configurationJson);
                Flush();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                LastError = exception.Message;
                writer?.Dispose();
                writer = null;
                Debug.LogWarning("Ring diagnostics unavailable: " + LastError);
            }
        }

        public string FilePath { get; }
        public bool IsAvailable => writer != null;
        public string LastError { get; private set; }

        /// <summary>Payload is a JSON object serialized by the caller; one physical line per record.</summary>
        public void Record(string type, string payloadJson)
        {
            lock (sync)
            {
                if (writer == null) return;
                try
                {
                    string payload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson.Trim();
                    // JsonUtility's optional pretty formatting is stripped; JSON string line breaks are already escaped.
                    payload = payload.Replace("\r", "").Replace("\n", "");
                    writer.Write("{\"type\":\"");
                    writer.Write(Escape(type ?? "unknown"));
                    writer.Write("\",\"utc\":\"");
                    writer.Write(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    writer.Write("\",\"inputClockSeconds\":");
                    writer.Write(SongClock.InputTimeNow.ToString("R", CultureInfo.InvariantCulture));
                    writer.Write(",\"payload\":");
                    writer.Write(payload);
                    writer.WriteLine("}");
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    DisableAfterStorageFailure(exception);
                }
            }
        }

        public void Flush()
        {
            lock (sync)
            {
                try { writer?.Flush(); }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    DisableAfterStorageFailure(exception);
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                try { writer?.Dispose(); }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    LastError = exception.Message;
                }
                finally { writer = null; }
            }
        }

        private void DisableAfterStorageFailure(Exception exception)
        {
            LastError = exception.Message;
            try { writer?.Dispose(); }
            catch (Exception closeException) when (closeException is IOException || closeException is UnauthorizedAccessException) { }
            writer = null;
            Debug.LogWarning("Ring diagnostics stopped: " + LastError);
        }

        private static string Escape(string value)
        {
            var output = new StringBuilder(value.Length + 8);
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (character < 32) output.Append("\\u" + ((int)character).ToString("x4"));
                        else output.Append(character);
                        break;
                }
            }
            return output.ToString();
        }

        [Serializable]
        private sealed class SessionHeader
        {
            public string sessionId;
            public string unityVersion;
            public string appVersion;
            public string platform;
            public string operatingSystem;
            public string deviceModel;
            public string graphicsApi;
            public int width;
            public int height;
            public int outputSampleRate;
            public int dspBufferLength;
            public int dspBufferCount;
            public int inputStreamTouchSlots;
            public string presentationEvidence;
            public string touchCapacityEvidence;
        }
    }
}
