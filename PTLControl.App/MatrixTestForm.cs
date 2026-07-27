using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PTLControl.Models;
using PTLControl.Services;

namespace PTLControl
{
    public partial class MatrixTestForm : Form
    {
        private record CellButton(Button Btn, int Layer, int Index);
        private readonly List<CellButton> _cells = new();
        private MatrixConfig _config = new();
        private bool _teachMode = false;

        private static readonly Keys[][] _keyMap = new Keys[][]
        {
            new[] { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0 },
            new[] { Keys.Q, Keys.W, Keys.E, Keys.R, Keys.T, Keys.Y, Keys.U, Keys.I, Keys.O, Keys.P },
            new[] { Keys.A, Keys.S, Keys.D, Keys.F, Keys.G, Keys.H, Keys.J, Keys.K, Keys.L },
            new[] { Keys.Z, Keys.X, Keys.C, Keys.V, Keys.B, Keys.N, Keys.M },
        };
        private readonly Dictionary<Keys, CellButton> _keyBinding = new();

        public MatrixTestForm()
        {
            InitializeComponent();
            KeyPreview = true;
            KeyDown += MatrixTestForm_KeyDown;
        }

        private void MatrixTestForm_Load(object sender, EventArgs e)
        {
            btnSaveMapping.Enabled = false;
            btnSaveMapping.BackColor = SystemColors.Control;
            BuildMatrix();
        }

        private void BuildMatrix()
        {
            _config = ConfigService.Load();
            if (_config.Rows.Count == 0)
            {
                lblStatus.Text = "尚无配置，请先在映射管理中添加数据。";
                return;
            }
            pnlMatrix.SuspendLayout();
            pnlMatrix.Controls.Clear();
            _cells.Clear();
            _keyBinding.Clear();

            for (int rowIdx = 0; rowIdx < _config.Rows.Count; rowIdx++)
            {
                var row = _config.Rows[rowIdx];
                int maxIdx = row.Cells.Count > 0 ? row.Cells.Max(c => c.Index) : 0;
                int count = maxIdx + 1;
                Keys[] rowKeys = rowIdx < _keyMap.Length ? _keyMap[rowIdx] : Array.Empty<Keys>();

                var matrixRow = new FlowLayoutPanel();
                matrixRow.AutoSize = true;
                matrixRow.FlowDirection = FlowDirection.LeftToRight;
                matrixRow.WrapContents = false;
                matrixRow.Margin = new Padding(0, 0, 0, 4);

                var rowLabel = new Label();
                rowLabel.Text = "L" + row.Layer;
                rowLabel.Width = 32;
                rowLabel.TextAlign = ContentAlignment.MiddleCenter;
                rowLabel.Font = new Font("Microsoft YaHei", 8F, FontStyle.Bold);
                rowLabel.Margin = new Padding(0, 0, 4, 0);
                matrixRow.Controls.Add(rowLabel);

                for (int i = 0; i < count; i++)
                {
                    var btn = new Button();
                    btn.Width = 32;
                    btn.Height = 28;
                    btn.Text = i.ToString();
                    btn.Font = new Font("Microsoft YaHei", 6.5F);
                    btn.BackColor = Color.WhiteSmoke;
                    btn.Margin = new Padding(1);
                    btn.Padding = new Padding(0);
                    btn.Tag = false;

                    var cb = new CellButton(btn, row.Layer, i);
                    _cells.Add(cb);
                    btn.Click += delegate { ToggleCell(cb); };
                    matrixRow.Controls.Add(btn);

                    Keys bk = i < rowKeys.Length ? rowKeys[i] : Keys.None;
                    if (bk != Keys.None) _keyBinding[bk] = cb;
                }
                pnlMatrix.Controls.Add(matrixRow);
            }
            pnlMatrix.ResumeLayout();
            lblStatus.Text = _config.Rows.Count + " 行，" + _cells.Count + " 个点位。";
        }

        // ── 多选切换（点亮/熄灭单颗，不影响其他）─────────────────────────────

