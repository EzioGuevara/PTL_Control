using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PTLControl.Compat.Models;

namespace PTLControl.Compat.Services
{
    /// <summary>串口代理；所有硬件操作均交给本机 PTLControl.HardwareHost.exe。</summary>
    public sealed class SerialService : IDisposable
    {
        private const string CommandPipeName = "PTLControl.Hardware.Command.v1";
        private const string EventPipeName = "PTLControl.Hardware.Event.v1";
        public static readonly SerialService Instance = new SerialService();

        private readonly object _requestLock = new object();
        private readonly object _eventLock = new object();
        private NamedPipeClientStream _pipe;
        private StreamReader _reader;
        private StreamWriter _writer;
        private CancellationTokenSource _eventCts;
        private Task _eventTask;
        private EventHandler _connectionFaulted;
        private EventHandler<SerialLineReceivedEventArgs> _lineReceived;
        private volatile bool _logicalConnected;
        private string _actualPort;
        private string _connectionMessage = "尚未连接 HardwareHost。";
        private bool _disposed;

        private SerialService() { }

        public event EventHandler ConnectionFaulted
        {
            add { lock (_eventLock) _connectionFaulted += value; }
            remove { lock (_eventLock) _connectionFaulted -= value; }
        }

        public event EventHandler<SerialLineReceivedEventArgs> LineReceived
        {
            add { lock (_eventLock) _lineReceived += value; }
            remove { lock (_eventLock) _lineReceived -= value; }
        }

        public bool IsOpen
        {
            get
            {
                if (!_logicalConnected) return false;
                try
                {
                    var response = Request("status");
                    _actualPort = response.ActualPort;
                    _connectionMessage = response.StatusMessage;
                    return response.IsOpen;
                }
                catch (Exception ex) { _connectionMessage = ex.Message; return false; }
            }
        }

        public string[] GetPortNames() => Request("ports").Ports ?? new string[0];
        public string ActualPort => _actualPort ?? string.Empty;
        public string ConnectionMessage => _connectionMessage ?? string.Empty;

        public void Open(string portName)
        {
            try
            {
                // 为兼容旧调用保留 portName 参数，但物理端口只由宿主读取 startup_config.json 决定。
                var response = Request("open", null, null);
                _actualPort = response.ActualPort;
                _connectionMessage = response.StatusMessage;
                _logicalConnected = response.IsOpen;
                if (!_logicalConnected)
                    throw new InvalidOperationException(_connectionMessage);
                EnsureEventListener();
            }
            catch (Exception ex)
            {
                _logicalConnected = false;
                _connectionMessage = ex.Message;
                throw;
            }
        }

        public void Close()
        {
            _logicalConnected = false;
            _actualPort = null;
            _connectionMessage = "当前调用方已逻辑断开；HardwareHost 物理连接不受影响。";
            lock (_requestLock)
            {
                if (_pipe == null || !_pipe.IsConnected)
                    return;
                try { RequestUnsafe("close", null, null); }
                finally { ClosePipeUnsafe(); }
            }
        }

        public void Send(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return;
            if (!_logicalConnected)
                throw new InvalidOperationException("当前调用方尚未调用 Connect。");
            Request("send", null, cmd);
        }

        internal void SetLight(int layer, int index, int r, int g, int b)
            => RequestAction("setLight", layer, index, r, g, b, 0, null);

        internal void SetBlink(int layer, int index, int r, int g, int b, int intervalMs)
            => RequestAction("setBlink", layer, index, r, g, b, intervalMs, null);

        internal void TurnOff(int layer, int index)
            => RequestAction("turnOff", layer, index, 0, 0, 0, 0, null);

        internal void AllOff()
            => RequestAction("allOff", 0, 0, 0, 0, 0, 0, null);

        internal void Marquee(int r, int g, int b, int intervalMs, IList<KeyValuePair<int, int>> strips)
        {
            var items = new List<BrokerStrip>();
            if (strips != null)
                foreach (var strip in strips)
                    items.Add(new BrokerStrip { Layer = strip.Key, Count = strip.Value });
            RequestAction("marquee", 0, 0, r, g, b, intervalMs, items);
        }

        private void RequestAction(string action, int layer, int index, int r, int g, int b, int intervalMs, IList<BrokerStrip> strips)
        {
            if (!_logicalConnected)
                throw new InvalidOperationException("当前调用方尚未调用 Connect。");
            Request(new BrokerRequest
            {
                Action = action, Layer = layer, Index = index, R = r, G = g, B = b,
                IntervalMs = intervalMs, Strips = strips
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            try { Close(); } catch { }
            _disposed = true;
            lock (_eventLock) { if (_eventCts != null) _eventCts.Cancel(); }
        }

        private BrokerResponse Request(string action, string port = null, string command = null)
            => Request(new BrokerRequest { Action = action, Port = port, Command = command });

        private BrokerResponse Request(BrokerRequest request)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SerialService));
            lock (_requestLock)
            {
                EnsurePipeUnsafe();
                try { return RequestUnsafe(request); }
                catch (IOException) { ClosePipeUnsafe(); throw; }
            }
        }

