# PTLControl.Compat —  VB.NET / .NET Framework 4.7.2 开发者的集成说明

主程序 `PTLControl.App` 跑在 .NET 10 上，4.7.2 的项目没法直接引用它。这个库（`PTLControl.Compat`）用 .NET Standard 2.0 重新封装了同一套灯控 API，4.7.2 和 .NET 10 都能引用 Standard 2.0，所以直接用这个就行。

---

[TOC]

## 项目结构

| 项目 | 说明 |
|------|------|
| `PTLControl.Compat` | 对外 DLL 库（本目录），串口 + MQTT 双模式 |
| `PTLControl.App` | 主程序（.NET 10 WinForms），灯位映射、连接配置、矩阵测试 |
| `PTLControl.Demo` | 演示程序，引用 App 层封装 |

第三方集成只需 `PTLControl.Compat` 及其依赖 DLL，不需要引用 App 或 Demo。

---

## 怎么拿到 DLL

在项目根目录跑一下：

```bat
dotnet build PTLControl.Compat\PTLControl.Compat.csproj -c Release
```

然后去 `PTLControl.Compat\bin\Release\net472\` 把这三个文件复制走：

- `PTLControl.Compat.dll` — 主库
- `Newtonsoft.Json.dll` — 读配置文件用的
- `MQTTnet.dll` — MQTT 通信依赖（`connectionMode=mqtt` 时必须）

> `System.IO.Ports` 在 .NET Framework 里是内置的，不需要额外的 dll。  
> `MQTTnet.dll` 不需要在 VB/C# 项目里“手动添加代码引用”才可运行，但运行时必须能被加载到（最简单就是与 EXE 放同目录）。

---

## 在 VB 项目里加引用

把上面三个 dll 放到你的 VB 项目里（比如建个 `Lib\` 文件夹），然后 Visual Studio 里右键项目 → **添加引用** → **浏览** → 至少把 `PTLControl.Compat.dll`、`Newtonsoft.Json.dll` 加上。  
`MQTTnet.dll` 建议也加引用（更直观），但即使不加，最终发布时也要随程序部署到 EXE 目录或可探测路径。

---

## 怎么用

文件顶部加两行 Imports：

```vb
Imports PTLControl.Compat
Imports PTLControl.Compat.Models
```

然后就可以直接调了：

```vb
' 连接（自动读取 startup_config.json：serial/mqtt 以及连接参数）
Dim ok As Boolean = PTLController.Connect()
If Not ok OrElse Not PTLController.IsConnected Then
    Console.WriteLine("连接失败，请检查配置与链路。")
    Return
End If

' 绿色常亮（按 key）
PTLController.SetLight("A1", LedColor.Green)

' 红色闪烁，500ms 间隔（按 key）
PTLController.SetBlink("A1", LedColor.Red, 500)

' 关掉某一颗（按 key）
PTLController.TurnOff("A1")

' 全灭
PTLController.AllOff()

' 断开
PTLController.Disconnect()
```

---

## 第三方“傻瓜模式”最小调用（推荐）

给外部调用方只保留 3 类动作：

1) 连接/断开  
2) 查看当前连接状态  
3) 灯控动作（点亮/闪烁/熄灭/换色）

```vb
' 1) 连接（按配置文件自动连接，返回是否成功）
Dim ok As Boolean = PTLController.Connect()

' 2) 状态
Dim connected As Boolean = PTLController.IsConnected

' 3) 灯控（统一按 key 调用）
PTLController.SetLight("A1", LedColor.Green)
PTLController.SetBlink("A1", LedColor.Red, 500)
PTLController.TurnOff("A1")
PTLController.AllOff()

