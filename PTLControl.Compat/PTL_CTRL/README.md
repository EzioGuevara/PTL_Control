# PTLControl.Compat — 给 VB.NET / .NET Framework 4.7.2 开发者的集成说明

主程序 `PTLControl.App` 跑在 .NET 10 上，4.7.2 的项目没法直接引用它。这个库（`PTLControl.Compat`）用 .NET Standard 2.0 重新封装了同一套 API，4.7.2 和 .NET 10 都能引用 Standard 2.0，所以直接用这个就行。

- `PTLControl.Compat.dll` — 主库
- `Newtonsoft.Json.dll` — 读配置文件用的
- `MQTTnet.dll` — MQTT 模式运行依赖

> `System.IO.Ports` 在 .NET Framework 里是内置的，不需要额外的 dll。

---

## 在 VB 项目里加引用

把上面三个 dll 放到你的 VB 项目里（比如建个 `Lib\` 文件夹），然后 Visual Studio 里右键项目 → **添加引用** → **浏览** → 三个都选上，确定。

---

## 怎么用

文件顶部加两行 Imports：

```vb
Imports PTLControl.Compat
Imports PTLControl.Compat.Models
```

然后就可以直接调了：

```vb
' 连接串口
PTLController.Connect("COM3")

' 绿色常亮（按 Key）
PTLController.SetLight("A1", LedColor.Green)

' 红色闪烁，500ms 间隔（按 Key）
PTLController.SetBlink("A1", LedColor.Red, 500)

' 关掉某一颗（按 Key）
PTLController.TurnOff("A1")

' 全灭
PTLController.AllOff()

' 断开
PTLController.Disconnect()
```

完整示例看 `VBNet_Example\PTLLightDemo.vb`。

---

## 所有 API

### 串口

```vb
PTLController.GetPortNames()          ' 返回 String()，列出可用串口
PTLController.Connect("COM3")         ' 连接
PTLController.Disconnect()            ' 断开
PTLController.IsConnected             ' Boolean，串口句柄是否已打开（非设备在线探测）
```

### 点灯（常亮）

```vb
PTLController.SetLight("A1", LedColor.Green)
PTLController.SetLight("A1", LedColor.Green, True) ' 可选 beep（仅无线模式有效）
```

### 闪烁

```vb
PTLController.SetBlink("A1", LedColor.Red, 500)   ' 500ms 间隔
PTLController.SetBlink("A1", LedColor.Yellow, 400, False) ' 可选 beep
```

### 熄灭

```vb
PTLController.TurnOff("A1")           ' 关单颗，不影响其他灯
PTLController.AllOff()                ' 全灭，同时停闪烁和跑马灯
```

### 跑马灯

```vb
PTLController.Marquee(LedColor.Blue, 80)        ' 80ms 一跳
' 停止跑马灯：调 AllOff()，或调用 SetLight/SetBlink/TurnOff 等普通灯控接口
```

### Key 相关

```vb
' 获取配置里所有 Key（需要先用主程序配好灯位映射）
Dim keys As IList(Of String) = PTLController.GetAllKeys()
```

> `GetAllKeys()` 返回的是配置文件中的真实 Key/Alias（例如 `A1`、`B12`、`SKU-001`），不会自动加 `KEY-` 前缀。

---

## 与 PTLControl.App 主程序的差异

- `PTLControl.App`（.NET 10 WinForms）带 UI：串口连接、扫码发送、映射管理、矩阵测试。
- `PTLControl.Compat` 是纯 API 库，不包含上述窗体功能。
- 两者共用同一份串口映射配置：`%AppData%\\PTLControl\\serial_mapping.json`。
- `IsConnected` 与主程序一致，表示“串口是否打开”，不是“Arduino 实时在线心跳状态”。
- 主程序常用业务动作在 Compat 可直接等价调用：
  - 查询（绿色常亮）≈ `SetLight(key, LedColor.Green)`
  - 查询（红色闪烁）≈ `SetBlink(key, LedColor.Red, 500)`
  - 查询（绿色闪烁）≈ `SetBlink(key, LedColor.Green, 500)`

---

## 日志配置（新增）

库已内置中文文件日志，默认从 `%AppData%\\PTLControl\\startup_config.json` 读取 `logLevel`。

配置示例（`startup_config.json` 根对象）：

```json
{
  "logLevel": "Info",
  "connectionMode": "serial"
}
```

可选值：

- `Off`：关闭日志
- `Info`：日常运行日志（默认）。记录连接/断开、异常与告警。
- `Debug`：接口调用日志。在 `Info` 基础上记录 API 名称与传入参数（例如 `接口调用：SetLight(key=A1, color=Green, beep=null)`）。

日志文件位置：

- 固定根目录：`%AppData%\\PTLControl\\`（例如 `C:\\Users\\Ezio\\AppData\\Roaming\\PTLControl\\`）
- 日志目录：`%AppData%\\PTLControl\\logs\`
- 文件：`ptl-YYYY-MM-DD.log`（按天滚动）
- 格式：`2026-04-10 14:23:45.123 [信息] 串口连接成功：COM3（115200/8N1）`

建议日常使用 `Info`，排障时临时切到 `Debug`。

---

## VB 推荐调用封装（可直接复制）

下面这个封装适合你的 Arduino 场景：不依赖实时心跳，不在每次发送前做强校验；发送失败时统一降级并要求重连。

```vb
Imports PTLControl.Compat
Imports PTLControl.Compat.Models

