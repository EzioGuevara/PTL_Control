using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace PTLControl.HardwareHost
{
    internal sealed class HardwareHostForm : Form
    {
        private readonly HardwareBrokerServer _server = new HardwareBrokerServer();
        private readonly EventWaitHandle _showEvent;
        private readonly Label _hostState = StatusValue();
        private readonly Label _connectionState = StatusValue();
        private readonly Label _clientsState = StatusValue();
        private readonly Label _queueState = StatusValue();
        private readonly Label _statusBanner = new Label();
        private readonly RichTextBox _logBox = new RichTextBox();
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        private readonly NotifyIcon _trayIcon;
        private Thread _serverThread;
        private Thread _showThread;
        private bool _allowExit;

        public HardwareHostForm(EventWaitHandle showEvent)
        {
            _showEvent = showEvent;
            Text = "PTLControl Hardware Host";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(640, 420);
            Size = new Size(760, 520);
            BackColor = Color.FromArgb(244, 247, 250);
            Icon = SystemIcons.Application;

            Controls.Add(BuildMainContent());
            Controls.Add(BuildHeader());

            var menu = new ContextMenuStrip();
            menu.Items.Add("显示窗口", null, (s, e) => ShowWindow());
            menu.Items.Add("打开日志目录", null, OpenLogDirectory);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, ExitMenu_Click);
            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "PTLControl Hardware Host",
                ContextMenuStrip = menu,
                Visible = true
            };
            _trayIcon.DoubleClick += (s, e) => ShowWindow();

            foreach (var line in HostLog.GetRecentLines(80)) AppendLog(line);
            HostLog.LineWritten += HostLog_LineWritten;
            Shown += HardwareHostForm_Shown;
            FormClosing += HardwareHostForm_FormClosing;
            _timer.Interval = 750;
            _timer.Tick += (s, e) => RefreshStatus();
            _timer.Start();
            RefreshStatus();
        }

        private Control BuildHeader()
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.FromArgb(24, 48, 83) };
            panel.Controls.Add(new Label
            {
                Text = "PTLControl Hardware Host",
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 14)
            });
            var hide = HeaderButton("隐藏");
            hide.Location = new Point(panel.Width - hide.Width - 18, 11);
            hide.Click += (s, e) => HideToTray();
            panel.Controls.Add(hide);
            panel.Resize += (s, e) => hide.Left = panel.ClientSize.Width - hide.Width - 18;
            return panel;
        }

        private Control BuildMainContent()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            for (var i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cards.Controls.Add(StatusCard("宿主", _hostState), 0, 0);
            cards.Controls.Add(StatusCard("串口", _connectionState), 1, 0);
            cards.Controls.Add(StatusCard("调用方", _clientsState), 2, 0);
            cards.Controls.Add(StatusCard("队列 / 已发", _queueState), 3, 0);
            root.Controls.Add(cards, 0, 0);

            _statusBanner.Dock = DockStyle.Fill;
            _statusBanner.TextAlign = ContentAlignment.MiddleLeft;
            _statusBanner.AutoEllipsis = true;
            _statusBanner.Padding = new Padding(10, 0, 10, 0);
            _statusBanner.Margin = new Padding(4, 3, 4, 3);
            root.Controls.Add(_statusBanner, 0, 1);
            root.Controls.Add(BuildLogPanel(), 0, 2);
            return root;
        }

        private Control BuildLogPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8) };
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, FlowDirection = FlowDirection.LeftToRight };
            toolbar.Controls.Add(new Label
            {
                Text = "实时日志",
                Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(2, 7, 14, 2)
            });
            var clear = SmallButton("清空");
            clear.Click += (s, e) => _logBox.Clear();
            toolbar.Controls.Add(clear);
            var open = SmallButton("日志目录");
            open.Click += OpenLogDirectory;
            toolbar.Controls.Add(open);
            var reload = SmallButton("重载配置");
            reload.Click += ReloadConfig_Click;
            toolbar.Controls.Add(reload);

            _logBox.Dock = DockStyle.Fill;
            _logBox.ReadOnly = true;
            _logBox.BackColor = Color.FromArgb(18, 24, 33);
            _logBox.ForeColor = Color.FromArgb(210, 220, 232);
            _logBox.BorderStyle = BorderStyle.None;
            _logBox.Font = new Font("Consolas", 9f);
            _logBox.WordWrap = false;
            panel.Controls.Add(_logBox);
            panel.Controls.Add(toolbar);
            return panel;
        }

        private void HardwareHostForm_Shown(object sender, EventArgs e)
        {
            _serverThread = new Thread(RunServer) { IsBackground = true, Name = "PTL-Hardware-Server" };
            _serverThread.Start();
            _showThread = new Thread(WaitForShowRequests) { IsBackground = true, Name = "PTL-Hardware-ShowWindow" };
            _showThread.Start();
        }

        private void RunServer()
        {
            try { _server.Run(); }
            catch (Exception ex) { HostLog.Write("宿主服务异常退出：" + ex); }
        }

        private void WaitForShowRequests()
        {
            while (!_allowExit)
            {
                try { _showEvent.WaitOne(); if (!_allowExit && IsHandleCreated) BeginInvoke((Action)ShowWindow); }
                catch { return; }
            }
        }

        private void RefreshStatus()
        {
            _hostState.Text = "运行中";
            _hostState.ForeColor = Good;
            _connectionState.Text = _server.IsSerialConnected ? Dash(_server.ActualPort) + " 已连接" : "未连接";
            _connectionState.ForeColor = _server.IsSerialConnected ? Good : Bad;
            _clientsState.Text = _server.ActiveClientCount.ToString();
            _queueState.Text = _server.QueueLength + " / " + _server.SentCount;

            var error = _server.LastError;
            if (string.IsNullOrWhiteSpace(error))
            {
                _statusBanner.Text = "状态正常  ·  配置端口 " + Dash(_server.ConfiguredPort);
                _statusBanner.BackColor = Color.FromArgb(226, 245, 235);
                _statusBanner.ForeColor = Color.FromArgb(20, 112, 68);
            }
            else
            {
                _statusBanner.Text = "警告：" + error;
                _statusBanner.BackColor = Color.FromArgb(255, 239, 219);
                _statusBanner.ForeColor = Color.FromArgb(161, 83, 13);
            }
        }

        private void HostLog_LineWritten(object sender, string line)
        {
            if (_allowExit || !IsHandleCreated) return;
            try { BeginInvoke((Action)(() => AppendLog(line))); } catch { }
        }

        private void AppendLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.SelectionColor = line.IndexOf("失败", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("错误", StringComparison.OrdinalIgnoreCase) >= 0
                ? Color.FromArgb(255, 120, 120)
                : line.IndexOf("未响应", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("警告", StringComparison.OrdinalIgnoreCase) >= 0
                    ? Color.FromArgb(255, 202, 105)
                    : Color.FromArgb(210, 220, 232);
            _logBox.AppendText(line + Environment.NewLine);
            if (_logBox.Lines.Length > 1200)
                _logBox.Text = string.Join(Environment.NewLine, _logBox.Lines, 300, _logBox.Lines.Length - 300);
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
        }

        private void ReloadConfig_Click(object sender, EventArgs e)
        {
            try { _server.ReloadConfiguration(); HostLog.Write("配置已重新加载。"); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "配置加载失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            RefreshStatus();
        }

        private static void OpenLogDirectory(object sender, EventArgs e)
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PTLControl", "logs");
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
        }

        private void HardwareHostForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_allowExit) return;
            e.Cancel = true;
            HideToTray();
        }

        private void ExitMenu_Click(object sender, EventArgs e)
        {
            ShowWindow();
            if (!ConfirmExit()) return;
            _allowExit = true;
            _trayIcon.Visible = false;
            Close();
        }

        private bool ConfirmExit()
        {
            using (var dialog = new Form
            {
                Text = "退出宿主",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(360, 132),
                BackColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 9f)
            })
            {
                var message = new Label
                {
                    Text = "退出后将释放串口，上层应用将无法控制硬件。\r\n确定退出吗？",
                    AutoSize = false,
                    Location = new Point(22, 18),
                    Size = new Size(316, 48)
                };
                var cancel = new Button
                {
                    Text = "取消",
                    DialogResult = DialogResult.Cancel,
                    Size = new Size(78, 30),
                    Location = new Point(260, 84)
                };
                var confirm = new Button
                {
                    Text = "退出",
                    DialogResult = DialogResult.Yes,
                    Size = new Size(78, 30),
                    Location = new Point(172, 84)
                };
                dialog.Controls.Add(message);
                dialog.Controls.Add(confirm);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = confirm;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog(this) == DialogResult.Yes;
            }
        }

        private void HideToTray()
        {
            Hide();
            _trayIcon.ShowBalloonTip(1000, "PTLControl Hardware Host", "宿主仍在后台运行。", ToolTipIcon.Info);
        }

        private void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); BringToFront(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                HostLog.LineWritten -= HostLog_LineWritten;
                _timer.Dispose();
                _trayIcon.Dispose();
                _server.Dispose();
            }
            base.Dispose(disposing);
        }

        private static Panel StatusCard(string title, Label value)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(4), Padding = new Padding(12, 8, 12, 7) };
            panel.Controls.Add(value);
            panel.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = Color.FromArgb(90, 103, 118),
                Font = new Font("Microsoft YaHei UI", 8.5f)
            });
            return panel;
        }

        private static Label StatusValue() => new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 53, 72),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        private static Button HeaderButton(string text) => new Button
        {
            Text = text,
            Width = 70,
            Height = 31,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(43, 75, 116)
        };

        private static Button SmallButton(string text) => new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(3, 3, 5, 2),
            Padding = new Padding(6, 1, 6, 1)
        };

        private static readonly Color Good = Color.FromArgb(20, 135, 78);
        private static readonly Color Bad = Color.FromArgb(190, 55, 55);
        private static string Dash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
    }
}
