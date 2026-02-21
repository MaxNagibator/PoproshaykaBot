namespace PoproshaykaBot.WinForms;

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
    /// Требуемый метод для поддержки конструктора — не изменяйте
    /// содержимое этого метода с помощью редактора кода.
    /// </summary>
    private void InitializeComponent()
    {
        _chatRichTextBox = new RichTextBox();
        _chatLabel = new Label();
        SuspendLayout();
        // 
        // _chatRichTextBox
        // 
        _chatRichTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _chatRichTextBox.Location = new Point(0, 20);
        _chatRichTextBox.Name = "_chatRichTextBox";
        _chatRichTextBox.ReadOnly = true;
        _chatRichTextBox.Size = new Size(375, 274);
        _chatRichTextBox.TabIndex = 1;
        _chatRichTextBox.Text = "";
        // 
        // _chatLabel
        // 
        _chatLabel.AutoSize = true;
        _chatLabel.Location = new Point(3, 2);
        _chatLabel.Name = "_chatLabel";
        _chatLabel.Size = new Size(29, 15);
        _chatLabel.TabIndex = 0;
        _chatLabel.Text = "Чат:";
        // 
        // ChatDisplay
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_chatLabel);
        Controls.Add(_chatRichTextBox);
        Name = "ChatDisplay";
        Size = new Size(375, 294);
        Load += OnLoad;
        ResumeLayout(false);
        PerformLayout();
    }

    private RichTextBox _chatRichTextBox;
    private Label _chatLabel;

    #endregion
}
