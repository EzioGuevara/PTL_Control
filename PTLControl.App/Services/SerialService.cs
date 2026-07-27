using System;
using CompatSerialService = PTLControl.Compat.Services.SerialService;

namespace PTLControl.Services;

/// <summary>
/// 单例串口服务，供所有窗口共享。
/// 线程安全写入（lock）。
/// </summary>
public sealed class SerialService : IDisposable
{
    public static readonly SerialService Instance = new();

    private SerialService() { }

    public bool IsOpen => CompatSerialService.Instance.IsOpen;

    public string[] GetPortNames() => CompatSerialService.Instance.GetPortNames();

    /// <summary>打开串口，115200/8N1/WriteTimeout=500ms</summary>
    public void Open(string portName)
        => CompatSerialService.Instance.Open(portName);

    /// <summary>关闭并释放串口</summary>
    public void Close() => CompatSerialService.Instance.Close();

    /// <summary>发送指令字符串（线程安全）</summary>
    public void Send(string cmd) => CompatSerialService.Instance.Send(cmd);

    public void Dispose() => Close();
}
