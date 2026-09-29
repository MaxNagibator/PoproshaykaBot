using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Streaming;

internal static class StreamInfoMapper
{
    public static StreamInfo MapFromHelix(HelixStreamInfo stream)
    {
        return new()
        {
            Id = stream.Id,
            UserId = stream.UserId,
            UserLogin = stream.UserLogin,
            UserName = stream.UserName,
            GameId = stream.GameId,
            GameName = stream.GameName,
            Title = stream.Title,
            Language = stream.Language,
            ViewerCount = stream.ViewerCount,
            StartedAt = stream.StartedAt,
            ThumbnailUrl = stream.ThumbnailUrl,
            Tags = stream.Tags,
            IsMature = stream.IsMature,
        };
    }

    public static StreamInfo Clone(StreamInfo source)
    {
        return new()
        {
            Id = source.Id,
            UserId = source.UserId,
            UserLogin = source.UserLogin,
            UserName = source.UserName,
            GameId = source.GameId,
            GameName = source.GameName,
            Title = source.Title,
            Language = source.Language,
            ViewerCount = source.ViewerCount,
            StartedAt = source.StartedAt,
            ThumbnailUrl = source.ThumbnailUrl,
            Tags = source.Tags,
            IsMature = source.IsMature,
        };
    }

    public static bool SameSnapshot(StreamInfo? left, StreamInfo? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(left.Id, right.Id, StringComparison.Ordinal)
               && string.Equals(left.UserId, right.UserId, StringComparison.Ordinal)
               && string.Equals(left.UserLogin, right.UserLogin, StringComparison.Ordinal)
               && string.Equals(left.UserName, right.UserName, StringComparison.Ordinal)
               && string.Equals(left.GameId, right.GameId, StringComparison.Ordinal)
               && string.Equals(left.GameName, right.GameName, StringComparison.Ordinal)
               && string.Equals(left.Title, right.Title, StringComparison.Ordinal)
               && string.Equals(left.Language, right.Language, StringComparison.Ordinal)
               && string.Equals(left.ThumbnailUrl, right.ThumbnailUrl, StringComparison.Ordinal)
               && left.StartedAt == right.StartedAt
               && left.IsMature == right.IsMature
               && left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal);
    }

    public static StreamInfo MergeChannelUpdate(StreamInfo source, ChannelUpdated update)
    {
        var merged = Clone(source);
        merged.Title = update.Title;
        merged.Language = update.Language;
        merged.GameId = update.GameId;
        merged.GameName = update.GameName;
        return merged;
    }
}
