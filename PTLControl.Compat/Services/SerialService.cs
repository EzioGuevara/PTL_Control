// ============================================================
// PTL LED Matrix Control System - .NET Standard 2.0 Compat
// Developer: Ezio @ IDEMIA
// Description: Singleton serial port service. Thread-safe write.
// ============================================================
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using PTLControl.Compat.Models;

namespace PTLControl.Compat.Services
{
    /// <summary>
    /// 单例串口服务，线程安全写入（lock）。
    /// </summary>
    public sealed class SerialService : IDisposable
    {
        private const int MaxQueueSize = 2000;
        private const int SendIntervalMs = 20;

        public static readonly SerialService Instance = new SerialService();

        private SerialPort _port;
        private readonly object _lock = new object();
        private readonly ConcurrentQueue<string> _sendQueue = new ConcurrentQueue<string>();
        private readonly AutoResetEvent _queueSignal = new AutoResetEvent(false);
        private readonly object _receiveLock = new object();
        private readonly StringBuilder _receiveBuffer = new StringBuilder();
        private CancellationTokenSource _senderCts;
        private Task _senderTask;
        private int _queuedCount;

        private SerialService() { }

        /// <summary>串口在后台发送时发生物理连接故障。</summary>
        public event EventHandler ConnectionFaulted;
        /// <summary>MCU 返回完整文本行时触发。</summary>
        public event EventHandler<SerialLineReceivedEventArgs> LineReceived;

        public bool IsOpen => _port != null && _port.IsOpen;

        public string[] GetPortNames() => SerialPort.GetPortNames();

        /// <summary>打开串口，115200/8N1/WriteTimeout=500ms</summary>
        public void Open(string portName)
        {
            LogService.RefreshLevelFromConfig();
            var opened = false;
            lock (_lock)
            {
                if (_port != null && _port.IsOpen)
                    throw new InvalidOperationException("串口已打开");

                var appName = GetHostProgramName();
                LogService.Info("开始连接串口：" + portName + "，来源程序=" + appName);
                _port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
                {
                    WriteTimeout = 500,
                    ReadTimeout = 500
                };
                _port.DataReceived += OnDataReceived;
                try
                {
                    lock (_receiveLock)
                        _receiveBuffer.Clear();
                    _port.Open();
                    opened = true;
                    LogService.Info("串口连接成功：" + portName + "（115200/8N1），来源程序=" + appName);
                }
                catch (Exception ex)
                {
                    LogService.Error("串口连接失败：" + portName + "，来源程序=" + appName, ex);
                    _port.DataReceived -= OnDataReceived;
                    try
                    {
                        _port.Dispose();
                    }
                    catch
                    {
                        // 保留原始打开异常。
                    }
                    _port = null;
                    throw;
                }
            }

            if (opened)
                StartSenderLoop();
        }

        /// <summary>关闭并释放串口</summary>
        public void Close()
        {
            SerialPort portToDispose = null;
            string name = string.Empty;

            lock (_lock)
            {
                if (_port != null)
                {
                    name = _port.PortName;
                    try
                    {
                        StopSenderLoopSignalUnsafe();
                        if (_port.IsOpen)
                        {
                            LogService.Info("开始断开串口：" + name);
                            _port.Close();
                        }
                        LogService.Info("串口已断开：" + name);
                    }
                    catch (Exception ex)
                    {
                        LogService.Error("串口断开异常：" + name, ex);
                        throw;
                    }
                    finally
                    {
                        portToDispose = _port;
                        _port.DataReceived -= OnDataReceived;
                        _port = null;
                    }
                }
            }

            StopSenderLoopWaitUnsafe();
            ClearQueueUnsafe();
            Interlocked.Exchange(ref _queuedCount, 0);
            lock (_receiveLock)
                _receiveBuffer.Clear();

            if (portToDispose != null)
                portToDispose.Dispose();
        }

        /// <summary>发送指令字符串（线程安全）</summary>
        public void Send(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                return;

            lock (_lock)
            {
                if (_port == null || !_port.IsOpen)
                {
                    LogService.Warn("发送失败：串口未连接，指令=" + cmd);
                    throw new InvalidOperationException("串口未连接");
                }
            }

            // 统一进入发送队列，避免上层并发/风暴直接冲击串口。
            EnqueueCommand(cmd);
        }

        public void Dispose() => Close();

        private void EnqueueCommand(string cmd)
        {
            if (string.Equals(cmd, CommandService.OffCommand, StringComparison.Ordinal))
            {
                // 全灭是安全指令，应覆盖尚未发送的旧状态，不能在风暴中排到队尾。
                ClearQueueUnsafe();
            }

            var count = Interlocked.Increment(ref _queuedCount);
            if (count > MaxQueueSize)
            {
                string dropped;
                if (_sendQueue.TryDequeue(out dropped))
                {
                    Interlocked.Decrement(ref _queuedCount);
                    LogService.Warn("串口发送队列过长，已丢弃最早指令：" + dropped);
                }
            }

            _sendQueue.Enqueue(cmd);
            _queueSignal.Set();
        }

        private void StartSenderLoop()
        {
            StopSenderLoopSignalUnsafe();
            StopSenderLoopWaitUnsafe();
            _senderCts = new CancellationTokenSource();
            var token = _senderCts.Token;
            _senderTask = Task.Run(() => SenderLoop(token), token);
        }

