namespace PoproshaykaBot.WinForms.Forms.Onboarding.Pages;

partial class AuthorizationPage
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
        components = new System.ComponentModel.Container();
        _layout = new TableLayoutPanel();
        _intro = new Label();
        _buttonsLayout = new FlowLayoutPanel();
        _openBrowserButton = new Button();
        _copyLinkButton = new Button();
        _embeddedLoginButton = new Button();
        _cancelButton = new Button();
        _statusLabel = new Label();
        _resultLabel = new Label();
        _toolTip = new ToolTip(components);
        _buttonsHintLabel = new Label();
        _layout.SuspendLayout();
        _buttonsLayout.SuspendLayout();
        SuspendLayout();
        //
        // _layout
        //
        _layout.ColumnCount = 1;
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _layout.Controls.Add(_intro, 0, 0);
        _layout.Controls.Add(_buttonsLayout, 0, 1);
        _layout.Controls.Add(_buttonsHintLabel, 0, 2);
        _layout.Controls.Add(_cancelButton, 0, 3);
        _layout.Controls.Add(_statusLabel, 0, 4);
        _layout.Controls.Add(_resultLabel, 0, 5);
        _layout.Dock = DockStyle.Fill;
        _layout.Name = "_layout";
        _layout.Padding = new Padding(20, 18, 20, 18);
        _layout.RowCount = 7;
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        //
        // _intro
        //
        _intro.AutoSize = true;
        _intro.Dock = DockStyle.Top;
        _intro.Margin = new Padding(0, 0, 0, 16);
        _intro.Name = "_intro";
        _intro.Text = "Откройте браузер и подтвердите доступ для бота.";
        _intro.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _buttonsLayout
        //
        _buttonsLayout.AutoSize = true;
        _buttonsLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _buttonsLayout.Controls.Add(_openBrowserButton);
        _buttonsLayout.Controls.Add(_copyLinkButton);
        _buttonsLayout.Controls.Add(_embeddedLoginButton);
        _buttonsLayout.Dock = DockStyle.Top;
        _buttonsLayout.FlowDirection = FlowDirection.LeftToRight;
        _buttonsLayout.Margin = new Padding(0, 0, 0, 8);
        _buttonsLayout.Name = "_buttonsLayout";
        _buttonsLayout.WrapContents = true;
        //
        // _openBrowserButton
        //
        _openBrowserButton.AutoSize = true;
        _openBrowserButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _openBrowserButton.Margin = new Padding(0, 0, 8, 6);
        _openBrowserButton.MinimumSize = new Size(220, 30);
        _openBrowserButton.Name = "_openBrowserButton";
        _openBrowserButton.Text = "🌐 Открыть в браузере";
        _openBrowserButton.UseVisualStyleBackColor = true;
        _openBrowserButton.Click += OnOpenBrowserButtonClicked;
        //
        // _copyLinkButton
        //
        _copyLinkButton.AutoSize = true;
        _copyLinkButton.Margin = new Padding(0, 0, 8, 6);
        _copyLinkButton.MinimumSize = new Size(180, 30);
        _copyLinkButton.Name = "_copyLinkButton";
        _copyLinkButton.Text = "⎘ Скопировать ссылку";
        _copyLinkButton.UseVisualStyleBackColor = true;
        _copyLinkButton.Click += OnCopyLinkButtonClicked;
        //
        // _embeddedLoginButton
        //
        _embeddedLoginButton.AutoSize = true;
        _embeddedLoginButton.Margin = new Padding(0, 0, 0, 6);
        _embeddedLoginButton.MinimumSize = new Size(260, 30);
        _embeddedLoginButton.Name = "_embeddedLoginButton";
        _embeddedLoginButton.Text = "🍪 Войти + куки чат-плитки";
        _embeddedLoginButton.UseVisualStyleBackColor = true;
        _embeddedLoginButton.Click += OnEmbeddedLoginButtonClicked;
        //
        // _buttonsHintLabel
        //
        _buttonsHintLabel.AutoSize = true;
        _buttonsHintLabel.Dock = DockStyle.Top;
        _buttonsHintLabel.ForeColor = Color.Gray;
        _buttonsHintLabel.Margin = new Padding(0, 0, 0, 10);
        _buttonsHintLabel.MaximumSize = new Size(640, 0);
        _buttonsHintLabel.Name = "_buttonsHintLabel";
        _buttonsHintLabel.Text = "🍪 – нужно, только если хотите, чтобы чат-плитка на дашборде была залогинена этим аккаунтом (эмоты, бейджи, sub-only сообщения). Иначе достаточно «Открыть в браузере».";
        _buttonsHintLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _cancelButton
        //
        _cancelButton.AutoSize = true;
        _cancelButton.Margin = new Padding(0, 0, 0, 12);
        _cancelButton.MinimumSize = new Size(220, 30);
        _cancelButton.Name = "_cancelButton";
        _cancelButton.Text = "⏹️ Отменить авторизацию";
        _cancelButton.UseVisualStyleBackColor = true;
        _cancelButton.Visible = false;
        _cancelButton.Click += OnCancelButtonClicked;
        //
        // _statusLabel
        //
        _statusLabel.AutoSize = true;
        _statusLabel.Dock = DockStyle.Top;
        _statusLabel.ForeColor = Color.Gray;
        _statusLabel.Margin = new Padding(0, 0, 0, 6);
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Text = "";
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _resultLabel
        //
        _resultLabel.AutoSize = true;
        _resultLabel.Dock = DockStyle.Top;
        _resultLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _resultLabel.Margin = new Padding(0);
        _resultLabel.Name = "_resultLabel";
        _resultLabel.Text = "";
        _resultLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // AuthorizationPage
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_layout);
        Name = "AuthorizationPage";
        _toolTip.InitialDelay = 400;
        _toolTip.ReshowDelay = 200;
        _toolTip.SetToolTip(_openBrowserButton, "Открыть Twitch-логин в системном браузере");
        _toolTip.SetToolTip(_copyLinkButton, "Скопировать ссылку для ручного открытия");
        _toolTip.SetToolTip(_embeddedLoginButton, "Открыть Twitch-логин внутри окна и сохранить куки для чат-плитки");
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        _buttonsLayout.ResumeLayout(false);
        _buttonsLayout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private TableLayoutPanel _layout;
    private Label _intro;
    private FlowLayoutPanel _buttonsLayout;
    private Button _openBrowserButton;
    private Button _copyLinkButton;
    private Button _embeddedLoginButton;
    private Label _buttonsHintLabel;
    private Button _cancelButton;
    private Label _statusLabel;
    private Label _resultLabel;
    private ToolTip _toolTip;
}
