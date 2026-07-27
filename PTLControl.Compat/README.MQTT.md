# PTLControl.Compat MQTT 版使用说明（仅无线灯条）

## 1. 依赖

部署时至少包含：

- `PTLControl.Compat.dll`
- `Newtonsoft.Json.dll`
- `MQTTnet.dll`

---

## 2. 配置文件

配置目录：`%AppData%\PTLControl\`

需要两个核心文件：

- `startup_config.json`
- `mqtt_mapping.json`

`startup_config.json` 示例：

```json
{
  "connectionMode": "mqtt",
  "logLevel": "Info",
  "mqtt": {
    "broker": "192.168.172.172",
    "port": 2026,
    "username": "idemia",
    "password": "123456",
    "eStationId": "90A9F73014A5",
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

### `wirelessDefaults` 字段

| 字段 | 作用 |
|------|------|
| `taskTimeSlot` | `SetLight` 下发任务的 TimeSlot（1 挡 = 5 秒） |
| `blinkTimeSlot` | `SetBlink` 下发任务的 TimeSlot（1 挡 = 5 秒） |
| `beepDefault` | 灯控未传 `beep` 参数时的默认蜂鸣开关 |
| `heartbeatTimeoutSec` | 无心跳超过此秒数后 `IsConnected` 变为 False |

例：`taskTimeSlot=24` → 常亮约 120 秒；`blinkTimeSlot=5` → 闪烁约 25 秒。

`mqtt_mapping.json` 示例：

```json
{
  "nodes": [
    { "key": "001", "tagId": "AD1E000E912A", "group": 0, "alias": "" },
    { "key": "002", "tagId": "AD1E000E93A9", "group": 0, "alias": "" }
  ]
}
```

说明：

- `key`：业务键（第三方传入）
- `tagId`：灯条 ID（必填；为空则拒绝发送）
- `group`：分组号（随节点保留；反查 key 时可参与匹配）
- `alias`：可选别名，与 key 等价用于查找

---

## 3. 调用示例（VB.NET）

> 无线模式对外灯控统一按 `key` 调用，颜色只传 `LedColor` 枚举。

```vb
Imports PTLControl.Compat
Imports PTLControl.Compat.Models

' 连接（返回 Boolean，失败不抛异常）
Dim ok As Boolean = PTLController.Connect()
If Not ok OrElse Not PTLController.IsConnected Then
    Console.WriteLine("MQTT 连接失败")
    Return
End If

' 常亮（时长由 taskTimeSlot 决定）
PTLController.SetLight("001", LedColor.Green)

' 闪烁（时长由 blinkTimeSlot 决定；intervalMs 在 MQTT 下不直接下发）
PTLController.SetBlink("001", LedColor.Red, 500)

' 熄灭
PTLController.TurnOff("001")

' 全灭（批量 task，MQTT 未连接时跳过并记 Warn）
PTLController.AllOff()

' 断开
PTLController.Disconnect()
```

`beep` 可选参数：

```vb
PTLController.SetLight("001", LedColor.Green, True)
PTLController.SetBlink("001", LedColor.Red, 500, False)

PTLController.BeepOn("001")
PTLController.BeepBlink("001", 500)
PTLController.BeepOff("001")
```

8 色枚举：

```vb
LedColor.Red / Orange / Yellow / Green / Cyan / Blue / Purple / White
```

灯控接口（`SetLight` / `SetBlink` / `TurnOff` / `Beep*`）在 key 未映射或 MQTT 未连接时返回 `False`。

---

## 4. 回传事件（双向）

```vb
AddHandler PTLController.ConnectionChanged, Sub(src, e)
    Console.WriteLine($"MQTT connected={e.IsConnected}")
End Sub

AddHandler PTLController.TagEventReceived, Sub(src, ev)
    Dim key As String = PTLController.GetKeyByTagId(ev.TagId, ev.Group)
    Console.WriteLine($"type={ev.EventType}, tagId={ev.TagId}, group={ev.Group}, key={key}")
End Sub

Dim state = PTLController.GetNodeState("001")
```

---

## 5. 发送队列与软失败

MQTT 底层已实现：

- 所有任务入发布队列，单线程消费
- 相邻发布间隔 ≥ 20ms
- 队列上限 2000，超出丢弃最早任务并记 Warn
- 未连接时 `SetLight/SetBlink/TurnOff` 返回 `False`，`AllOff` 跳过
- `Connect()` 失败返回 `False`，不抛异常到调用方

---

## 6. 日志与排障

日志目录：`%AppData%\PTLControl\logs\`

- `Info`：连接、订阅、发送结果、告警/异常
- `Debug`：额外记录完整接口调用参数（含 `traceId`）

```text
[trace:7F3A9C1D] 接口调用：SetBlink(key=001, color=Green, intervalMs=500, beep=True)
[trace:7F3A9C1D] 无线任务已发布：topic=/estation/90A9F73014A5/task，items=1，time=25，tagId=AD1E000E912A，group=0，color=Green，flashing=True，beep=True
```

---

## 7. 注意事项

- 不要在失败补偿里循环调用 `AllOff()`，否则会放大队列压力。
- `tagId` 为空的节点不会下发，日志会提示相关 Warn。
- 链路异常时建议“失败后断开 → 重连 → 单次重试”，避免重试风暴。
- `Marquee` 在 MQTT 模式不支持，调用会被忽略。
- 完整 API 与 Serial 对照见 `README.md`。
