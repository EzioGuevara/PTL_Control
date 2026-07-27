using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PTLControl.Models;
using PTLControl.Services;

namespace PTLControl
{
    public sealed class MqttMappingForm : Form
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly Button _btnAdd = new Button();
        private readonly Button _btnDelete = new Button();
        private readonly Button _btnSave = new Button();
        private readonly Label _lblTip = new Label();
        private MqttMappingConfig _mapping = new MqttMappingConfig();

        public MqttMappingForm()
        {
            Text = "MQTT 映射管理 - IDEMIA";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 420);
            Width = 920;
            Height = 560;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            _lblTip.AutoSize = true;
            _lblTip.Text = "MQTT 映射按节点维护：必须手工输入 group 与 tagId（灯条ID）。";
            _lblTip.Padding = new Padding(0, 2, 0, 6);
            root.Controls.Add(_lblTip, 0, 0);

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add("colNo", "#");
            _grid.Columns.Add("colKey", "Key");
            _grid.Columns.Add("colTagId", "TagId(灯条ID)");
            _grid.Columns.Add("colGroup", "Group");
            _grid.Columns.Add("colAlias", "Alias");
            _grid.Columns[0].ReadOnly = true;
            _grid.Columns[0].FillWeight = 8;
            _grid.Columns[1].FillWeight = 18;
            _grid.Columns[2].FillWeight = 34;
            _grid.Columns[3].FillWeight = 12;
            _grid.Columns[4].FillWeight = 26;
            root.Controls.Add(_grid, 0, 1);

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _btnAdd.Text = "添加节点";
            _btnAdd.Width = 90;
            _btnAdd.Click += (_, __) => AddNodeRow();
            _btnDelete.Text = "删除节点";
            _btnDelete.Width = 90;
            _btnDelete.Click += (_, __) => DeleteSelectedRow();
            _btnSave.Text = "保存 MQTT 映射";
            _btnSave.Width = 130;
            _btnSave.BackColor = Color.LightGreen;
            _btnSave.Click += (_, __) => SaveMapping();

            bottom.Controls.Add(_btnAdd);
            bottom.Controls.Add(_btnDelete);
            bottom.Controls.Add(_btnSave);
            root.Controls.Add(bottom, 0, 2);

            Load += (_, __) => LoadMapping();
        }

        private void LoadMapping()
        {
            _mapping = ConfigService.LoadMqttMapping();
            _grid.Rows.Clear();
            foreach (var node in _mapping.Nodes)
            {
                _grid.Rows.Add(
                    _grid.Rows.Count + 1,
                    node.Key,
                    node.TagId,
                    node.Group,
                    node.Alias);
            }
        }

        private void AddNodeRow()
        {
            _grid.Rows.Add(_grid.Rows.Count + 1, "", "", 0, "");
        }

        private void DeleteSelectedRow()
        {
            if (_grid.SelectedRows.Count == 0)
                return;
            _grid.Rows.RemoveAt(_grid.SelectedRows[0].Index);
            ReNumber();
        }

        private void ReNumber()
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
                _grid.Rows[i].Cells[0].Value = i + 1;
        }

        private void SaveMapping()
        {
            var result = new MqttMappingConfig { Nodes = new List<MqttNodeConfig>() };
            var keySet = new HashSet<string>(StringComparer.Ordinal);

            foreach (DataGridViewRow row in _grid.Rows)
            {
                var key = (row.Cells[1].Value?.ToString() ?? string.Empty).Trim();
                var tagId = (row.Cells[2].Value?.ToString() ?? string.Empty).Trim();
                var groupText = (row.Cells[3].Value?.ToString() ?? "0").Trim();
                var alias = (row.Cells[4].Value?.ToString() ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(alias) && string.IsNullOrWhiteSpace(tagId))
                    continue;

                if (string.IsNullOrWhiteSpace(tagId))
                {
                    MessageBox.Show("TagId 不能为空。", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int group;
                if (!int.TryParse(groupText, out group) || group < 0 || group > 254)
                {
                    MessageBox.Show("Group 必须是 0~254 的整数。", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(key))
                {
                    if (!keySet.Add(key))
                    {
                        MessageBox.Show("Key 重复：" + key, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                result.Nodes.Add(new MqttNodeConfig
                {
                    Key = key,
                    TagId = tagId,
                    Group = group,
                    Alias = alias
                });
            }

            ConfigService.SaveMqttMapping(result);
            MessageBox.Show("MQTT 映射保存成功。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadMapping();
        }
    }
}
