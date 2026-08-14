using System;
using System.Collections.Generic;
using PTLControl.Compat.Models;

namespace PTLControl.Compat.Services
{
    internal static class TransportRouter
    {
        private static readonly object SyncRoot = new object();
        private static readonly ITransportService SerialTransport = new SerialTransportService();
        private static readonly ITransportService MqttTransport = new MqttEStationService();
        private static string _connectedTransportType;

        static TransportRouter()
        {
            SerialTransport.ConnectionChanged += RelayConnectionChanged;
            MqttTransport.ConnectionChanged += RelayConnectionChanged;
            SerialTransport.TagEventReceived += RelayTagEvent;
            MqttTransport.TagEventReceived += RelayTagEvent;
        }

        public static event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;
        public static event EventHandler<TagEventArgs> TagEventReceived;

        public static bool IsWirelessMode
        {
            get
            {
                lock (SyncRoot)
                {
                    if (!string.IsNullOrWhiteSpace(_connectedTransportType))
                        return string.Equals(_connectedTransportType, "mqtt", StringComparison.OrdinalIgnoreCase);
                }
                var startup = ConfigService.LoadStartup();
                return IsMqtt(startup);
            }
        }

        public static bool IsConnected
        {
            get
            {
                var startup = ConfigService.LoadStartup();
                var active = GetRuntimeTransport(startup);
                if (!active.IsConnected)
                    return false;

                if (active.TransportType != "mqtt")
                    return true;

                var lastHeartbeat = active.LastHeartbeatUtc;
                if (!lastHeartbeat.HasValue)
                    return true;

                var heartbeatTimeout = startup.WirelessDefaults.HeartbeatTimeoutSec;
                return DateTime.UtcNow.Subtract(lastHeartbeat.Value).TotalSeconds <= heartbeatTimeout;
            }
        }

        public static string[] GetPortNames()
        {
            return SerialTransport.GetPortNames();
        }

        public static void Connect(string portName)
        {
            var startup = ConfigService.LoadStartup();
            ConnectCore(IsMqtt(startup) ? "mqtt" : "serial", portName, startup);
        }

        private static void ConnectCore(string mode, string portName, StartupConfig startup)
        {
            lock (SyncRoot)
            {
                var active = string.Equals(mode, "mqtt", StringComparison.OrdinalIgnoreCase)
                    ? MqttTransport
                    : SerialTransport;
                var inactive = active == MqttTransport ? SerialTransport : MqttTransport;

                try
                {
                    if (inactive.IsConnected)
                        inactive.Disconnect();
                }
                catch (Exception ex)
                {
                    LogService.Warn("切换传输层时断开旧连接失败：" + ex.Message);
                }

                if (active.TransportType == "serial")
                {
                    var finalPort = !string.IsNullOrWhiteSpace(portName)
                        ? portName
                        : (startup.Serial?.PortName ?? string.Empty);
                    active.Connect(finalPort);
                    _connectedTransportType = active.TransportType;
                    return;
                }

                active.Connect(string.Empty);
                _connectedTransportType = active.TransportType;
            }
        }

        public static void Disconnect()
        {
            lock (SyncRoot)
            {
                Exception disconnectError = null;
                try
                {
                    try
                    {
                        SerialTransport.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        disconnectError = ex;
                    }

                    try
                    {
                        MqttTransport.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        if (disconnectError == null)
                            disconnectError = ex;
                    }
                }
                finally
                {
                    _connectedTransportType = null;
                }

                if (disconnectError != null)
                    throw disconnectError;
            }
        }

        public static void SendSerialCommand(string cmd)
        {
            var startup = ConfigService.LoadStartup();
            var active = GetRuntimeTransport(startup);
            if (active.TransportType != "serial")
                throw new InvalidOperationException("当前为无线模式，不支持串口指令发送。");
            active.SendSerialCommand(cmd);
        }

        public static void SetSerialLight(int layer, int index, int r, int g, int b)
            => SerialService.Instance.SetLight(layer, index, r, g, b);

        public static void SetSerialBlink(int layer, int index, int r, int g, int b, int intervalMs)
            => SerialService.Instance.SetBlink(layer, index, r, g, b, intervalMs);

        public static void TurnOffSerialLight(int layer, int index)
            => SerialService.Instance.TurnOff(layer, index);

        public static void AllOffSerial()
            => SerialService.Instance.AllOff();

        public static void StartSerialMarquee(int r, int g, int b, int intervalMs, IList<KeyValuePair<int, int>> strips)
            => SerialService.Instance.Marquee(r, g, b, intervalMs, strips);

        public static void PublishWirelessTask(WirelessTask task)
        {
            var startup = ConfigService.LoadStartup();
            var active = GetRuntimeTransport(startup);
            if (active.TransportType != "mqtt")
                throw new InvalidOperationException("当前为串口模式，不支持无线任务发送。");
            active.PublishWirelessTask(task);
        }

        public static DateTime? GetLastHeartbeatUtc()
        {
            return MqttTransport.LastHeartbeatUtc;
        }

        public static string GetLastConnectionMessage()
        {
            var startup = ConfigService.LoadStartup();
            if (IsMqtt(startup))
                return MqttTransport.IsConnected ? "MQTT 已连接。" : "MQTT 未连接。";
            return SerialService.Instance.ConnectionMessage;
        }

        private static bool IsMqtt(StartupConfig startup)
        {
            return string.Equals(startup.ConnectionMode, "mqtt", StringComparison.OrdinalIgnoreCase);
        }

        private static ITransportService GetActiveTransport(StartupConfig startup)
        {
            return IsMqtt(startup) ? MqttTransport : SerialTransport;
        }

        private static ITransportService GetRuntimeTransport(StartupConfig startup)
        {
            lock (SyncRoot)
            {
                if (string.Equals(_connectedTransportType, "mqtt", StringComparison.OrdinalIgnoreCase))
                    return MqttTransport;
                if (string.Equals(_connectedTransportType, "serial", StringComparison.OrdinalIgnoreCase))
                    return SerialTransport;
            }
            return GetActiveTransport(startup);
        }

        private static void RelayConnectionChanged(object sender, ConnectionChangedEventArgs e)
        {
            ConnectionChanged?.Invoke(null, e);
        }

        private static void RelayTagEvent(object sender, TagEventArgs e)
        {
            TagEventReceived?.Invoke(null, e);
        }
    }
}
