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
        _streamStatusCard = new ObsOutputStatusCard();
        _recordStatusCard = new ObsOutputStatusCard();
        _sourcesScrollPanel = new Panel();
        _sourcesLayoutPanel = new TableLayoutPanel();
        _refreshTimer = new System.Windows.Forms.Timer(components);
        _volumeMeterTimer = new System.Windows.Forms.Timer(components);
        _mainTableLayoutPanel.SuspendLayout();
        _outputsTableLayoutPanel.SuspendLayout();
        _sourcesScrollPanel.SuspendLayout();
        SuspendLayout();
        //
        // _mainTableLayoutPanel
        //
        _mainTableLayoutPanel.ColumnCount = 1;
        _mainTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Controls.Add(_sceneLabel, 0, 0);
        _mainTableLayoutPanel.Controls.Add(_outputsTableLayoutPanel, 0, 1);
        _mainTableLayoutPanel.Controls.Add(_sourcesScrollPanel, 0, 2);
        _mainTableLayoutPanel.Dock = DockStyle.Fill;
        _mainTableLayoutPanel.Location = new Point(0, 0);
        _mainTableLayoutPanel.Name = "_mainTableLayoutPanel";
        _mainTableLayoutPanel.Padding = new Padding(3);
        _mainTableLayoutPanel.RowCount = 3;
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 160F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Size = new Size(320, 290);
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
        _sceneLabel.Text = "Сцена: –";
        _sceneLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _outputsTableLayoutPanel
        //
        _outputsTableLayoutPanel.ColumnCount = 2;
        _outputsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _outputsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _outputsTableLayoutPanel.Controls.Add(_streamStatusCard, 0, 0);
        _outputsTableLayoutPanel.Controls.Add(_recordStatusCard, 1, 0);
        _outputsTableLayoutPanel.Dock = DockStyle.Fill;
        _outputsTableLayoutPanel.Location = new Point(3, 29);
        _outputsTableLayoutPanel.Margin = new Padding(0);
        _outputsTableLayoutPanel.Name = "_outputsTableLayoutPanel";
        _outputsTableLayoutPanel.RowCount = 1;
        _outputsTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _outputsTableLayoutPanel.Size = new Size(314, 160);
        _outputsTableLayoutPanel.TabIndex = 1;
        //
        // _streamStatusCard
        //
        _streamStatusCard.Dock = DockStyle.Fill;
        _streamStatusCard.Location = new Point(0, 0);
        _streamStatusCard.Margin = new Padding(0, 0, 3, 0);
        _streamStatusCard.Name = "_streamStatusCard";
        _streamStatusCard.Size = new Size(154, 160);
        _streamStatusCard.TabIndex = 0;
        //
        // _recordStatusCard
        //
        _recordStatusCard.Dock = DockStyle.Fill;
        _recordStatusCard.Location = new Point(160, 0);
        _recordStatusCard.Margin = new Padding(3, 0, 0, 0);
        _recordStatusCard.Name = "_recordStatusCard";
        _recordStatusCard.Size = new Size(154, 160);
        _recordStatusCard.TabIndex = 1;
        //
        // _sourcesScrollPanel
        //
        _sourcesScrollPanel.AutoScroll = true;
        _sourcesScrollPanel.Controls.Add(_sourcesLayoutPanel);
        _sourcesScrollPanel.Dock = DockStyle.Fill;
        _sourcesScrollPanel.Location = new Point(6, 192);
        _sourcesScrollPanel.Margin = new Padding(3, 3, 3, 3);
        _sourcesScrollPanel.Name = "_sourcesScrollPanel";
        _sourcesScrollPanel.Size = new Size(308, 92);
        _sourcesScrollPanel.TabIndex = 2;
        //
        // _sourcesLayoutPanel
        //
        _sourcesLayoutPanel.AutoSize = true;
        _sourcesLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _sourcesLayoutPanel.ColumnCount = 1;
        _sourcesLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _sourcesLayoutPanel.Dock = DockStyle.Top;
        _sourcesLayoutPanel.Location = new Point(0, 0);
        _sourcesLayoutPanel.Margin = new Padding(0);
        _sourcesLayoutPanel.Name = "_sourcesLayoutPanel";
        _sourcesLayoutPanel.RowCount = 0;
        _sourcesLayoutPanel.Size = new Size(308, 0);
        _sourcesLayoutPanel.TabIndex = 0;
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
        Size = new Size(320, 290);
        _mainTableLayoutPanel.ResumeLayout(false);
        _outputsTableLayoutPanel.ResumeLayout(false);
        _sourcesScrollPanel.ResumeLayout(false);
        _sourcesScrollPanel.PerformLayout();
        ResumeLayout(false);
    }

    private TableLayoutPanel _mainTableLayoutPanel;
    private TableLayoutPanel _outputsTableLayoutPanel;
    private Label _sceneLabel;
    private ObsOutputStatusCard _streamStatusCard;
    private ObsOutputStatusCard _recordStatusCard;
    private Panel _sourcesScrollPanel;
    private TableLayoutPanel _sourcesLayoutPanel;
    private System.Windows.Forms.Timer _refreshTimer;
    private System.Windows.Forms.Timer _volumeMeterTimer;
}
