namespace PoproshaykaBot.WinForms;

sealed partial class StreamInfoWidget
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            ClearThumbnail();
            _openChannelButton.Click -= OnOpenChannelClick;
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _mainTableLayoutPanel = new TableLayoutPanel();
        _thumbnailPictureBox = new PictureBox();
        _infoTableLayoutPanel = new TableLayoutPanel();
        _statusIconLabel = new Label();
        _statusTextLabel = new Label();
        _titleLabel = new Label();
        _gameLabel = new Label();
        _statsTableLayoutPanel = new TableLayoutPanel();
        _viewersLabel = new Label();
        _uptimeLabel = new Label();
        _lastUpdateLabel = new Label();
        _openChannelButton = new Button();
        _mainTableLayoutPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_thumbnailPictureBox).BeginInit();
        _infoTableLayoutPanel.SuspendLayout();
        _statsTableLayoutPanel.SuspendLayout();
        SuspendLayout();
        // 
        // _mainTableLayoutPanel
        // 
        _mainTableLayoutPanel.ColumnCount = 2;
        _mainTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        _mainTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Controls.Add(_thumbnailPictureBox, 0, 0);
        _mainTableLayoutPanel.Controls.Add(_infoTableLayoutPanel, 1, 0);
        _mainTableLayoutPanel.Dock = DockStyle.Fill;
        _mainTableLayoutPanel.Location = new Point(0, 0);
        _mainTableLayoutPanel.Name = "_mainTableLayoutPanel";
        _mainTableLayoutPanel.Padding = new Padding(5);
        _mainTableLayoutPanel.RowCount = 1;
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Size = new Size(369, 158);
        _mainTableLayoutPanel.TabIndex = 0;
        // 
        // _thumbnailPictureBox
        // 
        _thumbnailPictureBox.BackColor = Color.FromArgb(240, 240, 240);
        _thumbnailPictureBox.Dock = DockStyle.Top;
        _thumbnailPictureBox.Location = new Point(8, 8);
        _thumbnailPictureBox.Name = "_thumbnailPictureBox";
        _thumbnailPictureBox.Size = new Size(134, 75);
        _thumbnailPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _thumbnailPictureBox.TabIndex = 0;
        _thumbnailPictureBox.TabStop = false;
        // 
        // _infoTableLayoutPanel
        // 
        _infoTableLayoutPanel.ColumnCount = 2;
        _infoTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30F));
        _infoTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _infoTableLayoutPanel.Controls.Add(_statusIconLabel, 0, 0);
        _infoTableLayoutPanel.Controls.Add(_statusTextLabel, 1, 0);
        _infoTableLayoutPanel.Controls.Add(_titleLabel, 0, 1);
        _infoTableLayoutPanel.Controls.Add(_gameLabel, 0, 2);
        _infoTableLayoutPanel.Controls.Add(_statsTableLayoutPanel, 0, 3);
        _infoTableLayoutPanel.Controls.Add(_lastUpdateLabel, 0, 4);
        _infoTableLayoutPanel.Dock = DockStyle.Fill;
        _infoTableLayoutPanel.Location = new Point(148, 8);
        _infoTableLayoutPanel.Name = "_infoTableLayoutPanel";
        _infoTableLayoutPanel.RowCount = 5;
        _infoTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        _infoTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        _infoTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        _infoTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        _infoTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _infoTableLayoutPanel.Size = new Size(213, 142);
        _infoTableLayoutPanel.TabIndex = 1;
        // 
        // _statusIconLabel
        // 
        _statusIconLabel.Dock = DockStyle.Fill;
        _statusIconLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _statusIconLabel.Location = new Point(3, 0);
        _statusIconLabel.Name = "_statusIconLabel";
        _statusIconLabel.Size = new Size(24, 25);
        _statusIconLabel.TabIndex = 0;
        _statusIconLabel.Text = "⚪";
        _statusIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // _statusTextLabel
        // 
        _statusTextLabel.Dock = DockStyle.Fill;
        _statusTextLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _statusTextLabel.Location = new Point(33, 0);
        _statusTextLabel.Name = "_statusTextLabel";
        _statusTextLabel.Size = new Size(177, 25);
        _statusTextLabel.TabIndex = 1;
        _statusTextLabel.Text = "UNKNOWN";
        _statusTextLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _titleLabel
        // 
        _titleLabel.AutoEllipsis = true;
        _infoTableLayoutPanel.SetColumnSpan(_titleLabel, 2);
        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _titleLabel.Location = new Point(3, 25);
        _titleLabel.Name = "_titleLabel";
        _titleLabel.Size = new Size(207, 45);
        _titleLabel.TabIndex = 2;
        _titleLabel.Text = "Статус неизвестен";
        // 
        // _gameLabel
        // 
        _gameLabel.AutoEllipsis = true;
        _infoTableLayoutPanel.SetColumnSpan(_gameLabel, 2);
        _gameLabel.Dock = DockStyle.Fill;
        _gameLabel.Font = new Font("Segoe UI", 8.25F);
        _gameLabel.ForeColor = Color.DimGray;
        _gameLabel.Location = new Point(3, 70);
        _gameLabel.Name = "_gameLabel";
        _gameLabel.Size = new Size(207, 20);
        _gameLabel.TabIndex = 3;
        _gameLabel.Text = "—";
        _gameLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _statsTableLayoutPanel
        // 
        _statsTableLayoutPanel.ColumnCount = 2;
        _infoTableLayoutPanel.SetColumnSpan(_statsTableLayoutPanel, 2);
        _statsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _statsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _statsTableLayoutPanel.Controls.Add(_viewersLabel, 0, 0);
        _statsTableLayoutPanel.Controls.Add(_uptimeLabel, 1, 0);
        _statsTableLayoutPanel.Dock = DockStyle.Fill;
        _statsTableLayoutPanel.Location = new Point(0, 90);
        _statsTableLayoutPanel.Margin = new Padding(0);
        _statsTableLayoutPanel.Name = "_statsTableLayoutPanel";
        _statsTableLayoutPanel.RowCount = 1;
        _statsTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _statsTableLayoutPanel.Size = new Size(213, 25);
        _statsTableLayoutPanel.TabIndex = 4;
        // 
        // _viewersLabel
        // 
        _viewersLabel.Dock = DockStyle.Fill;
        _viewersLabel.Font = new Font("Segoe UI", 8.25F);
        _viewersLabel.Location = new Point(3, 0);
        _viewersLabel.Name = "_viewersLabel";
        _viewersLabel.Size = new Size(100, 25);
        _viewersLabel.TabIndex = 0;
        _viewersLabel.Text = "👥 —";
        _viewersLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _uptimeLabel
        // 
        _uptimeLabel.Dock = DockStyle.Fill;
        _uptimeLabel.Font = new Font("Segoe UI", 8.25F);
        _uptimeLabel.Location = new Point(109, 0);
        _uptimeLabel.Name = "_uptimeLabel";
        _uptimeLabel.Size = new Size(101, 25);
        _uptimeLabel.TabIndex = 1;
        _uptimeLabel.Text = "⏱️ —";
        _uptimeLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _lastUpdateLabel
        // 
        _infoTableLayoutPanel.SetColumnSpan(_lastUpdateLabel, 2);
        _lastUpdateLabel.Dock = DockStyle.Fill;
        _lastUpdateLabel.Font = new Font("Segoe UI", 7F);
        _lastUpdateLabel.ForeColor = Color.Gray;
        _lastUpdateLabel.Location = new Point(3, 115);
        _lastUpdateLabel.Name = "_lastUpdateLabel";
        _lastUpdateLabel.Size = new Size(207, 27);
        _lastUpdateLabel.TabIndex = 5;
        _lastUpdateLabel.Text = "Обновлено: никогда";
        _lastUpdateLabel.TextAlign = ContentAlignment.BottomLeft;
        // 
        // _openChannelButton
        // 
        _openChannelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _openChannelButton.FlatStyle = FlatStyle.Flat;
        _openChannelButton.Font = new Font("Segoe UI", 8F);
        _openChannelButton.Location = new Point(8, 118);
        _openChannelButton.Name = "_openChannelButton";
        _openChannelButton.Size = new Size(134, 30);
        _openChannelButton.TabIndex = 1;
        _openChannelButton.Text = "🔗 Открыть Twitch";
        _openChannelButton.UseVisualStyleBackColor = true;
        _openChannelButton.Visible = false;
        // 
        // StreamInfoWidget
        // 
        BackColor = Color.White;
        BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(_openChannelButton);
        Controls.Add(_mainTableLayoutPanel);
        Name = "StreamInfoWidget";
        Size = new Size(371, 160);
        _mainTableLayoutPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_thumbnailPictureBox).EndInit();
        _infoTableLayoutPanel.ResumeLayout(false);
        _statsTableLayoutPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    private PictureBox _thumbnailPictureBox;
    private TableLayoutPanel _infoTableLayoutPanel;
    private TableLayoutPanel _statsTableLayoutPanel;
    private Label _lastUpdateLabel;
    private Button _openChannelButton;
    private Label _statusIconLabel;
    private Label _statusTextLabel;
    private Label _titleLabel;
    private Label _gameLabel;
    private Label _viewersLabel;
    private Label _uptimeLabel;
    private TableLayoutPanel _mainTableLayoutPanel;

}
