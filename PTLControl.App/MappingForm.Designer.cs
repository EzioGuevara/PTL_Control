namespace PTLControl;

partial class MappingForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        splitMain = new SplitContainer();
        lvRows = new ListView();
        colRowNo = new ColumnHeader();
        colLayer = new ColumnHeader();
        colCellCount = new ColumnHeader();
        pnlRowButtons = new Panel();
        lblColsHint = new Label();
        nudCols = new NumericUpDown();
        btnAddRow = new Button();
        btnDeleteRow = new Button();
        lblRows = new Label();
        dgvCells = new DataGridView();
        colNo = new DataGridViewTextBoxColumn();
        colKey = new DataGridViewTextBoxColumn();
        colAlias = new DataGridViewTextBoxColumn();
        colIndex = new DataGridViewTextBoxColumn();
        pnlCellButtons = new Panel();
        lblPrefixHint = new Label();
        cmbPrefix = new ComboBox();
        btnAutoKey = new Button();
        lblStartHint = new Label();
        nudStart = new NumericUpDown();
        lblStepHint = new Label();
        nudStep = new NumericUpDown();
        btnAutoIndex = new Button();
        btnAddCell = new Button();
        btnDeleteCell = new Button();
        btnSave = new Button();
        lblGridTitle = new Label();
        ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
        splitMain.Panel1.SuspendLayout();
        splitMain.Panel2.SuspendLayout();
        splitMain.SuspendLayout();
        pnlRowButtons.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)nudCols).BeginInit();
        ((System.ComponentModel.ISupportInitialize)dgvCells).BeginInit();
        pnlCellButtons.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)nudStart).BeginInit();
        ((System.ComponentModel.ISupportInitialize)nudStep).BeginInit();
        SuspendLayout();
        // 
        // splitMain
        // 
        splitMain.Dock = DockStyle.Fill;
        splitMain.Location = new Point(0, 0);
        splitMain.Name = "splitMain";
        // 
        // splitMain.Panel1
        // 
        splitMain.Panel1.Controls.Add(lvRows);
        splitMain.Panel1.Controls.Add(pnlRowButtons);
        splitMain.Panel1.Controls.Add(lblRows);
        splitMain.Panel1MinSize = 200;
        // 
        // splitMain.Panel2
        // 
        splitMain.Panel2.Controls.Add(dgvCells);
        splitMain.Panel2.Controls.Add(pnlCellButtons);
        splitMain.Panel2.Controls.Add(lblGridTitle);
        splitMain.Panel2MinSize = 200;
        splitMain.Size = new Size(800, 520);
        splitMain.SplitterDistance = 400;
        splitMain.TabIndex = 0;
        // 
        // lvRows
        // 
        lvRows.Columns.AddRange(new ColumnHeader[] { colRowNo, colLayer, colCellCount });
        lvRows.Dock = DockStyle.Fill;
        lvRows.FullRowSelect = true;
        lvRows.Location = new Point(0, 27);
        lvRows.MultiSelect = false;
        lvRows.Name = "lvRows";
        lvRows.Size = new Size(400, 455);
        lvRows.TabIndex = 0;
        lvRows.UseCompatibleStateImageBehavior = false;
        lvRows.View = View.Details;
        lvRows.SelectedIndexChanged += lvRows_SelectedIndexChanged;
        // 
        // colRowNo
        // 
        colRowNo.Text = "#";
        colRowNo.Width = 36;
        // 
        // colLayer
        // 
        colLayer.Text = "Layer";
        // 
        // colCellCount
        // 
        colCellCount.Text = "点位数";
        // 
        // pnlRowButtons
        // 
        pnlRowButtons.Controls.Add(lblColsHint);
        pnlRowButtons.Controls.Add(nudCols);
        pnlRowButtons.Controls.Add(btnAddRow);
        pnlRowButtons.Controls.Add(btnDeleteRow);
        pnlRowButtons.Dock = DockStyle.Bottom;
        pnlRowButtons.Location = new Point(0, 482);
        pnlRowButtons.Name = "pnlRowButtons";
        pnlRowButtons.Padding = new Padding(4);
        pnlRowButtons.Size = new Size(400, 38);
        pnlRowButtons.TabIndex = 1;
        // 
        // lblColsHint
        // 
        lblColsHint.AutoSize = true;
        lblColsHint.Location = new Point(4, 8);
        lblColsHint.Name = "lblColsHint";
        lblColsHint.Size = new Size(35, 17);
        lblColsHint.TabIndex = 0;
        lblColsHint.Text = "列数:";
        // 
        // nudCols
        // 
        nudCols.Location = new Point(46, 6);
        nudCols.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
        nudCols.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        nudCols.Name = "nudCols";
        nudCols.Size = new Size(60, 23);
        nudCols.TabIndex = 1;
        nudCols.Value = new decimal(new int[] { 10, 0, 0, 0 });
        nudCols.ValueChanged += nudCols_ValueChanged;
        // 
        // btnAddRow
        // 
        btnAddRow.Location = new Point(114, 6);
        btnAddRow.Name = "btnAddRow";
        btnAddRow.Size = new Size(68, 26);
        btnAddRow.TabIndex = 2;
        btnAddRow.Text = "添加行";
        btnAddRow.Click += btnAddRow_Click;
        // 
        // btnDeleteRow
        // 
        btnDeleteRow.Location = new Point(188, 6);
        btnDeleteRow.Name = "btnDeleteRow";
        btnDeleteRow.Size = new Size(68, 26);
        btnDeleteRow.TabIndex = 3;
        btnDeleteRow.Text = "删除行";
        btnDeleteRow.Click += btnDeleteRow_Click;
        // 
        // lblRows
        // 
        lblRows.AutoSize = true;
        lblRows.Dock = DockStyle.Top;
        lblRows.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
        lblRows.Location = new Point(0, 0);
        lblRows.Name = "lblRows";
        lblRows.Padding = new Padding(4, 6, 0, 4);
        lblRows.Size = new Size(60, 27);
        lblRows.TabIndex = 2;
        lblRows.Text = "行列结构";
        // 
        // dgvCells
        // 
        dgvCells.AllowUserToAddRows = false;
        dgvCells.AllowUserToDeleteRows = false;
        dgvCells.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvCells.Columns.AddRange(new DataGridViewColumn[] { colNo, colKey, colAlias, colIndex });
        dgvCells.Dock = DockStyle.Fill;
        dgvCells.Location = new Point(0, 27);
        dgvCells.Name = "dgvCells";
        dgvCells.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvCells.Size = new Size(396, 385);
        dgvCells.TabIndex = 0;
        dgvCells.CellEndEdit += dgvCells_CellEndEdit;
        // 
        // colNo
        // 
        colNo.FillWeight = 12F;
        colNo.HeaderText = "#";
        colNo.Name = "colNo";
        colNo.ReadOnly = true;
        // 
        // colKey
        // 
        colKey.FillWeight = 30F;
        colKey.HeaderText = "物料 Key";
        colKey.Name = "colKey";
        // 
        // colAlias
        // 
        colAlias.FillWeight = 40F;
        colAlias.HeaderText = "别名(物料号)";
        colAlias.Name = "colAlias";
        // 
        // colIndex
        // 
        colIndex.FillWeight = 18F;
        colIndex.HeaderText = "LED Index";
        colIndex.Name = "colIndex";
        // 
        // pnlCellButtons
        // 
        pnlCellButtons.Controls.Add(lblPrefixHint);
        pnlCellButtons.Controls.Add(cmbPrefix);
        pnlCellButtons.Controls.Add(btnAutoKey);
        pnlCellButtons.Controls.Add(lblStartHint);
        pnlCellButtons.Controls.Add(nudStart);
        pnlCellButtons.Controls.Add(lblStepHint);
        pnlCellButtons.Controls.Add(nudStep);
        pnlCellButtons.Controls.Add(btnAutoIndex);
        pnlCellButtons.Controls.Add(btnAddCell);
        pnlCellButtons.Controls.Add(btnDeleteCell);
        pnlCellButtons.Controls.Add(btnSave);
        pnlCellButtons.Dock = DockStyle.Bottom;
        pnlCellButtons.Location = new Point(0, 412);
        pnlCellButtons.Name = "pnlCellButtons";
        pnlCellButtons.Padding = new Padding(4);
        pnlCellButtons.Size = new Size(396, 108);
        pnlCellButtons.TabIndex = 1;
        // 
        // lblPrefixHint
        // 
        lblPrefixHint.AutoSize = true;
        lblPrefixHint.Location = new Point(4, 8);
        lblPrefixHint.Name = "lblPrefixHint";
        lblPrefixHint.Size = new Size(56, 17);
        lblPrefixHint.TabIndex = 0;
        lblPrefixHint.Text = "Key前缀:";
        // 
        // cmbPrefix
        // 
        cmbPrefix.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbPrefix.Location = new Point(62, 6);
        cmbPrefix.Name = "cmbPrefix";        cmbPrefix.Size = new Size(55, 25);
        cmbPrefix.TabIndex = 1;
        // 
        // btnAutoKey
        // 
        btnAutoKey.BackColor = Color.LightSkyBlue;
        btnAutoKey.Location = new Point(124, 5);
        btnAutoKey.Name = "btnAutoKey";
        btnAutoKey.Size = new Size(110, 26);
        btnAutoKey.TabIndex = 2;
        btnAutoKey.Text = "自动生成 Key";
        btnAutoKey.UseVisualStyleBackColor = false;
        btnAutoKey.Click += btnAutoKey_Click;
        // 
        // lblStartHint
        // 
        lblStartHint.AutoSize = true;
        lblStartHint.Location = new Point(4, 42);
        lblStartHint.Name = "lblStartHint";
        lblStartHint.Size = new Size(35, 17);
        lblStartHint.TabIndex = 3;
        lblStartHint.Text = "起始:";
        // 
        // nudStart
        // 
        nudStart.Location = new Point(42, 39);
        nudStart.Maximum = new decimal(new int[] { 9999, 0, 0, 0 });
        nudStart.Minimum = new decimal(new int[] { 0, 0, 0, 0 });
        nudStart.Name = "nudStart";
        nudStart.Size = new Size(55, 23);
        nudStart.TabIndex = 4;
        nudStart.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // lblStepHint
        // 
        lblStepHint.AutoSize = true;
        lblStepHint.Location = new Point(104, 42);
        lblStepHint.Name = "lblStepHint";
        lblStepHint.Size = new Size(35, 17);
        lblStepHint.TabIndex = 5;
        lblStepHint.Text = "间距:";
        // 
        // nudStep
        // 
        nudStep.Location = new Point(140, 39);
        nudStep.Maximum = new decimal(new int[] { 999, 0, 0, 0 });
        nudStep.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        nudStep.Name = "nudStep";
        nudStep.Size = new Size(55, 23);
        nudStep.TabIndex = 6;
        nudStep.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // btnAutoIndex
        // 
        btnAutoIndex.BackColor = Color.LightSalmon;
        btnAutoIndex.Location = new Point(202, 38);
        btnAutoIndex.Name = "btnAutoIndex";
        btnAutoIndex.Size = new Size(110, 26);
        btnAutoIndex.TabIndex = 7;
        btnAutoIndex.Text = "自动生成 Index";
        btnAutoIndex.UseVisualStyleBackColor = false;
        btnAutoIndex.Click += btnAutoIndex_Click;
        // 
        // btnAddCell
        // 
        btnAddCell.Location = new Point(4, 74);
        btnAddCell.Name = "btnAddCell";
        btnAddCell.Size = new Size(80, 26);
        btnAddCell.TabIndex = 8;
        btnAddCell.Text = "添加点位";
        btnAddCell.Click += btnAddCell_Click;
        // 
        // btnDeleteCell
        // 
        btnDeleteCell.Location = new Point(90, 74);
        btnDeleteCell.Name = "btnDeleteCell";
        btnDeleteCell.Size = new Size(80, 26);
        btnDeleteCell.TabIndex = 9;
        btnDeleteCell.Text = "删除点位";
        btnDeleteCell.Click += btnDeleteCell_Click;
        // 
        // btnSave
        // 
        btnSave.BackColor = Color.LightGreen;
        btnSave.Location = new Point(176, 74);
        btnSave.Name = "btnSave";
        btnSave.Size = new Size(80, 26);
        btnSave.TabIndex = 10;
        btnSave.Text = "保存配置";
        btnSave.UseVisualStyleBackColor = false;
        btnSave.Click += btnSave_Click;
        // 
        // lblGridTitle
        // 
        lblGridTitle.AutoSize = true;
        lblGridTitle.Dock = DockStyle.Top;
        lblGridTitle.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
        lblGridTitle.Location = new Point(0, 0);
        lblGridTitle.Name = "lblGridTitle";
        lblGridTitle.Padding = new Padding(4, 6, 0, 4);
        lblGridTitle.Size = new Size(108, 27);
        lblGridTitle.TabIndex = 2;
        lblGridTitle.Text = "请在左侧选择一行";
        // 
        // MappingForm
        // 
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(800, 520);
        Controls.Add(splitMain);
        MinimumSize = new Size(640, 420);
        Name = "MappingForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "映射管理 - IDEMIA";
        Load += MappingForm_Load;
        splitMain.Panel1.ResumeLayout(false);
        splitMain.Panel1.PerformLayout();
        splitMain.Panel2.ResumeLayout(false);
        splitMain.Panel2.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
        splitMain.ResumeLayout(false);
        pnlRowButtons.ResumeLayout(false);
        pnlRowButtons.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)nudCols).EndInit();
        ((System.ComponentModel.ISupportInitialize)dgvCells).EndInit();
        pnlCellButtons.ResumeLayout(false);
        pnlCellButtons.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)nudStart).EndInit();
        ((System.ComponentModel.ISupportInitialize)nudStep).EndInit();
        ResumeLayout(false);
    }

    private System.Windows.Forms.SplitContainer             splitMain;
    private System.Windows.Forms.Label                      lblRows;
    private System.Windows.Forms.ListView                   lvRows;
    private System.Windows.Forms.ColumnHeader               colRowNo;
    private System.Windows.Forms.ColumnHeader               colLayer;
    private System.Windows.Forms.ColumnHeader               colCellCount;
    private System.Windows.Forms.Panel                      pnlRowButtons;
    private System.Windows.Forms.Label                      lblColsHint;
    private System.Windows.Forms.NumericUpDown              nudCols;
    private System.Windows.Forms.Button                     btnAddRow;
    private System.Windows.Forms.Button                     btnDeleteRow;
    private System.Windows.Forms.Label                      lblGridTitle;
    private System.Windows.Forms.DataGridView               dgvCells;
    private System.Windows.Forms.DataGridViewTextBoxColumn  colNo;
    private System.Windows.Forms.DataGridViewTextBoxColumn  colKey;
    private System.Windows.Forms.DataGridViewTextBoxColumn  colAlias;
    private System.Windows.Forms.DataGridViewTextBoxColumn  colIndex;
    private System.Windows.Forms.Panel                      pnlCellButtons;
    private System.Windows.Forms.Label                      lblPrefixHint;
    private System.Windows.Forms.ComboBox                   cmbPrefix;
    private System.Windows.Forms.Button                     btnAutoKey;
    private System.Windows.Forms.Label                      lblStartHint;
    private System.Windows.Forms.NumericUpDown              nudStart;
    private System.Windows.Forms.Label                      lblStepHint;
    private System.Windows.Forms.NumericUpDown              nudStep;
    private System.Windows.Forms.Button                     btnAutoIndex;
    private System.Windows.Forms.Button                     btnAddCell;
    private System.Windows.Forms.Button                     btnDeleteCell;
    private System.Windows.Forms.Button                     btnSave;
}
