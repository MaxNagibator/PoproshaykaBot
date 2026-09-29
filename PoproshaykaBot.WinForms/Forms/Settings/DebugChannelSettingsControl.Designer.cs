namespace PoproshaykaBot.WinForms.Forms.Settings
{
    partial class DebugChannelSettingsControl
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
            _mainTableLayoutPanel = new TableLayoutPanel();
            _enabledCheckBox = new CheckBox();
            _channelTableLayoutPanel = new TableLayoutPanel();
            _channelLabel = new Label();
            _channelTextBox = new TextBox();
            _allowSendingCheckBox = new CheckBox();
            _hintLabel = new Label();
            _infoLabel = new Label();
            _mainTableLayoutPanel.SuspendLayout();
            _channelTableLayoutPanel.SuspendLayout();
            SuspendLayout();
            //
            // _mainTableLayoutPanel
            //
            _mainTableLayoutPanel.ColumnCount = 1;
            _mainTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _mainTableLayoutPanel.Controls.Add(_enabledCheckBox, 0, 0);
            _mainTableLayoutPanel.Controls.Add(_channelTableLayoutPanel, 0, 1);
            _mainTableLayoutPanel.Controls.Add(_allowSendingCheckBox, 0, 2);
            _mainTableLayoutPanel.Controls.Add(_hintLabel, 0, 3);
            _mainTableLayoutPanel.Controls.Add(_infoLabel, 0, 4);
            _mainTableLayoutPanel.Dock = DockStyle.Fill;
            _mainTableLayoutPanel.Location = new Point(0, 0);
            _mainTableLayoutPanel.Name = "_mainTableLayoutPanel";
            _mainTableLayoutPanel.RowCount = 6;
            _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F));
            _mainTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _mainTableLayoutPanel.Size = new Size(400, 300);
            _mainTableLayoutPanel.TabIndex = 0;
            //
            // _enabledCheckBox
            //
            _enabledCheckBox.AutoSize = true;
            _enabledCheckBox.Dock = DockStyle.Fill;
            _enabledCheckBox.Location = new Point(3, 3);
            _enabledCheckBox.Name = "_enabledCheckBox";
            _enabledCheckBox.Size = new Size(394, 24);
            _enabledCheckBox.TabIndex = 0;
            _enabledCheckBox.Text = "Подключаться к чужому каналу (режим отладки)";
            _enabledCheckBox.UseVisualStyleBackColor = true;
            _enabledCheckBox.CheckedChanged += OnSettingChanged;
            //
            // _channelTableLayoutPanel
            //
            _channelTableLayoutPanel.ColumnCount = 2;
            _channelTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
            _channelTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _channelTableLayoutPanel.Controls.Add(_channelLabel, 0, 0);
            _channelTableLayoutPanel.Controls.Add(_channelTextBox, 1, 0);
            _channelTableLayoutPanel.Dock = DockStyle.Fill;
            _channelTableLayoutPanel.Location = new Point(3, 33);
            _channelTableLayoutPanel.Margin = new Padding(3, 3, 3, 3);
            _channelTableLayoutPanel.Name = "_channelTableLayoutPanel";
            _channelTableLayoutPanel.RowCount = 1;
            _channelTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _channelTableLayoutPanel.Size = new Size(394, 28);
            _channelTableLayoutPanel.TabIndex = 1;
            //
            // _channelLabel
            //
            _channelLabel.AutoSize = true;
            _channelLabel.Dock = DockStyle.Fill;
            _channelLabel.Location = new Point(3, 0);
            _channelLabel.Name = "_channelLabel";
            _channelLabel.Size = new Size(64, 28);
            _channelLabel.TabIndex = 0;
            _channelLabel.Text = "Канал:";
            _channelLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _channelTextBox
            //
            _channelTextBox.Dock = DockStyle.Fill;
            _channelTextBox.Location = new Point(73, 3);
            _channelTextBox.Name = "_channelTextBox";
            _channelTextBox.PlaceholderText = "например, mrbeast";
            _channelTextBox.Size = new Size(318, 23);
            _channelTextBox.TabIndex = 1;
            _channelTextBox.TextChanged += OnSettingChanged;
            //
            // _allowSendingCheckBox
            //
            _allowSendingCheckBox.AutoSize = true;
            _allowSendingCheckBox.Dock = DockStyle.Fill;
            _allowSendingCheckBox.Location = new Point(3, 67);
            _allowSendingCheckBox.Name = "_allowSendingCheckBox";
            _allowSendingCheckBox.Size = new Size(394, 24);
            _allowSendingCheckBox.TabIndex = 2;
            _allowSendingCheckBox.Text = "Разрешить боту писать в этот чат";
            _allowSendingCheckBox.UseVisualStyleBackColor = true;
            _allowSendingCheckBox.CheckedChanged += OnSettingChanged;
            //
            // _hintLabel
            //
            _hintLabel.AutoSize = true;
            _hintLabel.Dock = DockStyle.Fill;
            _hintLabel.ForeColor = SystemColors.GrayText;
            _hintLabel.Location = new Point(3, 97);
            _hintLabel.Name = "_hintLabel";
            _hintLabel.Size = new Size(394, 60);
            _hintLabel.TabIndex = 3;
            //
            // _infoLabel
            //
            _infoLabel.AutoSize = true;
            _infoLabel.Dock = DockStyle.Fill;
            _infoLabel.ForeColor = SystemColors.GrayText;
            _infoLabel.Location = new Point(3, 157);
            _infoLabel.Name = "_infoLabel";
            _infoLabel.Size = new Size(394, 130);
            _infoLabel.TabIndex = 4;
            _infoLabel.Text = "ℹ️ Отладка нужна, чтобы посмотреть, как бот ведёт себя на живом чате. На чужом канале доступно только чтение: голосования, смена названия и категории не работают, потому что права есть лишь на ваш собственный канал." + Environment.NewLine + Environment.NewLine + "Плитка «Чат» – это настоящая страница Twitch под аккаунтом бота: то, что вы наберёте в её поле ввода руками, уйдёт в чужой чат в обход этой настройки." + Environment.NewLine + Environment.NewLine + "Смена канала применяется при следующем подключении бота. Статистика и очки зрителей пишутся в тот же профиль, что и обычно – для чистого прогона запускайте scripts/run-debug.ps1, он поднимает отдельную папку данных.";
            //
            // DebugChannelSettingsControl
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(_mainTableLayoutPanel);
            Name = "DebugChannelSettingsControl";
            Size = new Size(400, 300);
            _channelTableLayoutPanel.ResumeLayout(false);
            _channelTableLayoutPanel.PerformLayout();
            _mainTableLayoutPanel.ResumeLayout(false);
            _mainTableLayoutPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel _mainTableLayoutPanel;
        private CheckBox _enabledCheckBox;
        private TableLayoutPanel _channelTableLayoutPanel;
        private Label _channelLabel;
        private TextBox _channelTextBox;
        private CheckBox _allowSendingCheckBox;
        private Label _hintLabel;
        private Label _infoLabel;
    }
}