        private BrokerResponse RequestUnsafe(string action, string port, string command)
            => RequestUnsafe(new BrokerRequest { Action = action, Port = port, Command = command });

        private BrokerResponse RequestUnsafe(BrokerRequest request)
        {
            request.Id = Guid.NewGuid().ToString("N");
            request.Client = GetClientName();
            request.ProtocolVersion = 2;
            _writer.WriteLine(JsonConvert.SerializeObject(request));
            var readTask = _reader.ReadLineAsync();
            if (!readTask.Wait(5000))
            {
                ClosePipeUnsafe();
                throw new TimeoutException("PTL 硬件宿主在 5 秒内没有响应。");
            }
            var line = readTask.Result;
            if (line == null) throw new IOException("PTL 硬件宿主已断开连接。");
            var response = JsonConvert.DeserializeObject<BrokerResponse>(line);
            if (response == null || response.Id != request.Id)
                throw new IOException("PTL 硬件宿主返回了无效响应。");
            if (response.HostProtocolVersion != 2)
                throw new InvalidOperationException("PTLControl.Compat 与 HardwareHost 版本不兼容，请成套替换交付文件。");
            if (!response.Success)
            {
                _connectionMessage = response.StatusMessage ?? response.Error;
                throw new InvalidOperationException(response.Error ?? "PTL 硬件操作失败。");
            }
            return response;
        }

        private void EnsurePipeUnsafe()
        {
            if (_pipe != null && _pipe.IsConnected) return;
            ClosePipeUnsafe();
            if (TryConnectUnsafe()) return;
            StartHardwareHost();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(150);
                if (TryConnectUnsafe()) return;
            }
            throw new InvalidOperationException("无法连接 PTL 硬件宿主。请确认 PTLControl.HardwareHost.exe 与 DLL 位于同一目录且可运行。");
        }

        private bool TryConnectUnsafe()
        {
            try
            {
                var pipe = new NamedPipeClientStream(".", CommandPipeName, PipeDirection.InOut, PipeOptions.None);
                pipe.Connect(750);
                _pipe = pipe;
                _reader = new StreamReader(pipe);
                _writer = new StreamWriter(pipe) { AutoFlush = true };
                return true;
            }
            catch { ClosePipeUnsafe(); return false; }
        }

        private void ClosePipeUnsafe()
        {
            try { if (_writer != null) _writer.Dispose(); } catch { }
            try { if (_reader != null) _reader.Dispose(); } catch { }
            try { if (_pipe != null) _pipe.Dispose(); } catch { }
            _writer = null; _reader = null; _pipe = null;
        }

        private static void StartHardwareHost()
        {
            var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            var hostPath = Path.Combine(baseDirectory, "PTLControl.HardwareHost.exe");
            if (!File.Exists(hostPath))
                throw new FileNotFoundException("缺少 PTL 硬件宿主，无法执行任何串口操作。", hostPath);
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = hostPath, WorkingDirectory = baseDirectory,
                UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal
            })) { }
        }

        private void EnsureEventListener()
        {
            lock (_eventLock)
            {
                if (_eventTask != null && !_eventTask.IsCompleted) return;
                _eventCts = new CancellationTokenSource();
                var token = _eventCts.Token;
                _eventTask = Task.Run(() => EventLoop(token), token);
            }
        }

        private void EventLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var eventPipe = new NamedPipeClientStream(".", EventPipeName, PipeDirection.In))
                    {
                        eventPipe.Connect(1000);
                        using (var eventReader = new StreamReader(eventPipe))
                        {
                            while (!token.IsCancellationRequested)
                            {
                                var line = eventReader.ReadLine();
                                if (line == null) break;
                                DispatchEvent(JsonConvert.DeserializeObject<BrokerEvent>(line));
                            }
                        }
                    }
                }
                catch { }
                if (!token.IsCancellationRequested) Thread.Sleep(500);
            }
        }

        private void DispatchEvent(BrokerEvent value)
        {
            if (value == null) return;
            if (string.Equals(value.Type, "line", StringComparison.OrdinalIgnoreCase))
            {
                EventHandler<SerialLineReceivedEventArgs> handler;
                lock (_eventLock) handler = _lineReceived;
                if (handler != null) handler(this, new SerialLineReceivedEventArgs(value.Line ?? string.Empty));
            }
            else if (string.Equals(value.Type, "fault", StringComparison.OrdinalIgnoreCase))
            {
                EventHandler handler;
                lock (_eventLock) handler = _connectionFaulted;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        private static string GetClientName()
        {
            try { var p = Process.GetCurrentProcess(); return p.ProcessName + ":" + p.Id; }
            catch { return AppDomain.CurrentDomain.FriendlyName; }
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
