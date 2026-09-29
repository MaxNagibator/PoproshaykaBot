using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class ObsChatSettingsSectionViewModel : ObservableValidator
{
    private static readonly ObsChatSettings Defaults = new();

    private readonly ObsChatStore _store;
    private readonly SettingsManager _settings;
    private readonly IDialogService _dialogService;
    private readonly IShellLauncher _shellLauncher;

    private ObsChatSettings _baseline = new();

    [ObservableProperty]
    private Color _backgroundColor;

    [ObservableProperty]
    private Color _textColor;

    [ObservableProperty]
    private Color _usernameColor;

    [ObservableProperty]
    private Color _systemMessageColor;

    [ObservableProperty]
    private Color _timestampColor;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ObsChatSettingsSectionViewModel), nameof(ValidateFontFamily))]
    private string _fontFamily = ObsChatSettings.DefaultFontFamily;

    [ObservableProperty]
    private int _fontSize = Defaults.FontSize;

    [ObservableProperty]
    private bool _fontBold;

    [ObservableProperty]
    private int _padding = Defaults.Padding;

    [ObservableProperty]
    private int _margin = Defaults.Margin;

    [ObservableProperty]
    private int _borderRadius = Defaults.BorderRadius;

    [ObservableProperty]
    private bool _enableAnimations = Defaults.EnableAnimations;

    [ObservableProperty]
    private int _animationDuration = Defaults.AnimationDuration;

    [ObservableProperty]
    private int _maxMessages = Defaults.MaxMessages;

    [ObservableProperty]
    private bool _showTimestamp = Defaults.ShowTimestamp;

    [ObservableProperty]
    private int _emoteSizePixels = Defaults.EmoteSizePixels;

    [ObservableProperty]
    private int _badgeSizePixels = Defaults.BadgeSizePixels;

    [ObservableProperty]
    private bool _showUserAvatars;

    [ObservableProperty]
    private int _userAvatarSizePixels = Defaults.UserAvatarSizePixels;

    [ObservableProperty]
    private bool _showMessageImages = Defaults.ShowMessageImages;

    [ObservableProperty]
    private bool _messageImagesFromBroadcaster;

    [ObservableProperty]
    private bool _messageImagesFromModerators;

    [ObservableProperty]
    private bool _messageImagesFromVips;

    [ObservableProperty]
    private bool _messageImagesFromSubscribers;

    [ObservableProperty]
    private bool _messageImagesFromEveryone;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ObsChatSettingsSectionViewModel), nameof(ValidateMessageImageHosts))]
    private string _messageImageAllowedHostsText = string.Empty;

    [ObservableProperty]
    private int _messageImageMaxHeightPixels = Defaults.MessageImageMaxHeightPixels;

    [ObservableProperty]
    private bool _showUserTypeBorders = Defaults.ShowUserTypeBorders;

    [ObservableProperty]
    private bool _highlightFirstTimeUsers = Defaults.HighlightFirstTimeUsers;

    [ObservableProperty]
    private bool _highlightMentions = Defaults.HighlightMentions;

    [ObservableProperty]
    private bool _enableMessageShadows = Defaults.EnableMessageShadows;

    [ObservableProperty]
    private bool _enableSpecialEffects = Defaults.EnableSpecialEffects;

    [ObservableProperty]
    private bool _enableSmoothScroll = Defaults.EnableSmoothScroll;

    [ObservableProperty]
    private bool _autoScrollEnabled = Defaults.AutoScrollEnabled;

    [ObservableProperty]
    private int _scrollAnimationDuration = Defaults.ScrollAnimationDuration;

    [ObservableProperty]
    private bool _enableMessageFadeOut = Defaults.EnableMessageFadeOut;

    [ObservableProperty]
    private int _messageLifetimeSeconds = Defaults.MessageLifetimeSeconds;

    [ObservableProperty]
    private int _fadeOutAnimationDurationMs = Defaults.FadeOutAnimationDurationMs;

    [ObservableProperty]
    private string _fadeOutAnimationType = Defaults.FadeOutAnimationType;

    [ObservableProperty]
    private string _userMessageAnimation = Defaults.UserMessageAnimation;

    [ObservableProperty]
    private string _botMessageAnimation = Defaults.BotMessageAnimation;

    [ObservableProperty]
    private string _systemMessageAnimation = Defaults.SystemMessageAnimation;

    [ObservableProperty]
    private string _broadcasterMessageAnimation = Defaults.BroadcasterMessageAnimation;

    [ObservableProperty]
    private string _firstTimeUserMessageAnimation = Defaults.FirstTimeUserMessageAnimation;

    public ObsChatSettingsSectionViewModel(ObsChatStore store, SettingsManager settings, IDialogService dialogService, IShellLauncher shellLauncher)
    {
        _store = store;
        _settings = settings;
        _dialogService = dialogService;
        _shellLauncher = shellLauncher;

        EntryAnimationOptions = MessageAnimationType.EntryAnimations
            .Select(option => new AnimationOption(option.Value, option.DisplayName))
            .ToArray();

        ExitAnimationOptions = MessageAnimationType.ExitAnimations
            .Select(option => new AnimationOption(option.Value, option.DisplayName))
            .ToArray();

        LoadFrom(_store.Load());
    }

    public string Title => "Чат OBS";

    public IReadOnlyList<AnimationOption> EntryAnimationOptions { get; }

    public IReadOnlyList<AnimationOption> ExitAnimationOptions { get; }

    public void LoadFrom(ObsChatSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _baseline = settings;

        BackgroundColor = settings.BackgroundColor;
        TextColor = settings.TextColor;
        UsernameColor = settings.UsernameColor;
        SystemMessageColor = settings.SystemMessageColor;
        TimestampColor = settings.TimestampColor;

        FontFamily = settings.FontFamily;
        FontSize = settings.FontSize;
        FontBold = settings.FontBold;

        Padding = settings.Padding;
        Margin = settings.Margin;
        BorderRadius = settings.BorderRadius;

        EnableAnimations = settings.EnableAnimations;
        AnimationDuration = settings.AnimationDuration;

        MaxMessages = settings.MaxMessages;
        ShowTimestamp = settings.ShowTimestamp;

        EmoteSizePixels = settings.EmoteSizePixels;
        BadgeSizePixels = settings.BadgeSizePixels;

        ShowUserAvatars = settings.ShowUserAvatars;
        UserAvatarSizePixels = settings.UserAvatarSizePixels;

        ShowMessageImages = settings.ShowMessageImages;
        MessageImagesFromBroadcaster = settings.MessageImageRoles.HasFlag(MessageImageSenderRoles.Broadcaster);
        MessageImagesFromModerators = settings.MessageImageRoles.HasFlag(MessageImageSenderRoles.Moderator);
        MessageImagesFromVips = settings.MessageImageRoles.HasFlag(MessageImageSenderRoles.Vip);
        MessageImagesFromSubscribers = settings.MessageImageRoles.HasFlag(MessageImageSenderRoles.Subscriber);
        MessageImagesFromEveryone = settings.MessageImageRoles.HasFlag(MessageImageSenderRoles.Everyone);
        MessageImageAllowedHostsText = string.Join(Environment.NewLine, settings.MessageImageAllowedHosts ?? []);
        MessageImageMaxHeightPixels = settings.MessageImageMaxHeightPixels;

        ShowUserTypeBorders = settings.ShowUserTypeBorders;
        HighlightFirstTimeUsers = settings.HighlightFirstTimeUsers;
        HighlightMentions = settings.HighlightMentions;
        EnableMessageShadows = settings.EnableMessageShadows;
        EnableSpecialEffects = settings.EnableSpecialEffects;

        EnableSmoothScroll = settings.EnableSmoothScroll;
        AutoScrollEnabled = settings.AutoScrollEnabled;
        ScrollAnimationDuration = settings.ScrollAnimationDuration;

        EnableMessageFadeOut = settings.EnableMessageFadeOut;
        MessageLifetimeSeconds = settings.MessageLifetimeSeconds;
        FadeOutAnimationDurationMs = settings.FadeOutAnimationDurationMs;
        FadeOutAnimationType = settings.FadeOutAnimationType;

        UserMessageAnimation = settings.UserMessageAnimation;
        BotMessageAnimation = settings.BotMessageAnimation;
        SystemMessageAnimation = settings.SystemMessageAnimation;
        BroadcasterMessageAnimation = settings.BroadcasterMessageAnimation;
        FirstTimeUserMessageAnimation = settings.FirstTimeUserMessageAnimation;

        ValidateAllProperties();
    }

    public void ApplyTo(ObsChatSettings target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var current = BuildCurrent();

        target.BackgroundColor = current.BackgroundColor;
        target.TextColor = current.TextColor;
        target.UsernameColor = current.UsernameColor;
        target.SystemMessageColor = current.SystemMessageColor;
        target.TimestampColor = current.TimestampColor;

        target.FontFamily = current.FontFamily;
        target.FontSize = current.FontSize;
        target.FontBold = current.FontBold;

        target.Padding = current.Padding;
        target.Margin = current.Margin;
        target.BorderRadius = current.BorderRadius;

        target.EnableAnimations = current.EnableAnimations;
        target.AnimationDuration = current.AnimationDuration;

        target.MaxMessages = current.MaxMessages;
        target.ShowTimestamp = current.ShowTimestamp;

        target.EmoteSizePixels = current.EmoteSizePixels;
        target.BadgeSizePixels = current.BadgeSizePixels;

        target.ShowUserAvatars = current.ShowUserAvatars;
        target.UserAvatarSizePixels = current.UserAvatarSizePixels;

        target.ShowMessageImages = current.ShowMessageImages;
        target.MessageImageRoles = current.MessageImageRoles;
        target.MessageImageAllowedHosts = current.MessageImageAllowedHosts;
        target.MessageImageMaxHeightPixels = current.MessageImageMaxHeightPixels;

        target.ShowUserTypeBorders = current.ShowUserTypeBorders;
        target.HighlightFirstTimeUsers = current.HighlightFirstTimeUsers;
        target.HighlightMentions = current.HighlightMentions;
        target.EnableMessageShadows = current.EnableMessageShadows;
        target.EnableSpecialEffects = current.EnableSpecialEffects;

        target.EnableSmoothScroll = current.EnableSmoothScroll;
        target.AutoScrollEnabled = current.AutoScrollEnabled;
        target.ScrollAnimationDuration = current.ScrollAnimationDuration;
        target.ScrollToBottomThreshold = current.ScrollToBottomThreshold;
        target.ScrollPauseAfterUserMs = current.ScrollPauseAfterUserMs;

        target.EnableMessageFadeOut = current.EnableMessageFadeOut;
        target.MessageLifetimeSeconds = current.MessageLifetimeSeconds;
        target.FadeOutAnimationDurationMs = current.FadeOutAnimationDurationMs;
        target.FadeOutAnimationType = current.FadeOutAnimationType;

        target.UserMessageAnimation = current.UserMessageAnimation;
        target.BotMessageAnimation = current.BotMessageAnimation;
        target.SystemMessageAnimation = current.SystemMessageAnimation;
        target.BroadcasterMessageAnimation = current.BroadcasterMessageAnimation;
        target.FirstTimeUserMessageAnimation = current.FirstTimeUserMessageAnimation;
    }

    public void Reload()
    {
        LoadFrom(_store.Load());
    }

    public void Save()
    {
        _store.Save(BuildCurrent());
    }

    public static ValidationResult? ValidateMessageImageHosts(string? value, ValidationContext context)
    {
        var invalid = SplitHosts(value)
            .Where(line => !MessageImageHosts.TryNormalizeHost(line, out _))
            .Take(3)
            .ToArray();

        if (invalid.Length == 0)
        {
            return ValidationResult.Success;
        }

        return new($"Не похоже на имя хоста: {string.Join(", ", invalid)}. Нужно доменное имя без схемы и пути, например i.imgur.com");
    }

    public static ValidationResult? ValidateFontFamily(string? value, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success;
        }

        return ContainsForbiddenFontFamilyChars(value)
            ? new ValidationResult("Недопустимые символы. Запрещены: ; < > { }")
            : ValidationResult.Success;
    }

    [RelayCommand]
    private async Task PickColorAsync(string? target)
    {
        var current = ReadColor(target);
        var picker = new ColorPickerDialogViewModel(current, ColorTitle(target));

        if (await _dialogService.ShowAsync(picker))
        {
            WriteColor(target, picker.SelectedColor);
        }
    }

    [RelayCommand]
    private void ResetColor(string? target)
    {
        WriteColor(target, DefaultColor(target));
    }

    [RelayCommand]
    private void ResetAll()
    {
        LoadFrom(new());
    }

    [RelayCommand]
    private void OpenAnimationsDemo()
    {
        var port = _settings.Current.Twitch.HttpServerPort;

        if (port <= 0)
        {
            _dialogService.Warning("Демо анимаций", "Не удалось определить порт HTTP-сервера. Проверьте настройки в разделе «HTTP сервер оверлея».");
            return;
        }

        var url = string.Format(CultureInfo.InvariantCulture, "http://localhost:{0}/animations-demo", port);

        if (!_shellLauncher.Open(url))
        {
            _dialogService.Error("Демо анимаций", "Не удалось открыть демо в браузере.");
        }
    }

    [RelayCommand]
    private void IncreaseFontSize() => FontSize = Step(FontSize, 1, ObsChatRanges.FontSizeMin, ObsChatRanges.FontSizeMax);

    [RelayCommand]
    private void DecreaseFontSize() => FontSize = Step(FontSize, -1, ObsChatRanges.FontSizeMin, ObsChatRanges.FontSizeMax);

    [RelayCommand]
    private void IncreasePadding() => Padding = Step(Padding, 1, ObsChatRanges.PaddingMin, ObsChatRanges.PaddingMax);

    [RelayCommand]
    private void DecreasePadding() => Padding = Step(Padding, -1, ObsChatRanges.PaddingMin, ObsChatRanges.PaddingMax);

    [RelayCommand]
    private void IncreaseMargin() => Margin = Step(Margin, 1, ObsChatRanges.MarginMin, ObsChatRanges.MarginMax);

    [RelayCommand]
    private void DecreaseMargin() => Margin = Step(Margin, -1, ObsChatRanges.MarginMin, ObsChatRanges.MarginMax);

    [RelayCommand]
    private void IncreaseBorderRadius() => BorderRadius = Step(BorderRadius, 1, ObsChatRanges.BorderRadiusMin, ObsChatRanges.BorderRadiusMax);

    [RelayCommand]
    private void DecreaseBorderRadius() => BorderRadius = Step(BorderRadius, -1, ObsChatRanges.BorderRadiusMin, ObsChatRanges.BorderRadiusMax);

    [RelayCommand]
    private void IncreaseAnimationDuration() => AnimationDuration = Step(AnimationDuration, 50, ObsChatRanges.AnimationDurationMin, ObsChatRanges.AnimationDurationMax);

    [RelayCommand]
    private void DecreaseAnimationDuration() => AnimationDuration = Step(AnimationDuration, -50, ObsChatRanges.AnimationDurationMin, ObsChatRanges.AnimationDurationMax);

    [RelayCommand]
    private void IncreaseMaxMessages() => MaxMessages = Step(MaxMessages, 5, ObsChatRanges.MaxMessagesMin, ObsChatRanges.MaxMessagesMax);

    [RelayCommand]
    private void DecreaseMaxMessages() => MaxMessages = Step(MaxMessages, -5, ObsChatRanges.MaxMessagesMin, ObsChatRanges.MaxMessagesMax);

    [RelayCommand]
    private void IncreaseEmoteSize() => EmoteSizePixels = Step(EmoteSizePixels, 1, ObsChatRanges.EmoteSizeMin, ObsChatRanges.EmoteSizeMax);

    [RelayCommand]
    private void DecreaseEmoteSize() => EmoteSizePixels = Step(EmoteSizePixels, -1, ObsChatRanges.EmoteSizeMin, ObsChatRanges.EmoteSizeMax);

    [RelayCommand]
    private void IncreaseBadgeSize() => BadgeSizePixels = Step(BadgeSizePixels, 1, ObsChatRanges.BadgeSizeMin, ObsChatRanges.BadgeSizeMax);

    [RelayCommand]
    private void DecreaseBadgeSize() => BadgeSizePixels = Step(BadgeSizePixels, -1, ObsChatRanges.BadgeSizeMin, ObsChatRanges.BadgeSizeMax);

    [RelayCommand]
    private void IncreaseUserAvatarSize() => UserAvatarSizePixels = Step(UserAvatarSizePixels, 1, ObsChatRanges.UserAvatarSizeMin, ObsChatRanges.UserAvatarSizeMax);

    [RelayCommand]
    private void DecreaseUserAvatarSize() => UserAvatarSizePixels = Step(UserAvatarSizePixels, -1, ObsChatRanges.UserAvatarSizeMin, ObsChatRanges.UserAvatarSizeMax);

    [RelayCommand]
    private void IncreaseScrollAnimationDuration() => ScrollAnimationDuration = Step(ScrollAnimationDuration, 50, ObsChatRanges.ScrollAnimationDurationMin, ObsChatRanges.ScrollAnimationDurationMax);

    [RelayCommand]
    private void DecreaseScrollAnimationDuration() => ScrollAnimationDuration = Step(ScrollAnimationDuration, -50, ObsChatRanges.ScrollAnimationDurationMin, ObsChatRanges.ScrollAnimationDurationMax);

    [RelayCommand]
    private void IncreaseMessageLifetime() => MessageLifetimeSeconds = Step(MessageLifetimeSeconds, 1, ObsChatRanges.MessageLifetimeMin, ObsChatRanges.MessageLifetimeMax);

    [RelayCommand]
    private void DecreaseMessageLifetime() => MessageLifetimeSeconds = Step(MessageLifetimeSeconds, -1, ObsChatRanges.MessageLifetimeMin, ObsChatRanges.MessageLifetimeMax);

    [RelayCommand]
    private void IncreaseFadeOutAnimationDuration() => FadeOutAnimationDurationMs = Step(FadeOutAnimationDurationMs, 100, ObsChatRanges.FadeOutAnimationDurationMin, ObsChatRanges.FadeOutAnimationDurationMax);

    [RelayCommand]
    private void DecreaseFadeOutAnimationDuration() => FadeOutAnimationDurationMs = Step(FadeOutAnimationDurationMs, -100, ObsChatRanges.FadeOutAnimationDurationMin, ObsChatRanges.FadeOutAnimationDurationMax);

    [RelayCommand]
    private void IncreaseMessageImageMaxHeight() => MessageImageMaxHeightPixels = Step(MessageImageMaxHeightPixels, 10, ObsChatRanges.MessageImageMaxHeightMin, ObsChatRanges.MessageImageMaxHeightMax);

    [RelayCommand]
    private void DecreaseMessageImageMaxHeight() => MessageImageMaxHeightPixels = Step(MessageImageMaxHeightPixels, -10, ObsChatRanges.MessageImageMaxHeightMin, ObsChatRanges.MessageImageMaxHeightMax);

    [RelayCommand]
    private void ResetMessageImageHosts()
    {
        MessageImageAllowedHostsText = string.Join(Environment.NewLine, ObsChatSettings.DefaultMessageImageAllowedHosts);
    }

    private static IEnumerable<string> SplitHosts(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(['\r', '\n', ',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int Step(int value, int delta, int min, int max)
    {
        return ObsChatRanges.Clamp(value + delta, min, max);
    }

    private static bool ContainsForbiddenFontFamilyChars(string fontFamily)
    {
        return fontFamily.Contains(';', StringComparison.Ordinal)
               || fontFamily.Contains('<', StringComparison.Ordinal)
               || fontFamily.Contains('>', StringComparison.Ordinal)
               || fontFamily.Contains('{', StringComparison.Ordinal)
               || fontFamily.Contains('}', StringComparison.Ordinal);
    }

    private static string ColorTitle(string? target)
    {
        return target switch
        {
            "Text" => "Цвет текста",
            "Username" => "Цвет имени пользователя",
            "System" => "Цвет системных сообщений",
            "Timestamp" => "Цвет времени",
            _ => "Цвет фона",
        };
    }

    private MessageImageSenderRoles BuildMessageImageRoles()
    {
        var roles = MessageImageSenderRoles.None;

        if (MessageImagesFromBroadcaster)
        {
            roles |= MessageImageSenderRoles.Broadcaster;
        }

        if (MessageImagesFromModerators)
        {
            roles |= MessageImageSenderRoles.Moderator;
        }

        if (MessageImagesFromVips)
        {
            roles |= MessageImageSenderRoles.Vip;
        }

        if (MessageImagesFromSubscribers)
        {
            roles |= MessageImageSenderRoles.Subscriber;
        }

        if (MessageImagesFromEveryone)
        {
            roles |= MessageImageSenderRoles.Everyone;
        }

        return roles;
    }

    private ObsChatSettings BuildCurrent()
    {
        var fontFamily = string.IsNullOrWhiteSpace(FontFamily) || ContainsForbiddenFontFamilyChars(FontFamily)
            ? _baseline.FontFamily
            : FontFamily;

        var built = new ObsChatSettings
        {
            BackgroundColor = BackgroundColor,
            TextColor = TextColor,
            UsernameColor = UsernameColor,
            SystemMessageColor = SystemMessageColor,
            TimestampColor = TimestampColor,

            FontFamily = fontFamily,
            FontSize = FontSize,
            FontBold = FontBold,

            Padding = Padding,
            Margin = Margin,
            BorderRadius = BorderRadius,

            EnableAnimations = EnableAnimations,
            AnimationDuration = AnimationDuration,

            MaxMessages = MaxMessages,
            ShowTimestamp = ShowTimestamp,

            EmoteSizePixels = EmoteSizePixels,
            BadgeSizePixels = BadgeSizePixels,

            ShowUserAvatars = ShowUserAvatars,
            UserAvatarSizePixels = UserAvatarSizePixels,

            ShowMessageImages = ShowMessageImages,
            MessageImageRoles = BuildMessageImageRoles(),
            MessageImageAllowedHosts = MessageImageHosts.Normalize(SplitHosts(MessageImageAllowedHostsText)),
            MessageImageMaxHeightPixels = MessageImageMaxHeightPixels,

            ShowUserTypeBorders = ShowUserTypeBorders,
            HighlightFirstTimeUsers = HighlightFirstTimeUsers,
            HighlightMentions = HighlightMentions,
            EnableMessageShadows = EnableMessageShadows,
            EnableSpecialEffects = EnableSpecialEffects,

            EnableSmoothScroll = EnableSmoothScroll,
            AutoScrollEnabled = AutoScrollEnabled,
            ScrollAnimationDuration = ScrollAnimationDuration,
            ScrollToBottomThreshold = _baseline.ScrollToBottomThreshold,
            ScrollPauseAfterUserMs = _baseline.ScrollPauseAfterUserMs,

            EnableMessageFadeOut = EnableMessageFadeOut,
            MessageLifetimeSeconds = MessageLifetimeSeconds,
            FadeOutAnimationDurationMs = FadeOutAnimationDurationMs,
            FadeOutAnimationType = FadeOutAnimationType,

            UserMessageAnimation = UserMessageAnimation,
            BotMessageAnimation = BotMessageAnimation,
            SystemMessageAnimation = SystemMessageAnimation,
            BroadcasterMessageAnimation = BroadcasterMessageAnimation,
            FirstTimeUserMessageAnimation = FirstTimeUserMessageAnimation,
        };

        return ObsChatSettingsValidator.Clamp(built);
    }

    private Color ReadColor(string? target)
    {
        return target switch
        {
            "Text" => TextColor,
            "Username" => UsernameColor,
            "System" => SystemMessageColor,
            "Timestamp" => TimestampColor,
            _ => BackgroundColor,
        };
    }

    private void WriteColor(string? target, Color value)
    {
        switch (target)
        {
            case "Text":
                TextColor = value;
                break;
            case "Username":
                UsernameColor = value;
                break;
            case "System":
                SystemMessageColor = value;
                break;
            case "Timestamp":
                TimestampColor = value;
                break;
            default:
                BackgroundColor = value;
                break;
        }
    }

    private static Color DefaultColor(string? target)
    {
        return target switch
        {
            "Text" => Defaults.TextColor,
            "Username" => Defaults.UsernameColor,
            "System" => Defaults.SystemMessageColor,
            "Timestamp" => Defaults.TimestampColor,
            _ => Defaults.BackgroundColor,
        };
    }
}
