// ============================================================
// PTL LED Matrix Control System
// Developer: Ezio Li @ IDEMIA
// Description: Main window - serial connection, barcode scan,
//              light control buttons, mapping & matrix test.
// ============================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PTLControl.Models;
using PTLControl.Services;
using CompatConnectionChangedEventArgs = PTLControl.Compat.Models.ConnectionChangedEventArgs;
using CompatTagEventArgs = PTLControl.Compat.Models.TagEventArgs;
using CompatTagEventType = PTLControl.Compat.Models.TagEventType;

namespace PTLControl;

public partial class MainForm : Form
{
    private Dictionary<string, (int Layer, int Index)> _dict = new();
    private Dictionary<string, MqttNodeConfig> _mqttDict = new(StringComparer.Ordinal);
    private StartupConfig _startup = new();

    private readonly TabControl _tabTransport = new TabControl();
    private readonly TabPage _tabSerial = new TabPage("Serial");
    private readonly TabPage _tabMqtt = new TabPage("MQTT");
    private readonly Panel _modeHost = new Panel();
    private readonly FlowLayoutPanel _modePanel = new FlowLayoutPanel();
    private readonly Label _lblMode = new Label();
    private readonly ComboBox _cmbMode = new ComboBox();
    private readonly Button _btnSaveConnConfig = new Button();
    private readonly Button _btnAbout = new Button();
    private readonly Label _lblMqttHost = new Label();
    private readonly Label _lblMqttPort = new Label();
    private readonly Label _lblMqttUser = new Label();
    private readonly Label _lblMqttPwd = new Label();
    private readonly Label _lblMqttId = new Label();
    private readonly Label _lblLogLevel = new Label();
    private readonly TextBox _txtMqttHost = new TextBox();
    private readonly TextBox _txtMqttPort = new TextBox();
    private readonly TextBox _txtMqttUser = new TextBox();
    private readonly TextBox _txtMqttPwd = new TextBox();
    private readonly TextBox _txtMqttId = new TextBox();
    private readonly ComboBox _cmbLogLevel = new ComboBox();
    private readonly Label _lblBeepMode = new Label();
    private readonly ComboBox _cmbBeepMode = new ComboBox();
    private readonly Button _btnMqttConnect = new Button();
    private readonly Button _btnMqttMapping = new Button();
    private readonly Button _btnMqttTest = new Button();
    private readonly ContextMenuStrip _logMenu = new ContextMenuStrip();
    private EventHandler<CompatConnectionChangedEventArgs>? _connectionLogHandler;
    private EventHandler<CompatTagEventArgs>? _tagEventLogHandler;
    private bool _isInitializingUi;
    private bool _suppressModeSelectionChanged;
    private bool _suppressTabSelectionChanged;
    private string _activeMode = "serial";

    public MainForm()
    {
        InitializeComponent();
        ReloadDict();
    }

    // ── 启动 ──────────────────────────────────────────────────────────────────

    private void MainForm_Load(object sender, EventArgs e)
    {
        _isInitializingUi = true;
        // 加载嵌入的 logo
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream("PTLControl.idemia.png");
        if (stream != null) picLogo.Image = System.Drawing.Image.FromStream(stream);

        foreach (var p in PTLController.GetPortNames())
            cmbPorts.Items.Add(p);
        if (cmbPorts.Items.Count > 0)
            cmbPorts.SelectedIndex = 0;

        SetupModeSwitchUi();
        SetupConnectionEditorUi();
        SetupBeepUi();
        ArrangeMainButtons();
        SetupLogCopyUi();
        LoadStartupConfigToUi();
        ApplyModeUi();
        SetConnectedState(PTLController.IsConnected);
        WireRealtimeEventLogs();
        _activeMode = IsMqttMode() ? "mqtt" : "serial";
        _isInitializingUi = false;
        RefreshPortLayout();

        txtBarcode.Focus();
    }

    // ── 串口连接 ──────────────────────────────────────────────────────────────

    private async void btnConnect_Click(object sender, EventArgs e)
    {
        await ToggleConnectionAsync("serial", btnConnect);
    }

    private async void btnMqttConnect_Click(object? sender, EventArgs e)
    {
        await ToggleConnectionAsync("mqtt", _btnMqttConnect);
    }

