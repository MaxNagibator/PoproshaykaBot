namespace PoproshaykaBot.WinForms.Settings
{
    partial class MessagesSettingsControl
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
            _messagesTableLayout = new TableLayoutPanel();
            _welcomeMessageSection = new TableLayoutPanel();
            _welcomeMessageEnabledCheckBox = new CheckBox();
            _welcomeMessageLabel = new Label();
            _welcomeMessageResetButton = new Button();
            _welcomeMessageTextPanel = new Panel();
            _welcomeMessageTextBox = new TextBox();
            _farewellMessageSection = new TableLayoutPanel();
            _farewellMessageEnabledCheckBox = new CheckBox();
            _farewellMessageLabel = new Label();
            _farewellMessageResetButton = new Button();
            _farewellMessageTextPanel = new Panel();
            _farewellMessageTextBox = new TextBox();
            _connectionMessageSection = new TableLayoutPanel();
            _connectionMessageEnabledCheckBox = new CheckBox();
            _connectionMessageLabel = new Label();
            _connectionMessageResetButton = new Button();
            _connectionMessageTextPanel = new Panel();
            _connectionMessageTextBox = new TextBox();
            _disconnectionMessageSection = new TableLayoutPanel();
            _disconnectionMessageEnabledCheckBox = new CheckBox();
            _disconnectionMessageLabel = new Label();
            _disconnectionMessageResetButton = new Button();
            _disconnectionMessageTextPanel = new Panel();
            _disconnectionMessageTextBox = new TextBox();
            _messagesTableLayout.SuspendLayout();
            _welcomeMessageSection.SuspendLayout();
            _welcomeMessageTextPanel.SuspendLayout();
            _farewellMessageSection.SuspendLayout();
            _farewellMessageTextPanel.SuspendLayout();
            _connectionMessageSection.SuspendLayout();
            _connectionMessageTextPanel.SuspendLayout();
            _disconnectionMessageSection.SuspendLayout();
            _disconnectionMessageTextPanel.SuspendLayout();
            SuspendLayout();
            // 
            // _messagesTableLayout
            // 
            _messagesTableLayout.ColumnCount = 1;
            _messagesTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _messagesTableLayout.Controls.Add(_welcomeMessageSection, 0, 0);
            _messagesTableLayout.Controls.Add(_farewellMessageSection, 0, 1);
            _messagesTableLayout.Controls.Add(_connectionMessageSection, 0, 2);
            _messagesTableLayout.Controls.Add(_disconnectionMessageSection, 0, 3);
            _messagesTableLayout.Dock = DockStyle.Fill;
            _messagesTableLayout.Location = new Point(0, 0);
            _messagesTableLayout.Name = "_messagesTableLayout";
            _messagesTableLayout.Padding = new Padding(5);
            _messagesTableLayout.RowCount = 5;
            _messagesTableLayout.RowStyles.Add(new RowStyle());
            _messagesTableLayout.RowStyles.Add(new RowStyle());
            _messagesTableLayout.RowStyles.Add(new RowStyle());
            _messagesTableLayout.RowStyles.Add(new RowStyle());
            _messagesTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _messagesTableLayout.Size = new Size(548, 458);
            _messagesTableLayout.TabIndex = 0;
            // 
            // _welcomeMessageSection
            // 
            _welcomeMessageSection.ColumnCount = 2;
            _welcomeMessageSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _welcomeMessageSection.ColumnStyles.Add(new ColumnStyle());
            _welcomeMessageSection.Controls.Add(_welcomeMessageEnabledCheckBox, 0, 0);
            _welcomeMessageSection.Controls.Add(_welcomeMessageLabel, 0, 1);
            _welcomeMessageSection.Controls.Add(_welcomeMessageResetButton, 1, 1);
            _welcomeMessageSection.Controls.Add(_welcomeMessageTextPanel, 0, 2);
            _welcomeMessageSection.Dock = DockStyle.Fill;
            _welcomeMessageSection.Location = new Point(5, 5);
            _welcomeMessageSection.Margin = new Padding(0, 0, 0, 10);
            _welcomeMessageSection.Name = "_welcomeMessageSection";
            _welcomeMessageSection.RowCount = 3;
            _welcomeMessageSection.RowStyles.Add(new RowStyle());
            _welcomeMessageSection.RowStyles.Add(new RowStyle());
            _welcomeMessageSection.RowStyles.Add(new RowStyle());
            _welcomeMessageSection.Size = new Size(538, 100);
            _welcomeMessageSection.TabIndex = 0;
            // 
            // _welcomeMessageEnabledCheckBox
            // 
            _welcomeMessageEnabledCheckBox.AutoSize = true;
            _welcomeMessageSection.SetColumnSpan(_welcomeMessageEnabledCheckBox, 2);
            _welcomeMessageEnabledCheckBox.Dock = DockStyle.Fill;
            _welcomeMessageEnabledCheckBox.Location = new Point(3, 3);
            _welcomeMessageEnabledCheckBox.Name = "_welcomeMessageEnabledCheckBox";
            _welcomeMessageEnabledCheckBox.Size = new Size(532, 19);
            _welcomeMessageEnabledCheckBox.TabIndex = 0;
            _welcomeMessageEnabledCheckBox.Text = "Включить приветственные сообщения";
            _welcomeMessageEnabledCheckBox.UseVisualStyleBackColor = true;
            _welcomeMessageEnabledCheckBox.CheckedChanged += OnSettingChanged;
            // 
            // _welcomeMessageLabel
            // 
            _welcomeMessageLabel.AutoSize = true;
            _welcomeMessageLabel.Dock = DockStyle.Fill;
            _welcomeMessageLabel.Location = new Point(3, 25);
            _welcomeMessageLabel.Name = "_welcomeMessageLabel";
            _welcomeMessageLabel.Size = new Size(501, 29);
            _welcomeMessageLabel.TabIndex = 1;
            _welcomeMessageLabel.Text = "Текст приветствия:";
            _welcomeMessageLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _welcomeMessageResetButton
            // 
            _welcomeMessageResetButton.Location = new Point(510, 28);
            _welcomeMessageResetButton.Name = "_welcomeMessageResetButton";
            _welcomeMessageResetButton.Size = new Size(25, 23);
            _welcomeMessageResetButton.TabIndex = 3;
            _welcomeMessageResetButton.Text = "↺";
            _welcomeMessageResetButton.UseVisualStyleBackColor = true;
            _welcomeMessageResetButton.Click += OnWelcomeMessageResetButtonClicked;
            // 
            // _welcomeMessageTextPanel
            // 
            _welcomeMessageSection.SetColumnSpan(_welcomeMessageTextPanel, 2);
            _welcomeMessageTextPanel.Controls.Add(_welcomeMessageTextBox);
            _welcomeMessageTextPanel.Dock = DockStyle.Fill;
            _welcomeMessageTextPanel.Location = new Point(3, 57);
            _welcomeMessageTextPanel.Name = "_welcomeMessageTextPanel";
            _welcomeMessageTextPanel.Size = new Size(532, 66);
            _welcomeMessageTextPanel.TabIndex = 4;
            // 
            // _welcomeMessageTextBox
            // 
            _welcomeMessageTextBox.Dock = DockStyle.Fill;
            _welcomeMessageTextBox.Location = new Point(0, 0);
            _welcomeMessageTextBox.Multiline = true;
            _welcomeMessageTextBox.Name = "_welcomeMessageTextBox";
            _welcomeMessageTextBox.Size = new Size(532, 66);
            _welcomeMessageTextBox.TabIndex = 2;
            _welcomeMessageTextBox.TextChanged += OnSettingChanged;
            // 
            // _farewellMessageSection
            // 
            _farewellMessageSection.ColumnCount = 2;
            _farewellMessageSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _farewellMessageSection.ColumnStyles.Add(new ColumnStyle());
            _farewellMessageSection.Controls.Add(_farewellMessageEnabledCheckBox, 0, 0);
            _farewellMessageSection.Controls.Add(_farewellMessageLabel, 0, 1);
            _farewellMessageSection.Controls.Add(_farewellMessageResetButton, 1, 1);
            _farewellMessageSection.Controls.Add(_farewellMessageTextPanel, 0, 2);
            _farewellMessageSection.Dock = DockStyle.Fill;
            _farewellMessageSection.Location = new Point(5, 115);
            _farewellMessageSection.Margin = new Padding(0, 0, 0, 10);
            _farewellMessageSection.Name = "_farewellMessageSection";
            _farewellMessageSection.RowCount = 3;
            _farewellMessageSection.RowStyles.Add(new RowStyle());
            _farewellMessageSection.RowStyles.Add(new RowStyle());
            _farewellMessageSection.RowStyles.Add(new RowStyle());
            _farewellMessageSection.Size = new Size(538, 100);
            _farewellMessageSection.TabIndex = 1;
            // 
            // _farewellMessageEnabledCheckBox
            // 
            _farewellMessageEnabledCheckBox.AutoSize = true;
            _farewellMessageSection.SetColumnSpan(_farewellMessageEnabledCheckBox, 2);
            _farewellMessageEnabledCheckBox.Dock = DockStyle.Fill;
            _farewellMessageEnabledCheckBox.Location = new Point(3, 3);
            _farewellMessageEnabledCheckBox.Name = "_farewellMessageEnabledCheckBox";
            _farewellMessageEnabledCheckBox.Size = new Size(532, 19);
            _farewellMessageEnabledCheckBox.TabIndex = 4;
            _farewellMessageEnabledCheckBox.Text = "Включить прощальные сообщения";
            _farewellMessageEnabledCheckBox.UseVisualStyleBackColor = true;
            _farewellMessageEnabledCheckBox.CheckedChanged += OnSettingChanged;
            // 
            // _farewellMessageLabel
            // 
            _farewellMessageLabel.AutoSize = true;
            _farewellMessageLabel.Dock = DockStyle.Fill;
            _farewellMessageLabel.Location = new Point(3, 25);
            _farewellMessageLabel.Name = "_farewellMessageLabel";
            _farewellMessageLabel.Size = new Size(501, 29);
            _farewellMessageLabel.TabIndex = 5;
            _farewellMessageLabel.Text = "Текст прощания:";
            _farewellMessageLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _farewellMessageResetButton
            // 
            _farewellMessageResetButton.Location = new Point(510, 28);
            _farewellMessageResetButton.Name = "_farewellMessageResetButton";
            _farewellMessageResetButton.Size = new Size(25, 23);
            _farewellMessageResetButton.TabIndex = 7;
            _farewellMessageResetButton.Text = "↺";
            _farewellMessageResetButton.UseVisualStyleBackColor = true;
            _farewellMessageResetButton.Click += OnFarewellMessageResetButtonClicked;
            // 
            // _farewellMessageTextPanel
            // 
            _farewellMessageSection.SetColumnSpan(_farewellMessageTextPanel, 2);
            _farewellMessageTextPanel.Controls.Add(_farewellMessageTextBox);
            _farewellMessageTextPanel.Dock = DockStyle.Fill;
            _farewellMessageTextPanel.Location = new Point(3, 57);
            _farewellMessageTextPanel.Name = "_farewellMessageTextPanel";
            _farewellMessageTextPanel.Size = new Size(532, 66);
            _farewellMessageTextPanel.TabIndex = 8;
            // 
            // _farewellMessageTextBox
            // 
            _farewellMessageTextBox.Dock = DockStyle.Fill;
            _farewellMessageTextBox.Location = new Point(0, 0);
            _farewellMessageTextBox.Multiline = true;
            _farewellMessageTextBox.Name = "_farewellMessageTextBox";
            _farewellMessageTextBox.Size = new Size(532, 66);
            _farewellMessageTextBox.TabIndex = 6;
            _farewellMessageTextBox.TextChanged += OnSettingChanged;
            // 
            // _connectionMessageSection
            // 
            _connectionMessageSection.ColumnCount = 2;
            _connectionMessageSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _connectionMessageSection.ColumnStyles.Add(new ColumnStyle());
            _connectionMessageSection.Controls.Add(_connectionMessageEnabledCheckBox, 0, 0);
            _connectionMessageSection.Controls.Add(_connectionMessageLabel, 0, 1);
            _connectionMessageSection.Controls.Add(_connectionMessageResetButton, 1, 1);
            _connectionMessageSection.Controls.Add(_connectionMessageTextPanel, 0, 2);
            _connectionMessageSection.Dock = DockStyle.Fill;
            _connectionMessageSection.Location = new Point(5, 225);
            _connectionMessageSection.Margin = new Padding(0, 0, 0, 10);
            _connectionMessageSection.Name = "_connectionMessageSection";
            _connectionMessageSection.RowCount = 3;
            _connectionMessageSection.RowStyles.Add(new RowStyle());
            _connectionMessageSection.RowStyles.Add(new RowStyle());
            _connectionMessageSection.RowStyles.Add(new RowStyle());
            _connectionMessageSection.Size = new Size(538, 100);
            _connectionMessageSection.TabIndex = 2;
            // 
            // _connectionMessageEnabledCheckBox
            // 
            _connectionMessageEnabledCheckBox.AutoSize = true;
            _connectionMessageSection.SetColumnSpan(_connectionMessageEnabledCheckBox, 2);
            _connectionMessageEnabledCheckBox.Dock = DockStyle.Fill;
            _connectionMessageEnabledCheckBox.Location = new Point(3, 3);
            _connectionMessageEnabledCheckBox.Name = "_connectionMessageEnabledCheckBox";
            _connectionMessageEnabledCheckBox.Size = new Size(532, 19);
            _connectionMessageEnabledCheckBox.TabIndex = 8;
            _connectionMessageEnabledCheckBox.Text = "Включить сообщение при подключении";
            _connectionMessageEnabledCheckBox.UseVisualStyleBackColor = true;
            _connectionMessageEnabledCheckBox.CheckedChanged += OnSettingChanged;
            // 
            // _connectionMessageLabel
            // 
            _connectionMessageLabel.AutoSize = true;
            _connectionMessageLabel.Dock = DockStyle.Fill;
            _connectionMessageLabel.Location = new Point(3, 25);
            _connectionMessageLabel.Name = "_connectionMessageLabel";
            _connectionMessageLabel.Size = new Size(501, 29);
            _connectionMessageLabel.TabIndex = 9;
            _connectionMessageLabel.Text = "Текст при подключении:";
            _connectionMessageLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _connectionMessageResetButton
            // 
            _connectionMessageResetButton.Location = new Point(510, 28);
            _connectionMessageResetButton.Name = "_connectionMessageResetButton";
            _connectionMessageResetButton.Size = new Size(25, 23);
            _connectionMessageResetButton.TabIndex = 11;
            _connectionMessageResetButton.Text = "↺";
            _connectionMessageResetButton.UseVisualStyleBackColor = true;
            _connectionMessageResetButton.Click += OnConnectionMessageResetButtonClicked;
            // 
            // _connectionMessageTextPanel
            // 
            _connectionMessageSection.SetColumnSpan(_connectionMessageTextPanel, 2);
            _connectionMessageTextPanel.Controls.Add(_connectionMessageTextBox);
            _connectionMessageTextPanel.Dock = DockStyle.Fill;
            _connectionMessageTextPanel.Location = new Point(3, 57);
            _connectionMessageTextPanel.Name = "_connectionMessageTextPanel";
            _connectionMessageTextPanel.Size = new Size(532, 40);
            _connectionMessageTextPanel.TabIndex = 12;
            // 
            // _connectionMessageTextBox
            // 
            _connectionMessageTextBox.Dock = DockStyle.Fill;
            _connectionMessageTextBox.Location = new Point(0, 0);
            _connectionMessageTextBox.Name = "_connectionMessageTextBox";
            _connectionMessageTextBox.Size = new Size(532, 23);
            _connectionMessageTextBox.TabIndex = 10;
            _connectionMessageTextBox.TextChanged += OnSettingChanged;
            // 
            // _disconnectionMessageSection
            // 
            _disconnectionMessageSection.ColumnCount = 2;
            _disconnectionMessageSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _disconnectionMessageSection.ColumnStyles.Add(new ColumnStyle());
            _disconnectionMessageSection.Controls.Add(_disconnectionMessageEnabledCheckBox, 0, 0);
            _disconnectionMessageSection.Controls.Add(_disconnectionMessageLabel, 0, 1);
            _disconnectionMessageSection.Controls.Add(_disconnectionMessageResetButton, 1, 1);
            _disconnectionMessageSection.Controls.Add(_disconnectionMessageTextPanel, 0, 2);
            _disconnectionMessageSection.Dock = DockStyle.Fill;
            _disconnectionMessageSection.Location = new Point(8, 338);
            _disconnectionMessageSection.Name = "_disconnectionMessageSection";
            _disconnectionMessageSection.RowCount = 3;
            _disconnectionMessageSection.RowStyles.Add(new RowStyle());
            _disconnectionMessageSection.RowStyles.Add(new RowStyle());
            _disconnectionMessageSection.RowStyles.Add(new RowStyle());
            _disconnectionMessageSection.Size = new Size(532, 100);
            _disconnectionMessageSection.TabIndex = 3;
            // 
            // _disconnectionMessageEnabledCheckBox
            // 
            _disconnectionMessageEnabledCheckBox.AutoSize = true;
            _disconnectionMessageSection.SetColumnSpan(_disconnectionMessageEnabledCheckBox, 2);
            _disconnectionMessageEnabledCheckBox.Dock = DockStyle.Fill;
            _disconnectionMessageEnabledCheckBox.Location = new Point(3, 3);
            _disconnectionMessageEnabledCheckBox.Name = "_disconnectionMessageEnabledCheckBox";
            _disconnectionMessageEnabledCheckBox.Size = new Size(526, 19);
            _disconnectionMessageEnabledCheckBox.TabIndex = 12;
            _disconnectionMessageEnabledCheckBox.Text = "Включить сообщение при отключении";
            _disconnectionMessageEnabledCheckBox.UseVisualStyleBackColor = true;
            _disconnectionMessageEnabledCheckBox.CheckedChanged += OnSettingChanged;
            // 
            // _disconnectionMessageLabel
            // 
            _disconnectionMessageLabel.AutoSize = true;
            _disconnectionMessageLabel.Dock = DockStyle.Fill;
            _disconnectionMessageLabel.Location = new Point(3, 25);
            _disconnectionMessageLabel.Name = "_disconnectionMessageLabel";
            _disconnectionMessageLabel.Size = new Size(495, 29);
            _disconnectionMessageLabel.TabIndex = 13;
            _disconnectionMessageLabel.Text = "Текст при отключении:";
            _disconnectionMessageLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _disconnectionMessageResetButton
            // 
            _disconnectionMessageResetButton.Location = new Point(504, 28);
            _disconnectionMessageResetButton.Name = "_disconnectionMessageResetButton";
            _disconnectionMessageResetButton.Size = new Size(25, 23);
            _disconnectionMessageResetButton.TabIndex = 15;
            _disconnectionMessageResetButton.Text = "↺";
            _disconnectionMessageResetButton.UseVisualStyleBackColor = true;
            _disconnectionMessageResetButton.Click += OnDisconnectionMessageResetButtonClicked;
            // 
            // _disconnectionMessageTextPanel
            // 
            _disconnectionMessageSection.SetColumnSpan(_disconnectionMessageTextPanel, 2);
            _disconnectionMessageTextPanel.Controls.Add(_disconnectionMessageTextBox);
            _disconnectionMessageTextPanel.Dock = DockStyle.Fill;
            _disconnectionMessageTextPanel.Location = new Point(3, 57);
            _disconnectionMessageTextPanel.Name = "_disconnectionMessageTextPanel";
            _disconnectionMessageTextPanel.Size = new Size(526, 40);
            _disconnectionMessageTextPanel.TabIndex = 16;
            // 
            // _disconnectionMessageTextBox
            // 
            _disconnectionMessageTextBox.Dock = DockStyle.Fill;
            _disconnectionMessageTextBox.Location = new Point(0, 0);
            _disconnectionMessageTextBox.Name = "_disconnectionMessageTextBox";
            _disconnectionMessageTextBox.Size = new Size(526, 23);
            _disconnectionMessageTextBox.TabIndex = 14;
            _disconnectionMessageTextBox.TextChanged += OnSettingChanged;
            // 
            // MessagesSettingsControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(_messagesTableLayout);
            Name = "MessagesSettingsControl";
            Size = new Size(548, 458);
            _messagesTableLayout.ResumeLayout(false);
            _welcomeMessageSection.ResumeLayout(false);
            _welcomeMessageSection.PerformLayout();
            _welcomeMessageTextPanel.ResumeLayout(false);
            _welcomeMessageTextPanel.PerformLayout();
            _farewellMessageSection.ResumeLayout(false);
            _farewellMessageSection.PerformLayout();
            _farewellMessageTextPanel.ResumeLayout(false);
            _farewellMessageTextPanel.PerformLayout();
            _connectionMessageSection.ResumeLayout(false);
            _connectionMessageSection.PerformLayout();
            _connectionMessageTextPanel.ResumeLayout(false);
            _connectionMessageTextPanel.PerformLayout();
            _disconnectionMessageSection.ResumeLayout(false);
            _disconnectionMessageSection.PerformLayout();
            _disconnectionMessageTextPanel.ResumeLayout(false);
            _disconnectionMessageTextPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel _messagesTableLayout;
        private TableLayoutPanel _welcomeMessageSection;
        private TableLayoutPanel _farewellMessageSection;
        private TableLayoutPanel _connectionMessageSection;
        private TableLayoutPanel _disconnectionMessageSection;
        private Panel _welcomeMessageTextPanel;
        private Panel _farewellMessageTextPanel;
        private Panel _connectionMessageTextPanel;
        private Panel _disconnectionMessageTextPanel;
        private CheckBox _welcomeMessageEnabledCheckBox;
        private Label _welcomeMessageLabel;
        private TextBox _welcomeMessageTextBox;
        private Button _welcomeMessageResetButton;
        private CheckBox _farewellMessageEnabledCheckBox;
        private Label _farewellMessageLabel;
        private TextBox _farewellMessageTextBox;
        private Button _farewellMessageResetButton;
        private CheckBox _connectionMessageEnabledCheckBox;
        private Label _connectionMessageLabel;
        private TextBox _connectionMessageTextBox;
        private Button _connectionMessageResetButton;
        private CheckBox _disconnectionMessageEnabledCheckBox;
        private Label _disconnectionMessageLabel;
        private TextBox _disconnectionMessageTextBox;
        private Button _disconnectionMessageResetButton;
    }
}
