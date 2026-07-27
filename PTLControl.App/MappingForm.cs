using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PTLControl.Models;
using PTLControl.Services;

namespace PTLControl;

public partial class MappingForm : Form
{
    private MatrixConfig _config;
    private bool _suppressNudEvent;

    public MappingForm()
    {
        InitializeComponent();
        _config = ConfigService.Load();
    }

    private void MappingForm_Load(object sender, EventArgs e)
    {
        RefreshPrefixList();
        LoadRowList();
    }

    // ── 前缀下拉管理 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 重建前缀下拉：A-Z 中去掉已被任意行使用过的前缀字母。
    /// "使用过"定义为：该行至少有一个 Key 以该字母开头。
    /// </summary>
    private void RefreshPrefixList()
    {
        // 收集已使用的前缀
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _config.Rows)
            foreach (var cell in row.Cells)
                if (!string.IsNullOrWhiteSpace(cell.Key) && cell.Key.Length > 0)
                    used.Add(cell.Key[0].ToString().ToUpper());

        string? current = cmbPrefix.SelectedItem?.ToString();
        cmbPrefix.Items.Clear();
        for (char c = 'A'; c <= 'Z'; c++)
        {
            if (!used.Contains(c.ToString()))
                cmbPrefix.Items.Add(c.ToString());
        }

