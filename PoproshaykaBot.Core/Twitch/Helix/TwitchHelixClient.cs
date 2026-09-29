using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoproshaykaBot.Core.Twitch.Helix;

public abstract class TwitchHelixClient(IHttpClientFactory httpClientFactory, ILogger<TwitchHelixClient> logger) : ITwitchHelixClient
{
    public const int MaxGamesPerRequest = 100;
    public const int MaxEventSubSubscriptionPages = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected abstract string HttpClientName { get; }

    public async Task<UserInfo?> GetUserByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return null;
        }

        var dto = await GetSingleAsync<HelixUserDto>($"{TwitchEndpoints.HelixUsers}?login={Uri.EscapeDataString(login)}",
            cancellationToken);

        return dto is null ? null : MapUser(dto);
    }

    public async Task<UserInfo?> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default)
    {
        var dto = await GetSingleAsync<HelixUserDto>(TwitchEndpoints.HelixUsers, cancellationToken);
        return dto is null ? null : MapUser(dto);
    }

    public async Task<IReadOnlyList<UserInfo>> GetUsersByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Take(100).ToArray();
        if (idList.Length == 0)
        {
            return [];
        }

        var query = string.Join('&', idList.Select(id => $"id={Uri.EscapeDataString(id)}"));
        var data = await GetCollectionAsync<HelixUserDto>($"{TwitchEndpoints.HelixUsers}?{query}", cancellationToken);
        return data.Select(MapUser).ToArray();
    }

    public async Task<HelixStreamInfo?> GetStreamAsync(string broadcasterId, CancellationToken cancellationToken = default)
    {
        var dto = await GetSingleAsync<HelixStreamDto>($"{TwitchEndpoints.HelixStreams}?user_id={Uri.EscapeDataString(broadcasterId)}",
            cancellationToken);

        return dto is null ? null : MapStream(dto);
    }

    public async Task<ChannelInfo?> GetChannelInfoAsync(string broadcasterId, CancellationToken cancellationToken = default)
    {
        var dto = await GetSingleAsync<HelixChannelDto>($"{TwitchEndpoints.HelixChannels}?broadcaster_id={Uri.EscapeDataString(broadcasterId)}",
            cancellationToken);

        return dto is null ? null : MapChannel(dto);
    }

    public async Task PatchChannelAsync(string broadcasterId, PatchChannelRequest request, CancellationToken cancellationToken = default)
    {
        var ccl = request.ContentClassificationLabels?
            .Select(x => new HelixCclDto(x.Id, x.IsEnabled))
            .ToArray();

        var body = new HelixPatchChannelDto(request.Title,
            request.GameId,
            request.BroadcasterLanguage,
            request.Tags,
            ccl,
            request.IsBrandedContent);

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch,
            $"{TwitchEndpoints.HelixChannels}?broadcaster_id={Uri.EscapeDataString(broadcasterId)}")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(httpRequest, response, cancellationToken);
    }

    public async Task<IReadOnlyList<GameInfo>> SearchCategoriesAsync(string query, int first, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var data = await GetCollectionAsync<HelixCategoryDto>($"{TwitchEndpoints.HelixSearchCategories}?query={Uri.EscapeDataString(query)}&first={first}",
            cancellationToken);

        return data.Select(c => new GameInfo(c.Id, c.Name, c.BoxArtUrl, null)).ToArray();
    }

    public async Task<GameInfo?> GetGameByIdAsync(string gameId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return null;
        }

        var dto = await GetSingleAsync<HelixGameDto>($"{TwitchEndpoints.HelixGames}?id={Uri.EscapeDataString(gameId)}",
            cancellationToken);

        return dto is null ? null : new GameInfo(dto.Id, dto.Name, dto.BoxArtUrl, dto.IgdbId);
    }

    public async Task<IReadOnlyList<GameInfo>> GetGamesByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        var nameList = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxGamesPerRequest)
            .ToArray();

        if (nameList.Length == 0)
        {
            return [];
        }

        var query = string.Join('&', nameList.Select(name => $"name={Uri.EscapeDataString(name)}"));
        var data = await GetCollectionAsync<HelixGameDto>($"{TwitchEndpoints.HelixGames}?{query}", cancellationToken);

        return data.Select(dto => new GameInfo(dto.Id, dto.Name, dto.BoxArtUrl, dto.IgdbId)).ToArray();
    }

    public async Task<string?> SendChatMessageAsync(
        string broadcasterId,
        string senderId,
        string message,
        string? replyParentMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var dto = new HelixSendChatMessageDto(broadcasterId, senderId, message, replyParentMessageId);

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, TwitchEndpoints.HelixChatMessages)
        {
            Content = JsonContent.Create(dto, options: JsonOptions),
        };

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(httpRequest, response, cancellationToken);

        var envelope = await response.Content.ReadFromJsonAsync<HelixChatMessageSendResponse>(JsonOptions, cancellationToken);
        var item = envelope?.Data?.FirstOrDefault();

        if (item is { IsSent: false })
        {
            var code = item.DropReason?.Code ?? "unknown";
            var reason = item.DropReason?.Message ?? string.Empty;
            throw new HelixMessageDroppedException(code, reason);
        }

        return item?.MessageId;
    }

    public async Task SendWhisperAsync(string fromUserId, string toUserId, string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var query = $"from_user_id={Uri.EscapeDataString(fromUserId)}&to_user_id={Uri.EscapeDataString(toUserId)}";

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{TwitchEndpoints.HelixWhispers}?{query}")
        {
            Content = JsonContent.Create(new HelixSendWhisperDto(message), options: JsonOptions),
        };

        httpRequest.Options.Set(TwitchAuthHandlerBase.KeepTokenOnUnauthorized, true);

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(httpRequest, response, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, GlobalBadgeInfo>> GetGlobalChatBadgesAsync(CancellationToken cancellationToken = default)
    {
        var envelope = await GetRawAsync<HelixGlobalBadgesResponse>(TwitchEndpoints.HelixChatBadgesGlobal, cancellationToken);
        return (envelope?.Data ?? [])
            .ToDictionary(b => b.SetId,
                b => new GlobalBadgeInfo(b.SetId,
                    (b.Versions ?? []).Select(v => new GlobalBadgeVersion(v.Id, v.ImageUrl1x, v.ImageUrl2x, v.ImageUrl4x,
                        v.Title, v.Description, v.ClickAction, v.ClickUrl))
                    .ToArray()));
    }

    public async Task<IReadOnlyDictionary<string, GlobalEmoteInfo>> GetGlobalChatEmotesAsync(CancellationToken cancellationToken = default)
    {
        var envelope = await GetRawAsync<HelixGlobalEmotesResponse>(TwitchEndpoints.HelixChatEmotesGlobal, cancellationToken);
        return (envelope?.Data ?? [])
            .ToDictionary(e => e.Id,
                e => new GlobalEmoteInfo(e.Id,
                    e.Name,
                    new(e.Images?.Url1x ?? string.Empty,
                        e.Images?.Url2x ?? string.Empty,
                        e.Images?.Url4x ?? string.Empty),
                    e.Formats ?? [],
                    e.Scales ?? [],
                    e.ThemeModes ?? []));
    }

    public async Task<string> CreateEventSubSubscriptionAsync(
        string type,
        string version,
        IReadOnlyDictionary<string, string> condition,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var body = new HelixEventSubSubscriptionRequest(type,
            version,
            condition,
            new("websocket", sessionId));

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, TwitchEndpoints.HelixEventSubSubscriptions)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(httpRequest, response, cancellationToken);

        var envelope = await response.Content.ReadFromJsonAsync<HelixEnvelope<HelixEventSubSubscriptionDto>>(JsonOptions, cancellationToken);
        var subscription = envelope?.Data?.FirstOrDefault();
        if (subscription is null)
        {
            throw new InvalidOperationException("EventSub подписка создана, но Twitch не вернул данные подписки.");
        }

        logger.LogInformation("Создана EventSub подписка {Id} типа {Type} (status={Status})",
            subscription.Id, subscription.Type, subscription.Status);

        return subscription.Id;
    }

    public async Task<bool> DeleteEventSubSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete,
            $"{TwitchEndpoints.HelixEventSubSubscriptions}?id={Uri.EscapeDataString(subscriptionId)}");

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccessAsync(httpRequest, response, cancellationToken);

        logger.LogInformation("Удалена EventSub подписка {Id}", subscriptionId);

        return true;
    }

    public async Task<IReadOnlyList<EventSubSubscriptionInfo>> GetEventSubSubscriptionsByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var result = new List<EventSubSubscriptionInfo>();
        string? cursor = null;

        // TODO: потолок в MaxEventSubSubscriptionPages страниц по 100 подписок с одним пользователем в условии; упрётся, если на одного пользователя у клиента накопится больше тысячи подписок – тогда листать без потолка с защитой от повторяющегося курсора
        for (var page = 0; page < MaxEventSubSubscriptionPages; page++)
        {
            var uri = $"{TwitchEndpoints.HelixEventSubSubscriptions}?user_id={Uri.EscapeDataString(userId)}";

            if (cursor is not null)
            {
                uri += $"&after={Uri.EscapeDataString(cursor)}";
            }

            var envelope = await GetRawAsync<HelixEnvelope<HelixEventSubSubscriptionListItemDto>>(uri, cancellationToken);

            foreach (var dto in envelope?.Data ?? [])
            {
                result.Add(new(dto.Id,
                    dto.Type,
                    dto.Status,
                    dto.Transport?.SessionId,
                    dto.Condition ?? new Dictionary<string, string>()));
            }

            cursor = envelope?.Pagination?.Cursor;

            if (string.IsNullOrEmpty(cursor))
            {
                break;
            }
        }

        return result;
    }

    public async Task<HelixPollInfo> CreatePollAsync(CreatePollRequest request, CancellationToken cancellationToken = default)
    {
        var body = new HelixCreatePollDto(request.BroadcasterId,
            request.Title,
            request.Choices.Select(c => new HelixCreatePollChoiceDto(c)).ToArray(),
            request.DurationSeconds,
            request.ChannelPointsVotingEnabled,
            request.ChannelPointsVotingEnabled ? request.ChannelPointsPerVote : 0);

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, TwitchEndpoints.HelixPolls)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(httpRequest, response, cancellationToken);

        var envelope = await response.Content.ReadFromJsonAsync<HelixEnvelope<HelixPollDto>>(JsonOptions, cancellationToken);
        var poll = envelope?.Data?.FirstOrDefault()
                   ?? throw new InvalidOperationException("Helix вернул пустой ответ на создание голосования.");

        return MapPoll(poll);
    }

    public async Task<HelixPollInfo> EndPollAsync(EndPollRequest request, CancellationToken cancellationToken = default)
    {
        var body = new HelixEndPollDto(request.BroadcasterId,
            request.PollId,
            request.ShowResult ? "TERMINATED" : "ARCHIVED");

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, TwitchEndpoints.HelixPolls)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(httpRequest, response, cancellationToken);

        var envelope = await response.Content.ReadFromJsonAsync<HelixEnvelope<HelixPollDto>>(JsonOptions, cancellationToken);
        var poll = envelope?.Data?.FirstOrDefault()
                   ?? throw new InvalidOperationException("Helix вернул пустой ответ на завершение голосования.");

        return MapPoll(poll);
    }

    public async Task<IReadOnlyList<HelixPollInfo>> GetPollsAsync(
        string broadcasterId,
        int first = 20,
        CancellationToken cancellationToken = default)
    {
        var uri = $"{TwitchEndpoints.HelixPolls}?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&first={first}";

        var data = await GetCollectionAsync<HelixPollDto>(uri, cancellationToken);
        return data.Select(MapPoll).ToArray();
    }

    private static HelixPollInfo MapPoll(HelixPollDto dto)
    {
        var choices = (dto.Choices ?? [])
            .Select(c => new HelixPollChoice(c.Id, c.Title, c.Votes, c.ChannelPointsVotes, c.BitsVotes))
            .ToArray();

        return new(dto.Id,
            dto.BroadcasterId,
            dto.Title,
            choices,
            dto.ChannelPointsVotingEnabled,
            dto.ChannelPointsPerVote,
            dto.Status,
            dto.Duration,
            dto.StartedAt,
            dto.EndedAt);
    }

    private static UserInfo MapUser(HelixUserDto dto)
    {
        return new(dto.Id, dto.Login, dto.DisplayName, dto.Type, dto.BroadcasterType,
            dto.Description, dto.ProfileImageUrl, dto.OfflineImageUrl, dto.CreatedAt);
    }

    private static HelixStreamInfo MapStream(HelixStreamDto dto)
    {
        return new(dto.Id, dto.UserId, dto.UserLogin, dto.UserName, dto.GameId, dto.GameName,
            dto.Type, dto.Title, dto.ViewerCount, dto.StartedAt, dto.Language, dto.ThumbnailUrl,
            dto.Tags ?? [], dto.IsMature);
    }

    private static ChannelInfo MapChannel(HelixChannelDto dto)
    {
        return new(dto.BroadcasterId, dto.BroadcasterLogin, dto.BroadcasterName, dto.BroadcasterLanguage,
            dto.GameId, dto.GameName, dto.Title, dto.Delay, dto.Tags ?? [],
            dto.ContentClassificationLabels ?? [], dto.IsBrandedContent);
    }

    private async Task EnsureSuccessAsync(
        HttpRequestMessage request,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        logger.LogWarning("Helix {Method} {Path} вернул {Status}",
            request.Method,
            request.RequestUri?.AbsolutePath ?? string.Empty,
            (int)response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        string? twitchErrorMessage = null;
        try
        {
            var error = JsonSerializer.Deserialize<HelixErrorResponse>(body, JsonOptions);
            twitchErrorMessage = error?.Message;
        }
        catch (JsonException)
        {
            // Twitch occasionally returns non-JSON error bodies – fall back to raw body
        }

        throw new HelixRequestException(request.Method,
            request.RequestUri?.AbsolutePath ?? string.Empty,
            response.StatusCode,
            twitchErrorMessage,
            body);
    }

    private async Task<T?> GetRawAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(httpRequest, response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task<T?> GetSingleAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        var data = await GetCollectionAsync<T>(requestUri, cancellationToken);
        return data.FirstOrDefault();
    }

    private async Task<IReadOnlyList<T>> GetCollectionAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(httpRequest, response, cancellationToken);

        var envelope = await response.Content.ReadFromJsonAsync<HelixEnvelope<T>>(JsonOptions, cancellationToken);
        return envelope?.Data ?? [];
    }

    private sealed record HelixErrorResponse(
        [property: JsonPropertyName("error")]
        string? Error,
        [property: JsonPropertyName("status")]
        int Status,
        [property: JsonPropertyName("message")]
        string? Message);
}