        private void StopSenderLoopSignalUnsafe()
        {
            try
            {
                if (_senderCts != null)
                {
                    _senderCts.Cancel();
                    _queueSignal.Set();
                }
            }
            catch
            {
                // 忽略停止流程异常，避免影响关闭串口。
            }
        }

        private void StopSenderLoopWaitUnsafe()
        {
            try
            {
                if (_senderTask != null)
                    _senderTask.Wait(1000);
            }
            catch
            {
                // 忽略退出等待异常，避免影响关闭串口。
            }
            finally
            {
                if (_senderCts != null)
                {
                    _senderCts.Dispose();
                    _senderCts = null;
                }
                _senderTask = null;
            }
        }

        private void ClearQueueUnsafe()
        {
            string _;
            while (_sendQueue.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _queuedCount);
            }
        }

        private void SenderLoop(CancellationToken token)
        {
            var nextSendUtc = DateTime.UtcNow;
            while (!token.IsCancellationRequested)
            {
                string cmd;
                if (!_sendQueue.TryDequeue(out cmd))
                {
                    _queueSignal.WaitOne(50);
                    continue;
                }

                Interlocked.Decrement(ref _queuedCount);

                var now = DateTime.UtcNow;
                if (now < nextSendUtc)
                {
                    var waitMs = (int)(nextSendUtc - now).TotalMilliseconds;
                    if (waitMs > 0)
                    {
                        try
                        {
                            Task.Delay(waitMs, token).GetAwaiter().GetResult();
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                }
                nextSendUtc = DateTime.UtcNow.AddMilliseconds(SendIntervalMs);

                try
                {
                    lock (_lock)
                    {
                        if (_port == null || !_port.IsOpen)
                        {
                            LogService.Warn("发送失败：串口未连接，已丢弃指令=" + cmd);
                            continue;
                        }
                        _port.Write(cmd);
                    }
                }
                catch (Exception ex)
                {
                    var portName = string.Empty;
                    lock (_lock)
                    {
                        portName = _port != null ? _port.PortName : string.Empty;
                    }

                    LogService.Error("发送失败，串口=" + portName + "，指令=" + cmd, ex);

                    var connectionFaulted = IsConnectionFault(ex);
                    if (connectionFaulted)
                    {
                        lock (_lock)
                        {
                            ResetBrokenPortUnsafe();
                        }
                    }

                    if (connectionFaulted)
                    {
                        try
                        {
                            ConnectionFaulted?.Invoke(this, EventArgs.Empty);
                        }
                        catch (Exception eventEx)
                        {
                            LogService.Warn("串口断线事件处理异常：" + eventEx.Message);
                        }
                    }

                    // 队列线程只记录不抛出，避免上层调用线程被风暴放大。
                }
            }
        }

        private static string GetHostProgramName()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                if (!string.IsNullOrWhiteSpace(process.ProcessName))
                    return process.ProcessName;
            }
            catch
            {
                // 忽略并回退
            }

            return AppDomain.CurrentDomain.FriendlyName;
        }

        private static bool IsConnectionFault(Exception ex)
        {
            return ex is TimeoutException
                || ex is IOException
                || ex is InvalidOperationException;
        }

        private void ResetBrokenPortUnsafe()
        {
            if (_port == null)
                return;

            try
            {
                if (_port.IsOpen)
                {
                    try
                    {
                        _port.Close();
                    }
                    catch
                    {
                        // 保持原始发送异常，不覆盖。
                    }
                }
            }
            finally
            {
                _port.DataReceived -= OnDataReceived;
                try
                {
                    _port.Dispose();
                }
                catch
                {
                    // 保持原始发送异常，不覆盖。
                }

                _port = null;
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var port = sender as SerialPort;
            if (port == null)
                return;

            string chunk;
            try
            {
                chunk = port.ReadExisting();
            }
            catch (Exception ex)
            {
                LogService.Warn("读取 MCU 串口返回失败：" + ex.Message);
                return;
            }

            if (string.IsNullOrEmpty(chunk))
                return;

            var lines = new List<string>();
            lock (_receiveLock)
            {
                _receiveBuffer.Append(chunk);
                while (true)
                {
                    var text = _receiveBuffer.ToString();
                    var newline = text.IndexOf('\n');
                    if (newline < 0)
                        break;

                    var line = text.Substring(0, newline).TrimEnd('\r');
                    _receiveBuffer.Remove(0, newline + 1);
                    if (!string.IsNullOrWhiteSpace(line))
                        lines.Add(line);
                }

                if (_receiveBuffer.Length > 4096)
                {
                    LogService.Warn("MCU 串口返回缓冲区过长，已清空。");
                    _receiveBuffer.Clear();
                }
            }

            foreach (var line in lines)
            {
                if (line.StartsWith("ERR:", StringComparison.OrdinalIgnoreCase))
                    LogService.Warn("MCU 返回：" + line);
                else
                    LogService.Debug("MCU 返回：" + line);

                try
                {
                    LineReceived?.Invoke(this, new SerialLineReceivedEventArgs(line));
                }
                catch (Exception ex)
                {
                    LogService.Warn("MCU 串口返回事件处理异常：" + ex.Message);
                }
            }
        }
    }
}
