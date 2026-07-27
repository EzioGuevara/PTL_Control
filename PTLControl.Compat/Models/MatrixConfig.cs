// ============================================================
// PTL LED Matrix Control System - .NET Standard 2.0 Compat
// Developer: Ezio @ IDEMIA
// Description: Matrix configuration models for serial_mapping.json.
// ============================================================
using System.Collections.Generic;

namespace PTLControl.Compat.Models
{
    /// <summary>整个矩阵配置，对应 serial_mapping.json 根对象</summary>
    public class MatrixConfig
    {
        public List<RowConfig> Rows { get; set; } = new List<RowConfig>();
    }

    /// <summary>一行（对应一个 Layer）</summary>
    public class RowConfig
    {
        public int Layer { get; set; }
        public List<CellConfig> Cells { get; set; } = new List<CellConfig>();
    }

    /// <summary>一个点位</summary>
    public class CellConfig
    {
        public string Key { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public int Index { get; set; }
    }
}
