

namespace PoproshaykaBot.WinForms.Forms.StreamHistory;

sealed partial class StreamHistoryForm
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel tableLayoutPanelMain;
    private Panel panelTop;
    private Label labelSummary;
    private Button buttonRefresh;
    private ListView listViewSessions;
    private ColumnHeader columnHeaderStarted;
    private ColumnHeader columnHeaderDuration;
    private ColumnHeader columnHeaderGame;
    private ColumnHeader columnHeaderMessages;
    private ColumnHeader columnHeaderChatters;
    private ColumnHeader columnHeaderPeak;
    private ColumnHeader columnHeaderAverage;
    private GroupBox groupBoxDetails;
    private TableLayoutPanel tableLayoutPanelDetails;
    private Label labelTitle;
    private Label labelGame;
    private Label labelStarted;
    private Label labelEnded;
    private Label labelDuration;
    private Label labelMessages;
    private Label labelChatters;
    private Label labelViewers;
    private Label labelChattersHeader;
    private ListView listViewChatters;
    private ColumnHeader columnHeaderChatterName;
    private ColumnHeader columnHeaderChatterMessages;

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
        tableLayoutPanelMain = new TableLayoutPanel();
        panelTop = new Panel();
        labelSummary = new Label();
        buttonRefresh = new Button();
        listViewSessions = new ListView();
        columnHeaderStarted = new ColumnHeader();
        columnHeaderDuration = new ColumnHeader();
        columnHeaderGame = new ColumnHeader();
        columnHeaderMessages = new ColumnHeader();
        columnHeaderChatters = new ColumnHeader();
        columnHeaderPeak = new ColumnHeader();
        columnHeaderAverage = new ColumnHeader();
        groupBoxDetails = new GroupBox();
        tableLayoutPanelDetails = new TableLayoutPanel();
        labelTitle = new Label();
        labelGame = new Label();
        labelStarted = new Label();
        labelEnded = new Label();
        labelDuration = new Label();
        labelMessages = new Label();
        labelChatters = new Label();
        labelViewers = new Label();
        labelChattersHeader = new Label();
        listViewChatters = new ListView();
        columnHeaderChatterName = new ColumnHeader();
        columnHeaderChatterMessages = new ColumnHeader();
        tableLayoutPanelMain.SuspendLayout();
        panelTop.SuspendLayout();
        groupBoxDetails.SuspendLayout();
        tableLayoutPanelDetails.SuspendLayout();
        SuspendLayout();
        //
        // tableLayoutPanelMain
        //
        tableLayoutPanelMain.ColumnCount = 2;
        tableLayoutPanelMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        tableLayoutPanelMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        tableLayoutPanelMain.Controls.Add(panelTop, 0, 0);
        tableLayoutPanelMain.Controls.Add(listViewSessions, 0, 1);
        tableLayoutPanelMain.Controls.Add(groupBoxDetails, 1, 1);
        tableLayoutPanelMain.Dock = DockStyle.Fill;
        tableLayoutPanelMain.Location = new Point(0, 0);
        tableLayoutPanelMain.Name = "tableLayoutPanelMain";
        tableLayoutPanelMain.RowCount = 2;
        tableLayoutPanelMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        tableLayoutPanelMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanelMain.Size = new Size(884, 521);
        tableLayoutPanelMain.TabIndex = 0;
        //
        // panelTop
        //
        tableLayoutPanelMain.SetColumnSpan(panelTop, 2);
        panelTop.Controls.Add(buttonRefresh);
        panelTop.Controls.Add(labelSummary);
        panelTop.Dock = DockStyle.Fill;
        panelTop.Location = new Point(3, 3);
        panelTop.Name = "panelTop";
        panelTop.Size = new Size(878, 39);
        panelTop.TabIndex = 0;
        //
        // labelSummary
        //
        labelSummary.Anchor = AnchorStyles.Left;
        labelSummary.AutoSize = true;
        labelSummary.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        labelSummary.Location = new Point(6, 12);
        labelSummary.Name = "labelSummary";
        labelSummary.Size = new Size(120, 15);
        labelSummary.TabIndex = 0;
        labelSummary.Text = "🎬 Стримов: 0";
        //
        // buttonRefresh
        //
        buttonRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        buttonRefresh.Location = new Point(765, 7);
        buttonRefresh.Name = "buttonRefresh";
        buttonRefresh.Size = new Size(110, 27);
        buttonRefresh.TabIndex = 1;
        buttonRefresh.Text = "🔄 Обновить";
        buttonRefresh.UseVisualStyleBackColor = true;
        buttonRefresh.Click += buttonRefresh_Click;
        //
        // listViewSessions
        //
        listViewSessions.Columns.AddRange(new ColumnHeader[] { columnHeaderStarted, columnHeaderDuration, columnHeaderGame, columnHeaderMessages, columnHeaderChatters, columnHeaderPeak, columnHeaderAverage });
        listViewSessions.Dock = DockStyle.Fill;
        listViewSessions.FullRowSelect = true;
        listViewSessions.GridLines = true;
        listViewSessions.Location = new Point(3, 48);
        listViewSessions.MultiSelect = false;
        listViewSessions.Name = "listViewSessions";
        listViewSessions.Size = new Size(524, 470);
        listViewSessions.TabIndex = 1;
        listViewSessions.UseCompatibleStateImageBehavior = false;
        listViewSessions.View = View.Details;
        listViewSessions.ColumnClick += listViewSessions_ColumnClick;
        listViewSessions.SelectedIndexChanged += listViewSessions_SelectedIndexChanged;
        //
        // columnHeaderStarted
        //
        columnHeaderStarted.Text = "Начало";
        columnHeaderStarted.Width = 150;
        //
        // columnHeaderDuration
        //
        columnHeaderDuration.Text = "Длительность";
        columnHeaderDuration.Width = 110;
        //
        // columnHeaderGame
        //
        columnHeaderGame.Text = "Игра";
        columnHeaderGame.Width = 150;
        //
        // columnHeaderMessages
        //
        columnHeaderMessages.Text = "Сообщения";
        columnHeaderMessages.TextAlign = HorizontalAlignment.Right;
        columnHeaderMessages.Width = 90;
        //
        // columnHeaderChatters
        //
        columnHeaderChatters.Text = "Чаттеры";
        columnHeaderChatters.TextAlign = HorizontalAlignment.Right;
        columnHeaderChatters.Width = 75;
        //
        // columnHeaderPeak
        //
        columnHeaderPeak.Text = "Пик";
        columnHeaderPeak.TextAlign = HorizontalAlignment.Right;
        columnHeaderPeak.Width = 60;
        //
        // columnHeaderAverage
        //
        columnHeaderAverage.Text = "Средн.";
        columnHeaderAverage.TextAlign = HorizontalAlignment.Right;
        columnHeaderAverage.Width = 60;
        //
        // groupBoxDetails
        //
        groupBoxDetails.Controls.Add(tableLayoutPanelDetails);
        groupBoxDetails.Dock = DockStyle.Fill;
        groupBoxDetails.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        groupBoxDetails.Location = new Point(533, 48);
        groupBoxDetails.Name = "groupBoxDetails";
        groupBoxDetails.Size = new Size(348, 470);
        groupBoxDetails.TabIndex = 2;
        groupBoxDetails.TabStop = false;
        groupBoxDetails.Text = "📋 Детали сессии";
        //
        // tableLayoutPanelDetails
        //
        tableLayoutPanelDetails.ColumnCount = 1;
        tableLayoutPanelDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanelDetails.Controls.Add(labelTitle, 0, 0);
        tableLayoutPanelDetails.Controls.Add(labelGame, 0, 1);
        tableLayoutPanelDetails.Controls.Add(labelStarted, 0, 2);
        tableLayoutPanelDetails.Controls.Add(labelEnded, 0, 3);
        tableLayoutPanelDetails.Controls.Add(labelDuration, 0, 4);
        tableLayoutPanelDetails.Controls.Add(labelMessages, 0, 5);
        tableLayoutPanelDetails.Controls.Add(labelChatters, 0, 6);
        tableLayoutPanelDetails.Controls.Add(labelViewers, 0, 7);
        tableLayoutPanelDetails.Controls.Add(labelChattersHeader, 0, 8);
        tableLayoutPanelDetails.Controls.Add(listViewChatters, 0, 9);
        tableLayoutPanelDetails.Dock = DockStyle.Fill;
        tableLayoutPanelDetails.Font = new Font("Segoe UI", 9F);
        tableLayoutPanelDetails.Location = new Point(3, 19);
        tableLayoutPanelDetails.Name = "tableLayoutPanelDetails";
        tableLayoutPanelDetails.Padding = new Padding(4);
        tableLayoutPanelDetails.RowCount = 10;
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        tableLayoutPanelDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanelDetails.Size = new Size(342, 448);
        tableLayoutPanelDetails.TabIndex = 0;
        //
        // labelTitle
        //
        labelTitle.AutoEllipsis = true;
        labelTitle.Dock = DockStyle.Fill;
        labelTitle.Location = new Point(7, 4);
        labelTitle.Name = "labelTitle";
        labelTitle.Size = new Size(328, 24);
        labelTitle.TabIndex = 0;
        labelTitle.Text = "📝 Заголовок: —";
        labelTitle.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelGame
        //
        labelGame.AutoEllipsis = true;
        labelGame.Dock = DockStyle.Fill;
        labelGame.Location = new Point(7, 28);
        labelGame.Name = "labelGame";
        labelGame.Size = new Size(328, 24);
        labelGame.TabIndex = 1;
        labelGame.Text = "🎮 Игра: —";
        labelGame.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelStarted
        //
        labelStarted.Dock = DockStyle.Fill;
        labelStarted.Location = new Point(7, 52);
        labelStarted.Name = "labelStarted";
        labelStarted.Size = new Size(328, 24);
        labelStarted.TabIndex = 2;
        labelStarted.Text = "▶ Начало: —";
        labelStarted.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelEnded
        //
        labelEnded.Dock = DockStyle.Fill;
        labelEnded.Location = new Point(7, 76);
        labelEnded.Name = "labelEnded";
        labelEnded.Size = new Size(328, 24);
        labelEnded.TabIndex = 3;
        labelEnded.Text = "⏹ Окончание: —";
        labelEnded.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelDuration
        //
        labelDuration.Dock = DockStyle.Fill;
        labelDuration.Location = new Point(7, 100);
        labelDuration.Name = "labelDuration";
        labelDuration.Size = new Size(328, 24);
        labelDuration.TabIndex = 4;
        labelDuration.Text = "⏱️ Длительность: —";
        labelDuration.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelMessages
        //
        labelMessages.Dock = DockStyle.Fill;
        labelMessages.Location = new Point(7, 124);
        labelMessages.Name = "labelMessages";
        labelMessages.Size = new Size(328, 24);
        labelMessages.TabIndex = 5;
        labelMessages.Text = "💬 Сообщений: —";
        labelMessages.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelChatters
        //
        labelChatters.Dock = DockStyle.Fill;
        labelChatters.Location = new Point(7, 148);
        labelChatters.Name = "labelChatters";
        labelChatters.Size = new Size(328, 24);
        labelChatters.TabIndex = 6;
        labelChatters.Text = "👥 Чаттеров: —";
        labelChatters.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelViewers
        //
        labelViewers.Dock = DockStyle.Fill;
        labelViewers.Location = new Point(7, 172);
        labelViewers.Name = "labelViewers";
        labelViewers.Size = new Size(328, 24);
        labelViewers.TabIndex = 7;
        labelViewers.Text = "👁 Зрители: пик — / средн. —";
        labelViewers.TextAlign = ContentAlignment.MiddleLeft;
        //
        // labelChattersHeader
        //
        labelChattersHeader.Dock = DockStyle.Fill;
        labelChattersHeader.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        labelChattersHeader.Location = new Point(7, 196);
        labelChattersHeader.Name = "labelChattersHeader";
        labelChattersHeader.Size = new Size(328, 30);
        labelChattersHeader.TabIndex = 8;
        labelChattersHeader.Text = "👥 Чаттеры";
        labelChattersHeader.TextAlign = ContentAlignment.MiddleLeft;
        //
        // listViewChatters
        //
        listViewChatters.Columns.AddRange(new ColumnHeader[] { columnHeaderChatterName, columnHeaderChatterMessages });
        listViewChatters.Dock = DockStyle.Fill;
        listViewChatters.FullRowSelect = true;
        listViewChatters.GridLines = true;
        listViewChatters.Location = new Point(7, 229);
        listViewChatters.MultiSelect = false;
        listViewChatters.Name = "listViewChatters";
        listViewChatters.Size = new Size(328, 215);
        listViewChatters.TabIndex = 9;
        listViewChatters.UseCompatibleStateImageBehavior = false;
        listViewChatters.View = View.Details;
        //
        // columnHeaderChatterName
        //
        columnHeaderChatterName.Text = "Зритель";
        columnHeaderChatterName.Width = 210;
        //
        // columnHeaderChatterMessages
        //
        columnHeaderChatterMessages.Text = "Сообщений";
        columnHeaderChatterMessages.TextAlign = HorizontalAlignment.Right;
        columnHeaderChatterMessages.Width = 100;
        //
        // StreamHistoryForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(884, 521);
        Controls.Add(tableLayoutPanelMain);
        MinimumSize = new Size(760, 480);
        Name = "StreamHistoryForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "🎬 История стримов";
        tableLayoutPanelMain.ResumeLayout(false);
        panelTop.ResumeLayout(false);
        panelTop.PerformLayout();
        groupBoxDetails.ResumeLayout(false);
        tableLayoutPanelDetails.ResumeLayout(false);
        ResumeLayout(false);
    }
}
