

namespace PoproshaykaBot.WinForms.Forms.Users;

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
    private ColumnHeader columnHeaderPoints;
    private ColumnHeader columnHeaderRank;
    private Button buttonAction;
    private Label labelUserId;
    private Label labelUserName;
    private Label labelMessages;
    private Label labelPoints;
    private Label labelBonus;
    private Label labelPenalty;
    private Label labelChessPiece;
    private NumericUpDown numericIncrement;
    private TableLayoutPanel tableLayoutPanelMain;
    private Panel panelTop;
    private Button buttonClearFilter;
    private Button buttonPointTerm;
    private GroupBox groupBoxDetails;
    private GroupBox groupBoxActions;
    private GroupBox groupBoxGlobalStats;
    private TableLayoutPanel tableLayoutPanelDetails;
    private TableLayoutPanel tableLayoutPanelActions;
    private TableLayoutPanel tableLayoutPanelGlobal;
    private Label labelGlobalUsers;
    private Label labelGlobalMessages;
    private Label labelGlobalPoints;
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
        columnHeaderPoints = new ColumnHeader();
        columnHeaderRank = new ColumnHeader();
        buttonAction = new Button();
        labelUserId = new Label();
        labelUserName = new Label();
        labelMessages = new Label();
        labelPoints = new Label();
        labelBonus = new Label();
        labelPenalty = new Label();
        labelChessPiece = new Label();
        numericIncrement = new NumericUpDown();
        tableLayoutPanelMain = new TableLayoutPanel();
        panelTop = new Panel();
        buttonClearFilter = new Button();
        buttonPointTerm = new Button();
        groupBoxGlobalStats = new GroupBox();
        tableLayoutPanelGlobal = new TableLayoutPanel();
        labelGlobalUsers = new Label();
        labelGlobalMessages = new Label();
        labelGlobalPoints = new Label();
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
        textBoxFilter.Size = new Size(558, 23);
        textBoxFilter.TabIndex = 0;
        textBoxFilter.TextChanged += textBoxFilter_TextChanged;
        textBoxFilter.KeyDown += textBoxFilter_KeyDown;
        //
        // listViewUsers
        //
        listViewUsers.Columns.AddRange(new ColumnHeader[] { columnHeaderName, columnHeaderMessages, columnHeaderPoints, columnHeaderRank });
        listViewUsers.Dock = DockStyle.Fill;
        listViewUsers.FullRowSelect = true;
        listViewUsers.GridLines = true;
        listViewUsers.Location = new Point(3, 48);
        listViewUsers.MultiSelect = false;
        listViewUsers.Name = "listViewUsers";
        tableLayoutPanelMain.SetRowSpan(listViewUsers, 3);
        listViewUsers.Size = new Size(478, 448);
        listViewUsers.TabIndex = 1;
        listViewUsers.UseCompatibleStateImageBehavior = false;
        listViewUsers.View = View.Details;
        listViewUsers.ColumnClick += listViewUsers_ColumnClick;
        listViewUsers.SelectedIndexChanged += listViewUsers_SelectedIndexChanged;
        listViewUsers.MouseDoubleClick += listViewUsers_MouseDoubleClick;
        //
        // columnHeaderName
        //
        columnHeaderName.Text = "Пользователь";
        columnHeaderName.Width = 170;
        //
        // columnHeaderMessages
        //
        columnHeaderMessages.Text = "Сообщения";
        columnHeaderMessages.Width = 90;
        //
        // columnHeaderPoints
        //
        columnHeaderPoints.Text = "Баллы";
        columnHeaderPoints.Width = 80;
        //
        // columnHeaderRank
        //
        columnHeaderRank.Text = "Ранг";
        columnHeaderRank.Width = 130;
        //
        // buttonAction
        //
        buttonAction.Dock = DockStyle.Fill;
        buttonAction.Enabled = false;
        buttonAction.Location = new Point(3, 38);
        buttonAction.Name = "buttonAction";
        buttonAction.Size = new Size(305, 51);
        buttonAction.TabIndex = 1;
        buttonAction.Text = "Добавить баллы";
        buttonAction.UseVisualStyleBackColor = true;
        buttonAction.Click += buttonAction_Click;
        //
        // labelUserId
        //
        labelUserId.AutoSize = true;
        labelUserId.Dock = DockStyle.Fill;
        labelUserId.Location = new Point(3, 0);
        labelUserId.Name = "labelUserId";
        labelUserId.Size = new Size(305, 25);
        labelUserId.TabIndex = 0;
        labelUserId.Text = "🆔 ID: –";
        labelUserId.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelUserName
        //
        labelUserName.AutoSize = true;
        labelUserName.Dock = DockStyle.Fill;
        labelUserName.Location = new Point(3, 25);
        labelUserName.Name = "labelUserName";
        labelUserName.Size = new Size(305, 25);
        labelUserName.TabIndex = 1;
        labelUserName.Text = "👤 Имя: –";
        labelUserName.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelMessages
        //
        labelMessages.AutoSize = true;
        labelMessages.Dock = DockStyle.Fill;
        labelMessages.ForeColor = Color.DarkSlateGray;
        labelMessages.Location = new Point(3, 90);
        labelMessages.Name = "labelMessages";
        labelMessages.Size = new Size(305, 24);
        labelMessages.TabIndex = 4;
        labelMessages.Text = "💬 Сообщения: 0";
        labelMessages.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelPoints
        //
        labelPoints.AutoSize = true;
        labelPoints.Dock = DockStyle.Fill;
        labelPoints.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        labelPoints.Location = new Point(3, 114);
        labelPoints.Name = "labelPoints";
        labelPoints.Size = new Size(305, 30);
        labelPoints.TabIndex = 2;
        labelPoints.Text = "🏆 Баллы: 0";
        labelPoints.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelBonus
        //
        labelBonus.AutoSize = true;
        labelBonus.Dock = DockStyle.Fill;
        labelBonus.ForeColor = Color.DarkGreen;
        labelBonus.Location = new Point(3, 144);
        labelBonus.Name = "labelBonus";
        labelBonus.Size = new Size(305, 22);
        labelBonus.TabIndex = 5;
        labelBonus.Text = "    🎁 Бонус: 0";
        labelBonus.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelPenalty
        //
        labelPenalty.AutoSize = true;
        labelPenalty.Dock = DockStyle.Fill;
        labelPenalty.ForeColor = Color.DarkRed;
        labelPenalty.Location = new Point(3, 166);
        labelPenalty.Name = "labelPenalty";
        labelPenalty.Size = new Size(305, 22);
        labelPenalty.TabIndex = 6;
        labelPenalty.Text = "    🚫 Штраф: 0";
        labelPenalty.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelChessPiece
        //
        labelChessPiece.AutoSize = true;
        labelChessPiece.Dock = DockStyle.Fill;
        labelChessPiece.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        labelChessPiece.Location = new Point(3, 50);
        labelChessPiece.Name = "labelChessPiece";
        labelChessPiece.Size = new Size(305, 40);
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
        numericIncrement.Size = new Size(305, 23);
        numericIncrement.TabIndex = 0;
        numericIncrement.ValueChanged += numericIncrement_ValueChanged;
        numericIncrement.KeyDown += numericIncrement_KeyDown;
        numericIncrement.KeyUp += numericIncrement_KeyUp;
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
        tableLayoutPanelMain.Size = new Size(807, 499);
        tableLayoutPanelMain.TabIndex = 0;
        //
        // panelTop
        //
        tableLayoutPanelMain.SetColumnSpan(panelTop, 2);
        panelTop.Controls.Add(buttonClearFilter);
        panelTop.Controls.Add(buttonPointTerm);
        panelTop.Controls.Add(textBoxFilter);
        panelTop.Dock = DockStyle.Fill;
        panelTop.Location = new Point(3, 3);
        panelTop.Name = "panelTop";
        panelTop.Size = new Size(801, 39);
        panelTop.TabIndex = 0;
        //
        // buttonPointTerm
        //
        buttonPointTerm.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        buttonPointTerm.Location = new Point(572, 8);
        buttonPointTerm.Name = "buttonPointTerm";
        buttonPointTerm.Size = new Size(145, 25);
        buttonPointTerm.TabIndex = 1;
        buttonPointTerm.Text = "🏷 Названия баллов...";
        buttonPointTerm.UseVisualStyleBackColor = true;
        buttonPointTerm.Click += ButtonPointTermOnClick;
        //
        // buttonClearFilter
        //
        buttonClearFilter.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        buttonClearFilter.Location = new Point(722, 8);
        buttonClearFilter.Name = "buttonClearFilter";
        buttonClearFilter.Size = new Size(70, 25);
        buttonClearFilter.TabIndex = 2;
        buttonClearFilter.Text = "Очистить";
        buttonClearFilter.UseVisualStyleBackColor = true;
        buttonClearFilter.Click += ButtonClearFilterOnClick;
        //
        // groupBoxGlobalStats
        //
        groupBoxGlobalStats.Controls.Add(tableLayoutPanelGlobal);
        groupBoxGlobalStats.Dock = DockStyle.Fill;
        groupBoxGlobalStats.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        groupBoxGlobalStats.Location = new Point(487, 48);
        groupBoxGlobalStats.Name = "groupBoxGlobalStats";
        groupBoxGlobalStats.Size = new Size(317, 114);
        groupBoxGlobalStats.TabIndex = 4;
        groupBoxGlobalStats.TabStop = false;
        groupBoxGlobalStats.Text = "🌍 Общая статистика";
        //
        // tableLayoutPanelGlobal
        //
        tableLayoutPanelGlobal.ColumnCount = 1;
        tableLayoutPanelGlobal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanelGlobal.Controls.Add(labelGlobalUsers, 0, 0);
        tableLayoutPanelGlobal.Controls.Add(labelGlobalMessages, 0, 1);
        tableLayoutPanelGlobal.Controls.Add(labelGlobalPoints, 0, 2);
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
        tableLayoutPanelGlobal.Size = new Size(311, 92);
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
        // labelGlobalMessages
        //
        labelGlobalMessages.AutoSize = true;
        labelGlobalMessages.ForeColor = Color.DarkSlateGray;
        labelGlobalMessages.Location = new Point(3, 22);
        labelGlobalMessages.Name = "labelGlobalMessages";
        labelGlobalMessages.Size = new Size(106, 15);
        labelGlobalMessages.TabIndex = 1;
        labelGlobalMessages.Text = "💬 Сообщений: 0";
        //
        // labelGlobalPoints
        //
        labelGlobalPoints.AutoSize = true;
        labelGlobalPoints.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        labelGlobalPoints.Location = new Point(3, 44);
        labelGlobalPoints.Name = "labelGlobalPoints";
        labelGlobalPoints.Size = new Size(89, 15);
        labelGlobalPoints.TabIndex = 2;
        labelGlobalPoints.Text = "🏆 Баллов: 0";
        //
        // labelGlobalBonus
        //
        labelGlobalBonus.AutoSize = true;
        labelGlobalBonus.Location = new Point(3, 66);
        labelGlobalBonus.Name = "labelGlobalBonus";
        labelGlobalBonus.Size = new Size(109, 15);
        labelGlobalBonus.TabIndex = 3;
        labelGlobalBonus.Text = "🎁 Бонус: 0 / 🚫 Штраф: 0";
        //
        // groupBoxDetails
        //
        groupBoxDetails.Controls.Add(tableLayoutPanelDetails);
        groupBoxDetails.Dock = DockStyle.Fill;
        groupBoxDetails.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        groupBoxDetails.Location = new Point(487, 168);
        groupBoxDetails.Name = "groupBoxDetails";
        groupBoxDetails.Size = new Size(317, 208);
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
        tableLayoutPanelDetails.Controls.Add(labelMessages, 0, 3);
        tableLayoutPanelDetails.Controls.Add(labelPoints, 0, 4);
        tableLayoutPanelDetails.Controls.Add(labelBonus, 0, 5);
        tableLayoutPanelDetails.Controls.Add(labelPenalty, 0, 6);
        tableLayoutPanelDetails.Dock = DockStyle.Fill;
        tableLayoutPanelDetails.Font = new Font("Segoe UI", 9F);
        tableLayoutPanelDetails.Location = new Point(3, 19);
        tableLayoutPanelDetails.Name = "tableLayoutPanelDetails";
        tableLayoutPanelDetails.RowCount = 8;
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanelDetails.Size = new Size(311, 186);
        tableLayoutPanelDetails.TabIndex = 0;
        //
        // groupBoxActions
        //
        groupBoxActions.Controls.Add(tableLayoutPanelActions);
        groupBoxActions.Dock = DockStyle.Fill;
        groupBoxActions.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        groupBoxActions.Location = new Point(487, 382);
        groupBoxActions.Name = "groupBoxActions";
        groupBoxActions.Size = new Size(317, 114);
        groupBoxActions.TabIndex = 3;
        groupBoxActions.TabStop = false;
        groupBoxActions.Text = "🛠 Управление баллами";
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
        tableLayoutPanelActions.Size = new Size(311, 92);
        tableLayoutPanelActions.TabIndex = 0;
        //
        // UserStatisticsForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(807, 499);
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