' 结束
PTLController.Disconnect()
```

说明：

- `Connect()` 不需要传参，具体连谁、怎么连，全部来自 `%AppData%\PTLControl\startup_config.json`。
- `Connect()` 返回 `Boolean`：失败时**不抛异常**，只记 Warn 日志并返回 `False`；请同时检查 `IsConnected`。
- 如果 `connectionMode=serial`，使用 `serial.portName`。新代码统一调用无参 `Connect()`；旧版 `Connect("COM3")` 仍保留为无返回值兼容入口。
- 如果 `connectionMode=mqtt`，使用 `mqtt.broker/port/username/password/eStationId`。
- 对外灯控统一按 **key 接口** 调用，颜色只传 `LedColor` 枚举，不提供 RGB 对外接口。

完整示例看 `PTL_CTRL\VBNet_Example\PTLLightDemo.vb`。  
MQTT 专项说明见同目录 `README.MQTT.md`。

---

## 所有 API（对外）

> 以下均为第三方推荐用法。库内部虽保留 `layer/index` 重载，但**不对外文档化**，第三方请始终按 key 调用。

### 连接管理

```vb
PTLController.GetPortNames()          ' String()，列出可用串口（serial 模式选口用）
PTLController.Connect()               ' Boolean，按 startup_config 自动连接（推荐）
PTLController.Connect("COM3")         ' 旧版兼容入口，无返回值
PTLController.Disconnect()            ' 断开当前传输层
PTLController.IsConnected             ' Boolean，当前模式连接状态
```

`IsConnected` 含义：

- **serial**：串口句柄是否已打开（非 Arduino 实时心跳探测）。
- **mqtt**：MQTT 客户端已连接，且距上次心跳未超过 `wirelessDefaults.heartbeatTimeoutSec`（首次连上尚未收到心跳时先视为已连接）。

### 点灯（常亮）

```vb
PTLController.SetLight("A1", LedColor.Green)              ' Boolean
PTLController.SetLight("A1", LedColor.Green, True)        ' 可选 beep（仅 MQTT 有效）
```

### 闪烁

```vb
PTLController.SetBlink("A1", LedColor.Red, 500)           ' Boolean，500ms 间隔（serial 本地闪烁）
PTLController.SetBlink("A1", LedColor.Yellow, 400, False) ' 可选 beep
```

> **注意**：MQTT 模式下 `intervalMs` 不直接下发给灯条，闪烁时长由 `wirelessDefaults.blinkTimeSlot` 控制（见下文配置说明）。serial 模式下 `intervalMs` 为本地 on/off 切换间隔。

### 熄灭

```vb
PTLController.TurnOff("A1")           ' Boolean，关单颗，不影响其他灯
PTLController.AllOff()                ' 全灭，同时停闪烁、跑马灯、蜂鸣
```

### 跑马灯（仅 serial）

```vb
PTLController.Marquee(LedColor.Blue, 80)   ' 80ms 一跳；MQTT 模式会忽略并记 Warn
' 停止跑马灯：调 AllOff()，或调用 SetLight/SetBlink/TurnOff
```

### 蜂鸣（仅 MQTT）

```vb
PTLController.BeepOn("A1")                ' Boolean，常鸣
PTLController.BeepBlink("A1", 500)        ' Boolean，闪鸣
PTLController.BeepOff("A1")               ' Boolean，关闭蜂鸣
```

> serial 模式下蜂鸣接口返回 `False` 并记 Warn，不会抛异常。

### Key 与状态

```vb
Dim keys As IList(Of String) = PTLController.GetAllKeys()
Dim state = PTLController.GetNodeState("A1")          ' 或 tagId
Dim key As String = PTLController.GetKeyByTagId(ev.TagId, ev.Group)
```

### 事件（MQTT 双向）

```vb
AddHandler PTLController.ConnectionChanged, Sub(s, e)
    Console.WriteLine($"连接变化：{e.TransportType} connected={e.IsConnected}")
End Sub

AddHandler PTLController.TagEventReceived, Sub(s, ev)
    Dim key = PTLController.GetKeyByTagId(ev.TagId, ev.Group)
    Console.WriteLine($"回传：{ev.EventType}, tagId={ev.TagId}, key={key}")
End Sub

AddHandler PTLController.SerialLineReceived, Sub(s, ev)
    Console.WriteLine($"MCU 返回：{ev.Line}") ' 例如 OK:SET / ERR:CMD
