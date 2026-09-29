using PoproshaykaBot.Core.Twitch.Helix;
using System.Text.Json;

namespace PoproshaykaBot.Core.Polls;

public static class PollEventSubMapper
{
    public static PollSnapshot FromEventSubBegin(JsonElement payload, Guid? sourceProfileId = null)
    {
        return FromEventSubPayload(payload, PollSnapshotStatus.Active, sourceProfileId, null);
    }

    public static PollSnapshot FromEventSubProgress(JsonElement payload, Guid? sourceProfileId = null)
    {
        return FromEventSubPayload(payload, PollSnapshotStatus.Active, sourceProfileId, null);
    }

    public static PollSnapshot FromEventSubEnd(JsonElement payload, Guid? sourceProfileId = null)
    {
        var status = ParseEventSubStatus(TryGetString(payload, "status"));
        return FromEventSubPayload(payload, status, sourceProfileId, "ended_at");
    }

    public static PollSnapshot? FromHelix(HelixPollInfo info, Guid? sourceProfileId)
    {
        return TryParseHelixStatus(info.Status, out var status)
            ? FromHelix(info, sourceProfileId, status)
            : null;
    }

    public static PollSnapshot FromCreatedPoll(HelixPollInfo info, Guid sourceProfileId)
    {
        TryParseHelixStatus(info.Status, out var status);
        return FromHelix(info, sourceProfileId, status);
    }

    private static PollSnapshot FromHelix(HelixPollInfo info, Guid? sourceProfileId, PollSnapshotStatus status)
    {
        var startedUtc = DateTime.SpecifyKind(info.StartedAt, DateTimeKind.Utc);
        var endsUtc = startedUtc.AddSeconds(info.DurationSeconds);
        var endedUtc = info.EndedAt is null
            ? (DateTime?)null
            : DateTime.SpecifyKind(info.EndedAt.Value, DateTimeKind.Utc);

        var choices = info.Choices
            .Select(c => new PollChoiceSnapshot(c.Id, c.Title, c.Votes, c.ChannelPointsVotes, c.BitsVotes))
            .ToArray();

        return new(info.Id,
            sourceProfileId,
            info.Title,
            choices,
            startedUtc,
            endsUtc,
            info.ChannelPointsVotingEnabled,
            info.ChannelPointsPerVote,
            status,
            endedUtc);
    }

    public static (PollChoiceSnapshot? Winner, bool IsTie) DetectWinner(PollSnapshot snapshot)
    {
        if (snapshot.Choices.Count == 0)
        {
            return (null, false);
        }

        var maxVotes = snapshot.Choices.Max(c => c.Votes);

        if (maxVotes == 0)
        {
            return (null, false);
        }

        var leaders = snapshot.Choices.Where(c => c.Votes == maxVotes).ToArray();

        if (leaders.Length > 1)
        {
            return (null, true);
        }

        return (leaders[0], false);
    }

    private static PollSnapshot FromEventSubPayload(
        JsonElement payload,
        PollSnapshotStatus status,
        Guid? sourceProfileId,
        string? endedAtProperty)
    {
        var pollId = payload.GetProperty("id").GetString() ?? string.Empty;
        var title = payload.GetProperty("title").GetString() ?? string.Empty;

        var choices = payload.GetProperty("choices")
            .EnumerateArray()
            .Select(c => new PollChoiceSnapshot(c.GetProperty("id").GetString() ?? string.Empty,
                c.GetProperty("title").GetString() ?? string.Empty,
                TryGetInt(c, "votes"),
                TryGetInt(c, "channel_points_votes"),
                TryGetInt(c, "bits_votes")))
            .ToArray();

        var channelPoints = TryGetObject(payload, "channel_points_voting");
        var cpEnabled = channelPoints?.TryGetProperty("is_enabled", out var e) == true && e.GetBoolean();
        var cpPerVote = channelPoints is null
            ? 0
            : TryGetInt(channelPoints.Value, "amount_per_vote");

        var startedUtc = payload.GetProperty("started_at").GetDateTime().ToUniversalTime();

        DateTime endsUtc;
        if (payload.TryGetProperty("ends_at", out var endsAtElement) && endsAtElement.ValueKind == JsonValueKind.String)
        {
            endsUtc = endsAtElement.GetDateTime().ToUniversalTime();
        }
        else
        {
            endsUtc = startedUtc;
        }

        DateTime? endedUtc = null;

        if (endedAtProperty is not null
            && payload.TryGetProperty(endedAtProperty, out var endedElement)
            && endedElement.ValueKind == JsonValueKind.String)
        {
            endedUtc = endedElement.GetDateTime().ToUniversalTime();
        }

        return new(pollId,
            sourceProfileId,
            title,
            choices,
            startedUtc,
            endsUtc,
            cpEnabled,
            cpPerVote,
            status,
            endedUtc);
    }

    private static PollSnapshotStatus ParseEventSubStatus(string? status)
    {
        return (status ?? string.Empty).ToLowerInvariant() switch
        {
            "completed" => PollSnapshotStatus.Completed,
            "terminated" => PollSnapshotStatus.Terminated,
            "archived" => PollSnapshotStatus.Archived,
            "moderated" => PollSnapshotStatus.Moderated,
            "invalid" => PollSnapshotStatus.Invalid,
            _ => PollSnapshotStatus.Completed,
        };
    }

    private static bool TryParseHelixStatus(string? status, out PollSnapshotStatus parsed)
    {
        switch ((status ?? string.Empty).ToUpperInvariant())
        {
            case "ACTIVE":
                parsed = PollSnapshotStatus.Active;
                return true;

            case "COMPLETED":
                parsed = PollSnapshotStatus.Completed;
                return true;

            case "TERMINATED":
                parsed = PollSnapshotStatus.Terminated;
                return true;

            case "ARCHIVED":
                parsed = PollSnapshotStatus.Archived;
                return true;

            case "MODERATED":
                parsed = PollSnapshotStatus.Moderated;
                return true;

            case "INVALID":
                parsed = PollSnapshotStatus.Invalid;
                return true;

            default:
                parsed = PollSnapshotStatus.Active;
                return false;
        }
    }

    private static int TryGetInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }

    private static string? TryGetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static JsonElement? TryGetObject(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : null;
    }
}
