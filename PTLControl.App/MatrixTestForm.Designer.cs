namespace PTLControl
{
    partial class MatrixTestForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.tableMain = new System.Windows.Forms.TableLayoutPanel();
            this.pnlButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnCalibrate = new System.Windows.Forms.Button();
            this.btnSaveMapping = new System.Windows.Forms.Button();
            this.btnOffAll = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.pnlMatrixScroll = new System.Windows.Forms.Panel();
            this.pnlMatrix = new System.Windows.Forms.FlowLayoutPanel();
            this.lstLog = new System.Windows.Forms.ListBox();
            this.tableMain.SuspendLayout();
            this.pnlButtons.SuspendLayout();
            this.pnlMatrixScroll.SuspendLayout();
            this.SuspendLayout();
            // tableMain
            this.tableMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableMain.ColumnCount = 1;
            this.tableMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableMain.RowCount = 3;
            this.tableMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tableMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableMain.Padding = new System.Windows.Forms.Padding(8);
            // pnlButtons
            this.pnlButtons.AutoSize = true;
            this.pnlButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlButtons.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.pnlButtons.WrapContents = false;
            this.pnlButtons.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            // btnCalibrate
            this.btnCalibrate.Text = "教授模式";
            this.btnCalibrate.Size = new System.Drawing.Size(90, 30);
            this.btnCalibrate.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btnCalibrate.BackColor = System.Drawing.Color.LightSkyBlue;
            this.btnCalibrate.UseVisualStyleBackColor = false;
            this.btnCalibrate.Click += new System.EventHandler(this.btnCalibrate_Click);
            // btnSaveMapping
            this.btnSaveMapping.Text = "保存映射";
            this.btnSaveMapping.Size = new System.Drawing.Size(90, 30);
            this.btnSaveMapping.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btnSaveMapping.BackColor = System.Drawing.Color.Gold;
            this.btnSaveMapping.UseVisualStyleBackColor = false;
            this.btnSaveMapping.Click += new System.EventHandler(this.btnSaveMapping_Click);
            // btnOffAll
            this.btnOffAll.Text = "全局熄灭";
            this.btnOffAll.Size = new System.Drawing.Size(90, 30);
            this.btnOffAll.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.btnOffAll.BackColor = System.Drawing.Color.Tomato;
            this.btnOffAll.ForeColor = System.Drawing.Color.White;
            this.btnOffAll.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.btnOffAll.UseVisualStyleBackColor = false;
            this.btnOffAll.Click += new System.EventHandler(this.btnOffAll_Click);
            // lblStatus
            this.lblStatus.Text = "点击「生成矩阵」加载配置。";
            this.lblStatus.AutoSize = true;
            this.lblStatus.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblStatus.Margin = new System.Windows.Forms.Padding(4, 0, 0, 0);
            this.pnlButtons.Controls.Add(this.btnCalibrate);
            this.pnlButtons.Controls.Add(this.btnSaveMapping);
            this.pnlButtons.Controls.Add(this.btnOffAll);
            this.pnlButtons.Controls.Add(this.lblStatus);
            // pnlMatrixScroll
            this.pnlMatrixScroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMatrixScroll.AutoScroll = true;
            this.pnlMatrixScroll.Controls.Add(this.pnlMatrix);
            // pnlMatrix
            this.pnlMatrix.AutoSize = true;
            this.pnlMatrix.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.pnlMatrix.WrapContents = false;
            this.pnlMatrix.Padding = new System.Windows.Forms.Padding(4);
            this.pnlMatrix.Location = new System.Drawing.Point(0, 0);
            // lstLog
            this.lstLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstLog.Font = new System.Drawing.Font("Consolas", 9F);
            this.lstLog.HorizontalScrollbar = true;
            this.lstLog.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            // assemble
            this.tableMain.Controls.Add(this.pnlButtons, 0, 0);
            this.tableMain.Controls.Add(this.pnlMatrixScroll, 0, 1);
            this.tableMain.Controls.Add(this.lstLog, 0, 2);
            // MatrixTestForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(960, 560);
            this.MinimumSize = new System.Drawing.Size(700, 400);
            this.Text = "LED 矩阵测试 - IDEMIA";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Load += new System.EventHandler(this.MatrixTestForm_Load);
            this.Controls.Add(this.tableMain);
            this.tableMain.ResumeLayout(false);
            this.pnlButtons.ResumeLayout(false);
            this.pnlButtons.PerformLayout();
            this.pnlMatrixScroll.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.TableLayoutPanel tableMain;
        private System.Windows.Forms.FlowLayoutPanel pnlButtons;
        private System.Windows.Forms.Button btnCalibrate;
        private System.Windows.Forms.Button btnSaveMapping;
        private System.Windows.Forms.Button btnOffAll;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Panel pnlMatrixScroll;
        private System.Windows.Forms.FlowLayoutPanel pnlMatrix;
        private System.Windows.Forms.ListBox lstLog;
    }
}