Public NotInheritable Class PtlClient
    Private Shared ReadOnly _sync As New Object()
    Private Shared _portName As String
    Private Shared _opened As Boolean = False

    Public Shared Sub Initialize(portName As String)
        _portName = portName
    End Sub

    Public Shared Sub Connect()
        SyncLock _sync
            If _opened Then Return
            PTLController.Connect(_portName)
            _opened = True
        End SyncLock
    End Sub

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
                If Not _opened Then
                    PTLController.Connect(_portName)
                    _opened = True
                End If
                PTLController.AllOff()
            Catch
                Try : PTLController.Disconnect() : Catch : End Try
                _opened = False
                Throw
            End Try
        End SyncLock
    End Sub

    Private Shared Sub SendCore(action As Func(Of Boolean))
        SyncLock _sync
            Try
                If Not _opened Then
                    PTLController.Connect(_portName)
                    _opened = True
                End If

                If Not action.Invoke() Then
                    Throw New InvalidOperationException("发送失败：Key/Alias 未在映射中配置。")
                End If
            Catch
                ' 发送失败即认为链路失效，统一降级
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
' 程序启动时
PtlClient.Initialize("COM3")
PtlClient.Connect()

' 业务调用
PtlClient.SetLight("A1", LedColor.Green)
PtlClient.SetLight("A3", LedColor.Red)  ' 可同时点亮多颗（不自动全灭）

' 程序退出时
PtlClient.Disconnect()
```

注意：

- 不要在每次点灯后立刻 `Disconnect()`，否则下一次很容易触发“未连接”。
- 不要在每次点灯前后调用 `AllOff()`，否则会表现成“只能亮一个”。
- `IsConnected` 只代表串口句柄状态，建议仅用于界面显示，不用于业务强拦截。

---

## 无线双向模式（新增）

`PTLControl.Compat` 现在拆分为 3 个配置文件（都在 `%AppData%\\PTLControl\\`）：

- `serial_mapping.json`：**仅串口映射表**（保持原结构，Key/Alias -> Layer/Index）。
- `mqtt_mapping.json`：无线节点映射（`key/tagId/group/alias`）。
- `startup_config.json`：启动连接模式与连接参数（`serial` 或 `mqtt`）。

`startup_config.json` 示例：

```json
{
  "connectionMode": "mqtt",
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
    "taskTimeSlot": 5,
    "blinkTimeSlot": 5,
    "beepDefault": false,
    "heartbeatTimeoutSec": 90
  }
}
```

`mqtt_mapping.json` 示例：

```json
{
  "nodes": [
    { "key": "A1", "tagId": "AD100000048F", "group": 1, "alias": "" }
  ]
}
```

兼容说明：

- 旧调用方接口保持不变，仍使用 `SetLight/SetBlink/TurnOff/AllOff`。
- 旧的 `ptl_config.json` 会在首次读取时自动迁移到 `serial_mapping.json`。
- MQTT 模式下若目标点位找不到 `tagId`，会拒绝发送并记录告警。
- `SetBlink(intervalMs)` 在无线模式按 `Flashing + TimeSlot` 近似映射。
- `Marquee(...)` 在无线模式不支持，会被忽略并记录告警。

双向事件 API：

- `PTLController.ConnectionChanged`：连接状态变化（串口或 MQTT）。
- `PTLController.TagEventReceived`：接收灯条按键/通信/心跳事件（`0xFD/0xFE/0xFF`）。
- `PTLController.GetNodeState(keyOrTagId)`：获取节点最近一次上报状态缓存。

---

## 颜色枚举

| 枚举 | RGB | 说明 |
|------|-----|------|
| `LedColor.Red` | 255, 0, 0 | 错误 |
| `LedColor.Orange` | 255, 128, 0 | 次要提醒 |
| `LedColor.Yellow` | 255, 180, 0 | 待确认 |
| `LedColor.Green` | 0, 255, 0 | 取料 |
| `LedColor.Cyan` | 0, 255, 255 | 中性提示 |
| `LedColor.Blue` | 0, 0, 255 | 已借走 |
| `LedColor.Purple` | 128, 0, 255 | 特殊 |
| `LedColor.White` | 255, 255, 255 | 通用 |

VB 调用示例（8种枚举全部展示）：

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
把三个 dll 都复制到 exe 旁边（输出目录）：`PTLControl.Compat.dll`、`Newtonsoft.Json.dll`、`MQTTnet.dll`。少一个都会报错。

**`GetPortNames()` 返回空？**  
Arduino 没插，或者驱动没装，去设备管理器看一眼。

**`SetLight` 报"串口未连接"？**  
先 `Connect()` 成功再调灯控接口。

**按 Key 操作返回 False？**  
这个 Key（或 Alias）在配置文件里没有。先用主程序的"灯位映射"把业务 Key 配到物理点位。

**配置文件在哪？**  
都在 `%AppData%\\PTLControl\\`：`serial_mapping.json`、`mqtt_mapping.json`、`startup_config.json`。

---

## 串口协议（参考）

发给 Arduino 的是纯文本：

```
<1,3,0,255,0>   第1层第3颗，绿色
<2,0,255,0,0>   第2层第0颗，红色
<OFF>           全灭
```

波特率 115200，8N1。
