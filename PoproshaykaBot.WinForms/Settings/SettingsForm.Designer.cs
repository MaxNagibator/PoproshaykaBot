namespace PoproshaykaBot.WinForms.Settings;

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
        _basicTabPage = new TabPage();
        _basicSettingsControl = new BasicSettingsControl();
        _rateLimitingTabPage = new TabPage();
        _rateLimitingSettingsControl = new RateLimitingSettingsControl();
        _messagesTabPage = new TabPage();
        _messagesSettingsControl = new MessagesSettingsControl();
        _httpServerTabPage = new TabPage();
        _httpServerSettingsControl = new HttpServerSettingsControl();
        _oauthTabPage = new TabPage();
        _oauthSettingsControl = new OAuthSettingsControl();
        _obsChatTabPage = new TabPage();
        _obsChatSettingsControl = new ObsChatSettingsControl();
        _buttonPanel = new FlowLayoutPanel();
        _resetButton = new Button();
        _okButton = new Button();
        _cancelButton = new Button();
        _applyButton = new Button();
        _mainTableLayout.SuspendLayout();
        _tabControl.SuspendLayout();
        _basicTabPage.SuspendLayout();
        _rateLimitingTabPage.SuspendLayout();
        _messagesTabPage.SuspendLayout();
        _httpServerTabPage.SuspendLayout();
        _oauthTabPage.SuspendLayout();
        _obsChatTabPage.SuspendLayout();
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
        _mainTableLayout.Padding = new Padding(12);
        _mainTableLayout.RowCount = 2;
        _mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _mainTableLayout.RowStyles.Add(new RowStyle());
        _mainTableLayout.Size = new Size(626, 635);
        _mainTableLayout.TabIndex = 0;
        // 
        // _tabControl
        // 
        _tabControl.Controls.Add(_basicTabPage);
        _tabControl.Controls.Add(_rateLimitingTabPage);
        _tabControl.Controls.Add(_messagesTabPage);
        _tabControl.Controls.Add(_httpServerTabPage);
        _tabControl.Controls.Add(_oauthTabPage);
        _tabControl.Controls.Add(_obsChatTabPage);
        _tabControl.Dock = DockStyle.Fill;
        _tabControl.Location = new Point(15, 15);
        _tabControl.Name = "_tabControl";
        _tabControl.SelectedIndex = 0;
        _tabControl.Size = new Size(596, 576);
        _tabControl.TabIndex = 13;
        // 
        // _basicTabPage
        // 
        _basicTabPage.Controls.Add(_basicSettingsControl);
        _basicTabPage.Location = new Point(4, 24);
        _basicTabPage.Name = "_basicTabPage";
        _basicTabPage.Padding = new Padding(10);
        _basicTabPage.Size = new Size(545, 452);
        _basicTabPage.TabIndex = 0;
        _basicTabPage.Text = "Основные";
        _basicTabPage.UseVisualStyleBackColor = true;
        // 
        // _basicSettingsControl
        // 
        _basicSettingsControl.Dock = DockStyle.Fill;
        _basicSettingsControl.Location = new Point(10, 10);
        _basicSettingsControl.Name = "_basicSettingsControl";
        _basicSettingsControl.Size = new Size(525, 432);
        _basicSettingsControl.TabIndex = 0;
        _basicSettingsControl.SettingChanged += OnSettingChanged;
        // 
        // _rateLimitingTabPage
        // 
        _rateLimitingTabPage.Controls.Add(_rateLimitingSettingsControl);
        _rateLimitingTabPage.Location = new Point(4, 24);
        _rateLimitingTabPage.Name = "_rateLimitingTabPage";
        _rateLimitingTabPage.Padding = new Padding(10);
        _rateLimitingTabPage.Size = new Size(545, 452);
        _rateLimitingTabPage.TabIndex = 1;
        _rateLimitingTabPage.Text = "Ограничения";
        _rateLimitingTabPage.UseVisualStyleBackColor = true;
        // 
        // _rateLimitingSettingsControl
        // 
        _rateLimitingSettingsControl.Dock = DockStyle.Fill;
        _rateLimitingSettingsControl.Location = new Point(10, 10);
        _rateLimitingSettingsControl.Name = "_rateLimitingSettingsControl";
        _rateLimitingSettingsControl.Size = new Size(525, 432);
        _rateLimitingSettingsControl.TabIndex = 0;
        _rateLimitingSettingsControl.SettingChanged += OnSettingChanged;
        // 
        // _messagesTabPage
        // 
        _messagesTabPage.Controls.Add(_messagesSettingsControl);
        _messagesTabPage.Location = new Point(4, 24);
        _messagesTabPage.Name = "_messagesTabPage";
        _messagesTabPage.Padding = new Padding(10);
        _messagesTabPage.Size = new Size(545, 452);
        _messagesTabPage.TabIndex = 3;
        _messagesTabPage.Text = "Сообщения";
        _messagesTabPage.UseVisualStyleBackColor = true;
        // 
        // _messagesSettingsControl
        // 
        _messagesSettingsControl.Dock = DockStyle.Fill;
        _messagesSettingsControl.Location = new Point(10, 10);
        _messagesSettingsControl.Name = "_messagesSettingsControl";
        _messagesSettingsControl.Size = new Size(525, 432);
        _messagesSettingsControl.TabIndex = 0;
        _messagesSettingsControl.SettingChanged += OnSettingChanged;
        // 
        // _httpServerTabPage
        // 
        _httpServerTabPage.Controls.Add(_httpServerSettingsControl);
        _httpServerTabPage.Location = new Point(4, 24);
        _httpServerTabPage.Name = "_httpServerTabPage";
        _httpServerTabPage.Padding = new Padding(10);
        _httpServerTabPage.Size = new Size(545, 452);
        _httpServerTabPage.TabIndex = 4;
        _httpServerTabPage.Text = "HTTP Сервер";
        _httpServerTabPage.UseVisualStyleBackColor = true;
        // 
        // _httpServerSettingsControl
        // 
        _httpServerSettingsControl.Dock = DockStyle.Fill;
        _httpServerSettingsControl.Location = new Point(10, 10);
        _httpServerSettingsControl.Name = "_httpServerSettingsControl";
        _httpServerSettingsControl.Size = new Size(525, 432);
        _httpServerSettingsControl.TabIndex = 0;
        _httpServerSettingsControl.SettingChanged += OnSettingChanged;
        // 
        // _oauthTabPage
        // 
        _oauthTabPage.Controls.Add(_oauthSettingsControl);
        _oauthTabPage.Location = new Point(4, 24);
        _oauthTabPage.Name = "_oauthTabPage";
        _oauthTabPage.Padding = new Padding(10);
        _oauthTabPage.Size = new Size(545, 452);
        _oauthTabPage.TabIndex = 2;
        _oauthTabPage.Text = "OAuth";
        _oauthTabPage.UseVisualStyleBackColor = true;
        // 
        // _oauthSettingsControl
        // 
        _oauthSettingsControl.Dock = DockStyle.Fill;
        _oauthSettingsControl.Location = new Point(10, 10);
        _oauthSettingsControl.Name = "_oauthSettingsControl";
        _oauthSettingsControl.Size = new Size(525, 432);
        _oauthSettingsControl.TabIndex = 0;
        _oauthSettingsControl.SettingChanged += OnSettingChanged;
        // 
        // _obsChatTabPage
        // 
        _obsChatTabPage.Controls.Add(_obsChatSettingsControl);
        _obsChatTabPage.Location = new Point(4, 24);
        _obsChatTabPage.Name = "_obsChatTabPage";
        _obsChatTabPage.Padding = new Padding(10);
        _obsChatTabPage.Size = new Size(588, 548);
        _obsChatTabPage.TabIndex = 5;
        _obsChatTabPage.Text = "OBS Чат";
        _obsChatTabPage.UseVisualStyleBackColor = true;
        // 
        // _obsChatSettingsControl
        // 
        _obsChatSettingsControl.Dock = DockStyle.Fill;
        _obsChatSettingsControl.Location = new Point(10, 10);
        _obsChatSettingsControl.Name = "_obsChatSettingsControl";
        _obsChatSettingsControl.Size = new Size(568, 528);
        _obsChatSettingsControl.TabIndex = 0;
        _obsChatSettingsControl.SettingChanged += OnSettingChanged;
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
        _buttonPanel.Location = new Point(15, 597);
        _buttonPanel.Name = "_buttonPanel";
        _buttonPanel.Size = new Size(596, 23);
        _buttonPanel.TabIndex = 1;
        // 
        // _resetButton
        // 
        _resetButton.Anchor = AnchorStyles.Left;
        _resetButton.Location = new Point(518, 0);
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
        _okButton.Location = new Point(440, 0);
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
        _cancelButton.Location = new Point(362, 0);
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
        _applyButton.Location = new Point(284, 0);
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
        ClientSize = new Size(626, 635);
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
        _basicTabPage.ResumeLayout(false);
        _rateLimitingTabPage.ResumeLayout(false);
        _messagesTabPage.ResumeLayout(false);
        _httpServerTabPage.ResumeLayout(false);
        _oauthTabPage.ResumeLayout(false);
        _obsChatTabPage.ResumeLayout(false);
        _buttonPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel _mainTableLayout;
    private TabControl _tabControl;
    private TabPage _basicTabPage;
    private TabPage _rateLimitingTabPage;
    private TabPage _messagesTabPage;
    private TabPage _httpServerTabPage;
    private TabPage _oauthTabPage;
    private TabPage _obsChatTabPage;
    private BasicSettingsControl _basicSettingsControl;
    private RateLimitingSettingsControl _rateLimitingSettingsControl;
    private MessagesSettingsControl _messagesSettingsControl;
    private HttpServerSettingsControl _httpServerSettingsControl;
    private OAuthSettingsControl _oauthSettingsControl;
    private ObsChatSettingsControl _obsChatSettingsControl;
    private FlowLayoutPanel _buttonPanel;
    private Button _resetButton;
    private Button _okButton;
    private Button _cancelButton;
    private Button _applyButton;
}
