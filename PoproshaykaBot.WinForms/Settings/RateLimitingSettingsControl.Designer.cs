namespace PoproshaykaBot.WinForms.Settings;

partial class RateLimitingSettingsControl
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
        _rateLimitingTableLayout = new TableLayoutPanel();
        _messagesAllowedLabel = new Label();
        _messagesAllowedPanel = new Panel();
        _messagesAllowedNumeric = new NumericUpDown();
        _messagesAllowedResetButton = new Button();
        _throttlingPeriodLabel = new Label();
        _throttlingPeriodPanel = new Panel();
        _throttlingPeriodNumeric = new NumericUpDown();
        _throttlingPeriodResetButton = new Button();
        _rateLimitingTableLayout.SuspendLayout();
        _messagesAllowedPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_messagesAllowedNumeric).BeginInit();
        _throttlingPeriodPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_throttlingPeriodNumeric).BeginInit();
        SuspendLayout();
        //
        // _rateLimitingTableLayout
        //
        _rateLimitingTableLayout.ColumnCount = 2;
        _rateLimitingTableLayout.ColumnStyles.Add(new ColumnStyle());
        _rateLimitingTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _rateLimitingTableLayout.Controls.Add(_messagesAllowedLabel, 0, 0);
        _rateLimitingTableLayout.Controls.Add(_messagesAllowedPanel, 1, 0);
        _rateLimitingTableLayout.Controls.Add(_throttlingPeriodLabel, 0, 1);
        _rateLimitingTableLayout.Controls.Add(_throttlingPeriodPanel, 1, 1);
        _rateLimitingTableLayout.Dock = DockStyle.Fill;
        _rateLimitingTableLayout.Location = new Point(0, 0);
        _rateLimitingTableLayout.Name = "_rateLimitingTableLayout";
        _rateLimitingTableLayout.Padding = new Padding(5);
        _rateLimitingTableLayout.RowCount = 3;
        _rateLimitingTableLayout.RowStyles.Add(new RowStyle());
        _rateLimitingTableLayout.RowStyles.Add(new RowStyle());
        _rateLimitingTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _rateLimitingTableLayout.Size = new Size(548, 458);
        _rateLimitingTableLayout.TabIndex = 0;
        //
        // _messagesAllowedLabel
        //
        _messagesAllowedLabel.AutoSize = true;
        _messagesAllowedLabel.Dock = DockStyle.Fill;
        _messagesAllowedLabel.Location = new Point(8, 5);
        _messagesAllowedLabel.Name = "_messagesAllowedLabel";
        _messagesAllowedLabel.Size = new Size(156, 35);
        _messagesAllowedLabel.TabIndex = 0;
        _messagesAllowedLabel.Text = "Сообщений в период:";
        _messagesAllowedLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _messagesAllowedPanel
        //
        _messagesAllowedPanel.Controls.Add(_messagesAllowedNumeric);
        _messagesAllowedPanel.Controls.Add(_messagesAllowedResetButton);
        _messagesAllowedPanel.Dock = DockStyle.Fill;
        _messagesAllowedPanel.Location = new Point(170, 8);
        _messagesAllowedPanel.Name = "_messagesAllowedPanel";
        _messagesAllowedPanel.Size = new Size(370, 29);
        _messagesAllowedPanel.TabIndex = 1;
        //
        // _messagesAllowedNumeric
        //
        _messagesAllowedNumeric.Dock = DockStyle.Fill;
        _messagesAllowedNumeric.Location = new Point(0, 0);
        _messagesAllowedNumeric.Margin = new Padding(0, 0, 3, 0);
        _messagesAllowedNumeric.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
        _messagesAllowedNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _messagesAllowedNumeric.Name = "_messagesAllowedNumeric";
        _messagesAllowedNumeric.Size = new Size(345, 23);
        _messagesAllowedNumeric.TabIndex = 1;
        _messagesAllowedNumeric.Value = new decimal(new int[] { 750, 0, 0, 0 });
        _messagesAllowedNumeric.ValueChanged += OnSettingChanged;
        //
        // _messagesAllowedResetButton
        //
        _messagesAllowedResetButton.Dock = DockStyle.Right;
        _messagesAllowedResetButton.Location = new Point(345, 0);
        _messagesAllowedResetButton.Name = "_messagesAllowedResetButton";
        _messagesAllowedResetButton.Size = new Size(25, 29);
        _messagesAllowedResetButton.TabIndex = 2;
        _messagesAllowedResetButton.Text = "↺";
        _messagesAllowedResetButton.UseVisualStyleBackColor = true;
        _messagesAllowedResetButton.Click += OnMessagesAllowedResetButtonClicked;
        //
        // _throttlingPeriodLabel
        //
        _throttlingPeriodLabel.AutoSize = true;
        _throttlingPeriodLabel.Dock = DockStyle.Fill;
        _throttlingPeriodLabel.Location = new Point(8, 40);
        _throttlingPeriodLabel.Name = "_throttlingPeriodLabel";
        _throttlingPeriodLabel.Size = new Size(156, 35);
        _throttlingPeriodLabel.TabIndex = 3;
        _throttlingPeriodLabel.Text = "Период ограничения (сек):";
        _throttlingPeriodLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _throttlingPeriodPanel
        //
        _throttlingPeriodPanel.Controls.Add(_throttlingPeriodNumeric);
        _throttlingPeriodPanel.Controls.Add(_throttlingPeriodResetButton);
        _throttlingPeriodPanel.Dock = DockStyle.Fill;
        _throttlingPeriodPanel.Location = new Point(170, 43);
        _throttlingPeriodPanel.Name = "_throttlingPeriodPanel";
        _throttlingPeriodPanel.Size = new Size(370, 29);
        _throttlingPeriodPanel.TabIndex = 4;
        //
        // _throttlingPeriodNumeric
        //
        _throttlingPeriodNumeric.Dock = DockStyle.Fill;
        _throttlingPeriodNumeric.Location = new Point(0, 0);
        _throttlingPeriodNumeric.Margin = new Padding(0, 0, 3, 0);
        _throttlingPeriodNumeric.Maximum = new decimal(new int[] { 300, 0, 0, 0 });
        _throttlingPeriodNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _throttlingPeriodNumeric.Name = "_throttlingPeriodNumeric";
        _throttlingPeriodNumeric.Size = new Size(345, 23);
        _throttlingPeriodNumeric.TabIndex = 4;
        _throttlingPeriodNumeric.Value = new decimal(new int[] { 30, 0, 0, 0 });
        _throttlingPeriodNumeric.ValueChanged += OnSettingChanged;
        //
        // _throttlingPeriodResetButton
        //
        _throttlingPeriodResetButton.Dock = DockStyle.Right;
        _throttlingPeriodResetButton.Location = new Point(345, 0);
        _throttlingPeriodResetButton.Name = "_throttlingPeriodResetButton";
        _throttlingPeriodResetButton.Size = new Size(25, 29);
        _throttlingPeriodResetButton.TabIndex = 5;
        _throttlingPeriodResetButton.Text = "↺";
        _throttlingPeriodResetButton.UseVisualStyleBackColor = true;
        _throttlingPeriodResetButton.Click += OnThrottlingPeriodResetButtonClicked;
        //
        // RateLimitingSettingsControl
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_rateLimitingTableLayout);
        Name = "RateLimitingSettingsControl";
        Size = new Size(548, 458);
        _rateLimitingTableLayout.ResumeLayout(false);
        _rateLimitingTableLayout.PerformLayout();
        _messagesAllowedPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_messagesAllowedNumeric).EndInit();
        _throttlingPeriodPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_throttlingPeriodNumeric).EndInit();
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel _rateLimitingTableLayout;
    private Panel _messagesAllowedPanel;
    private Panel _throttlingPeriodPanel;
    private Label _messagesAllowedLabel;
    private NumericUpDown _messagesAllowedNumeric;
    private Button _messagesAllowedResetButton;
    private Label _throttlingPeriodLabel;
    private NumericUpDown _throttlingPeriodNumeric;
    private Button _throttlingPeriodResetButton;
}
