using System.Drawing;

namespace PoproshaykaBot.Core.Settings.Obs;

public sealed class ObsChatSettings
{
    public const string DefaultFontFamily = """
                                            "Motiva Sans", "Inter", "Noto Sans", Arial, sans-serif
                                            """;

    public static readonly IReadOnlyList<string> DefaultMessageImageAllowedHosts =
    [
        "i.imgur.com",
        "imgur.com",
        "static-cdn.jtvnw.net",
        "clips-media-assets2.twitch.tv",
        "cdn.7tv.app",
        "media.discordapp.net",
    ];

    public const MessageImageSenderRoles DefaultMessageImageRoles =
        MessageImageSenderRoles.Broadcaster | MessageImageSenderRoles.Moderator | MessageImageSenderRoles.Vip;

    public Color BackgroundColor { get; set; } = Color.FromArgb(179, 0, 0, 0);
    public Color TextColor { get; set; } = Color.FromArgb(255, 255, 255);
    public Color UsernameColor { get; set; } = Color.FromArgb(145, 70, 255);
    public Color SystemMessageColor { get; set; } = Color.FromArgb(255, 204, 0);
    public Color TimestampColor { get; set; } = Color.FromArgb(153, 153, 153);

    public string FontFamily { get; set; } = DefaultFontFamily;
    public int FontSize { get; set; } = 14;
    public bool FontBold { get; set; } = false;

    public int Padding { get; set; } = 5;
    public int Margin { get; set; } = 5;
    public int BorderRadius { get; set; } = 5;

    public int AnimationDuration { get; set; } = 300;
    public bool EnableAnimations { get; set; } = true;

    public int MaxMessages { get; set; } = 50;
    public bool ShowTimestamp { get; set; } = true;

    public int EmoteSizePixels { get; set; } = 28;
    public int BadgeSizePixels { get; set; } = 18;

    public bool ShowUserAvatars { get; set; } = false;
    public int UserAvatarSizePixels { get; set; } = 32;

    public bool ShowMessageImages { get; set; } = false;
    public MessageImageSenderRoles MessageImageRoles { get; set; } = DefaultMessageImageRoles;
    public List<string> MessageImageAllowedHosts { get; set; } = [..DefaultMessageImageAllowedHosts];
    public int MessageImageMaxHeightPixels { get; set; } = 160;

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
}
