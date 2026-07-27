using System;
using System.Drawing;
using System.Windows.Forms;
using PTLControl.Models;
using PTLControl.Services;

namespace PTLControl
{
    public sealed class MqttTestForm : Form
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly Button _btnRefresh = new Button();
        private readonly Button _btnOnGreen = new Button();
        private readonly Button _btnBlinkRed = new Button();
        private readonly Button _btnOff = new Button();
        private readonly Button _btnAllOff = new Button();
        private readonly ListBox _log = new ListBox();

        public MqttTestForm()
        {
            Text = "MQTT 节点测试 - IDEMIA";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 460);
            Width = 960;
            Height = 620;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 65f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 35f));
            Controls.Add(root);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _btnRefresh.Text = "刷新映射";
            _btnRefresh.Click += (_, __) => LoadNodes();
            _btnOnGreen.Text = "点亮绿色";
            _btnOnGreen.Click += (_, __) => SendSelected(LedColor.Green, 0);
            _btnBlinkRed.Text = "红色闪烁";
            _btnBlinkRed.Click += (_, __) => SendSelected(LedColor.Red, 500);
            _btnOff.Text = "熄灭选中";
            _btnOff.Click += (_, __) => TurnOffSelected();
            _btnAllOff.Text = "全灭";
            _btnAllOff.Click += (_, __) => AllOff();
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnOnGreen);
            toolbar.Controls.Add(_btnBlinkRed);
            toolbar.Controls.Add(_btnOff);
            toolbar.Controls.Add(_btnAllOff);
            root.Controls.Add(toolbar, 0, 0);

            _grid.Dock = DockStyle.Fill;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.ReadOnly = true;
            _grid.Columns.Add("colKey", "Key");
            _grid.Columns.Add("colTagId", "TagId");
            _grid.Columns.Add("colGroup", "Group");
            _grid.Columns.Add("colAlias", "Alias");
            root.Controls.Add(_grid, 0, 1);

            _log.Dock = DockStyle.Fill;
            _log.Font = new Font("Consolas", 9f);
            _log.HorizontalScrollbar = true;
            root.Controls.Add(_log, 0, 2);

            Load += (_, __) => LoadNodes();
        }

        private void LoadNodes()
        {
            _grid.Rows.Clear();
            var mapping = ConfigService.LoadMqttMapping();
            foreach (var node in mapping.Nodes)
            {
                _grid.Rows.Add(node.Key, node.TagId, node.Group, node.Alias);
            }
            Log("已加载 MQTT 节点：" + mapping.Nodes.Count);
        }

        private string ResolveKeyFromSelected()
        {
            if (_grid.SelectedRows.Count == 0)
                return string.Empty;

            var row = _grid.SelectedRows[0];
            var key = row.Cells[0].Value?.ToString() ?? string.Empty;
            var alias = row.Cells[3].Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(key)) return key;
            if (!string.IsNullOrWhiteSpace(alias)) return alias;
            return string.Empty;
        }

        private void SendSelected(LedColor color, int blinkMs)
        {
            var key = ResolveKeyFromSelected();
            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show("选中行没有可用的 Key/Alias，无法通过兼容接口发送。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var ok = blinkMs > 0
                    ? PTLController.SetBlink(key, color, blinkMs)
                    : PTLController.SetLight(key, color);
                if (!ok)
                {
                    Log("发送失败：未找到映射 key=" + key);
                    return;
                }
                var row = _grid.SelectedRows[0];
                Log($"MQTT发送 key={key}, group={row.Cells[2].Value}, tagId={row.Cells[1].Value}, color={color}, blinkMs={blinkMs}");
            }
            catch (Exception ex)
            {
                Log("发送异常：" + ex.Message);
            }
        }

        private void TurnOffSelected()
        {
            var key = ResolveKeyFromSelected();
            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show("选中行没有可用的 Key/Alias，无法通过兼容接口发送。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var ok = PTLController.TurnOff(key);
                if (!ok)
                {
                    Log("熄灭失败：未找到映射 key=" + key);
                    return;
                }
                var row = _grid.SelectedRows[0];
                Log($"MQTT熄灭 key={key}, group={row.Cells[2].Value}, tagId={row.Cells[1].Value}");
            }
            catch (Exception ex)
            {
                Log("熄灭异常：" + ex.Message);
            }
        }

        private void AllOff()
        {
            try
            {
                PTLController.AllOff();
                Log("MQTT全灭已发送（按节点批量task）。");
            }
            catch (Exception ex)
            {
                Log("全灭异常：" + ex.Message);
            }
        }

        private void Log(string text)
        {
            _log.Items.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text);
            _log.TopIndex = _log.Items.Count - 1;
        }
    }
}
