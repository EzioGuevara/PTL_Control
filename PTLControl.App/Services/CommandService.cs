// ============================================================
// PTL LED Matrix Control System
// Developer: Ezio Li @ IDEMIA
// Description: Serial command formatting and parsing.
//              Protocol: <Layer,Index,R,G,B> / <OFF>
// ============================================================
namespace PTLControl.Services;

/// <summary>PTL 指令格式化与解析（协议：&lt;Layer,Index,R,G,B&gt;）</summary>
public static class CommandService
{
    /// <summary>默认点亮颜色（绿色）</summary>
    public static (int R, int G, int B) DefaultColor { get; set; } = (0, 255, 0);

    /// <summary>格式化点亮指令：(layer=1, index=3) → "&lt;1,3,0,255,0&gt;"</summary>
    public static string FormatCommand(int layer, int index)
        => FormatCommand(layer, index, DefaultColor.R, DefaultColor.G, DefaultColor.B);

    /// <summary>格式化点亮指令（自定义颜色）</summary>
    public static string FormatCommand(int layer, int index, int r, int g, int b)
        => $"<{layer},{index},{r},{g},{b}>";

    /// <summary>解析点亮指令 "&lt;1,3,0,255,0&gt;" → (Layer, Index, R, G, B)</summary>
    public static (int Layer, int Index, int R, int G, int B) ParseCommand(string cmd)
    {
        var inner = cmd.Trim('<', '>');
        var parts = inner.Split(',');
        return (int.Parse(parts[0]), int.Parse(parts[1]),
                int.Parse(parts[2]), int.Parse(parts[3]), int.Parse(parts[4]));
    }

    public const string OffCommand = "<OFF>";
}
