// ============================================================
// PTL LED Matrix Control System - .NET Standard 2.0 Compat
// Developer: Ezio @ IDEMIA
// Description: Serial command formatting and parsing.
//              Protocol: <Layer,Index,R,G,B> / <OFF>
// ============================================================
namespace PTLControl.Compat.Services
{
    /// <summary>PTL 指令格式化与解析（协议：&lt;Layer,Index,R,G,B&gt;）</summary>
    public static class CommandService
    {
        private static int _defaultR = 0;
        private static int _defaultG = 255;
        private static int _defaultB = 0;

        /// <summary>格式化点亮指令（自定义颜色）</summary>
        public static string FormatCommand(int layer, int index, int r, int g, int b)
            => string.Format("<{0},{1},{2},{3},{4}>", layer, index, r, g, b);

        /// <summary>格式化点亮指令（使用默认绿色）</summary>
        public static string FormatCommand(int layer, int index)
            => FormatCommand(layer, index, _defaultR, _defaultG, _defaultB);

        /// <summary>全灭指令</summary>
        public const string OffCommand = "<OFF>";
    }
}
