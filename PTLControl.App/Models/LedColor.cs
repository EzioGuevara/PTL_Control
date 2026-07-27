// ============================================================
// PTL LED Matrix Control System
// Developer: Ezio Li @ IDEMIA
// Description: Predefined LED color enum and RGB mapping.
// ============================================================
namespace PTLControl.Models;

/// <summary>预定义灯光颜色</summary>
public enum LedColor
{
    /// <summary>红色 — 错误警告</summary>
    Red,
    /// <summary>橙色 — 次要提醒</summary>
    Orange,
    /// <summary>黄色 — 待确认</summary>
    Yellow,
    /// <summary>绿色 — 指示取料</summary>
    Green,
    /// <summary>青色 — 中性提示</summary>
    Cyan,
    /// <summary>蓝色 — 已借走</summary>
    Blue,
    /// <summary>紫色 — 特殊标记</summary>
    Purple,
    /// <summary>白色 — 通用</summary>
    White,
}

/// <summary>颜色到 RGB 的映射</summary>
public static class LedColorMap
{
    public static (int R, int G, int B) ToRgb(this LedColor color) => color switch
    {
        LedColor.Red    => (255, 0, 0),
        LedColor.Orange => (255, 128, 0),
        LedColor.Yellow => (255, 180, 0),
        LedColor.Green  => (0, 255, 0),
        LedColor.Cyan   => (0, 255, 255),
        LedColor.Blue   => (0, 0, 255),
        LedColor.Purple => (128, 0, 255),
        LedColor.White  => (255, 255, 255),
        _ => (0, 255, 0),
    };
}
