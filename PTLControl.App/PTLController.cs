// ============================================================
// PTL LED Matrix Control System
// Developer: Ezio Li @ IDEMIA
// Description: Public API for LED light control via serial port.
//              Supports multi-light independent control, blink,
//              marquee, and color presets (LedColor enum).
// ============================================================
using System.Collections.Generic;
using PTLControl.Models;
using CompatController = PTLControl.Compat.PTLController;
using CompatConnectionChangedEventArgs = PTLControl.Compat.Models.ConnectionChangedEventArgs;
using CompatLedColor = PTLControl.Compat.Models.LedColor;
using CompatTagEventArgs = PTLControl.Compat.Models.TagEventArgs;
using CompatSerialLineReceivedEventArgs = PTLControl.Compat.Models.SerialLineReceivedEventArgs;

namespace PTLControl;

/// <summary>
/// PTL 灯控对外接口 — 多灯独立控制。
/// 每颗灯可独立设置常亮、闪烁、熄灭，互不干扰。
/// </summary>
public static class PTLController
{
    public static event System.EventHandler<CompatConnectionChangedEventArgs> ConnectionChanged
    {
        add { CompatController.ConnectionChanged += value; }
        remove { CompatController.ConnectionChanged -= value; }
    }

    public static event System.EventHandler<CompatTagEventArgs> TagEventReceived
    {
        add { CompatController.TagEventReceived += value; }
        remove { CompatController.TagEventReceived -= value; }
    }

    public static event System.EventHandler<CompatSerialLineReceivedEventArgs> SerialLineReceived
    {
        add { CompatController.SerialLineReceived += value; }
        remove { CompatController.SerialLineReceived -= value; }
    }

    // ── 串口管理 ──────────────────────────────────────────────────────────────

    public static string[] GetPortNames() => CompatController.GetPortNames();
    public static bool Connect() => CompatController.Connect();
    public static void Connect(string portName) => CompatController.Connect(portName);
    public static void Disconnect() => CompatController.Disconnect();
    public static bool IsConnected => CompatController.IsConnected;
    public static string LastConnectionMessage => CompatController.LastConnectionMessage;

    // ── 核心灯控接口（枚举颜色版，推荐调用方使用）─────────────────────────

    /// <summary>常亮某颗灯（按 Key，返回 false 表示未找到映射）</summary>
    public static bool SetLight(string key, LedColor color)
        => SetLight(key, color, null);

    public static bool SetLight(string key, LedColor color, bool? beep)
        => CompatController.SetLight(key, ToCompatColor(color), beep);

    /// <summary>常亮某颗灯（按 Layer/Index）</summary>
    public static void SetLight(int layer, int index, LedColor color)
        => SetLight(layer, index, color, null);

    public static void SetLight(int layer, int index, LedColor color, bool? beep)
        => CompatController.SetLight(layer, index, ToCompatColor(color), beep);

    /// <summary>闪烁某颗灯</summary>
    public static bool SetBlink(string key, LedColor color, int intervalMs = 500)
        => SetBlink(key, color, intervalMs, null);

    public static bool SetBlink(string key, LedColor color, int intervalMs, bool? beep)
        => CompatController.SetBlink(key, ToCompatColor(color), intervalMs, beep);

    /// <summary>闪烁某颗灯（按 Layer/Index）</summary>
    public static void SetBlink(int layer, int index, LedColor color, int intervalMs = 500)
        => SetBlink(layer, index, color, intervalMs, null);

    public static void SetBlink(int layer, int index, LedColor color, int intervalMs, bool? beep)
        => CompatController.SetBlink(layer, index, ToCompatColor(color), intervalMs, beep);

    /// <summary>跑马灯</summary>
    public static void Marquee(LedColor color, int intervalMs = 100)
    {
        CompatController.Marquee(ToCompatColor(color), intervalMs);
    }

    /// <summary>根据 Key 熄灭（不影响其他灯）</summary>
    public static bool TurnOff(string key)
        => CompatController.TurnOff(key);

    /// <summary>根据 Layer/Index 熄灭（不影响其他灯）</summary>
    public static void TurnOff(int layer, int index)
        => CompatController.TurnOff(layer, index);

    /// <summary>全部熄灭（停止所有闪烁 + 跑马灯 + 发送 OFF）</summary>
    public static void AllOff()
        => CompatController.AllOff();

    public static bool BeepOn(string key)
        => CompatController.BeepOn(key);

    public static bool BeepBlink(string key, int intervalMs = 500)
        => CompatController.BeepBlink(key, intervalMs);

    public static bool BeepOff(string key)
        => CompatController.BeepOff(key);

    // ── 配置 & UI ─────────────────────────────────────────────────────────────

    public static IReadOnlyList<string> GetAllKeys()
        => new List<string>(CompatController.GetAllKeys());

    public static string GetKeyByTagId(string tagId, int? group = null)
        => CompatController.GetKeyByTagId(tagId, group);

    public static void ShowMappingForm()
    {
        var startup = Services.ConfigService.LoadStartup();
        if (string.Equals(startup.ConnectionMode, "mqtt", System.StringComparison.OrdinalIgnoreCase))
            new MqttMappingForm().ShowDialog();
        else
            new MappingForm().ShowDialog();
    }

    public static void ShowMatrixTestForm()
    {
        var startup = Services.ConfigService.LoadStartup();
        if (string.Equals(startup.ConnectionMode, "mqtt", System.StringComparison.OrdinalIgnoreCase))
            new MqttTestForm().ShowDialog();
        else
            new MatrixTestForm().ShowDialog();
    }

    // ── 内部工具 ──────────────────────────────────────────────────────────────

    private static CompatLedColor ToCompatColor(LedColor color) => color switch
    {
        LedColor.Red => CompatLedColor.Red,
        LedColor.Orange => CompatLedColor.Orange,
        LedColor.Yellow => CompatLedColor.Yellow,
        LedColor.Green => CompatLedColor.Green,
        LedColor.Cyan => CompatLedColor.Cyan,
        LedColor.Blue => CompatLedColor.Blue,
        LedColor.Purple => CompatLedColor.Purple,
        LedColor.White => CompatLedColor.White,
        _ => CompatLedColor.Green
    };
}
