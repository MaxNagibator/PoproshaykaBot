namespace PoproshaykaBot.WinForms.Widgets;

partial class ChatDisplay
{
    /// <summary>
    /// Обязательная переменная конструктора.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Освободить все используемые ресурсы.
    /// </summary>
    /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Код, автоматически созданный конструктором компонентов

    /// <summary>
    /// Требуемый метод для поддержки конструктора – не изменяйте
    /// содержимое этого метода с помощью редактора кода.
    /// </summary>
    private void InitializeComponent()
    {
        _webView = new Microsoft.Web.WebView2.WinForms.WebView2();
        _fallbackPanel = new Panel();
        _fallbackLabel = new Label();
        _fallbackLink = new LinkLabel();
        ((System.ComponentModel.ISupportInitialize)_webView).BeginInit();
        _fallbackPanel.SuspendLayout();
        SuspendLayout();
        //
        // _webView
        //
        _webView.AllowExternalDrop = true;
        _webView.CreationProperties = null;
        _webView.DefaultBackgroundColor = Color.White;
        _webView.Dock = DockStyle.Fill;
        _webView.Location = new Point(0, 0);
        _webView.Name = "_webView";
        _webView.Size = new Size(375, 294);
        _webView.TabIndex = 0;
        _webView.ZoomFactor = 1D;
        //
        // _fallbackPanel
        //
        _fallbackPanel.Controls.Add(_fallbackLabel);
        _fallbackPanel.Controls.Add(_fallbackLink);
        _fallbackPanel.Dock = DockStyle.Fill;
        _fallbackPanel.Location = new Point(0, 0);
        _fallbackPanel.Name = "_fallbackPanel";
        _fallbackPanel.Padding = new Padding(12);
        _fallbackPanel.Size = new Size(375, 294);
        _fallbackPanel.TabIndex = 1;
        _fallbackPanel.Visible = false;
        //
        // _fallbackLabel
        //
        _fallbackLabel.Dock = DockStyle.Fill;
        _fallbackLabel.Location = new Point(12, 12);
        _fallbackLabel.Name = "_fallbackLabel";
        _fallbackLabel.Size = new Size(351, 246);
        _fallbackLabel.TabIndex = 0;
        _fallbackLabel.Text = "Инициализация чата...";
        _fallbackLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _fallbackLink
        //
        _fallbackLink.Dock = DockStyle.Bottom;
        _fallbackLink.Location = new Point(12, 258);
        _fallbackLink.Name = "_fallbackLink";
        _fallbackLink.Size = new Size(351, 24);
        _fallbackLink.TabIndex = 1;
        _fallbackLink.TabStop = true;
        _fallbackLink.TextAlign = ContentAlignment.MiddleCenter;
        _fallbackLink.Visible = false;
        _fallbackLink.LinkClicked += OnFallbackLinkClicked;
        //
        // ChatDisplay
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_webView);
        Controls.Add(_fallbackPanel);
        Name = "ChatDisplay";
        Size = new Size(375, 294);
        ((System.ComponentModel.ISupportInitialize)_webView).EndInit();
        _fallbackPanel.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    private Microsoft.Web.WebView2.WinForms.WebView2 _webView;
    private Panel _fallbackPanel;
    private Label _fallbackLabel;
    private LinkLabel _fallbackLink;

    #endregion
}
