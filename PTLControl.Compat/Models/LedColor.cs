// ============================================================
// PTL LED Matrix Control System - .NET Standard 2.0 Compat
// Developer: Ezio Li @ IDEMIA
// Description: Predefined LED color enum and RGB mapping.
// ============================================================
namespace PTLControl.Compat.Models
{
    /// <summary>预定义灯光颜色</summary>
    public enum LedColor
    {
        /// <summary>红色 — 错误警告</summary>
        Red = 0,
        /// <summary>橙色 — 次要提醒</summary>
        Orange = 1,
        /// <summary>黄色 — 待确认</summary>
        Yellow = 2,
        /// <summary>绿色 — 指示取料</summary>
        Green = 3,
        /// <summary>青色 — 中性提示</summary>
        Cyan = 4,
        /// <summary>蓝色 — 已借走</summary>
        Blue = 5,
        /// <summary>紫色 — 特殊标记</summary>
        Purple = 6,
        /// <summary>白色 — 通用</summary>
        White = 7
    }

    /// <summary>颜色到 RGB 的映射</summary>
    public static class LedColorMap
    {
        /// <summary>将 LedColor 枚举转换为 RGB 元组</summary>
        public static void ToRgb(this LedColor color, out int r, out int g, out int b)
        {
            switch (color)
            {
                case LedColor.Red:
                    r = 255; g = 0; b = 0; break;
                case LedColor.Orange:
                    r = 255; g = 128; b = 0; break;
                case LedColor.Yellow:
                    r = 255; g = 180; b = 0; break;
                case LedColor.Green:
                    r = 0; g = 255; b = 0; break;
                case LedColor.Cyan:
                    r = 0; g = 255; b = 255; break;
                case LedColor.Blue:
                    r = 0; g = 0; b = 255; break;
                case LedColor.Purple:
                    r = 128; g = 0; b = 255; break;
                case LedColor.White:
                    r = 255; g = 255; b = 255; break;
                default:
                    r = 0; g = 255; b = 0; break;
            }
        }
    }
}
