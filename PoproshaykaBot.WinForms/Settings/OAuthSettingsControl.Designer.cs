namespace PoproshaykaBot.WinForms.Settings
{
    partial class OAuthSettingsControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            _oauthTableLayout = new TableLayoutPanel();
            _oauthCredentialsSection = new TableLayoutPanel();
            _clientIdLabel = new Label();
            _clientIdPanel = new Panel();
            _clientIdTextBox = new TextBox();
            _clientIdResetButton = new Button();
            _clientSecretLabel = new Label();
            _clientSecretPanel = new Panel();
            _clientSecretTextBox = new TextBox();
            _clientSecretResetButton = new Button();
            _redirectUriLabel = new Label();
            _redirectUriPanel = new Panel();
            _redirectUriTextBox = new TextBox();
            _redirectUriEditButton = new Button();
            _redirectUriResetButton = new Button();
            _scopesLabel = new Label();
            _scopesPanel = new Panel();
            _scopesTextBox = new TextBox();
            _scopesResetButton = new Button();
            _tokenSectionLabel = new Label();
            _tokenManagementSection = new TableLayoutPanel();
            _accessTokenLabel = new Label();
            _accessTokenPanel = new Panel();
            _accessTokenTextBox = new TextBox();
            _showTokenButton = new Button();
            _refreshTokenLabel = new Label();
            _refreshTokenPanel = new Panel();
            _refreshTokenTextBox = new TextBox();
            _tokenStatusLabel = new Label();
            _tokenStatusValueLabel = new Label();
            _lastRefreshLabel = new Label();
            _lastRefreshValueLabel = new Label();
            _tokenButtonsPanel = new FlowLayoutPanel();
            _validateTokenButton = new Button();
            _refreshTokenButton = new Button();
            _clearTokensButton = new Button();
            _testAuthSection = new TableLayoutPanel();
            _testAuthButton = new Button();
            _authStatusLabel = new Label();
            _oauthInfoLabel = new Label();
            _oauthTableLayout.SuspendLayout();
            _oauthCredentialsSection.SuspendLayout();
            _clientIdPanel.SuspendLayout();
            _clientSecretPanel.SuspendLayout();
            _redirectUriPanel.SuspendLayout();
            _scopesPanel.SuspendLayout();
            _tokenManagementSection.SuspendLayout();
            _accessTokenPanel.SuspendLayout();
            _refreshTokenPanel.SuspendLayout();
            _tokenButtonsPanel.SuspendLayout();
            _testAuthSection.SuspendLayout();
            SuspendLayout();
            // 
            // _oauthTableLayout
            // 
            _oauthTableLayout.ColumnCount = 1;
            _oauthTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _oauthTableLayout.Controls.Add(_oauthCredentialsSection, 0, 0);
            _oauthTableLayout.Controls.Add(_tokenSectionLabel, 0, 1);
            _oauthTableLayout.Controls.Add(_tokenManagementSection, 0, 2);
            _oauthTableLayout.Controls.Add(_testAuthSection, 0, 3);
            _oauthTableLayout.Controls.Add(_oauthInfoLabel, 0, 4);
            _oauthTableLayout.Dock = DockStyle.Fill;
            _oauthTableLayout.Location = new Point(0, 0);
            _oauthTableLayout.Name = "_oauthTableLayout";
            _oauthTableLayout.Padding = new Padding(5);
            _oauthTableLayout.RowCount = 6;
            _oauthTableLayout.RowStyles.Add(new RowStyle());
            _oauthTableLayout.RowStyles.Add(new RowStyle());
            _oauthTableLayout.RowStyles.Add(new RowStyle());
            _oauthTableLayout.RowStyles.Add(new RowStyle());
            _oauthTableLayout.RowStyles.Add(new RowStyle());
            _oauthTableLayout.RowStyles.Add(new RowStyle());
            _oauthTableLayout.Size = new Size(548, 458);
            _oauthTableLayout.TabIndex = 0;
            // 
            // _oauthCredentialsSection
            // 
            _oauthCredentialsSection.ColumnCount = 2;
            _oauthCredentialsSection.ColumnStyles.Add(new ColumnStyle());
            _oauthCredentialsSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _oauthCredentialsSection.Controls.Add(_clientIdLabel, 0, 0);
            _oauthCredentialsSection.Controls.Add(_clientIdPanel, 1, 0);
            _oauthCredentialsSection.Controls.Add(_clientSecretLabel, 0, 1);
            _oauthCredentialsSection.Controls.Add(_clientSecretPanel, 1, 1);
            _oauthCredentialsSection.Controls.Add(_redirectUriLabel, 0, 2);
            _oauthCredentialsSection.Controls.Add(_redirectUriPanel, 1, 2);
            _oauthCredentialsSection.Controls.Add(_scopesLabel, 0, 3);
            _oauthCredentialsSection.Controls.Add(_scopesPanel, 1, 3);
            _oauthCredentialsSection.Dock = DockStyle.Fill;
            _oauthCredentialsSection.Location = new Point(5, 5);
            _oauthCredentialsSection.Margin = new Padding(0, 0, 0, 15);
            _oauthCredentialsSection.Name = "_oauthCredentialsSection";
            _oauthCredentialsSection.RowCount = 4;
            _oauthCredentialsSection.RowStyles.Add(new RowStyle());
            _oauthCredentialsSection.RowStyles.Add(new RowStyle());
            _oauthCredentialsSection.RowStyles.Add(new RowStyle());
            _oauthCredentialsSection.RowStyles.Add(new RowStyle());
            _oauthCredentialsSection.Size = new Size(538, 100);
            _oauthCredentialsSection.TabIndex = 0;
            // 
            // _clientIdLabel
            // 
            _clientIdLabel.AutoSize = true;
            _clientIdLabel.Dock = DockStyle.Fill;
            _clientIdLabel.Location = new Point(3, 0);
            _clientIdLabel.Name = "_clientIdLabel";
            _clientIdLabel.Size = new Size(76, 35);
            _clientIdLabel.TabIndex = 0;
            _clientIdLabel.Text = "Client ID:";
            _clientIdLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _clientIdPanel
            // 
            _clientIdPanel.Controls.Add(_clientIdTextBox);
            _clientIdPanel.Controls.Add(_clientIdResetButton);
            _clientIdPanel.Dock = DockStyle.Fill;
            _clientIdPanel.Location = new Point(85, 3);
            _clientIdPanel.Name = "_clientIdPanel";
            _clientIdPanel.Size = new Size(450, 29);
            _clientIdPanel.TabIndex = 1;
            // 
            // _clientIdTextBox
            // 
            _clientIdTextBox.Dock = DockStyle.Fill;
            _clientIdTextBox.Location = new Point(0, 0);
            _clientIdTextBox.Margin = new Padding(0, 0, 3, 0);
            _clientIdTextBox.Name = "_clientIdTextBox";
            _clientIdTextBox.Size = new Size(425, 23);
            _clientIdTextBox.TabIndex = 1;
            _clientIdTextBox.UseSystemPasswordChar = true;
            _clientIdTextBox.TextChanged += OnSettingChanged;
            // 
            // _clientIdResetButton
            // 
            _clientIdResetButton.Dock = DockStyle.Right;
            _clientIdResetButton.Location = new Point(425, 0);
            _clientIdResetButton.Name = "_clientIdResetButton";
            _clientIdResetButton.Size = new Size(25, 29);
            _clientIdResetButton.TabIndex = 2;
            _clientIdResetButton.Text = "↺";
            _clientIdResetButton.UseVisualStyleBackColor = true;
            _clientIdResetButton.Click += OnClientIdResetButtonClicked;
            // 
            // _clientSecretLabel
            // 
            _clientSecretLabel.AutoSize = true;
            _clientSecretLabel.Dock = DockStyle.Fill;
            _clientSecretLabel.Location = new Point(3, 35);
            _clientSecretLabel.Name = "_clientSecretLabel";
            _clientSecretLabel.Size = new Size(76, 35);
            _clientSecretLabel.TabIndex = 3;
            _clientSecretLabel.Text = "Client Secret:";
            _clientSecretLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _clientSecretPanel
            // 
            _clientSecretPanel.Controls.Add(_clientSecretTextBox);
            _clientSecretPanel.Controls.Add(_clientSecretResetButton);
            _clientSecretPanel.Dock = DockStyle.Fill;
            _clientSecretPanel.Location = new Point(85, 38);
            _clientSecretPanel.Name = "_clientSecretPanel";
            _clientSecretPanel.Size = new Size(450, 29);
            _clientSecretPanel.TabIndex = 4;
            // 
            // _clientSecretTextBox
            // 
            _clientSecretTextBox.Dock = DockStyle.Fill;
            _clientSecretTextBox.Location = new Point(0, 0);
            _clientSecretTextBox.Margin = new Padding(0, 0, 3, 0);
            _clientSecretTextBox.Name = "_clientSecretTextBox";
            _clientSecretTextBox.Size = new Size(425, 23);
            _clientSecretTextBox.TabIndex = 4;
            _clientSecretTextBox.UseSystemPasswordChar = true;
            _clientSecretTextBox.TextChanged += OnSettingChanged;
            // 
            // _clientSecretResetButton
            // 
            _clientSecretResetButton.Dock = DockStyle.Right;
            _clientSecretResetButton.Location = new Point(425, 0);
            _clientSecretResetButton.Name = "_clientSecretResetButton";
            _clientSecretResetButton.Size = new Size(25, 29);
            _clientSecretResetButton.TabIndex = 5;
            _clientSecretResetButton.Text = "↺";
            _clientSecretResetButton.UseVisualStyleBackColor = true;
            _clientSecretResetButton.Click += OnClientSecretResetButtonClicked;
            // 
            // _redirectUriLabel
            // 
            _redirectUriLabel.AutoSize = true;
            _redirectUriLabel.Dock = DockStyle.Fill;
            _redirectUriLabel.Location = new Point(3, 70);
            _redirectUriLabel.Name = "_redirectUriLabel";
            _redirectUriLabel.Size = new Size(76, 35);
            _redirectUriLabel.TabIndex = 6;
            _redirectUriLabel.Text = "Redirect URI:";
            _redirectUriLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _redirectUriPanel
            // 
            _redirectUriPanel.Controls.Add(_redirectUriTextBox);
            _redirectUriPanel.Controls.Add(_redirectUriEditButton);
            _redirectUriPanel.Controls.Add(_redirectUriResetButton);
            _redirectUriPanel.Dock = DockStyle.Fill;
            _redirectUriPanel.Location = new Point(85, 73);
            _redirectUriPanel.Name = "_redirectUriPanel";
            _redirectUriPanel.Size = new Size(450, 29);
            _redirectUriPanel.TabIndex = 7;
            // 
            // _redirectUriTextBox
            // 
            _redirectUriTextBox.Dock = DockStyle.Fill;
            _redirectUriTextBox.Location = new Point(0, 0);
            _redirectUriTextBox.Margin = new Padding(0, 0, 3, 0);
            _redirectUriTextBox.Name = "_redirectUriTextBox";
            _redirectUriTextBox.ReadOnly = true;
            _redirectUriTextBox.Size = new Size(395, 23);
            _redirectUriTextBox.TabIndex = 7;
            _redirectUriTextBox.TextChanged += OnSettingChanged;
            // 
            // _redirectUriEditButton
            // 
            _redirectUriEditButton.Dock = DockStyle.Right;
            _redirectUriEditButton.Location = new Point(395, 0);
            _redirectUriEditButton.Name = "_redirectUriEditButton";
            _redirectUriEditButton.Size = new Size(30, 29);
            _redirectUriEditButton.TabIndex = 8;
            _redirectUriEditButton.Text = "✏";
            _redirectUriEditButton.UseVisualStyleBackColor = true;
            _redirectUriEditButton.Click += OnRedirectUriEditButtonClicked;
            // 
            // _redirectUriResetButton
            // 
            _redirectUriResetButton.Dock = DockStyle.Right;
            _redirectUriResetButton.Location = new Point(425, 0);
            _redirectUriResetButton.Name = "_redirectUriResetButton";
            _redirectUriResetButton.Size = new Size(25, 29);
            _redirectUriResetButton.TabIndex = 9;
            _redirectUriResetButton.Text = "↺";
            _redirectUriResetButton.UseVisualStyleBackColor = true;
            _redirectUriResetButton.Click += OnRedirectUriResetButtonClicked;
            // 
            // _scopesLabel
            // 
            _scopesLabel.AutoSize = true;
            _scopesLabel.Dock = DockStyle.Fill;
            _scopesLabel.Location = new Point(3, 105);
            _scopesLabel.Name = "_scopesLabel";
            _scopesLabel.Size = new Size(76, 35);
            _scopesLabel.TabIndex = 9;
            _scopesLabel.Text = "Scopes:";
            _scopesLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _scopesPanel
            // 
            _scopesPanel.Controls.Add(_scopesTextBox);
            _scopesPanel.Controls.Add(_scopesResetButton);
            _scopesPanel.Dock = DockStyle.Fill;
            _scopesPanel.Location = new Point(85, 108);
            _scopesPanel.Name = "_scopesPanel";
            _scopesPanel.Size = new Size(450, 29);
            _scopesPanel.TabIndex = 10;
            // 
            // _scopesTextBox
            // 
            _scopesTextBox.Dock = DockStyle.Fill;
            _scopesTextBox.Location = new Point(0, 0);
            _scopesTextBox.Margin = new Padding(0, 0, 3, 0);
            _scopesTextBox.Name = "_scopesTextBox";
            _scopesTextBox.Size = new Size(425, 23);
            _scopesTextBox.TabIndex = 10;
            _scopesTextBox.TextChanged += OnSettingChanged;
            // 
            // _scopesResetButton
            // 
            _scopesResetButton.Dock = DockStyle.Right;
            _scopesResetButton.Location = new Point(425, 0);
            _scopesResetButton.Name = "_scopesResetButton";
            _scopesResetButton.Size = new Size(25, 29);
            _scopesResetButton.TabIndex = 11;
            _scopesResetButton.Text = "↺";
            _scopesResetButton.UseVisualStyleBackColor = true;
            _scopesResetButton.Click += OnScopesResetButtonClicked;
            // 
            // _tokenSectionLabel
            // 
            _tokenSectionLabel.AutoSize = true;
            _tokenSectionLabel.Dock = DockStyle.Fill;
            _tokenSectionLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _tokenSectionLabel.Location = new Point(5, 130);
            _tokenSectionLabel.Margin = new Padding(0, 10, 0, 5);
            _tokenSectionLabel.Name = "_tokenSectionLabel";
            _tokenSectionLabel.Size = new Size(538, 15);
            _tokenSectionLabel.TabIndex = 13;
            _tokenSectionLabel.Text = "Управление токенами";
            _tokenSectionLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _tokenManagementSection
            // 
            _tokenManagementSection.AutoSize = true;
            _tokenManagementSection.ColumnCount = 3;
            _tokenManagementSection.ColumnStyles.Add(new ColumnStyle());
            _tokenManagementSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _tokenManagementSection.ColumnStyles.Add(new ColumnStyle());
            _tokenManagementSection.Controls.Add(_accessTokenLabel, 0, 0);
            _tokenManagementSection.Controls.Add(_accessTokenPanel, 1, 0);
            _tokenManagementSection.Controls.Add(_showTokenButton, 2, 0);
            _tokenManagementSection.Controls.Add(_refreshTokenLabel, 0, 1);
            _tokenManagementSection.Controls.Add(_refreshTokenPanel, 1, 1);
            _tokenManagementSection.Controls.Add(_tokenStatusLabel, 0, 2);
            _tokenManagementSection.Controls.Add(_tokenStatusValueLabel, 1, 2);
            _tokenManagementSection.Controls.Add(_lastRefreshLabel, 0, 3);
            _tokenManagementSection.Controls.Add(_lastRefreshValueLabel, 1, 3);
            _tokenManagementSection.Controls.Add(_tokenButtonsPanel, 0, 4);
            _tokenManagementSection.Dock = DockStyle.Fill;
            _tokenManagementSection.Location = new Point(5, 150);
            _tokenManagementSection.Margin = new Padding(0, 0, 0, 15);
            _tokenManagementSection.Name = "_tokenManagementSection";
            _tokenManagementSection.RowCount = 5;
            _tokenManagementSection.RowStyles.Add(new RowStyle());
            _tokenManagementSection.RowStyles.Add(new RowStyle());
            _tokenManagementSection.RowStyles.Add(new RowStyle());
            _tokenManagementSection.RowStyles.Add(new RowStyle());
            _tokenManagementSection.RowStyles.Add(new RowStyle());
            _tokenManagementSection.Size = new Size(538, 135);
            _tokenManagementSection.TabIndex = 14;
            // 
            // _accessTokenLabel
            // 
            _accessTokenLabel.AutoSize = true;
            _accessTokenLabel.Dock = DockStyle.Fill;
            _accessTokenLabel.Location = new Point(3, 0);
            _accessTokenLabel.Name = "_accessTokenLabel";
            _accessTokenLabel.Size = new Size(140, 35);
            _accessTokenLabel.TabIndex = 14;
            _accessTokenLabel.Text = "Access Token:";
            _accessTokenLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _accessTokenPanel
            // 
            _accessTokenPanel.Controls.Add(_accessTokenTextBox);
            _accessTokenPanel.Dock = DockStyle.Fill;
            _accessTokenPanel.Location = new Point(149, 3);
            _accessTokenPanel.Name = "_accessTokenPanel";
            _accessTokenPanel.Size = new Size(320, 29);
            _accessTokenPanel.TabIndex = 15;
            // 
            // _accessTokenTextBox
            // 
            _accessTokenTextBox.Dock = DockStyle.Fill;
            _accessTokenTextBox.Location = new Point(0, 0);
            _accessTokenTextBox.Name = "_accessTokenTextBox";
            _accessTokenTextBox.ReadOnly = true;
            _accessTokenTextBox.Size = new Size(320, 23);
            _accessTokenTextBox.TabIndex = 15;
            _accessTokenTextBox.UseSystemPasswordChar = true;
            // 
            // _showTokenButton
            // 
            _showTokenButton.Location = new Point(475, 3);
            _showTokenButton.Name = "_showTokenButton";
            _showTokenButton.Size = new Size(60, 23);
            _showTokenButton.TabIndex = 25;
            _showTokenButton.Text = "👁";
            _showTokenButton.UseVisualStyleBackColor = true;
            _showTokenButton.Click += OnShowTokenButtonClicked;
            // 
            // _refreshTokenLabel
            // 
            _refreshTokenLabel.AutoSize = true;
            _refreshTokenLabel.Dock = DockStyle.Fill;
            _refreshTokenLabel.Location = new Point(3, 35);
            _refreshTokenLabel.Name = "_refreshTokenLabel";
            _refreshTokenLabel.Size = new Size(140, 35);
            _refreshTokenLabel.TabIndex = 16;
            _refreshTokenLabel.Text = "Refresh Token:";
            _refreshTokenLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _refreshTokenPanel
            // 
            _refreshTokenPanel.Controls.Add(_refreshTokenTextBox);
            _refreshTokenPanel.Dock = DockStyle.Fill;
            _refreshTokenPanel.Location = new Point(149, 38);
            _refreshTokenPanel.Name = "_refreshTokenPanel";
            _refreshTokenPanel.Size = new Size(320, 29);
            _refreshTokenPanel.TabIndex = 26;
            // 
            // _refreshTokenTextBox
            // 
            _refreshTokenTextBox.Dock = DockStyle.Fill;
            _refreshTokenTextBox.Location = new Point(0, 0);
            _refreshTokenTextBox.Name = "_refreshTokenTextBox";
            _refreshTokenTextBox.ReadOnly = true;
            _refreshTokenTextBox.Size = new Size(320, 23);
            _refreshTokenTextBox.TabIndex = 17;
            _refreshTokenTextBox.UseSystemPasswordChar = true;
            // 
            // _tokenStatusLabel
            // 
            _tokenStatusLabel.AutoSize = true;
            _tokenStatusLabel.Dock = DockStyle.Fill;
            _tokenStatusLabel.Location = new Point(3, 70);
            _tokenStatusLabel.Name = "_tokenStatusLabel";
            _tokenStatusLabel.Size = new Size(140, 15);
            _tokenStatusLabel.TabIndex = 18;
            _tokenStatusLabel.Text = "Статус токена:";
            _tokenStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _tokenStatusValueLabel
            // 
            _tokenStatusValueLabel.AutoSize = true;
            _tokenStatusValueLabel.Dock = DockStyle.Fill;
            _tokenStatusValueLabel.ForeColor = Color.Gray;
            _tokenStatusValueLabel.Location = new Point(149, 70);
            _tokenStatusValueLabel.Name = "_tokenStatusValueLabel";
            _tokenStatusValueLabel.Size = new Size(320, 15);
            _tokenStatusValueLabel.TabIndex = 19;
            _tokenStatusValueLabel.Text = "Не проверен";
            _tokenStatusValueLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _lastRefreshLabel
            // 
            _lastRefreshLabel.AutoSize = true;
            _lastRefreshLabel.Dock = DockStyle.Fill;
            _lastRefreshLabel.Location = new Point(3, 85);
            _lastRefreshLabel.Name = "_lastRefreshLabel";
            _lastRefreshLabel.Size = new Size(140, 15);
            _lastRefreshLabel.TabIndex = 20;
            _lastRefreshLabel.Text = "Последнее обновление:";
            _lastRefreshLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _lastRefreshValueLabel
            // 
            _lastRefreshValueLabel.AutoSize = true;
            _lastRefreshValueLabel.Dock = DockStyle.Fill;
            _lastRefreshValueLabel.ForeColor = Color.Gray;
            _lastRefreshValueLabel.Location = new Point(149, 85);
            _lastRefreshValueLabel.Name = "_lastRefreshValueLabel";
            _lastRefreshValueLabel.Size = new Size(320, 15);
            _lastRefreshValueLabel.TabIndex = 21;
            _lastRefreshValueLabel.Text = "Неизвестно";
            _lastRefreshValueLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _tokenButtonsPanel
            // 
            _tokenButtonsPanel.AutoSize = true;
            _tokenManagementSection.SetColumnSpan(_tokenButtonsPanel, 3);
            _tokenButtonsPanel.Controls.Add(_validateTokenButton);
            _tokenButtonsPanel.Controls.Add(_refreshTokenButton);
            _tokenButtonsPanel.Controls.Add(_clearTokensButton);
            _tokenButtonsPanel.Dock = DockStyle.Fill;
            _tokenButtonsPanel.Location = new Point(0, 110);
            _tokenButtonsPanel.Margin = new Padding(0, 10, 0, 0);
            _tokenButtonsPanel.Name = "_tokenButtonsPanel";
            _tokenButtonsPanel.Size = new Size(538, 25);
            _tokenButtonsPanel.TabIndex = 27;
            // 
            // _validateTokenButton
            // 
            _validateTokenButton.AutoSize = true;
            _validateTokenButton.Location = new Point(0, 0);
            _validateTokenButton.Margin = new Padding(0, 0, 5, 0);
            _validateTokenButton.Name = "_validateTokenButton";
            _validateTokenButton.Size = new Size(100, 25);
            _validateTokenButton.TabIndex = 22;
            _validateTokenButton.Text = "Проверить";
            _validateTokenButton.UseVisualStyleBackColor = true;
            _validateTokenButton.Click += OnValidateTokenButtonClicked;
            // 
            // _refreshTokenButton
            // 
            _refreshTokenButton.AutoSize = true;
            _refreshTokenButton.Location = new Point(105, 0);
            _refreshTokenButton.Margin = new Padding(0, 0, 5, 0);
            _refreshTokenButton.Name = "_refreshTokenButton";
            _refreshTokenButton.Size = new Size(100, 25);
            _refreshTokenButton.TabIndex = 23;
            _refreshTokenButton.Text = "Обновить";
            _refreshTokenButton.UseVisualStyleBackColor = true;
            _refreshTokenButton.Click += OnRefreshTokenButtonClicked;
            // 
            // _clearTokensButton
            // 
            _clearTokensButton.AutoSize = true;
            _clearTokensButton.Location = new Point(210, 0);
            _clearTokensButton.Margin = new Padding(0, 0, 5, 0);
            _clearTokensButton.Name = "_clearTokensButton";
            _clearTokensButton.Size = new Size(100, 25);
            _clearTokensButton.TabIndex = 24;
            _clearTokensButton.Text = "Очистить";
            _clearTokensButton.UseVisualStyleBackColor = true;
            _clearTokensButton.Click += OnClearTokensButtonClicked;
            // 
            // _testAuthSection
            // 
            _testAuthSection.AutoSize = true;
            _testAuthSection.ColumnCount = 2;
            _testAuthSection.ColumnStyles.Add(new ColumnStyle());
            _testAuthSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _testAuthSection.Controls.Add(_testAuthButton, 0, 0);
            _testAuthSection.Controls.Add(_authStatusLabel, 1, 0);
            _testAuthSection.Dock = DockStyle.Fill;
            _testAuthSection.Location = new Point(5, 300);
            _testAuthSection.Margin = new Padding(0, 0, 0, 10);
            _testAuthSection.Name = "_testAuthSection";
            _testAuthSection.RowCount = 1;
            _testAuthSection.RowStyles.Add(new RowStyle());
            _testAuthSection.Size = new Size(538, 29);
            _testAuthSection.TabIndex = 15;
            // 
            // _testAuthButton
            // 
            _testAuthButton.Location = new Point(3, 3);
            _testAuthButton.Name = "_testAuthButton";
            _testAuthButton.Size = new Size(120, 23);
            _testAuthButton.TabIndex = 26;
            _testAuthButton.Text = "Тест авторизации";
            _testAuthButton.UseVisualStyleBackColor = true;
            _testAuthButton.Click += OnTestAuthButtonClicked;
            // 
            // _authStatusLabel
            // 
            _authStatusLabel.AutoSize = true;
            _authStatusLabel.Dock = DockStyle.Fill;
            _authStatusLabel.Location = new Point(136, 0);
            _authStatusLabel.Margin = new Padding(10, 0, 0, 0);
            _authStatusLabel.Name = "_authStatusLabel";
            _authStatusLabel.Size = new Size(402, 29);
            _authStatusLabel.TabIndex = 27;
            _authStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _oauthInfoLabel
            // 
            _oauthInfoLabel.AutoSize = true;
            _oauthInfoLabel.Dock = DockStyle.Fill;
            _oauthInfoLabel.ForeColor = Color.Gray;
            _oauthInfoLabel.Location = new Point(8, 339);
            _oauthInfoLabel.Name = "_oauthInfoLabel";
            _oauthInfoLabel.Size = new Size(532, 30);
            _oauthInfoLabel.TabIndex = 28;
            _oauthInfoLabel.Text = "Получите Client ID и Client Secret на https://dev.twitch.tv/console/apps. \r\nScopes разделяйте пробелами.";
            // 
            // OAuthSettingsControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(_oauthTableLayout);
            Name = "OAuthSettingsControl";
            Size = new Size(548, 458);
            _oauthTableLayout.ResumeLayout(false);
            _oauthTableLayout.PerformLayout();
            _oauthCredentialsSection.ResumeLayout(false);
            _oauthCredentialsSection.PerformLayout();
            _clientIdPanel.ResumeLayout(false);
            _clientIdPanel.PerformLayout();
            _clientSecretPanel.ResumeLayout(false);
            _clientSecretPanel.PerformLayout();
            _redirectUriPanel.ResumeLayout(false);
            _redirectUriPanel.PerformLayout();
            _scopesPanel.ResumeLayout(false);
            _scopesPanel.PerformLayout();
            _tokenManagementSection.ResumeLayout(false);
            _tokenManagementSection.PerformLayout();
            _accessTokenPanel.ResumeLayout(false);
            _accessTokenPanel.PerformLayout();
            _refreshTokenPanel.ResumeLayout(false);
            _refreshTokenPanel.PerformLayout();
            _tokenButtonsPanel.ResumeLayout(false);
            _tokenButtonsPanel.PerformLayout();
            _testAuthSection.ResumeLayout(false);
            _testAuthSection.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel _oauthTableLayout;
        private TableLayoutPanel _oauthCredentialsSection;
        private TableLayoutPanel _tokenManagementSection;
        private FlowLayoutPanel _tokenButtonsPanel;
        private TableLayoutPanel _testAuthSection;
        private Panel _clientIdPanel;
        private Panel _clientSecretPanel;
        private Panel _redirectUriPanel;
        private Panel _scopesPanel;
        private Panel _accessTokenPanel;
        private Panel _refreshTokenPanel;
        private Label _clientIdLabel;
        private TextBox _clientIdTextBox;
        private Button _clientIdResetButton;
        private Label _clientSecretLabel;
        private TextBox _clientSecretTextBox;
        private Button _clientSecretResetButton;
        private Label _redirectUriLabel;
        private TextBox _redirectUriTextBox;
        private Button _redirectUriEditButton;
        private Button _redirectUriResetButton;
        private Label _scopesLabel;
        private TextBox _scopesTextBox;
        private Button _scopesResetButton;
        private Label _tokenSectionLabel;
        private Label _accessTokenLabel;
        private TextBox _accessTokenTextBox;
        private Label _refreshTokenLabel;
        private TextBox _refreshTokenTextBox;
        private Button _showTokenButton;
        private Label _tokenStatusLabel;
        private Label _tokenStatusValueLabel;
        private Label _lastRefreshLabel;
        private Label _lastRefreshValueLabel;
        private Button _validateTokenButton;
        private Button _refreshTokenButton;
        private Button _clearTokensButton;
        private Button _testAuthButton;
        private Label _authStatusLabel;
        private Label _oauthInfoLabel;
    }
}
