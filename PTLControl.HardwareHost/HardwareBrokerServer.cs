using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using Newtonsoft.Json;

namespace PTLControl.HardwareHost
{
    internal sealed class HardwareBrokerServer : IDisposable
    {
        private const string CommandPipeName = "PTLControl.Hardware.Command.v1";
        private const string EventPipeName = "PTLControl.Hardware.Event.v1";
        private readonly SerialHardwareService _serial = new SerialHardwareService();
        private readonly LightActionEngine _actions;
        private readonly object _connectionLock = new object();
        private readonly object _subscribersLock = new object();
        private readonly List<StreamWriter> _subscribers = new List<StreamWriter>();
        private int _nextSessionId;
        private int _activeClientCount;
        private volatile string _configuredPort;
        private volatile string _lastError;
        private volatile string _healthError;
        private int _reconnectCount;
        private readonly DateTime _startedUtc = DateTime.UtcNow;
        private bool _disposed;

        internal bool IsSerialConnected => _serial.IsOpen;
        internal string ActualPort => _serial.PortName;
        internal string ConfiguredPort => _configuredPort;
        internal string LastError => _lastError ?? _healthError ?? HardwareConfig.LastError;
        internal int ActiveClientCount => Volatile.Read(ref _activeClientCount);
        internal int QueueLength => _serial.QueueLength;
        internal long SentCount => _serial.SentCount;
        internal long DroppedCount => _serial.DroppedCount;
        internal DateTime? LastSentUtc => _serial.LastSentUtc;
        internal DateTime? LastReceivedUtc => _serial.LastReceivedUtc;
        internal int ReconnectCount => Volatile.Read(ref _reconnectCount);
        internal TimeSpan Uptime => DateTime.UtcNow - _startedUtc;
        internal int ActiveLightStates => _actions.ActiveStateCount;
        internal bool IsMarqueeActive => _actions.IsMarqueeActive;

        public HardwareBrokerServer()
        {
            _actions = new LightActionEngine(_serial);
            _serial.LineReceived += (s, line) => Broadcast(new BrokerEvent { Type = "line", Line = line });
            _serial.ConnectionFaulted += (s, e) => Broadcast(new BrokerEvent { Type = "fault" });
        }

        public void Run()
        {
            try { EnsureConfiguredConnection(); }
            catch (Exception ex) { HostLog.Write("按配置初始化串口失败：" + ex.Message); }

            var eventThread = new Thread(AcceptEventSubscribers)
            {
                IsBackground = true,
                Name = "PTL-Hardware-Events"
            };
            eventThread.Start();
            var monitorThread = new Thread(ConnectionMonitorLoop)
            {
                IsBackground = true,
                Name = "PTL-Hardware-Monitor"
            };
            monitorThread.Start();
            while (!_disposed)
            {
                var pipe = NewPipe(CommandPipeName, PipeDirection.InOut);
                try
                {
                    pipe.WaitForConnection();
                    if (ActiveClientCount >= 64)
                    {
                        pipe.Dispose();
                        continue;
                    }
                    var sessionId = Interlocked.Increment(ref _nextSessionId);
                    var clientThread = new Thread(() => HandleClient(pipe, sessionId))
                    {
                        IsBackground = true,
                        Name = "PTL-Hardware-Client-" + sessionId
                    };
                    clientThread.Start();
                }
                catch { pipe.Dispose(); if (_disposed) return; }
            }
        }

        private void HandleClient(NamedPipeServerStream pipe, int sessionId)
        {
            Interlocked.Increment(ref _activeClientCount);
            try
            {
                using (pipe)
                using (var reader = new StreamReader(pipe))
                using (var writer = new StreamWriter(pipe) { AutoFlush = true })
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length > 65536)
                            throw new InvalidDataException("IPC 请求超过 64KB 上限。");
                        BrokerRequest request = null;
                        BrokerResponse response;
                        try
                        {
                            request = JsonConvert.DeserializeObject<BrokerRequest>(line);
                            if (request == null) throw new InvalidDataException("请求为空。");
                            response = HandleRequest(request);
                        }
                        catch (Exception ex)
                        {
                            response = new BrokerResponse
                            {
                                Id = request == null ? null : request.Id,
                                Success = false,
                                Error = ex.Message,
                                IsOpen = _serial.IsOpen,
                                HostProtocolVersion = 2,
                                ActualPort = _serial.PortName,
                                HostState = "Error",
                                StatusMessage = "HardwareHost 请求失败：" + ex.Message,
                                HostProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                                UptimeSeconds = (long)Uptime.TotalSeconds
                            };
                            HostLog.Write("请求失败 session=" + sessionId + ": " + ex);
                        }
                        writer.WriteLine(JsonConvert.SerializeObject(response));
                    }
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { HostLog.Write("客户端会话异常 session=" + sessionId + ": " + ex.Message); }
            finally { Interlocked.Decrement(ref _activeClientCount); }
        }

