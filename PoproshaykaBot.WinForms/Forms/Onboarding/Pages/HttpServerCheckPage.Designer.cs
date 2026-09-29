namespace PoproshaykaBot.WinForms.Forms.Onboarding.Pages;

partial class HttpServerCheckPage
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
        _statusLabel = new Label();
        _detailsLabel = new Label();
        _retryButton = new Button();
        _layout.SuspendLayout();
        SuspendLayout();
        //
        // _layout
        //
        _layout.ColumnCount = 1;
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _layout.Controls.Add(_intro, 0, 0);
        _layout.Controls.Add(_statusLabel, 0, 1);
        _layout.Controls.Add(_detailsLabel, 0, 2);
        _layout.Controls.Add(_retryButton, 0, 3);
        _layout.Dock = DockStyle.Fill;
        _layout.Name = "_layout";
        _layout.Padding = new Padding(20, 18, 20, 18);
        _layout.RowCount = 5;
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
        _intro.Margin = new Padding(0, 0, 0, 12);
        _intro.Name = "_intro";
        _intro.Text =
            "Перед авторизацией нужно убедиться, что порт из Redirect URI свободен."
            + Environment.NewLine
            + "Если порт уже использует HTTP сервер бота – пропустим проверку. Если занят сторонним приложением – закройте его или измените URI.";
        _intro.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _statusLabel
        //
        _statusLabel.AutoSize = true;
        _statusLabel.Dock = DockStyle.Top;
        _statusLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _statusLabel.Margin = new Padding(0, 0, 0, 6);
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Text = "Проверка...";
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _detailsLabel
        //
        _detailsLabel.AutoSize = true;
        _detailsLabel.Dock = DockStyle.Top;
        _detailsLabel.ForeColor = Color.Gray;
        _detailsLabel.Margin = new Padding(0, 0, 0, 12);
        _detailsLabel.Name = "_detailsLabel";
        _detailsLabel.Text = "";
        _detailsLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _retryButton
        //
        _retryButton.AutoSize = true;
        _retryButton.MinimumSize = new Size(150, 28);
        _retryButton.Name = "_retryButton";
        _retryButton.Text = "Повторить проверку";
        _retryButton.UseVisualStyleBackColor = true;
        _retryButton.Visible = false;
        _retryButton.Click += OnRetryButtonClicked;
        //
        // HttpServerCheckPage
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_layout);
        Name = "HttpServerCheckPage";
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel _layout;
    private Label _intro;
    private Label _statusLabel;
    private Label _detailsLabel;
    private Button _retryButton;
}