End Sub
```

### 返回值与异常行为

| 接口 | 返回值 | 失败时行为 |
|------|--------|------------|
| `Connect()` | `Boolean` | 不抛异常，记 Warn |
| `SetLight(key)` / `SetBlink(key)` / `TurnOff(key)` | `Boolean` | key 未映射 → `False`；MQTT 未连接 → `False` |
| `BeepOn/BeepBlink/BeepOff` | `Boolean` | serial 模式 → `False`；key 未映射 → `False` |
| `AllOff()` | 无 | MQTT 未连接时跳过并记 Warn |
| serial 发送 | — | 串口未连接时 `Send` 仍可能抛 `InvalidOperationException` |

---

## Serial vs MQTT 能力对照

| 能力 | Serial | MQTT |
|------|--------|------|
| 连接参数 | `serial.portName` | `mqtt.broker/port/.../eStationId` |
| 点位映射 | `serial_mapping.json`（Key → Layer/Index） | `mqtt_mapping.json`（Key → tagId/group） |
| `SetLight` / `SetBlink` / `TurnOff` | 按 key 解析到 Layer/Index 发文本指令 | 按 key 解析到 tagId 发 `/task` |
| 闪烁间隔 `intervalMs` | 本地 on/off 周期 | 由 `blinkTimeSlot` 决定总时长 |
| 常亮时长 | 持续到 `TurnOff` / `AllOff` | 由 `taskTimeSlot` 决定 |
| `Marquee` | 支持 | 不支持（忽略 + Warn） |
| `BeepOn/BeepBlink/BeepOff` | 不支持 | 支持 |
| `TagEventReceived` | 无 | 订阅 result/heartbeat |
| `IsConnected` | 串口是否打开 | MQTT 连接 + 心跳超时判断 |

---

## 配置文件

均在 `%AppData%\PTLControl\`：

| 文件 | 用途 |
|------|------|
| `startup_config.json` | 连接模式、日志级别、串口/MQTT 参数、无线默认项 |
| `serial_mapping.json` | 串口灯位映射（Key/Alias → Layer/Index） |
| `mqtt_mapping.json` | 无线节点映射（key/tagId/group/alias） |

`startup_config.json` 示例：

```json
{
  "connectionMode": "mqtt",
  "logLevel": "Info",
  "serial": { "portName": "COM3" },
  "mqtt": {
    "broker": "127.0.0.1",
    "port": 1883,
    "username": "test",
    "password": "123456",
    "eStationId": "90A9F7300000",
    "qos": 1,
    "keepAliveSec": 30
  },
  "wirelessDefaults": {
    "taskTimeSlot": 24,
    "blinkTimeSlot": 5,
    "beepDefault": false,
    "heartbeatTimeoutSec": 90
  }
}
```

### `wirelessDefaults` 说明（MQTT）

| 字段 | 含义 |
|------|------|
| `taskTimeSlot` | `SetLight` 常亮任务的 TimeSlot（1 挡 = 5 秒）。例：`24` → 约 120 秒 |
| `blinkTimeSlot` | `SetBlink` 闪烁任务的 TimeSlot（1 挡 = 5 秒）。例：`5` → 约 25 秒 |
| `beepDefault` | `SetLight/SetBlink` 未显式传 `beep` 时的默认值 |
| `heartbeatTimeoutSec` | 超过此秒数未收到心跳则 `IsConnected` 变为 `False` |

> 同一配置下，`SetLight` 与 `SetBlink` 亮灯时长不同是**预期行为**（分别走 `taskTimeSlot` / `blinkTimeSlot`），不是 DLL 版本差异。

`mqtt_mapping.json` 示例：

```json
{
  "nodes": [
    { "key": "001", "tagId": "AD1E000E912A", "group": 0, "alias": "" }
  ]
}
```

兼容说明：

- 旧的 `ptl_config.json` 会在首次读取时自动迁移到 `serial_mapping.json`。
- MQTT 模式下 `tagId` 为空的节点拒绝发送并记 Warn。
- `GetAllKeys()` 返回配置中的真实 Key/Alias（区分大小写）。

---

## 发送队列与抗风暴（内置）

Serial 与 MQTT 底层均已实现统一发送策略，避免上层并发或失败补偿导致“指令风暴”：

| 机制 | Serial | MQTT |
|------|--------|------|
| 入队 | 所有 `Send(cmd)` 进队列 | 所有 `PublishWirelessTask` 进队列 |
| 消费 | 单后台线程顺序写出 | 单后台线程顺序发布 |
| 限速 | 相邻两条间隔 ≥ 20ms | 相邻两条间隔 ≥ 20ms |
| 队列上限 | 2000 条，超出丢弃最早一条并记 Warn | 2000 条，超出丢弃最早一条并记 Warn |
| 发送线程异常 | 只记日志，不向调用线程抛 | 只记日志，不向调用线程抛 |

建议上层仍遵守：

- 程序启动时连接一次，退出时断开；不要在每次点灯后立刻 `Disconnect()`。
- 失败补偿不要循环 `AllOff()`，否则仍会放大队列压力。
- 多进程不要抢同一 COM 口；MQTT 侧注意 Broker 容量。

---

## 与 PTLControl.App 主程序的差异

- `PTLControl.App`（.NET 10 WinForms）带 UI：串口连接、扫码发送、映射管理、矩阵测试。
- `PTLControl.Compat` 是纯 API 库，不包含上述窗体功能。
- 两者共用同一份配置目录：`%AppData%\PTLControl\`。
- 主程序常用业务动作在 Compat 可直接等价调用：
  - 查询（绿色常亮）≈ `SetLight(key, LedColor.Green)`
  - 查询（红色闪烁）≈ `SetBlink(key, LedColor.Red, 500)`
  - 查询（绿色闪烁）≈ `SetBlink(key, LedColor.Green, 500)`

---

## 日志配置

库已内置中文文件日志，从 `startup_config.json` 读取 `logLevel`。

可选值：

- `Off`：关闭日志
- `Info`：日常运行（默认）。连接/断开、异常与告警
- `Debug`：在 `Info` 基础上记录 API 调用参数；每次调用附带 `traceId`

日志位置：

- 目录：`%AppData%\PTLControl\logs\`
- 文件：`ptl-YYYY-MM-DD.log`（按天滚动）

格式示例：

```text
2026-06-10 14:23:45.123 [信息] [trace:7F3A9C1D] 串口连接成功：COM3（115200/8N1），来源程序=MyApp
2026-06-10 14:23:47.210 [调试] [trace:7F3A9C1D] 接口调用：SetLight(key=A1, color=Green, beep=null)
2026-06-10 14:23:47.230 [信息] [trace:7F3A9C1D] 无线任务已发布：topic=/estation/90A9F73014A5/task，items=1，time=120，tagId=AD1E000E912A，group=0，color=Green，flashing=False，beep=False
```

建议日常使用 `Info`，排障时临时切到 `Debug`。

---

## VB 推荐调用封装（可直接复制）

下面封装适合长期保持连接的场景：启动时连接、业务期复用、发送失败时统一降级。

```vb
Imports PTLControl.Compat
Imports PTLControl.Compat.Models