    private async Task ToggleConnectionAsync(string mode, Button sourceButton)
    {
        sourceButton.Enabled = false;
        try
        {
            if (PTLController.IsConnected)
            {
                try
                {
                    await Task.Run(() => PTLController.Disconnect());
                    btnConnect.Text = "连接";
                    _btnMqttConnect.Text = "连接";
                    AppendLog("连接已断开");
                    SetConnectedState(false);
                }
                catch (Exception ex)
                {
                    AppendLog($"断开失败：{ex.Message}", error: true);
                }
            }
            else
            {
                try
                {
                    bool connectedOk;
                    if (mode == "serial")
                    {
                        if (cmbPorts.SelectedItem is not string port || string.IsNullOrEmpty(port))
                        {
                            AppendLog("请先选择串口", error: true);
                            return;
                        }

                        CaptureStartupFromUi();
                        ConfigService.SaveStartup(_startup);
                        connectedOk = await Task.Run(() => PTLController.Connect());
                        if (!connectedOk || !PTLController.IsConnected)
                        {
                            btnConnect.Text = "连接";
                            _btnMqttConnect.Text = "连接";
                            SetConnectedState(false);
                            AppendLog($"连接失败：串口 {port} 未连接（可能被占用）", error: true);
                            return;
                        }

                        _startup.Serial.PortName = port;
                        AppendLog($"已连接到串口 {port}");
                    }
                    else
                    {
                        CaptureStartupFromUi();
                        ConfigService.SaveStartup(_startup);

                        connectedOk = await Task.Run(() => PTLController.Connect());
                        if (!connectedOk || !PTLController.IsConnected)
                        {
                            btnConnect.Text = "连接";
                            _btnMqttConnect.Text = "连接";
                            SetConnectedState(false);
                            AppendLog($"连接失败：MQTT {_startup.Mqtt.Broker}:{_startup.Mqtt.Port} 未连接", error: true);
                            return;
                        }

                        AppendLog($"已连接 MQTT {_startup.Mqtt.Broker}:{_startup.Mqtt.Port}");
                    }

                    btnConnect.Text = "断开";
                    _btnMqttConnect.Text = "断开";
                    SetConnectedState(true);
                }
                catch (Exception ex)
                {
                    AppendLog($"连接失败：{ex.Message}", error: true);
                }
            }
        }
        finally
        {
            sourceButton.Enabled = true;
        }
    }

    /// <summary>根据串口连接状态启用/禁用操作按钮</summary>
    private void SetConnectedState(bool connected)
    {
        var mqttMode = IsMqttMode();
        btnSend.Enabled          = connected;
        btnSendGreen.Enabled     = connected;
        btnSendRedBlink.Enabled  = connected;
        btnSendGreenBlink.Enabled = connected;
        _cmbBeepMode.Enabled     = connected && mqttMode;
        _lblBeepMode.Enabled     = connected && mqttMode;
        cmbPorts.Enabled         = !mqttMode;
        btnOff.Enabled           = connected;
        btnMarquee.Enabled       = connected;
        btnMapping.Enabled       = !mqttMode;
        btnMatrixTest.Enabled    = connected && !mqttMode;
        _txtMqttHost.Enabled     = mqttMode;
        _txtMqttPort.Enabled     = mqttMode;
        _txtMqttUser.Enabled     = mqttMode;
        _txtMqttPwd.Enabled      = mqttMode;
        _txtMqttId.Enabled       = mqttMode;
        _btnMqttMapping.Enabled  = mqttMode;
        _btnMqttTest.Enabled     = connected && mqttMode;
    }

    // ── 扫码 ──────────────────────────────────────────────────────────────────

