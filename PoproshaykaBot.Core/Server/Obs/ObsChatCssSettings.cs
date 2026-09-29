using PoproshaykaBot.Core.Settings.Obs;
using System.Drawing;

namespace PoproshaykaBot.Core.Server.Obs;

public sealed class ObsChatCssSettings
{
    public string BackgroundColor { get; set; } = "#000000b5";
    public string TextColor { get; set; } = "#ffffff";
    public string UsernameColor { get; set; } = "#9146ff";
    public string SystemMessageColor { get; set; } = "#ffcc00";
    public string TimestampColor { get; set; } = "#999999";
    public string FontFamily { get; set; } = ObsChatSettings.DefaultFontFamily;
    public string FontSize { get; set; } = "14px";
    public string FontWeight { get; set; } = "normal";
    public string Padding { get; set; } = "5px";
    public string Margin { get; set; } = "5px 0";
    public string BorderRadius { get; set; } = "5px";
    public string AnimationDuration { get; set; } = "300ms";
    public bool EnableAnimations { get; set; } = true;
    public int MaxMessages { get; set; } = 50;
    public bool ShowTimestamp { get; set; } = true;

    public string EmoteSize { get; set; } = "28px";
    public string BadgeSize { get; set; } = "18px";

    public bool ShowUserAvatars { get; set; }
    public string UserAvatarSize { get; set; } = "32px";

    public bool ShowMessageImages { get; set; }
    public string MessageImageMaxHeight { get; set; } = "160px";

    public bool ShowUserTypeBorders { get; set; } = true;
    public bool HighlightFirstTimeUsers { get; set; } = true;
    public bool HighlightMentions { get; set; } = true;
    public bool EnableMessageShadows { get; set; } = true;
    public bool EnableSpecialEffects { get; set; } = true;

    public bool EnableSmoothScroll { get; set; } = true;
    public int ScrollAnimationDuration { get; set; } = 300;
    public bool AutoScrollEnabled { get; set; } = true;
    public int ScrollToBottomThreshold { get; set; } = 100;
    public int ScrollPauseAfterUserMs { get; set; } = 3000;

    public string UserMessageAnimation { get; set; } = MessageAnimationType.SlideInRight;
    public string BotMessageAnimation { get; set; } = MessageAnimationType.FadeInUp;
    public string SystemMessageAnimation { get; set; } = MessageAnimationType.FadeInUp;
    public string BroadcasterMessageAnimation { get; set; } = MessageAnimationType.SlideInLeft;
    public string FirstTimeUserMessageAnimation { get; set; } = MessageAnimationType.BounceIn;

    public bool EnableMessageFadeOut { get; set; } = true;
    public int MessageLifetimeSeconds { get; set; } = 30;
    public string FadeOutAnimationType { get; set; } = MessageAnimationType.FadeOut;
    public int FadeOutAnimationDurationMs { get; set; } = 1000;

