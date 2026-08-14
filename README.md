# PTL Control

当前正式版本：`v1.0.0`

用于控制 PTL 灯带的 .NET 项目，支持两种连接方式：

- Serial：由独立的 `PTLControl.HardwareHost.exe` 独占串口并控制有线灯带；调用方 DLL 不接触 COM 口。
- MQTT：通过 MQTT Broker 连接 eStation，控制无线灯条。

连接方式由 `%AppData%\PTLControl\startup_config.json` 的 `connectionMode` 决定，上层统一调用 `PTLController.Connect()`。

## 快速开始

```csharp
using PTLControl.Compat;
using PTLControl.Compat.Models;

if (!PTLController.Connect())
{
    Console.WriteLine(PTLController.LastConnectionMessage);
    return;
}

Console.WriteLine(PTLController.LastConnectionMessage);

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

Serial 模式必须同时部署 `PTLControl.Compat.dll` 和 `PTLControl.HardwareHost.exe`。DLL 会按需启动宿主，
所有调用进程通过本机命名管道共享宿主中的唯一串口连接。`Connect(portName)` 的参数仅为旧接口兼容，
实际端口只由 `%AppData%\PTLControl\startup_config.json` 的 `serial.portName` 决定。调用方的
`Disconnect()` 或进程退出不会关闭物理串口；物理连接的完整生命周期由宿主控制。
`Connect()` 只在 Host 可达且配置的物理串口已连接时返回 `true`；
`PTLController.LastConnectionMessage` 会返回 Host 的实际端口、健康状态或失败原因。

## 项目结构

- `PTLControl.App`：WinForms 配置和测试程序。
- `PTLControl.Compat`：供第三方调用的兼容 DLL。
- `PTLControl.HardwareHost`：唯一允许直接枚举、打开、读写串口的硬件宿主。
  启动后显示状态窗口并常驻系统托盘，可查看配置端口、物理连接、调用方数量和最近错误。
  串口模式的闪烁、跑马灯、灯位状态合并、PING 和自动重连均在宿主内部执行，Compat 不再创建串口闪烁循环。
- `PTLControl.Demo`：API 调用示例。
- `LedArdunio`：MCU 灯带控制固件。

更详细的 MQTT 说明见 [PTLControl.Compat/README.MQTT.md](PTLControl.Compat/README.MQTT.md)。
