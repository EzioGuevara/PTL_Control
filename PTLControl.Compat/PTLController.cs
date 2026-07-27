// ============================================================
// PTL LED Matrix Control System - .NET Standard 2.0 Compat
// Developer: Ezio @ IDEMIA
// Description: Public API for LED light control via serial port.
//              Supports multi-light independent control, blink,
//              marquee, and color presets (LedColor enum).
//              Compatible with .NET Framework 4.7.2 / VB.NET
// ============================================================
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PTLControl.Compat.Models;
using PTLControl.Compat.Services;

namespace PTLControl.Compat
{
    /// <summary>
    /// PTL 灯控对外接口 — 多灯独立控制。
    /// 每颗灯可独立设置常亮、闪烁、熄灭，互不干扰。
    /// 兼容 .NET Standard 2.0 / .NET Framework 4.7.2 / VB.NET
    /// </summary>
    public static class PTLController
    {
        private const int WirelessBatchSize = 60;

        // 闪烁任务表：key="Layer_Index" → CancellationTokenSource
        private static readonly Dictionary<string, CancellationTokenSource> _blinkTasks
            = new Dictionary<string, CancellationTokenSource>();
        private static readonly Dictionary<string, CancellationTokenSource> _beepTasks
            = new Dictionary<string, CancellationTokenSource>();
        private static readonly object _blinkLock = new object();
        private static readonly ConcurrentDictionary<string, NodeState> _nodeStates
            = new ConcurrentDictionary<string, NodeState>(StringComparer.OrdinalIgnoreCase);

        // 跑马灯专用
        private static CancellationTokenSource _marqueeCts;

        public static event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;
        public static event EventHandler<TagEventArgs> TagEventReceived;
        public static event EventHandler<SerialLineReceivedEventArgs> SerialLineReceived
        {
            add { SerialService.Instance.LineReceived += value; }
            remove { SerialService.Instance.LineReceived -= value; }
        }

        static PTLController()
        {
            TransportRouter.ConnectionChanged += OnTransportConnectionChanged;
            TransportRouter.TagEventReceived += OnTransportTagEventReceived;
        }

        // ── 串口管理 ──────────────────────────────────────────────────────────────

        /// <summary>获取所有可用串口名称</summary>
        public static string[] GetPortNames()
        {
            using (LogService.BeginApiCall("接口调用：GetPortNames()"))
            {
                return TransportRouter.GetPortNames();
            }
        }

        /// <summary>连接串口（例如 "COM3"）</summary>
        public static bool Connect()
        {
            using (LogService.BeginApiCall("接口调用：Connect()"))
            {
                return ConnectCore(string.Empty);
            }
        }

        /// <summary>
        /// 旧版二进制兼容入口。新代码统一使用无参 Connect，由配置决定传输层。
        /// </summary>
        public static void Connect(string portName)
        {
            ConnectCore(portName);
        }

        private static bool ConnectCore(string portName)
        {
            using (LogService.BeginApiCall("接口调用：ConnectCore(portName=" + portName + ")"))
            {
                LogService.RefreshLevelFromConfig();
                try
                {
                    TransportRouter.Connect(portName);
                    return true;
                }
                catch (Exception ex)
                {
                    // 为了避免上层未捕获导致崩溃，连接失败改为记录并返回；
                    // 调用方可通过 IsConnected 判断当前连接状态。
                    LogService.Warn("Connect 失败：" + ex.Message);
                    return false;
                }
            }
        }

        /// <summary>断开串口</summary>
        public static void Disconnect()
        {
            using (LogService.BeginApiCall("接口调用：Disconnect()"))
            {
                StopAllBeeps();
                StopAllBlinks();
                StopMarquee();
                try
                {
                    TransportRouter.Disconnect();
                }
                catch (Exception ex)
                {
                    // 断开失败不抛出到上层，避免 UI 线程崩溃。
                    LogService.Warn("Disconnect 失败：" + ex.Message);
                }
            }
        }

        /// <summary>是否已连接</summary>
        public static bool IsConnected
        {
            get
            {
                using (LogService.BeginApiCall("接口调用：IsConnected"))
                {
                    return TransportRouter.IsConnected;
                }
            }
        }

        // ── 核心灯控接口（枚举颜色版，推荐调用方使用）─────────────────────────
        /// <summary>常亮某颗灯（根据 Layer/Index，使用预定义颜色枚举）</summary>
        public static void SetLight(int layer, int index, LedColor color)
            => SetLight(layer, index, color, null);

