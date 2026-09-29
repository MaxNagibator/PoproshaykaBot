using System.Text.Json.Serialization;

namespace PoproshaykaBot.Core.Twitch.Helix;

internal sealed record HelixEnvelope<T>(
    [property: JsonPropertyName("data")]
    IReadOnlyList<T>? Data,
    [property: JsonPropertyName("pagination")]
    HelixPagination? Pagination);

internal sealed record HelixPagination(
    [property: JsonPropertyName("cursor")]
    string? Cursor);

internal sealed record HelixUserDto(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("login")]
    string Login,
    [property: JsonPropertyName("display_name")]
    string DisplayName,
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("broadcaster_type")]
    string BroadcasterType,
    [property: JsonPropertyName("description")]
    string? Description,
    [property: JsonPropertyName("profile_image_url")]
    string? ProfileImageUrl,
    [property: JsonPropertyName("offline_image_url")]
    string? OfflineImageUrl,
    [property: JsonPropertyName("created_at")]
    DateTime CreatedAt);

internal sealed record HelixStreamDto(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("user_id")]
    string UserId,
    [property: JsonPropertyName("user_login")]
    string UserLogin,
    [property: JsonPropertyName("user_name")]
    string UserName,
    [property: JsonPropertyName("game_id")]
    string GameId,
    [property: JsonPropertyName("game_name")]
    string GameName,
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("title")]
    string Title,
    [property: JsonPropertyName("viewer_count")]
    int ViewerCount,
    [property: JsonPropertyName("started_at")]
    DateTime StartedAt,
    [property: JsonPropertyName("language")]
    string Language,
    [property: JsonPropertyName("thumbnail_url")]
    string ThumbnailUrl,
    [property: JsonPropertyName("tags")]
    IReadOnlyList<string>? Tags,
    [property: JsonPropertyName("is_mature")]
    bool IsMature);

internal sealed record HelixChannelDto(
    [property: JsonPropertyName("broadcaster_id")]
    string BroadcasterId,
    [property: JsonPropertyName("broadcaster_login")]
    string BroadcasterLogin,
    [property: JsonPropertyName("broadcaster_name")]
    string BroadcasterName,
    [property: JsonPropertyName("broadcaster_language")]
    string BroadcasterLanguage,
    [property: JsonPropertyName("game_id")]
    string GameId,
    [property: JsonPropertyName("game_name")]
    string GameName,
    [property: JsonPropertyName("title")]
    string Title,
    [property: JsonPropertyName("delay")]
    int Delay,
    [property: JsonPropertyName("tags")]
    IReadOnlyList<string>? Tags,
    [property: JsonPropertyName("content_classification_labels")]
    IReadOnlyList<string>? ContentClassificationLabels,
    [property: JsonPropertyName("is_branded_content")]
    bool IsBrandedContent);

internal sealed record HelixGameDto(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("box_art_url")]
    string? BoxArtUrl,
    [property: JsonPropertyName("igdb_id")]
    string? IgdbId);

internal sealed record HelixCategoryDto(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("box_art_url")]
    string? BoxArtUrl);

internal sealed record HelixPatchChannelDto(
    [property: JsonPropertyName("title")]
    string? Title,
    [property: JsonPropertyName("game_id")]
    string? GameId,
    [property: JsonPropertyName("broadcaster_language")]
    string? BroadcasterLanguage,
    [property: JsonPropertyName("tags")]
    IReadOnlyList<string>? Tags,
    [property: JsonPropertyName("content_classification_labels")]
    IReadOnlyList<HelixCclDto>? ContentClassificationLabels,
    [property: JsonPropertyName("is_branded_content")]
    bool? IsBrandedContent);

internal sealed record HelixCclDto(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("is_enabled")]
    bool IsEnabled);

internal sealed record HelixSendChatMessageDto(
    [property: JsonPropertyName("broadcaster_id")]
    string BroadcasterId,
    [property: JsonPropertyName("sender_id")]
    string SenderId,
    [property: JsonPropertyName("message")]
    string Message,
    [property: JsonPropertyName("reply_parent_message_id")]
    string? ReplyParentMessageId);

internal sealed record HelixSendWhisperDto(
    [property: JsonPropertyName("message")]
    string Message);

internal sealed record HelixChatMessageSendResponse(
    [property: JsonPropertyName("data")]
    IReadOnlyList<HelixChatMessageSendItemDto>? Data);

