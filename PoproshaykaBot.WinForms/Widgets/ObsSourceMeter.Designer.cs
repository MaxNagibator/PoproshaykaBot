namespace PoproshaykaBot.WinForms.Widgets;

sealed partial class ObsSourceMeter
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _layoutTableLayoutPanel = new TableLayoutPanel();
        _stateLabel = new Label();
        _volumeMeterPanel = new Panel();
        _volumeMeterFillPanel = new Panel();
        _detailLabel = new Label();
        _layoutTableLayoutPanel.SuspendLayout();
        _volumeMeterPanel.SuspendLayout();
        SuspendLayout();
        //
        // _layoutTableLayoutPanel
        //
        _layoutTableLayoutPanel.ColumnCount = 1;
        _layoutTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _layoutTableLayoutPanel.Controls.Add(_stateLabel, 0, 0);
        _layoutTableLayoutPanel.Controls.Add(_volumeMeterPanel, 0, 1);
        _layoutTableLayoutPanel.Controls.Add(_detailLabel, 0, 2);
        _layoutTableLayoutPanel.Dock = DockStyle.Fill;
        _layoutTableLayoutPanel.Location = new Point(0, 0);
        _layoutTableLayoutPanel.Name = "_layoutTableLayoutPanel";
        _layoutTableLayoutPanel.RowCount = 3;
        _layoutTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        _layoutTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
        _layoutTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _layoutTableLayoutPanel.Size = new Size(280, 60);
        _layoutTableLayoutPanel.TabIndex = 0;
        //
        // _stateLabel
        //
        _stateLabel.AutoEllipsis = true;
        _stateLabel.BackColor = Color.Gray;
        _stateLabel.Dock = DockStyle.Fill;
        _stateLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _stateLabel.ForeColor = Color.White;
        _stateLabel.Location = new Point(0, 0);
        _stateLabel.Margin = new Padding(0);
        _stateLabel.Name = "_stateLabel";
        _stateLabel.Size = new Size(280, 24);
        _stateLabel.TabIndex = 0;
        _stateLabel.Text = "–";
        _stateLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _volumeMeterPanel
        //
        _volumeMeterPanel.BackColor = Color.Gainsboro;
        _volumeMeterPanel.BorderStyle = BorderStyle.FixedSingle;
        _volumeMeterPanel.Controls.Add(_volumeMeterFillPanel);
        _volumeMeterPanel.Dock = DockStyle.Fill;
        _volumeMeterPanel.Location = new Point(0, 26);
        _volumeMeterPanel.Margin = new Padding(0, 2, 0, 2);
        _volumeMeterPanel.Name = "_volumeMeterPanel";
        _volumeMeterPanel.Size = new Size(280, 10);
        _volumeMeterPanel.TabIndex = 1;
        _volumeMeterPanel.Resize += OnVolumeMeterPanelResize;
        //
        // _volumeMeterFillPanel
        //
        _volumeMeterFillPanel.BackColor = Color.SeaGreen;
        _volumeMeterFillPanel.Dock = DockStyle.Left;
        _volumeMeterFillPanel.Location = new Point(0, 0);
        _volumeMeterFillPanel.Name = "_volumeMeterFillPanel";
        _volumeMeterFillPanel.Size = new Size(0, 8);
        _volumeMeterFillPanel.TabIndex = 0;
        //
        // _detailLabel
        //
        _detailLabel.AutoEllipsis = true;
        _detailLabel.Dock = DockStyle.Fill;
        _detailLabel.ForeColor = Color.DimGray;
        _detailLabel.Location = new Point(0, 38);
        _detailLabel.Margin = new Padding(0);
        _detailLabel.Name = "_detailLabel";
        _detailLabel.Size = new Size(280, 22);
        _detailLabel.TabIndex = 2;
        _detailLabel.Text = "–";
        _detailLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // ObsSourceMeter
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(_layoutTableLayoutPanel);
        Margin = new Padding(0, 0, 0, 4);
        Name = "ObsSourceMeter";
        Size = new Size(280, 60);
        _layoutTableLayoutPanel.ResumeLayout(false);
        _volumeMeterPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    private TableLayoutPanel _layoutTableLayoutPanel;
    private Label _stateLabel;
    private Panel _volumeMeterPanel;
    private Panel _volumeMeterFillPanel;
    private Label _detailLabel;
}