        public static void SetLight(int layer, int index, LedColor color, bool? beep)
        {
            using (LogService.BeginApiCall("接口调用：SetLight(layer=" + layer + ", index=" + index + ", color=" + color + ", beep=" + (beep.HasValue ? beep.Value.ToString() : "null") + ")"))
            {
                int r, g, b;
                color.ToRgb(out r, out g, out b);
                SetLightRgb(layer, index, r, g, b, beep);
            }
        }

        /// <summary>旧版 RGB 接口，保留用于二进制兼容。</summary>
        public static void SetLight(int layer, int index, int r, int g, int b)
            => SetLightRgb(layer, index, r, g, b, null);

        /// <summary>常亮某颗灯（根据 Key，使用预定义颜色枚举）</summary>
        public static bool SetLight(string key, LedColor color)
            => SetLight(key, color, null);

        public static bool SetLight(string key, LedColor color, bool? beep)
        {
            using (LogService.BeginApiCall("接口调用：SetLight(key=" + key + ", color=" + color + ", beep=" + (beep.HasValue ? beep.Value.ToString() : "null") + ")"))
            {
                int r, g, b;
                color.ToRgb(out r, out g, out b);
                return SetLightRgb(key, r, g, b, beep);
            }
        }

        /// <summary>旧版 RGB 接口，保留用于二进制兼容。</summary>
        public static bool SetLight(string key, int r, int g, int b)
            => SetLightRgb(key, r, g, b, null);

        /// <summary>闪烁某颗灯（根据 Key，使用预定义颜色枚举）</summary>
        public static void SetBlink(int layer, int index, LedColor color, int intervalMs = 500)
            => SetBlink(layer, index, color, intervalMs, null);

        public static void SetBlink(int layer, int index, LedColor color, int intervalMs, bool? beep)
        {
            using (LogService.BeginApiCall("接口调用：SetBlink(layer=" + layer + ", index=" + index + ", color=" + color + ", intervalMs=" + intervalMs + ", beep=" + (beep.HasValue ? beep.Value.ToString() : "null") + ")"))
            {
                int r, g, b;
                color.ToRgb(out r, out g, out b);
                SetBlinkRgb(layer, index, r, g, b, intervalMs, beep);
            }
        }

        /// <summary>旧版 RGB 接口，保留用于二进制兼容。</summary>
        public static void SetBlink(int layer, int index, int r, int g, int b, int intervalMs)
            => SetBlinkRgb(layer, index, r, g, b, intervalMs, null);

        /// <summary>闪烁某颗灯（根据 Key，使用预定义颜色枚举）</summary>
        public static bool SetBlink(string key, LedColor color, int intervalMs = 500)
            => SetBlink(key, color, intervalMs, null);

        public static bool SetBlink(string key, LedColor color, int intervalMs, bool? beep)
        {
            using (LogService.BeginApiCall("接口调用：SetBlink(key=" + key + ", color=" + color + ", intervalMs=" + intervalMs + ", beep=" + (beep.HasValue ? beep.Value.ToString() : "null") + ")"))
            {
                int r, g, b;
                color.ToRgb(out r, out g, out b);
                return SetBlinkRgb(key, r, g, b, intervalMs, beep);
            }
        }

        /// <summary>旧版 RGB 接口，保留用于二进制兼容。</summary>
        public static bool SetBlink(string key, int r, int g, int b, int intervalMs)
            => SetBlinkRgb(key, r, g, b, intervalMs, null);

        /// <summary>跑马灯（使用预定义颜色枚举）</summary>
        public static void Marquee(LedColor color, int intervalMs = 100)
        {
            using (LogService.BeginApiCall("接口调用：Marquee(color=" + color + ", intervalMs=" + intervalMs + ")"))
            {
                int r, g, b;
                color.ToRgb(out r, out g, out b);
                MarqueeRgb(r, g, b, intervalMs);
            }
        }

        /// <summary>旧版 RGB 接口，保留用于二进制兼容。</summary>
        public static void Marquee(int r, int g, int b, int intervalMs)
            => MarqueeRgb(r, g, b, intervalMs);

        /// <summary>旧版按坐标点灯接口，保留用于二进制兼容。</summary>
        public static void LightByIndex(int layer, int index)
            => SetLightRgb(layer, index, 0, 255, 0, null);

