namespace PoproshaykaBot.WinForms.Widgets;

sealed partial class StreamInfoWidget
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            ClearThumbnail();
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _mainTableLayoutPanel = new TableLayoutPanel();
        _thumbnailPictureBox = new PictureBox();
        _infoTableLayoutPanel = new TableLayoutPanel();
        _statusHeaderTableLayoutPanel = new TableLayoutPanel();
        _statusIconLabel = new Label();
        _statusTextLabel = new Label();
        _titleLabel = new Label();
        _gameLabel = new Label();
        _statsTableLayoutPanel = new TableLayoutPanel();
        _viewersLabel = new Label();
        _uptimeLabel = new Label();
        _lastUpdateLabel = new Label();
        _streamInfoTimer = new System.Windows.Forms.Timer(components);
        _mainTableLayoutPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_thumbnailPictureBox).BeginInit();
        _infoTableLayoutPanel.SuspendLayout();
        _statusHeaderTableLayoutPanel.SuspendLayout();
        _statsTableLayoutPanel.SuspendLayout();
        SuspendLayout();
        //
        // _mainTableLayoutPanel
        //
        _mainTableLayoutPanel.ColumnCount = 2;
        _mainTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240F));
        _mainTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Controls.Add(_thumbnailPictureBox, 0, 0);
        _mainTableLayoutPanel.Controls.Add(_infoTableLayoutPanel, 1, 0);
        _mainTableLayoutPanel.Controls.Add(_statsTableLayoutPanel, 0, 1);
        _mainTableLayoutPanel.Dock = DockStyle.Fill;
        _mainTableLayoutPanel.Location = new Point(0, 0);
        _mainTableLayoutPanel.Name = "_mainTableLayoutPanel";
        _mainTableLayoutPanel.RowCount = 2;
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _mainTableLayoutPanel.SetColumnSpan(_statsTableLayoutPanel, 2);
        _mainTableLayoutPanel.Size = new Size(420, 200);
        _mainTableLayoutPanel.TabIndex = 0;
        //
        // _thumbnailPictureBox
        //
        _thumbnailPictureBox.Anchor = AnchorStyles.None;
        _thumbnailPictureBox.BackColor = Color.FromArgb(28, 28, 28);
        _thumbnailPictureBox.Margin = new Padding(3);
        _thumbnailPictureBox.Name = "_thumbnailPictureBox";
        _thumbnailPictureBox.Size = new Size(234, 134);
        _thumbnailPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _thumbnailPictureBox.TabIndex = 0;
        _thumbnailPictureBox.TabStop = false;
        //
        // _infoTableLayoutPanel
        //
        _infoTableLayoutPanel.ColumnCount = 1;
        _infoTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _infoTableLayoutPanel.Controls.Add(_statusHeaderTableLayoutPanel, 0, 1);
        _infoTableLayoutPanel.Controls.Add(_titleLabel, 0, 2);
        _infoTableLayoutPanel.Controls.Add(_gameLabel, 0, 3);
        _infoTableLayoutPanel.Dock = DockStyle.Fill;
        _infoTableLayoutPanel.Margin = new Padding(3);
        _infoTableLayoutPanel.Name = "_infoTableLayoutPanel";
        _infoTableLayoutPanel.RowCount = 5;
        _infoTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        _infoTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _infoTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        _infoTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        _infoTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        _infoTableLayoutPanel.TabIndex = 1;
        //
        // _statusHeaderTableLayoutPanel
        //
        _statusHeaderTableLayoutPanel.ColumnCount = 2;
        _statusHeaderTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32F));
        _statusHeaderTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _statusHeaderTableLayoutPanel.Controls.Add(_statusIconLabel, 0, 0);
        _statusHeaderTableLayoutPanel.Controls.Add(_statusTextLabel, 1, 0);
        _statusHeaderTableLayoutPanel.Dock = DockStyle.Fill;
        _statusHeaderTableLayoutPanel.Margin = new Padding(0);
        _statusHeaderTableLayoutPanel.Name = "_statusHeaderTableLayoutPanel";
        _statusHeaderTableLayoutPanel.RowCount = 1;
        _statusHeaderTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _statusHeaderTableLayoutPanel.TabIndex = 0;
        //
        // _statusIconLabel
        //
        _statusIconLabel.Dock = DockStyle.Fill;
        _statusIconLabel.Font = new Font("Segoe UI Emoji", 12F, FontStyle.Bold);
        _statusIconLabel.Name = "_statusIconLabel";
        _statusIconLabel.TabIndex = 0;
        _statusIconLabel.Text = "⚪";
        _statusIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _statusTextLabel
        //
        _statusTextLabel.Dock = DockStyle.Fill;
        _statusTextLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _statusTextLabel.Name = "_statusTextLabel";
        _statusTextLabel.TabIndex = 1;
        _statusTextLabel.Text = "НЕИЗВЕСТНО";
        _statusTextLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _titleLabel
        //
        _titleLabel.AutoEllipsis = true;
        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _titleLabel.Name = "_titleLabel";
        _titleLabel.TabIndex = 1;
        _titleLabel.Text = "Статус неизвестен";
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _gameLabel
        //
        _gameLabel.AutoEllipsis = true;
        _gameLabel.Dock = DockStyle.Fill;
        _gameLabel.ForeColor = Color.DimGray;
        _gameLabel.Name = "_gameLabel";
        _gameLabel.TabIndex = 2;
        _gameLabel.Text = "–";
        _gameLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _statsTableLayoutPanel
        //
        _statsTableLayoutPanel.BackColor = Color.FromArgb(245, 245, 245);
        _statsTableLayoutPanel.ColumnCount = 3;
        _statsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
        _statsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
        _statsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
        _statsTableLayoutPanel.Controls.Add(_viewersLabel, 0, 0);
        _statsTableLayoutPanel.Controls.Add(_uptimeLabel, 1, 0);
        _statsTableLayoutPanel.Controls.Add(_lastUpdateLabel, 2, 0);
        _statsTableLayoutPanel.Dock = DockStyle.Fill;
        _statsTableLayoutPanel.Margin = new Padding(0);
        _statsTableLayoutPanel.Name = "_statsTableLayoutPanel";
        _statsTableLayoutPanel.RowCount = 1;
        _statsTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _statsTableLayoutPanel.TabIndex = 2;
        //
        // _viewersLabel
        //
        _viewersLabel.Dock = DockStyle.Fill;
        _viewersLabel.Name = "_viewersLabel";
        _viewersLabel.TabIndex = 0;
        _viewersLabel.Text = "👥 –";
        _viewersLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _uptimeLabel
        //
        _uptimeLabel.Dock = DockStyle.Fill;
        _uptimeLabel.Name = "_uptimeLabel";
        _uptimeLabel.TabIndex = 1;
        _uptimeLabel.Text = "⏱️ –";
        _uptimeLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _lastUpdateLabel
        //
        _lastUpdateLabel.Dock = DockStyle.Fill;
        _lastUpdateLabel.Font = new Font("Segoe UI", 8F);
        _lastUpdateLabel.ForeColor = Color.Gray;
        _lastUpdateLabel.Name = "_lastUpdateLabel";
        _lastUpdateLabel.TabIndex = 2;
        _lastUpdateLabel.Text = "🕐 –";
        _lastUpdateLabel.TextAlign = ContentAlignment.MiddleRight;
        //
        // _streamInfoTimer
        //
        _streamInfoTimer.Interval = 15000;
        _streamInfoTimer.Tick += OnStreamInfoTimerTick;
        //
        // StreamInfoWidget
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_mainTableLayoutPanel);
        Name = "StreamInfoWidget";
        Size = new Size(420, 200);
        _mainTableLayoutPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_thumbnailPictureBox).EndInit();
        _infoTableLayoutPanel.ResumeLayout(false);
        _statusHeaderTableLayoutPanel.ResumeLayout(false);
        _statsTableLayoutPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    private PictureBox _thumbnailPictureBox;
    private TableLayoutPanel _statusHeaderTableLayoutPanel;
    private TableLayoutPanel _infoTableLayoutPanel;
    private TableLayoutPanel _statsTableLayoutPanel;
    private Label _lastUpdateLabel;
    private Label _statusIconLabel;
    private Label _statusTextLabel;
    private Label _titleLabel;
    private Label _gameLabel;
    private Label _viewersLabel;
    private Label _uptimeLabel;
    private TableLayoutPanel _mainTableLayoutPanel;
    private System.Windows.Forms.Timer _streamInfoTimer;
}
