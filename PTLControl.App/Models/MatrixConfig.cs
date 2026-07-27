using System.Collections.Generic;

namespace PTLControl.Models;

/// <summary>整个矩阵配置，对应 serial_mapping.json 根对象</summary>
public class MatrixConfig
{
    public List<RowConfig> Rows { get; set; } = new();
}

/// <summary>一行（对应一个 Layer）</summary>
public class RowConfig
{
    public int Layer { get; set; }
    public List<CellConfig> Cells { get; set; } = new();
}

/// <summary>一个点位</summary>
public class CellConfig
{
    public string Key { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public int Index { get; set; }
}