        /// <summary>旧版按坐标点灯/闪烁接口，保留用于二进制兼容。</summary>
        public static void LightByIndex(int layer, int index, int r, int g, int b, int intervalMs)
        {
            if (intervalMs > 0)
                SetBlinkRgb(layer, index, r, g, b, intervalMs, null);
            else
                SetLightRgb(layer, index, r, g, b, null);
        }

        /// <summary>旧版按 Key 点灯接口，保留用于二进制兼容。</summary>
        public static bool LightByKey(string key)
            => SetLightRgb(key, 0, 255, 0, null);

        /// <summary>旧版按 Key 点灯/闪烁接口，保留用于二进制兼容。</summary>
        public static bool LightByKey(string key, int r, int g, int b, int intervalMs)
        {
            return intervalMs > 0
                ? SetBlinkRgb(key, r, g, b, intervalMs, null)
                : SetLightRgb(key, r, g, b, null);
        }

        // ── RGB 内部实现（不对外暴露）──────────────────────────────────────────
        private static void SetLightRgb(int layer, int index, int r, int g, int b, bool? beep)
        {
            if (TransportRouter.IsWirelessMode)
            {
                ResolvedCellInfo cell;
                if (!TryResolveCellByLayerIndex(layer, index, out cell))
                {
                    LogService.Warn("SetLight 失败：未找到 Layer/Index=" + layer + "/" + index);
                    return;
                }
                StopBeepSingle(cell);
                if (!TryPublishWirelessTask(cell, r, g, b, false, beep))
                    return;
                return;
            }

            StopMarquee();
            StopBlinkSingle(layer, index);
            var cmd = CommandService.FormatCommand(layer, index, r, g, b);
            TransportRouter.SendSerialCommand(cmd);
        }

        private static bool SetLightRgb(string key, int r, int g, int b, bool? beep)
        {
            if (TransportRouter.IsWirelessMode)
            {
                ResolvedCellInfo wirelessCell;
                if (!TryResolveCellByKey(key, out wirelessCell))
                {
                    LogService.Warn("SetLight 失败：未找到 Key/Alias=" + key);
                    return false;
                }
                StopBeepSingle(wirelessCell);
                return TryPublishWirelessTask(wirelessCell, r, g, b, false, beep);
            }

            int layer, index;
            if (!ResolveKey(key, out layer, out index))
            {
                LogService.Warn("SetLight 失败：未找到 Key/Alias=" + key);
                return false;
            }
            SetLightRgb(layer, index, r, g, b, beep);
            return true;
        }

        private static void SetBlinkRgb(int layer, int index, int r, int g, int b, int intervalMs, bool? beep)
        {
            if (intervalMs < 50)
                intervalMs = 50;
            if (TransportRouter.IsWirelessMode)
            {
                ResolvedCellInfo cell;
                if (!TryResolveCellByLayerIndex(layer, index, out cell))
                {
                    LogService.Warn("SetBlink 失败：未找到 Layer/Index=" + layer + "/" + index);
                    return;
                }
                StopBeepSingle(cell);
                if (!TryPublishWirelessTask(cell, r, g, b, true, beep))
                    return;
                return;
            }

            StopMarquee();
            StopBlinkSingle(layer, index);
            var cts = new CancellationTokenSource();
            var blinkKey = BlinkKey(layer, index);
            lock (_blinkLock)
                _blinkTasks[blinkKey] = cts;

            var token = cts.Token;
            var cmdOn  = CommandService.FormatCommand(layer, index, r, g, b);
            var cmdOff = CommandService.FormatCommand(layer, index, 0, 0, 0);

            Task.Run(async () =>
            {
                try
                {
                    bool on = true;
                    TransportRouter.SendSerialCommand(cmdOn);
                    while (!token.IsCancellationRequested)
                    {
                        try { await Task.Delay(intervalMs, token).ConfigureAwait(false); }
                        catch (TaskCanceledException) { return; }
                        on = !on;
                        TransportRouter.SendSerialCommand(on ? cmdOn : cmdOff);
                    }
                }
                catch (Exception ex)
                {
                    LogService.Error("闪烁任务异常：Layer=" + layer + ", Index=" + index, ex);
                }
                finally
                {
                    lock (_blinkLock)
                    {
                        CancellationTokenSource existing;
                        if (_blinkTasks.TryGetValue(blinkKey, out existing) && ReferenceEquals(existing, cts))
                            _blinkTasks.Remove(blinkKey);
                    }
                    cts.Dispose();
                }
            }, token);
        }

