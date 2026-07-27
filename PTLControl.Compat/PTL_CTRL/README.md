# PTLControl DLL 快速使用

## 文件

`net472` 目录包含：

- `PTLControl.Compat.dll`
- `Newtonsoft.Json.dll`
- `MQTTnet.dll`

将它们复制到调用程序目录，并引用 `PTLControl.Compat.dll`。

## VB.NET 示例

```vb
Imports PTLControl.Compat
Imports PTLControl.Compat.Models

If PTLController.Connect() Then
    PTLController.SetLight("A1", LedColor.Green)
    PTLController.SetBlink("A2", LedColor.Red, 500)
    PTLController.TurnOff("A1")
    PTLController.AllOff()
    PTLController.Disconnect()
End If
```

连接方式由以下配置决定：

```text
%AppData%\PTLControl\startup_config.json
```

灯位映射：

```text
serial_mapping.json   串口灯位
mqtt_mapping.json     MQTT 无线灯条
```

完整示例见 `VBNet_Example\PTLLightDemo.vb`。
