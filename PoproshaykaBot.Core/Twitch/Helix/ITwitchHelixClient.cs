namespace PoproshaykaBot.Core.Twitch.Helix;

public interface ITwitchHelixClient
{
    Task<UserInfo?> GetUserByLoginAsync(string login, CancellationToken cancellationToken = default);

    Task<UserInfo?> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserInfo>> GetUsersByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    Task<HelixStreamInfo?> GetStreamAsync(string broadcasterId, CancellationToken cancellationToken = default);

    Task<ChannelInfo?> GetChannelInfoAsync(string broadcasterId, CancellationToken cancellationToken = default);

    Task PatchChannelAsync(string broadcasterId, PatchChannelRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameInfo>> SearchCategoriesAsync(string query, int first, CancellationToken cancellationToken = default);

    Task<GameInfo?> GetGameByIdAsync(string gameId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameInfo>> GetGamesByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default);

    Task<string?> SendChatMessageAsync(
        string broadcasterId,
        string senderId,
        string message,
        string? replyParentMessageId = null,
        CancellationToken cancellationToken = default);

    Task SendWhisperAsync(string fromUserId, string toUserId, string message, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, GlobalBadgeInfo>> GetGlobalChatBadgesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, GlobalEmoteInfo>> GetGlobalChatEmotesAsync(CancellationToken cancellationToken = default);

    Task<string> CreateEventSubSubscriptionAsync(
        string type,
        string version,
        IReadOnlyDictionary<string, string> condition,
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteEventSubSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventSubSubscriptionInfo>> GetEventSubSubscriptionsByUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<HelixPollInfo> CreatePollAsync(CreatePollRequest request, CancellationToken cancellationToken = default);

    Task<HelixPollInfo> EndPollAsync(EndPollRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HelixPollInfo>> GetPollsAsync(
        string broadcasterId,
        int first = 20,
        CancellationToken cancellationToken = default);
}