        private static bool SetBlinkRgb(string key, int r, int g, int b, int intervalMs, bool? beep)
        {
            if (TransportRouter.IsWirelessMode)
            {
                ResolvedCellInfo wirelessCell;
                if (!TryResolveCellByKey(key, out wirelessCell))
                {
                    LogService.Warn("SetBlink 失败：未找到 Key/Alias=" + key);
                    return false;
                }
                StopBeepSingle(wirelessCell);
                return TryPublishWirelessTask(wirelessCell, r, g, b, true, beep);
            }

            int layer, index;
            if (!ResolveKey(key, out layer, out index))
            {
                LogService.Warn("SetBlink 失败：未找到 Key/Alias=" + key);
                return false;
            }
            SetBlinkRgb(layer, index, r, g, b, intervalMs, beep);
            return true;
        }

        private static void TurnOffByLayerIndex(int layer, int index)
        {
            if (TransportRouter.IsWirelessMode)
            {
                ResolvedCellInfo cell;
                if (!TryResolveCellByLayerIndex(layer, index, out cell))
                {
                    LogService.Warn("TurnOff 失败：未找到 Layer/Index=" + layer + "/" + index);
                    return;
                }
                StopBeepSingle(cell);
                if (!TryPublishWirelessTask(cell, 0, 0, 0, null, false))
                    return;
                return;
            }

            StopMarquee();
            StopBlinkSingle(layer, index);
            var cmd = CommandService.FormatCommand(layer, index, 0, 0, 0);
            TransportRouter.SendSerialCommand(cmd);
        }

        /// <summary>根据 Layer/Index 熄灭（不影响其他灯）</summary>
        public static void TurnOff(int layer, int index)
        {
            using (LogService.BeginApiCall("接口调用：TurnOff(layer=" + layer + ", index=" + index + ")"))
            {
                TurnOffByLayerIndex(layer, index);
            }
        }

        /// <summary>根据 Key 熄灭（不影响其他灯）</summary>
        public static bool TurnOff(string key)
        {
            using (LogService.BeginApiCall("接口调用：TurnOff(key=" + key + ")"))
            {
                if (TransportRouter.IsWirelessMode)
                {
                    ResolvedCellInfo wirelessCell;
                    if (!TryResolveCellByKey(key, out wirelessCell))
                    {
                        LogService.Warn("TurnOff 失败：未找到 Key/Alias=" + key);
                        return false;
                    }
                    StopBeepSingle(wirelessCell);
                    return TryPublishWirelessTask(wirelessCell, 0, 0, 0, null, false);
                }

                int layer, index;
                if (!ResolveKey(key, out layer, out index))
                {
                    LogService.Warn("TurnOff 失败：未找到 Key/Alias=" + key);
                    return false;
                }
                TurnOffByLayerIndex(layer, index);
                return true;
            }
        }

        /// <summary>按 Key 蜂鸣常鸣（无线模式）</summary>
        public static bool BeepOn(string key)
        {
            using (LogService.BeginApiCall("接口调用：BeepOn(key=" + key + ")"))
            {
                if (!TransportRouter.IsWirelessMode)
                {
                    LogService.Warn("BeepOn 仅在无线模式下支持。");
                    return false;
                }

                ResolvedCellInfo cell;
                if (!TryResolveCellByKey(key, out cell))
                {
                    LogService.Warn("BeepOn 失败：未找到 Key/Alias=" + key);
                    return false;
                }

                StopBeepSingle(cell);
                return TryPublishWirelessTask(cell, 0, 0, 0, null, true);
            }
        }


        /// <summary>按 Key 蜂鸣闪烁（无线模式）</summary>
        public static bool BeepBlink(string key, int intervalMs = 500)
        {
            using (LogService.BeginApiCall("接口调用：BeepBlink(key=" + key + ", intervalMs=" + intervalMs + ")"))
            {
                if (!TransportRouter.IsWirelessMode)
                {
                    LogService.Warn("BeepBlink 仅在无线模式下支持。");
                    return false;
                }

                ResolvedCellInfo cell;
                if (!TryResolveCellByKey(key, out cell))
                {
                    LogService.Warn("BeepBlink 失败：未找到 Key/Alias=" + key);
                    return false;
                }

                StartBeepBlinkTask(cell, intervalMs);
                return true;
            }
        }


