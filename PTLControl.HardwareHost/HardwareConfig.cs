using System;
using System.IO;
using Newtonsoft.Json;

namespace PTLControl.HardwareHost
{
    internal static class HardwareConfig
    {
        private static readonly object SyncRoot = new object();
        private static DateTime _lastWriteUtc;
        private static SerialConnectionConfig _cached;
        internal static string LastError { get; private set; }
        internal static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PTLControl",
            "startup_config.json");

        public static SerialConnectionConfig LoadSerialConnection()
        {
            lock (SyncRoot)
            {
                try
                {
                    if (!File.Exists(ConfigPath))
                        throw new InvalidOperationException("未找到硬件配置文件：" + ConfigPath);
                    var writeUtc = File.GetLastWriteTimeUtc(ConfigPath);
                    if (_cached != null && writeUtc == _lastWriteUtc) return _cached;

                    var startup = JsonConvert.DeserializeObject<StartupConfig>(File.ReadAllText(ConfigPath)) ?? new StartupConfig();
                    SerialConnectionConfig next;
                    if (!string.Equals(startup.ConnectionMode, "serial", StringComparison.OrdinalIgnoreCase))
                        next = new SerialConnectionConfig { Enabled = false };
                    else
                    {
                        if (startup.Serial == null || string.IsNullOrWhiteSpace(startup.Serial.PortName))
                            throw new InvalidOperationException("startup_config.json 未配置 serial.portName。");
                        next = new SerialConnectionConfig { Enabled = true, PortName = startup.Serial.PortName.Trim() };
                    }
                    _cached = next;
                    _lastWriteUtc = writeUtc;
                    LastError = null;
                    return next;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    if (_cached != null) return _cached;
                    throw;
                }
            }
        }

        private sealed class StartupConfig
        {
            public string ConnectionMode { get; set; }
            public SerialConfig Serial { get; set; }
        }

        private sealed class SerialConfig { public string PortName { get; set; } }
    }

    internal sealed class SerialConnectionConfig
    {
        public bool Enabled { get; set; }
        public string PortName { get; set; }
    }
}