internal sealed record HelixChatMessageSendItemDto(
    [property: JsonPropertyName("message_id")]
    string MessageId,
    [property: JsonPropertyName("is_sent")]
    bool IsSent,
    [property: JsonPropertyName("drop_reason")]
    HelixChatMessageDropReasonDto? DropReason);

internal sealed record HelixChatMessageDropReasonDto(
    [property: JsonPropertyName("code")]
    string Code,
    [property: JsonPropertyName("message")]
    string Message);

internal sealed record HelixEventSubSubscriptionRequest(
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("version")]
    string Version,
    [property: JsonPropertyName("condition")]
    IReadOnlyDictionary<string, string> Condition,
    [property: JsonPropertyName("transport")]
    HelixEventSubTransportRequest Transport);

internal sealed record HelixEventSubTransportRequest(
    [property: JsonPropertyName("method")]
    string Method,
    [property: JsonPropertyName("session_id")]
    string SessionId);

internal sealed record HelixEventSubSubscriptionDto(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("version")]
    string Version,
    [property: JsonPropertyName("created_at")]
    DateTime CreatedAt);

internal sealed record HelixEventSubSubscriptionListItemDto(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("condition")]
    IReadOnlyDictionary<string, string>? Condition,
    [property: JsonPropertyName("transport")]
    HelixEventSubTransportDto? Transport);

internal sealed record HelixEventSubTransportDto(
    [property: JsonPropertyName("method")]
    string? Method,
    [property: JsonPropertyName("session_id")]
    string? SessionId);

internal sealed record HelixGlobalBadgesResponse(
    [property: JsonPropertyName("data")]
    IReadOnlyList<HelixGlobalBadgeDto>? Data);

internal sealed record HelixGlobalBadgeDto(
    [property: JsonPropertyName("set_id")]
    string SetId,
    [property: JsonPropertyName("versions")]
    IReadOnlyList<HelixGlobalBadgeVersionDto>? Versions);

internal sealed record HelixGlobalBadgeVersionDto(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("image_url_1x")]
    string ImageUrl1x,
    [property: JsonPropertyName("image_url_2x")]
    string ImageUrl2x,
    [property: JsonPropertyName("image_url_4x")]
    string ImageUrl4x,
    [property: JsonPropertyName("title")]
    string? Title,
    [property: JsonPropertyName("description")]
    string? Description,
    [property: JsonPropertyName("click_action")]
    string? ClickAction,
    [property: JsonPropertyName("click_url")]
    string? ClickUrl);

internal sealed record HelixGlobalEmotesResponse(
    [property: JsonPropertyName("data")]
    IReadOnlyList<HelixGlobalEmoteDto>? Data);

internal sealed record HelixGlobalEmoteDto(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("images")]
    HelixGlobalEmoteImagesDto? Images,
    [property: JsonPropertyName("format")]
    IReadOnlyList<string>? Formats,
    [property: JsonPropertyName("scale")]
    IReadOnlyList<string>? Scales,
    [property: JsonPropertyName("theme_mode")]
    IReadOnlyList<string>? ThemeModes);

internal sealed record HelixGlobalEmoteImagesDto(
    [property: JsonPropertyName("url_1x")]
    string Url1x,
    [property: JsonPropertyName("url_2x")]
    string Url2x,
    [property: JsonPropertyName("url_4x")]
    string Url4x);

public sealed record UserInfo(
    string Id,
    string Login,
    string DisplayName,
    string Type,
    string BroadcasterType,
    string? Description,
    string? ProfileImageUrl,
    string? OfflineImageUrl,
    DateTime CreatedAt);

public sealed record HelixStreamInfo(
    string Id,
    string UserId,
    string UserLogin,
    string UserName,
    string GameId,
    string GameName,
    string Type,
    string Title,
    int ViewerCount,
    DateTime StartedAt,
    string Language,
    string ThumbnailUrl,
    IReadOnlyList<string> Tags,
    bool IsMature);

public sealed record ChannelInfo(
    string BroadcasterId,
    string BroadcasterLogin,
    string BroadcasterName,
    string BroadcasterLanguage,
    string GameId,
    string GameName,
    string Title,
    int Delay,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> ContentClassificationLabels,
    bool IsBrandedContent);

