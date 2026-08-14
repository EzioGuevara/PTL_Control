# PTLControl DLL 快速使用

## 文件

`net472` 目录包含：

- `PTLControl.Compat.dll`
- `PTLControl.HardwareHost.exe`（Serial 模式必需，必须与 DLL 同目录）
- `Newtonsoft.Json.dll`
- `MQTTnet.dll`
- `PTLControl.HardwareHost.exe.config`

将这 5 个文件全部复制到调用程序的输出目录，但项目只需添加
`PTLControl.Compat.dll` 引用。兼容 DLL 不直接访问串口，
它会按需启动硬件宿主并通过本机命名管道发送请求。
调用项目的目标框架应为 `.NET Framework 4.7.2` 或更高版本。

旧接口 `Connect("COM3")` 仍可调用，但传入端口不会生效；HardwareHost 只读取
`%AppData%\PTLControl\startup_config.json` 中的 `serial.portName`。调用方执行
`Disconnect()` 不会关闭宿主持有的物理串口。

## VB.NET 示例

```vb
Imports PTLControl.Compat
Imports PTLControl.Compat.Models

If Not PTLController.Connect() Then
    MessageBox.Show(
        "PTL连接失败，请检查HardwareHost是否启动。" & vbCrLf &
        PTLController.LastConnectionMessage)
    Return
End If

PTLController.SetLight("A1", LedColor.Green)
PTLController.SetBlink("A2", LedColor.Red, 500)
PTLController.TurnOff("A1")
PTLController.AllOff()
```

`Connect()` 只在 HardwareHost 可达且配置的物理串口已连接时返回
`True`。无论成功或失败，都可读取 `PTLController.LastConnectionMessage` 获取实际端口、
Host 健康状态和失败原因。

连接方式由以下配置决定：

```text
%AppData%\PTLControl\startup_config.json
```

灯位映射：

```text
serial_mapping.json   串口灯位
mqtt_mapping.json     MQTT 无线灯条
```

首次部署时，将交付包根目录中的 `startup_config.json`、
`serial_mapping.json` 和 `mqtt_mapping.json` 复制到上述目录。
之后的串口和灯位映射由配置文件管理，VB 调用方不需要传入 COM 口。

完整示例见 `VBNet_Example\PTLLightDemo.vb`。
