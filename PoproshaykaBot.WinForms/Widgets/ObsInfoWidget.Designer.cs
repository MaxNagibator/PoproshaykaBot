namespace PoproshaykaBot.WinForms.Widgets;

sealed partial class ObsInfoWidget
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelActiveRefresh();
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _mainTableLayoutPanel = new TableLayoutPanel();
        _sceneLabel = new Label();
        _outputsTableLayoutPanel = new TableLayoutPanel();
        _streamingLabel = new Label();
        _recordingLabel = new Label();
        _microphoneStateLabel = new Label();
        _volumeMeterPanel = new Panel();
        _volumeMeterFillPanel = new Panel();
        _microphoneLabel = new Label();
        _refreshTimer = new System.Windows.Forms.Timer(components);
        _volumeMeterTimer = new System.Windows.Forms.Timer(components);
        _mainTableLayoutPanel.SuspendLayout();
        _outputsTableLayoutPanel.SuspendLayout();
        _volumeMeterPanel.SuspendLayout();
        SuspendLayout();
        //
        // _mainTableLayoutPanel
        //
        _mainTableLayoutPanel.ColumnCount = 1;
        _mainTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Controls.Add(_sceneLabel, 0, 0);
        _mainTableLayoutPanel.Controls.Add(_outputsTableLayoutPanel, 0, 1);
        _mainTableLayoutPanel.Controls.Add(_microphoneStateLabel, 0, 2);
        _mainTableLayoutPanel.Controls.Add(_volumeMeterPanel, 0, 3);
        _mainTableLayoutPanel.Controls.Add(_microphoneLabel, 0, 4);
        _mainTableLayoutPanel.Dock = DockStyle.Fill;
        _mainTableLayoutPanel.Location = new Point(0, 0);
        _mainTableLayoutPanel.Name = "_mainTableLayoutPanel";
        _mainTableLayoutPanel.Padding = new Padding(3);
        _mainTableLayoutPanel.RowCount = 6;
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Size = new Size(320, 160);
        _mainTableLayoutPanel.TabIndex = 0;
        //
        // _sceneLabel
        //
        _sceneLabel.AutoEllipsis = true;
        _sceneLabel.Dock = DockStyle.Fill;
        _sceneLabel.Location = new Point(6, 3);
        _sceneLabel.Name = "_sceneLabel";
        _sceneLabel.Size = new Size(308, 26);
        _sceneLabel.TabIndex = 0;
        _sceneLabel.Text = "Сцена: —";
        _sceneLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _outputsTableLayoutPanel
        //
        _outputsTableLayoutPanel.ColumnCount = 2;
        _outputsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _outputsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _outputsTableLayoutPanel.Controls.Add(_streamingLabel, 0, 0);
        _outputsTableLayoutPanel.Controls.Add(_recordingLabel, 1, 0);
        _outputsTableLayoutPanel.Dock = DockStyle.Fill;
        _outputsTableLayoutPanel.Location = new Point(3, 29);
        _outputsTableLayoutPanel.Margin = new Padding(0);
        _outputsTableLayoutPanel.Name = "_outputsTableLayoutPanel";
        _outputsTableLayoutPanel.RowCount = 1;
        _outputsTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _outputsTableLayoutPanel.Size = new Size(314, 30);
        _outputsTableLayoutPanel.TabIndex = 1;
        //
        // _streamingLabel
        //
        _streamingLabel.AutoEllipsis = true;
        _streamingLabel.Dock = DockStyle.Fill;
        _streamingLabel.ForeColor = Color.DimGray;
        _streamingLabel.Name = "_streamingLabel";
        _streamingLabel.TabIndex = 0;
        _streamingLabel.Text = "Эфир: —";
        _streamingLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _recordingLabel
        //
        _recordingLabel.AutoEllipsis = true;
        _recordingLabel.Dock = DockStyle.Fill;
        _recordingLabel.ForeColor = Color.DimGray;
        _recordingLabel.Name = "_recordingLabel";
        _recordingLabel.TabIndex = 1;
        _recordingLabel.Text = "Запись: —";
        _recordingLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _microphoneStateLabel
        //
        _microphoneStateLabel.AutoEllipsis = true;
        _microphoneStateLabel.BackColor = Color.Gray;
        _microphoneStateLabel.Dock = DockStyle.Fill;
        _microphoneStateLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _microphoneStateLabel.ForeColor = Color.White;
        _microphoneStateLabel.Location = new Point(6, 62);
        _microphoneStateLabel.Margin = new Padding(3, 3, 3, 3);
        _microphoneStateLabel.Name = "_microphoneStateLabel";
        _microphoneStateLabel.Size = new Size(308, 36);
        _microphoneStateLabel.TabIndex = 2;
        _microphoneStateLabel.Text = "МИКРОФОН —";
        _microphoneStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _volumeMeterPanel
        //
        _volumeMeterPanel.BackColor = Color.Gainsboro;
        _volumeMeterPanel.BorderStyle = BorderStyle.FixedSingle;
        _volumeMeterPanel.Controls.Add(_volumeMeterFillPanel);
        _volumeMeterPanel.Dock = DockStyle.Fill;
        _volumeMeterPanel.Location = new Point(6, 104);
        _volumeMeterPanel.Margin = new Padding(3, 3, 3, 3);
        _volumeMeterPanel.Name = "_volumeMeterPanel";
        _volumeMeterPanel.Size = new Size(308, 16);
        _volumeMeterPanel.TabIndex = 3;
        _volumeMeterPanel.Resize += OnVolumeMeterPanelResize;
        //
        // _volumeMeterFillPanel
        //
        _volumeMeterFillPanel.BackColor = Color.SeaGreen;
        _volumeMeterFillPanel.Dock = DockStyle.Left;
        _volumeMeterFillPanel.Location = new Point(0, 0);
        _volumeMeterFillPanel.Name = "_volumeMeterFillPanel";
        _volumeMeterFillPanel.Size = new Size(0, 14);
        _volumeMeterFillPanel.TabIndex = 0;
        //
        // _microphoneLabel
        //
        _microphoneLabel.AutoEllipsis = true;
        _microphoneLabel.Dock = DockStyle.Fill;
        _microphoneLabel.ForeColor = Color.DimGray;
        _microphoneLabel.Location = new Point(6, 123);
        _microphoneLabel.Name = "_microphoneLabel";
        _microphoneLabel.Size = new Size(308, 34);
        _microphoneLabel.TabIndex = 4;
        _microphoneLabel.Text = "Микрофон: —";
        _microphoneLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _refreshTimer
        //
        _refreshTimer.Interval = 5000;
        _refreshTimer.Tick += OnRefreshTimerTick;
        //
        // _volumeMeterTimer
        //
        _volumeMeterTimer.Interval = 33;
        _volumeMeterTimer.Tick += OnVolumeMeterTimerTick;
        //
        // ObsInfoWidget
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_mainTableLayoutPanel);
        Name = "ObsInfoWidget";
        Size = new Size(320, 160);
        _mainTableLayoutPanel.ResumeLayout(false);
        _outputsTableLayoutPanel.ResumeLayout(false);
        _volumeMeterPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    private TableLayoutPanel _mainTableLayoutPanel;
    private TableLayoutPanel _outputsTableLayoutPanel;
    private Label _sceneLabel;
    private Label _streamingLabel;
    private Label _recordingLabel;
    private Label _microphoneStateLabel;
    private Panel _volumeMeterPanel;
    private Panel _volumeMeterFillPanel;
    private Label _microphoneLabel;
    private System.Windows.Forms.Timer _refreshTimer;
    private System.Windows.Forms.Timer _volumeMeterTimer;
}
