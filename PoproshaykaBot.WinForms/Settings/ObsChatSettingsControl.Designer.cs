namespace PoproshaykaBot.WinForms.Settings
{
    partial class ObsChatSettingsControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            _mainTableLayout = new TableLayoutPanel();
            _colorsGroupBox = new GroupBox();
            _colorsTableLayout = new TableLayoutPanel();
            _backgroundColorLabel = new Label();
            _backgroundColorTextBox = new TextBox();
            _backgroundColorResetButton = new Button();
            _textColorLabel = new Label();
            _textColorTextBox = new TextBox();
            _textColorResetButton = new Button();
            _usernameColorLabel = new Label();
            _usernameColorTextBox = new TextBox();
            _usernameColorResetButton = new Button();
            _systemMessageColorLabel = new Label();
            _systemMessageColorTextBox = new TextBox();
            _systemMessageColorResetButton = new Button();
            _timestampColorLabel = new Label();
            _timestampColorTextBox = new TextBox();
            _timestampColorResetButton = new Button();
            _fontsGroupBox = new GroupBox();
            _fontsTableLayout = new TableLayoutPanel();
            _fontFamilyLabel = new Label();
            _fontFamilyTextBox = new TextBox();
            _fontFamilyResetButton = new Button();
            _fontSizeLabel = new Label();
            _fontSizeNumeric = new NumericUpDown();
            _fontSizeResetButton = new Button();
            _fontBoldCheckBox = new CheckBox();
            _layoutGroupBox = new GroupBox();
            _layoutTableLayout = new TableLayoutPanel();
            _paddingLabel = new Label();
            _paddingNumeric = new NumericUpDown();
            _paddingResetButton = new Button();
            _marginLabel = new Label();
            _marginNumeric = new NumericUpDown();
            _marginResetButton = new Button();
            _borderRadiusLabel = new Label();
            _borderRadiusNumeric = new NumericUpDown();
            _borderRadiusResetButton = new Button();
            _animationsGroupBox = new GroupBox();
            _animationsTableLayout = new TableLayoutPanel();
            _enableAnimationsCheckBox = new CheckBox();
            _animationDurationLabel = new Label();
            _animationDurationNumeric = new NumericUpDown();
            _animationDurationResetButton = new Button();
            _limitsGroupBox = new GroupBox();
            _limitsTableLayout = new TableLayoutPanel();
            _maxMessagesLabel = new Label();
            _maxMessagesNumeric = new NumericUpDown();
            _maxMessagesResetButton = new Button();
            _showTimestampCheckBox = new CheckBox();
            _mainTableLayout.SuspendLayout();
            _colorsGroupBox.SuspendLayout();
            _colorsTableLayout.SuspendLayout();
            _fontsGroupBox.SuspendLayout();
            _fontsTableLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_fontSizeNumeric).BeginInit();
            _layoutGroupBox.SuspendLayout();
            _layoutTableLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_paddingNumeric).BeginInit();
            ((System.ComponentModel.ISupportInitialize)_marginNumeric).BeginInit();
            ((System.ComponentModel.ISupportInitialize)_borderRadiusNumeric).BeginInit();
            _animationsGroupBox.SuspendLayout();
            _animationsTableLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_animationDurationNumeric).BeginInit();
            _limitsGroupBox.SuspendLayout();
            _limitsTableLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_maxMessagesNumeric).BeginInit();
            SuspendLayout();
            // 
            // _mainTableLayout
            // 
            _mainTableLayout.ColumnCount = 1;
            _mainTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _mainTableLayout.Controls.Add(_colorsGroupBox, 0, 0);
            _mainTableLayout.Controls.Add(_fontsGroupBox, 0, 1);
            _mainTableLayout.Controls.Add(_layoutGroupBox, 0, 2);
            _mainTableLayout.Controls.Add(_animationsGroupBox, 0, 3);
            _mainTableLayout.Controls.Add(_limitsGroupBox, 0, 4);
            _mainTableLayout.Dock = DockStyle.Fill;
            _mainTableLayout.Location = new Point(0, 0);
            _mainTableLayout.Name = "_mainTableLayout";
            _mainTableLayout.RowCount = 6;
            _mainTableLayout.RowStyles.Add(new RowStyle());
            _mainTableLayout.RowStyles.Add(new RowStyle());
            _mainTableLayout.RowStyles.Add(new RowStyle());
            _mainTableLayout.RowStyles.Add(new RowStyle());
            _mainTableLayout.RowStyles.Add(new RowStyle());
            _mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _mainTableLayout.Size = new Size(548, 581);
            _mainTableLayout.TabIndex = 0;
            // 
            // _colorsGroupBox
            // 
            _colorsGroupBox.Controls.Add(_colorsTableLayout);
            _colorsGroupBox.Dock = DockStyle.Fill;
            _colorsGroupBox.Location = new Point(3, 3);
            _colorsGroupBox.Name = "_colorsGroupBox";
            _colorsGroupBox.Size = new Size(542, 160);
            _colorsGroupBox.TabIndex = 0;
            _colorsGroupBox.TabStop = false;
            _colorsGroupBox.Text = "Цвета";
            // 
            // _colorsTableLayout
            // 
            _colorsTableLayout.ColumnCount = 3;
            _colorsTableLayout.ColumnStyles.Add(new ColumnStyle());
            _colorsTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _colorsTableLayout.ColumnStyles.Add(new ColumnStyle());
            _colorsTableLayout.Controls.Add(_backgroundColorLabel, 0, 0);
            _colorsTableLayout.Controls.Add(_backgroundColorTextBox, 1, 0);
            _colorsTableLayout.Controls.Add(_backgroundColorResetButton, 2, 0);
            _colorsTableLayout.Controls.Add(_textColorLabel, 0, 1);
            _colorsTableLayout.Controls.Add(_textColorTextBox, 1, 1);
            _colorsTableLayout.Controls.Add(_textColorResetButton, 2, 1);
            _colorsTableLayout.Controls.Add(_usernameColorLabel, 0, 2);
            _colorsTableLayout.Controls.Add(_usernameColorTextBox, 1, 2);
            _colorsTableLayout.Controls.Add(_usernameColorResetButton, 2, 2);
            _colorsTableLayout.Controls.Add(_systemMessageColorLabel, 0, 3);
            _colorsTableLayout.Controls.Add(_systemMessageColorTextBox, 1, 3);
            _colorsTableLayout.Controls.Add(_systemMessageColorResetButton, 2, 3);
            _colorsTableLayout.Controls.Add(_timestampColorLabel, 0, 4);
            _colorsTableLayout.Controls.Add(_timestampColorTextBox, 1, 4);
            _colorsTableLayout.Controls.Add(_timestampColorResetButton, 2, 4);
            _colorsTableLayout.Dock = DockStyle.Fill;
            _colorsTableLayout.Location = new Point(3, 19);
            _colorsTableLayout.Name = "_colorsTableLayout";
            _colorsTableLayout.RowCount = 5;
            _colorsTableLayout.RowStyles.Add(new RowStyle());
            _colorsTableLayout.RowStyles.Add(new RowStyle());
            _colorsTableLayout.RowStyles.Add(new RowStyle());
            _colorsTableLayout.RowStyles.Add(new RowStyle());
            _colorsTableLayout.RowStyles.Add(new RowStyle());
            _colorsTableLayout.Size = new Size(536, 138);
            _colorsTableLayout.TabIndex = 0;
            // 
            // _backgroundColorLabel
            // 
            _backgroundColorLabel.AutoSize = true;
            _backgroundColorLabel.Dock = DockStyle.Fill;
            _backgroundColorLabel.Location = new Point(3, 3);
            _backgroundColorLabel.Margin = new Padding(3);
            _backgroundColorLabel.Name = "_backgroundColorLabel";
            _backgroundColorLabel.Size = new Size(100, 23);
            _backgroundColorLabel.TabIndex = 0;
            _backgroundColorLabel.Text = "Цвет фона:";
            _backgroundColorLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _backgroundColorTextBox
            // 
            _backgroundColorTextBox.Dock = DockStyle.Fill;
            _backgroundColorTextBox.Location = new Point(109, 3);
            _backgroundColorTextBox.Name = "_backgroundColorTextBox";
            _backgroundColorTextBox.Size = new Size(370, 23);
            _backgroundColorTextBox.TabIndex = 1;
            _backgroundColorTextBox.TextChanged += OnSettingChanged;
            // 
            // _backgroundColorResetButton
            // 
            _backgroundColorResetButton.Location = new Point(485, 3);
            _backgroundColorResetButton.Name = "_backgroundColorResetButton";
            _backgroundColorResetButton.Size = new Size(48, 23);
            _backgroundColorResetButton.TabIndex = 2;
            _backgroundColorResetButton.Text = "↻";
            _backgroundColorResetButton.UseVisualStyleBackColor = true;
            _backgroundColorResetButton.Click += OnBackgroundColorResetButtonClicked;
            // 
            // _textColorLabel
            // 
            _textColorLabel.AutoSize = true;
            _textColorLabel.Dock = DockStyle.Fill;
            _textColorLabel.Location = new Point(3, 32);
            _textColorLabel.Margin = new Padding(3);
            _textColorLabel.Name = "_textColorLabel";
            _textColorLabel.Size = new Size(100, 23);
            _textColorLabel.TabIndex = 3;
            _textColorLabel.Text = "Цвет текста:";
            _textColorLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _textColorTextBox
            // 
            _textColorTextBox.Dock = DockStyle.Fill;
            _textColorTextBox.Location = new Point(109, 32);
            _textColorTextBox.Name = "_textColorTextBox";
            _textColorTextBox.Size = new Size(370, 23);
            _textColorTextBox.TabIndex = 4;
            _textColorTextBox.TextChanged += OnSettingChanged;
            // 
            // _textColorResetButton
            // 
            _textColorResetButton.Location = new Point(485, 32);
            _textColorResetButton.Name = "_textColorResetButton";
            _textColorResetButton.Size = new Size(48, 23);
            _textColorResetButton.TabIndex = 5;
            _textColorResetButton.Text = "↻";
            _textColorResetButton.UseVisualStyleBackColor = true;
            _textColorResetButton.Click += OnTextColorResetButtonClicked;
            // 
            // _usernameColorLabel
            // 
            _usernameColorLabel.AutoSize = true;
            _usernameColorLabel.Dock = DockStyle.Fill;
            _usernameColorLabel.Location = new Point(3, 61);
            _usernameColorLabel.Margin = new Padding(3);
            _usernameColorLabel.Name = "_usernameColorLabel";
            _usernameColorLabel.Size = new Size(100, 23);
            _usernameColorLabel.TabIndex = 6;
            _usernameColorLabel.Text = "Цвет имени:";
            _usernameColorLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _usernameColorTextBox
            // 
            _usernameColorTextBox.Dock = DockStyle.Fill;
            _usernameColorTextBox.Location = new Point(109, 61);
            _usernameColorTextBox.Name = "_usernameColorTextBox";
            _usernameColorTextBox.Size = new Size(370, 23);
            _usernameColorTextBox.TabIndex = 7;
            _usernameColorTextBox.TextChanged += OnSettingChanged;
            // 
            // _usernameColorResetButton
            // 
            _usernameColorResetButton.Location = new Point(485, 61);
            _usernameColorResetButton.Name = "_usernameColorResetButton";
            _usernameColorResetButton.Size = new Size(48, 23);
            _usernameColorResetButton.TabIndex = 8;
            _usernameColorResetButton.Text = "↻";
            _usernameColorResetButton.UseVisualStyleBackColor = true;
            _usernameColorResetButton.Click += OnUsernameColorResetButtonClicked;
            // 
            // _systemMessageColorLabel
            // 
            _systemMessageColorLabel.AutoSize = true;
            _systemMessageColorLabel.Dock = DockStyle.Fill;
            _systemMessageColorLabel.Location = new Point(3, 90);
            _systemMessageColorLabel.Margin = new Padding(3);
            _systemMessageColorLabel.Name = "_systemMessageColorLabel";
            _systemMessageColorLabel.Size = new Size(100, 23);
            _systemMessageColorLabel.TabIndex = 9;
            _systemMessageColorLabel.Text = "Цвет системных:";
            _systemMessageColorLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _systemMessageColorTextBox
            // 
            _systemMessageColorTextBox.Dock = DockStyle.Fill;
            _systemMessageColorTextBox.Location = new Point(109, 90);
            _systemMessageColorTextBox.Name = "_systemMessageColorTextBox";
            _systemMessageColorTextBox.Size = new Size(370, 23);
            _systemMessageColorTextBox.TabIndex = 10;
            _systemMessageColorTextBox.TextChanged += OnSettingChanged;
            // 
            // _systemMessageColorResetButton
            // 
            _systemMessageColorResetButton.Location = new Point(485, 90);
            _systemMessageColorResetButton.Name = "_systemMessageColorResetButton";
            _systemMessageColorResetButton.Size = new Size(48, 23);
            _systemMessageColorResetButton.TabIndex = 11;
            _systemMessageColorResetButton.Text = "↻";
            _systemMessageColorResetButton.UseVisualStyleBackColor = true;
            _systemMessageColorResetButton.Click += OnSystemMessageColorResetButtonClicked;
            // 
            // _timestampColorLabel
            // 
            _timestampColorLabel.AutoSize = true;
            _timestampColorLabel.Dock = DockStyle.Fill;
            _timestampColorLabel.Location = new Point(3, 119);
            _timestampColorLabel.Margin = new Padding(3);
            _timestampColorLabel.Name = "_timestampColorLabel";
            _timestampColorLabel.Size = new Size(100, 23);
            _timestampColorLabel.TabIndex = 12;
            _timestampColorLabel.Text = "Цвет времени:";
            _timestampColorLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _timestampColorTextBox
            // 
            _timestampColorTextBox.Dock = DockStyle.Fill;
            _timestampColorTextBox.Location = new Point(109, 119);
            _timestampColorTextBox.Name = "_timestampColorTextBox";
            _timestampColorTextBox.Size = new Size(370, 23);
            _timestampColorTextBox.TabIndex = 13;
            _timestampColorTextBox.TextChanged += OnSettingChanged;
            // 
            // _timestampColorResetButton
            // 
            _timestampColorResetButton.Location = new Point(485, 119);
            _timestampColorResetButton.Name = "_timestampColorResetButton";
            _timestampColorResetButton.Size = new Size(48, 23);
            _timestampColorResetButton.TabIndex = 14;
            _timestampColorResetButton.Text = "↻";
            _timestampColorResetButton.UseVisualStyleBackColor = true;
            _timestampColorResetButton.Click += OnTimestampColorResetButtonClicked;
            // 
            // _fontsGroupBox
            // 
            _fontsGroupBox.Controls.Add(_fontsTableLayout);
            _fontsGroupBox.Dock = DockStyle.Fill;
            _fontsGroupBox.Location = new Point(3, 169);
            _fontsGroupBox.Name = "_fontsGroupBox";
            _fontsGroupBox.Size = new Size(542, 110);
            _fontsGroupBox.TabIndex = 1;
            _fontsGroupBox.TabStop = false;
            _fontsGroupBox.Text = "Шрифт";
            // 
            // _fontsTableLayout
            // 
            _fontsTableLayout.ColumnCount = 3;
            _fontsTableLayout.ColumnStyles.Add(new ColumnStyle());
            _fontsTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _fontsTableLayout.ColumnStyles.Add(new ColumnStyle());
            _fontsTableLayout.Controls.Add(_fontFamilyLabel, 0, 0);
            _fontsTableLayout.Controls.Add(_fontFamilyTextBox, 1, 0);
            _fontsTableLayout.Controls.Add(_fontFamilyResetButton, 2, 0);
            _fontsTableLayout.Controls.Add(_fontSizeLabel, 0, 1);
            _fontsTableLayout.Controls.Add(_fontSizeNumeric, 1, 1);
            _fontsTableLayout.Controls.Add(_fontSizeResetButton, 2, 1);
            _fontsTableLayout.Controls.Add(_fontBoldCheckBox, 0, 2);
            _fontsTableLayout.Dock = DockStyle.Fill;
            _fontsTableLayout.Location = new Point(3, 19);
            _fontsTableLayout.Name = "_fontsTableLayout";
            _fontsTableLayout.RowCount = 3;
            _fontsTableLayout.RowStyles.Add(new RowStyle());
            _fontsTableLayout.RowStyles.Add(new RowStyle());
            _fontsTableLayout.RowStyles.Add(new RowStyle());
            _fontsTableLayout.Size = new Size(536, 88);
            _fontsTableLayout.TabIndex = 0;
            // 
            // _fontFamilyLabel
            // 
            _fontFamilyLabel.AutoSize = true;
            _fontFamilyLabel.Dock = DockStyle.Fill;
            _fontFamilyLabel.Location = new Point(3, 3);
            _fontFamilyLabel.Margin = new Padding(3);
            _fontFamilyLabel.Name = "_fontFamilyLabel";
            _fontFamilyLabel.Size = new Size(118, 23);
            _fontFamilyLabel.TabIndex = 0;
            _fontFamilyLabel.Text = "Семейство шрифта:";
            _fontFamilyLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _fontFamilyTextBox
            // 
            _fontFamilyTextBox.Dock = DockStyle.Fill;
            _fontFamilyTextBox.Location = new Point(127, 3);
            _fontFamilyTextBox.Name = "_fontFamilyTextBox";
            _fontFamilyTextBox.Size = new Size(352, 23);
            _fontFamilyTextBox.TabIndex = 1;
            _fontFamilyTextBox.TextChanged += OnSettingChanged;
            // 
            // _fontFamilyResetButton
            // 
            _fontFamilyResetButton.Location = new Point(485, 3);
            _fontFamilyResetButton.Name = "_fontFamilyResetButton";
            _fontFamilyResetButton.Size = new Size(48, 23);
            _fontFamilyResetButton.TabIndex = 2;
            _fontFamilyResetButton.Text = "↻";
            _fontFamilyResetButton.UseVisualStyleBackColor = true;
            _fontFamilyResetButton.Click += OnFontFamilyResetButtonClicked;
            // 
            // _fontSizeLabel
            // 
            _fontSizeLabel.AutoSize = true;
            _fontSizeLabel.Dock = DockStyle.Fill;
            _fontSizeLabel.Location = new Point(3, 32);
            _fontSizeLabel.Margin = new Padding(3);
            _fontSizeLabel.Name = "_fontSizeLabel";
            _fontSizeLabel.Size = new Size(118, 23);
            _fontSizeLabel.TabIndex = 3;
            _fontSizeLabel.Text = "Размер шрифта:";
            _fontSizeLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _fontSizeNumeric
            // 
            _fontSizeNumeric.Dock = DockStyle.Fill;
            _fontSizeNumeric.Location = new Point(127, 32);
            _fontSizeNumeric.Maximum = new decimal(new int[] { 72, 0, 0, 0 });
            _fontSizeNumeric.Minimum = new decimal(new int[] { 8, 0, 0, 0 });
            _fontSizeNumeric.Name = "_fontSizeNumeric";
            _fontSizeNumeric.Size = new Size(352, 23);
            _fontSizeNumeric.TabIndex = 4;
            _fontSizeNumeric.Value = new decimal(new int[] { 14, 0, 0, 0 });
            _fontSizeNumeric.ValueChanged += OnSettingChanged;
            // 
            // _fontSizeResetButton
            // 
            _fontSizeResetButton.Location = new Point(485, 32);
            _fontSizeResetButton.Name = "_fontSizeResetButton";
            _fontSizeResetButton.Size = new Size(48, 23);
            _fontSizeResetButton.TabIndex = 5;
            _fontSizeResetButton.Text = "↻";
            _fontSizeResetButton.UseVisualStyleBackColor = true;
            _fontSizeResetButton.Click += OnFontSizeResetButtonClicked;
            // 
            // _fontBoldCheckBox
            // 
            _fontBoldCheckBox.AutoSize = true;
            _fontBoldCheckBox.Dock = DockStyle.Fill;
            _fontBoldCheckBox.Location = new Point(3, 61);
            _fontBoldCheckBox.Name = "_fontBoldCheckBox";
            _fontBoldCheckBox.Size = new Size(118, 24);
            _fontBoldCheckBox.TabIndex = 6;
            _fontBoldCheckBox.Text = "Жирный шрифт";
            _fontBoldCheckBox.UseVisualStyleBackColor = true;
            _fontBoldCheckBox.CheckedChanged += OnSettingChanged;
            // 
            // _layoutGroupBox
            // 
            _layoutGroupBox.Controls.Add(_layoutTableLayout);
            _layoutGroupBox.Dock = DockStyle.Fill;
            _layoutGroupBox.Location = new Point(3, 285);
            _layoutGroupBox.Name = "_layoutGroupBox";
            _layoutGroupBox.Size = new Size(542, 110);
            _layoutGroupBox.TabIndex = 2;
            _layoutGroupBox.TabStop = false;
            _layoutGroupBox.Text = "Размеры и отступы";
            // 
            // _layoutTableLayout
            // 
            _layoutTableLayout.ColumnCount = 3;
            _layoutTableLayout.ColumnStyles.Add(new ColumnStyle());
            _layoutTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _layoutTableLayout.ColumnStyles.Add(new ColumnStyle());
            _layoutTableLayout.Controls.Add(_paddingLabel, 0, 0);
            _layoutTableLayout.Controls.Add(_paddingNumeric, 1, 0);
            _layoutTableLayout.Controls.Add(_paddingResetButton, 2, 0);
            _layoutTableLayout.Controls.Add(_marginLabel, 0, 1);
            _layoutTableLayout.Controls.Add(_marginNumeric, 1, 1);
            _layoutTableLayout.Controls.Add(_marginResetButton, 2, 1);
            _layoutTableLayout.Controls.Add(_borderRadiusLabel, 0, 2);
            _layoutTableLayout.Controls.Add(_borderRadiusNumeric, 1, 2);
            _layoutTableLayout.Controls.Add(_borderRadiusResetButton, 2, 2);
            _layoutTableLayout.Dock = DockStyle.Fill;
            _layoutTableLayout.Location = new Point(3, 19);
            _layoutTableLayout.Name = "_layoutTableLayout";
            _layoutTableLayout.RowCount = 3;
            _layoutTableLayout.RowStyles.Add(new RowStyle());
            _layoutTableLayout.RowStyles.Add(new RowStyle());
            _layoutTableLayout.RowStyles.Add(new RowStyle());
            _layoutTableLayout.Size = new Size(536, 88);
            _layoutTableLayout.TabIndex = 0;
            // 
            // _paddingLabel
            // 
            _paddingLabel.AutoSize = true;
            _paddingLabel.Dock = DockStyle.Fill;
            _paddingLabel.Location = new Point(3, 3);
            _paddingLabel.Margin = new Padding(3);
            _paddingLabel.Name = "_paddingLabel";
            _paddingLabel.Size = new Size(115, 23);
            _paddingLabel.TabIndex = 0;
            _paddingLabel.Text = "Внутренний отступ:";
            _paddingLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _paddingNumeric
            // 
            _paddingNumeric.Dock = DockStyle.Fill;
            _paddingNumeric.Location = new Point(124, 3);
            _paddingNumeric.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
            _paddingNumeric.Name = "_paddingNumeric";
            _paddingNumeric.Size = new Size(355, 23);
            _paddingNumeric.TabIndex = 1;
            _paddingNumeric.Value = new decimal(new int[] { 5, 0, 0, 0 });
            _paddingNumeric.ValueChanged += OnSettingChanged;
            // 
            // _paddingResetButton
            // 
            _paddingResetButton.Location = new Point(485, 3);
            _paddingResetButton.Name = "_paddingResetButton";
            _paddingResetButton.Size = new Size(48, 23);
            _paddingResetButton.TabIndex = 2;
            _paddingResetButton.Text = "↻";
            _paddingResetButton.UseVisualStyleBackColor = true;
            _paddingResetButton.Click += OnPaddingResetButtonClicked;
            // 
            // _marginLabel
            // 
            _marginLabel.AutoSize = true;
            _marginLabel.Dock = DockStyle.Fill;
            _marginLabel.Location = new Point(3, 32);
            _marginLabel.Margin = new Padding(3);
            _marginLabel.Name = "_marginLabel";
            _marginLabel.Size = new Size(115, 23);
            _marginLabel.TabIndex = 3;
            _marginLabel.Text = "Внешний отступ:";
            _marginLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _marginNumeric
            // 
            _marginNumeric.Dock = DockStyle.Fill;
            _marginNumeric.Location = new Point(124, 32);
            _marginNumeric.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
            _marginNumeric.Name = "_marginNumeric";
            _marginNumeric.Size = new Size(355, 23);
            _marginNumeric.TabIndex = 4;
            _marginNumeric.Value = new decimal(new int[] { 5, 0, 0, 0 });
            _marginNumeric.ValueChanged += OnSettingChanged;
            // 
            // _marginResetButton
            // 
            _marginResetButton.Location = new Point(485, 32);
            _marginResetButton.Name = "_marginResetButton";
            _marginResetButton.Size = new Size(48, 23);
            _marginResetButton.TabIndex = 5;
            _marginResetButton.Text = "↻";
            _marginResetButton.UseVisualStyleBackColor = true;
            _marginResetButton.Click += OnMarginResetButtonClicked;
            // 
            // _borderRadiusLabel
            // 
            _borderRadiusLabel.AutoSize = true;
            _borderRadiusLabel.Dock = DockStyle.Fill;
            _borderRadiusLabel.Location = new Point(3, 61);
            _borderRadiusLabel.Margin = new Padding(3);
            _borderRadiusLabel.Name = "_borderRadiusLabel";
            _borderRadiusLabel.Size = new Size(115, 24);
            _borderRadiusLabel.TabIndex = 6;
            _borderRadiusLabel.Text = "Скругление углов:";
            _borderRadiusLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _borderRadiusNumeric
            // 
            _borderRadiusNumeric.Dock = DockStyle.Fill;
            _borderRadiusNumeric.Location = new Point(124, 61);
            _borderRadiusNumeric.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
            _borderRadiusNumeric.Name = "_borderRadiusNumeric";
            _borderRadiusNumeric.Size = new Size(355, 23);
            _borderRadiusNumeric.TabIndex = 7;
            _borderRadiusNumeric.Value = new decimal(new int[] { 5, 0, 0, 0 });
            _borderRadiusNumeric.ValueChanged += OnSettingChanged;
            // 
            // _borderRadiusResetButton
            // 
            _borderRadiusResetButton.Location = new Point(485, 61);
            _borderRadiusResetButton.Name = "_borderRadiusResetButton";
            _borderRadiusResetButton.Size = new Size(48, 23);
            _borderRadiusResetButton.TabIndex = 8;
            _borderRadiusResetButton.Text = "↻";
            _borderRadiusResetButton.UseVisualStyleBackColor = true;
            _borderRadiusResetButton.Click += OnBorderRadiusResetButtonClicked;
            // 
            // _animationsGroupBox
            // 
            _animationsGroupBox.Controls.Add(_animationsTableLayout);
            _animationsGroupBox.Dock = DockStyle.Fill;
            _animationsGroupBox.Location = new Point(3, 401);
            _animationsGroupBox.Name = "_animationsGroupBox";
            _animationsGroupBox.Size = new Size(542, 70);
            _animationsGroupBox.TabIndex = 3;
            _animationsGroupBox.TabStop = false;
            _animationsGroupBox.Text = "Анимации";
            // 
            // _animationsTableLayout
            // 
            _animationsTableLayout.ColumnCount = 3;
            _animationsTableLayout.ColumnStyles.Add(new ColumnStyle());
            _animationsTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _animationsTableLayout.ColumnStyles.Add(new ColumnStyle());
            _animationsTableLayout.Controls.Add(_enableAnimationsCheckBox, 0, 0);
            _animationsTableLayout.Controls.Add(_animationDurationLabel, 0, 1);
            _animationsTableLayout.Controls.Add(_animationDurationNumeric, 1, 1);
            _animationsTableLayout.Controls.Add(_animationDurationResetButton, 2, 1);
            _animationsTableLayout.Dock = DockStyle.Fill;
            _animationsTableLayout.Location = new Point(3, 19);
            _animationsTableLayout.Name = "_animationsTableLayout";
            _animationsTableLayout.RowCount = 2;
            _animationsTableLayout.RowStyles.Add(new RowStyle());
            _animationsTableLayout.RowStyles.Add(new RowStyle());
            _animationsTableLayout.Size = new Size(536, 48);
            _animationsTableLayout.TabIndex = 0;
            // 
            // _enableAnimationsCheckBox
            // 
            _enableAnimationsCheckBox.AutoSize = true;
            _enableAnimationsCheckBox.Dock = DockStyle.Fill;
            _enableAnimationsCheckBox.Location = new Point(3, 3);
            _enableAnimationsCheckBox.Name = "_enableAnimationsCheckBox";
            _enableAnimationsCheckBox.Size = new Size(140, 19);
            _enableAnimationsCheckBox.TabIndex = 0;
            _enableAnimationsCheckBox.Text = "Включить анимации";
            _enableAnimationsCheckBox.UseVisualStyleBackColor = true;
            _enableAnimationsCheckBox.CheckedChanged += OnSettingChanged;
            // 
            // _animationDurationLabel
            // 
            _animationDurationLabel.AutoSize = true;
            _animationDurationLabel.Dock = DockStyle.Fill;
            _animationDurationLabel.Location = new Point(3, 28);
            _animationDurationLabel.Margin = new Padding(3);
            _animationDurationLabel.Name = "_animationDurationLabel";
            _animationDurationLabel.Size = new Size(140, 23);
            _animationDurationLabel.TabIndex = 1;
            _animationDurationLabel.Text = "Длительность (мс):";
            _animationDurationLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _animationDurationNumeric
            // 
            _animationDurationNumeric.Dock = DockStyle.Fill;
            _animationDurationNumeric.Location = new Point(149, 28);
            _animationDurationNumeric.Maximum = new decimal(new int[] { 2000, 0, 0, 0 });
            _animationDurationNumeric.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
            _animationDurationNumeric.Name = "_animationDurationNumeric";
            _animationDurationNumeric.Size = new Size(330, 23);
            _animationDurationNumeric.TabIndex = 2;
            _animationDurationNumeric.Value = new decimal(new int[] { 300, 0, 0, 0 });
            _animationDurationNumeric.ValueChanged += OnSettingChanged;
            // 
            // _animationDurationResetButton
            // 
            _animationDurationResetButton.Location = new Point(485, 28);
            _animationDurationResetButton.Name = "_animationDurationResetButton";
            _animationDurationResetButton.Size = new Size(48, 23);
            _animationDurationResetButton.TabIndex = 3;
            _animationDurationResetButton.Text = "↻";
            _animationDurationResetButton.UseVisualStyleBackColor = true;
            _animationDurationResetButton.Click += OnAnimationDurationResetButtonClicked;
            // 
            // _limitsGroupBox
            // 
            _limitsGroupBox.Controls.Add(_limitsTableLayout);
            _limitsGroupBox.Dock = DockStyle.Fill;
            _limitsGroupBox.Location = new Point(3, 477);
            _limitsGroupBox.Name = "_limitsGroupBox";
            _limitsGroupBox.Size = new Size(542, 70);
            _limitsGroupBox.TabIndex = 4;
            _limitsGroupBox.TabStop = false;
            _limitsGroupBox.Text = "Лимиты";
            // 
            // _limitsTableLayout
            // 
            _limitsTableLayout.ColumnCount = 3;
            _limitsTableLayout.ColumnStyles.Add(new ColumnStyle());
            _limitsTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _limitsTableLayout.ColumnStyles.Add(new ColumnStyle());
            _limitsTableLayout.Controls.Add(_maxMessagesLabel, 0, 0);
            _limitsTableLayout.Controls.Add(_maxMessagesNumeric, 1, 0);
            _limitsTableLayout.Controls.Add(_maxMessagesResetButton, 2, 0);
            _limitsTableLayout.Controls.Add(_showTimestampCheckBox, 0, 1);
            _limitsTableLayout.Dock = DockStyle.Fill;
            _limitsTableLayout.Location = new Point(3, 19);
            _limitsTableLayout.Name = "_limitsTableLayout";
            _limitsTableLayout.RowCount = 2;
            _limitsTableLayout.RowStyles.Add(new RowStyle());
            _limitsTableLayout.RowStyles.Add(new RowStyle());
            _limitsTableLayout.Size = new Size(536, 48);
            _limitsTableLayout.TabIndex = 0;
            // 
            // _maxMessagesLabel
            // 
            _maxMessagesLabel.AutoSize = true;
            _maxMessagesLabel.Dock = DockStyle.Fill;
            _maxMessagesLabel.Location = new Point(3, 3);
            _maxMessagesLabel.Margin = new Padding(3);
            _maxMessagesLabel.Name = "_maxMessagesLabel";
            _maxMessagesLabel.Size = new Size(128, 23);
            _maxMessagesLabel.TabIndex = 0;
            _maxMessagesLabel.Text = "Макс. сообщений:";
            _maxMessagesLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _maxMessagesNumeric
            // 
            _maxMessagesNumeric.Dock = DockStyle.Fill;
            _maxMessagesNumeric.Location = new Point(137, 3);
            _maxMessagesNumeric.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            _maxMessagesNumeric.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            _maxMessagesNumeric.Name = "_maxMessagesNumeric";
            _maxMessagesNumeric.Size = new Size(342, 23);
            _maxMessagesNumeric.TabIndex = 1;
            _maxMessagesNumeric.Value = new decimal(new int[] { 50, 0, 0, 0 });
            _maxMessagesNumeric.ValueChanged += OnSettingChanged;
            // 
            // _maxMessagesResetButton
            // 
            _maxMessagesResetButton.Location = new Point(485, 3);
            _maxMessagesResetButton.Name = "_maxMessagesResetButton";
            _maxMessagesResetButton.Size = new Size(48, 23);
            _maxMessagesResetButton.TabIndex = 2;
            _maxMessagesResetButton.Text = "↻";
            _maxMessagesResetButton.UseVisualStyleBackColor = true;
            _maxMessagesResetButton.Click += OnMaxMessagesResetButtonClicked;
            // 
            // _showTimestampCheckBox
            // 
            _showTimestampCheckBox.AutoSize = true;
            _showTimestampCheckBox.Dock = DockStyle.Fill;
            _showTimestampCheckBox.Location = new Point(3, 32);
            _showTimestampCheckBox.Name = "_showTimestampCheckBox";
            _showTimestampCheckBox.Size = new Size(128, 19);
            _showTimestampCheckBox.TabIndex = 3;
            _showTimestampCheckBox.Text = "Показывать время";
            _showTimestampCheckBox.UseVisualStyleBackColor = true;
            _showTimestampCheckBox.CheckedChanged += OnSettingChanged;
            // 
            // ObsChatSettingsControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(_mainTableLayout);
            Name = "ObsChatSettingsControl";
            Size = new Size(548, 581);
            _mainTableLayout.ResumeLayout(false);
            _colorsGroupBox.ResumeLayout(false);
            _colorsTableLayout.ResumeLayout(false);
            _colorsTableLayout.PerformLayout();
            _fontsGroupBox.ResumeLayout(false);
            _fontsTableLayout.ResumeLayout(false);
            _fontsTableLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)_fontSizeNumeric).EndInit();
            _layoutGroupBox.ResumeLayout(false);
            _layoutTableLayout.ResumeLayout(false);
            _layoutTableLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)_paddingNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)_marginNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)_borderRadiusNumeric).EndInit();
            _animationsGroupBox.ResumeLayout(false);
            _animationsTableLayout.ResumeLayout(false);
            _animationsTableLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)_animationDurationNumeric).EndInit();
            _limitsGroupBox.ResumeLayout(false);
            _limitsTableLayout.ResumeLayout(false);
            _limitsTableLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)_maxMessagesNumeric).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel _mainTableLayout;

        // Colors section
        private GroupBox _colorsGroupBox;
        private TableLayoutPanel _colorsTableLayout;
        private Label _backgroundColorLabel;
        private TextBox _backgroundColorTextBox;
        private Button _backgroundColorResetButton;
        private Label _textColorLabel;
        private TextBox _textColorTextBox;
        private Button _textColorResetButton;
        private Label _usernameColorLabel;
        private TextBox _usernameColorTextBox;
        private Button _usernameColorResetButton;
        private Label _systemMessageColorLabel;
        private TextBox _systemMessageColorTextBox;
        private Button _systemMessageColorResetButton;
        private Label _timestampColorLabel;
        private TextBox _timestampColorTextBox;
        private Button _timestampColorResetButton;

        // Fonts section
        private GroupBox _fontsGroupBox;
        private TableLayoutPanel _fontsTableLayout;
        private Label _fontFamilyLabel;
        private TextBox _fontFamilyTextBox;
        private Button _fontFamilyResetButton;
        private Label _fontSizeLabel;
        private NumericUpDown _fontSizeNumeric;
        private Button _fontSizeResetButton;
        private CheckBox _fontBoldCheckBox;

        // Layout section
        private GroupBox _layoutGroupBox;
        private TableLayoutPanel _layoutTableLayout;
        private Label _paddingLabel;
        private NumericUpDown _paddingNumeric;
        private Button _paddingResetButton;
        private Label _marginLabel;
        private NumericUpDown _marginNumeric;
        private Button _marginResetButton;
        private Label _borderRadiusLabel;
        private NumericUpDown _borderRadiusNumeric;
        private Button _borderRadiusResetButton;

        // Animations section
        private GroupBox _animationsGroupBox;
        private TableLayoutPanel _animationsTableLayout;
        private CheckBox _enableAnimationsCheckBox;
        private Label _animationDurationLabel;
        private NumericUpDown _animationDurationNumeric;
        private Button _animationDurationResetButton;

        // Limits section
        private GroupBox _limitsGroupBox;
        private TableLayoutPanel _limitsTableLayout;
        private Label _maxMessagesLabel;
        private NumericUpDown _maxMessagesNumeric;
        private Button _maxMessagesResetButton;
        private CheckBox _showTimestampCheckBox;
    }
}