    public static ObsChatCssSettings FromObsChatSettings(ObsChatSettings? settings)
    {
        var safeSettings = settings ?? new ObsChatSettings();

        return new()
        {
            BackgroundColor = ColorToCssString(safeSettings.BackgroundColor),
            TextColor = ColorToCssString(safeSettings.TextColor),
            UsernameColor = ColorToCssString(safeSettings.UsernameColor),
            SystemMessageColor = ColorToCssString(safeSettings.SystemMessageColor),
            TimestampColor = ColorToCssString(safeSettings.TimestampColor),
            FontFamily = safeSettings.FontFamily ?? ObsChatSettings.DefaultFontFamily,
            FontSize = $"{ObsChatRanges.Clamp(safeSettings.FontSize, ObsChatRanges.FontSizeMin, ObsChatRanges.FontSizeMax)}px",
            FontWeight = safeSettings.FontBold ? "bold" : "normal",
            Padding = $"{ObsChatRanges.Clamp(safeSettings.Padding, ObsChatRanges.PaddingMin, ObsChatRanges.PaddingMax)}px",
            Margin = $"{ObsChatRanges.Clamp(safeSettings.Margin, ObsChatRanges.MarginMin, ObsChatRanges.MarginMax)}px 0",
            BorderRadius = $"{ObsChatRanges.Clamp(safeSettings.BorderRadius, ObsChatRanges.BorderRadiusMin, ObsChatRanges.BorderRadiusMax)}px",
            AnimationDuration = $"{ObsChatRanges.Clamp(safeSettings.AnimationDuration, ObsChatRanges.AnimationDurationMin, ObsChatRanges.AnimationDurationMax)}ms",
            EnableAnimations = safeSettings.EnableAnimations,
            MaxMessages = ObsChatRanges.Clamp(safeSettings.MaxMessages, ObsChatRanges.MaxMessagesMin, ObsChatRanges.MaxMessagesMax),
            ShowTimestamp = safeSettings.ShowTimestamp,

            EmoteSize = $"{ObsChatRanges.Clamp(safeSettings.EmoteSizePixels, ObsChatRanges.EmoteSizeMin, ObsChatRanges.EmoteSizeMax)}px",
            BadgeSize = $"{ObsChatRanges.Clamp(safeSettings.BadgeSizePixels, ObsChatRanges.BadgeSizeMin, ObsChatRanges.BadgeSizeMax)}px",

            ShowUserAvatars = safeSettings.ShowUserAvatars,
            UserAvatarSize = $"{ObsChatRanges.Clamp(safeSettings.UserAvatarSizePixels, ObsChatRanges.UserAvatarSizeMin, ObsChatRanges.UserAvatarSizeMax)}px",

            ShowMessageImages = safeSettings.ShowMessageImages,
            MessageImageMaxHeight = $"{ObsChatRanges.Clamp(safeSettings.MessageImageMaxHeightPixels, ObsChatRanges.MessageImageMaxHeightMin, ObsChatRanges.MessageImageMaxHeightMax)}px",

            ShowUserTypeBorders = safeSettings.ShowUserTypeBorders,
            HighlightFirstTimeUsers = safeSettings.HighlightFirstTimeUsers,
            HighlightMentions = safeSettings.HighlightMentions,
            EnableMessageShadows = safeSettings.EnableMessageShadows,
            EnableSpecialEffects = safeSettings.EnableSpecialEffects,

            EnableSmoothScroll = safeSettings.EnableSmoothScroll,
            ScrollAnimationDuration = ObsChatRanges.Clamp(safeSettings.ScrollAnimationDuration, ObsChatRanges.ScrollAnimationDurationMin, ObsChatRanges.ScrollAnimationDurationMax),
            AutoScrollEnabled = safeSettings.AutoScrollEnabled,
            ScrollToBottomThreshold = ObsChatRanges.Clamp(safeSettings.ScrollToBottomThreshold, ObsChatRanges.ScrollToBottomThresholdMin, ObsChatRanges.ScrollToBottomThresholdMax),
            ScrollPauseAfterUserMs = ObsChatRanges.Clamp(safeSettings.ScrollPauseAfterUserMs, ObsChatRanges.ScrollPauseAfterUserMsMin, ObsChatRanges.ScrollPauseAfterUserMsMax),

            UserMessageAnimation = safeSettings.UserMessageAnimation,
            BotMessageAnimation = safeSettings.BotMessageAnimation,
            SystemMessageAnimation = safeSettings.SystemMessageAnimation,
            BroadcasterMessageAnimation = safeSettings.BroadcasterMessageAnimation,
            FirstTimeUserMessageAnimation = safeSettings.FirstTimeUserMessageAnimation,

            EnableMessageFadeOut = safeSettings.EnableMessageFadeOut,
            MessageLifetimeSeconds = ObsChatRanges.Clamp(safeSettings.MessageLifetimeSeconds, ObsChatRanges.MessageLifetimeMin, ObsChatRanges.MessageLifetimeMax),
            FadeOutAnimationType = safeSettings.FadeOutAnimationType,
            FadeOutAnimationDurationMs = ObsChatRanges.Clamp(safeSettings.FadeOutAnimationDurationMs, ObsChatRanges.FadeOutAnimationDurationMin, ObsChatRanges.FadeOutAnimationDurationMax),
        };
    }

    private static string ColorToCssString(Color color)
    {
        if (color.A == 255)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
    }
}