        /// <summary>按 Key 关闭蜂鸣（无线模式）</summary>
        public static bool BeepOff(string key)
        {
            using (LogService.BeginApiCall("接口调用：BeepOff(key=" + key + ")"))
            {
                if (!TransportRouter.IsWirelessMode)
                {
                    LogService.Warn("BeepOff 仅在无线模式下支持。");
                    return false;
                }

                ResolvedCellInfo cell;
                if (!TryResolveCellByKey(key, out cell))
                {
                    LogService.Warn("BeepOff 失败：未找到 Key/Alias=" + key);
                    return false;
                }

                StopBeepSingle(cell);
                return TryPublishWirelessTask(cell, 0, 0, 0, null, false);
            }
        }

        /// <summary>全部熄灭（停止所有闪烁 + 跑马灯 + 发送 OFF）</summary>
        public static void AllOff()
        {
            using (LogService.BeginApiCall("接口调用：AllOff()"))
            {
                StopAllBeeps();
                if (TransportRouter.IsWirelessMode)
                {
                    PublishAllOffWireless();
                    return;
                }

                StopMarquee();
                StopAllBlinks();
                TransportRouter.SendSerialCommand(CommandService.OffCommand);
            }
        }

        // ── 跑马灯 ────────────────────────────────────────────────────────────────