    private void txtBarcode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.Handled = e.SuppressKeyPress = true;
        QueryAndLight(LedColor.Green, 0);
    }

    // ── 查询发送（等同回车） ──────────────────────────────────────────────────

    private void btnSend_Click(object sender, EventArgs e)
    {
        // 复用 KeyDown 逻辑：模拟 Enter
        var fakeArgs = new KeyEventArgs(Keys.Enter);
        txtBarcode_KeyDown(sender, fakeArgs);
    }

    // ── 全局熄灭 ──────────────────────────────────────────────────────────────

    private void btnOff_Click(object sender, EventArgs e)
    {
        if (!PTLController.IsConnected)
        {
            AppendLog("未连接", error: true);
            return;
        }
        try
        {
            PTLController.AllOff();
            AppendLog(IsMqttMode() ? "发送全局熄灭：MQTT task(batch, 含关闭蜂鸣)" : $"发送全局熄灭：{CommandService.OffCommand}");
        }
        catch (Exception ex)
        {
            AppendLog($"发送失败：{ex.Message}", error: true);
        }
    }

    // ── 查询（绿色常亮） ────────────────────────────────────────────────────

    private void btnSendGreen_Click(object sender, EventArgs e)
    {
        QueryAndLight(LedColor.Green, 0);
    }

    // ── 查询（红色闪烁） ────────────────────────────────────────────────────

    private void btnSendRedBlink_Click(object sender, EventArgs e)
    {
        QueryAndLight(LedColor.Red, 500);
    }

    // ── 查询（绿色闪烁） ────────────────────────────────────────────────────

    private void btnSendGreenBlink_Click(object sender, EventArgs e)
    {
        QueryAndLight(LedColor.Green, 500);
    }

    // ── 跑马灯 ────────────────────────────────────────────────────────────────

    private void btnMarquee_Click(object sender, EventArgs e)
    {
        if (!PTLController.IsConnected)
        {
            AppendLog("未连接", error: true);
            return;
        }
        try
        {
            PTLController.Marquee(LedColor.Blue, 100);
            AppendLog("跑马灯已启动（蓝色，100ms间隔）");
        }
        catch (Exception ex)
        {
            AppendLog($"跑马灯启动失败：{ex.Message}", error: true);
        }
    }

    /// <summary>统一查询接口：读取扫码框内容，查找映射，发送指定颜色+闪烁</summary>
    private void QueryAndLight(LedColor color, int blinkMs)
    {
        if (!PTLController.IsConnected)
        {
            AppendLog("未连接", error: true);
            return;
        }
        var input = txtBarcode.Text.Trim();
        if (string.IsNullOrEmpty(input))
        {
            AppendLog("请输入物料编码", error: true);
            return;
        }
        try
        {
            var beepFlag = IsMqttMode() ? ResolveBeepFlag(blinkMs) : (bool?)null;
            var ok = blinkMs > 0
                ? PTLController.SetBlink(input, color, blinkMs, beepFlag)
                : PTLController.SetLight(input, color, beepFlag);
            if (!ok)
            {
                AppendLog($"未找到物料：{input}", error: true);
                return;
            }

            var rgb = color.ToRgb();
            var beepOption = IsMqttMode() ? GetSelectedBeepMode() : string.Empty;

            if (IsMqttMode() && _mqttDict.TryGetValue(input, out var mqttNode))
            {
                var mode = blinkMs > 0 ? "SetBlink" : "SetLight";
                AppendLog($"接口 {mode}(key={input},color={color},beep={beepOption})->MQTT[group={mqttNode.Group},tagId={mqttNode.TagId}]");
            }
            else if (_dict.TryGetValue(input, out var entry))
            {
                if (blinkMs > 0)
                {
                    var cmdOn = CommandService.FormatCommand(entry.Layer, entry.Index, rgb.R, rgb.G, rgb.B);
                    var cmdOff = CommandService.FormatCommand(entry.Layer, entry.Index, 0, 0, 0);
                    AppendLog($"接口 SetBlink(key={input},color={color},ms={blinkMs})->ON {cmdOn} / OFF {cmdOff}");
                }
                else
                {
                    var cmd = CommandService.FormatCommand(entry.Layer, entry.Index, rgb.R, rgb.G, rgb.B);
                    AppendLog($"接口 SetLight(key={input},color={color})->{cmd}");
                }
            }
            else
            {
                var mode = blinkMs > 0 ? $"SetBlink(ms={blinkMs})" : "SetLight";
                AppendLog($"接口 {mode}(key={input},color={color})");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"发送失败：{ex.Message}", error: true);
        }
        txtBarcode.Clear();
        txtBarcode.Focus();
    }

    // ── 映射管理 ──────────────────────────────────────────────────────────────

    private void btnMapping_Click(object sender, EventArgs e)
    {
        using Form form = new MappingForm();
        form.ShowDialog(this);
        ReloadDict(); // 关闭后重新加载字典
    }

    // ── 矩阵测试 ──────────────────────────────────────────────────────────────

    private void btnMatrixTest_Click(object sender, EventArgs e)
    {
        if (!PTLController.IsConnected)
        {
            AppendLog("请先连接再进行矩阵测试", error: true);
            return;
        }
        using Form form = new MatrixTestForm();
        form.ShowDialog(this);
    }

    private void btnMqttMapping_Click(object? sender, EventArgs e)
    {
        using Form form = new MqttMappingForm();
        form.ShowDialog(this);
        ReloadDict();
    }

    private void btnMqttTest_Click(object? sender, EventArgs e)
    {
        if (!PTLController.IsConnected)
        {
            AppendLog("请先连接再进行MQTT测试", error: true);
            return;
        }
        using Form form = new MqttTestForm();
        form.ShowDialog(this);
    }

    // ── 关闭 ──────────────────────────────────────────────────────────────────

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        if (_connectionLogHandler != null) PTLController.ConnectionChanged -= _connectionLogHandler;
        if (_tagEventLogHandler != null) PTLController.TagEventReceived -= _tagEventLogHandler;
        try { PTLController.Disconnect(); } catch { }
    }

    // ── 工具方法 ──────────────────────────────────────────────────────────────

    private void ReloadDict()
    {
        var config = ConfigService.Load();
        _dict = ConfigService.BuildDict(config);
        _mqttDict = ConfigService.BuildMqttNodeDict(ConfigService.LoadMqttMapping());
    }

    private void SetupModeSwitchUi()
    {
        _lblMode.Text = "模式：";
        _lblMode.AutoSize = true;
        _lblMode.Margin = new Padding(0, 6, 4, 0);
        _lblMode.ForeColor = Color.White;

        _cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbMode.Width = 92;
        _cmbMode.Items.Clear();
        _cmbMode.Items.AddRange(new object[] { "serial", "mqtt" });
        _cmbMode.SelectedIndexChanged += OnModeSelectionChanged;

        _btnSaveConnConfig.Text = "保存";
        _btnSaveConnConfig.Size = new Size(72, 26);
        _btnSaveConnConfig.Click += (_, __) =>
        {
            SaveStartupConfigFromUi();
            AppendLog($"已保存启动配置：mode={_startup.ConnectionMode}");
        };

        _btnAbout.Text = "关于";
        _btnAbout.Size = new Size(64, 26);
        _btnAbout.Click += (_, __) =>
        {
            using var form = new AboutForm();
            form.ShowDialog(this);
        };

        _lblLogLevel.Text = "日志：";
        _lblLogLevel.AutoSize = true;
        _lblLogLevel.Margin = new Padding(10, 6, 4, 0);
        _lblLogLevel.ForeColor = Color.White;

        _cmbLogLevel.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbLogLevel.Width = 80;
        _cmbLogLevel.Items.Clear();
        _cmbLogLevel.Items.AddRange(new object[] { "Off", "Info", "Debug" });
        _cmbLogLevel.SelectedIndex = 1;

        _modePanel.AutoSize = true;
        _modePanel.WrapContents = false;
        _modePanel.FlowDirection = FlowDirection.LeftToRight;
        _modePanel.BackColor = Color.Transparent;
        _modePanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _modePanel.Margin = new Padding(0);
        _modePanel.Controls.Clear();
        _modePanel.Controls.Add(_lblMode);
        _modePanel.Controls.Add(_cmbMode);
        _modePanel.Controls.Add(_btnSaveConnConfig);
        _modePanel.Controls.Add(_lblLogLevel);
        _modePanel.Controls.Add(_cmbLogLevel);
        _modePanel.Controls.Add(_btnAbout);
    }

    private void SetupBeepUi()
    {
        _lblBeepMode.Text = "蜂鸣：";
        _lblBeepMode.AutoSize = true;
        _lblBeepMode.Margin = new Padding(0, 8, 4, 0);

        _cmbBeepMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbBeepMode.Width = 96;
        _cmbBeepMode.Items.Clear();
        _cmbBeepMode.Items.AddRange(new object[] { "关闭", "常鸣", "闪鸣" });
        _cmbBeepMode.SelectedIndex = 0;
        _cmbBeepMode.Margin = new Padding(0, 4, 8, 0);
    }

    private string GetSelectedBeepMode()
    {
        return (_cmbBeepMode.SelectedItem?.ToString() ?? "关闭").Trim();
    }

    private bool? ResolveBeepFlag(int blinkMs)
    {
        var mode = GetSelectedBeepMode();
        if (mode == "关闭") return false;
        if (mode == "常鸣") return true;

        // 协议里 task 的 Beep 是 Bool，闪鸣只能跟随灯光闪烁语义。
        if (blinkMs <= 0)
            AppendLog("蜂鸣=闪鸣 需要配合闪烁灯效；当前按常鸣下发。");
        return true;
    }

    private void SetupConnectionEditorUi()
    {
        pnlPort.WrapContents = false;
        pnlPort.FlowDirection = FlowDirection.TopDown;
        pnlPort.AutoSize = false;
        pnlPort.Height = 210;
        pnlPort.Padding = new Padding(0);
        pnlPort.Margin = new Padding(0, 0, 0, 6);

        _lblMqttHost.Text = "MQTT IP:";
        _lblMqttPort.Text = "端口:";
        _lblMqttUser.Text = "用户:";
        _lblMqttPwd.Text = "密码:";
        _lblMqttId.Text = "eStationId:";
        foreach (var lbl in new[] { _lblMqttHost, _lblMqttPort, _lblMqttUser, _lblMqttPwd, _lblMqttId })
        {
            lbl.AutoSize = true;
            lbl.Anchor = AnchorStyles.Left;
            lbl.Margin = new Padding(8, 3, 4, 0);
        }

        _txtMqttHost.Width = 120;
        _txtMqttPort.Width = 60;
        _txtMqttUser.Width = 90;
        _txtMqttPwd.Width = 90;
        _txtMqttPwd.UseSystemPasswordChar = true;
        _txtMqttId.Width = 120;

        var serialRow1 = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 4)
        };
        serialRow1.Controls.Add(lblPort);
        serialRow1.Controls.Add(cmbPorts);
        serialRow1.Controls.Add(btnConnect);

        var serialRow2 = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };
        serialRow2.Controls.Add(btnMapping);
        serialRow2.Controls.Add(btnMatrixTest);

        var serialRoot = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            Dock = DockStyle.Fill,
            Margin = new Padding(8)
        };
        serialRoot.Controls.Add(serialRow1);
        serialRoot.Controls.Add(serialRow2);

        _btnMqttConnect.Text = "连接";
        _btnMqttConnect.Size = new Size(72, 26);
        _btnMqttConnect.Click += btnMqttConnect_Click;

        _btnMqttMapping.Text = "MQTT映射";
        _btnMqttMapping.Size = new Size(100, 30);
        _btnMqttMapping.Click += btnMqttMapping_Click;

        _btnMqttTest.Text = "MQTT测试";
        _btnMqttTest.Size = new Size(100, 30);
        _btnMqttTest.Click += btnMqttTest_Click;

        var mqttRow1 = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 4)
        };
        mqttRow1.Controls.Add(_lblMqttHost);
        mqttRow1.Controls.Add(_txtMqttHost);
        mqttRow1.Controls.Add(_lblMqttPort);
        mqttRow1.Controls.Add(_txtMqttPort);
        mqttRow1.Controls.Add(_lblMqttUser);
        mqttRow1.Controls.Add(_txtMqttUser);
        mqttRow1.Controls.Add(_lblMqttPwd);
        mqttRow1.Controls.Add(_txtMqttPwd);
        mqttRow1.Controls.Add(_lblMqttId);
        mqttRow1.Controls.Add(_txtMqttId);

        var mqttRow2 = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 4)
        };
        mqttRow2.Controls.Add(_btnMqttConnect);

        var mqttRow3 = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };
        mqttRow3.Controls.Add(_btnMqttMapping);
        mqttRow3.Controls.Add(_btnMqttTest);

        var mqttRoot = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            Dock = DockStyle.Fill,
            Margin = new Padding(8)
        };
        mqttRoot.Controls.Add(mqttRow1);
        mqttRoot.Controls.Add(mqttRow2);
        mqttRoot.Controls.Add(mqttRow3);

        _tabTransport.Dock = DockStyle.None;
        _tabTransport.Height = 160;
        _tabTransport.SelectedIndexChanged += OnTransportTabChanged;

        _tabSerial.Controls.Clear();
        _tabSerial.Controls.Add(serialRoot);
        _tabMqtt.Controls.Clear();
        _tabMqtt.Controls.Add(mqttRoot);
        _tabTransport.TabPages.Clear();
        _tabTransport.TabPages.Add(_tabSerial);
        _tabTransport.TabPages.Add(_tabMqtt);

        _modeHost.Height = 34;
        _modeHost.Margin = new Padding(0, 0, 0, 6);
        _modeHost.Padding = new Padding(0);
        _modeHost.Controls.Clear();
        _modeHost.Controls.Add(_modePanel);
        _modeHost.SizeChanged -= ModeHost_SizeChanged;
        _modeHost.SizeChanged += ModeHost_SizeChanged;

        pnlPort.Controls.Clear();
        pnlPort.Controls.Add(_modeHost);
        pnlPort.Controls.Add(_tabTransport);
        pnlPort.SizeChanged -= PnlPort_SizeChanged;
        pnlPort.SizeChanged += PnlPort_SizeChanged;
    }

    private void LoadStartupConfigToUi()
    {
        _startup = ConfigService.LoadStartup();
        var startupMode = string.Equals(_startup.ConnectionMode, "mqtt", StringComparison.OrdinalIgnoreCase) ? "mqtt" : "serial";
        SetModeUi(startupMode);
        _activeMode = startupMode;

        _txtMqttHost.Text = _startup.Mqtt.Broker;
        _txtMqttPort.Text = _startup.Mqtt.Port.ToString();
        _txtMqttUser.Text = _startup.Mqtt.Username;
        _txtMqttPwd.Text = _startup.Mqtt.Password;
        _txtMqttId.Text = _startup.Mqtt.EStationId;
        _cmbLogLevel.SelectedItem = string.Equals(_startup.LogLevel, "Off", StringComparison.OrdinalIgnoreCase)
            ? "Off"
            : string.Equals(_startup.LogLevel, "Debug", StringComparison.OrdinalIgnoreCase)
                ? "Debug"
                : "Info";

        if (!string.IsNullOrWhiteSpace(_startup.Serial.PortName))
        {
            var idx = cmbPorts.FindStringExact(_startup.Serial.PortName);
            if (idx >= 0) cmbPorts.SelectedIndex = idx;
        }
    }

    private void SaveStartupConfigFromUi()
    {
        CaptureStartupFromUi();
        ConfigService.SaveStartup(_startup);
    }

    private void CaptureStartupFromUi()
    {
        _startup.ConnectionMode = _activeMode;
        _startup.Serial ??= new SerialStartupConfig();
        _startup.Mqtt ??= new MqttStartupConfig();
        _startup.WirelessDefaults ??= new WirelessDefaultsConfig();
        _startup.Serial.PortName = cmbPorts.SelectedItem?.ToString() ?? string.Empty;
        _startup.Mqtt.Broker = _txtMqttHost.Text.Trim();
        if (!int.TryParse(_txtMqttPort.Text.Trim(), out var port)) port = 2026;
        _startup.Mqtt.Port = port;
        _startup.Mqtt.Username = _txtMqttUser.Text.Trim();
        _startup.Mqtt.Password = _txtMqttPwd.Text;
        _startup.Mqtt.EStationId = _txtMqttId.Text.Trim();
        _startup.LogLevel = (_cmbLogLevel.SelectedItem?.ToString() ?? "Info").Trim();
    }

    private bool IsMqttMode()
    {
        return string.Equals(_activeMode, "mqtt", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyModeUi()
    {
        var isMqtt = IsMqttMode();
        _startup.ConnectionMode = _activeMode;
        btnConnect.Enabled = !isMqtt;
        _btnMqttConnect.Enabled = isMqtt;
        SetConnectedState(PTLController.IsConnected);
    }

    private void SetModeUi(string mode)
    {
        _suppressModeSelectionChanged = true;
        _suppressTabSelectionChanged = true;
        _cmbMode.SelectedItem = mode;
        _tabTransport.SelectedTab = mode == "mqtt" ? _tabMqtt : _tabSerial;
        _suppressModeSelectionChanged = false;
        _suppressTabSelectionChanged = false;
    }

    private async Task SwitchModeAsync(string nextMode)
    {
        if (string.Equals(nextMode, _activeMode, StringComparison.OrdinalIgnoreCase))
        {
            SetModeUi(_activeMode);
            ApplyModeUi();
            return;
        }

        if (PTLController.IsConnected)
        {
            var dr = MessageBox.Show(
                "切换连接模式前需要先断开当前连接，是否继续？",
                "切换模式确认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dr != DialogResult.Yes)
            {
                SetModeUi(_activeMode);
                return;
            }

            btnConnect.Enabled = false;
            _btnMqttConnect.Enabled = false;
            try
            {
                await Task.Run(() => PTLController.Disconnect());
                btnConnect.Text = "连接";
                _btnMqttConnect.Text = "连接";
                SetConnectedState(false);
                AppendLog("已断开当前连接，准备切换模式");
            }
            catch (Exception ex)
            {
                AppendLog("断开失败，模式未切换：" + ex.Message, true);
                SetModeUi(_activeMode);
                btnConnect.Enabled = true;
                _btnMqttConnect.Enabled = true;
                return;
            }
            finally
            {
                btnConnect.Enabled = true;
                _btnMqttConnect.Enabled = true;
            }
        }

        _activeMode = nextMode;
        SetModeUi(_activeMode);
        ApplyModeUi();
        AppendLog("已切换模式（本次运行）：" + _activeMode);
    }

    private async void OnModeSelectionChanged(object? sender, EventArgs e)
    {
        if (_isInitializingUi || _suppressModeSelectionChanged)
            return;

        var nextMode = (_cmbMode.SelectedItem?.ToString() ?? "serial").ToLowerInvariant();
        await SwitchModeAsync(nextMode);
    }

    private async void OnTransportTabChanged(object? sender, EventArgs e)
    {
        if (_isInitializingUi || _suppressTabSelectionChanged)
            return;

        SetConnectedState(PTLController.IsConnected);
        if ((_tabTransport.SelectedTab == _tabMqtt ? "mqtt" : "serial") != _activeMode)
            AppendLog("当前为只读预览页；请使用右上角“模式”下拉框切换运行模式。");
    }

    private void ArrangeMainButtons()
    {
        pnlLights.WrapContents = true;
        pnlLights.FlowDirection = FlowDirection.LeftToRight;
        pnlLights.AutoSize = true;
        pnlTools.WrapContents = true;
        pnlTools.FlowDirection = FlowDirection.LeftToRight;
        pnlTools.AutoSize = true;

        pnlLights.Controls.Clear();
        pnlLights.Controls.Add(_lblBeepMode);
        pnlLights.Controls.Add(_cmbBeepMode);
        pnlLights.Controls.Add(btnSendGreen);
        pnlLights.Controls.Add(btnSendRedBlink);
        pnlLights.Controls.Add(btnSendGreenBlink);
        pnlLights.Controls.Add(btnOff);
        pnlLights.Controls.Add(btnMarquee);

        pnlTools.Visible = false;
        pnlTools.Height = 0;

        var actionButtons = new[] { btnSendGreen, btnSendRedBlink, btnSendGreenBlink, btnOff, btnMarquee, btnMapping, btnMatrixTest, _btnMqttMapping, _btnMqttTest };
        foreach (var b in actionButtons)
        {
            b.Width = 118;
            b.Height = 32;
            b.Margin = new Padding(0, 0, 8, 6);
        }
        _cmbBeepMode.Height = 30;
    }

    private void PnlPort_SizeChanged(object? sender, EventArgs e)
    {
        RefreshPortLayout();
    }

    private void ModeHost_SizeChanged(object? sender, EventArgs e)
    {
        _modePanel.Location = new Point(
            Math.Max(0, _modeHost.ClientSize.Width - _modePanel.Width),
            Math.Max(0, (_modeHost.ClientSize.Height - _modePanel.Height) / 2));
    }

    private void RefreshPortLayout()
    {
        var w = Math.Max(420, pnlPort.ClientSize.Width - 8);
        _modeHost.Width = w;
        _tabTransport.Size = new Size(w, 160);
        ModeHost_SizeChanged(this, EventArgs.Empty);
    }

    private void WireRealtimeEventLogs()
    {
        _connectionLogHandler = (_, e) =>
        {
            var mode = string.IsNullOrWhiteSpace(e.TransportType) ? "unknown" : e.TransportType;
            AppendLogSafe($"连接事件[{mode}] => {(e.IsConnected ? "已连接" : "已断开")} {e.Message}");
        };
        _tagEventLogHandler = (_, e) =>
        {
            var eventName = e.EventType switch
            {
                CompatTagEventType.Button => "按键",
                CompatTagEventType.Communication => "通信",
                CompatTagEventType.Heartbeat => "心跳",
                _ => "未知"
            };
            var key = PTLController.GetKeyByTagId(e.TagId, e.Group);
            if (string.IsNullOrWhiteSpace(key))
                key = "?";
            AppendLogSafe($"回传[{eventName}] key={key}, tagId={e.TagId}, group={e.Group}, color={DescribeColor(e.R, e.G, e.B)}, off={e.IsOff}, battery={e.BatteryVoltage:0.0}V");
        };

        PTLController.ConnectionChanged += _connectionLogHandler;
        PTLController.TagEventReceived += _tagEventLogHandler;
    }

    private static string DescribeColor(bool r, bool g, bool b)
    {
        if (!r && !g && !b) return "Off";
        if (r && !g && !b) return "Red";
        if (!r && g && !b) return "Green";
        if (!r && !g && b) return "Blue";
        if (r && g && !b) return "Yellow/Orange";
        if (!r && g && b) return "Cyan";
        if (r && !g && b) return "Purple";
        if (r && g && b) return "White";
        return "Unknown";
    }

    private void SetupLogCopyUi()
    {
        lstLog.SelectionMode = SelectionMode.MultiExtended;
        lstLog.KeyDown += LstLog_KeyDown;

        var miCopySelected = new ToolStripMenuItem("复制选中");
        miCopySelected.Click += (_, __) => CopySelectedLogs();
        var miCopyAll = new ToolStripMenuItem("复制全部");
        miCopyAll.Click += (_, __) =>
        {
            var all = lstLog.Items.Cast<object>().Select(x => x?.ToString() ?? string.Empty).ToArray();
            if (all.Length > 0)
                Clipboard.SetText(string.Join(Environment.NewLine, all));
        };
        _logMenu.Items.Add(miCopySelected);
        _logMenu.Items.Add(miCopyAll);
        lstLog.ContextMenuStrip = _logMenu;
    }

    private void LstLog_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.C)
        {
            CopySelectedLogs();
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.A)
        {
            for (int i = 0; i < lstLog.Items.Count; i++)
                lstLog.SetSelected(i, true);
            e.Handled = true;
        }
    }

    private void CopySelectedLogs()
    {
        if (lstLog.SelectedItems.Count <= 0)
            return;

        var lines = new List<string>();
        foreach (var item in lstLog.SelectedItems)
            lines.Add(item?.ToString() ?? string.Empty);

        Clipboard.SetText(string.Join(Environment.NewLine, lines));
    }

    private void AppendLog(string msg, bool error = false)
    {
        var item = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        int idx = lstLog.Items.Add(item);
        // 用 DrawItem 事件处理颜色，这里先存颜色信息
        lstLog.TopIndex = lstLog.Items.Count - 1;

        if (error)
        {
            // 通过 Tag 标记错误行索引
            _errorLines.Add(idx);
            lstLog.Invalidate();
        }
    }

    private void AppendLogSafe(string msg, bool error = false)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AppendLog(msg, error)));
            return;
        }
        AppendLog(msg, error);
    }

    private readonly System.Collections.Generic.HashSet<int> _errorLines = new();

    private void lstLog_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();
        var text = lstLog.Items[e.Index]?.ToString() ?? "";
        var color = _errorLines.Contains(e.Index) ? Color.Red : Color.Black;
        e.Graphics.DrawString(text, e.Font!, new SolidBrush(color), e.Bounds);
        e.DrawFocusRectangle();
    }
}
