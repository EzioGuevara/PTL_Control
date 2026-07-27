using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using PTLControl.Compat.Models;

namespace PTLControl.Compat.Services
{
    internal sealed class MqttEStationService : ITransportService
    {
        private const int MaxPublishQueueSize = 2000;
        private const int PublishIntervalMs = 20;

        private readonly object _sync = new object();
        private readonly ConcurrentQueue<WirelessTask> _publishQueue = new ConcurrentQueue<WirelessTask>();
        private readonly AutoResetEvent _publishSignal = new AutoResetEvent(false);
        private IMqttClient _client;
        private DateTime? _lastHeartbeatUtc;
        private CancellationTokenSource _publisherCts;
        private System.Threading.Tasks.Task _publisherTask;
        private int _queuedCount;
        private Timer _reconnectTimer;
        private int _reconnectAttempt;
        private volatile bool _manualDisconnect;

        public string TransportType => "mqtt";
        public bool IsConnected => _client != null && _client.IsConnected;
        public DateTime? LastHeartbeatUtc => _lastHeartbeatUtc;

        public event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;
        public event EventHandler<TagEventArgs> TagEventReceived;

        public void Connect(string endpoint)
        {
            lock (_sync)
            {
                if (_client != null && _client.IsConnected)
                    return;

                _manualDisconnect = false;
                CancelReconnectUnsafe();
                CleanupDisconnectedClientUnsafe();

                var startup = ConfigService.LoadStartup();
                var mqtt = startup.Mqtt;
                if (string.IsNullOrWhiteSpace(mqtt.EStationId))
                    throw new InvalidOperationException("MQTT 模式必须配置 startup_config.json 的 mqtt.eStationId。");
                var appName = GetHostProgramName();
                LogService.Info(
                    "开始连接MQTT：" + mqtt.Broker + ":" + mqtt.Port +
                    "，eStationId=" + mqtt.EStationId +
                    "，来源程序=" + appName);

                var factory = new MqttFactory();
                _client = factory.CreateMqttClient();
                _client.ApplicationMessageReceivedAsync += HandleMessageAsync;
                _client.DisconnectedAsync += HandleDisconnectedAsync;
                _client.ConnectedAsync += HandleConnectedAsync;

                var builder = new MqttClientOptionsBuilder()
                    .WithTcpServer(mqtt.Broker, mqtt.Port)
                    .WithClientId("PTLControlCompat-" + Guid.NewGuid().ToString("N").Substring(0, 8))
                    .WithProtocolVersion(MqttProtocolVersion.V311)
                    .WithCleanSession()
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(mqtt.KeepAliveSec));

                if (!string.IsNullOrWhiteSpace(mqtt.Username))
                    builder = builder.WithCredentials(mqtt.Username, mqtt.Password ?? string.Empty);

                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                    {
                        _client.ConnectAsync(builder.Build(), cts.Token).GetAwaiter().GetResult();
                    }
                    SubscribeCore(mqtt);
                    StartPublisherLoopUnsafe();
                    _reconnectAttempt = 0;
                }
                catch (Exception ex)
                {
                    var message = "MQTT 连接失败，请检查 Broker 是否启动、地址端口/账号密码是否正确。";
                    LogService.Error(
                        "MQTT 连接失败：" + mqtt.Broker + ":" + mqtt.Port +
                        "，eStationId=" + mqtt.EStationId +
                        "，来源程序=" + appName, ex);
                    LogService.Warn(message + " 详情：" + ex.Message);

                    try
                    {
                        if (_client != null)
                        {
                            DetachClientHandlersUnsafe(_client);
                            _client.Dispose();
                        }
                    }
                    catch
                    {
                        // 忽略清理异常
                    }
                    finally
                    {
                        StopPublisherLoopSignalUnsafe();
                        StopPublisherLoopWaitUnsafe();
                        ClearPublishQueueUnsafe();
                        _client = null;
                        _lastHeartbeatUtc = null;
                    }

                    throw new InvalidOperationException(message, ex);
                }

