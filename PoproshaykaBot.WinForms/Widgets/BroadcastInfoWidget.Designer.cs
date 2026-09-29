namespace PoproshaykaBot.WinForms.Widgets;

sealed partial class BroadcastInfoWidget
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
        _mainTableLayoutPanel = new TableLayoutPanel();
        _statusLabel = new Label();
        _modeLabel = new Label();
        _sentCountLabel = new Label();
        _nextTimeLabel = new Label();
        _mainTableLayoutPanel.SuspendLayout();
        SuspendLayout();
        //
        // _mainTableLayoutPanel
        //
        _mainTableLayoutPanel.ColumnCount = 1;
        _mainTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Controls.Add(_statusLabel, 0, 0);
        _mainTableLayoutPanel.Controls.Add(_modeLabel, 0, 1);
        _mainTableLayoutPanel.Controls.Add(_sentCountLabel, 0, 2);
        _mainTableLayoutPanel.Controls.Add(_nextTimeLabel, 0, 3);
        _mainTableLayoutPanel.Dock = DockStyle.Fill;
        _mainTableLayoutPanel.Location = new Point(0, 0);
        _mainTableLayoutPanel.Name = "_mainTableLayoutPanel";
        _mainTableLayoutPanel.RowCount = 5;
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Size = new Size(254, 150);
        _mainTableLayoutPanel.TabIndex = 0;
        //
        // _statusLabel
        //
        _statusLabel.AutoSize = true;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _statusLabel.Location = new Point(3, 0);
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new Size(248, 32);
        _statusLabel.TabIndex = 0;
        _statusLabel.Text = "Бот не подключен";
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _modeLabel
        //
        _modeLabel.AutoSize = true;
        _modeLabel.Dock = DockStyle.Fill;
        _modeLabel.Location = new Point(3, 32);
        _modeLabel.Name = "_modeLabel";
        _modeLabel.Size = new Size(248, 28);
        _modeLabel.TabIndex = 1;
        _modeLabel.Text = "Режим: Ручной";
        _modeLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _sentCountLabel
        //
        _sentCountLabel.AutoSize = true;
        _sentCountLabel.Dock = DockStyle.Fill;
        _sentCountLabel.Location = new Point(3, 60);
        _sentCountLabel.Name = "_sentCountLabel";
        _sentCountLabel.Size = new Size(248, 28);
        _sentCountLabel.TabIndex = 2;
        _sentCountLabel.Text = "Отправлено: 0";
        _sentCountLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _nextTimeLabel
        //
        _nextTimeLabel.AutoSize = true;
        _nextTimeLabel.Dock = DockStyle.Fill;
        _nextTimeLabel.Location = new Point(3, 88);
        _nextTimeLabel.Name = "_nextTimeLabel";
        _nextTimeLabel.Size = new Size(248, 28);
        _nextTimeLabel.TabIndex = 3;
        _nextTimeLabel.Text = "Следующая: –";
        _nextTimeLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // BroadcastInfoWidget
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_mainTableLayoutPanel);
        Name = "BroadcastInfoWidget";
        Size = new Size(254, 150);
        _mainTableLayoutPanel.ResumeLayout(false);
        _mainTableLayoutPanel.PerformLayout();
        ResumeLayout(false);
    }

    private Label _statusLabel;
    private Label _modeLabel;
    private Label _sentCountLabel;
    private Label _nextTimeLabel;
    private TableLayoutPanel _mainTableLayoutPanel;
}