        private BrokerResponse HandleRequest(BrokerRequest request)
        {
            if (request.ProtocolVersion != 0 && request.ProtocolVersion != 2)
                throw new InvalidOperationException("IPC 协议版本不兼容，Host=2, Client=" + request.ProtocolVersion);
            var action = (request.Action ?? string.Empty).ToLowerInvariant();
            if (action == "ports")
                return Ok(request.Id, _serial.GetPortNames());
            if (action == "status")
            {
                EnsureConfiguredConnection();
                return Ok(request.Id, null);
            }
            if (action == "open")
            {
                EnsureConfiguredConnection();
                if (!_serial.IsOpen)
                    throw new InvalidOperationException("配置指定的串口尚未连接。");
                HostLog.Write("客户端连接：" + Client(request) + " · 实际串口 " + _serial.PortName);
                return Ok(request.Id, null);
            }
            if (action == "close")
            {
                // 仅兼容上层 Disconnect；物理串口继续由宿主持有。
                HostLog.Write("客户端断开：" + Client(request) + " · 物理串口保持连接");
                return Ok(request.Id, null);
            }
            if (action == "send")
            {
                EnsureConfiguredConnection();
                ValidateRawCommand(request.Command);
                _serial.Send(request.Command);
                HostLog.Write("收到指令 [" + Client(request) + "] 原始发送 " + Short(request.Command));
                return Ok(request.Id, null);
            }
            if (action == "setlight")
            {
                EnsureConfiguredConnection();
                _actions.SetLight(request.Layer, request.Index, request.R, request.G, request.B);
                HostLog.Write("收到指令 [" + Client(request) + "] 点亮 L" + request.Layer + "/" + request.Index + " RGB(" + request.R + "," + request.G + "," + request.B + ")");
                return Ok(request.Id, null);
            }
            if (action == "setblink")
            {
                EnsureConfiguredConnection();
                _actions.SetBlink(request.Layer, request.Index, request.R, request.G, request.B, request.IntervalMs);
                HostLog.Write("收到指令 [" + Client(request) + "] 闪烁 L" + request.Layer + "/" + request.Index + " RGB(" + request.R + "," + request.G + "," + request.B + ") " + request.IntervalMs + "ms");
                return Ok(request.Id, null);
            }
            if (action == "turnoff")
            {
                EnsureConfiguredConnection();
                _actions.TurnOff(request.Layer, request.Index);
                HostLog.Write("收到指令 [" + Client(request) + "] 熄灭 L" + request.Layer + "/" + request.Index);
                return Ok(request.Id, null);
            }
            if (action == "alloff")
            {
                EnsureConfiguredConnection();
                _actions.AllOff();
                HostLog.Write("收到指令 [" + Client(request) + "] 全部熄灭");
                return Ok(request.Id, null);
            }
            if (action == "marquee")
            {
                EnsureConfiguredConnection();
                var strips = new List<StripDefinition>();
                if (request.Strips != null)
                    foreach (var strip in request.Strips)
                        strips.Add(new StripDefinition { Layer = strip.Layer, Count = strip.Count });
                _actions.StartMarquee(request.R, request.G, request.B, request.IntervalMs, strips);
                HostLog.Write("收到指令 [" + Client(request) + "] 跑马灯 RGB(" + request.R + "," + request.G + "," + request.B + ") " + request.IntervalMs + "ms");
                return Ok(request.Id, null);
            }
            throw new InvalidOperationException("不支持的硬件操作：" + request.Action);
        }

        private static string Client(BrokerRequest request)
        {
            return string.IsNullOrWhiteSpace(request.Client) ? "未知应用" : Short(request.Client);
        }

        private static string Short(string value)
        {
            var text = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= 160 ? text : text.Substring(0, 160) + "...";
        }

        internal void ReloadConfiguration()
        {
            EnsureConfiguredConnection();
        }

        private void EnsureConfiguredConnection()
        {
            try
            {
                EnsureConfiguredConnectionCore();
                _lastError = null;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                throw;
            }
        }

        private void EnsureConfiguredConnectionCore()
        {
            lock (_connectionLock)
            {
                var config = HardwareConfig.LoadSerialConnection();
                _configuredPort = config.PortName;
                if (!config.Enabled)
                {
                    if (_serial.IsOpen)
                    {
                        HostLog.Write("配置已切换为非串口模式，关闭物理串口。");
                        _serial.Close();
                    }
                    return;
                }

                if (_serial.IsOpen && string.Equals(_serial.PortName, config.PortName, StringComparison.OrdinalIgnoreCase))
                    return;

                if (_serial.IsOpen)
                {
                    HostLog.Write("配置端口已变化：" + _serial.PortName + " -> " + config.PortName);
                    _serial.Close();
                }

                _serial.Open(config.PortName);
                Interlocked.Increment(ref _reconnectCount);
                _actions.ReplayAll();
            }
        }