                LogService.Info(
                    "MQTT 连接成功：" + mqtt.Broker + ":" + mqtt.Port +
                    "，eStationId=" + mqtt.EStationId +
                    "，来源程序=" + appName);
            }
        }

        public void Disconnect()
        {
            lock (_sync)
            {
                _manualDisconnect = true;
                CancelReconnectUnsafe();
                _reconnectAttempt = 0;
                if (_client == null)
                    return;

                try
                {
                    if (_client.IsConnected)
                    {
                        LogService.Info("开始断开MQTT连接。");
                        _client.DisconnectAsync().GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    LogService.Error("MQTT 断开异常。", ex);
                    throw;
                }
                finally
                {
                    StopPublisherLoopSignalUnsafe();
                    StopPublisherLoopWaitUnsafe();
                    ClearPublishQueueUnsafe();
                    _lastHeartbeatUtc = null;
                    DetachClientHandlersUnsafe(_client);
                    _client.Dispose();
                    _client = null;
                    LogService.Info("MQTT 已断开。");
                    ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
                    {
                        TransportType = TransportType,
                        IsConnected = false,
                        Message = "MQTT 已断开"
                    });
                }
            }
        }

        public string[] GetPortNames()
        {
            return new string[0];
        }

        public void SendSerialCommand(string cmd)
        {
            throw new NotSupportedException("MQTT 模式不支持串口文本指令。");
        }

        public void PublishWirelessTask(WirelessTask task)
        {
            if (task == null || task.Items == null || task.Items.Count == 0)
                return;

            lock (_sync)
            {
                if (_client == null || !_client.IsConnected)
                {
                    LogService.Warn("发送失败：MQTT 未连接，任务项数=" + task.Items.Count);
                    return;
                }
            }

            // 统一入队，防止上层命令风暴直接冲击 MQTT 客户端。
            EnqueuePublishTask(CloneTask(task));
        }

        private void SubscribeCore(MqttStartupConfig mqtt)
        {
            var resultTopic = string.Format("/estation/{0}/result", mqtt.EStationId);
            var heartbeatTopic = string.Format("/estation/{0}/heartbeat", mqtt.EStationId);
            var qos = ToQos(mqtt.Qos);

            _client.SubscribeAsync(resultTopic, qos).GetAwaiter().GetResult();
            _client.SubscribeAsync(heartbeatTopic, qos).GetAwaiter().GetResult();
            LogService.Info("MQTT 订阅成功：result=" + resultTopic + "，heartbeat=" + heartbeatTopic + "，qos=" + mqtt.Qos);
        }

        private System.Threading.Tasks.Task HandleConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
            {
                TransportType = TransportType,
                IsConnected = true,
                Message = "MQTT 已连接"
            });
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private System.Threading.Tasks.Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            _lastHeartbeatUtc = null;
            if (_manualDisconnect)
                return System.Threading.Tasks.Task.CompletedTask;

            lock (_sync)
            {
                if (!_manualDisconnect)
                    ScheduleReconnectUnsafe();
            }

            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs
            {
                TransportType = TransportType,
                IsConnected = false,
                Message = "MQTT 连接断开"
            });
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private void ScheduleReconnectUnsafe()
        {
            if (_reconnectTimer != null)
                return;

            var attempt = Math.Min(_reconnectAttempt++, 5);
            var delaySeconds = Math.Min(30, 1 << attempt);
            LogService.Warn("MQTT 连接断开，将在 " + delaySeconds + " 秒后尝试重连。");
            _reconnectTimer = new Timer(_ => ReconnectTimerCallback(), null, TimeSpan.FromSeconds(delaySeconds), Timeout.InfiniteTimeSpan);
        }

        private void ReconnectTimerCallback()
        {
            lock (_sync)
            {
                if (_reconnectTimer != null)
                {
                    _reconnectTimer.Dispose();
                    _reconnectTimer = null;
                }
                if (_manualDisconnect || (_client != null && _client.IsConnected))
                    return;
            }

            try
            {
                Connect(string.Empty);
            }
            catch (Exception ex)
            {
                LogService.Warn("MQTT 自动重连失败：" + ex.Message);
                lock (_sync)
                {
                    if (!_manualDisconnect)
                        ScheduleReconnectUnsafe();
                }
            }
        }

        private void CancelReconnectUnsafe()
        {
            if (_reconnectTimer == null)
                return;
            _reconnectTimer.Dispose();
            _reconnectTimer = null;
        }

        private void CleanupDisconnectedClientUnsafe()
        {
            if (_client == null || _client.IsConnected)
                return;
            DetachClientHandlersUnsafe(_client);
            try
            {
                _client.Dispose();
            }
            catch
            {
                // 已断线客户端清理失败不阻塞新连接。
            }
            _client = null;
        }

        private void DetachClientHandlersUnsafe(IMqttClient client)
        {
            if (client == null)
                return;
            client.ApplicationMessageReceivedAsync -= HandleMessageAsync;
            client.DisconnectedAsync -= HandleDisconnectedAsync;
            client.ConnectedAsync -= HandleConnectedAsync;
        }

        private System.Threading.Tasks.Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            try
            {
                var topic = arg.ApplicationMessage?.Topic ?? string.Empty;
                // MQTTnet 4 在 net472 下仍以 Payload(byte[]) 为主，这里保持兼容实现。
#pragma warning disable CS0618
                var payload = GetPayload(arg.ApplicationMessage?.Payload);
#pragma warning restore CS0618

                if (topic.EndsWith("/heartbeat", StringComparison.OrdinalIgnoreCase))
                {
                    _lastHeartbeatUtc = DateTime.UtcNow;
                    PublishHeartbeatEvent(payload);
                    return System.Threading.Tasks.Task.CompletedTask;
                }

                if (topic.EndsWith("/result", StringComparison.OrdinalIgnoreCase))
                {
                    _lastHeartbeatUtc = DateTime.UtcNow;
                    PublishResultEvents(payload);
                }
            }
            catch (Exception ex)
            {
                LogService.Warn("处理 MQTT 消息失败：" + ex.Message);
            }

            return System.Threading.Tasks.Task.CompletedTask;
        }

        private void PublishHeartbeatEvent(string payload)
        {
            try
            {
                var hb = JsonConvert.DeserializeObject<HeartbeatPayload>(payload) ?? new HeartbeatPayload();
                TagEventReceived?.Invoke(this, new TagEventArgs
                {
                    EStationId = hb.ID ?? string.Empty,
                    EventType = TagEventType.Heartbeat,
                    ReceivedAtUtc = DateTime.UtcNow,
                    RawPayload = payload
                });
            }
            catch (Exception ex)
            {
                LogService.Warn("解析 heartbeat 失败：" + ex.Message);
            }
        }

        private void PublishResultEvents(string payload)
        {
            var result = JsonConvert.DeserializeObject<TaskResultPayload>(payload);
            if (result == null || result.Results == null)
                return;

            foreach (var item in result.Results)
            {
                var color = (item.Colors != null && item.Colors.Count > 0) ? item.Colors[0] : new RgbPayload();
                var eventType = MapEventType(item.ResultType);
                var batteryV = item.Battery <= 0 ? 0.0 : item.Battery / 10.0;

                TagEventReceived?.Invoke(this, new TagEventArgs
                {
                    EStationId = result.ID ?? string.Empty,
                    TagId = item.TagID ?? string.Empty,
                    Group = item.Group,
                    EventType = eventType,
                    R = color.R,
                    G = color.G,
                    B = color.B,
                    IsOff = !color.R && !color.G && !color.B,
                    BatteryVoltage = batteryV,
                    ReceivedAtUtc = DateTime.UtcNow,
                    RawPayload = payload
                });
            }
        }

        private static TagEventType MapEventType(int resultType)
        {
            if (resultType == 0xFD) return TagEventType.Button;
            if (resultType == 0xFE) return TagEventType.Communication;
            if (resultType == 0xFF) return TagEventType.Heartbeat;
            return TagEventType.Unknown;
        }

        private static MqttQualityOfServiceLevel ToQos(int qos)
        {
            if (qos <= 0) return MqttQualityOfServiceLevel.AtMostOnce;
            if (qos == 1) return MqttQualityOfServiceLevel.AtLeastOnce;
            return MqttQualityOfServiceLevel.ExactlyOnce;
        }

        private static string BuildTaskPayload(WirelessTask task)
        {
            var items = new List<Dictionary<string, object>>();
            foreach (var item in task.Items)
            {
                var one = new Dictionary<string, object>();
                one["TagID"] = item.TagId;
                one["Beep"] = item.Beep;
                one["Colors"] = new[]
                {
                    new Dictionary<string, bool>
                    {
                        { "R", item.R },
                        { "G", item.G },
                        { "B", item.B }
                    }
                };

                if (item.Flashing.HasValue)
                    one["Flashing"] = item.Flashing.Value;

                items.Add(one);
            }

            var root = new Dictionary<string, object>
            {
                { "Items", items },
                { "Time", task.TimeSlot }
            };
            return JsonConvert.SerializeObject(root);
        }

        private void EnqueuePublishTask(WirelessTask task)
        {
            var count = Interlocked.Increment(ref _queuedCount);
            if (count > MaxPublishQueueSize)
            {
                WirelessTask dropped;
                if (_publishQueue.TryDequeue(out dropped))
                {
                    Interlocked.Decrement(ref _queuedCount);
                    LogService.Warn("MQTT 发布队列过长，已丢弃最早任务：" + SummarizeTask(dropped));
                }
            }

            _publishQueue.Enqueue(task);
            _publishSignal.Set();
        }

        private void StartPublisherLoopUnsafe()
        {
            StopPublisherLoopSignalUnsafe();
            StopPublisherLoopWaitUnsafe();

            _publisherCts = new CancellationTokenSource();
            var token = _publisherCts.Token;
            _publisherTask = System.Threading.Tasks.Task.Run(() => PublisherLoop(token), token);
        }

        private void StopPublisherLoopSignalUnsafe()
        {
            try
            {
                if (_publisherCts != null)
                {
                    _publisherCts.Cancel();
                    _publishSignal.Set();
                }
            }
            catch
            {
                // 忽略停止流程异常，避免影响连接关闭。
            }
        }

        private void StopPublisherLoopWaitUnsafe()
        {
            try
            {
                if (_publisherTask != null)
                    _publisherTask.Wait(1000);
            }
            catch
            {
                // 忽略退出等待异常，避免影响连接关闭。
            }
            finally
            {
                if (_publisherCts != null)
                {
                    _publisherCts.Dispose();
                    _publisherCts = null;
                }

                _publisherTask = null;
            }
        }

        private void ClearPublishQueueUnsafe()
        {
            WirelessTask _;
            while (_publishQueue.TryDequeue(out _))
            {
                // clear
            }

            Interlocked.Exchange(ref _queuedCount, 0);
        }

        private void PublisherLoop(CancellationToken token)
        {
            var nextPublishUtc = DateTime.UtcNow;
            while (!token.IsCancellationRequested)
            {
                WirelessTask task;
                if (!_publishQueue.TryDequeue(out task))
                {
                    _publishSignal.WaitOne(50);
                    continue;
                }

                Interlocked.Decrement(ref _queuedCount);

                var now = DateTime.UtcNow;
                if (now < nextPublishUtc)
                {
                    var waitMs = (int)(nextPublishUtc - now).TotalMilliseconds;
                    if (waitMs > 0)
                    {
                        try
                        {
                            System.Threading.Tasks.Task.Delay(waitMs, token).GetAwaiter().GetResult();
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                }
                nextPublishUtc = DateTime.UtcNow.AddMilliseconds(PublishIntervalMs);

                PublishTaskCore(task);
            }
        }

        private void PublishTaskCore(WirelessTask task)
        {
            IMqttClient client;
            MqttStartupConfig mqtt;

            lock (_sync)
            {
                client = _client;
                if (client == null || !client.IsConnected)
                {
                    LogService.Warn("MQTT 未连接，已丢弃队列任务：" + SummarizeTask(task));
                    return;
                }

                var startup = ConfigService.LoadStartup();
                mqtt = startup.Mqtt;
                if (string.IsNullOrWhiteSpace(mqtt.EStationId))
                {
                    LogService.Warn("MQTT 任务发送失败：未配置 eStationId，已丢弃任务：" + SummarizeTask(task));
                    return;
                }
            }

            var payload = BuildTaskPayload(task);
            var topic = string.Format("/estation/{0}/task", mqtt.EStationId);
            var qos = ToQos(mqtt.Qos);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qos)
                .Build();

            try
            {
                client.PublishAsync(message).GetAwaiter().GetResult();
                LogService.Info("无线任务已发布：topic=" + topic + "，items=" + task.Items.Count + "，" + SummarizeTask(task));
            }
            catch (Exception ex)
            {
                LogService.Error("无线任务发布失败：topic=" + topic + "，items=" + task.Items.Count + "，" + SummarizeTask(task), ex);
            }
        }

        private static WirelessTask CloneTask(WirelessTask source)
        {
            var clone = new WirelessTask
            {
                TimeSlot = source.TimeSlot,
                Items = new List<WirelessTaskItem>()
            };

            if (source.Items == null)
                return clone;

            foreach (var item in source.Items)
            {
                if (item == null)
                    continue;

                clone.Items.Add(new WirelessTaskItem
                {
                    TagId = item.TagId,
                    Group = item.Group,
                    R = item.R,
                    G = item.G,
                    B = item.B,
                    Flashing = item.Flashing,
                    Beep = item.Beep
                });
            }

            return clone;
        }

        private static string GetPayload(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return string.Empty;
            return Encoding.UTF8.GetString(payload);
        }

        private static string SummarizeTask(WirelessTask task)
        {
            if (task == null || task.Items == null || task.Items.Count == 0)
                return "time=0，task=empty";

            var first = task.Items[0];
            var flashText = first.Flashing.HasValue ? first.Flashing.Value.ToString() : "null";
            var more = task.Items.Count > 1 ? "，more=" + (task.Items.Count - 1) : string.Empty;
            return "time=" + task.TimeSlot +
                   "，tagId=" + (first.TagId ?? string.Empty) +
                   "，group=" + first.Group +
                   "，color=" + DescribeColor(first.R, first.G, first.B) +
                   "，flashing=" + flashText +
                   "，beep=" + first.Beep + more;
        }

        private static string DescribeColor(bool r, bool g, bool b)
        {
            if (!r && !g && !b) return "Off";
            if (r && !g && !b) return "Red";
            if (!r && g && !b) return "Green";
            if (!r && !g && b) return "Blue";
            if (r && g && !b) return "Yellow/Orange";
            if (!r && g && b) return "Cyan";
            if (r && !g && b) return "Purple";
            if (r && g && b) return "White";
            return "Unknown";
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

        private sealed class HeartbeatPayload
        {
            public string ID { get; set; }
        }

        private sealed class TaskResultPayload
        {
            public string ID { get; set; }
            public List<TaskItemResultPayload> Results { get; set; }
        }

        private sealed class TaskItemResultPayload
        {
            public string TagID { get; set; }
            public int ResultType { get; set; }
            public int Battery { get; set; }
            public int Group { get; set; }
            public List<RgbPayload> Colors { get; set; }
        }

        private sealed class RgbPayload
        {
            public bool R { get; set; }
            public bool G { get; set; }
            public bool B { get; set; }
        }
    }
}
