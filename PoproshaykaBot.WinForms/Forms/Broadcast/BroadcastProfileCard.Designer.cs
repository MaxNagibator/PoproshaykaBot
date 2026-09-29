namespace PoproshaykaBot.WinForms.Forms.Broadcast
{
    partial class BroadcastProfileCard
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.TableLayoutPanel _mainLayout;
        private System.Windows.Forms.TableLayoutPanel _headerRow;
        private System.Windows.Forms.Label _activeBadge;
        private System.Windows.Forms.Label _numberBadge;
        private System.Windows.Forms.Label _nameLabel;
        private System.Windows.Forms.Label _titleLabel;
        private System.Windows.Forms.Label _metaLabel;
        private System.Windows.Forms.Label _driftLabel;
        private System.Windows.Forms.Label _applyingLabel;
        private System.Windows.Forms.ContextMenuStrip _menu;
        private System.Windows.Forms.ToolStripMenuItem _applyMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _editMenuItem;
        private System.Windows.Forms.ToolStripSeparator _numberMenuSeparator;
        private System.Windows.Forms.ToolStripMenuItem _incrementNumberMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _decrementNumberMenuItem;
        private System.Windows.Forms.ToolStripSeparator _menuSeparator;
        private System.Windows.Forms.ToolStripMenuItem _duplicateMenuItem;
        private System.Windows.Forms.ToolStripMenuItem _deleteMenuItem;
        private System.Windows.Forms.ToolTip _toolTip;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            _menu = new ContextMenuStrip(components);
            _applyMenuItem = new ToolStripMenuItem();
            _editMenuItem = new ToolStripMenuItem();
            _numberMenuSeparator = new ToolStripSeparator();
            _incrementNumberMenuItem = new ToolStripMenuItem();
            _decrementNumberMenuItem = new ToolStripMenuItem();
            _menuSeparator = new ToolStripSeparator();
            _duplicateMenuItem = new ToolStripMenuItem();
            _deleteMenuItem = new ToolStripMenuItem();
            _toolTip = new ToolTip(components);
            _mainLayout = new TableLayoutPanel();
            _headerRow = new TableLayoutPanel();
            _activeBadge = new Label();
            _numberBadge = new Label();
            _nameLabel = new Label();
            _titleLabel = new Label();
            _metaLabel = new Label();
            _driftLabel = new Label();
            _applyingLabel = new Label();
            _menu.SuspendLayout();
            _mainLayout.SuspendLayout();
            _headerRow.SuspendLayout();
            SuspendLayout();
            //
            // _menu
            //
            _menu.Items.AddRange(new ToolStripItem[] { _applyMenuItem, _editMenuItem, _numberMenuSeparator, _incrementNumberMenuItem, _decrementNumberMenuItem, _menuSeparator, _duplicateMenuItem, _deleteMenuItem });
            _menu.Name = "_menu";
            //
            // _applyMenuItem
            //
            _applyMenuItem.Name = "_applyMenuItem";
            _applyMenuItem.Text = "↻ Применить";
            _applyMenuItem.Click += OnApplyMenuClicked;
            //
            // _editMenuItem
            //
            _editMenuItem.Name = "_editMenuItem";
            _editMenuItem.Text = "✎ Редактировать";
            _editMenuItem.Click += OnEditMenuClicked;
            //
            // _numberMenuSeparator
            //
            _numberMenuSeparator.Name = "_numberMenuSeparator";
            //
            // _incrementNumberMenuItem
            //
            _incrementNumberMenuItem.Name = "_incrementNumberMenuItem";
            _incrementNumberMenuItem.Text = "▲ Номер +1";
            _incrementNumberMenuItem.Click += OnIncrementNumberMenuClicked;
            //
            // _decrementNumberMenuItem
            //
            _decrementNumberMenuItem.Name = "_decrementNumberMenuItem";
            _decrementNumberMenuItem.Text = "▼ Номер −1";
            _decrementNumberMenuItem.Click += OnDecrementNumberMenuClicked;
            //
            // _menuSeparator
            //
            _menuSeparator.Name = "_menuSeparator";
            //
            // _duplicateMenuItem
            //
            _duplicateMenuItem.Name = "_duplicateMenuItem";
            _duplicateMenuItem.Text = "⎘ Дублировать";
            _duplicateMenuItem.Click += OnDuplicateMenuClicked;
            //
            // _deleteMenuItem
            //
            _deleteMenuItem.Name = "_deleteMenuItem";
            _deleteMenuItem.Text = "🗑 Удалить";
            _deleteMenuItem.Click += OnDeleteMenuClicked;
            //
            // _mainLayout
            //
            _mainLayout.AutoSize = true;
            _mainLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _mainLayout.ColumnCount = 1;
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _mainLayout.Controls.Add(_headerRow, 0, 0);
            _mainLayout.Controls.Add(_titleLabel, 0, 1);
            _mainLayout.Controls.Add(_metaLabel, 0, 2);
            _mainLayout.Controls.Add(_driftLabel, 0, 3);
            _mainLayout.Controls.Add(_applyingLabel, 0, 4);
            _mainLayout.Dock = DockStyle.Fill;
            _mainLayout.Name = "_mainLayout";
            _mainLayout.RowCount = 5;
            _mainLayout.RowStyles.Add(new RowStyle());
            _mainLayout.RowStyles.Add(new RowStyle());
            _mainLayout.RowStyles.Add(new RowStyle());
            _mainLayout.RowStyles.Add(new RowStyle());
            _mainLayout.RowStyles.Add(new RowStyle());
            //
            // _headerRow
            //
            _headerRow.AutoSize = true;
            _headerRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _headerRow.ColumnCount = 3;
            _headerRow.ColumnStyles.Add(new ColumnStyle());
            _headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _headerRow.ColumnStyles.Add(new ColumnStyle());
            _headerRow.Controls.Add(_activeBadge, 0, 0);
            _headerRow.Controls.Add(_nameLabel, 1, 0);
            _headerRow.Controls.Add(_numberBadge, 2, 0);
            _headerRow.Dock = DockStyle.Top;
            _headerRow.Margin = new Padding(0, 0, 0, 2);
            _headerRow.Name = "_headerRow";
            _headerRow.RowCount = 1;
            _headerRow.RowStyles.Add(new RowStyle());
            //
            // _activeBadge
            //
            _activeBadge.Anchor = AnchorStyles.Left;
            _activeBadge.AutoSize = true;
            _activeBadge.ForeColor = Color.SeaGreen;
            _activeBadge.Margin = new Padding(0, 0, 4, 0);
            _activeBadge.Name = "_activeBadge";
            _activeBadge.Text = "✓";
            _activeBadge.Visible = false;
            //
            // _nameLabel
            //
            _nameLabel.AutoEllipsis = true;
            _nameLabel.AutoSize = false;
            _nameLabel.Dock = DockStyle.Fill;
            _nameLabel.Height = 22;
            _nameLabel.Margin = new Padding(0);
            _nameLabel.Name = "_nameLabel";
            _nameLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _numberBadge
            //
            _numberBadge.Anchor = AnchorStyles.Right;
            _numberBadge.AutoSize = true;
            _numberBadge.ForeColor = Color.SteelBlue;
            _numberBadge.Margin = new Padding(6, 0, 0, 0);
            _numberBadge.Name = "_numberBadge";
            _numberBadge.Visible = false;
            //
            // _titleLabel
            //
            _titleLabel.AutoEllipsis = true;
            _titleLabel.AutoSize = false;
            _titleLabel.Dock = DockStyle.Fill;
            _titleLabel.ForeColor = Color.DimGray;
            _titleLabel.Height = 18;
            _titleLabel.Margin = new Padding(0, 0, 0, 1);
            _titleLabel.Name = "_titleLabel";
            _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            _titleLabel.Visible = false;
            //
            // _metaLabel
            //
            _metaLabel.AutoEllipsis = true;
            _metaLabel.AutoSize = false;
            _metaLabel.Dock = DockStyle.Fill;
            _metaLabel.ForeColor = Color.Gray;
            _metaLabel.Height = 18;
            _metaLabel.Margin = new Padding(0, 0, 0, 1);
            _metaLabel.Name = "_metaLabel";
            _metaLabel.TextAlign = ContentAlignment.MiddleLeft;
            _metaLabel.Visible = false;
            //
            // _driftLabel
            //
            _driftLabel.AutoEllipsis = true;
            _driftLabel.AutoSize = false;
            _driftLabel.Dock = DockStyle.Fill;
            _driftLabel.ForeColor = Color.DarkOrange;
            _driftLabel.Height = 18;
            _driftLabel.Margin = new Padding(0);
            _driftLabel.Name = "_driftLabel";
            _driftLabel.Text = "⚠ Стрим изменён вручную";
            _driftLabel.TextAlign = ContentAlignment.MiddleLeft;
            _driftLabel.Visible = false;
            //
            // _applyingLabel
            //
            _applyingLabel.AutoEllipsis = true;
            _applyingLabel.AutoSize = false;
            _applyingLabel.Dock = DockStyle.Fill;
            _applyingLabel.ForeColor = Color.SteelBlue;
            _applyingLabel.Height = 18;
            _applyingLabel.Margin = new Padding(0);
            _applyingLabel.Name = "_applyingLabel";
            _applyingLabel.Text = "⏳ Применяется…";
            _applyingLabel.TextAlign = ContentAlignment.MiddleLeft;
            _applyingLabel.Visible = false;
            //
            // BroadcastProfileCard
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BorderStyle = BorderStyle.FixedSingle;
            ContextMenuStrip = _menu;
            Controls.Add(_mainLayout);
            Margin = new Padding(3);
            MinimumSize = new Size(220, 0);
            Name = "BroadcastProfileCard";
            Padding = new Padding(6, 4, 6, 4);
            DoubleClick += OnCardDoubleClick;
            _toolTip.SetToolTip(this, "Двойной клик – применить, ПКМ – действия");
            _menu.ResumeLayout(false);
            _mainLayout.ResumeLayout(false);
            _mainLayout.PerformLayout();
            _headerRow.ResumeLayout(false);
            _headerRow.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
