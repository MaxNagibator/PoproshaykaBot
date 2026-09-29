namespace PoproshaykaBot.WinForms.Forms.Settings
{
    partial class OAuthAccountSection
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
            _sectionLayout = new TableLayoutPanel();
            _sectionHeaderLabel = new Label();
            _scopesSection = new TableLayoutPanel();
            _scopesLabel = new Label();
            _scopesPanel = new Panel();
            _scopesTextBox = new TextBox();
            _scopesResetButton = new Button();
            _testAuthSection = new TableLayoutPanel();
            _testAuthButton = new Button();
            _authStatusLabel = new Label();
            _advancedSubPanel = new Panel();
            _tokenManagementSection = new TableLayoutPanel();
            _accessTokenLabel = new Label();
            _accessTokenPanel = new Panel();
            _accessTokenTextBox = new TextBox();
            _showTokenButton = new Button();
            _refreshTokenLabel = new Label();
            _refreshTokenPanel = new Panel();
            _refreshTokenTextBox = new TextBox();
            _accountLoginLabel = new Label();
            _accountLoginValueLabel = new Label();
            _tokenStatusLabel = new Label();
            _tokenStatusValueLabel = new Label();
            _lastRefreshLabel = new Label();
            _lastRefreshValueLabel = new Label();
            _tokenButtonsPanel = new FlowLayoutPanel();
            _validateTokenButton = new Button();
            _refreshTokenButton = new Button();
            _clearTokensButton = new Button();
            _sectionLayout.SuspendLayout();
            _scopesSection.SuspendLayout();
            _scopesPanel.SuspendLayout();
            _testAuthSection.SuspendLayout();
            _advancedSubPanel.SuspendLayout();
            _tokenManagementSection.SuspendLayout();
            _accessTokenPanel.SuspendLayout();
            _refreshTokenPanel.SuspendLayout();
            _tokenButtonsPanel.SuspendLayout();
            SuspendLayout();
            //
            // _sectionLayout
            //
            _sectionLayout.AutoSize = true;
            _sectionLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _sectionLayout.ColumnCount = 1;
            _sectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _sectionLayout.Controls.Add(_sectionHeaderLabel, 0, 0);
            _sectionLayout.Controls.Add(_testAuthSection, 0, 1);
            _sectionLayout.Controls.Add(_advancedSubPanel, 0, 2);
            _sectionLayout.Dock = DockStyle.Fill;
            _sectionLayout.Location = new Point(0, 0);
            _sectionLayout.Name = "_sectionLayout";
            _sectionLayout.RowCount = 3;
            _sectionLayout.RowStyles.Add(new RowStyle());
            _sectionLayout.RowStyles.Add(new RowStyle());
            _sectionLayout.RowStyles.Add(new RowStyle());
            _sectionLayout.Size = new Size(540, 245);
            _sectionLayout.TabIndex = 0;
            //
            // _sectionHeaderLabel
            //
            _sectionHeaderLabel.AutoSize = true;
            _sectionHeaderLabel.Dock = DockStyle.Fill;
            _sectionHeaderLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _sectionHeaderLabel.Location = new Point(0, 0);
            _sectionHeaderLabel.Margin = new Padding(0, 0, 0, 5);
            _sectionHeaderLabel.Name = "_sectionHeaderLabel";
            _sectionHeaderLabel.Size = new Size(540, 15);
            _sectionHeaderLabel.TabIndex = 0;
            _sectionHeaderLabel.Text = "Авторизация";
            _sectionHeaderLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _scopesSection
            //
            _scopesSection.AutoSize = true;
            _scopesSection.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _scopesSection.ColumnCount = 2;
            _scopesSection.ColumnStyles.Add(new ColumnStyle());
            _scopesSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _scopesSection.Controls.Add(_scopesLabel, 0, 0);
            _scopesSection.Controls.Add(_scopesPanel, 1, 0);
            _scopesSection.Dock = DockStyle.Top;
            _scopesSection.Location = new Point(0, 0);
            _scopesSection.Margin = new Padding(0, 0, 0, 8);
            _scopesSection.Name = "_scopesSection";
            _scopesSection.RowCount = 1;
            _scopesSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            _scopesSection.Size = new Size(540, 35);
            _scopesSection.TabIndex = 1;
            //
            // _scopesLabel
            //
            _scopesLabel.AutoSize = true;
            _scopesLabel.Dock = DockStyle.Fill;
            _scopesLabel.Location = new Point(3, 0);
            _scopesLabel.Name = "_scopesLabel";
            _scopesLabel.Size = new Size(76, 35);
            _scopesLabel.TabIndex = 0;
            _scopesLabel.Text = "Scopes:";
            _scopesLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _scopesPanel
            //
            _scopesPanel.Controls.Add(_scopesTextBox);
            _scopesPanel.Controls.Add(_scopesResetButton);
            _scopesPanel.Dock = DockStyle.Fill;
            _scopesPanel.Location = new Point(85, 3);
            _scopesPanel.Name = "_scopesPanel";
            _scopesPanel.Size = new Size(452, 29);
            _scopesPanel.TabIndex = 1;
            //
            // _scopesTextBox
            //
            _scopesTextBox.Dock = DockStyle.Fill;
            _scopesTextBox.Location = new Point(0, 0);
            _scopesTextBox.Margin = new Padding(0, 0, 3, 0);
            _scopesTextBox.Name = "_scopesTextBox";
            _scopesTextBox.Size = new Size(427, 23);
            _scopesTextBox.TabIndex = 1;
            _scopesTextBox.TextChanged += OnScopesTextChanged;
            //
            // _scopesResetButton
            //
            _scopesResetButton.Dock = DockStyle.Right;
            _scopesResetButton.Location = new Point(427, 0);
            _scopesResetButton.Name = "_scopesResetButton";
            _scopesResetButton.Size = new Size(25, 29);
            _scopesResetButton.TabIndex = 2;
            _scopesResetButton.Text = "↺";
            _scopesResetButton.UseVisualStyleBackColor = true;
            _scopesResetButton.Click += OnScopesResetButtonClicked;
            //
            // _testAuthSection
            //
            _testAuthSection.AutoSize = true;
            _testAuthSection.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _testAuthSection.ColumnCount = 2;
            _testAuthSection.ColumnStyles.Add(new ColumnStyle());
            _testAuthSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _testAuthSection.Controls.Add(_testAuthButton, 0, 0);
            _testAuthSection.Controls.Add(_authStatusLabel, 1, 0);
            _testAuthSection.Dock = DockStyle.Fill;
            _testAuthSection.Location = new Point(0, 63);
            _testAuthSection.Margin = new Padding(0, 0, 0, 8);
            _testAuthSection.Name = "_testAuthSection";
            _testAuthSection.RowCount = 1;
            _testAuthSection.RowStyles.Add(new RowStyle());
            _testAuthSection.Size = new Size(540, 29);
            _testAuthSection.TabIndex = 2;
            //
            // _testAuthButton
            //
            _testAuthButton.Location = new Point(3, 3);
            _testAuthButton.Name = "_testAuthButton";
            _testAuthButton.Size = new Size(160, 23);
            _testAuthButton.TabIndex = 0;
            _testAuthButton.Text = "Авторизовать";
            _testAuthButton.UseVisualStyleBackColor = true;
            _testAuthButton.Click += OnTestAuthButtonClicked;
            //
            // _authStatusLabel
            //
            _authStatusLabel.AutoSize = true;
            _authStatusLabel.Dock = DockStyle.Fill;
            _authStatusLabel.Location = new Point(176, 0);
            _authStatusLabel.Margin = new Padding(10, 0, 0, 0);
            _authStatusLabel.Name = "_authStatusLabel";
            _authStatusLabel.Size = new Size(364, 29);
            _authStatusLabel.TabIndex = 1;
            _authStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _advancedSubPanel
            //
            _advancedSubPanel.AutoSize = true;
            _advancedSubPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _advancedSubPanel.Controls.Add(_tokenManagementSection);
            _advancedSubPanel.Controls.Add(_scopesSection);
            _advancedSubPanel.Dock = DockStyle.Fill;
            _advancedSubPanel.Margin = new Padding(0);
            _advancedSubPanel.Name = "_advancedSubPanel";
            _advancedSubPanel.Size = new Size(540, 180);
            _advancedSubPanel.TabIndex = 4;
            //
            // _tokenManagementSection
            //
            _tokenManagementSection.AutoSize = true;
            _tokenManagementSection.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _tokenManagementSection.ColumnCount = 3;
            _tokenManagementSection.ColumnStyles.Add(new ColumnStyle());
            _tokenManagementSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _tokenManagementSection.ColumnStyles.Add(new ColumnStyle());
            _tokenManagementSection.Controls.Add(_accessTokenLabel, 0, 0);
            _tokenManagementSection.Controls.Add(_accessTokenPanel, 1, 0);
            _tokenManagementSection.Controls.Add(_showTokenButton, 2, 0);
            _tokenManagementSection.Controls.Add(_refreshTokenLabel, 0, 1);
            _tokenManagementSection.Controls.Add(_refreshTokenPanel, 1, 1);
            _tokenManagementSection.Controls.Add(_accountLoginLabel, 0, 2);
            _tokenManagementSection.Controls.Add(_accountLoginValueLabel, 1, 2);
            _tokenManagementSection.Controls.Add(_tokenStatusLabel, 0, 3);
            _tokenManagementSection.Controls.Add(_tokenStatusValueLabel, 1, 3);
            _tokenManagementSection.Controls.Add(_lastRefreshLabel, 0, 4);
            _tokenManagementSection.Controls.Add(_lastRefreshValueLabel, 1, 4);
            _tokenManagementSection.Controls.Add(_tokenButtonsPanel, 0, 5);
            _tokenManagementSection.Dock = DockStyle.Top;
            _tokenManagementSection.Location = new Point(0, 35);
            _tokenManagementSection.Margin = new Padding(0);
            _tokenManagementSection.Name = "_tokenManagementSection";
            _tokenManagementSection.RowCount = 6;
            _tokenManagementSection.RowStyles.Add(new RowStyle());
            _tokenManagementSection.RowStyles.Add(new RowStyle());
            _tokenManagementSection.RowStyles.Add(new RowStyle());
            _tokenManagementSection.RowStyles.Add(new RowStyle());
            _tokenManagementSection.RowStyles.Add(new RowStyle());
            _tokenManagementSection.RowStyles.Add(new RowStyle());
            _tokenManagementSection.Size = new Size(540, 145);
            _tokenManagementSection.TabIndex = 3;
            //
            // _accessTokenLabel
            //
            _accessTokenLabel.AutoSize = true;
            _accessTokenLabel.Dock = DockStyle.Fill;
            _accessTokenLabel.Location = new Point(3, 0);
            _accessTokenLabel.Name = "_accessTokenLabel";
            _accessTokenLabel.Size = new Size(140, 35);
            _accessTokenLabel.TabIndex = 0;
            _accessTokenLabel.Text = "Access Token:";
            _accessTokenLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _accessTokenPanel
            //
            _accessTokenPanel.Controls.Add(_accessTokenTextBox);
            _accessTokenPanel.Dock = DockStyle.Fill;
            _accessTokenPanel.Location = new Point(149, 3);
            _accessTokenPanel.Name = "_accessTokenPanel";
            _accessTokenPanel.Size = new Size(322, 29);
            _accessTokenPanel.TabIndex = 1;
            //
            // _accessTokenTextBox
            //
            _accessTokenTextBox.Dock = DockStyle.Fill;
            _accessTokenTextBox.Location = new Point(0, 0);
            _accessTokenTextBox.Name = "_accessTokenTextBox";
            _accessTokenTextBox.ReadOnly = true;
            _accessTokenTextBox.Size = new Size(322, 23);
            _accessTokenTextBox.TabIndex = 0;
            _accessTokenTextBox.UseSystemPasswordChar = true;
            //
            // _showTokenButton
            //
            _showTokenButton.Location = new Point(477, 3);
            _showTokenButton.Name = "_showTokenButton";
            _showTokenButton.Size = new Size(60, 23);
            _showTokenButton.TabIndex = 2;
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
            _refreshTokenLabel.TabIndex = 3;
            _refreshTokenLabel.Text = "Refresh Token:";
            _refreshTokenLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _refreshTokenPanel
            //
            _refreshTokenPanel.Controls.Add(_refreshTokenTextBox);
            _refreshTokenPanel.Dock = DockStyle.Fill;
            _refreshTokenPanel.Location = new Point(149, 38);
            _refreshTokenPanel.Name = "_refreshTokenPanel";
            _refreshTokenPanel.Size = new Size(322, 29);
            _refreshTokenPanel.TabIndex = 4;
            //
            // _refreshTokenTextBox
            //
            _refreshTokenTextBox.Dock = DockStyle.Fill;
            _refreshTokenTextBox.Location = new Point(0, 0);
            _refreshTokenTextBox.Name = "_refreshTokenTextBox";
            _refreshTokenTextBox.ReadOnly = true;
            _refreshTokenTextBox.Size = new Size(322, 23);
            _refreshTokenTextBox.TabIndex = 0;
            _refreshTokenTextBox.UseSystemPasswordChar = true;
            //
            // _accountLoginLabel
            //
            _accountLoginLabel.AutoSize = true;
            _accountLoginLabel.Dock = DockStyle.Fill;
            _accountLoginLabel.Location = new Point(3, 70);
            _accountLoginLabel.Name = "_accountLoginLabel";
            _accountLoginLabel.Size = new Size(140, 15);
            _accountLoginLabel.TabIndex = 5;
            _accountLoginLabel.Text = "Авторизован как:";
            _accountLoginLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _accountLoginValueLabel
            //
            _accountLoginValueLabel.AutoSize = true;
            _accountLoginValueLabel.Dock = DockStyle.Fill;
            _accountLoginValueLabel.ForeColor = Color.Gray;
            _accountLoginValueLabel.Location = new Point(149, 70);
            _accountLoginValueLabel.Name = "_accountLoginValueLabel";
            _accountLoginValueLabel.Size = new Size(322, 15);
            _accountLoginValueLabel.TabIndex = 6;
            _accountLoginValueLabel.Text = "–";
            _accountLoginValueLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _tokenStatusLabel
            //
            _tokenStatusLabel.AutoSize = true;
            _tokenStatusLabel.Dock = DockStyle.Fill;
            _tokenStatusLabel.Location = new Point(3, 85);
            _tokenStatusLabel.Name = "_tokenStatusLabel";
            _tokenStatusLabel.Size = new Size(140, 15);
            _tokenStatusLabel.TabIndex = 7;
            _tokenStatusLabel.Text = "Статус токена:";
            _tokenStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _tokenStatusValueLabel
            //
            _tokenStatusValueLabel.AutoSize = true;
            _tokenStatusValueLabel.Dock = DockStyle.Fill;
            _tokenStatusValueLabel.ForeColor = Color.Gray;
            _tokenStatusValueLabel.Location = new Point(149, 85);
            _tokenStatusValueLabel.Name = "_tokenStatusValueLabel";
            _tokenStatusValueLabel.Size = new Size(322, 15);
            _tokenStatusValueLabel.TabIndex = 8;
            _tokenStatusValueLabel.Text = "Не проверен";
            _tokenStatusValueLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _lastRefreshLabel
            //
            _lastRefreshLabel.AutoSize = true;
            _lastRefreshLabel.Dock = DockStyle.Fill;
            _lastRefreshLabel.Location = new Point(3, 100);
            _lastRefreshLabel.Name = "_lastRefreshLabel";
            _lastRefreshLabel.Size = new Size(140, 15);
            _lastRefreshLabel.TabIndex = 9;
            _lastRefreshLabel.Text = "Последнее обновление:";
            _lastRefreshLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _lastRefreshValueLabel
            //
            _lastRefreshValueLabel.AutoSize = true;
            _lastRefreshValueLabel.Dock = DockStyle.Fill;
            _lastRefreshValueLabel.ForeColor = Color.Gray;
            _lastRefreshValueLabel.Location = new Point(149, 100);
            _lastRefreshValueLabel.Name = "_lastRefreshValueLabel";
            _lastRefreshValueLabel.Size = new Size(322, 15);
            _lastRefreshValueLabel.TabIndex = 10;
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
            _tokenButtonsPanel.Location = new Point(0, 120);
            _tokenButtonsPanel.Margin = new Padding(0, 5, 0, 0);
            _tokenButtonsPanel.Name = "_tokenButtonsPanel";
            _tokenButtonsPanel.Size = new Size(540, 25);
            _tokenButtonsPanel.TabIndex = 11;
            //
            // _validateTokenButton
            //
            _validateTokenButton.AutoSize = true;
            _validateTokenButton.Location = new Point(0, 0);
            _validateTokenButton.Margin = new Padding(0, 0, 5, 0);
            _validateTokenButton.Name = "_validateTokenButton";
            _validateTokenButton.Size = new Size(100, 25);
            _validateTokenButton.TabIndex = 0;
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
            _refreshTokenButton.TabIndex = 1;
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
            _clearTokensButton.TabIndex = 2;
            _clearTokensButton.Text = "Очистить";
            _clearTokensButton.UseVisualStyleBackColor = true;
            _clearTokensButton.Click += OnClearTokensButtonClicked;
            //
            // OAuthAccountSection
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Controls.Add(_sectionLayout);
            Name = "OAuthAccountSection";
            Size = new Size(540, 245);
            _sectionLayout.ResumeLayout(false);
            _sectionLayout.PerformLayout();
            _scopesSection.ResumeLayout(false);
            _scopesSection.PerformLayout();
            _scopesPanel.ResumeLayout(false);
            _scopesPanel.PerformLayout();
            _testAuthSection.ResumeLayout(false);
            _testAuthSection.PerformLayout();
            _advancedSubPanel.ResumeLayout(false);
            _advancedSubPanel.PerformLayout();
            _tokenManagementSection.ResumeLayout(false);
            _tokenManagementSection.PerformLayout();
            _accessTokenPanel.ResumeLayout(false);
            _accessTokenPanel.PerformLayout();
            _refreshTokenPanel.ResumeLayout(false);
            _refreshTokenPanel.PerformLayout();
            _tokenButtonsPanel.ResumeLayout(false);
            _tokenButtonsPanel.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TableLayoutPanel _sectionLayout;
        private Label _sectionHeaderLabel;
        private TableLayoutPanel _scopesSection;
        private Label _scopesLabel;
        private Panel _scopesPanel;
        private TextBox _scopesTextBox;
        private Button _scopesResetButton;
        private TableLayoutPanel _testAuthSection;
        private Button _testAuthButton;
        private Label _authStatusLabel;
        private Panel _advancedSubPanel;
        private TableLayoutPanel _tokenManagementSection;
        private Label _accessTokenLabel;
        private Panel _accessTokenPanel;
        private TextBox _accessTokenTextBox;
        private Button _showTokenButton;
        private Label _refreshTokenLabel;
        private Panel _refreshTokenPanel;
        private TextBox _refreshTokenTextBox;
        private Label _accountLoginLabel;
        private Label _accountLoginValueLabel;
        private Label _tokenStatusLabel;
        private Label _tokenStatusValueLabel;
        private Label _lastRefreshLabel;
        private Label _lastRefreshValueLabel;
        private FlowLayoutPanel _tokenButtonsPanel;
        private Button _validateTokenButton;
        private Button _refreshTokenButton;
        private Button _clearTokensButton;
    }
}