        // 尽量保持原来的选中项，否则选第一个
        if (current != null && cmbPrefix.Items.Contains(current))
            cmbPrefix.SelectedItem = current;
        else if (cmbPrefix.Items.Count > 0)
            cmbPrefix.SelectedIndex = 0;
    }

    // ── 行列结构管理 ──────────────────────────────────────────────────────────

    private void LoadRowList()
    {
        lvRows.Items.Clear();
        for (int i = 0; i < _config.Rows.Count; i++)
        {
            var row = _config.Rows[i];
            var item = new ListViewItem((i + 1).ToString());
           
            item.SubItems.Add(row.Layer.ToString());
            item.SubItems.Add(row.Cells.Count.ToString());
            item.Tag = i;
            lvRows.Items.Add(item);
        }
    }

    private void btnAddRow_Click(object sender, EventArgs e)
    {
        int layer = _config.Rows.Count + 1;
        int cols = (int)nudCols.Value;

        var row = new RowConfig { Layer = layer };
        for (int i = 0; i < cols; i++)
            row.Cells.Add(new CellConfig { Key = "", Index = i + 1 });

        _config.Rows.Add(row);
        LoadRowList();

        int newIdx = lvRows.Items.Count - 1;
        lvRows.Items[newIdx].Selected = true;
        lvRows.Items[newIdx].EnsureVisible();
        ShowGrid(_config.Rows.Count - 1);
    }

    private void btnDeleteRow_Click(object sender, EventArgs e)
    {
        if (lvRows.SelectedItems.Count == 0) return;
        int idx = (int)lvRows.SelectedItems[0].Tag!;
        _config.Rows.RemoveAt(idx);

        for (int i = 0; i < _config.Rows.Count; i++)
            _config.Rows[i].Layer = i + 1;

        LoadRowList();
        dgvCells.Rows.Clear();
        dgvCells.Tag = null;
        lblGridTitle.Text = "请在左侧选择一行";

        _suppressNudEvent = true;
        nudCols.Value = 10;
        _suppressNudEvent = false;

        RefreshPrefixList();
    }

    private void lvRows_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lvRows.SelectedItems.Count == 0) return;
        int idx = (int)lvRows.SelectedItems[0].Tag!;
        ShowGrid(idx);

        _suppressNudEvent = true;
        nudCols.Value = Math.Max(1, _config.Rows[idx].Cells.Count);
        _suppressNudEvent = false;
    }

    // ── nudCols 实时修改点位数 ────────────────────────────────────────────────

    private void nudCols_ValueChanged(object sender, EventArgs e)
    {
        if (_suppressNudEvent) return;
        if (dgvCells.Tag is not int rowIdx) return;

        var row = _config.Rows[rowIdx];
        int target = (int)nudCols.Value;
        int current = row.Cells.Count;

        if (target > current)
        {
            int nextIndex = current > 0 ? row.Cells[^1].Index + 1 : 1;
            for (int i = current; i < target; i++)
                row.Cells.Add(new CellConfig { Key = "", Index = nextIndex++ });
        }
        else if (target < current)
        {
            row.Cells.RemoveRange(target, current - target);
        }

        ShowGrid(rowIdx);
        LoadRowList();
    }

    // ── 点位表格 ──────────────────────────────────────────────────────────────

    private void ShowGrid(int rowIdx)
    {
        dgvCells.Tag = rowIdx;
        dgvCells.Rows.Clear();

        var row = _config.Rows[rowIdx];
        lblGridTitle.Text = $"第 {rowIdx + 1} 行（Layer {row.Layer}）— 共 {row.Cells.Count} 个点位";

        for (int i = 0; i < row.Cells.Count; i++)
        {
            var cell = row.Cells[i];
            dgvCells.Rows.Add(i + 1, cell.Key, cell.Alias, cell.Index);
        }
    }

    private void btnAddCell_Click(object sender, EventArgs e)
    {
        if (dgvCells.Tag is not int rowIdx) return;
        var row = _config.Rows[rowIdx];
        int nextIndex = row.Cells.Count > 0 ? row.Cells[^1].Index + 1 : 1;
        row.Cells.Add(new CellConfig { Key = "", Index = nextIndex });

        _suppressNudEvent = true;
        nudCols.Value = row.Cells.Count;
        _suppressNudEvent = false;

        ShowGrid(rowIdx);
        LoadRowList();
    }

    private void btnDeleteCell_Click(object sender, EventArgs e)
    {
        if (dgvCells.Tag is not int rowIdx) return;
        if (dgvCells.SelectedRows.Count == 0) return;

        int cellIdx = Convert.ToInt32(dgvCells.SelectedRows[0].Cells[0].Value) - 1;
        _config.Rows[rowIdx].Cells.RemoveAt(cellIdx);

        _suppressNudEvent = true;
        nudCols.Value = Math.Max(1, _config.Rows[rowIdx].Cells.Count);
        _suppressNudEvent = false;

        ShowGrid(rowIdx);
        LoadRowList();
    }

    private void dgvCells_CellEndEdit(object sender, DataGridViewCellEventArgs e)
    {
        if (dgvCells.Tag is not int rowIdx) return;
        var row = _config.Rows[rowIdx];
        if (e.RowIndex >= row.Cells.Count) return;

        var cell = row.Cells[e.RowIndex];
        var dgvRow = dgvCells.Rows[e.RowIndex];

        if (e.ColumnIndex == 1)
        {
            cell.Key = dgvRow.Cells[1].Value?.ToString() ?? "";
            RefreshPrefixList();
        }
        else if (e.ColumnIndex == 2)
        {
            cell.Alias = dgvRow.Cells[2].Value?.ToString() ?? "";
        }
        else if (e.ColumnIndex == 3)
        {
            if (int.TryParse(dgvRow.Cells[3].Value?.ToString(), out int idx))
                cell.Index = idx;
        }
    }

    // ── 自动生成 Index ────────────────────────────────────────────────────────

    private void btnAutoIndex_Click(object sender, EventArgs e)
    {
        if (dgvCells.Tag is not int rowIdx) return;
        var row = _config.Rows[rowIdx];
        if (row.Cells.Count == 0) return;

        int start = (int)nudStart.Value;
        int step  = (int)nudStep.Value;

        for (int i = 0; i < row.Cells.Count; i++)
            row.Cells[i].Index = start + i * step;

        ShowGrid(rowIdx);
    }

    // ── 自动生成 Key ──────────────────────────────────────────────────────────

    private void btnAutoKey_Click(object sender, EventArgs e)
    {
        if (dgvCells.Tag is not int rowIdx) return;
        var row = _config.Rows[rowIdx];
        if (row.Cells.Count == 0) return;

        string prefix = cmbPrefix.SelectedItem?.ToString() ?? "A";

        // 检查该前缀是否已被其他行占用
        foreach (var r in _config.Rows)
        {
            if (_config.Rows.IndexOf(r) == rowIdx) continue;
            foreach (var c in r.Cells)
            {
                if (!string.IsNullOrWhiteSpace(c.Key) &&
                    c.Key[0].ToString().Equals(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"前缀 \"{prefix}\" 已被第 {_config.Rows.IndexOf(r) + 1} 行使用，请选择其他前缀。",
                        "前缀冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
        }

        // 生成 Key
        for (int i = 0; i < row.Cells.Count; i++)
            row.Cells[i].Key = $"{prefix}{i + 1}";

        ShowGrid(rowIdx);

        // 从下拉中移除已用前缀，并自动选中下一个可用字母
        RefreshPrefixList();
    }

    // ── 保存（含全量重复校验） ────────────────────────────────────────────────

    private void btnSave_Click(object sender, EventArgs e)
    {
        // 全量扫描，收集所有重复 Key
        var seen = new Dictionary<string, (int RowIdx, int CellIdx)>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        for (int ri = 0; ri < _config.Rows.Count; ri++)
        {
            var row = _config.Rows[ri];
            for (int ci = 0; ci < row.Cells.Count; ci++)
            {
                var key = row.Cells[ci].Key;
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (seen.TryGetValue(key, out var first))
                {
                    duplicates.Add(
                        $"  \"{key}\"：第 {first.RowIdx + 1} 行第 {first.CellIdx + 1} 位  ↔  第 {ri + 1} 行第 {ci + 1} 位");
                }
                else
                {
                    seen[key] = (ri, ci);
                }
            }
        }

        if (duplicates.Count > 0)
        {
            var msg = "发现重复 Key，请修正后再保存：\n\n" + string.Join("\n", duplicates);
            MessageBox.Show(msg, "保存失败 — Key 重复", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            // 高亮当前显示行中的重复项
            HighlightDuplicatesInGrid(seen, duplicates);
            return;
        }

        ConfigService.Save(_config);
        MessageBox.Show("保存成功！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>将 dgvCells 中重复的 Key 单元格标红</summary>
    private void HighlightDuplicatesInGrid(
        Dictionary<string, (int RowIdx, int CellIdx)> seen,
        List<string> duplicates)
    {
        if (dgvCells.Tag is not int rowIdx) return;

        // 收集本行中重复的 Key 集合
        var dupKeys = new HashSet<string>(StringComparer.Ordinal);
        var row = _config.Rows[rowIdx];
        for (int ci = 0; ci < row.Cells.Count; ci++)
        {
            var key = row.Cells[ci].Key;
            if (string.IsNullOrWhiteSpace(key)) continue;
            // 如果全局有重复，标记
            int count = 0;
            foreach (var r in _config.Rows)
                foreach (var c in r.Cells)
                    if (c.Key == key) count++;
            if (count > 1) dupKeys.Add(key);
        }

        foreach (DataGridViewRow dgvRow in dgvCells.Rows)
        {
            var keyCell = dgvRow.Cells[1];
            var keyVal = keyCell.Value?.ToString() ?? "";
            keyCell.Style.BackColor = dupKeys.Contains(keyVal)
                ? Color.LightCoral
                : Color.Empty;
        }
    }
}
