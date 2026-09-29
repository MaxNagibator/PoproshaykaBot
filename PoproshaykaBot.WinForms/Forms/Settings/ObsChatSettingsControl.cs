using Cyotek.Windows.Forms;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.WinForms.Infrastructure;
using System.Diagnostics;
using System.Globalization;

namespace PoproshaykaBot.WinForms.Forms.Settings;

public partial class ObsChatSettingsControl : UserControl
{
    private static readonly ObsChatSettings DefaultSettings = new();

    private readonly ErrorProvider _fontFamilyErrorProvider = new();

    private int _httpServerPort;

    public ObsChatSettingsControl()
    {
        InitializeComponent();
        SetPlaceholders();
        PopulateAnimationItems();
        ApplyRanges();
        WireFontFamilyValidation();
    }

    public event EventHandler? SettingChanged;

    public void LoadSettings(ObsChatSettings settings, int httpServerPort)
    {
        _httpServerPort = httpServerPort;

        _backgroundColorButton.BackColor = settings.BackgroundColor;
        _textColorButton.BackColor = settings.TextColor;
        _usernameColorButton.BackColor = settings.UsernameColor;
        _systemMessageColorButton.BackColor = settings.SystemMessageColor;
        _timestampColorButton.BackColor = settings.TimestampColor;

        UpdateColorButtons();

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

        _emoteSizeNumeric.Value = settings.EmoteSizePixels;
        _badgeSizeNumeric.Value = settings.BadgeSizePixels;

        _showUserAvatarsCheckBox.Checked = settings.ShowUserAvatars;
        _userAvatarSizeNumeric.Value = settings.UserAvatarSizePixels;

        _showUserTypeBordersCheckBox.Checked = settings.ShowUserTypeBorders;
        _highlightFirstTimeUsersCheckBox.Checked = settings.HighlightFirstTimeUsers;
        _highlightMentionsCheckBox.Checked = settings.HighlightMentions;
        _enableMessageShadowsCheckBox.Checked = settings.EnableMessageShadows;
        _enableSpecialEffectsCheckBox.Checked = settings.EnableSpecialEffects;

        _enableSmoothScrollCheckBox.Checked = settings.EnableSmoothScroll;
        _autoScrollEnabledCheckBox.Checked = settings.AutoScrollEnabled;
        _scrollAnimationDurationNumeric.Value = settings.ScrollAnimationDuration;

        _enableMessageFadeOutCheckBox.Checked = settings.EnableMessageFadeOut;
        _messageLifetimeNumeric.Value = settings.MessageLifetimeSeconds;
        SetAnimationTypeInComboBox(_fadeOutAnimationComboBox, settings.FadeOutAnimationType, MessageAnimationType.ExitAnimations);
        _fadeOutAnimationDurationNumeric.Value = settings.FadeOutAnimationDurationMs;

        SetAnimationTypeInComboBox(_userMessageAnimationComboBox, settings.UserMessageAnimation, MessageAnimationType.EntryAnimations);
        SetAnimationTypeInComboBox(_botMessageAnimationComboBox, settings.BotMessageAnimation, MessageAnimationType.EntryAnimations);
        SetAnimationTypeInComboBox(_systemMessageAnimationComboBox, settings.SystemMessageAnimation, MessageAnimationType.EntryAnimations);
        SetAnimationTypeInComboBox(_broadcasterMessageAnimationComboBox, settings.BroadcasterMessageAnimation, MessageAnimationType.EntryAnimations);
        SetAnimationTypeInComboBox(_firstTimeUserMessageAnimationComboBox, settings.FirstTimeUserMessageAnimation, MessageAnimationType.EntryAnimations);
    }

