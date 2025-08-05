namespace PoproshaykaBot.WinForms.Settings;

public partial class ObsChatSettingsControl : UserControl
{
    private static readonly ObsChatSettings DefaultSettings = new();

    public ObsChatSettingsControl()
    {
        InitializeComponent();
        SetPlaceholders();
    }

    public event EventHandler? SettingChanged;

    public void LoadSettings(ObsChatSettings settings)
    {
        _backgroundColorTextBox.Text = settings.BackgroundColor;
        _textColorTextBox.Text = settings.TextColor;
        _usernameColorTextBox.Text = settings.UsernameColor;
        _systemMessageColorTextBox.Text = settings.SystemMessageColor;
        _timestampColorTextBox.Text = settings.TimestampColor;

        _fontFamilyTextBox.Text = settings.FontFamily;
        _fontSizeNumeric.Value = settings.FontSize;
        _fontBoldCheckBox.Checked = settings.FontBold;

        _paddingNumeric.Value = settings.Padding;
        _marginNumeric.Value = settings.Margin;
        _borderRadiusNumeric.Value = settings.BorderRadius;

        _enableAnimationsCheckBox.Checked = settings.EnableAnimations;
        _animationDurationNumeric.Value = settings.AnimationDuration;

        _maxMessagesNumeric.Value = settings.MaxMessages;
        _showTimestampCheckBox.Checked = settings.ShowTimestamp;
    }

    public void SaveSettings(ObsChatSettings settings)
    {
        settings.BackgroundColor = ValidateColorValue(_backgroundColorTextBox.Text.Trim()) ?? DefaultSettings.BackgroundColor;
        settings.TextColor = ValidateColorValue(_textColorTextBox.Text.Trim()) ?? DefaultSettings.TextColor;
        settings.UsernameColor = ValidateColorValue(_usernameColorTextBox.Text.Trim()) ?? DefaultSettings.UsernameColor;
        settings.SystemMessageColor = ValidateColorValue(_systemMessageColorTextBox.Text.Trim()) ?? DefaultSettings.SystemMessageColor;
        settings.TimestampColor = ValidateColorValue(_timestampColorTextBox.Text.Trim()) ?? DefaultSettings.TimestampColor;

        settings.FontFamily = ValidateFontFamily(_fontFamilyTextBox.Text.Trim()) ?? DefaultSettings.FontFamily;
        settings.FontSize = (int)_fontSizeNumeric.Value;
        settings.FontBold = _fontBoldCheckBox.Checked;

        settings.Padding = (int)_paddingNumeric.Value;
        settings.Margin = (int)_marginNumeric.Value;
        settings.BorderRadius = (int)_borderRadiusNumeric.Value;

        settings.EnableAnimations = _enableAnimationsCheckBox.Checked;
        settings.AnimationDuration = (int)_animationDurationNumeric.Value;

        settings.MaxMessages = (int)_maxMessagesNumeric.Value;
        settings.ShowTimestamp = _showTimestampCheckBox.Checked;
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnBackgroundColorResetButtonClicked(object sender, EventArgs e)
    {
        _backgroundColorTextBox.Text = DefaultSettings.BackgroundColor;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTextColorResetButtonClicked(object sender, EventArgs e)
    {
        _textColorTextBox.Text = DefaultSettings.TextColor;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnUsernameColorResetButtonClicked(object sender, EventArgs e)
    {
        _usernameColorTextBox.Text = DefaultSettings.UsernameColor;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSystemMessageColorResetButtonClicked(object sender, EventArgs e)
    {
        _systemMessageColorTextBox.Text = DefaultSettings.SystemMessageColor;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTimestampColorResetButtonClicked(object sender, EventArgs e)
    {
        _timestampColorTextBox.Text = DefaultSettings.TimestampColor;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnFontFamilyResetButtonClicked(object sender, EventArgs e)
    {
        _fontFamilyTextBox.Text = DefaultSettings.FontFamily;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnFontSizeResetButtonClicked(object sender, EventArgs e)
    {
        _fontSizeNumeric.Value = DefaultSettings.FontSize;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPaddingResetButtonClicked(object sender, EventArgs e)
    {
        _paddingNumeric.Value = DefaultSettings.Padding;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnMarginResetButtonClicked(object sender, EventArgs e)
    {
        _marginNumeric.Value = DefaultSettings.Margin;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnBorderRadiusResetButtonClicked(object sender, EventArgs e)
    {
        _borderRadiusNumeric.Value = DefaultSettings.BorderRadius;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnAnimationDurationResetButtonClicked(object sender, EventArgs e)
    {
        _animationDurationNumeric.Value = DefaultSettings.AnimationDuration;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnMaxMessagesResetButtonClicked(object sender, EventArgs e)
    {
        _maxMessagesNumeric.Value = DefaultSettings.MaxMessages;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetPlaceholders()
    {
        _backgroundColorTextBox.PlaceholderText = DefaultSettings.BackgroundColor;
        _textColorTextBox.PlaceholderText = DefaultSettings.TextColor;
        _usernameColorTextBox.PlaceholderText = DefaultSettings.UsernameColor;
        _systemMessageColorTextBox.PlaceholderText = DefaultSettings.SystemMessageColor;
        _timestampColorTextBox.PlaceholderText = DefaultSettings.TimestampColor;

        _fontFamilyTextBox.PlaceholderText = DefaultSettings.FontFamily;
    }

    private string? ValidateColorValue(string colorValue)
    {
        if (string.IsNullOrWhiteSpace(colorValue))
        {
            return null;
        }

        if (colorValue.StartsWith('#') && colorValue.Length is 4 or 7)
        {
            return colorValue;
        }

        if (colorValue.StartsWith("rgb(") && colorValue.EndsWith(')'))
        {
            return colorValue;
        }

        if (colorValue.StartsWith("rgba(") && colorValue.EndsWith(')'))
        {
            return colorValue;
        }

        var namedColors = new[] { "white", "black", "red", "green", "blue", "yellow", "orange", "purple", "pink", "gray", "grey" };

        if (namedColors.Contains(colorValue.ToLower()))
        {
            return colorValue;
        }

        return null;
    }

    private string? ValidateFontFamily(string fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return null;
        }

        if (fontFamily.Contains(';') || fontFamily.Contains('"') || fontFamily.Contains('\''))
        {
            return null;
        }

        return fontFamily;
    }
}