public sealed record GameInfo(
    string Id,
    string Name,
    string? BoxArtUrl,
    string? IgdbId);

public sealed record EventSubSubscriptionInfo(
    string Id,
    string Type,
    string Status,
    string? SessionId,
    IReadOnlyDictionary<string, string> Condition);

public sealed record PatchChannelRequest
{
    public string? Title { get; init; }
    public string? GameId { get; init; }
    public string? BroadcasterLanguage { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public IReadOnlyList<ContentClassificationLabelUpdate>? ContentClassificationLabels { get; init; }
    public bool? IsBrandedContent { get; init; }
}

public sealed record ContentClassificationLabelUpdate(string Id, bool IsEnabled);

public sealed record GlobalBadgeInfo(
    string SetId,
    IReadOnlyList<GlobalBadgeVersion> Versions);

public sealed record GlobalBadgeVersion(
    string Id,
    string ImageUrl1x,
    string ImageUrl2x,
    string ImageUrl4x,
    string? Title,
    string? Description,
    string? ClickAction,
    string? ClickUrl);

public sealed record GlobalEmoteInfo(
    string Id,
    string Name,
    GlobalEmoteImages Images,
    IReadOnlyList<string> Formats,
    IReadOnlyList<string> Scales,
    IReadOnlyList<string> ThemeModes);

public sealed record GlobalEmoteImages(
    string Url1x,
    string Url2x,
    string Url4x);

internal sealed record HelixCreatePollDto(
    [property: JsonPropertyName("broadcaster_id")]
    string BroadcasterId,
    [property: JsonPropertyName("title")]
    string Title,
    [property: JsonPropertyName("choices")]
    IReadOnlyList<HelixCreatePollChoiceDto> Choices,
    [property: JsonPropertyName("duration")]
    int Duration,
    [property: JsonPropertyName("channel_points_voting_enabled")]
    bool ChannelPointsVotingEnabled,
    [property: JsonPropertyName("channel_points_per_vote")]
    int ChannelPointsPerVote);

internal sealed record HelixCreatePollChoiceDto(
    [property: JsonPropertyName("title")]
    string Title);

internal sealed record HelixEndPollDto(
    [property: JsonPropertyName("broadcaster_id")]
    string BroadcasterId,
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("status")]
    string Status);

internal sealed record HelixPollDto(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("broadcaster_id")]
    string BroadcasterId,
    [property: JsonPropertyName("broadcaster_name")]
    string BroadcasterName,
    [property: JsonPropertyName("broadcaster_login")]
    string BroadcasterLogin,
    [property: JsonPropertyName("title")]
    string Title,
    [property: JsonPropertyName("choices")]
    IReadOnlyList<HelixPollChoiceDto>? Choices,
    [property: JsonPropertyName("channel_points_voting_enabled")]
    bool ChannelPointsVotingEnabled,
    [property: JsonPropertyName("channel_points_per_vote")]
    int ChannelPointsPerVote,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("duration")]
    int Duration,
    [property: JsonPropertyName("started_at")]
    DateTime StartedAt,
    [property: JsonPropertyName("ended_at")]
    DateTime? EndedAt);

internal sealed record HelixPollChoiceDto(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("title")]
    string Title,
    [property: JsonPropertyName("votes")]
    int Votes,
    [property: JsonPropertyName("channel_points_votes")]
    int ChannelPointsVotes,
    [property: JsonPropertyName("bits_votes")]
    int BitsVotes);

public sealed record CreatePollRequest
{
    public required string BroadcasterId { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<string> Choices { get; init; }
    public required int DurationSeconds { get; init; }
    public bool ChannelPointsVotingEnabled { get; init; }
    public int ChannelPointsPerVote { get; init; }
}

public sealed record EndPollRequest
{
    public required string BroadcasterId { get; init; }
    public required string PollId { get; init; }
    public bool ShowResult { get; init; } = true;
}

public sealed record HelixPollInfo(
    string Id,
    string BroadcasterId,
    string Title,
    IReadOnlyList<HelixPollChoice> Choices,
    bool ChannelPointsVotingEnabled,
    int ChannelPointsPerVote,
    string Status,
    int DurationSeconds,
    DateTime StartedAt,
    DateTime? EndedAt);

public sealed record HelixPollChoice(
    string Id,
    string Title,
    int Votes,
    int ChannelPointsVotes,
    int BitsVotes);
