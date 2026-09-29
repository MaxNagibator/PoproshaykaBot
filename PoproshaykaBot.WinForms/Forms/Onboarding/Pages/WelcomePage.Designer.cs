namespace PoproshaykaBot.WinForms.Forms.Onboarding.Pages;

partial class WelcomePage
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
        _bodyText = new Label();
        _consoleLink = new LinkLabel();
        _hint = new Label();
        _layout.SuspendLayout();
        SuspendLayout();
        //
        // _layout
        //
        _layout.ColumnCount = 1;
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _layout.Controls.Add(_intro, 0, 0);
        _layout.Controls.Add(_bodyText, 0, 1);
        _layout.Controls.Add(_consoleLink, 0, 2);
        _layout.Controls.Add(_hint, 0, 3);
        _layout.Dock = DockStyle.Fill;
        _layout.Padding = new Padding(20, 18, 20, 18);
        _layout.RowCount = 5;
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle());
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _layout.Name = "_layout";
        //
        // _intro
        //
        _intro.AutoSize = true;
        _intro.Dock = DockStyle.Top;
        _intro.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _intro.Margin = new Padding(0, 0, 0, 12);
        _intro.Name = "_intro";
        _intro.Text = "Этот мастер поможет настроить бота за несколько шагов.";
        _intro.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _bodyText
        //
        _bodyText.AutoSize = true;
        _bodyText.Dock = DockStyle.Top;
        _bodyText.Margin = new Padding(0, 0, 0, 12);
        _bodyText.Name = "_bodyText";
        _bodyText.Text =
            "Понадобится приложение в Twitch Developer Console: оно даёт пару Client ID + Client Secret и принимает Redirect URI."
            + Environment.NewLine + Environment.NewLine
            + "Если приложения ещё нет – откройте консоль, создайте новое приложение, укажите Redirect URI http://localhost:3000 (порт можно поменять)."
            + Environment.NewLine + Environment.NewLine
            + "Если уже есть – переходите к следующему шагу.";
        _bodyText.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _consoleLink
        //
        _consoleLink.AutoSize = true;
        _consoleLink.Dock = DockStyle.Top;
        _consoleLink.Margin = new Padding(0, 0, 0, 16);
        _consoleLink.Name = "_consoleLink";
        _consoleLink.Text = "Открыть Twitch Developer Console";
        _consoleLink.LinkClicked += OnConsoleLinkClicked;
        //
        // _hint
        //
        _hint.AutoSize = true;
        _hint.Dock = DockStyle.Top;
        _hint.ForeColor = Color.Gray;
        _hint.Margin = new Padding(0);
        _hint.Name = "_hint";
        _hint.Text = "Нажмите «Далее», чтобы продолжить.";
        _hint.TextAlign = ContentAlignment.MiddleLeft;
        //
        // WelcomePage
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_layout);
        Name = "WelcomePage";
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel _layout;
    private Label _intro;
    private Label _bodyText;
    private LinkLabel _consoleLink;
    private Label _hint;
}
