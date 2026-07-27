# PTLControl.Compat

正式版本：`v1.0.0`

供 .NET Framework 4.7.2、VB.NET 和现代 .NET 项目调用的 PTL 灯控库。

## 引用文件

从 `PTL_CTRL\net472\` 复制：

- `PTLControl.Compat.dll`
- `Newtonsoft.Json.dll`
- `MQTTnet.dll`

## 基本用法

```vb
Imports PTLControl.Compat
Imports PTLControl.Compat.Models

If Not PTLController.Connect() Then
    Console.WriteLine("连接失败")
    Return
End If

PTLController.SetLight("A1", LedColor.Green)
PTLController.SetBlink("A2", LedColor.Red, 500)
PTLController.TurnOff("A1")
PTLController.AllOff()
PTLController.Disconnect()
```

`Connect()` 会读取 `%AppData%\PTLControl\startup_config.json`，自动选择 Serial 或 MQTT。

## 常用接口

```text
Connect()                       连接
Disconnect()                    断开
IsConnected                     查询状态
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
