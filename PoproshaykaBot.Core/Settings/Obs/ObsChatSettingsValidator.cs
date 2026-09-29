namespace PoproshaykaBot.Core.Settings.Obs;

public static class ObsChatSettingsValidator
{
    public static ObsChatSettings Clamp(ObsChatSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new()
        {
            BackgroundColor = source.BackgroundColor,
            TextColor = source.TextColor,
            UsernameColor = source.UsernameColor,
            SystemMessageColor = source.SystemMessageColor,
            TimestampColor = source.TimestampColor,

            FontFamily = string.IsNullOrWhiteSpace(source.FontFamily)
                ? ObsChatSettings.DefaultFontFamily
                : source.FontFamily,
            FontSize = ObsChatRanges.Clamp(source.FontSize, ObsChatRanges.FontSizeMin, ObsChatRanges.FontSizeMax),
            FontBold = source.FontBold,

            Padding = ObsChatRanges.Clamp(source.Padding, ObsChatRanges.PaddingMin, ObsChatRanges.PaddingMax),
            Margin = ObsChatRanges.Clamp(source.Margin, ObsChatRanges.MarginMin, ObsChatRanges.MarginMax),
            BorderRadius = ObsChatRanges.Clamp(source.BorderRadius, ObsChatRanges.BorderRadiusMin, ObsChatRanges.BorderRadiusMax),

            AnimationDuration = ObsChatRanges.Clamp(source.AnimationDuration, ObsChatRanges.AnimationDurationMin, ObsChatRanges.AnimationDurationMax),
            EnableAnimations = source.EnableAnimations,

            MaxMessages = ObsChatRanges.Clamp(source.MaxMessages, ObsChatRanges.MaxMessagesMin, ObsChatRanges.MaxMessagesMax),
            ShowTimestamp = source.ShowTimestamp,

            EmoteSizePixels = ObsChatRanges.Clamp(source.EmoteSizePixels, ObsChatRanges.EmoteSizeMin, ObsChatRanges.EmoteSizeMax),
            BadgeSizePixels = ObsChatRanges.Clamp(source.BadgeSizePixels, ObsChatRanges.BadgeSizeMin, ObsChatRanges.BadgeSizeMax),

            ShowUserAvatars = source.ShowUserAvatars,
            UserAvatarSizePixels = ObsChatRanges.Clamp(source.UserAvatarSizePixels, ObsChatRanges.UserAvatarSizeMin, ObsChatRanges.UserAvatarSizeMax),

            ShowMessageImages = source.ShowMessageImages,
            MessageImageRoles = NormalizeRoles(source.MessageImageRoles),
            MessageImageAllowedHosts = MessageImageHosts.Normalize(source.MessageImageAllowedHosts),
            MessageImageMaxHeightPixels = ObsChatRanges.Clamp(source.MessageImageMaxHeightPixels, ObsChatRanges.MessageImageMaxHeightMin, ObsChatRanges.MessageImageMaxHeightMax),

            ShowUserTypeBorders = source.ShowUserTypeBorders,
            HighlightFirstTimeUsers = source.HighlightFirstTimeUsers,
            HighlightMentions = source.HighlightMentions,
            EnableMessageShadows = source.EnableMessageShadows,
            EnableSpecialEffects = source.EnableSpecialEffects,

            EnableSmoothScroll = source.EnableSmoothScroll,
            ScrollAnimationDuration = ObsChatRanges.Clamp(source.ScrollAnimationDuration, ObsChatRanges.ScrollAnimationDurationMin, ObsChatRanges.ScrollAnimationDurationMax),
            AutoScrollEnabled = source.AutoScrollEnabled,
            ScrollToBottomThreshold = ObsChatRanges.Clamp(source.ScrollToBottomThreshold, ObsChatRanges.ScrollToBottomThresholdMin, ObsChatRanges.ScrollToBottomThresholdMax),
            ScrollPauseAfterUserMs = ObsChatRanges.Clamp(source.ScrollPauseAfterUserMs, ObsChatRanges.ScrollPauseAfterUserMsMin, ObsChatRanges.ScrollPauseAfterUserMsMax),

            UserMessageAnimation = NormalizeAnimation(source.UserMessageAnimation, MessageAnimationType.EntryValues, MessageAnimationType.SlideInRight),
            BotMessageAnimation = NormalizeAnimation(source.BotMessageAnimation, MessageAnimationType.EntryValues, MessageAnimationType.FadeInUp),
            SystemMessageAnimation = NormalizeAnimation(source.SystemMessageAnimation, MessageAnimationType.EntryValues, MessageAnimationType.FadeInUp),
            BroadcasterMessageAnimation = NormalizeAnimation(source.BroadcasterMessageAnimation, MessageAnimationType.EntryValues, MessageAnimationType.SlideInLeft),
            FirstTimeUserMessageAnimation = NormalizeAnimation(source.FirstTimeUserMessageAnimation, MessageAnimationType.EntryValues, MessageAnimationType.BounceIn),

            EnableMessageFadeOut = source.EnableMessageFadeOut,
            MessageLifetimeSeconds = ObsChatRanges.Clamp(source.MessageLifetimeSeconds, ObsChatRanges.MessageLifetimeMin, ObsChatRanges.MessageLifetimeMax),
            FadeOutAnimationType = NormalizeAnimation(source.FadeOutAnimationType, MessageAnimationType.ExitValues, MessageAnimationType.FadeOut),
            FadeOutAnimationDurationMs = ObsChatRanges.Clamp(source.FadeOutAnimationDurationMs, ObsChatRanges.FadeOutAnimationDurationMin, ObsChatRanges.FadeOutAnimationDurationMax),
        };
    }

    private static MessageImageSenderRoles NormalizeRoles(MessageImageSenderRoles source)
    {
        const MessageImageSenderRoles Known = MessageImageSenderRoles.Broadcaster
            | MessageImageSenderRoles.Moderator
            | MessageImageSenderRoles.Vip
            | MessageImageSenderRoles.Subscriber
            | MessageImageSenderRoles.Everyone;

        return source & Known;
    }

    private static string NormalizeAnimation(string? incoming, IReadOnlyList<string> allowed, string fallback)
    {
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return fallback;
        }

        for (var index = 0; index < allowed.Count; index++)
        {
            if (string.Equals(allowed[index], incoming, StringComparison.Ordinal))
            {
                return incoming;
            }
        }

        return fallback;
    }
}
