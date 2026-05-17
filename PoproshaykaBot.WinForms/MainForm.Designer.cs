using PoproshaykaBot.WinForms.Controls;
using PoproshaykaBot.WinForms.Tiles;

namespace PoproshaykaBot.WinForms;

partial class MainForm
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        var resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
        _mainTableLayoutPanel = new TableLayoutPanel();
        _onboardingBannerPanel = new Panel();
        _onboardingBannerLabel = new Label();
        _onboardingBannerButton = new Button();
        _mainToolStrip = new ClickThroughToolStrip();
        _connectToolStripButton = new ToolStripButton();
        _settingsToolStripButton = new ToolStripButton();
        _statsToolStripButton = new ToolStripButton();
        _streamHistoryToolStripButton = new ToolStripButton();
        _dashboardControl = new DashboardControl();
        _connectionProgressBar = new ToolStripProgressBar();
        _connectionStatusLabel = new ToolStripStatusLabel();
        _streamMonitoringStatusLabel = new ToolStripStatusLabel();
        _statusStrip = new StatusStrip();
        _mainTableLayoutPanel.SuspendLayout();
        _onboardingBannerPanel.SuspendLayout();
        _mainToolStrip.SuspendLayout();
        _statusStrip.SuspendLayout();
        SuspendLayout();
        // 
        // _mainTableLayoutPanel
        // 
        _mainTableLayoutPanel.ColumnCount = 1;
        _mainTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Controls.Add(_onboardingBannerPanel, 0, 0);
        _mainTableLayoutPanel.Controls.Add(_mainToolStrip, 0, 1);
        _mainTableLayoutPanel.Controls.Add(_dashboardControl, 0, 2);
        _mainTableLayoutPanel.Dock = DockStyle.Fill;
        _mainTableLayoutPanel.Location = new Point(0, 0);
        _mainTableLayoutPanel.Name = "_mainTableLayoutPanel";
        _mainTableLayoutPanel.RowCount = 3;
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle());
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _mainTableLayoutPanel.Size = new Size(800, 562);
        _mainTableLayoutPanel.TabIndex = 0;
        //
        // _onboardingBannerPanel
        //
        _onboardingBannerPanel.BackColor = Color.LightYellow;
        _onboardingBannerPanel.Controls.Add(_onboardingBannerLabel);
        _onboardingBannerPanel.Controls.Add(_onboardingBannerButton);
        _onboardingBannerPanel.Dock = DockStyle.Fill;
        _onboardingBannerPanel.Margin = new Padding(0);
        _onboardingBannerPanel.MinimumSize = new Size(0, 40);
        _onboardingBannerPanel.Name = "_onboardingBannerPanel";
        _onboardingBannerPanel.Padding = new Padding(10, 6, 10, 6);
        _onboardingBannerPanel.Size = new Size(800, 40);
        _onboardingBannerPanel.TabIndex = 0;
        _onboardingBannerPanel.Visible = false;
        //
        // _onboardingBannerLabel
        //
        _onboardingBannerLabel.AutoSize = false;
        _onboardingBannerLabel.Dock = DockStyle.Fill;
        _onboardingBannerLabel.ForeColor = Color.DarkGoldenrod;
        _onboardingBannerLabel.Name = "_onboardingBannerLabel";
        _onboardingBannerLabel.Size = new Size(670, 28);
        _onboardingBannerLabel.TabIndex = 0;
        _onboardingBannerLabel.Text = "⚠ Подключение бота недоступно: не заполнены настройки авторизации.";
        _onboardingBannerLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _onboardingBannerButton
        //
        _onboardingBannerButton.Dock = DockStyle.Right;
        _onboardingBannerButton.Margin = new Padding(0);
        _onboardingBannerButton.Name = "_onboardingBannerButton";
        _onboardingBannerButton.Size = new Size(160, 28);
        _onboardingBannerButton.TabIndex = 1;
        _onboardingBannerButton.Text = "🧙 Открыть мастер →";
        _onboardingBannerButton.UseVisualStyleBackColor = true;
        _onboardingBannerButton.Click += OnOnboardingBannerButtonClicked;
        // 
        // _mainToolStrip
        // 
        _mainToolStrip.AutoSize = false;
        _mainToolStrip.BackColor = SystemColors.Control;
        _mainToolStrip.CanOverflow = false;
        _mainToolStrip.Dock = DockStyle.Fill;
        _mainToolStrip.GripStyle = ToolStripGripStyle.Hidden;
        _mainToolStrip.ImageScalingSize = new Size(24, 24);
        _mainToolStrip.Items.AddRange(new ToolStripItem[] { _connectToolStripButton, _settingsToolStripButton, _statsToolStripButton, _streamHistoryToolStripButton });
        _mainToolStrip.Location = new Point(12, 12);
        _mainToolStrip.Name = "_mainToolStrip";
        _mainToolStrip.Padding = new Padding(5, 0, 5, 0);
        _mainToolStrip.Size = new Size(776, 40);
        _mainToolStrip.TabIndex = 0;
        _mainToolStrip.Text = "Панель управления";
        // 
        // _connectToolStripButton
        // 
        _connectToolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _connectToolStripButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _connectToolStripButton.Name = "_connectToolStripButton";
        _connectToolStripButton.Size = new Size(101, 37);
        _connectToolStripButton.Text = "🔌 Подключить";
        _connectToolStripButton.Click += OnConnectButtonClicked;
        // 
        // _settingsToolStripButton
        // 
        _settingsToolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _settingsToolStripButton.Name = "_settingsToolStripButton";
        _settingsToolStripButton.Size = new Size(86, 37);
        _settingsToolStripButton.Text = "⚙️ Настройки";
        _settingsToolStripButton.Click += OnSettingsButtonClicked;
        // 
        // _statsToolStripButton
        // 
        _statsToolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _statsToolStripButton.Name = "_statsToolStripButton";
        _statsToolStripButton.Size = new Size(87, 37);
        _statsToolStripButton.Text = "📊 Статистика";
        _statsToolStripButton.ToolTipText = "Открыть окно статистики (Alt+U)";
        _statsToolStripButton.Click += OnUserStatisticsButtonClicked;
        //
        // _streamHistoryToolStripButton
        //
        _streamHistoryToolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _streamHistoryToolStripButton.Name = "_streamHistoryToolStripButton";
        _streamHistoryToolStripButton.Size = new Size(120, 37);
        _streamHistoryToolStripButton.Text = "🎬 История стримов";
        _streamHistoryToolStripButton.ToolTipText = "Открыть историю стримов (Alt+H)";
        _streamHistoryToolStripButton.Click += OnStreamHistoryButtonClicked;
        //
        // _dashboardControl
        // 
        _dashboardControl.Dock = DockStyle.Fill;
        _dashboardControl.Location = new Point(15, 55);
        _dashboardControl.Name = "_dashboardControl";
        _dashboardControl.Size = new Size(770, 492);
        _dashboardControl.TabIndex = 1;
        // 
        // _connectionProgressBar
        // 
        _connectionProgressBar.MarqueeAnimationSpeed = 30;
        _connectionProgressBar.Name = "_connectionProgressBar";
        _connectionProgressBar.Size = new Size(100, 16);
        _connectionProgressBar.Style = ProgressBarStyle.Marquee;
        _connectionProgressBar.Visible = false;
        //
        // _connectionStatusLabel
        //
        _connectionStatusLabel.Name = "_connectionStatusLabel";
        _connectionStatusLabel.Size = new Size(0, 17);
        _connectionStatusLabel.Visible = false;
        //
        // _streamMonitoringStatusLabel
        //
        _streamMonitoringStatusLabel.Name = "_streamMonitoringStatusLabel";
        _streamMonitoringStatusLabel.Size = new Size(150, 17);
        _streamMonitoringStatusLabel.Spring = true;
        _streamMonitoringStatusLabel.TextAlign = ContentAlignment.MiddleRight;
        _streamMonitoringStatusLabel.Text = "⚪ Стрим-мониторинг: ожидание";
        _streamMonitoringStatusLabel.ToolTipText = "Состояние независимого подключения к EventSub WebSocket";
        //
        // _statusStrip
        //
        _statusStrip.ImageScalingSize = new Size(36, 36);
        _statusStrip.Items.AddRange(new ToolStripItem[] { _connectionStatusLabel, _connectionProgressBar, _streamMonitoringStatusLabel });
        _statusStrip.Location = new Point(0, 562);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Size = new Size(800, 22);
        _statusStrip.TabIndex = 1;
        _statusStrip.Text = "statusStrip1";
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(800, 584);
        Controls.Add(_mainTableLayoutPanel);
        Controls.Add(_statusStrip);
        Icon = (Icon)resources.GetObject("$this.Icon");
        MinimumSize = new Size(594, 376);
        Name = "MainForm";
        Text = "Попрощайка Бот - Управление";
        _mainTableLayoutPanel.ResumeLayout(false);
        _mainTableLayoutPanel.PerformLayout();
        _onboardingBannerPanel.ResumeLayout(false);
        _onboardingBannerPanel.PerformLayout();
        _mainToolStrip.ResumeLayout(false);
        _mainToolStrip.PerformLayout();
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private TableLayoutPanel _mainTableLayoutPanel;
    private Panel _onboardingBannerPanel;
    private Label _onboardingBannerLabel;
    private Button _onboardingBannerButton;
    private ClickThroughToolStrip _mainToolStrip;
    private ToolStripButton _connectToolStripButton;
    private ToolStripButton _settingsToolStripButton;
    private ToolStripButton _statsToolStripButton;
    private ToolStripButton _streamHistoryToolStripButton;
    private DashboardControl _dashboardControl;
    private ToolStripProgressBar _connectionProgressBar;
    private ToolStripStatusLabel _connectionStatusLabel;
    private ToolStripStatusLabel _streamMonitoringStatusLabel;
    private StatusStrip _statusStrip;

    #endregion
}