Public NotInheritable Class PtlClient
    Private Shared ReadOnly _sync As New Object()
    Private Shared _opened As Boolean = False

    Public Shared Function Connect() As Boolean
        SyncLock _sync
            If _opened AndAlso PTLController.IsConnected Then Return True
            Dim ok = PTLController.Connect()
            _opened = ok AndAlso PTLController.IsConnected
            Return _opened
        End SyncLock
    End Function

    Public Shared Sub Disconnect()
        SyncLock _sync
            Try
                PTLController.Disconnect()
            Finally
                _opened = False
            End Try
        End SyncLock
    End Sub

    Public Shared Sub SetLight(key As String, color As LedColor)
        SendCore(Function() PTLController.SetLight(key, color))
    End Sub

    Public Shared Sub SetBlink(key As String, color As LedColor, intervalMs As Integer)
        SendCore(Function() PTLController.SetBlink(key, color, intervalMs))
    End Sub

    Public Shared Sub TurnOff(key As String)
        SendCore(Function() PTLController.TurnOff(key))
    End Sub

    Public Shared Sub AllOff()
        SyncLock _sync
            Try
                If Not EnsureConnected() Then
                    Throw New InvalidOperationException("未连接，无法执行 AllOff。")
                End If
                PTLController.AllOff()
            Catch
                Try : PTLController.Disconnect() : Catch : End Try
                _opened = False
                Throw
            End Try
        End SyncLock
    End Sub

    Private Shared Function EnsureConnected() As Boolean
        If _opened AndAlso PTLController.IsConnected Then Return True
        Dim ok = PTLController.Connect()
        _opened = ok AndAlso PTLController.IsConnected
        Return _opened
    End Function

    Private Shared Sub SendCore(action As Func(Of Boolean))
        SyncLock _sync
            Try
                If Not EnsureConnected() Then
                    Throw New InvalidOperationException("连接失败，请检查配置与链路。")
                End If
                If Not action.Invoke() Then
                    Throw New InvalidOperationException("发送失败：Key/Alias 未映射，或 MQTT 未连接。")
                End If
            Catch
                Try : PTLController.Disconnect() : Catch : End Try
                _opened = False
                Throw
            End Try
        End SyncLock
    End Sub