        private void ConnectionMonitorLoop()
        {
            var nextPingUtc = DateTime.MinValue;
            DateTime? pendingPingUtc = null;
            var pingWarningLogged = false;
            while (!_disposed)
            {
                try
                {
                    EnsureConfiguredConnection();
                    if (pendingPingUtc.HasValue && DateTime.UtcNow.Subtract(pendingPingUtc.Value).TotalSeconds >= 5)
                    {
                        if (!_serial.LastReceivedUtc.HasValue || _serial.LastReceivedUtc.Value < pendingPingUtc.Value)
                        {
                            _healthError = "MCU PING 在 5 秒内未响应。";
                            if (!pingWarningLogged)
                            {
                                HostLog.Write(_healthError);
                                pingWarningLogged = true;
                            }
                        }
                        else
                        {
                            _healthError = null;
                            pingWarningLogged = false;
                        }
                        pendingPingUtc = null;
                    }
                    if (_serial.IsOpen && DateTime.UtcNow >= nextPingUtc)
                    {
                        _serial.Send("<PING>");
                        pendingPingUtc = DateTime.UtcNow;
                        nextPingUtc = DateTime.UtcNow.AddSeconds(10);
                    }
                }
                catch { }
                Thread.Sleep(1000);
            }
        }

        private static void ValidateRawCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command) || command.Length > 128)
                throw new InvalidOperationException("串口指令为空或过长。");
            if (!command.StartsWith("<", StringComparison.Ordinal) || !command.EndsWith(">", StringComparison.Ordinal))
                throw new InvalidOperationException("串口指令格式无效。");
        }

        private BrokerResponse Ok(string id, string[] ports)
        {
            return new BrokerResponse
            {
                Id = id, Success = true, IsOpen = _serial.IsOpen, Ports = ports,
                HostProtocolVersion = 2, ActualPort = _serial.PortName,
                HostState = _serial.IsOpen ? (string.IsNullOrWhiteSpace(LastError) ? "Healthy" : "Degraded") : "Disconnected",
                StatusMessage = BuildStatusMessage(),
                HostProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                UptimeSeconds = (long)Uptime.TotalSeconds,
                QueueLength = QueueLength,
                LastError = LastError
            };
        }

        private string BuildStatusMessage()
        {
            if (!_serial.IsOpen)
                return "HardwareHost 运行中，但配置串口未连接" +
                    (string.IsNullOrWhiteSpace(LastError) ? "。" : "：" + LastError);
            if (!string.IsNullOrWhiteSpace(LastError))
                return "HardwareHost 已连接 " + _serial.PortName + "，健康状态降级：" + LastError;
            return "HardwareHost 运行正常，实际串口=" + _serial.PortName + "，队列=" + QueueLength + "。";
        }

        private void AcceptEventSubscribers()
        {
            while (!_disposed)
            {
                var pipe = NewPipe(EventPipeName, PipeDirection.Out);
                try
                {
                    pipe.WaitForConnection();
                    var writer = new StreamWriter(pipe) { AutoFlush = true };
                    lock (_subscribersLock)
                    {
                        if (_subscribers.Count >= 64)
                        {
                            try { _subscribers[0].Dispose(); } catch { }
                            _subscribers.RemoveAt(0);
                        }
                        _subscribers.Add(writer);
                    }
                }
                catch { pipe.Dispose(); if (_disposed) return; }
            }
        }

        private void Broadcast(BrokerEvent value)
        {
            var json = JsonConvert.SerializeObject(value);
            lock (_subscribersLock)
            {
                for (var i = _subscribers.Count - 1; i >= 0; i--)
                {
                    try { _subscribers[i].WriteLine(json); }
                    catch { try { _subscribers[i].Dispose(); } catch { } _subscribers.RemoveAt(i); }
                }
            }
        }

        private static NamedPipeServerStream NewPipe(string name, PipeDirection direction)
        {
            return new NamedPipeServerStream(name, direction, NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte, PipeOptions.None);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _actions.Dispose();
            _serial.Dispose();
            lock (_subscribersLock)
            {
                foreach (var writer in _subscribers) try { writer.Dispose(); } catch { }
                _subscribers.Clear();
            }
        }

        private sealed class BrokerRequest
        {
            public int ProtocolVersion { get; set; }
            public string Id { get; set; }
            public string Action { get; set; }
            public string Port { get; set; }
            public string Command { get; set; }
            public string Client { get; set; }
            public int Layer { get; set; }
            public int Index { get; set; }
            public int R { get; set; }
            public int G { get; set; }
            public int B { get; set; }
            public int IntervalMs { get; set; }
            public IList<BrokerStrip> Strips { get; set; }
        }
        private sealed class BrokerStrip { public int Layer { get; set; } public int Count { get; set; } }
        private sealed class BrokerResponse
        {
            public string Id { get; set; }
            public bool Success { get; set; }
            public string Error { get; set; }
            public bool IsOpen { get; set; }
            public string[] Ports { get; set; }
            public int HostProtocolVersion { get; set; }
            public string ActualPort { get; set; }
            public string HostState { get; set; }
            public string StatusMessage { get; set; }
            public int HostProcessId { get; set; }
            public long UptimeSeconds { get; set; }
            public int QueueLength { get; set; }
            public string LastError { get; set; }
        }
        private sealed class BrokerEvent { public string Type { get; set; } public string Line { get; set; } }
    }
}
