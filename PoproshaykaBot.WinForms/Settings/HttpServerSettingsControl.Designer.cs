namespace PoproshaykaBot.WinForms.Settings
{
    partial class HttpServerSettingsControl
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
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(HttpServerSettingsControl));
            _httpServerTableLayout = new TableLayoutPanel();
            _httpServerEnabledCheckBox = new CheckBox();
            _portPanel = new Panel();
            _httpServerPortLabel = new Label();
            _httpServerPortNumeric = new NumericUpDown();
            _portResetButton = new Button();
            _obsOverlayEnabledCheckBox = new CheckBox();
            _separatorPanel1 = new Panel();
            _statusPanel = new Panel();
            _serverStatusLabel = new Label();
            _obsUrlLabel = new Label();
            _buttonsPanel = new Panel();
            _copyUrlButton = new Button();
            _restartServerButton = new Button();
            _separatorPanel2 = new Panel();
            _infoPanel = new Panel();
            _infoLabel = new Label();
            _httpServerTableLayout.SuspendLayout();
            _portPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_httpServerPortNumeric).BeginInit();
            _statusPanel.SuspendLayout();
            _buttonsPanel.SuspendLayout();
            _infoPanel.SuspendLayout();
            SuspendLayout();
            // 
            // _httpServerTableLayout
            // 
            _httpServerTableLayout.ColumnCount = 1;
            _httpServerTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _httpServerTableLayout.Controls.Add(_httpServerEnabledCheckBox, 0, 0);
            _httpServerTableLayout.Controls.Add(_portPanel, 0, 1);
            _httpServerTableLayout.Controls.Add(_obsOverlayEnabledCheckBox, 0, 2);
            _httpServerTableLayout.Controls.Add(_separatorPanel1, 0, 3);
            _httpServerTableLayout.Controls.Add(_statusPanel, 0, 4);
            _httpServerTableLayout.Controls.Add(_buttonsPanel, 0, 5);
            _httpServerTableLayout.Controls.Add(_separatorPanel2, 0, 6);
            _httpServerTableLayout.Controls.Add(_infoPanel, 0, 7);
            _httpServerTableLayout.Dock = DockStyle.Fill;
            _httpServerTableLayout.Location = new Point(0, 0);
            _httpServerTableLayout.Name = "_httpServerTableLayout";
            _httpServerTableLayout.RowCount = 8;
            _httpServerTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _httpServerTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            _httpServerTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _httpServerTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10F));
            _httpServerTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            _httpServerTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            _httpServerTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10F));
            _httpServerTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _httpServerTableLayout.Size = new Size(548, 458);
            _httpServerTableLayout.TabIndex = 0;
            // 
            // _httpServerEnabledCheckBox
            // 
            _httpServerEnabledCheckBox.AutoSize = true;
            _httpServerEnabledCheckBox.Dock = DockStyle.Fill;
            _httpServerEnabledCheckBox.Location = new Point(3, 3);
            _httpServerEnabledCheckBox.Name = "_httpServerEnabledCheckBox";
            _httpServerEnabledCheckBox.Size = new Size(542, 24);
            _httpServerEnabledCheckBox.TabIndex = 0;
            _httpServerEnabledCheckBox.Text = "Включить HTTP сервер";
            _httpServerEnabledCheckBox.UseVisualStyleBackColor = true;
            _httpServerEnabledCheckBox.CheckedChanged += OnHttpServerEnabledChanged;
            // 
            // _portPanel
            // 
            _portPanel.Controls.Add(_httpServerPortLabel);
            _portPanel.Controls.Add(_httpServerPortNumeric);
            _portPanel.Controls.Add(_portResetButton);
            _portPanel.Dock = DockStyle.Fill;
            _portPanel.Location = new Point(3, 33);
            _portPanel.Name = "_portPanel";
            _portPanel.Size = new Size(542, 29);
            _portPanel.TabIndex = 1;
            // 
            // _httpServerPortLabel
            // 
            _httpServerPortLabel.AutoSize = true;
            _httpServerPortLabel.Location = new Point(0, 6);
            _httpServerPortLabel.Name = "_httpServerPortLabel";
            _httpServerPortLabel.Size = new Size(85, 15);
            _httpServerPortLabel.TabIndex = 0;
            _httpServerPortLabel.Text = "Порт сервера:";
            // 
            // _httpServerPortNumeric
            // 
            _httpServerPortNumeric.Location = new Point(95, 3);
            _httpServerPortNumeric.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            _httpServerPortNumeric.Minimum = new decimal(new int[] { 1024, 0, 0, 0 });
            _httpServerPortNumeric.Name = "_httpServerPortNumeric";
            _httpServerPortNumeric.Size = new Size(80, 23);
            _httpServerPortNumeric.TabIndex = 1;
            _httpServerPortNumeric.Value = new decimal(new int[] { 8080, 0, 0, 0 });
            _httpServerPortNumeric.ValueChanged += OnSettingChanged;
            // 
            // _portResetButton
            // 
            _portResetButton.Location = new Point(181, 2);
            _portResetButton.Name = "_portResetButton";
            _portResetButton.Size = new Size(25, 25);
            _portResetButton.TabIndex = 2;
            _portResetButton.Text = "↺";
            _portResetButton.UseVisualStyleBackColor = true;
            _portResetButton.Click += OnPortResetButtonClicked;
            // 
            // _obsOverlayEnabledCheckBox
            // 
            _obsOverlayEnabledCheckBox.AutoSize = true;
            _obsOverlayEnabledCheckBox.Dock = DockStyle.Fill;
            _obsOverlayEnabledCheckBox.Location = new Point(3, 68);
            _obsOverlayEnabledCheckBox.Name = "_obsOverlayEnabledCheckBox";
            _obsOverlayEnabledCheckBox.Size = new Size(542, 24);
            _obsOverlayEnabledCheckBox.TabIndex = 2;
            _obsOverlayEnabledCheckBox.Text = "Включить OBS overlay";
            _obsOverlayEnabledCheckBox.UseVisualStyleBackColor = true;
            _obsOverlayEnabledCheckBox.CheckedChanged += OnSettingChanged;
            // 
            // _separatorPanel1
            // 
            _separatorPanel1.BorderStyle = BorderStyle.Fixed3D;
            _separatorPanel1.Dock = DockStyle.Fill;
            _separatorPanel1.Location = new Point(3, 98);
            _separatorPanel1.Name = "_separatorPanel1";
            _separatorPanel1.Size = new Size(542, 4);
            _separatorPanel1.TabIndex = 3;
            // 
            // _statusPanel
            // 
            _statusPanel.Controls.Add(_serverStatusLabel);
            _statusPanel.Controls.Add(_obsUrlLabel);
            _statusPanel.Dock = DockStyle.Fill;
            _statusPanel.Location = new Point(3, 108);
            _statusPanel.Name = "_statusPanel";
            _statusPanel.Size = new Size(542, 44);
            _statusPanel.TabIndex = 4;
            // 
            // _serverStatusLabel
            // 
            _serverStatusLabel.AutoSize = true;
            _serverStatusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _serverStatusLabel.Location = new Point(0, 3);
            _serverStatusLabel.Name = "_serverStatusLabel";
            _serverStatusLabel.Size = new Size(142, 15);
            _serverStatusLabel.TabIndex = 0;
            _serverStatusLabel.Text = "Статус сервера: ○ Готов";
            // 
            // _obsUrlLabel
            // 
            _obsUrlLabel.AutoSize = true;
            _obsUrlLabel.Location = new Point(0, 23);
            _obsUrlLabel.Name = "_obsUrlLabel";
            _obsUrlLabel.Size = new Size(219, 15);
            _obsUrlLabel.TabIndex = 1;
            _obsUrlLabel.Text = "URL для OBS: http://localhost:8080/chat";
            // 
            // _buttonsPanel
            // 
            _buttonsPanel.Controls.Add(_copyUrlButton);
            _buttonsPanel.Controls.Add(_restartServerButton);
            _buttonsPanel.Dock = DockStyle.Fill;
            _buttonsPanel.Location = new Point(3, 158);
            _buttonsPanel.Name = "_buttonsPanel";
            _buttonsPanel.Size = new Size(542, 34);
            _buttonsPanel.TabIndex = 5;
            // 
            // _copyUrlButton
            // 
            _copyUrlButton.Location = new Point(0, 5);
            _copyUrlButton.Name = "_copyUrlButton";
            _copyUrlButton.Size = new Size(120, 25);
            _copyUrlButton.TabIndex = 0;
            _copyUrlButton.Text = "Копировать URL";
            _copyUrlButton.UseVisualStyleBackColor = true;
            _copyUrlButton.Click += OnCopyUrlButtonClicked;
            // 
            // _restartServerButton
            // 
            _restartServerButton.Location = new Point(130, 5);
            _restartServerButton.Name = "_restartServerButton";
            _restartServerButton.Size = new Size(140, 25);
            _restartServerButton.TabIndex = 1;
            _restartServerButton.Text = "Перезапустить сервер";
            _restartServerButton.UseVisualStyleBackColor = true;
            _restartServerButton.Click += OnRestartServerButtonClicked;
            // 
            // _separatorPanel2
            // 
            _separatorPanel2.BorderStyle = BorderStyle.Fixed3D;
            _separatorPanel2.Dock = DockStyle.Fill;
            _separatorPanel2.Location = new Point(3, 198);
            _separatorPanel2.Name = "_separatorPanel2";
            _separatorPanel2.Size = new Size(542, 4);
            _separatorPanel2.TabIndex = 6;
            // 
            // _infoPanel
            // 
            _infoPanel.Controls.Add(_infoLabel);
            _infoPanel.Dock = DockStyle.Fill;
            _infoPanel.Location = new Point(3, 208);
            _infoPanel.Name = "_infoPanel";
            _infoPanel.Size = new Size(542, 247);
            _infoPanel.TabIndex = 7;
            // 
            // _infoLabel
            // 
            _infoLabel.Dock = DockStyle.Fill;
            _infoLabel.Location = new Point(0, 0);
            _infoLabel.Name = "_infoLabel";
            _infoLabel.Size = new Size(542, 247);
            _infoLabel.TabIndex = 0;
            _infoLabel.Text = resources.GetString("_infoLabel.Text");
            // 
            // HttpServerSettingsControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(_httpServerTableLayout);
            Name = "HttpServerSettingsControl";
            Size = new Size(548, 458);
            _httpServerTableLayout.ResumeLayout(false);
            _httpServerTableLayout.PerformLayout();
            _portPanel.ResumeLayout(false);
            _portPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)_httpServerPortNumeric).EndInit();
            _statusPanel.ResumeLayout(false);
            _statusPanel.PerformLayout();
            _buttonsPanel.ResumeLayout(false);
            _infoPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel _httpServerTableLayout;
        private CheckBox _httpServerEnabledCheckBox;
        private Panel _portPanel;
        private Label _httpServerPortLabel;
        private NumericUpDown _httpServerPortNumeric;
        private Button _portResetButton;
        private CheckBox _obsOverlayEnabledCheckBox;
        private Panel _separatorPanel1;
        private Panel _statusPanel;
        private Label _serverStatusLabel;
        private Label _obsUrlLabel;
        private Panel _buttonsPanel;
        private Button _copyUrlButton;
        private Button _restartServerButton;
        private Panel _separatorPanel2;
        private Panel _infoPanel;
        private Label _infoLabel;
    }
}
