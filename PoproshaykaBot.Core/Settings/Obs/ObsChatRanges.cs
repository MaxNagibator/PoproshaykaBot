namespace PoproshaykaBot.Core.Settings.Obs;

public static class ObsChatRanges
{
    public const int FontSizeMin = 8;
    public const int FontSizeMax = 72;

    public const int PaddingMin = 0;
    public const int PaddingMax = 50;

    public const int MarginMin = 0;
    public const int MarginMax = 50;

    public const int BorderRadiusMin = 0;
    public const int BorderRadiusMax = 50;

    public const int AnimationDurationMin = 100;
    public const int AnimationDurationMax = 2000;

    public const int MaxMessagesMin = 10;
    public const int MaxMessagesMax = 200;

    public const int EmoteSizeMin = 16;
    public const int EmoteSizeMax = 128;

    public const int BadgeSizeMin = 12;
    public const int BadgeSizeMax = 72;

    public const int UserAvatarSizeMin = 16;
    public const int UserAvatarSizeMax = 96;

    public const int MessageImageMaxHeightMin = 40;
    public const int MessageImageMaxHeightMax = 720;

    public const int MessageLifetimeMin = 1;
    public const int MessageLifetimeMax = 3600;

    public const int FadeOutAnimationDurationMin = 100;
    public const int FadeOutAnimationDurationMax = 10000;

    public const int ScrollAnimationDurationMin = 0;
    public const int ScrollAnimationDurationMax = 2000;

    public const int ScrollToBottomThresholdMin = 0;
    public const int ScrollToBottomThresholdMax = 1000;

    public const int ScrollPauseAfterUserMsMin = 0;
    public const int ScrollPauseAfterUserMsMax = 30000;

    public static int Clamp(int value, int min, int max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}