        private static void MarqueeRgb(int r, int g, int b, int intervalMs)
        {
            if (intervalMs < 50)
                intervalMs = 50;
            if (TransportRouter.IsWirelessMode)
            {
                LogService.Warn("Marquee 在无线模式下不支持，已忽略。");
                return;
            }

            StopMarquee();
            StopAllBlinks();
            TransportRouter.SendSerialCommand(CommandService.OffCommand);

            var config = ConfigService.Load();
            var strips = new List<KeyValuePair<int, int>>(); // Layer → Count
            foreach (var row in config.Rows)
            {
                int maxIdx = 0;
                foreach (var cell in row.Cells)
                    if (cell.Index > maxIdx) maxIdx = cell.Index;
                strips.Add(new KeyValuePair<int, int>(row.Layer, maxIdx + 1));
            }
            if (strips.Count == 0) return;

            _marqueeCts = new CancellationTokenSource();
            var token = _marqueeCts.Token;
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    foreach (var strip in strips)
                    {
                        int layer = strip.Key;
                        int count = strip.Value;
                        for (int i = 0; i < count; i++)
                        {
                            if (token.IsCancellationRequested) return;
                            TransportRouter.SendSerialCommand(CommandService.OffCommand);
                            TransportRouter.SendSerialCommand(CommandService.FormatCommand(layer, i, r, g, b));
                            try { await Task.Delay(intervalMs, token).ConfigureAwait(false); }
                            catch (TaskCanceledException) { return; }
                        }
                    }
                }
            }, token);
        }

        // ── 配置 ──────────────────────────────────────────────────────────────────

        /// <summary>获取所有已配置的 Key 列表</summary>
        public static IList<string> GetAllKeys()
        {
            using (LogService.BeginApiCall("接口调用：GetAllKeys()"))
            {
                var dict = ConfigService.BuildDict(ConfigService.Load());
                return new List<string>(dict.Keys);
            }
        }

        /// <summary>获取节点最近一次上报状态（入参支持 Key/Alias 或 TagId）</summary>
        public static NodeState GetNodeState(string keyOrTagId)
        {
            using (LogService.BeginApiCall("接口调用：GetNodeState(keyOrTagId=" + keyOrTagId + ")"))
            {
                if (string.IsNullOrWhiteSpace(keyOrTagId))
                    return null;

                NodeState state;
                if (_nodeStates.TryGetValue(keyOrTagId, out state))
                    return CloneState(state);

                ResolvedCellInfo cell;
                if (TryResolveCellByKey(keyOrTagId, out cell) &&
                    !string.IsNullOrWhiteSpace(cell.TagId) &&
                    _nodeStates.TryGetValue(cell.TagId, out state))
                {
                    return CloneState(state);
                }

                return null;
            }
        }

        /// <summary>
        /// 根据 TagId 反查配置中的 Key（可选带 group 参与优先匹配）。
        /// </summary>
        public static string GetKeyByTagId(string tagId, int? group = null)
        {
            using (LogService.BeginApiCall("接口调用：GetKeyByTagId(tagId=" + tagId + ", group=" + (group.HasValue ? group.Value.ToString() : "null") + ")"))
            {
                if (string.IsNullOrWhiteSpace(tagId))
                    return string.Empty;

                var mapping = ConfigService.LoadMqttMapping();
                MqttNodeConfig fallback = null;
                foreach (var node in mapping.Nodes)
                {
                    if (!string.Equals(node.TagId, tagId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (fallback == null)
                        fallback = node;

                    if (!group.HasValue || node.Group == group.Value)
                    {
                        if (!string.IsNullOrWhiteSpace(node.Key))
                            return node.Key;
                        if (!string.IsNullOrWhiteSpace(node.Alias))
                            return node.Alias;
                    }
                }

                if (fallback != null)
                {
                    if (!string.IsNullOrWhiteSpace(fallback.Key))
                        return fallback.Key;
                    if (!string.IsNullOrWhiteSpace(fallback.Alias))
                        return fallback.Alias;
                }

                return string.Empty;
            }
        }

        // ── 内部工具 ──────────────────────────────────────────────────────────────

        private static string BlinkKey(int layer, int index) =>
            layer.ToString() + "_" + index.ToString();

        private static bool ResolveKey(string key, out int layer, out int index)
        {
            var dict = ConfigService.BuildDict(ConfigService.Load());
            KeyValuePair<int, int> entry;
            if (dict.TryGetValue(key, out entry))
            {
                layer = entry.Key;
                index = entry.Value;
                return true;
            }
            layer = index = 0;
            return false;
        }

        private static bool TryResolveCellByLayerIndex(int layer, int index, out ResolvedCellInfo cellInfo)
        {
            var serialConfig = ConfigService.Load();
            var mqttConfig = ConfigService.LoadMqttMapping();
            foreach (var row in serialConfig.Rows)
            {
                if (row.Layer != layer) continue;
                foreach (var cell in row.Cells)
                {
                    if (cell.Index != index) continue;
                    var mqttNode = FindMqttNodeByLayerIndex(mqttConfig, layer, index, cell.Key, cell.Alias);
                    cellInfo = new ResolvedCellInfo
                    {
                        Layer = row.Layer,
                        Index = cell.Index,
                        Key = cell.Key ?? string.Empty,
                        Alias = cell.Alias ?? string.Empty,
                        TagId = mqttNode?.TagId ?? string.Empty,
                        Group = mqttNode?.Group ?? 0
                    };
                    return true;
                }
            }

            cellInfo = null;
            return false;
        }

        private static bool TryResolveCellByKey(string key, out ResolvedCellInfo cellInfo)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                cellInfo = null;
                return false;
            }

            var mqttDict = ConfigService.BuildMqttNodeDict(ConfigService.LoadMqttMapping());
            MqttNodeConfig mqttNode;
            if (mqttDict.TryGetValue(key, out mqttNode))
            {
                int layer;
                int index;
                if (!ResolveKey(key, out layer, out index))
                {
                    // MQTT 模式允许仅通过 mqtt_mapping（无 serial Layer/Index）定位节点。
                    layer = 0;
                    index = 0;
                }

                cellInfo = new ResolvedCellInfo
                {
                    Layer = layer,
                    Index = index,
                    Key = mqttNode.Key ?? string.Empty,
                    Alias = mqttNode.Alias ?? string.Empty,
                    TagId = mqttNode.TagId ?? string.Empty,
                    Group = mqttNode.Group
                };
                return true;
            }

            var serialConfig = ConfigService.Load();
            foreach (var row in serialConfig.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (!string.Equals(cell.Key, key, StringComparison.Ordinal) &&
                        !string.Equals(cell.Alias, key, StringComparison.Ordinal))
                        continue;

                    MqttNodeConfig mappedByCell = null;
                    var hasMqtt = false;
                    if (!string.IsNullOrWhiteSpace(cell.Key) && mqttDict.TryGetValue(cell.Key, out mappedByCell))
                        hasMqtt = true;
                    else if (!string.IsNullOrWhiteSpace(cell.Alias) && mqttDict.TryGetValue(cell.Alias, out mappedByCell))
                        hasMqtt = true;
                    cellInfo = new ResolvedCellInfo
                    {
                        Layer = row.Layer,
                        Index = cell.Index,
                        Key = cell.Key ?? string.Empty,
                        Alias = cell.Alias ?? string.Empty,
                        TagId = hasMqtt ? (mappedByCell.TagId ?? string.Empty) : string.Empty,
                        Group = hasMqtt ? mappedByCell.Group : 0
                    };
                    return true;
                }
            }

            cellInfo = null;
            return false;
        }

        private static bool TryPublishWirelessTask(ResolvedCellInfo cell, int r, int g, int b, bool? flashing, bool? beep = null)
        {
            if (cell == null)
                return false;

            if (!TransportRouter.IsConnected)
            {
                LogService.Warn("无线发送失败：MQTT 未连接。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(cell.TagId))
            {
                LogService.Warn("无线发送失败：灯位未配置 tagId。Layer=" + cell.Layer + ", Index=" + cell.Index);
                return false;
            }

            var startup = ConfigService.LoadStartup();
            var defaults = startup.WirelessDefaults;
            var timeSlot = defaults.TaskTimeSlot;
            if (flashing.HasValue && flashing.Value)
                timeSlot = defaults.BlinkTimeSlot;
            if (flashing.HasValue && !flashing.Value && r == 0 && g == 0 && b == 0)
                timeSlot = 0;
            if (!flashing.HasValue)
                timeSlot = 0;

            var task = new WirelessTask
            {
                TimeSlot = Math.Max(0, Math.Min(255, timeSlot)),
                Items = new List<WirelessTaskItem>
                {
                    new WirelessTaskItem
                    {
                        TagId = cell.TagId,
                        Group = cell.Group,
                        R = r > 0,
                        G = g > 0,
                        B = b > 0,
                        Flashing = flashing,
                        Beep = beep ?? defaults.BeepDefault
                    }
                }
            };

            try
            {
                TransportRouter.PublishWirelessTask(task);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                LogService.Warn("无线发送失败：" + ex.Message);
                return false;
            }
        }

        private static void PublishAllOffWireless()
        {
            StopMarquee();
            StopAllBlinks();

            if (!TransportRouter.IsConnected)
            {
                LogService.Warn("AllOff 跳过：MQTT 未连接。");
                return;
            }

            var config = ConfigService.LoadMqttMapping();
            var batch = new List<WirelessTaskItem>();
            foreach (var node in config.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.TagId))
                    continue;

                batch.Add(new WirelessTaskItem
                {
                    TagId = node.TagId,
                    Group = node.Group,
                    R = false,
                    G = false,
                    B = false,
                    Flashing = null,
                    Beep = false
                });

                if (batch.Count >= WirelessBatchSize)
                {
                    TransportRouter.PublishWirelessTask(new WirelessTask
                    {
                        TimeSlot = 0,
                        Items = new List<WirelessTaskItem>(batch)
                    });
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                TransportRouter.PublishWirelessTask(new WirelessTask
                {
                    TimeSlot = 0,
                    Items = batch
                });
            }
        }

        private static MqttNodeConfig FindMqttNodeByLayerIndex(
            MqttMappingConfig mqttConfig,
            int layer,
            int index,
            string key,
            string alias)
        {
            var dict = ConfigService.BuildMqttNodeDict(mqttConfig);
            MqttNodeConfig mapped;
            if (!string.IsNullOrWhiteSpace(key) && dict.TryGetValue(key, out mapped))
                return mapped;
            if (!string.IsNullOrWhiteSpace(alias) && dict.TryGetValue(alias, out mapped))
                return mapped;
            return null;
        }

        private static string TryGetMqttTagId(Dictionary<string, MqttNodeConfig> dict, string key, string alias)
        {
            MqttNodeConfig node;
            if (!string.IsNullOrWhiteSpace(key) && dict.TryGetValue(key, out node))
                return node.TagId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(alias) && dict.TryGetValue(alias, out node))
                return node.TagId ?? string.Empty;
            return string.Empty;
        }

        private static int TryGetMqttGroup(Dictionary<string, MqttNodeConfig> dict, string key, string alias)
        {
            MqttNodeConfig node;
            if (!string.IsNullOrWhiteSpace(key) && dict.TryGetValue(key, out node))
                return node.Group;
            if (!string.IsNullOrWhiteSpace(alias) && dict.TryGetValue(alias, out node))
                return node.Group;
            return 0;
        }

        private static void OnTransportConnectionChanged(object sender, ConnectionChangedEventArgs e)
        {
            ConnectionChanged?.Invoke(null, e);
        }

        private static void OnTransportTagEventReceived(object sender, TagEventArgs e)
        {
            if (e != null && !string.IsNullOrWhiteSpace(e.TagId))
            {
                _nodeStates[e.TagId] = new NodeState
                {
                    TagId = e.TagId,
                    Group = e.Group,
                    R = e.R,
                    G = e.G,
                    B = e.B,
                    IsOff = e.IsOff,
                    LastEventType = e.EventType,
                    BatteryVoltage = e.BatteryVoltage,
                    LastUpdatedUtc = e.ReceivedAtUtc
                };
            }

            TagEventReceived?.Invoke(null, e);
        }

        private static NodeState CloneState(NodeState state)
        {
            if (state == null) return null;
            return new NodeState
            {
                TagId = state.TagId,
                Group = state.Group,
                R = state.R,
                G = state.G,
                B = state.B,
                IsOff = state.IsOff,
                LastEventType = state.LastEventType,
                BatteryVoltage = state.BatteryVoltage,
                LastUpdatedUtc = state.LastUpdatedUtc
            };
        }

        private static void StartBeepBlinkTask(ResolvedCellInfo cell, int intervalMs)
        {
            if (cell == null)
                return;
            if (intervalMs < 50)
                intervalMs = 50;

            StopBeepSingle(cell);
            var cts = new CancellationTokenSource();
            var key = BeepKey(cell);
            lock (_blinkLock)
                _beepTasks[key] = cts;

            var token = cts.Token;
            Task.Run(async () =>
            {
                try
                {
                    bool on = true;
                    TryPublishWirelessTask(cell, 0, 0, 0, null, true);
                    while (!token.IsCancellationRequested)
                    {
                        try { await Task.Delay(intervalMs, token).ConfigureAwait(false); }
                        catch (TaskCanceledException) { return; }
                        on = !on;
                        TryPublishWirelessTask(cell, 0, 0, 0, null, on);
                    }
                }
                catch (Exception ex)
                {
                    LogService.Error("蜂鸣闪烁任务异常：Layer=" + cell.Layer + ", Index=" + cell.Index, ex);
                }
                finally
                {
                    lock (_blinkLock)
                    {
                        CancellationTokenSource existing;
                        if (_beepTasks.TryGetValue(key, out existing) && ReferenceEquals(existing, cts))
                            _beepTasks.Remove(key);
                    }
                    cts.Dispose();
                }
            }, token);
        }

        private static void StopBlinkSingle(int layer, int index)
        {
            var key = BlinkKey(layer, index);
            lock (_blinkLock)
            {
                CancellationTokenSource cts;
                if (_blinkTasks.TryGetValue(key, out cts))
                {
                    cts.Cancel();
                    _blinkTasks.Remove(key);
                }
            }
        }

        private static void StopBeepSingle(int layer, int index)
        {
            var key = BlinkKey(layer, index);
            lock (_blinkLock)
            {
                CancellationTokenSource cts;
                if (_beepTasks.TryGetValue(key, out cts))
                {
                    cts.Cancel();
                    _beepTasks.Remove(key);
                }
            }
        }

        private static void StopBeepSingle(ResolvedCellInfo cell)
        {
            if (cell == null)
                return;
            var key = BeepKey(cell);
            lock (_blinkLock)
            {
                CancellationTokenSource cts;
                if (_beepTasks.TryGetValue(key, out cts))
                {
                    cts.Cancel();
                    _beepTasks.Remove(key);
                }
            }
        }

        private static string BeepKey(ResolvedCellInfo cell)
        {
            if (cell == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(cell.TagId))
                return "T:" + cell.TagId + ":" + cell.Group.ToString();
            return BlinkKey(cell.Layer, cell.Index);
        }

        private static void StopAllBlinks()
        {
            lock (_blinkLock)
            {
                foreach (var kvp in _blinkTasks)
                {
                    kvp.Value.Cancel();
                }
                _blinkTasks.Clear();
            }
        }

        private static void StopAllBeeps()
        {
            lock (_blinkLock)
            {
                foreach (var kvp in _beepTasks)
                {
                    kvp.Value.Cancel();
                }
                _beepTasks.Clear();
            }
        }

        private static void StopMarquee()
        {
            if (_marqueeCts != null)
            {
                _marqueeCts.Cancel();
                _marqueeCts.Dispose();
                _marqueeCts = null;
            }
        }

        private sealed class ResolvedCellInfo
        {
            public int Layer { get; set; }
            public int Index { get; set; }
            public string Key { get; set; }
            public string Alias { get; set; }
            public string TagId { get; set; }
            public int Group { get; set; }
        }
    }
}
