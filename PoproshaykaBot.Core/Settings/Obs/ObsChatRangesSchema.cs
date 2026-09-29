using System.Collections.ObjectModel;

namespace PoproshaykaBot.Core.Settings.Obs;

public sealed record ObsChatRangeSchema(int Min, int Max, int Step);

public static class ObsChatRangesSchema
{
    public static IReadOnlyDictionary<string, ObsChatRangeSchema> All { get; } =
        new ReadOnlyDictionary<string, ObsChatRangeSchema>(new Dictionary<string, ObsChatRangeSchema>(StringComparer.Ordinal)
        {
            ["fontSize"] = new(ObsChatRanges.FontSizeMin, ObsChatRanges.FontSizeMax, 1),
            ["padding"] = new(ObsChatRanges.PaddingMin, ObsChatRanges.PaddingMax, 1),
            ["margin"] = new(ObsChatRanges.MarginMin, ObsChatRanges.MarginMax, 1),
            ["borderRadius"] = new(ObsChatRanges.BorderRadiusMin, ObsChatRanges.BorderRadiusMax, 1),
            ["animationDuration"] = new(ObsChatRanges.AnimationDurationMin, ObsChatRanges.AnimationDurationMax, 50),
            ["maxMessages"] = new(ObsChatRanges.MaxMessagesMin, ObsChatRanges.MaxMessagesMax, 1),
            ["emoteSizePixels"] = new(ObsChatRanges.EmoteSizeMin, ObsChatRanges.EmoteSizeMax, 1),
            ["badgeSizePixels"] = new(ObsChatRanges.BadgeSizeMin, ObsChatRanges.BadgeSizeMax, 1),
            ["userAvatarSizePixels"] = new(ObsChatRanges.UserAvatarSizeMin, ObsChatRanges.UserAvatarSizeMax, 1),
            ["messageImageMaxHeightPixels"] = new(ObsChatRanges.MessageImageMaxHeightMin, ObsChatRanges.MessageImageMaxHeightMax, 10),
            ["scrollAnimationDuration"] = new(ObsChatRanges.ScrollAnimationDurationMin, ObsChatRanges.ScrollAnimationDurationMax, 50),
            ["scrollToBottomThreshold"] = new(ObsChatRanges.ScrollToBottomThresholdMin, ObsChatRanges.ScrollToBottomThresholdMax, 10),
            ["scrollPauseAfterUserMs"] = new(ObsChatRanges.ScrollPauseAfterUserMsMin, ObsChatRanges.ScrollPauseAfterUserMsMax, 100),
            ["messageLifetimeSeconds"] = new(ObsChatRanges.MessageLifetimeMin, ObsChatRanges.MessageLifetimeMax, 1),
            ["fadeOutAnimationDurationMs"] = new(ObsChatRanges.FadeOutAnimationDurationMin, ObsChatRanges.FadeOutAnimationDurationMax, 50),
        });
}