    public void SaveSettings(ObsChatSettings settings)
    {
        settings.BackgroundColor = _backgroundColorButton.BackColor;
        settings.TextColor = _textColorButton.BackColor;
        settings.UsernameColor = _usernameColorButton.BackColor;
        settings.SystemMessageColor = _systemMessageColorButton.BackColor;
        settings.TimestampColor = _timestampColorButton.BackColor;

        var validatedFontFamily = ValidateFontFamily(_fontFamilyTextBox.Text.Trim());
        if (validatedFontFamily != null)
        {
            settings.FontFamily = validatedFontFamily;
        }

        settings.FontSize = (int)_fontSizeNumeric.Value;
        settings.FontBold = _fontBoldCheckBox.Checked;

        settings.Padding = (int)_paddingNumeric.Value;
        settings.Margin = (int)_marginNumeric.Value;
        settings.BorderRadius = (int)_borderRadiusNumeric.Value;

        settings.EnableAnimations = _enableAnimationsCheckBox.Checked;
        settings.AnimationDuration = (int)_animationDurationNumeric.Value;

        settings.MaxMessages = (int)_maxMessagesNumeric.Value;
        settings.ShowTimestamp = _showTimestampCheckBox.Checked;

        settings.EmoteSizePixels = (int)_emoteSizeNumeric.Value;
        settings.BadgeSizePixels = (int)_badgeSizeNumeric.Value;

        settings.ShowUserAvatars = _showUserAvatarsCheckBox.Checked;
        settings.UserAvatarSizePixels = (int)_userAvatarSizeNumeric.Value;

        settings.ShowUserTypeBorders = _showUserTypeBordersCheckBox.Checked;
        settings.HighlightFirstTimeUsers = _highlightFirstTimeUsersCheckBox.Checked;
        settings.HighlightMentions = _highlightMentionsCheckBox.Checked;
        settings.EnableMessageShadows = _enableMessageShadowsCheckBox.Checked;
        settings.EnableSpecialEffects = _enableSpecialEffectsCheckBox.Checked;

        settings.EnableSmoothScroll = _enableSmoothScrollCheckBox.Checked;
        settings.AutoScrollEnabled = _autoScrollEnabledCheckBox.Checked;
        settings.ScrollAnimationDuration = (int)_scrollAnimationDurationNumeric.Value;

        settings.EnableMessageFadeOut = _enableMessageFadeOutCheckBox.Checked;
        settings.MessageLifetimeSeconds = (int)_messageLifetimeNumeric.Value;
        settings.FadeOutAnimationType = GetAnimationTypeFromComboBox(_fadeOutAnimationComboBox, MessageAnimationType.ExitAnimations);
        settings.FadeOutAnimationDurationMs = (int)_fadeOutAnimationDurationNumeric.Value;

        settings.UserMessageAnimation = GetAnimationTypeFromComboBox(_userMessageAnimationComboBox, MessageAnimationType.EntryAnimations);
        settings.BotMessageAnimation = GetAnimationTypeFromComboBox(_botMessageAnimationComboBox, MessageAnimationType.EntryAnimations);
        settings.SystemMessageAnimation = GetAnimationTypeFromComboBox(_systemMessageAnimationComboBox, MessageAnimationType.EntryAnimations);
        settings.BroadcasterMessageAnimation = GetAnimationTypeFromComboBox(_broadcasterMessageAnimationComboBox, MessageAnimationType.EntryAnimations);
        settings.FirstTimeUserMessageAnimation = GetAnimationTypeFromComboBox(_firstTimeUserMessageAnimationComboBox, MessageAnimationType.EntryAnimations);
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnBackgroundColorResetButtonClicked(object sender, EventArgs e)
    {
        ResetColor(_backgroundColorButton, DefaultSettings.BackgroundColor);
    }

    private void OnTextColorResetButtonClicked(object sender, EventArgs e)
    {
        ResetColor(_textColorButton, DefaultSettings.TextColor);
    }

    private void OnUsernameColorResetButtonClicked(object sender, EventArgs e)
    {
        ResetColor(_usernameColorButton, DefaultSettings.UsernameColor);
    }

    private void OnSystemMessageColorResetButtonClicked(object sender, EventArgs e)
    {
        ResetColor(_systemMessageColorButton, DefaultSettings.SystemMessageColor);
    }

    private void OnTimestampColorResetButtonClicked(object sender, EventArgs e)
    {
        ResetColor(_timestampColorButton, DefaultSettings.TimestampColor);
    }

    private void OnFontFamilyResetButtonClicked(object sender, EventArgs e)
    {
        ResetText(_fontFamilyTextBox, DefaultSettings.FontFamily);
    }

    private void OnFontSizeResetButtonClicked(object sender, EventArgs e)
    {
        ResetNumeric(_fontSizeNumeric, DefaultSettings.FontSize);
    }

    private void OnPaddingResetButtonClicked(object sender, EventArgs e)
    {
        ResetNumeric(_paddingNumeric, DefaultSettings.Padding);
    }

    private void OnMarginResetButtonClicked(object sender, EventArgs e)
    {
        ResetNumeric(_marginNumeric, DefaultSettings.Margin);
    }

    private void OnBorderRadiusResetButtonClicked(object sender, EventArgs e)
    {
        ResetNumeric(_borderRadiusNumeric, DefaultSettings.BorderRadius);
    }

    private void OnAnimationDurationResetButtonClicked(object sender, EventArgs e)
    {
        ResetNumeric(_animationDurationNumeric, DefaultSettings.AnimationDuration);
    }

    private void OnEnableAnimationsResetButtonClicked(object sender, EventArgs e)
    {
        ResetCheck(_enableAnimationsCheckBox, DefaultSettings.EnableAnimations);
    }

    private void OnMaxMessagesResetButtonClicked(object sender, EventArgs e)
    {
        ResetNumeric(_maxMessagesNumeric, DefaultSettings.MaxMessages);
    }

    private void OnEmoteSizeResetButtonClicked(object sender, EventArgs e)
    {
        ResetNumeric(_emoteSizeNumeric, DefaultSettings.EmoteSizePixels);
    }

    private void OnBadgeSizeResetButtonClicked(object sender, EventArgs e)
    {
        ResetNumeric(_badgeSizeNumeric, DefaultSettings.BadgeSizePixels);
    }

    private void OnUserAvatarSizeResetButtonClicked(object sender, EventArgs e)
    {
        ResetNumeric(_userAvatarSizeNumeric, DefaultSettings.UserAvatarSizePixels);
    }

    private void OnScrollAnimationDurationResetButtonClicked(object sender, EventArgs e)
    {
        ResetNumeric(_scrollAnimationDurationNumeric, DefaultSettings.ScrollAnimationDuration);
    }

    private void OnEnableMessageFadeOutResetButtonClicked(object sender, EventArgs e)
    {
        ResetCheck(_enableMessageFadeOutCheckBox, DefaultSettings.EnableMessageFadeOut);
    }

    private void OnMessageLifetimeResetButtonClicked(object sender, EventArgs e)
    {
        ResetNumeric(_messageLifetimeNumeric, DefaultSettings.MessageLifetimeSeconds);
    }

    private void OnFadeOutAnimationResetButtonClicked(object sender, EventArgs e)
    {
        ResetAnimation(_fadeOutAnimationComboBox, DefaultSettings.FadeOutAnimationType, MessageAnimationType.ExitAnimations);
    }

    private void OnFadeOutAnimationDurationResetButtonClicked(object sender, EventArgs e)
    {
        ResetNumeric(_fadeOutAnimationDurationNumeric, DefaultSettings.FadeOutAnimationDurationMs);
    }

    private void OnUserMessageAnimationResetButtonClicked(object sender, EventArgs e)
    {
        ResetAnimation(_userMessageAnimationComboBox, DefaultSettings.UserMessageAnimation, MessageAnimationType.EntryAnimations);
    }

    private void OnBotMessageAnimationResetButtonClicked(object sender, EventArgs e)
    {
        ResetAnimation(_botMessageAnimationComboBox, DefaultSettings.BotMessageAnimation, MessageAnimationType.EntryAnimations);
    }

    private void OnSystemMessageAnimationResetButtonClicked(object sender, EventArgs e)
    {
        ResetAnimation(_systemMessageAnimationComboBox, DefaultSettings.SystemMessageAnimation, MessageAnimationType.EntryAnimations);
    }

    private void OnBroadcasterMessageAnimationResetButtonClicked(object sender, EventArgs e)
    {
        ResetAnimation(_broadcasterMessageAnimationComboBox, DefaultSettings.BroadcasterMessageAnimation, MessageAnimationType.EntryAnimations);
    }

    private void OnFirstTimeUserMessageAnimationResetButtonClicked(object sender, EventArgs e)
    {
        ResetAnimation(_firstTimeUserMessageAnimationComboBox, DefaultSettings.FirstTimeUserMessageAnimation, MessageAnimationType.EntryAnimations);
    }

    private void OnOpenAnimationsDemoButtonClicked(object sender, EventArgs e)
    {
        if (_httpServerPort <= 0)
        {
            MessageBox.Show(this,
                "Не удалось определить порт HTTP-сервера. Проверьте настройки на вкладке «HTTP сервер».",
                "Демо анимаций",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        var url = string.Format(CultureInfo.InvariantCulture,
            "http://localhost:{0}/animations-demo",
            _httpServerPort);

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(this,
                $"Не удалось открыть демо в браузере.\n\n{exception.Message}",
                "Демо анимаций",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnBackgroundColorButtonClicked(object sender, EventArgs e)
    {
        HandleColorPick(_backgroundColorButton);
    }

    private void OnTextColorButtonClicked(object sender, EventArgs e)
    {
        HandleColorPick(_textColorButton);
    }

    private void OnUsernameColorButtonClicked(object sender, EventArgs e)
    {
        HandleColorPick(_usernameColorButton);
    }

    private void OnSystemMessageColorButtonClicked(object sender, EventArgs e)
    {
        HandleColorPick(_systemMessageColorButton);
    }

    private void OnTimestampColorButtonClicked(object sender, EventArgs e)
    {
        HandleColorPick(_timestampColorButton);
    }

    private void OnFontFamilyTextChanged(object? sender, EventArgs e)
    {
        var input = _fontFamilyTextBox.Text.Trim();

        if (input.Length == 0)
        {
            _fontFamilyErrorProvider.SetError(_fontFamilyTextBox, string.Empty);
            return;
        }

        if (ContainsForbiddenFontFamilyChars(input))
        {
            _fontFamilyErrorProvider.SetError(_fontFamilyTextBox,
                "Недопустимые символы. Запрещены: ; < > { }");

            return;
        }

        _fontFamilyErrorProvider.SetError(_fontFamilyTextBox, string.Empty);
    }

    private static string GetAnimationTypeFromComboBox(ComboBox comboBox, (string Value, string DisplayName)[] options)
    {
        var index = comboBox.SelectedIndex;
        return index >= 0 && index < options.Length ? options[index].Value : options[0].Value;
    }

    private static void SetAnimationTypeInComboBox(ComboBox comboBox, string animationType, (string Value, string DisplayName)[] options)
    {
        var index = Array.FindIndex(options, option => option.Value == animationType);
        comboBox.SelectedIndex = index >= 0 ? index : 0;
    }

    private static void SetRange(NumericUpDown control, int min, int max)
    {
        control.Minimum = min;
        control.Maximum = max;
    }

    private static bool ContainsForbiddenFontFamilyChars(string fontFamily)
    {
        return fontFamily.Contains(';')
               || fontFamily.Contains('<')
               || fontFamily.Contains('>')
               || fontFamily.Contains('{')
               || fontFamily.Contains('}');
    }

    private static string GetColorDisplayText(Color color)
    {
        return color.A == 255
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
    }

    private static string? ValidateFontFamily(string fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return null;
        }

        if (ContainsForbiddenFontFamilyChars(fontFamily))
        {
            return null;
        }

        return fontFamily;
    }

    private void ResetNumeric(NumericUpDown control, int defaultValue)
    {
        control.Value = ObsChatRanges.Clamp(defaultValue, (int)control.Minimum, (int)control.Maximum);
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetCheck(CheckBox control, bool defaultValue)
    {
        control.Checked = defaultValue;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetText(TextBox control, string defaultValue)
    {
        control.Text = defaultValue;
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetColor(Button button, Color defaultColor)
    {
        button.BackColor = defaultColor;
        UpdateColorButtons();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetAnimation(ComboBox combo, string defaultValue, (string Value, string DisplayName)[] options)
    {
        SetAnimationTypeInComboBox(combo, defaultValue, options);
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleColorPick(Button button)
    {
        if (!ShowColorPickerDialog(button))
        {
            return;
        }

        UpdateColorButtons();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetPlaceholders()
    {
        _fontFamilyTextBox.PlaceholderText = DefaultSettings.FontFamily;
    }

    private void PopulateAnimationItems()
    {
        var entryComboBoxes = new[]
        {
            _userMessageAnimationComboBox,
            _botMessageAnimationComboBox,
            _systemMessageAnimationComboBox,
            _broadcasterMessageAnimationComboBox,
            _firstTimeUserMessageAnimationComboBox,
        };

        foreach (var comboBox in entryComboBoxes)
        {
            foreach (var (_, displayName) in MessageAnimationType.EntryAnimations)
            {
                comboBox.Items.Add(displayName);
            }
        }

        foreach (var (_, displayName) in MessageAnimationType.ExitAnimations)
        {
            _fadeOutAnimationComboBox.Items.Add(displayName);
        }
    }

    private void ApplyRanges()
    {
        SetRange(_fontSizeNumeric, ObsChatRanges.FontSizeMin, ObsChatRanges.FontSizeMax);
        SetRange(_paddingNumeric, ObsChatRanges.PaddingMin, ObsChatRanges.PaddingMax);
        SetRange(_marginNumeric, ObsChatRanges.MarginMin, ObsChatRanges.MarginMax);
        SetRange(_borderRadiusNumeric, ObsChatRanges.BorderRadiusMin, ObsChatRanges.BorderRadiusMax);
        SetRange(_animationDurationNumeric, ObsChatRanges.AnimationDurationMin, ObsChatRanges.AnimationDurationMax);
        SetRange(_maxMessagesNumeric, ObsChatRanges.MaxMessagesMin, ObsChatRanges.MaxMessagesMax);
        SetRange(_emoteSizeNumeric, ObsChatRanges.EmoteSizeMin, ObsChatRanges.EmoteSizeMax);
        SetRange(_badgeSizeNumeric, ObsChatRanges.BadgeSizeMin, ObsChatRanges.BadgeSizeMax);
        SetRange(_userAvatarSizeNumeric, ObsChatRanges.UserAvatarSizeMin, ObsChatRanges.UserAvatarSizeMax);
        SetRange(_messageLifetimeNumeric, ObsChatRanges.MessageLifetimeMin, ObsChatRanges.MessageLifetimeMax);
        SetRange(_fadeOutAnimationDurationNumeric, ObsChatRanges.FadeOutAnimationDurationMin, ObsChatRanges.FadeOutAnimationDurationMax);
        SetRange(_scrollAnimationDurationNumeric, ObsChatRanges.ScrollAnimationDurationMin, ObsChatRanges.ScrollAnimationDurationMax);
    }

    private void WireFontFamilyValidation()
    {
        _fontFamilyErrorProvider.SetIconAlignment(_fontFamilyTextBox, ErrorIconAlignment.MiddleRight);
        _fontFamilyErrorProvider.BlinkStyle = ErrorBlinkStyle.NeverBlink;

        _fontFamilyTextBox.TextChanged += OnFontFamilyTextChanged;
        Disposed += (_, _) => _fontFamilyErrorProvider.Dispose();
    }

    private bool ShowColorPickerDialog(Button colorButton)
    {
        using var colorPickerDialog = new ColorPickerDialog();
        OffScreenForm.PrepareIfHeadless(colorPickerDialog);
        colorPickerDialog.Color = colorButton.BackColor;
        colorPickerDialog.ShowAlphaChannel = true;

        if (colorPickerDialog.ShowDialog(this) == DialogResult.OK)
        {
            colorButton.BackColor = colorPickerDialog.Color;
            return true;
        }

        return false;
    }

    private void UpdateColorButtons()
    {
        _backgroundColorButton.Text = GetColorDisplayText(_backgroundColorButton.BackColor);
        _textColorButton.Text = GetColorDisplayText(_textColorButton.BackColor);
        _usernameColorButton.Text = GetColorDisplayText(_usernameColorButton.BackColor);
        _systemMessageColorButton.Text = GetColorDisplayText(_systemMessageColorButton.BackColor);
        _timestampColorButton.Text = GetColorDisplayText(_timestampColorButton.BackColor);
    }
}
