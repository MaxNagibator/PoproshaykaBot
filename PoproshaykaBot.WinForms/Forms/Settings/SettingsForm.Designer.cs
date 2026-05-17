namespace PoproshaykaBot.WinForms.Forms.Settings;

partial class SettingsForm
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
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
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        var resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
        _mainTableLayout = new TableLayoutPanel();
        _tabControl = new TabControl();
        _generalTabPage = new TabPage();
        _generalTableLayout = new TableLayoutPanel();
        _basicGroupBox = new GroupBox();
        _basicSettingsControl = new BasicSettingsControl();
        _rateLimitingGroupBox = new GroupBox();
        _rateLimitingSettingsControl = new RateLimitingSettingsControl();
        _httpServerGroupBox = new GroupBox();
        _httpServerSettingsControl = new HttpServerSettingsControl();
        _messagesTabPage = new TabPage();
        _messagesSettingsControl = new MessagesSettingsControl();
        _oauthTabPage = new TabPage();
        _oauthSettingsControl = new OAuthSettingsControl();
        _obsChatTabPage = new TabPage();
        _obsChatSettingsControl = new ObsChatSettingsControl();
        _obsIntegrationTabPage = new TabPage();
        _obsIntegrationSettingsControl = new ObsIntegrationSettingsControl();
        _autoBroadcastTabPage = new TabPage();
        _autoBroadcastSettingsControl = new AutoBroadcastSettingsControl();
        _botLifecycleAutomationTabPage = new TabPage();
        _botLifecycleAutomationSettingsControl = new BotLifecycleAutomationSettingsControl();
        _pollsTabPage = new TabPage();
        _pollsSettingsControl = new PollsSettingsControl();
        _dashboardTabPage = new TabPage();
        _dashboardSettingsControl = new DashboardSettingsControl();
        _miscTabPage = new TabPage();
        _miscSettingsControl = new MiscSettingsControl();
        _buttonPanel = new FlowLayoutPanel();
        _resetButton = new Button();
        _okButton = new Button();
        _cancelButton = new Button();
        _applyButton = new Button();
        _mainTableLayout.SuspendLayout();
        _tabControl.SuspendLayout();
        _generalTabPage.SuspendLayout();
        _generalTableLayout.SuspendLayout();
        _basicGroupBox.SuspendLayout();
        _rateLimitingGroupBox.SuspendLayout();
        _httpServerGroupBox.SuspendLayout();
        _messagesTabPage.SuspendLayout();
        _oauthTabPage.SuspendLayout();
        _obsChatTabPage.SuspendLayout();
        _obsIntegrationTabPage.SuspendLayout();
        _autoBroadcastTabPage.SuspendLayout();
        _botLifecycleAutomationTabPage.SuspendLayout();
        _pollsTabPage.SuspendLayout();
        _dashboardTabPage.SuspendLayout();
        _miscTabPage.SuspendLayout();
        _buttonPanel.SuspendLayout();
        SuspendLayout();
        // 
        // _mainTableLayout
        // 
        _mainTableLayout.ColumnCount = 1;
        _mainTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _mainTableLayout.Controls.Add(_tabControl, 0, 0);
        _mainTableLayout.Controls.Add(_buttonPanel, 0, 1);
        _mainTableLayout.Dock = DockStyle.Fill;
        _mainTableLayout.Location = new Point(0, 0);
        _mainTableLayout.Name = "_mainTableLayout";
        _mainTableLayout.Padding = new Padding(12, 12, 12, 12);
        _mainTableLayout.RowCount = 2;
        _mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _mainTableLayout.RowStyles.Add(new RowStyle());
        _mainTableLayout.Size = new Size(741, 643);
        _mainTableLayout.TabIndex = 0;
        // 
        // _tabControl
        // 
        _tabControl.Controls.Add(_generalTabPage);
        _tabControl.Controls.Add(_messagesTabPage);
        _tabControl.Controls.Add(_oauthTabPage);
        _tabControl.Controls.Add(_obsChatTabPage);
        _tabControl.Controls.Add(_obsIntegrationTabPage);
        _tabControl.Controls.Add(_autoBroadcastTabPage);
        _tabControl.Controls.Add(_botLifecycleAutomationTabPage);
        _tabControl.Controls.Add(_pollsTabPage);
        _tabControl.Controls.Add(_dashboardTabPage);
        _tabControl.Controls.Add(_miscTabPage);
        _tabControl.Dock = DockStyle.Fill;
        _tabControl.Location = new Point(15, 15);
        _tabControl.Name = "_tabControl";
        _tabControl.SelectedIndex = 0;
        _tabControl.Size = new Size(711, 584);
        _tabControl.TabIndex = 13;
        _tabControl.SelectedIndexChanged += OnTabControlSelectedIndexChanged;
        //
        // _generalTabPage
        //
        _generalTabPage.Controls.Add(_generalTableLayout);
        _generalTabPage.Location = new Point(4, 24);
        _generalTabPage.Name = "_generalTabPage";
        _generalTabPage.Padding = new Padding(10, 10, 10, 10);
        _generalTabPage.Size = new Size(703, 556);
        _generalTabPage.TabIndex = 0;
        _generalTabPage.Text = "Основные";
        _generalTabPage.UseVisualStyleBackColor = true;
        //
        // _generalTableLayout
        //
        _generalTableLayout.ColumnCount = 1;
        _generalTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _generalTableLayout.Controls.Add(_basicGroupBox, 0, 0);
        _generalTableLayout.Controls.Add(_rateLimitingGroupBox, 0, 1);
        _generalTableLayout.Controls.Add(_httpServerGroupBox, 0, 2);
        _generalTableLayout.Dock = DockStyle.Fill;
        _generalTableLayout.Location = new Point(10, 10);
        _generalTableLayout.Name = "_generalTableLayout";
        _generalTableLayout.RowCount = 4;
        _generalTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 124F));
        _generalTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
        _generalTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160F));
        _generalTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _generalTableLayout.Size = new Size(683, 536);
        _generalTableLayout.TabIndex = 0;
        //
        // _basicGroupBox
        //
        _basicGroupBox.Controls.Add(_basicSettingsControl);
        _basicGroupBox.Dock = DockStyle.Fill;
        _basicGroupBox.Location = new Point(3, 3);
        _basicGroupBox.Name = "_basicGroupBox";
        _basicGroupBox.Size = new Size(677, 124);
        _basicGroupBox.TabIndex = 0;
        _basicGroupBox.TabStop = false;
        _basicGroupBox.Text = "Основные";
        //
        // _basicSettingsControl
        //
        _basicSettingsControl.Dock = DockStyle.Fill;
        _basicSettingsControl.Location = new Point(3, 19);
        _basicSettingsControl.Margin = new Padding(6, 7, 6, 7);
        _basicSettingsControl.Name = "_basicSettingsControl";
        _basicSettingsControl.Size = new Size(671, 102);
        _basicSettingsControl.TabIndex = 0;
        _basicSettingsControl.SettingChanged += OnSettingChanged;
        //
        // _rateLimitingGroupBox
        //
        _rateLimitingGroupBox.Controls.Add(_rateLimitingSettingsControl);
        _rateLimitingGroupBox.Dock = DockStyle.Fill;
        _rateLimitingGroupBox.Location = new Point(3, 133);
        _rateLimitingGroupBox.Name = "_rateLimitingGroupBox";
        _rateLimitingGroupBox.Size = new Size(677, 94);
        _rateLimitingGroupBox.TabIndex = 1;
        _rateLimitingGroupBox.TabStop = false;
        _rateLimitingGroupBox.Text = "Ограничения";
        //
        // _rateLimitingSettingsControl
        //
        _rateLimitingSettingsControl.Dock = DockStyle.Fill;
        _rateLimitingSettingsControl.Location = new Point(3, 19);
        _rateLimitingSettingsControl.Margin = new Padding(6, 7, 6, 7);
        _rateLimitingSettingsControl.Name = "_rateLimitingSettingsControl";
        _rateLimitingSettingsControl.Size = new Size(671, 72);
        _rateLimitingSettingsControl.TabIndex = 0;
        _rateLimitingSettingsControl.SettingChanged += OnSettingChanged;
        //
        // _httpServerGroupBox
        //
        _httpServerGroupBox.Controls.Add(_httpServerSettingsControl);
        _httpServerGroupBox.Dock = DockStyle.Fill;
        _httpServerGroupBox.Location = new Point(3, 233);
        _httpServerGroupBox.Name = "_httpServerGroupBox";
        _httpServerGroupBox.Size = new Size(677, 300);
        _httpServerGroupBox.TabIndex = 2;
        _httpServerGroupBox.TabStop = false;
        _httpServerGroupBox.Text = "HTTP сервер";
        //
        // _httpServerSettingsControl
        //
        _httpServerSettingsControl.Dock = DockStyle.Fill;
        _httpServerSettingsControl.Location = new Point(3, 19);
        _httpServerSettingsControl.Margin = new Padding(6, 7, 6, 7);
        _httpServerSettingsControl.Name = "_httpServerSettingsControl";
        _httpServerSettingsControl.Size = new Size(671, 278);
        _httpServerSettingsControl.TabIndex = 0;
        //
        // _messagesTabPage
        // 
        _messagesTabPage.Controls.Add(_messagesSettingsControl);
        _messagesTabPage.Location = new Point(4, 24);
        _messagesTabPage.Name = "_messagesTabPage";
        _messagesTabPage.Padding = new Padding(10, 10, 10, 10);
        _messagesTabPage.Size = new Size(613, 549);
        _messagesTabPage.TabIndex = 2;
        _messagesTabPage.Text = "Сообщения";
        _messagesTabPage.UseVisualStyleBackColor = true;
        // 
        // _messagesSettingsControl
        // 
        _messagesSettingsControl.Dock = DockStyle.Fill;
        _messagesSettingsControl.Location = new Point(10, 10);
        _messagesSettingsControl.Margin = new Padding(6, 7, 6, 7);
        _messagesSettingsControl.Name = "_messagesSettingsControl";
        _messagesSettingsControl.Size = new Size(593, 529);
        _messagesSettingsControl.TabIndex = 0;
        _messagesSettingsControl.SettingChanged += OnSettingChanged;
        //
        // _oauthTabPage
        // 
        _oauthTabPage.Controls.Add(_oauthSettingsControl);
        _oauthTabPage.Location = new Point(4, 24);
        _oauthTabPage.Name = "_oauthTabPage";
        _oauthTabPage.Padding = new Padding(10, 10, 10, 10);
        _oauthTabPage.Size = new Size(613, 549);
        _oauthTabPage.TabIndex = 4;
        _oauthTabPage.Text = "OAuth";
        _oauthTabPage.UseVisualStyleBackColor = true;
        // 
        // _oauthSettingsControl
        // 
        _oauthSettingsControl.Dock = DockStyle.Fill;
        _oauthSettingsControl.Location = new Point(10, 10);
        _oauthSettingsControl.Margin = new Padding(6, 7, 6, 7);
        _oauthSettingsControl.Name = "_oauthSettingsControl";
        _oauthSettingsControl.Size = new Size(593, 529);
        _oauthSettingsControl.TabIndex = 0;
        _oauthSettingsControl.SettingChanged += OnSettingChanged;
        _oauthSettingsControl.LaunchOnboardingRequested += OnOAuthLaunchOnboardingRequested;
        // 
        // _obsChatTabPage
        // 
        _obsChatTabPage.Controls.Add(_obsChatSettingsControl);
        _obsChatTabPage.Location = new Point(4, 24);
        _obsChatTabPage.Name = "_obsChatTabPage";
        _obsChatTabPage.Padding = new Padding(10, 10, 10, 10);
        _obsChatTabPage.Size = new Size(613, 549);
        _obsChatTabPage.TabIndex = 5;
        _obsChatTabPage.Text = "OBS Чат";
        _obsChatTabPage.UseVisualStyleBackColor = true;
        // 
        // _obsChatSettingsControl
        // 
        _obsChatSettingsControl.Dock = DockStyle.Fill;
        _obsChatSettingsControl.Location = new Point(10, 10);
        _obsChatSettingsControl.Margin = new Padding(6, 7, 6, 7);
        _obsChatSettingsControl.Name = "_obsChatSettingsControl";
        _obsChatSettingsControl.Size = new Size(593, 529);
        _obsChatSettingsControl.TabIndex = 0;
        _obsChatSettingsControl.SettingChanged += OnSettingChanged;
        //
        // _obsIntegrationTabPage
        //
        _obsIntegrationTabPage.Controls.Add(_obsIntegrationSettingsControl);
        _obsIntegrationTabPage.Location = new Point(4, 24);
        _obsIntegrationTabPage.Name = "_obsIntegrationTabPage";
        _obsIntegrationTabPage.Padding = new Padding(10, 10, 10, 10);
        _obsIntegrationTabPage.Size = new Size(613, 549);
        _obsIntegrationTabPage.TabIndex = 11;
        _obsIntegrationTabPage.Text = "OBS подключение";
        _obsIntegrationTabPage.UseVisualStyleBackColor = true;
        //
        // _obsIntegrationSettingsControl
        //
        _obsIntegrationSettingsControl.Dock = DockStyle.Fill;
        _obsIntegrationSettingsControl.Location = new Point(10, 10);
        _obsIntegrationSettingsControl.Margin = new Padding(6, 7, 6, 7);
        _obsIntegrationSettingsControl.Name = "_obsIntegrationSettingsControl";
        _obsIntegrationSettingsControl.Size = new Size(593, 529);
        _obsIntegrationSettingsControl.TabIndex = 0;
        _obsIntegrationSettingsControl.SettingChanged += OnSettingChanged;
        //
        // _autoBroadcastTabPage
        // 
        _autoBroadcastTabPage.Controls.Add(_autoBroadcastSettingsControl);
        _autoBroadcastTabPage.Location = new Point(4, 24);
        _autoBroadcastTabPage.Name = "_autoBroadcastTabPage";
        _autoBroadcastTabPage.Padding = new Padding(10, 10, 10, 10);
        _autoBroadcastTabPage.Size = new Size(613, 549);
        _autoBroadcastTabPage.TabIndex = 6;
        _autoBroadcastTabPage.Text = "Автоматический режим";
        _autoBroadcastTabPage.UseVisualStyleBackColor = true;
        // 
        // _autoBroadcastSettingsControl
        // 
        _autoBroadcastSettingsControl.Dock = DockStyle.Fill;
        _autoBroadcastSettingsControl.Location = new Point(10, 10);
        _autoBroadcastSettingsControl.Margin = new Padding(6, 7, 6, 7);
        _autoBroadcastSettingsControl.Name = "_autoBroadcastSettingsControl";
        _autoBroadcastSettingsControl.Size = new Size(593, 529);
        _autoBroadcastSettingsControl.TabIndex = 0;
        _autoBroadcastSettingsControl.SettingChanged += OnSettingChanged;
        //
        // _botLifecycleAutomationTabPage
        //
        _botLifecycleAutomationTabPage.Controls.Add(_botLifecycleAutomationSettingsControl);
        _botLifecycleAutomationTabPage.Location = new Point(4, 24);
        _botLifecycleAutomationTabPage.Name = "_botLifecycleAutomationTabPage";
        _botLifecycleAutomationTabPage.Padding = new Padding(10, 10, 10, 10);
        _botLifecycleAutomationTabPage.Size = new Size(613, 549);
        _botLifecycleAutomationTabPage.TabIndex = 10;
        _botLifecycleAutomationTabPage.Text = "Автозапуск бота";
        _botLifecycleAutomationTabPage.UseVisualStyleBackColor = true;
        //
        // _botLifecycleAutomationSettingsControl
        //
        _botLifecycleAutomationSettingsControl.Dock = DockStyle.Fill;
        _botLifecycleAutomationSettingsControl.Location = new Point(10, 10);
        _botLifecycleAutomationSettingsControl.Margin = new Padding(6, 7, 6, 7);
        _botLifecycleAutomationSettingsControl.Name = "_botLifecycleAutomationSettingsControl";
        _botLifecycleAutomationSettingsControl.Size = new Size(593, 529);
        _botLifecycleAutomationSettingsControl.TabIndex = 0;
        _botLifecycleAutomationSettingsControl.SettingChanged += OnSettingChanged;
        //
        // _pollsTabPage
        //
        _pollsTabPage.Controls.Add(_pollsSettingsControl);
        _pollsTabPage.Location = new Point(4, 24);
        _pollsTabPage.Name = "_pollsTabPage";
        _pollsTabPage.Padding = new Padding(10, 10, 10, 10);
        _pollsTabPage.Size = new Size(613, 549);
        _pollsTabPage.TabIndex = 8;
        _pollsTabPage.Text = "Голосования";
        _pollsTabPage.UseVisualStyleBackColor = true;
        //
        // _pollsSettingsControl
        //
        _pollsSettingsControl.Dock = DockStyle.Fill;
        _pollsSettingsControl.Location = new Point(10, 10);
        _pollsSettingsControl.Margin = new Padding(6, 7, 6, 7);
        _pollsSettingsControl.Name = "_pollsSettingsControl";
        _pollsSettingsControl.Size = new Size(593, 529);
        _pollsSettingsControl.TabIndex = 0;
        _pollsSettingsControl.SettingChanged += OnSettingChanged;
        //
        // _dashboardTabPage
        //
        _dashboardTabPage.Controls.Add(_dashboardSettingsControl);
        _dashboardTabPage.Location = new Point(4, 24);
        _dashboardTabPage.Name = "_dashboardTabPage";
        _dashboardTabPage.Padding = new Padding(10, 10, 10, 10);
        _dashboardTabPage.Size = new Size(613, 549);
        _dashboardTabPage.TabIndex = 9;
        _dashboardTabPage.Text = "Дашборд";
        _dashboardTabPage.UseVisualStyleBackColor = true;
        //
        // _dashboardSettingsControl
        //
        _dashboardSettingsControl.Dock = DockStyle.Fill;
        _dashboardSettingsControl.Location = new Point(10, 10);
        _dashboardSettingsControl.Margin = new Padding(6, 7, 6, 7);
        _dashboardSettingsControl.Name = "_dashboardSettingsControl";
        _dashboardSettingsControl.Size = new Size(593, 529);
        _dashboardSettingsControl.TabIndex = 0;
        _dashboardSettingsControl.SettingChanged += OnSettingChanged;
        //
        // _miscTabPage
        //
        _miscTabPage.Controls.Add(_miscSettingsControl);
        _miscTabPage.Location = new Point(4, 24);
        _miscTabPage.Name = "_miscTabPage";
        _miscTabPage.Padding = new Padding(10, 10, 10, 10);
        _miscTabPage.Size = new Size(613, 549);
        _miscTabPage.TabIndex = 7;
        _miscTabPage.Text = "Прочее";
        _miscTabPage.UseVisualStyleBackColor = true;
        // 
        // _miscSettingsControl
        // 
        _miscSettingsControl.Dock = DockStyle.Fill;
        _miscSettingsControl.Location = new Point(10, 10);
        _miscSettingsControl.Margin = new Padding(6, 7, 6, 7);
        _miscSettingsControl.Name = "_miscSettingsControl";
        _miscSettingsControl.Size = new Size(593, 529);
        _miscSettingsControl.TabIndex = 0;
        //
        // _buttonPanel
        // 
        _buttonPanel.AutoSize = true;
        _buttonPanel.Controls.Add(_resetButton);
        _buttonPanel.Controls.Add(_okButton);
        _buttonPanel.Controls.Add(_cancelButton);
        _buttonPanel.Controls.Add(_applyButton);
        _buttonPanel.Dock = DockStyle.Fill;
        _buttonPanel.FlowDirection = FlowDirection.RightToLeft;
        _buttonPanel.Location = new Point(15, 605);
        _buttonPanel.Name = "_buttonPanel";
        _buttonPanel.Size = new Size(711, 23);
        _buttonPanel.TabIndex = 1;
        // 
        // _resetButton
        // 
        _resetButton.Anchor = AnchorStyles.Left;
        _resetButton.Location = new Point(633, 0);
        _resetButton.Margin = new Padding(0, 0, 3, 0);
        _resetButton.Name = "_resetButton";
        _resetButton.Size = new Size(75, 23);
        _resetButton.TabIndex = 17;
        _resetButton.Text = "Сброс";
        _resetButton.UseVisualStyleBackColor = true;
        _resetButton.Click += OnResetButtonClicked;
        // 
        // _okButton
        // 
        _okButton.DialogResult = DialogResult.OK;
        _okButton.Location = new Point(555, 0);
        _okButton.Margin = new Padding(0, 0, 3, 0);
        _okButton.Name = "_okButton";
        _okButton.Size = new Size(75, 23);
        _okButton.TabIndex = 14;
        _okButton.Text = "OK";
        _okButton.UseVisualStyleBackColor = true;
        _okButton.Click += OnOkButtonClicked;
        // 
        // _cancelButton
        // 
        _cancelButton.DialogResult = DialogResult.Cancel;
        _cancelButton.Location = new Point(477, 0);
        _cancelButton.Margin = new Padding(0, 0, 3, 0);
        _cancelButton.Name = "_cancelButton";
        _cancelButton.Size = new Size(75, 23);
        _cancelButton.TabIndex = 15;
        _cancelButton.Text = "Отмена";
        _cancelButton.UseVisualStyleBackColor = true;
        _cancelButton.Click += OnCancelButtonClicked;
        // 
        // _applyButton
        // 
        _applyButton.Enabled = false;
        _applyButton.Location = new Point(399, 0);
        _applyButton.Margin = new Padding(0, 0, 3, 0);
        _applyButton.Name = "_applyButton";
        _applyButton.Size = new Size(75, 23);
        _applyButton.TabIndex = 16;
        _applyButton.Text = "Применить";
        _applyButton.UseVisualStyleBackColor = true;
        _applyButton.Click += OnApplyButtonClicked;
        // 
        // SettingsForm
        // 
        AcceptButton = _okButton;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = _cancelButton;
        ClientSize = new Size(741, 643);
        Controls.Add(_mainTableLayout);
        Icon = (Icon)resources.GetObject("$this.Icon");
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SettingsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Настройки Twitch бота";
        _mainTableLayout.ResumeLayout(false);
        _mainTableLayout.PerformLayout();
        _tabControl.ResumeLayout(false);
        _generalTabPage.ResumeLayout(false);
        _generalTableLayout.ResumeLayout(false);
        _basicGroupBox.ResumeLayout(false);
        _rateLimitingGroupBox.ResumeLayout(false);
        _httpServerGroupBox.ResumeLayout(false);
        _messagesTabPage.ResumeLayout(false);
        _oauthTabPage.ResumeLayout(false);
        _obsChatTabPage.ResumeLayout(false);
        _obsIntegrationTabPage.ResumeLayout(false);
        _autoBroadcastTabPage.ResumeLayout(false);
        _botLifecycleAutomationTabPage.ResumeLayout(false);
        _pollsTabPage.ResumeLayout(false);
        _dashboardTabPage.ResumeLayout(false);
        _miscTabPage.ResumeLayout(false);
        _buttonPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel _mainTableLayout;
    private TabControl _tabControl;
    private TabPage _generalTabPage;
    private TableLayoutPanel _generalTableLayout;
    private GroupBox _basicGroupBox;
    private GroupBox _rateLimitingGroupBox;
    private GroupBox _httpServerGroupBox;
    private TabPage _messagesTabPage;
    private TabPage _oauthTabPage;
    private TabPage _obsChatTabPage;
    private TabPage _obsIntegrationTabPage;
    private TabPage _autoBroadcastTabPage;
    private TabPage _botLifecycleAutomationTabPage;
    private TabPage _miscTabPage;
    private BasicSettingsControl _basicSettingsControl;
    private RateLimitingSettingsControl _rateLimitingSettingsControl;
    private MessagesSettingsControl _messagesSettingsControl;
    private HttpServerSettingsControl _httpServerSettingsControl;
    private OAuthSettingsControl _oauthSettingsControl;
    private ObsChatSettingsControl _obsChatSettingsControl;
    private ObsIntegrationSettingsControl _obsIntegrationSettingsControl;
    private AutoBroadcastSettingsControl _autoBroadcastSettingsControl;
    private BotLifecycleAutomationSettingsControl _botLifecycleAutomationSettingsControl;
    private MiscSettingsControl _miscSettingsControl;
    private TabPage _pollsTabPage;
    private PollsSettingsControl _pollsSettingsControl;
    private TabPage _dashboardTabPage;
    private DashboardSettingsControl _dashboardSettingsControl;
    private FlowLayoutPanel _buttonPanel;
    private Button _resetButton;
    private Button _okButton;
    private Button _cancelButton;
    private Button _applyButton;
}
