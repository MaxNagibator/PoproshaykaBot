namespace PoproshaykaBot.WinForms.Forms.Settings;

partial class DashboardSettingsControl
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _mainTableLayoutPanel = new TableLayoutPanel();
        _toolbarFlowLayoutPanel = new FlowLayoutPanel();
        _gridColumnsLabel = new Label();
        _gridColumnsNumeric = new NumericUpDown();
        _gridRowsLabel = new Label();
        _gridRowsNumeric = new NumericUpDown();
        _resetLayoutButton = new Button();
        _clearGridButton = new Button();
        _infoLabel = new Label();
        _editorTableLayoutPanel = new TableLayoutPanel();
        _paletteGroupBox = new GroupBox();
        _paletteFlowLayoutPanel = new FlowLayoutPanel();
        _gridGroupBox = new GroupBox();
        _gridContainerPanel = new Panel();
        _gridLayoutPanel = new TableLayoutPanel();
        _tileContextMenu = new ContextMenuStrip(components);
        _mainTableLayoutPanel.SuspendLayout();
        _toolbarFlowLayoutPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_gridColumnsNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_gridRowsNumeric).BeginInit();
        _editorTableLayoutPanel.SuspendLayout();
        _paletteGroupBox.SuspendLayout();
        _gridGroupBox.SuspendLayout();
        _gridContainerPanel.SuspendLayout();
        SuspendLayout();
        //
        // _mainTableLayoutPanel
        //
        _mainTableLayoutPanel.ColumnCount = 1;
        _mainTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Controls.Add(_toolbarFlowLayoutPanel, 0, 0);
        _mainTableLayoutPanel.Controls.Add(_infoLabel, 0, 1);
        _mainTableLayoutPanel.Controls.Add(_editorTableLayoutPanel, 0, 2);
        _mainTableLayoutPanel.Dock = DockStyle.Fill;
        _mainTableLayoutPanel.Location = new Point(0, 0);
        _mainTableLayoutPanel.Name = "_mainTableLayoutPanel";
        _mainTableLayoutPanel.RowCount = 3;
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle());
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle());
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Size = new Size(593, 529);
        _mainTableLayoutPanel.TabIndex = 0;
        //
        // _toolbarFlowLayoutPanel
        //
        _toolbarFlowLayoutPanel.AutoSize = true;
        _toolbarFlowLayoutPanel.Controls.Add(_gridColumnsLabel);
        _toolbarFlowLayoutPanel.Controls.Add(_gridColumnsNumeric);
        _toolbarFlowLayoutPanel.Controls.Add(_gridRowsLabel);
        _toolbarFlowLayoutPanel.Controls.Add(_gridRowsNumeric);
        _toolbarFlowLayoutPanel.Controls.Add(_resetLayoutButton);
        _toolbarFlowLayoutPanel.Controls.Add(_clearGridButton);
        _toolbarFlowLayoutPanel.Dock = DockStyle.Fill;
        _toolbarFlowLayoutPanel.FlowDirection = FlowDirection.LeftToRight;
        _toolbarFlowLayoutPanel.Location = new Point(3, 3);
        _toolbarFlowLayoutPanel.Margin = new Padding(3, 3, 3, 6);
        _toolbarFlowLayoutPanel.Name = "_toolbarFlowLayoutPanel";
        _toolbarFlowLayoutPanel.Size = new Size(587, 31);
        _toolbarFlowLayoutPanel.TabIndex = 0;
        //
        // _gridColumnsLabel
        //
        _gridColumnsLabel.Anchor = AnchorStyles.Left;
        _gridColumnsLabel.AutoSize = true;
        _gridColumnsLabel.Margin = new Padding(0, 6, 6, 0);
        _gridColumnsLabel.Name = "_gridColumnsLabel";
        _gridColumnsLabel.TabIndex = 0;
        _gridColumnsLabel.Text = "Колонок сетки:";
        //
        // _gridColumnsNumeric
        //
        _gridColumnsNumeric.Anchor = AnchorStyles.Left;
        _gridColumnsNumeric.Margin = new Padding(0, 3, 12, 4);
        _gridColumnsNumeric.Maximum = new decimal(new int[] { 8, 0, 0, 0 });
        _gridColumnsNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _gridColumnsNumeric.Name = "_gridColumnsNumeric";
        _gridColumnsNumeric.Size = new Size(60, 23);
        _gridColumnsNumeric.TabIndex = 1;
        _gridColumnsNumeric.Value = new decimal(new int[] { 4, 0, 0, 0 });
        _gridColumnsNumeric.ValueChanged += OnGridSizeValueChanged;
        //
        // _gridRowsLabel
        //
        _gridRowsLabel.Anchor = AnchorStyles.Left;
        _gridRowsLabel.AutoSize = true;
        _gridRowsLabel.Margin = new Padding(0, 6, 6, 0);
        _gridRowsLabel.Name = "_gridRowsLabel";
        _gridRowsLabel.TabIndex = 2;
        _gridRowsLabel.Text = "Строк:";
        //
        // _gridRowsNumeric
        //
        _gridRowsNumeric.Anchor = AnchorStyles.Left;
        _gridRowsNumeric.Margin = new Padding(0, 3, 12, 4);
        _gridRowsNumeric.Maximum = new decimal(new int[] { 8, 0, 0, 0 });
        _gridRowsNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _gridRowsNumeric.Name = "_gridRowsNumeric";
        _gridRowsNumeric.Size = new Size(60, 23);
        _gridRowsNumeric.TabIndex = 3;
        _gridRowsNumeric.Value = new decimal(new int[] { 3, 0, 0, 0 });
        _gridRowsNumeric.ValueChanged += OnGridSizeValueChanged;
        //
        // _resetLayoutButton
        //
        _resetLayoutButton.AutoSize = true;
        _resetLayoutButton.Margin = new Padding(0, 0, 6, 0);
        _resetLayoutButton.MinimumSize = new Size(0, 28);
        _resetLayoutButton.Name = "_resetLayoutButton";
        _resetLayoutButton.Padding = new Padding(8, 4, 8, 4);
        _resetLayoutButton.TabIndex = 4;
        _resetLayoutButton.Text = "Восстановить раскладку";
        _resetLayoutButton.UseVisualStyleBackColor = true;
        _resetLayoutButton.Click += OnResetLayoutButtonClicked;
        //
        // _clearGridButton
        //
        _clearGridButton.AutoSize = true;
        _clearGridButton.Margin = new Padding(0);
        _clearGridButton.MinimumSize = new Size(0, 28);
        _clearGridButton.Name = "_clearGridButton";
        _clearGridButton.Padding = new Padding(8, 4, 8, 4);
        _clearGridButton.TabIndex = 5;
        _clearGridButton.Text = "Очистить сетку";
        _clearGridButton.UseVisualStyleBackColor = true;
        _clearGridButton.Click += OnClearGridButtonClicked;
        //
        // _infoLabel
        //
        _infoLabel.AutoSize = true;
        _infoLabel.Dock = DockStyle.Fill;
        _infoLabel.ForeColor = SystemColors.GrayText;
        _infoLabel.Location = new Point(3, 43);
        _infoLabel.Margin = new Padding(3, 3, 3, 6);
        _infoLabel.MaximumSize = new Size(580, 0);
        _infoLabel.Name = "_infoLabel";
        _infoLabel.Size = new Size(580, 30);
        _infoLabel.TabIndex = 1;
        _infoLabel.Text = "ℹ️ Перетащите плитку из палитры в нужную ячейку. Перетащите плитку из сетки, чтобы переместить. Правый клик по плитке – меню (размер, удалить).";
        //
        // _editorTableLayoutPanel
        //
        _editorTableLayoutPanel.ColumnCount = 2;
        _editorTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        _editorTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _editorTableLayoutPanel.Controls.Add(_paletteGroupBox, 0, 0);
        _editorTableLayoutPanel.Controls.Add(_gridGroupBox, 1, 0);
        _editorTableLayoutPanel.Dock = DockStyle.Fill;
        _editorTableLayoutPanel.Location = new Point(0, 82);
        _editorTableLayoutPanel.Margin = new Padding(0);
        _editorTableLayoutPanel.Name = "_editorTableLayoutPanel";
        _editorTableLayoutPanel.RowCount = 1;
        _editorTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _editorTableLayoutPanel.Size = new Size(593, 447);
        _editorTableLayoutPanel.TabIndex = 2;
        //
        // _paletteGroupBox
        //
        _paletteGroupBox.Controls.Add(_paletteFlowLayoutPanel);
        _paletteGroupBox.Dock = DockStyle.Fill;
        _paletteGroupBox.Margin = new Padding(0, 0, 6, 0);
        _paletteGroupBox.Name = "_paletteGroupBox";
        _paletteGroupBox.Padding = new Padding(8);
        _paletteGroupBox.TabIndex = 0;
        _paletteGroupBox.TabStop = false;
        _paletteGroupBox.Text = "Палитра";
        //
        // _paletteFlowLayoutPanel
        //
        _paletteFlowLayoutPanel.AutoScroll = true;
        _paletteFlowLayoutPanel.Dock = DockStyle.Fill;
        _paletteFlowLayoutPanel.FlowDirection = FlowDirection.TopDown;
        _paletteFlowLayoutPanel.Margin = new Padding(0);
        _paletteFlowLayoutPanel.Name = "_paletteFlowLayoutPanel";
        _paletteFlowLayoutPanel.TabIndex = 0;
        _paletteFlowLayoutPanel.WrapContents = false;
        //
        // _gridGroupBox
        //
        _gridGroupBox.Controls.Add(_gridContainerPanel);
        _gridGroupBox.Dock = DockStyle.Fill;
        _gridGroupBox.Margin = new Padding(0);
        _gridGroupBox.Name = "_gridGroupBox";
        _gridGroupBox.Padding = new Padding(8);
        _gridGroupBox.TabIndex = 1;
        _gridGroupBox.TabStop = false;
        _gridGroupBox.Text = "Сетка";
        //
        // _gridContainerPanel
        //
        _gridContainerPanel.AutoScroll = true;
        _gridContainerPanel.Controls.Add(_gridLayoutPanel);
        _gridContainerPanel.Dock = DockStyle.Fill;
        _gridContainerPanel.Margin = new Padding(0);
        _gridContainerPanel.Name = "_gridContainerPanel";
        _gridContainerPanel.TabIndex = 0;
        //
        // _gridLayoutPanel
        //
        _gridLayoutPanel.ColumnCount = 4;
        _gridLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        _gridLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        _gridLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        _gridLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        _gridLayoutPanel.Dock = DockStyle.Fill;
        _gridLayoutPanel.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
        _gridLayoutPanel.Margin = new Padding(0);
        _gridLayoutPanel.Name = "_gridLayoutPanel";
        _gridLayoutPanel.RowCount = 3;
        _gridLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        _gridLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        _gridLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        _gridLayoutPanel.TabIndex = 0;
        //
        // _tileContextMenu
        //
        _tileContextMenu.Name = "_tileContextMenu";
        _tileContextMenu.Opening += OnTileContextMenuOpening;
        //
        // DashboardSettingsControl
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_mainTableLayoutPanel);
        Name = "DashboardSettingsControl";
        Size = new Size(593, 529);
        _mainTableLayoutPanel.ResumeLayout(false);
        _mainTableLayoutPanel.PerformLayout();
        _toolbarFlowLayoutPanel.ResumeLayout(false);
        _toolbarFlowLayoutPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_gridColumnsNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)_gridRowsNumeric).EndInit();
        _editorTableLayoutPanel.ResumeLayout(false);
        _paletteGroupBox.ResumeLayout(false);
        _gridGroupBox.ResumeLayout(false);
        _gridContainerPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    private TableLayoutPanel _mainTableLayoutPanel;
    private FlowLayoutPanel _toolbarFlowLayoutPanel;
    private Label _gridColumnsLabel;
    private NumericUpDown _gridColumnsNumeric;
    private Label _gridRowsLabel;
    private NumericUpDown _gridRowsNumeric;
    private Button _resetLayoutButton;
    private Button _clearGridButton;
    private Label _infoLabel;
    private TableLayoutPanel _editorTableLayoutPanel;
    private GroupBox _paletteGroupBox;
    private FlowLayoutPanel _paletteFlowLayoutPanel;
    private GroupBox _gridGroupBox;
    private Panel _gridContainerPanel;
    private TableLayoutPanel _gridLayoutPanel;
    private ContextMenuStrip _tileContextMenu;
}
