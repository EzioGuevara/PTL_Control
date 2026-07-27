# PTL Control

当前正式版本：`v1.0.0`

用于控制 PTL 灯带的 .NET 项目，支持两种连接方式：

- Serial：通过串口连接 MCU，控制有线灯带。
- MQTT：通过 MQTT Broker 连接 eStation，控制无线灯条。

连接方式由 `%AppData%\PTLControl\startup_config.json` 的 `connectionMode` 决定，上层统一调用 `PTLController.Connect()`。

## 快速开始

```csharp
using PTLControl.Compat;
using PTLControl.Compat.Models;

if (!PTLController.Connect())
    return;

PTLController.SetLight("A1", LedColor.Green);
PTLController.SetBlink("A2", LedColor.Red, 500);
PTLController.TurnOff("A1");
PTLController.AllOff();
PTLController.Disconnect();
```

## 配置

```json
{
  "connectionMode": "serial",
  "serial": {
    "portName": "COM3"
  },
  "mqtt": {
    "broker": "127.0.0.1",
    "port": 2026,
    "username": "",
    "password": "",
    "eStationId": "90A9F7300000"
  }
}
```

配置和灯位映射保存在：

```text
%AppData%\PTLControl\
```

## 构建

```powershell
dotnet build PTLControl.sln -c Release
```

面向 .NET Framework 4.7.2 的交付文件位于：

```text
PTLControl.Compat\PTL_CTRL\net472\
```

## 项目结构

- `PTLControl.App`：WinForms 配置和测试程序。
- `PTLControl.Compat`：供第三方调用的兼容 DLL。
- `PTLControl.Demo`：API 调用示例。
- `LedArdunio`：MCU 灯带控制固件。

更详细的 MQTT 说明见 [PTLControl.Compat/README.MQTT.md](PTLControl.Compat/README.MQTT.md)。
