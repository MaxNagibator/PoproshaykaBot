namespace PoproshaykaBot.WinForms.Widgets;

sealed partial class ObsOutputStatusCard
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _layoutTableLayoutPanel = new TableLayoutPanel();
        _topLeftLayoutPanel = new TableLayoutPanel();
        _kickerLabel = new Label();
        _stateRowLayoutPanel = new TableLayoutPanel();
        _dotPanel = new Panel();
        _stateLabel = new Label();
        _topRightLayoutPanel = new TableLayoutPanel();
        _kickerRightLabel = new Label();
        _timecodeLabel = new Label();
        _metaLabel = new Label();
        _primaryButton = new Button();
        _secondaryWrapPanel = new Panel();
        _secondaryButton = new Button();
        _secondaryChipLabel = new Label();
        _pulseTimer = new System.Windows.Forms.Timer(components);
        _layoutTableLayoutPanel.SuspendLayout();
        _topLeftLayoutPanel.SuspendLayout();
        _stateRowLayoutPanel.SuspendLayout();
        _topRightLayoutPanel.SuspendLayout();
        _secondaryWrapPanel.SuspendLayout();
        SuspendLayout();
        //
        // _layoutTableLayoutPanel
        //
        _layoutTableLayoutPanel.ColumnCount = 2;
        _layoutTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _layoutTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _layoutTableLayoutPanel.Controls.Add(_topLeftLayoutPanel, 0, 0);
        _layoutTableLayoutPanel.Controls.Add(_topRightLayoutPanel, 1, 0);
        _layoutTableLayoutPanel.Controls.Add(_primaryButton, 0, 1);
        _layoutTableLayoutPanel.Controls.Add(_secondaryWrapPanel, 1, 1);
        _layoutTableLayoutPanel.Dock = DockStyle.Fill;
        _layoutTableLayoutPanel.Location = new Point(2, 2);
        _layoutTableLayoutPanel.Margin = new Padding(0);
        _layoutTableLayoutPanel.Name = "_layoutTableLayoutPanel";
        _layoutTableLayoutPanel.Padding = new Padding(4);
        _layoutTableLayoutPanel.RowCount = 2;
        _layoutTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        _layoutTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        _layoutTableLayoutPanel.Size = new Size(176, 156);
        _layoutTableLayoutPanel.TabIndex = 0;
        //
        // _topLeftLayoutPanel
        //
        _topLeftLayoutPanel.ColumnCount = 1;
        _topLeftLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _topLeftLayoutPanel.Controls.Add(_kickerLabel, 0, 0);
        _topLeftLayoutPanel.Controls.Add(_stateRowLayoutPanel, 0, 1);
        _topLeftLayoutPanel.Dock = DockStyle.Fill;
        _topLeftLayoutPanel.Location = new Point(7, 7);
        _topLeftLayoutPanel.Margin = new Padding(3, 3, 3, 3);
        _topLeftLayoutPanel.Name = "_topLeftLayoutPanel";
        _topLeftLayoutPanel.RowCount = 2;
        _topLeftLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 16F));
        _topLeftLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _topLeftLayoutPanel.Size = new Size(78, 68);
        _topLeftLayoutPanel.TabIndex = 0;
        //
        // _kickerLabel
        //
        _kickerLabel.AutoEllipsis = true;
        _kickerLabel.Dock = DockStyle.Fill;
        _kickerLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        _kickerLabel.ForeColor = Color.Gray;
        _kickerLabel.Location = new Point(0, 0);
        _kickerLabel.Margin = new Padding(0);
        _kickerLabel.Name = "_kickerLabel";
        _kickerLabel.Size = new Size(78, 16);
        _kickerLabel.TabIndex = 0;
        _kickerLabel.Text = "СТАТУС";
        _kickerLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _stateRowLayoutPanel
        //
        _stateRowLayoutPanel.ColumnCount = 2;
        _stateRowLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18F));
        _stateRowLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _stateRowLayoutPanel.Controls.Add(_dotPanel, 0, 0);
        _stateRowLayoutPanel.Controls.Add(_stateLabel, 1, 0);
        _stateRowLayoutPanel.Dock = DockStyle.Fill;
        _stateRowLayoutPanel.Location = new Point(0, 16);
        _stateRowLayoutPanel.Margin = new Padding(0);
        _stateRowLayoutPanel.Name = "_stateRowLayoutPanel";
        _stateRowLayoutPanel.RowCount = 1;
        _stateRowLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _stateRowLayoutPanel.Size = new Size(78, 52);
        _stateRowLayoutPanel.TabIndex = 1;
        //
        // _dotPanel
        //
        _dotPanel.BackColor = Color.Transparent;
        _dotPanel.Dock = DockStyle.Fill;
        _dotPanel.Location = new Point(0, 0);
        _dotPanel.Margin = new Padding(0);
        _dotPanel.Name = "_dotPanel";
        _dotPanel.Size = new Size(18, 52);
        _dotPanel.TabIndex = 0;
        _dotPanel.Paint += OnDotPanelPaint;
        //
        // _stateLabel
        //
        _stateLabel.AutoEllipsis = true;
        _stateLabel.Dock = DockStyle.Fill;
        _stateLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _stateLabel.ForeColor = Color.Gray;
        _stateLabel.Location = new Point(18, 0);
        _stateLabel.Margin = new Padding(0);
        _stateLabel.Name = "_stateLabel";
        _stateLabel.Size = new Size(60, 52);
        _stateLabel.TabIndex = 1;
        _stateLabel.Text = "–";
        _stateLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _topRightLayoutPanel
        //
        _topRightLayoutPanel.ColumnCount = 1;
        _topRightLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _topRightLayoutPanel.Controls.Add(_kickerRightLabel, 0, 0);
        _topRightLayoutPanel.Controls.Add(_timecodeLabel, 0, 1);
        _topRightLayoutPanel.Controls.Add(_metaLabel, 0, 2);
        _topRightLayoutPanel.Dock = DockStyle.Fill;
        _topRightLayoutPanel.Location = new Point(91, 7);
        _topRightLayoutPanel.Margin = new Padding(3, 3, 3, 3);
        _topRightLayoutPanel.Name = "_topRightLayoutPanel";
        _topRightLayoutPanel.RowCount = 3;
        _topRightLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 16F));
        _topRightLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _topRightLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
        _topRightLayoutPanel.Size = new Size(78, 68);
        _topRightLayoutPanel.TabIndex = 1;
        //
        // _kickerRightLabel
        //
        _kickerRightLabel.AutoEllipsis = true;
        _kickerRightLabel.Dock = DockStyle.Fill;
        _kickerRightLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        _kickerRightLabel.ForeColor = Color.Gray;
        _kickerRightLabel.Location = new Point(0, 0);
        _kickerRightLabel.Margin = new Padding(0);
        _kickerRightLabel.Name = "_kickerRightLabel";
        _kickerRightLabel.Size = new Size(78, 16);
        _kickerRightLabel.TabIndex = 0;
        _kickerRightLabel.Text = "ТАЙМЕР";
        _kickerRightLabel.TextAlign = ContentAlignment.MiddleRight;
        //
        // _timecodeLabel
        //
        _timecodeLabel.AutoEllipsis = true;
        _timecodeLabel.Dock = DockStyle.Fill;
        _timecodeLabel.Font = new Font("Consolas", 13F, FontStyle.Bold);
        _timecodeLabel.ForeColor = Color.DimGray;
        _timecodeLabel.Location = new Point(0, 16);
        _timecodeLabel.Margin = new Padding(0);
        _timecodeLabel.Name = "_timecodeLabel";
        _timecodeLabel.Size = new Size(78, 38);
        _timecodeLabel.TabIndex = 1;
        _timecodeLabel.Text = "–:–:–";
        _timecodeLabel.TextAlign = ContentAlignment.MiddleRight;
        //
        // _metaLabel
        //
        _metaLabel.AutoEllipsis = true;
        _metaLabel.Dock = DockStyle.Fill;
        _metaLabel.Font = new Font("Segoe UI", 7.5F);
        _metaLabel.ForeColor = Color.Gray;
        _metaLabel.Location = new Point(0, 54);
        _metaLabel.Margin = new Padding(0);
        _metaLabel.Name = "_metaLabel";
        _metaLabel.Size = new Size(78, 14);
        _metaLabel.TabIndex = 2;
        _metaLabel.Text = string.Empty;
        _metaLabel.TextAlign = ContentAlignment.MiddleRight;
        //
        // _primaryButton
        //
        _primaryButton.Dock = DockStyle.Fill;
        _primaryButton.FlatAppearance.BorderColor = Color.Silver;
        _primaryButton.FlatAppearance.BorderSize = 1;
        _primaryButton.FlatStyle = FlatStyle.Flat;
        _primaryButton.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold);
        _primaryButton.Location = new Point(7, 81);
        _primaryButton.Margin = new Padding(3, 3, 3, 3);
        _primaryButton.Name = "_primaryButton";
        _primaryButton.Size = new Size(78, 68);
        _primaryButton.TabIndex = 2;
        _primaryButton.Text = "–";
        _primaryButton.UseVisualStyleBackColor = true;
        _primaryButton.Click += OnPrimaryButtonClick;
        //
        // _secondaryWrapPanel
        //
        _secondaryWrapPanel.Controls.Add(_secondaryChipLabel);
        _secondaryWrapPanel.Controls.Add(_secondaryButton);
        _secondaryWrapPanel.Dock = DockStyle.Fill;
        _secondaryWrapPanel.Location = new Point(91, 81);
        _secondaryWrapPanel.Margin = new Padding(3, 3, 3, 3);
        _secondaryWrapPanel.Name = "_secondaryWrapPanel";
        _secondaryWrapPanel.Size = new Size(78, 68);
        _secondaryWrapPanel.TabIndex = 3;
        //
        // _secondaryButton
        //
        _secondaryButton.Dock = DockStyle.Fill;
        _secondaryButton.Enabled = false;
        _secondaryButton.FlatAppearance.BorderColor = Color.Silver;
        _secondaryButton.FlatAppearance.BorderSize = 1;
        _secondaryButton.FlatStyle = FlatStyle.Flat;
        _secondaryButton.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold);
        _secondaryButton.Location = new Point(0, 0);
        _secondaryButton.Margin = new Padding(0);
        _secondaryButton.Name = "_secondaryButton";
        _secondaryButton.Size = new Size(78, 68);
        _secondaryButton.TabIndex = 0;
        _secondaryButton.Text = "–";
        _secondaryButton.UseVisualStyleBackColor = true;
        _secondaryButton.Click += OnSecondaryButtonClick;
        //
        // _secondaryChipLabel
        //
        _secondaryChipLabel.AutoEllipsis = true;
        _secondaryChipLabel.BorderStyle = BorderStyle.FixedSingle;
        _secondaryChipLabel.Dock = DockStyle.Fill;
        _secondaryChipLabel.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold);
        _secondaryChipLabel.ForeColor = Color.Gray;
        _secondaryChipLabel.Location = new Point(0, 0);
        _secondaryChipLabel.Margin = new Padding(0);
        _secondaryChipLabel.Name = "_secondaryChipLabel";
        _secondaryChipLabel.Padding = new Padding(2);
        _secondaryChipLabel.Size = new Size(78, 68);
        _secondaryChipLabel.TabIndex = 1;
        _secondaryChipLabel.Text = "–";
        _secondaryChipLabel.TextAlign = ContentAlignment.MiddleCenter;
        _secondaryChipLabel.Visible = false;
        //
        // _pulseTimer
        //
        _pulseTimer.Interval = 60;
        _pulseTimer.Tick += OnPulseTimerTick;
        //
        // ObsOutputStatusCard
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_layoutTableLayoutPanel);
        DoubleBuffered = true;
        Margin = new Padding(0);
        Name = "ObsOutputStatusCard";
        Padding = new Padding(2);
        Size = new Size(180, 160);
        _layoutTableLayoutPanel.ResumeLayout(false);
        _topLeftLayoutPanel.ResumeLayout(false);
        _stateRowLayoutPanel.ResumeLayout(false);
        _topRightLayoutPanel.ResumeLayout(false);
        _secondaryWrapPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    private TableLayoutPanel _layoutTableLayoutPanel;
    private TableLayoutPanel _topLeftLayoutPanel;
    private Label _kickerLabel;
    private TableLayoutPanel _stateRowLayoutPanel;
    private Panel _dotPanel;
    private Label _stateLabel;
    private TableLayoutPanel _topRightLayoutPanel;
    private Label _kickerRightLabel;
    private Label _timecodeLabel;
    private Label _metaLabel;
    private Button _primaryButton;
    private Panel _secondaryWrapPanel;
    private Button _secondaryButton;
    private Label _secondaryChipLabel;
    private System.Windows.Forms.Timer _pulseTimer;
}
