# PTLControl.Compat

正式版本：`v1.0.0`

供 .NET Framework 4.7.2、VB.NET 和现代 .NET 项目调用的 PTL 灯控库。

## 引用文件

从 `PTL_CTRL\net472\` 复制：

- `PTLControl.Compat.dll`
- `PTLControl.HardwareHost.exe`
- `Newtonsoft.Json.dll`
- `MQTTnet.dll`

`PTLControl.Compat.dll` 不再引用 `System.IO.Ports`，也不会直接枚举、打开或写入串口。
Serial 模式下它会按需启动同目录的硬件宿主，并通过本机命名管道发送请求。请勿遗漏宿主程序。
宿主带有独立状态窗口，关闭窗口只会缩小到系统托盘；需从托盘菜单明确选择退出。

## 基本用法

```vb
Imports PTLControl.Compat
Imports PTLControl.Compat.Models

If Not PTLController.Connect() Then
    Console.WriteLine(PTLController.LastConnectionMessage)
    Return
End If

Console.WriteLine(PTLController.LastConnectionMessage)

PTLController.SetLight("A1", LedColor.Green)
PTLController.SetBlink("A2", LedColor.Red, 500)
PTLController.TurnOff("A1")
PTLController.AllOff()
PTLController.Disconnect()
```

`Connect()` 会读取 `%AppData%\PTLControl\startup_config.json`，自动选择 Serial 或 MQTT。

多个业务进程同时调用时，共用宿主中的一个物理串口连接和一个限速发送队列。旧版
`Connect("COM3")` 参数会被忽略，物理端口只认 `startup_config.json`；每个进程的
`Connect()` / `Disconnect()` 只维护自己的兼容逻辑状态，不会打开或关闭物理串口。

Serial 模式的 `Connect()` 会询问 Host 当前状态：只有 Host 可达且物理串口已打开才返回
`True`。详细结果可通过 `PTLController.LastConnectionMessage` 读取，同时也会放入
`ConnectionChanged` 事件的 `Message`。旧的 `Connect(String)` 是 `Sub`，可在调用后读取该消息。

串口模式下 `SetBlink` 和 `Marquee` 只向宿主提交一次动作，后续时序由宿主统一调度；
宿主按灯位合并状态，并负责 PING、断线重连、队列上限和运行指标。Compat 与 Host 必须成套发布，
IPC 协议版本不一致时会明确拒绝连接。

## 常用接口

```text
Connect()                       连接
Disconnect()                    断开
IsConnected                     查询状态
LastConnectionMessage           Host 实际连接状态/原因
SetLight(key, color)            常亮
SetBlink(key, color, interval)  闪烁
TurnOff(key)                    关闭单灯
AllOff()                        全部关闭
GetAllKeys()                    获取全部 Key
```

旧版 `Connect("COM3")`、RGB 和 `LightBy*` 接口继续保留，用于二进制兼容；新代码建议使用上面的统一接口。

## 配置文件

```text
%AppData%\PTLControl\startup_config.json
%AppData%\PTLControl\serial_mapping.json
%AppData%\PTLControl\mqtt_mapping.json
```

MQTT 的 Topic、心跳、蜂鸣和回传事件说明见 [README.MQTT.md](README.MQTT.md)。