        private void ToggleCell(CellButton cb)
        {
            bool isOn = cb.Btn.Tag is true;
            if (isOn)
            {
                // 熄灭
                PTLController.TurnOff(cb.Layer, cb.Index);
                cb.Btn.BackColor = Color.WhiteSmoke;
                cb.Btn.Tag = false;
                LogCmd("OFF L" + cb.Layer + ":" + cb.Index);
            }
            else
            {
                // 教授模式下检查该行已亮灯数是否已达到 Key 数量上限
                if (_teachMode)
                {
                    var row = _config.Rows.FirstOrDefault(r => r.Layer == cb.Layer);
                    if (row != null)
                    {
                        int keyCount = row.Cells.Count(c => !string.IsNullOrWhiteSpace(c.Key));
                        int litCount = _cells.Count(c => c.Layer == cb.Layer && c.Btn.Tag is true);
                        if (litCount >= keyCount)
                        {
                            MessageBox.Show(
                                "L" + cb.Layer + " 行已点亮 " + litCount + " 颗灯，等于物料 Key 数量 " + keyCount + "。\n请先到映射管理中添加更多物料 Key。",
                                "无法继续点亮", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                }
                // 点亮
                PTLController.SetLight(cb.Layer, cb.Index, LedColor.Green);
                cb.Btn.BackColor = Color.LimeGreen;
                cb.Btn.Tag = true;
                LogCmd("ON  L" + cb.Layer + ":" + cb.Index);
            }
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var parts = new List<string>();
            foreach (var row in _config.Rows)
            {
                int keyCount = row.Cells.Count(c => !string.IsNullOrWhiteSpace(c.Key));
                int litCount = _cells.Count(c => c.Layer == row.Layer && c.Btn.Tag is true);
                string mark = litCount == keyCount ? "✓" : (litCount + "/" + keyCount);
                parts.Add("L" + row.Layer + ":" + mark);
            }
            lblStatus.Text = string.Join("  ", parts);
        }

        // ── 教授模式：读取映射，点亮已配置的点位 ─────────────────────────────

        private async void btnCalibrate_Click(object sender, EventArgs e)
        {
            if (_teachMode)
            {
                // 退出教授模式：全灭 + 禁用保存
                _teachMode = false;
                PTLController.AllOff();
                foreach (var c in _cells) { c.Btn.BackColor = Color.WhiteSmoke; c.Btn.Tag = false; }
                btnSaveMapping.Enabled = false;
                btnSaveMapping.BackColor = SystemColors.Control;
                btnCalibrate.BackColor = Color.LightSkyBlue;
                btnCalibrate.Text = "教授模式";
                LogCmd("退出教授模式");
                lblStatus.Text = _config.Rows.Count + " 行，" + _cells.Count + " 个点位。";
                return;
            }

            // 进入教授模式
            if (_cells.Count == 0) { BuildMatrix(); }
            if (_config.Rows.Count == 0) return;

            // 先全灭
            PTLController.AllOff();
            foreach (var c in _cells) { c.Btn.BackColor = Color.WhiteSmoke; c.Btn.Tag = false; }

            // 禁用所有操作，显示进度
            tableMain.Enabled = false;
            lblStatus.Text = "教授模式加载中...";
            var progress = new ProgressBar();
            progress.Dock = DockStyle.Top;
            progress.Height = 18;
            progress.Style = ProgressBarStyle.Continuous;
            Controls.Add(progress);
            progress.BringToFront();

            int total = 0;
            foreach (var row in _config.Rows)
                foreach (var cell in row.Cells)
                    if (!string.IsNullOrWhiteSpace(cell.Key)) total++;
            progress.Maximum = total > 0 ? total : 1;
            progress.Value = 0;

            await Task.Delay(50);

            int litCount = 0;
            foreach (var row in _config.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    if (string.IsNullOrWhiteSpace(cell.Key)) continue;
                    var cb = _cells.FirstOrDefault(c => c.Layer == row.Layer && c.Index == cell.Index);
                    if (cb == null) { litCount++; progress.Value = litCount; continue; }
                    PTLController.SetLight(cb.Layer, cb.Index, LedColor.Green);
                    cb.Btn.BackColor = Color.LimeGreen;
                    cb.Btn.Tag = true;
                    litCount++;
                    progress.Value = litCount;
                    lblStatus.Text = "教授模式加载中... " + litCount + "/" + total;
                    await Task.Delay(10);
                }
            }

            Controls.Remove(progress);
            progress.Dispose();
            tableMain.Enabled = true;

            _teachMode = true;
            btnSaveMapping.Enabled = true;
            btnSaveMapping.BackColor = Color.Gold;
            btnCalibrate.BackColor = Color.Orange;
            btnCalibrate.Text = "退出教授";
            LogCmd("教授模式：点亮 " + litCount + " 个已映射点位");
            UpdateStatus();
        }

        // ── 保存映射：保留原 Key 顺序，重新分配物理 Index ─────────────────────

        private void btnSaveMapping_Click(object sender, EventArgs e)
        {
            if (!_teachMode || _config.Rows.Count == 0) return;

            // 先检查每行亮灯数是否等于 Key 数
            var errors = new List<string>();
            foreach (var row in _config.Rows)
            {
                int keyCount = row.Cells.Count(c => !string.IsNullOrWhiteSpace(c.Key));
                int litCount = _cells.Count(c => c.Layer == row.Layer && c.Btn.Tag is true);
                if (litCount < keyCount)
                    errors.Add("L" + row.Layer + "：还有 " + (keyCount - litCount) + " 个物料未分配灯珠");
                else if (litCount > keyCount)
                    errors.Add("L" + row.Layer + "：多出 " + (litCount - keyCount) + " 颗灯，请到映射管理添加 Key");
            }
            if (errors.Count > 0)
            {
                MessageBox.Show(
                    "以下行的灯珠数与物料 Key 数不匹配，无法保存：\n\n" + string.Join("\n", errors),
                    "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 按行保存：保留原 Key 顺序，按亮灯从左到右分配新 Index
            foreach (var row in _config.Rows)
            {
                // 收集该行原有的 Key（按原顺序）
                var keys = row.Cells
                    .Where(c => !string.IsNullOrWhiteSpace(c.Key))
                    .Select(c => c.Key)
                    .ToList();

                // 收集该行亮着的物理 Index（从小到大）
                var litIndexes = _cells
                    .Where(c => c.Layer == row.Layer && c.Btn.Tag is true)
                    .Select(c => c.Index)
                    .OrderBy(x => x)
                    .ToList();

                // 一一对应：第1个Key → 第1个亮灯Index，第2个Key → 第2个...
                var newCells = new List<CellConfig>();
                for (int i = 0; i < keys.Count; i++)
                {
                    newCells.Add(new CellConfig { Key = keys[i], Index = litIndexes[i] });
                }
                row.Cells = newCells;
            }

            ConfigService.Save(_config);
            int total = _config.Rows.Sum(r => r.Cells.Count);
            LogCmd("已保存映射，共 " + total + " 个点位");
            lblStatus.Text = "映射已保存，共 " + total + " 个点位。";
        }

        // ── 键盘 ─────────────────────────────────────────────────────────────

        private void MatrixTestForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_keyBinding.TryGetValue(e.KeyCode, out var cb))
            { ToggleCell(cb); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape)
            { btnOffAll_Click(this, EventArgs.Empty); e.Handled = true; }
        }

        // ── 全灭 ─────────────────────────────────────────────────────────────

        private void btnOffAll_Click(object sender, EventArgs e)
        {
            PTLController.AllOff();
            foreach (var c in _cells) { c.Btn.BackColor = Color.WhiteSmoke; c.Btn.Tag = false; }
            LogCmd("<OFF>");
        }

        // ── 关闭 ─────────────────────────────────────────────────────────────

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            try { PTLController.AllOff(); } catch { }
        }

        private void LogCmd(string cmd)
        {
            lstLog.Items.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + cmd);
            lstLog.TopIndex = lstLog.Items.Count - 1;
        }

    }
}
