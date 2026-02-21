

namespace PoproshaykaBot.WinForms;

sealed partial class UserStatisticsForm
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;
    private TextBox textBoxFilter;
    private ListView listViewUsers;
    private ColumnHeader columnHeaderName;
    private ColumnHeader columnHeaderMessages;
    private ColumnHeader columnHeaderRank;
    private Button buttonAction;
    private Label labelUserId;
    private Label labelUserName;
    private Label labelMessageTotal;
    private Label labelMessageWritten;
    private Label labelMessageBonus;
    private Label labelMessagePenalty;
    private Label labelChessPiece;
    private NumericUpDown numericIncrement;
    private TableLayoutPanel tableLayoutPanelMain;
    private Panel panelTop;
    private Button buttonClearFilter;
    private GroupBox groupBoxDetails;
    private GroupBox groupBoxActions;
    private GroupBox groupBoxGlobalStats;
    private TableLayoutPanel tableLayoutPanelDetails;
    private TableLayoutPanel tableLayoutPanelActions;
    private TableLayoutPanel tableLayoutPanelGlobal;
    private Label labelGlobalUsers;
    private Label labelGlobalTotal;
    private Label labelGlobalWritten;
    private Label labelGlobalBonus;

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

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        var resources = new System.ComponentModel.ComponentResourceManager(typeof(UserStatisticsForm));
        textBoxFilter = new TextBox();
        listViewUsers = new ListView();
        columnHeaderName = new ColumnHeader();
        columnHeaderMessages = new ColumnHeader();
        columnHeaderRank = new ColumnHeader();
        buttonAction = new Button();
        labelUserId = new Label();
        labelUserName = new Label();
        labelMessageTotal = new Label();
        labelMessageWritten = new Label();
        labelMessageBonus = new Label();
        labelMessagePenalty = new Label();
        labelChessPiece = new Label();
        numericIncrement = new NumericUpDown();
        tableLayoutPanelMain = new TableLayoutPanel();
        panelTop = new Panel();
        buttonClearFilter = new Button();
        groupBoxGlobalStats = new GroupBox();
        tableLayoutPanelGlobal = new TableLayoutPanel();
        labelGlobalUsers = new Label();
        labelGlobalTotal = new Label();
        labelGlobalWritten = new Label();
        labelGlobalBonus = new Label();
        groupBoxDetails = new GroupBox();
        tableLayoutPanelDetails = new TableLayoutPanel();
        groupBoxActions = new GroupBox();
        tableLayoutPanelActions = new TableLayoutPanel();
        ((System.ComponentModel.ISupportInitialize)numericIncrement).BeginInit();
        tableLayoutPanelMain.SuspendLayout();
        panelTop.SuspendLayout();
        groupBoxGlobalStats.SuspendLayout();
        tableLayoutPanelGlobal.SuspendLayout();
        groupBoxDetails.SuspendLayout();
        tableLayoutPanelDetails.SuspendLayout();
        groupBoxActions.SuspendLayout();
        tableLayoutPanelActions.SuspendLayout();
        SuspendLayout();
        // 
        // textBoxFilter
        // 
        textBoxFilter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        textBoxFilter.Location = new Point(9, 9);
        textBoxFilter.Name = "textBoxFilter";
        textBoxFilter.PlaceholderText = "🔍 Поиск пользователя (Имя или ID)...";
        textBoxFilter.Size = new Size(674, 23);
        textBoxFilter.TabIndex = 0;
        textBoxFilter.TextChanged += textBoxFilter_TextChanged;
        // 
        // listViewUsers
        // 
        listViewUsers.Columns.AddRange(new ColumnHeader[] { columnHeaderName, columnHeaderMessages, columnHeaderRank });
        listViewUsers.Dock = DockStyle.Fill;
        listViewUsers.FullRowSelect = true;
        listViewUsers.GridLines = true;
        listViewUsers.Location = new Point(3, 48);
        listViewUsers.MultiSelect = false;
        listViewUsers.Name = "listViewUsers";
        tableLayoutPanelMain.SetRowSpan(listViewUsers, 3);
        listViewUsers.Size = new Size(458, 410);
        listViewUsers.TabIndex = 1;
        listViewUsers.UseCompatibleStateImageBehavior = false;
        listViewUsers.View = View.Details;
        listViewUsers.SelectedIndexChanged += listBoxUsers_SelectedIndexChanged;
        // 
        // columnHeaderName
        // 
        columnHeaderName.Text = "Пользователь";
        columnHeaderName.Width = 200;
        // 
        // columnHeaderMessages
        // 
        columnHeaderMessages.Text = "Сообщения";
        columnHeaderMessages.Width = 100;
        // 
        // columnHeaderRank
        // 
        columnHeaderRank.Text = "Ранг";
        columnHeaderRank.Width = 150;
        // 
        // buttonAction
        // 
        buttonAction.Dock = DockStyle.Fill;
        buttonAction.Enabled = false;
        buttonAction.Location = new Point(3, 38);
        buttonAction.Name = "buttonAction";
        buttonAction.Size = new Size(292, 51);
        buttonAction.TabIndex = 1;
        buttonAction.Text = "Добавить сообщения";
        buttonAction.UseVisualStyleBackColor = true;
        buttonAction.Click += buttonAction_Click;
        // 
        // labelUserId
        // 
        labelUserId.AutoSize = true;
        labelUserId.Dock = DockStyle.Fill;
        labelUserId.Location = new Point(3, 0);
        labelUserId.Name = "labelUserId";
        labelUserId.Size = new Size(292, 25);
        labelUserId.TabIndex = 0;
        labelUserId.Text = "🆔 ID: —";
        labelUserId.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // labelUserName
        // 
        labelUserName.AutoSize = true;
        labelUserName.Dock = DockStyle.Fill;
        labelUserName.Location = new Point(3, 25);
        labelUserName.Name = "labelUserName";
        labelUserName.Size = new Size(292, 25);
        labelUserName.TabIndex = 1;
        labelUserName.Text = "👤 Имя: —";
        labelUserName.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // labelMessageTotal
        // 
        labelMessageTotal.AutoSize = true;
        labelMessageTotal.Dock = DockStyle.Fill;
        labelMessageTotal.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        labelMessageTotal.Location = new Point(3, 90);
        labelMessageTotal.Name = "labelMessageTotal";
        labelMessageTotal.Size = new Size(292, 30);
        labelMessageTotal.TabIndex = 2;
        labelMessageTotal.Text = "💬 Всего: 0";
        labelMessageTotal.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // labelMessageWritten
        // 
        labelMessageWritten.AutoSize = true;
        labelMessageWritten.Dock = DockStyle.Fill;
        labelMessageWritten.ForeColor = Color.DarkSlateGray;
        labelMessageWritten.Location = new Point(3, 120);
        labelMessageWritten.Name = "labelMessageWritten";
        labelMessageWritten.Size = new Size(292, 22);
        labelMessageWritten.TabIndex = 4;
        labelMessageWritten.Text = "    ✍️ Написано: 0";
        labelMessageWritten.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // labelMessageBonus
        // 
        labelMessageBonus.AutoSize = true;
        labelMessageBonus.Dock = DockStyle.Fill;
        labelMessageBonus.ForeColor = Color.DarkGreen;
        labelMessageBonus.Location = new Point(3, 142);
        labelMessageBonus.Name = "labelMessageBonus";
        labelMessageBonus.Size = new Size(292, 22);
        labelMessageBonus.TabIndex = 5;
        labelMessageBonus.Text = "    🎁 Бонус: 0";
        labelMessageBonus.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // labelMessagePenalty
        // 
        labelMessagePenalty.AutoSize = true;
        labelMessagePenalty.Dock = DockStyle.Fill;
        labelMessagePenalty.ForeColor = Color.DarkRed;
        labelMessagePenalty.Location = new Point(3, 164);
        labelMessagePenalty.Name = "labelMessagePenalty";
        labelMessagePenalty.Size = new Size(292, 22);
        labelMessagePenalty.TabIndex = 6;
        labelMessagePenalty.Text = "    🚫 Штраф: 0";
        labelMessagePenalty.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // labelChessPiece
        // 
        labelChessPiece.AutoSize = true;
        labelChessPiece.Dock = DockStyle.Fill;
        labelChessPiece.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        labelChessPiece.Location = new Point(3, 50);
        labelChessPiece.Name = "labelChessPiece";
        labelChessPiece.Size = new Size(292, 40);
        labelChessPiece.TabIndex = 3;
        labelChessPiece.Text = "♟ ПЕШКА";
        labelChessPiece.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // numericIncrement
        // 
        numericIncrement.Dock = DockStyle.Fill;
        numericIncrement.Location = new Point(3, 3);
        numericIncrement.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
        numericIncrement.Minimum = new decimal(new int[] { 1000000, 0, 0, int.MinValue });
        numericIncrement.Name = "numericIncrement";
        numericIncrement.Size = new Size(292, 23);
        numericIncrement.TabIndex = 0;
        // 
        // tableLayoutPanelMain
        // 
        tableLayoutPanelMain.ColumnCount = 2;
        tableLayoutPanelMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        tableLayoutPanelMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        tableLayoutPanelMain.Controls.Add(panelTop, 0, 0);
        tableLayoutPanelMain.Controls.Add(listViewUsers, 0, 1);
        tableLayoutPanelMain.Controls.Add(groupBoxGlobalStats, 1, 1);
        tableLayoutPanelMain.Controls.Add(groupBoxDetails, 1, 2);
        tableLayoutPanelMain.Controls.Add(groupBoxActions, 1, 3);
        tableLayoutPanelMain.Dock = DockStyle.Fill;
        tableLayoutPanelMain.Location = new Point(0, 0);
        tableLayoutPanelMain.Name = "tableLayoutPanelMain";
        tableLayoutPanelMain.RowCount = 4;
        tableLayoutPanelMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        tableLayoutPanelMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
        tableLayoutPanelMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanelMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
        tableLayoutPanelMain.Size = new Size(774, 461);
        tableLayoutPanelMain.TabIndex = 0;
        // 
        // panelTop
        // 
        tableLayoutPanelMain.SetColumnSpan(panelTop, 2);
        panelTop.Controls.Add(buttonClearFilter);
        panelTop.Controls.Add(textBoxFilter);
        panelTop.Dock = DockStyle.Fill;
        panelTop.Location = new Point(3, 3);
        panelTop.Name = "panelTop";
        panelTop.Size = new Size(768, 39);
        panelTop.TabIndex = 0;
        // 
        // buttonClearFilter
        // 
        buttonClearFilter.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        buttonClearFilter.Location = new Point(689, 8);
        buttonClearFilter.Name = "buttonClearFilter";
        buttonClearFilter.Size = new Size(70, 25);
        buttonClearFilter.TabIndex = 1;
        buttonClearFilter.Text = "Очистить";
        buttonClearFilter.UseVisualStyleBackColor = true;
        buttonClearFilter.Click += ButtonClearFilterOnClick;
        // 
        // groupBoxGlobalStats
        // 
        groupBoxGlobalStats.Controls.Add(tableLayoutPanelGlobal);
        groupBoxGlobalStats.Dock = DockStyle.Fill;
        groupBoxGlobalStats.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        groupBoxGlobalStats.Location = new Point(467, 48);
        groupBoxGlobalStats.Name = "groupBoxGlobalStats";
        groupBoxGlobalStats.Size = new Size(304, 114);
        groupBoxGlobalStats.TabIndex = 4;
        groupBoxGlobalStats.TabStop = false;
        groupBoxGlobalStats.Text = "🌍 Общая статистика";
        // 
        // tableLayoutPanelGlobal
        // 
        tableLayoutPanelGlobal.ColumnCount = 1;
        tableLayoutPanelGlobal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanelGlobal.Controls.Add(labelGlobalUsers, 0, 0);
        tableLayoutPanelGlobal.Controls.Add(labelGlobalTotal, 0, 1);
        tableLayoutPanelGlobal.Controls.Add(labelGlobalWritten, 0, 2);
        tableLayoutPanelGlobal.Controls.Add(labelGlobalBonus, 0, 3);
        tableLayoutPanelGlobal.Dock = DockStyle.Fill;
        tableLayoutPanelGlobal.Font = new Font("Segoe UI", 9F);
        tableLayoutPanelGlobal.Location = new Point(3, 19);
        tableLayoutPanelGlobal.Name = "tableLayoutPanelGlobal";
        tableLayoutPanelGlobal.RowCount = 4;
        tableLayoutPanelGlobal.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tableLayoutPanelGlobal.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tableLayoutPanelGlobal.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tableLayoutPanelGlobal.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tableLayoutPanelGlobal.Size = new Size(298, 92);
        tableLayoutPanelGlobal.TabIndex = 0;
        // 
        // labelGlobalUsers
        // 
        labelGlobalUsers.AutoSize = true;
        labelGlobalUsers.Location = new Point(3, 0);
        labelGlobalUsers.Name = "labelGlobalUsers";
        labelGlobalUsers.Size = new Size(118, 15);
        labelGlobalUsers.TabIndex = 0;
        labelGlobalUsers.Text = "👥 Пользователей: 0";
        // 
        // labelGlobalTotal
        // 
        labelGlobalTotal.AutoSize = true;
        labelGlobalTotal.Location = new Point(3, 22);
        labelGlobalTotal.Name = "labelGlobalTotal";
        labelGlobalTotal.Size = new Size(106, 15);
        labelGlobalTotal.TabIndex = 1;
        labelGlobalTotal.Text = "💬 Всего сообщ: 0";
        // 
        // labelGlobalWritten
        // 
        labelGlobalWritten.AutoSize = true;
        labelGlobalWritten.Location = new Point(3, 44);
        labelGlobalWritten.Name = "labelGlobalWritten";
        labelGlobalWritten.Size = new Size(89, 15);
        labelGlobalWritten.TabIndex = 2;
        labelGlobalWritten.Text = "✍️ Написано: 0";
        // 
        // labelGlobalBonus
        // 
        labelGlobalBonus.AutoSize = true;
        labelGlobalBonus.Location = new Point(3, 66);
        labelGlobalBonus.Name = "labelGlobalBonus";
        labelGlobalBonus.Size = new Size(110, 15);
        labelGlobalBonus.TabIndex = 3;
        labelGlobalBonus.Text = "🎁 Бонус/Штраф: 0";
        // 
        // groupBoxDetails
        // 
        groupBoxDetails.Controls.Add(tableLayoutPanelDetails);
        groupBoxDetails.Dock = DockStyle.Fill;
        groupBoxDetails.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        groupBoxDetails.Location = new Point(467, 168);
        groupBoxDetails.Name = "groupBoxDetails";
        groupBoxDetails.Size = new Size(304, 170);
        groupBoxDetails.TabIndex = 2;
        groupBoxDetails.TabStop = false;
        groupBoxDetails.Text = "📋 Информация о пользователе";
        // 
        // tableLayoutPanelDetails
        // 
        tableLayoutPanelDetails.ColumnCount = 1;
        tableLayoutPanelDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanelDetails.Controls.Add(labelUserId, 0, 0);
        tableLayoutPanelDetails.Controls.Add(labelUserName, 0, 1);
        tableLayoutPanelDetails.Controls.Add(labelChessPiece, 0, 2);
        tableLayoutPanelDetails.Controls.Add(labelMessageTotal, 0, 3);
        tableLayoutPanelDetails.Controls.Add(labelMessageWritten, 0, 4);
        tableLayoutPanelDetails.Controls.Add(labelMessageBonus, 0, 5);
        tableLayoutPanelDetails.Controls.Add(labelMessagePenalty, 0, 6);
        tableLayoutPanelDetails.Dock = DockStyle.Fill;
        tableLayoutPanelDetails.Font = new Font("Segoe UI", 9F);
        tableLayoutPanelDetails.Location = new Point(3, 19);
        tableLayoutPanelDetails.Name = "tableLayoutPanelDetails";
        tableLayoutPanelDetails.RowCount = 8;
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanelDetails.Size = new Size(298, 148);
        tableLayoutPanelDetails.TabIndex = 0;
        // 
        // groupBoxActions
        // 
        groupBoxActions.Controls.Add(tableLayoutPanelActions);
        groupBoxActions.Dock = DockStyle.Fill;
        groupBoxActions.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        groupBoxActions.Location = new Point(467, 344);
        groupBoxActions.Name = "groupBoxActions";
        groupBoxActions.Size = new Size(304, 114);
        groupBoxActions.TabIndex = 3;
        groupBoxActions.TabStop = false;
        groupBoxActions.Text = "🛠 Управление сообщениями";
        // 
        // tableLayoutPanelActions
        // 
        tableLayoutPanelActions.ColumnCount = 1;
        tableLayoutPanelActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanelActions.Controls.Add(numericIncrement, 0, 0);
        tableLayoutPanelActions.Controls.Add(buttonAction, 0, 1);
        tableLayoutPanelActions.Dock = DockStyle.Fill;
        tableLayoutPanelActions.Font = new Font("Segoe UI", 9F);
        tableLayoutPanelActions.Location = new Point(3, 19);
        tableLayoutPanelActions.Name = "tableLayoutPanelActions";
        tableLayoutPanelActions.RowCount = 2;
        tableLayoutPanelActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
        tableLayoutPanelActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        tableLayoutPanelActions.Size = new Size(298, 92);
        tableLayoutPanelActions.TabIndex = 0;
        // 
        // UserStatisticsForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(774, 461);
        Controls.Add(tableLayoutPanelMain);
        Icon = (Icon)resources.GetObject("$this.Icon");
        MinimumSize = new Size(700, 500);
        Name = "UserStatisticsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "📊 Статистика пользователей 👥";
        ((System.ComponentModel.ISupportInitialize)numericIncrement).EndInit();
        tableLayoutPanelMain.ResumeLayout(false);
        panelTop.ResumeLayout(false);
        panelTop.PerformLayout();
        groupBoxGlobalStats.ResumeLayout(false);
        tableLayoutPanelGlobal.ResumeLayout(false);
        tableLayoutPanelGlobal.PerformLayout();
        groupBoxDetails.ResumeLayout(false);
        tableLayoutPanelDetails.ResumeLayout(false);
        tableLayoutPanelDetails.PerformLayout();
        groupBoxActions.ResumeLayout(false);
        tableLayoutPanelActions.ResumeLayout(false);
        ResumeLayout(false);
    }
}