End Class
```

推荐使用方式：

```vb
If Not PtlClient.Connect() Then
    MessageBox.Show("PTL 连接失败")
    Return
End If

PtlClient.SetLight("A1", LedColor.Green)
PtlClient.SetLight("A3", LedColor.Red)   ' 可同时点亮多颗

PtlClient.Disconnect()
```

注意：

- 不要在每次点灯后立刻 `Disconnect()`。
- 不要在每次点灯前后调用 `AllOff()`，否则会表现成“只能亮一个”。
- `IsConnected` 建议用于界面显示；业务发送以 `Connect()` 返回值和灯控接口 `Boolean` 为准。

---

## 颜色枚举

对外只使用 `LedColor` 枚举，库内部完成 RGB 映射，调用方无需传 R/G/B。

| 枚举 | RGB（内部） | 说明 |
|------|-------------|------|
| `LedColor.Red` | 255, 0, 0 | 错误 |
| `LedColor.Orange` | 255, 128, 0 | 次要提醒 |
| `LedColor.Yellow` | 255, 180, 0 | 待确认 |
| `LedColor.Green` | 0, 255, 0 | 取料 |
| `LedColor.Cyan` | 0, 255, 255 | 中性提示 |
| `LedColor.Blue` | 0, 0, 255 | 已借走 |
| `LedColor.Purple` | 128, 0, 255 | 特殊 |
| `LedColor.White` | 255, 255, 255 | 通用 |

```vb
PTLController.SetLight("001", LedColor.Red)
PTLController.SetLight("001", LedColor.Orange)
PTLController.SetLight("001", LedColor.Yellow)
PTLController.SetLight("001", LedColor.Green)
PTLController.SetLight("001", LedColor.Cyan)
PTLController.SetLight("001", LedColor.Blue)
PTLController.SetLight("001", LedColor.Purple)
PTLController.SetLight("001", LedColor.White)
```

---

## 几个常见问题

**运行报找不到 dll？**  
把三个 dll 都复制到 exe 旁边：`PTLControl.Compat.dll`、`Newtonsoft.Json.dll`、`MQTTnet.dll`。

**`GetPortNames()` 返回空？**  
设备未插或驱动未装，去设备管理器确认。

**`Connect()` 返回 False？**  
检查 `startup_config.json`：serial 看端口是否被占用；mqtt 看 Broker 地址、账号、`eStationId`。详情见 `%AppData%\PTLControl\logs\`。

**界面显示已连接但灯不亮？**  
必须同时满足 `Connect()` 返回 True 且 `IsConnected` 为 True。MQTT 还要确认 `mqtt_mapping.json` 中 `tagId` 已配置。

**按 Key 操作返回 False？**  
Key（或 Alias）不在映射表里。先用主程序“灯位映射”配好，或手工编辑 json。

**`SetLight` 时长和 `SetBlink` 不一样？**  
MQTT 模式下正常：`taskTimeSlot` 管常亮，`blinkTimeSlot` 管闪烁，单位均为 5 秒/挡。

**配置文件在哪？**  
`%AppData%\PTLControl\`：`serial_mapping.json`、`mqtt_mapping.json`、`startup_config.json`。

---

## 串口协议（参考）

发给 Arduino 的是纯文本：

```text
<1,3,0,255,0>   第1层第3颗，绿色
<2,0,255,0,0>   第2层第0颗，红色
<OFF>           全灭
```

波特率 115200，8N1。
