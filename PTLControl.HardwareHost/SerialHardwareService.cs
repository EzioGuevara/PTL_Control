using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PTLControl.HardwareHost
{
    internal sealed class SerialHardwareService : IDisposable
    {
        private const int MaxQueueSize = 500;
        private const int SendIntervalMs = 20;
        private readonly object _portLock = new object();
        private readonly object _receiveLock = new object();
        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private readonly AutoResetEvent _queueSignal = new AutoResetEvent(false);
        private readonly StringBuilder _receiveBuffer = new StringBuilder();
        private SerialPort _port;
        private CancellationTokenSource _senderCts;
        private Task _senderTask;
        private int _queuedCount;
        private long _sentCount;
        private long _droppedCount;
        private DateTime? _lastSentUtc;
        private DateTime? _lastReceivedUtc;

        public event EventHandler<string> LineReceived;
        public event EventHandler ConnectionFaulted;
        public bool IsOpen { get { lock (_portLock) return _port != null && _port.IsOpen; } }
        public string PortName { get { lock (_portLock) return _port == null ? string.Empty : _port.PortName; } }
        public string[] GetPortNames() => SerialPort.GetPortNames();
        public int QueueLength => Math.Max(0, Volatile.Read(ref _queuedCount));
        public long SentCount => Interlocked.Read(ref _sentCount);
        public long DroppedCount => Interlocked.Read(ref _droppedCount);
        public DateTime? LastSentUtc => _lastSentUtc;
        public DateTime? LastReceivedUtc => _lastReceivedUtc;

        public void Open(string portName)
        {
            lock (_portLock)
            {
                if (_port != null && _port.IsOpen) return;
                var port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
                {
                    WriteTimeout = 500,
                    ReadTimeout = 500
                };
                port.DataReceived += OnDataReceived;
                try
                {
                    port.Open();
                    _port = port;
                    HostLog.Write("串口连接成功：" + portName + " (115200/8N1)");
                }
                catch
                {
                    port.DataReceived -= OnDataReceived;
                    port.Dispose();
                    throw;
                }
            }
            StartSender();
        }

        public void Close()
        {
            StopSender();
            SerialPort port;
            lock (_portLock) { port = _port; _port = null; }
            if (port != null)
            {
                try { port.DataReceived -= OnDataReceived; if (port.IsOpen) port.Close(); }
                finally { HostLog.Write("串口已断开：" + port.PortName); port.Dispose(); }
            }
            ClearQueue();
            lock (_receiveLock) _receiveBuffer.Clear();
        }

        public void Send(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            if (!IsOpen) throw new InvalidOperationException("串口未连接。");

            if (string.Equals(command, "<OFF>", StringComparison.Ordinal)) ClearQueue();

            _queue.Enqueue(command);
            var count = Interlocked.Increment(ref _queuedCount);
            while (count > MaxQueueSize && _queue.TryDequeue(out _))
            {
                count = Interlocked.Decrement(ref _queuedCount);
                Interlocked.Increment(ref _droppedCount);
                HostLog.Write("发送队列达到上限，已丢弃最早指令。");
            }
            _queueSignal.Set();
        }

        public void SendPriority(string command)
        {
            ClearQueue();
            Send(command);
        }

        private void StartSender()
        {
            StopSender();
            _senderCts = new CancellationTokenSource();
            var token = _senderCts.Token;
            _senderTask = Task.Run(() => SenderLoop(token), token);
        }

        private void StopSender()
        {
            var cts = _senderCts;
            var task = _senderTask;
            _senderCts = null;
            _senderTask = null;
            if (cts == null) return;
            try { cts.Cancel(); _queueSignal.Set(); if (task != null) task.Wait(1000); } catch { }
            cts.Dispose();
        }

        private void SenderLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                string command;
                if (!_queue.TryDequeue(out command)) { _queueSignal.WaitOne(50); continue; }
                Interlocked.Decrement(ref _queuedCount);
                try
                {
                    lock (_portLock)
                    {
                        if (_port == null || !_port.IsOpen) continue;
                        _port.Write(command);
                    }
                    _lastSentUtc = DateTime.UtcNow;
                    Interlocked.Increment(ref _sentCount);
                    if (token.WaitHandle.WaitOne(SendIntervalMs)) return;
                }
                catch (Exception ex)
                {
                    HostLog.Write("串口发送失败：" + ex);
                    ResetBrokenPort();
                    ClearQueue();
                    try { ConnectionFaulted?.Invoke(this, EventArgs.Empty); } catch { }
                    return;
                }
            }
        }

        private void ResetBrokenPort()
        {
            SerialPort port;
            lock (_portLock) { port = _port; _port = null; }
            if (port == null) return;
            try { port.DataReceived -= OnDataReceived; if (port.IsOpen) port.Close(); } catch { }
            finally { try { port.Dispose(); } catch { } }
        }

        private void ClearQueue()
        {
            string ignored;
            while (_queue.TryDequeue(out ignored)) Interlocked.Decrement(ref _queuedCount);
            Interlocked.Exchange(ref _queuedCount, 0);
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs args)
        {
            var port = sender as SerialPort;
            if (port == null) return;
            string chunk;
            try { chunk = port.ReadExisting(); }
            catch (Exception ex) { HostLog.Write("串口读取失败：" + ex.Message); return; }
            if (string.IsNullOrEmpty(chunk)) return;
            _lastReceivedUtc = DateTime.UtcNow;

            var lines = new List<string>();
            lock (_receiveLock)
            {
                _receiveBuffer.Append(chunk);
                while (true)
                {
                    var text = _receiveBuffer.ToString();
                    var newline = text.IndexOf('\n');
                    if (newline < 0) break;
                    var line = text.Substring(0, newline).TrimEnd('\r');
                    _receiveBuffer.Remove(0, newline + 1);
                    if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
                }
                if (_receiveBuffer.Length > 4096) _receiveBuffer.Clear();
            }
            foreach (var line in lines)
            {
                HostLog.Write("MCU 返回：" + line);
                try { LineReceived?.Invoke(this, line); } catch { }
            }
        }

        public void Dispose() { Close(); _queueSignal.Dispose(); }
    }

    internal static class HostLog
    {
        private static readonly object SyncRoot = new object();
        public static event EventHandler<string> LineWritten;

        public static string[] GetRecentLines(int maxLines)
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PTLControl", "logs", "hardware-host.log");
                if (!File.Exists(path)) return new string[0];
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                var count = Math.Min(Math.Max(0, maxLines), lines.Length);
                var result = new string[count];
                Array.Copy(lines, lines.Length - count, result, 0, count);
                return result;
            }
            catch { return new string[0]; }
        }

        public static void Write(string message)
        {
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PTLControl", "logs");
                Directory.CreateDirectory(directory);
                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [HardwareHost] " + message + Environment.NewLine;
                lock (SyncRoot)
                {
                    var path = Path.Combine(directory, "hardware-host.log");
                    if (File.Exists(path) && new FileInfo(path).Length >= 10 * 1024 * 1024)
                    {
                        for (var i = 4; i >= 1; i--)
                        {
                            var source = path + "." + i;
                            var target = path + "." + (i + 1);
                            if (File.Exists(source))
                            {
                                if (File.Exists(target)) File.Delete(target);
                                File.Move(source, target);
                            }
                        }
                        File.Move(path, path + ".1");
                    }
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
                try { LineWritten?.Invoke(null, line.TrimEnd('\r', '\n')); } catch { }
            }
            catch { }
        }
    }
}
