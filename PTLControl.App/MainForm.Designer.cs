namespace PTLControl;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        pnlBrand = new Panel();
        picLogo = new PictureBox();
        lblBrand = new Label();
        tableMain = new TableLayoutPanel();
        pnlPort = new FlowLayoutPanel();
        lblPort = new Label();
        cmbPorts = new ComboBox();
        btnConnect = new Button();
        lblBarcode = new Label();
        pnlBarcode = new TableLayoutPanel();
        txtBarcode = new TextBox();
        btnSend = new Button();
        pnlLights = new FlowLayoutPanel();
        btnOff = new Button();
        btnSendGreen = new Button();
        btnSendRedBlink = new Button();
        btnSendGreenBlink = new Button();
        btnMarquee = new Button();
        pnlTools = new FlowLayoutPanel();
        btnMapping = new Button();
        btnMatrixTest = new Button();
        pnlLog = new TableLayoutPanel();
        lblLog = new Label();
        lstLog = new ListBox();
        pnlBrand.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)picLogo).BeginInit();
        tableMain.SuspendLayout();
        pnlPort.SuspendLayout();
        pnlBarcode.SuspendLayout();
        pnlLights.SuspendLayout();
        pnlTools.SuspendLayout();
        pnlLog.SuspendLayout();
        SuspendLayout();
        // 
        // pnlBrand
        // 
        pnlBrand.BackColor = Color.FromArgb(67, 0, 153);
        pnlBrand.Controls.Add(picLogo);
        pnlBrand.Controls.Add(lblBrand);
        pnlBrand.Dock = DockStyle.Top;
        pnlBrand.Location = new Point(0, 0);
        pnlBrand.Name = "pnlBrand";
        pnlBrand.Padding = new Padding(8, 4, 8, 4);
        pnlBrand.Size = new Size(660, 48);
        pnlBrand.TabIndex = 1;
        // 
        // picLogo
        // 
        picLogo.BackColor = Color.Transparent;
        picLogo.Location = new Point(8, 4);
        picLogo.Name = "picLogo";
        picLogo.Size = new Size(40, 40);
        picLogo.SizeMode = PictureBoxSizeMode.Zoom;
        picLogo.TabIndex = 0;
        picLogo.TabStop = false;
        // 
        // lblBrand
        // 
        lblBrand.AutoSize = true;
        lblBrand.BackColor = Color.Transparent;
        lblBrand.Font = new Font("微软雅黑", 14F, FontStyle.Bold);
        lblBrand.ForeColor = Color.White;
        lblBrand.Location = new Point(56, 10);
        lblBrand.Name = "lblBrand";
        lblBrand.Size = new Size(235, 26);
        lblBrand.TabIndex = 1;
        lblBrand.Text = "PTL LED Matrix Control";
        // 
        // tableMain
        // 
        tableMain.ColumnCount = 1;
        tableMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableMain.Controls.Add(pnlPort, 0, 0);
        tableMain.Controls.Add(lblBarcode, 0, 1);
        tableMain.Controls.Add(pnlBarcode, 0, 2);
        tableMain.Controls.Add(pnlLights, 0, 3);
        tableMain.Controls.Add(pnlTools, 0, 4);
        tableMain.Controls.Add(pnlLog, 0, 5);
        tableMain.Dock = DockStyle.Fill;
        tableMain.Location = new Point(0, 48);
        tableMain.Name = "tableMain";
        tableMain.Padding = new Padding(10);
        tableMain.RowCount = 6;
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableMain.Size = new Size(660, 492);
        tableMain.TabIndex = 0;
        // 
        // pnlPort
        // 
        pnlPort.AutoSize = true;
        pnlPort.Controls.Add(lblPort);
        pnlPort.Controls.Add(cmbPorts);
        pnlPort.Controls.Add(btnConnect);
        pnlPort.Dock = DockStyle.Fill;
        pnlPort.Location = new Point(10, 10);
        pnlPort.Margin = new Padding(0, 0, 0, 6);
        pnlPort.Name = "pnlPort";
        pnlPort.Size = new Size(640, 26);
        pnlPort.TabIndex = 0;
        pnlPort.WrapContents = false;
        // 
        // lblPort
        // 
        lblPort.Anchor = AnchorStyles.Left;
        lblPort.AutoSize = true;
        lblPort.Location = new Point(0, 6);
        lblPort.Margin = new Padding(0, 3, 6, 0);
        lblPort.Name = "lblPort";
        lblPort.Size = new Size(44, 17);
        lblPort.TabIndex = 0;
        lblPort.Text = "串口：";
        // 
        // cmbPorts
        // 
        cmbPorts.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbPorts.Location = new Point(50, 0);
        cmbPorts.Margin = new Padding(0, 0, 6, 0);
        cmbPorts.Name = "cmbPorts";
        cmbPorts.Size = new Size(150, 25);
        cmbPorts.TabIndex = 1;
        // 
        // btnConnect
        // 
        btnConnect.Location = new Point(206, 0);
        btnConnect.Margin = new Padding(0);
        btnConnect.Name = "btnConnect";
        btnConnect.Size = new Size(72, 26);
        btnConnect.TabIndex = 2;
        btnConnect.Text = "连接";
        btnConnect.Click += btnConnect_Click;
        // 
        // lblBarcode
        // 
        lblBarcode.AutoSize = true;
        lblBarcode.Dock = DockStyle.Fill;
        lblBarcode.Location = new Point(13, 42);
        lblBarcode.Name = "lblBarcode";
        lblBarcode.Padding = new Padding(0, 4, 0, 2);
        lblBarcode.Size = new Size(634, 23);
        lblBarcode.TabIndex = 1;
        lblBarcode.Text = "扫码输入：";
        // 
        // pnlBarcode
        // 
        pnlBarcode.AutoSize = true;
        pnlBarcode.ColumnCount = 2;
        pnlBarcode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pnlBarcode.ColumnStyles.Add(new ColumnStyle());
        pnlBarcode.Controls.Add(txtBarcode, 0, 0);
        pnlBarcode.Controls.Add(btnSend, 1, 0);
        pnlBarcode.Dock = DockStyle.Fill;
        pnlBarcode.Location = new Point(10, 65);
        pnlBarcode.Margin = new Padding(0, 0, 0, 8);
        pnlBarcode.Name = "pnlBarcode";
        pnlBarcode.RowCount = 1;
        pnlBarcode.RowStyles.Add(new RowStyle());
        pnlBarcode.Size = new Size(640, 42);
        pnlBarcode.TabIndex = 2;
        // 
        // txtBarcode
        // 
        txtBarcode.Dock = DockStyle.Fill;
        txtBarcode.Font = new Font("微软雅黑", 16F);
        txtBarcode.Location = new Point(3, 3);
        txtBarcode.Name = "txtBarcode";
        txtBarcode.Size = new Size(538, 36);
        txtBarcode.TabIndex = 0;
        txtBarcode.KeyDown += txtBarcode_KeyDown;
        // 
        // btnSend
        // 
        btnSend.Enabled = false;
        btnSend.Font = new Font("微软雅黑", 10F);
        btnSend.Location = new Point(550, 0);
        btnSend.Margin = new Padding(6, 0, 0, 0);
        btnSend.Name = "btnSend";
        btnSend.Size = new Size(90, 38);
        btnSend.TabIndex = 1;
        btnSend.Text = "查询发送";
        btnSend.Click += btnSend_Click;
        // 
        // pnlLights
        // 
        pnlLights.AutoSize = true;
        pnlLights.Controls.Add(btnOff);
        pnlLights.Controls.Add(btnSendGreen);
        pnlLights.Controls.Add(btnSendRedBlink);
        pnlLights.Controls.Add(btnSendGreenBlink);
        pnlLights.Controls.Add(btnMarquee);
        pnlLights.Dock = DockStyle.Fill;
        pnlLights.Location = new Point(10, 115);
        pnlLights.Margin = new Padding(0, 0, 0, 4);
        pnlLights.Name = "pnlLights";
        pnlLights.Size = new Size(640, 30);
        pnlLights.TabIndex = 3;
        pnlLights.WrapContents = false;
        // 
        // btnOff
        // 
        btnOff.BackColor = Color.LightCoral;
        btnOff.Enabled = false;
        btnOff.Location = new Point(0, 0);
        btnOff.Margin = new Padding(0, 0, 8, 0);
        btnOff.Name = "btnOff";
        btnOff.Size = new Size(100, 30);
        btnOff.TabIndex = 0;
        btnOff.Text = "全局熄灭";
        btnOff.UseVisualStyleBackColor = false;
        btnOff.Click += btnOff_Click;
        // 
        // btnSendGreen
        // 
        btnSendGreen.BackColor = Color.LightGreen;
        btnSendGreen.Enabled = false;
        btnSendGreen.Location = new Point(108, 0);
        btnSendGreen.Margin = new Padding(0, 0, 8, 0);
        btnSendGreen.Name = "btnSendGreen";
        btnSendGreen.Size = new Size(110, 30);
        btnSendGreen.TabIndex = 1;
        btnSendGreen.Text = "查询（绿色）";
        btnSendGreen.UseVisualStyleBackColor = false;
        btnSendGreen.Click += btnSendGreen_Click;
        // 
        // btnSendRedBlink
        // 
        btnSendRedBlink.BackColor = Color.LightCoral;
        btnSendRedBlink.Enabled = false;
        btnSendRedBlink.Location = new Point(226, 0);
        btnSendRedBlink.Margin = new Padding(0, 0, 8, 0);
        btnSendRedBlink.Name = "btnSendRedBlink";
        btnSendRedBlink.Size = new Size(130, 30);
        btnSendRedBlink.TabIndex = 2;
        btnSendRedBlink.Text = "查询（红色闪烁）";
        btnSendRedBlink.UseVisualStyleBackColor = false;
        btnSendRedBlink.Click += btnSendRedBlink_Click;
        // 
        // btnSendGreenBlink
        // 
        btnSendGreenBlink.BackColor = Color.LightGreen;
        btnSendGreenBlink.Enabled = false;
        btnSendGreenBlink.Location = new Point(364, 0);
        btnSendGreenBlink.Margin = new Padding(0);
        btnSendGreenBlink.Name = "btnSendGreenBlink";
        btnSendGreenBlink.Size = new Size(130, 30);
        btnSendGreenBlink.TabIndex = 3;
        btnSendGreenBlink.Text = "查询（绿色闪烁）";
        btnSendGreenBlink.UseVisualStyleBackColor = false;
        btnSendGreenBlink.Click += btnSendGreenBlink_Click;
        // 
        // btnMarquee
        // 
        btnMarquee.BackColor = Color.LightSkyBlue;
        btnMarquee.Enabled = false;
        btnMarquee.Location = new Point(494, 0);
        btnMarquee.Margin = new Padding(0);
        btnMarquee.Name = "btnMarquee";
        btnMarquee.Size = new Size(90, 30);
        btnMarquee.TabIndex = 4;
        btnMarquee.Text = "跑马灯";
        btnMarquee.UseVisualStyleBackColor = false;
        btnMarquee.Click += btnMarquee_Click;
        // 
        // pnlTools
        // 
        pnlTools.AutoSize = true;
        pnlTools.Controls.Add(btnMapping);
        pnlTools.Controls.Add(btnMatrixTest);
        pnlTools.Dock = DockStyle.Fill;
        pnlTools.Location = new Point(10, 149);
        pnlTools.Margin = new Padding(0, 0, 0, 8);
        pnlTools.Name = "pnlTools";
        pnlTools.Size = new Size(640, 30);
        pnlTools.TabIndex = 4;
        pnlTools.WrapContents = false;
        // 
        // btnMapping
        // 
        btnMapping.Location = new Point(0, 0);
        btnMapping.Margin = new Padding(0, 0, 8, 0);
        btnMapping.Name = "btnMapping";
        btnMapping.Size = new Size(100, 30);
        btnMapping.TabIndex = 0;
        btnMapping.Text = "映射管理";
        btnMapping.Click += btnMapping_Click;
        // 
        // btnMatrixTest
        // 
        btnMatrixTest.Enabled = false;
        btnMatrixTest.Location = new Point(108, 0);
        btnMatrixTest.Margin = new Padding(0);
        btnMatrixTest.Name = "btnMatrixTest";
        btnMatrixTest.Size = new Size(100, 30);
        btnMatrixTest.TabIndex = 1;
        btnMatrixTest.Text = "矩阵测试";
        btnMatrixTest.Click += btnMatrixTest_Click;
        // 
        // pnlLog
        // 
        pnlLog.ColumnCount = 1;
        pnlLog.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pnlLog.Controls.Add(lblLog, 0, 0);
        pnlLog.Controls.Add(lstLog, 0, 1);
        pnlLog.Dock = DockStyle.Fill;
        pnlLog.Location = new Point(10, 187);
        pnlLog.Margin = new Padding(0);
        pnlLog.Name = "pnlLog";
        pnlLog.RowCount = 2;
        pnlLog.RowStyles.Add(new RowStyle());
        pnlLog.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        pnlLog.Size = new Size(640, 295);
        pnlLog.TabIndex = 5;
        // 
        // lblLog
        // 
        lblLog.AutoSize = true;
        lblLog.Dock = DockStyle.Fill;
        lblLog.Location = new Point(3, 0);
        lblLog.Name = "lblLog";
        lblLog.Size = new Size(634, 17);
        lblLog.TabIndex = 0;
        lblLog.Text = "日志：";
        // 
        // lstLog
        // 
        lstLog.Dock = DockStyle.Fill;
        lstLog.DrawMode = DrawMode.OwnerDrawFixed;
        lstLog.Font = new Font("Consolas", 10F);
        lstLog.HorizontalScrollbar = true;
        lstLog.Location = new Point(3, 20);
        lstLog.Name = "lstLog";
        lstLog.Size = new Size(634, 272);
        lstLog.TabIndex = 1;
        lstLog.DrawItem += lstLog_DrawItem;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(660, 540);
        Controls.Add(tableMain);
        Controls.Add(pnlBrand);
        MinimumSize = new Size(500, 460);
        Name = "MainForm";
        Text = "PTL - IDEMIA";
        Load += MainForm_Load;
        pnlBrand.ResumeLayout(false);
        pnlBrand.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)picLogo).EndInit();
        tableMain.ResumeLayout(false);
        tableMain.PerformLayout();
        pnlPort.ResumeLayout(false);
        pnlPort.PerformLayout();
        pnlBarcode.ResumeLayout(false);
        pnlBarcode.PerformLayout();
        pnlLights.ResumeLayout(false);
        pnlTools.ResumeLayout(false);
        pnlLog.ResumeLayout(false);
        pnlLog.PerformLayout();
        ResumeLayout(false);
    }

    private System.Windows.Forms.Panel             pnlBrand;
    private System.Windows.Forms.PictureBox       picLogo;
    private System.Windows.Forms.Label            lblBrand;
    private System.Windows.Forms.TableLayoutPanel tableMain;
    private System.Windows.Forms.FlowLayoutPanel  pnlPort;
    private System.Windows.Forms.Label            lblPort;
    private System.Windows.Forms.ComboBox         cmbPorts;
    private System.Windows.Forms.Button           btnConnect;
    private System.Windows.Forms.Label            lblBarcode;
    private System.Windows.Forms.TableLayoutPanel pnlBarcode;
    private System.Windows.Forms.TextBox          txtBarcode;
    private System.Windows.Forms.Button           btnSend;
    private System.Windows.Forms.FlowLayoutPanel  pnlLights;
    private System.Windows.Forms.Button           btnOff;
    private System.Windows.Forms.Button           btnSendGreen;
    private System.Windows.Forms.Button           btnSendRedBlink;
    private System.Windows.Forms.Button           btnSendGreenBlink;
    private System.Windows.Forms.Button           btnMarquee;
    private System.Windows.Forms.FlowLayoutPanel  pnlTools;
    private System.Windows.Forms.Button           btnMapping;
    private System.Windows.Forms.Button           btnMatrixTest;
    private System.Windows.Forms.TableLayoutPanel pnlLog;
    private System.Windows.Forms.Label            lblLog;
    private System.Windows.Forms.ListBox          lstLog;
}
