namespace PoproshaykaBot.WinForms.Forms.Onboarding.Pages;

partial class HealthCheckPage
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Component Designer generated code

    private void InitializeComponent()
    {
        _layout = new TableLayoutPanel();
        _intro = new Label();
        _chatTestPanel = new TableLayoutPanel();
        _chatTestHeader = new Label();
        _chatTestMessageTextBox = new TextBox();
        _chatTestButton = new Button();
        _chatTestStatusLabel = new Label();
        _overlayPanel = new TableLayoutPanel();
        _overlayHeader = new Label();
        _overlayButton = new Button();
        _overlayUrlLabel = new Label();
        _botStatusLabel = new Label();
        _hintLabel = new Label();
        _layout.SuspendLayout();
        _chatTestPanel.SuspendLayout();
        _overlayPanel.SuspendLayout();
        SuspendLayout();
        //
        // _layout
        //
        _layout.ColumnCount = 1;
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _layout.Controls.Add(_intro, 0, 0);
        _layout.Controls.Add(_overlayPanel, 0, 1);
        _layout.Controls.Add(_chatTestPanel, 0, 2);
        _layout.Controls.Add(_botStatusLabel, 0, 3);
        _layout.Controls.Add(_hintLabel, 0, 4);
        _layout.Dock = DockStyle.Fill;
        _layout.Name = "_layout";
        _layout.Padding = new Padding(20, 18, 20, 18);
        _layout.RowCount = 5;
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle());
        //
        // _intro
        //
        _intro.AutoSize = true;
        _intro.Dock = DockStyle.Top;
        _intro.Margin = new Padding(0, 0, 0, 14);
        _intro.Name = "_intro";
        _intro.Text =
            "Опциональная диагностика. Можно пропустить и нажать «Далее»."
            + Environment.NewLine
            + "Тесты помогут убедиться, что бот сможет писать в чат и что OBS-оверлей работает.";
        _intro.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _chatTestPanel
        //
        _chatTestPanel.AutoSize = true;
        _chatTestPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _chatTestPanel.ColumnCount = 2;
        _chatTestPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _chatTestPanel.ColumnStyles.Add(new ColumnStyle());
        _chatTestPanel.Controls.Add(_chatTestHeader, 0, 0);
        _chatTestPanel.Controls.Add(_chatTestMessageTextBox, 0, 1);
        _chatTestPanel.Controls.Add(_chatTestButton, 1, 1);
        _chatTestPanel.Controls.Add(_chatTestStatusLabel, 0, 2);
        _chatTestPanel.Dock = DockStyle.Top;
        _chatTestPanel.Margin = new Padding(0, 0, 0, 12);
        _chatTestPanel.Name = "_chatTestPanel";
        _chatTestPanel.RowCount = 3;
        _chatTestPanel.RowStyles.Add(new RowStyle());
        _chatTestPanel.RowStyles.Add(new RowStyle());
        _chatTestPanel.RowStyles.Add(new RowStyle());
        _chatTestPanel.SetColumnSpan(_chatTestHeader, 2);
        _chatTestPanel.SetColumnSpan(_chatTestStatusLabel, 2);
        //
        // _chatTestHeader
        //
        _chatTestHeader.AutoSize = true;
        _chatTestHeader.Dock = DockStyle.Top;
        _chatTestHeader.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _chatTestHeader.Margin = new Padding(0, 0, 0, 4);
        _chatTestHeader.Name = "_chatTestHeader";
        _chatTestHeader.Text = "💬 Тестовое сообщение в чат";
        _chatTestHeader.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _chatTestMessageTextBox
        //
        _chatTestMessageTextBox.Dock = DockStyle.Fill;
        _chatTestMessageTextBox.Margin = new Padding(0, 0, 6, 0);
        _chatTestMessageTextBox.Name = "_chatTestMessageTextBox";
        _chatTestMessageTextBox.Text = "Бот подключён, проверка связи.";
        //
        // _chatTestButton
        //
        _chatTestButton.AutoSize = true;
        _chatTestButton.MinimumSize = new Size(120, 28);
        _chatTestButton.Name = "_chatTestButton";
        _chatTestButton.Text = "Отправить";
        _chatTestButton.UseVisualStyleBackColor = true;
        _chatTestButton.Click += OnChatTestButtonClicked;
        //
        // _chatTestStatusLabel
        //
        _chatTestStatusLabel.AutoSize = true;
        _chatTestStatusLabel.Dock = DockStyle.Top;
        _chatTestStatusLabel.ForeColor = Color.Gray;
        _chatTestStatusLabel.Margin = new Padding(0, 4, 0, 0);
        _chatTestStatusLabel.Name = "_chatTestStatusLabel";
        _chatTestStatusLabel.Text = "Не выполнено";
        _chatTestStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _overlayPanel
        //
        _overlayPanel.AutoSize = true;
        _overlayPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _overlayPanel.ColumnCount = 2;
        _overlayPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _overlayPanel.ColumnStyles.Add(new ColumnStyle());
        _overlayPanel.Controls.Add(_overlayHeader, 0, 0);
        _overlayPanel.Controls.Add(_overlayUrlLabel, 0, 1);
        _overlayPanel.Controls.Add(_overlayButton, 1, 1);
        _overlayPanel.Dock = DockStyle.Top;
        _overlayPanel.Margin = new Padding(0, 0, 0, 12);
        _overlayPanel.Name = "_overlayPanel";
        _overlayPanel.RowCount = 2;
        _overlayPanel.RowStyles.Add(new RowStyle());
        _overlayPanel.RowStyles.Add(new RowStyle());
        _overlayPanel.SetColumnSpan(_overlayHeader, 2);
        //
        // _overlayHeader
        //
        _overlayHeader.AutoSize = true;
        _overlayHeader.Dock = DockStyle.Top;
        _overlayHeader.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _overlayHeader.Margin = new Padding(0, 0, 0, 4);
        _overlayHeader.Name = "_overlayHeader";
        _overlayHeader.Text = "🎬 OBS-оверлей";
        _overlayHeader.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _overlayUrlLabel
        //
        _overlayUrlLabel.AutoSize = true;
        _overlayUrlLabel.Dock = DockStyle.Fill;
        _overlayUrlLabel.ForeColor = Color.Gray;
        _overlayUrlLabel.Margin = new Padding(0, 4, 6, 0);
        _overlayUrlLabel.Name = "_overlayUrlLabel";
        _overlayUrlLabel.Text = "Адрес страницы для Browser Source в OBS:";
        _overlayUrlLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _overlayButton
        //
        _overlayButton.AutoSize = true;
        _overlayButton.MinimumSize = new Size(120, 28);
        _overlayButton.Name = "_overlayButton";
        _overlayButton.Text = "🌐 Открыть";
        _overlayButton.UseVisualStyleBackColor = true;
        _overlayButton.Click += OnOverlayButtonClicked;
        //
        // _botStatusLabel
        //
        _botStatusLabel.AutoSize = true;
        _botStatusLabel.Dock = DockStyle.Top;
        _botStatusLabel.ForeColor = Color.Gray;
        _botStatusLabel.Margin = new Padding(0, 0, 0, 12);
        _botStatusLabel.Name = "_botStatusLabel";
        _botStatusLabel.Text = "Бот в чате как: ожидание сообщения...";
        _botStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _hintLabel
        //
        _hintLabel.AutoSize = true;
        _hintLabel.Dock = DockStyle.Top;
        _hintLabel.ForeColor = Color.Gray;
        _hintLabel.Margin = new Padding(0);
        _hintLabel.Name = "_hintLabel";
        _hintLabel.Text = "Все проверки опциональны. После завершения – нажмите «Далее».";
        _hintLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // HealthCheckPage
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_layout);
        Name = "HealthCheckPage";
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        _chatTestPanel.ResumeLayout(false);
        _chatTestPanel.PerformLayout();
        _overlayPanel.ResumeLayout(false);
        _overlayPanel.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel _layout;
    private Label _intro;
    private TableLayoutPanel _chatTestPanel;
    private Label _chatTestHeader;
    private TextBox _chatTestMessageTextBox;
    private Button _chatTestButton;
    private Label _chatTestStatusLabel;
    private TableLayoutPanel _overlayPanel;
    private Label _overlayHeader;
    private Button _overlayButton;
    private Label _overlayUrlLabel;
    private Label _botStatusLabel;
    private Label _hintLabel;
}
