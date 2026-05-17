namespace PoproshaykaBot.WinForms.Forms.Settings;

partial class ObsIntegrationSettingsControl
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
        _rootLayout = new TableLayoutPanel();
        _enabledCheckBox = new CheckBox();
        _autoConnectCheckBox = new CheckBox();
        _autoProvisionCheckBox = new CheckBox();
        _syncSceneOnProfileCheckBox = new CheckBox();
        _syncProfileOnSceneCheckBox = new CheckBox();
        _formLayout = new TableLayoutPanel();
        _hostLabel = new Label();
        _hostTextBox = new TextBox();
        _portLabel = new Label();
        _portNumeric = new NumericUpDown();
        _passwordLabel = new Label();
        _passwordTextBox = new TextBox();
        _sceneLabel = new Label();
        _sceneComboBox = new ComboBox();
        _microphoneLabel = new Label();
        _microphoneComboBox = new ComboBox();
        _volumeMeterDelayLabel = new Label();
        _volumeMeterDelayNumeric = new NumericUpDown();
        _sourceNameLabel = new Label();
        _sourceNameTextBox = new TextBox();
        _sizeLabel = new Label();
        _sizeFlowPanel = new FlowLayoutPanel();
        _widthNumeric = new NumericUpDown();
        _sizeSeparatorLabel = new Label();
        _heightNumeric = new NumericUpDown();
        _overlayUrlLabel = new Label();
        _overlayUrlTextBox = new TextBox();
        _statusLabel = new Label();
        _buttonPanel = new FlowLayoutPanel();
        _testConnectionButton = new Button();
        _reconnectButton = new Button();
        _loadScenesButton = new Button();
        _provisionButton = new Button();
        _copyUrlButton = new Button();
        _rootLayout.SuspendLayout();
        _formLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_portNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_volumeMeterDelayNumeric).BeginInit();
        _sizeFlowPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_widthNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_heightNumeric).BeginInit();
        _buttonPanel.SuspendLayout();
        SuspendLayout();
        //
        // _rootLayout
        //
        _rootLayout.ColumnCount = 1;
        _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _rootLayout.Controls.Add(_enabledCheckBox, 0, 0);
        _rootLayout.Controls.Add(_autoConnectCheckBox, 0, 1);
        _rootLayout.Controls.Add(_autoProvisionCheckBox, 0, 2);
        _rootLayout.Controls.Add(_syncSceneOnProfileCheckBox, 0, 3);
        _rootLayout.Controls.Add(_syncProfileOnSceneCheckBox, 0, 4);
        _rootLayout.Controls.Add(_formLayout, 0, 5);
        _rootLayout.Controls.Add(_statusLabel, 0, 6);
        _rootLayout.Controls.Add(_buttonPanel, 0, 7);
        _rootLayout.Dock = DockStyle.Fill;
        _rootLayout.Location = new Point(0, 0);
        _rootLayout.Name = "_rootLayout";
        _rootLayout.Padding = new Padding(4, 4, 4, 4);
        _rootLayout.RowCount = 9;
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 320F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _rootLayout.Size = new Size(600, 536);
        _rootLayout.TabIndex = 0;
        //
        // _enabledCheckBox
        //
        _enabledCheckBox.AutoSize = true;
        _enabledCheckBox.Dock = DockStyle.Fill;
        _enabledCheckBox.Location = new Point(7, 7);
        _enabledCheckBox.Name = "_enabledCheckBox";
        _enabledCheckBox.Size = new Size(586, 22);
        _enabledCheckBox.TabIndex = 0;
        _enabledCheckBox.Text = "Включить интеграцию с OBS";
        _enabledCheckBox.UseVisualStyleBackColor = true;
        _enabledCheckBox.CheckedChanged += OnSettingChanged;
        //
        // _autoConnectCheckBox
        //
        _autoConnectCheckBox.AutoSize = true;
        _autoConnectCheckBox.Dock = DockStyle.Fill;
        _autoConnectCheckBox.Location = new Point(7, 35);
        _autoConnectCheckBox.Name = "_autoConnectCheckBox";
        _autoConnectCheckBox.Size = new Size(586, 22);
        _autoConnectCheckBox.TabIndex = 1;
        _autoConnectCheckBox.Text = "Подключаться к OBS при запуске приложения";
        _autoConnectCheckBox.UseVisualStyleBackColor = true;
        _autoConnectCheckBox.CheckedChanged += OnSettingChanged;
        //
        // _autoProvisionCheckBox
        //
        _autoProvisionCheckBox.AutoSize = true;
        _autoProvisionCheckBox.Dock = DockStyle.Fill;
        _autoProvisionCheckBox.Location = new Point(7, 63);
        _autoProvisionCheckBox.Name = "_autoProvisionCheckBox";
        _autoProvisionCheckBox.Size = new Size(586, 22);
        _autoProvisionCheckBox.TabIndex = 2;
        _autoProvisionCheckBox.Text = "Автоматически создавать или обновлять Browser Source";
        _autoProvisionCheckBox.UseVisualStyleBackColor = true;
        _autoProvisionCheckBox.CheckedChanged += OnSettingChanged;
        //
        // _syncSceneOnProfileCheckBox
        //
        _syncSceneOnProfileCheckBox.AutoSize = true;
        _syncSceneOnProfileCheckBox.Dock = DockStyle.Fill;
        _syncSceneOnProfileCheckBox.Location = new Point(7, 91);
        _syncSceneOnProfileCheckBox.Name = "_syncSceneOnProfileCheckBox";
        _syncSceneOnProfileCheckBox.Size = new Size(586, 22);
        _syncSceneOnProfileCheckBox.TabIndex = 3;
        _syncSceneOnProfileCheckBox.Text = "Переключать сцену OBS при смене профиля";
        _syncSceneOnProfileCheckBox.UseVisualStyleBackColor = true;
        _syncSceneOnProfileCheckBox.CheckedChanged += OnSettingChanged;
        //
        // _syncProfileOnSceneCheckBox
        //
        _syncProfileOnSceneCheckBox.AutoSize = true;
        _syncProfileOnSceneCheckBox.Dock = DockStyle.Fill;
        _syncProfileOnSceneCheckBox.Location = new Point(7, 119);
        _syncProfileOnSceneCheckBox.Name = "_syncProfileOnSceneCheckBox";
        _syncProfileOnSceneCheckBox.Size = new Size(586, 22);
        _syncProfileOnSceneCheckBox.TabIndex = 4;
        _syncProfileOnSceneCheckBox.Text = "Применять профиль при смене сцены OBS";
        _syncProfileOnSceneCheckBox.UseVisualStyleBackColor = true;
        _syncProfileOnSceneCheckBox.CheckedChanged += OnSettingChanged;
        //
        // _formLayout
        //
        _formLayout.ColumnCount = 2;
        _formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        _formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _formLayout.Controls.Add(_hostLabel, 0, 0);
        _formLayout.Controls.Add(_hostTextBox, 1, 0);
        _formLayout.Controls.Add(_portLabel, 0, 1);
        _formLayout.Controls.Add(_portNumeric, 1, 1);
        _formLayout.Controls.Add(_passwordLabel, 0, 2);
        _formLayout.Controls.Add(_passwordTextBox, 1, 2);
        _formLayout.Controls.Add(_sceneLabel, 0, 3);
        _formLayout.Controls.Add(_sceneComboBox, 1, 3);
        _formLayout.Controls.Add(_microphoneLabel, 0, 4);
        _formLayout.Controls.Add(_microphoneComboBox, 1, 4);
        _formLayout.Controls.Add(_volumeMeterDelayLabel, 0, 5);
        _formLayout.Controls.Add(_volumeMeterDelayNumeric, 1, 5);
        _formLayout.Controls.Add(_sourceNameLabel, 0, 6);
        _formLayout.Controls.Add(_sourceNameTextBox, 1, 6);
        _formLayout.Controls.Add(_sizeLabel, 0, 7);
        _formLayout.Controls.Add(_sizeFlowPanel, 1, 7);
        _formLayout.Controls.Add(_overlayUrlLabel, 0, 8);
        _formLayout.Controls.Add(_overlayUrlTextBox, 1, 8);
        _formLayout.Dock = DockStyle.Fill;
        _formLayout.Location = new Point(7, 91);
        _formLayout.Name = "_formLayout";
        _formLayout.RowCount = 10;
        _formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _formLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _formLayout.Size = new Size(586, 314);
        _formLayout.TabIndex = 5;
        //
        // _hostLabel
        //
        _hostLabel.AutoSize = true;
        _hostLabel.Dock = DockStyle.Fill;
        _hostLabel.Location = new Point(0, 0);
        _hostLabel.Margin = new Padding(0, 0, 6, 4);
        _hostLabel.Name = "_hostLabel";
        _hostLabel.Size = new Size(144, 28);
        _hostLabel.TabIndex = 0;
        _hostLabel.Text = "Хост:";
        _hostLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _hostTextBox
        //
        _hostTextBox.Dock = DockStyle.Fill;
        _hostTextBox.Location = new Point(150, 3);
        _hostTextBox.Margin = new Padding(0, 3, 0, 4);
        _hostTextBox.Name = "_hostTextBox";
        _hostTextBox.PlaceholderText = "127.0.0.1";
        _hostTextBox.Size = new Size(436, 23);
        _hostTextBox.TabIndex = 1;
        _hostTextBox.TextChanged += OnConnectionFieldChanged;
        //
        // _portLabel
        //
        _portLabel.AutoSize = true;
        _portLabel.Dock = DockStyle.Fill;
        _portLabel.Location = new Point(0, 32);
        _portLabel.Margin = new Padding(0, 0, 6, 4);
        _portLabel.Name = "_portLabel";
        _portLabel.Size = new Size(144, 28);
        _portLabel.TabIndex = 2;
        _portLabel.Text = "Порт:";
        _portLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _portNumeric
        //
        _portNumeric.Dock = DockStyle.Left;
        _portNumeric.Location = new Point(150, 35);
        _portNumeric.Margin = new Padding(0, 3, 0, 4);
        _portNumeric.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
        _portNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _portNumeric.Name = "_portNumeric";
        _portNumeric.Size = new Size(100, 23);
        _portNumeric.TabIndex = 3;
        _portNumeric.Value = new decimal(new int[] { 4455, 0, 0, 0 });
        _portNumeric.ValueChanged += OnConnectionFieldChanged;
        //
        // _passwordLabel
        //
        _passwordLabel.AutoSize = true;
        _passwordLabel.Dock = DockStyle.Fill;
        _passwordLabel.Location = new Point(0, 64);
        _passwordLabel.Margin = new Padding(0, 0, 6, 4);
        _passwordLabel.Name = "_passwordLabel";
        _passwordLabel.Size = new Size(144, 28);
        _passwordLabel.TabIndex = 4;
        _passwordLabel.Text = "Пароль:";
        _passwordLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _passwordTextBox
        //
        _passwordTextBox.Dock = DockStyle.Fill;
        _passwordTextBox.Location = new Point(150, 67);
        _passwordTextBox.Margin = new Padding(0, 3, 0, 4);
        _passwordTextBox.Name = "_passwordTextBox";
        _passwordTextBox.Size = new Size(436, 23);
        _passwordTextBox.TabIndex = 5;
        _passwordTextBox.UseSystemPasswordChar = true;
        _passwordTextBox.TextChanged += OnConnectionFieldChanged;
        //
        // _sceneLabel
        //
        _sceneLabel.AutoSize = true;
        _sceneLabel.Dock = DockStyle.Fill;
        _sceneLabel.Location = new Point(0, 96);
        _sceneLabel.Margin = new Padding(0, 0, 6, 4);
        _sceneLabel.Name = "_sceneLabel";
        _sceneLabel.Size = new Size(144, 28);
        _sceneLabel.TabIndex = 6;
        _sceneLabel.Text = "Сцена:";
        _sceneLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _sceneComboBox
        //
        _sceneComboBox.Dock = DockStyle.Fill;
        _sceneComboBox.FormattingEnabled = true;
        _sceneComboBox.Location = new Point(150, 99);
        _sceneComboBox.Margin = new Padding(0, 3, 0, 4);
        _sceneComboBox.Name = "_sceneComboBox";
        _sceneComboBox.Size = new Size(436, 23);
        _sceneComboBox.TabIndex = 7;
        _sceneComboBox.SelectedIndexChanged += OnSettingChanged;
        _sceneComboBox.TextChanged += OnSettingChanged;
        //
        // _microphoneLabel
        //
        _microphoneLabel.AutoSize = true;
        _microphoneLabel.Dock = DockStyle.Fill;
        _microphoneLabel.Location = new Point(0, 128);
        _microphoneLabel.Margin = new Padding(0, 0, 6, 4);
        _microphoneLabel.Name = "_microphoneLabel";
        _microphoneLabel.Size = new Size(144, 28);
        _microphoneLabel.TabIndex = 8;
        _microphoneLabel.Text = "Микрофон виджета:";
        _microphoneLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _microphoneComboBox
        //
        _microphoneComboBox.Dock = DockStyle.Fill;
        _microphoneComboBox.FormattingEnabled = true;
        _microphoneComboBox.Location = new Point(150, 131);
        _microphoneComboBox.Margin = new Padding(0, 3, 0, 4);
        _microphoneComboBox.Name = "_microphoneComboBox";
        _microphoneComboBox.Size = new Size(436, 23);
        _microphoneComboBox.TabIndex = 9;
        _microphoneComboBox.SelectedIndexChanged += OnSettingChanged;
        _microphoneComboBox.TextChanged += OnSettingChanged;
        //
        // _volumeMeterDelayLabel
        //
        _volumeMeterDelayLabel.AutoSize = true;
        _volumeMeterDelayLabel.Dock = DockStyle.Fill;
        _volumeMeterDelayLabel.Location = new Point(0, 160);
        _volumeMeterDelayLabel.Margin = new Padding(0, 0, 6, 4);
        _volumeMeterDelayLabel.Name = "_volumeMeterDelayLabel";
        _volumeMeterDelayLabel.Size = new Size(144, 28);
        _volumeMeterDelayLabel.TabIndex = 10;
        _volumeMeterDelayLabel.Text = "Задержка шкалы, мс:";
        _volumeMeterDelayLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _volumeMeterDelayNumeric
        //
        _volumeMeterDelayNumeric.Dock = DockStyle.Left;
        _volumeMeterDelayNumeric.Increment = new decimal(new int[] { 10, 0, 0, 0 });
        _volumeMeterDelayNumeric.Location = new Point(150, 163);
        _volumeMeterDelayNumeric.Margin = new Padding(0, 3, 0, 4);
        _volumeMeterDelayNumeric.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
        _volumeMeterDelayNumeric.Minimum = new decimal(new int[] { 30, 0, 0, 0 });
        _volumeMeterDelayNumeric.Name = "_volumeMeterDelayNumeric";
        _volumeMeterDelayNumeric.Size = new Size(100, 23);
        _volumeMeterDelayNumeric.TabIndex = 11;
        _volumeMeterDelayNumeric.Value = new decimal(new int[] { 120, 0, 0, 0 });
        _volumeMeterDelayNumeric.ValueChanged += OnSettingChanged;
        //
        // _sourceNameLabel
        //
        _sourceNameLabel.AutoSize = true;
        _sourceNameLabel.Dock = DockStyle.Fill;
        _sourceNameLabel.Location = new Point(0, 192);
        _sourceNameLabel.Margin = new Padding(0, 0, 6, 4);
        _sourceNameLabel.Name = "_sourceNameLabel";
        _sourceNameLabel.Size = new Size(144, 28);
        _sourceNameLabel.TabIndex = 12;
        _sourceNameLabel.Text = "Источник:";
        _sourceNameLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _sourceNameTextBox
        //
        _sourceNameTextBox.Dock = DockStyle.Fill;
        _sourceNameTextBox.Location = new Point(150, 195);
        _sourceNameTextBox.Margin = new Padding(0, 3, 0, 4);
        _sourceNameTextBox.Name = "_sourceNameTextBox";
        _sourceNameTextBox.PlaceholderText = "PoproshaykaBot Chat";
        _sourceNameTextBox.Size = new Size(436, 23);
        _sourceNameTextBox.TabIndex = 13;
        _sourceNameTextBox.TextChanged += OnSettingChanged;
        //
        // _sizeLabel
        //
        _sizeLabel.AutoSize = true;
        _sizeLabel.Dock = DockStyle.Fill;
        _sizeLabel.Location = new Point(0, 224);
        _sizeLabel.Margin = new Padding(0, 0, 6, 4);
        _sizeLabel.Name = "_sizeLabel";
        _sizeLabel.Size = new Size(144, 28);
        _sizeLabel.TabIndex = 14;
        _sizeLabel.Text = "Размер:";
        _sizeLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _sizeFlowPanel
        //
        _sizeFlowPanel.Controls.Add(_widthNumeric);
        _sizeFlowPanel.Controls.Add(_sizeSeparatorLabel);
        _sizeFlowPanel.Controls.Add(_heightNumeric);
        _sizeFlowPanel.Dock = DockStyle.Fill;
        _sizeFlowPanel.Location = new Point(150, 224);
        _sizeFlowPanel.Margin = new Padding(0, 0, 0, 4);
        _sizeFlowPanel.Name = "_sizeFlowPanel";
        _sizeFlowPanel.Size = new Size(436, 28);
        _sizeFlowPanel.TabIndex = 15;
        //
        // _widthNumeric
        //
        _widthNumeric.Location = new Point(0, 3);
        _widthNumeric.Margin = new Padding(0, 3, 4, 0);
        _widthNumeric.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
        _widthNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _widthNumeric.Name = "_widthNumeric";
        _widthNumeric.Size = new Size(90, 23);
        _widthNumeric.TabIndex = 0;
        _widthNumeric.Value = new decimal(new int[] { 1920, 0, 0, 0 });
        _widthNumeric.ValueChanged += OnSettingChanged;
        //
        // _sizeSeparatorLabel
        //
        _sizeSeparatorLabel.AutoSize = true;
        _sizeSeparatorLabel.Location = new Point(94, 6);
        _sizeSeparatorLabel.Margin = new Padding(0, 6, 4, 0);
        _sizeSeparatorLabel.Name = "_sizeSeparatorLabel";
        _sizeSeparatorLabel.Size = new Size(12, 15);
        _sizeSeparatorLabel.TabIndex = 1;
        _sizeSeparatorLabel.Text = "×";
        //
        // _heightNumeric
        //
        _heightNumeric.Location = new Point(110, 3);
        _heightNumeric.Margin = new Padding(0, 3, 0, 0);
        _heightNumeric.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
        _heightNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _heightNumeric.Name = "_heightNumeric";
        _heightNumeric.Size = new Size(90, 23);
        _heightNumeric.TabIndex = 2;
        _heightNumeric.Value = new decimal(new int[] { 1080, 0, 0, 0 });
        _heightNumeric.ValueChanged += OnSettingChanged;
        //
        // _overlayUrlLabel
        //
        _overlayUrlLabel.AutoSize = true;
        _overlayUrlLabel.Dock = DockStyle.Fill;
        _overlayUrlLabel.Location = new Point(0, 256);
        _overlayUrlLabel.Margin = new Padding(0, 0, 6, 4);
        _overlayUrlLabel.Name = "_overlayUrlLabel";
        _overlayUrlLabel.Size = new Size(144, 28);
        _overlayUrlLabel.TabIndex = 16;
        _overlayUrlLabel.Text = "URL оверлея:";
        _overlayUrlLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _overlayUrlTextBox
        //
        _overlayUrlTextBox.Dock = DockStyle.Fill;
        _overlayUrlTextBox.Location = new Point(150, 259);
        _overlayUrlTextBox.Margin = new Padding(0, 3, 0, 4);
        _overlayUrlTextBox.Name = "_overlayUrlTextBox";
        _overlayUrlTextBox.ReadOnly = true;
        _overlayUrlTextBox.Size = new Size(436, 23);
        _overlayUrlTextBox.TabIndex = 17;
        //
        // _statusLabel
        //
        _statusLabel.AutoEllipsis = true;
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Color.Gray;
        _statusLabel.Location = new Point(7, 408);
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new Size(586, 30);
        _statusLabel.TabIndex = 6;
        _statusLabel.Text = "● Не проверено";
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _buttonPanel
        //
        _buttonPanel.Controls.Add(_testConnectionButton);
        _buttonPanel.Controls.Add(_reconnectButton);
        _buttonPanel.Controls.Add(_loadScenesButton);
        _buttonPanel.Controls.Add(_provisionButton);
        _buttonPanel.Controls.Add(_copyUrlButton);
        _buttonPanel.Dock = DockStyle.Fill;
        _buttonPanel.Location = new Point(7, 441);
        _buttonPanel.Name = "_buttonPanel";
        _buttonPanel.Size = new Size(586, 28);
        _buttonPanel.TabIndex = 7;
        //
        // _testConnectionButton
        //
        _testConnectionButton.Location = new Point(0, 0);
        _testConnectionButton.Margin = new Padding(0, 0, 3, 0);
        _testConnectionButton.Name = "_testConnectionButton";
        _testConnectionButton.Size = new Size(102, 27);
        _testConnectionButton.TabIndex = 0;
        _testConnectionButton.Text = "Проверить";
        _testConnectionButton.UseVisualStyleBackColor = true;
        _testConnectionButton.Click += OnTestConnectionButtonClicked;
        //
        // _reconnectButton
        //
        _reconnectButton.Location = new Point(105, 0);
        _reconnectButton.Margin = new Padding(0, 0, 3, 0);
        _reconnectButton.Name = "_reconnectButton";
        _reconnectButton.Size = new Size(125, 27);
        _reconnectButton.TabIndex = 1;
        _reconnectButton.Text = "Переподключить";
        _reconnectButton.UseVisualStyleBackColor = true;
        _reconnectButton.Click += OnReconnectButtonClicked;
        //
        // _loadScenesButton
        //
        _loadScenesButton.Location = new Point(233, 0);
        _loadScenesButton.Margin = new Padding(0, 0, 3, 0);
        _loadScenesButton.Name = "_loadScenesButton";
        _loadScenesButton.Size = new Size(105, 27);
        _loadScenesButton.TabIndex = 2;
        _loadScenesButton.Text = "Списки";
        _loadScenesButton.UseVisualStyleBackColor = true;
        _loadScenesButton.Click += OnLoadScenesButtonClicked;
        //
        // _provisionButton
        //
        _provisionButton.Location = new Point(341, 0);
        _provisionButton.Margin = new Padding(0, 0, 3, 0);
        _provisionButton.Name = "_provisionButton";
        _provisionButton.Size = new Size(130, 27);
        _provisionButton.TabIndex = 3;
        _provisionButton.Text = "Создать источник";
        _provisionButton.UseVisualStyleBackColor = true;
        _provisionButton.Click += OnProvisionButtonClicked;
        //
        // _copyUrlButton
        //
        _copyUrlButton.Location = new Point(474, 0);
        _copyUrlButton.Margin = new Padding(0, 0, 3, 0);
        _copyUrlButton.Name = "_copyUrlButton";
        _copyUrlButton.Size = new Size(110, 27);
        _copyUrlButton.TabIndex = 4;
        _copyUrlButton.Text = "Копировать URL";
        _copyUrlButton.UseVisualStyleBackColor = true;
        _copyUrlButton.Click += OnCopyUrlButtonClicked;
        //
        // ObsIntegrationSettingsControl
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_rootLayout);
        Name = "ObsIntegrationSettingsControl";
        Size = new Size(600, 536);
        _rootLayout.ResumeLayout(false);
        _rootLayout.PerformLayout();
        _formLayout.ResumeLayout(false);
        _formLayout.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_portNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)_volumeMeterDelayNumeric).EndInit();
        _sizeFlowPanel.ResumeLayout(false);
        _sizeFlowPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_widthNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)_heightNumeric).EndInit();
        _buttonPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel _rootLayout;
    private CheckBox _enabledCheckBox;
    private CheckBox _autoConnectCheckBox;
    private CheckBox _autoProvisionCheckBox;
    private CheckBox _syncSceneOnProfileCheckBox;
    private CheckBox _syncProfileOnSceneCheckBox;
    private TableLayoutPanel _formLayout;
    private Label _hostLabel;
    private TextBox _hostTextBox;
    private Label _portLabel;
    private NumericUpDown _portNumeric;
    private Label _passwordLabel;
    private TextBox _passwordTextBox;
    private Label _sceneLabel;
    private ComboBox _sceneComboBox;
    private Label _microphoneLabel;
    private ComboBox _microphoneComboBox;
    private Label _volumeMeterDelayLabel;
    private NumericUpDown _volumeMeterDelayNumeric;
    private Label _sourceNameLabel;
    private TextBox _sourceNameTextBox;
    private Label _sizeLabel;
    private FlowLayoutPanel _sizeFlowPanel;
    private NumericUpDown _widthNumeric;
    private Label _sizeSeparatorLabel;
    private NumericUpDown _heightNumeric;
    private Label _overlayUrlLabel;
    private TextBox _overlayUrlTextBox;
    private Label _statusLabel;
    private FlowLayoutPanel _buttonPanel;
    private Button _testConnectionButton;
    private Button _reconnectButton;
    private Button _loadScenesButton;
    private Button _provisionButton;
    private Button _copyUrlButton;
}
