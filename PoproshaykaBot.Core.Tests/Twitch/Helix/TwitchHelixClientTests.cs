using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Net;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Twitch.Helix;

[TestFixture]
public sealed class TwitchHelixClientTests
{
    private const string HelixBaseUrl = TwitchEndpoints.HelixBaseUrl;

    private static (BotHelixClient client, StubHttpMessageHandler handler) Build(StubHttpMessageHandler? handler = null)
    {
        var stub = handler ?? new StubHttpMessageHandler();
        var httpClient = new HttpClient(stub) { BaseAddress = new(HelixBaseUrl) };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var client = new BotHelixClient(factory, NullLogger<BotHelixClient>.Instance);
        return (client, stub);
    }

    [Test]
    public async Task GetUserByLoginAsync_BuildsCorrectUrlAndDeserializesPayload()
    {
        const string Body = """
                            {
                              "data": [{
                                "id": "42",
                                "login": "thebot",
                                "display_name": "TheBot",
                                "type": "",
                                "broadcaster_type": "",
                                "description": "тест",
                                "profile_image_url": "https://example.com/avatar.png",
                                "offline_image_url": "",
                                "created_at": "2020-01-02T03:04:05Z"
                              }]
                            }
                            """;

        var (client, handler) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.OK, Body));

        var user = await client.GetUserByLoginAsync("the bot");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(user, Is.Not.Null);
            Assert.That(user!.Id, Is.EqualTo("42"));
            Assert.That(user.Login, Is.EqualTo("thebot"));
            Assert.That(user.DisplayName, Is.EqualTo("TheBot"));
            Assert.That(user.ProfileImageUrl, Is.EqualTo("https://example.com/avatar.png"));

            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(handler.Requests[0].RequestUri!.AbsoluteUri,
                Is.EqualTo(HelixBaseUrl + "helix/users?login=the%20bot"),
                "login должен быть URL-escaped через Uri.EscapeDataString");
        }
    }

    [Test]
    public async Task GetUserByLoginAsync_ReturnsNullForBlankLogin_WithoutHttpCall()
    {
        var (client, handler) = Build();

        var user = await client.GetUserByLoginAsync("   ");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(user, Is.Null);
            Assert.That(handler.Requests, Is.Empty,
                "Пустой login не должен генерировать HTTP-вызов – это лишний удар по quota Helix");
        }
    }

    [Test]
    public async Task GetUserByLoginAsync_ReturnsNullForEmptyDataArray()
    {
        var (client, _) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.OK, """{"data":[]}"""));

        var user = await client.GetUserByLoginAsync("nobody");

        Assert.That(user, Is.Null);
    }

    [Test]
    public async Task GetUsersByIdsAsync_DeduplicatesIdsAndCapsAt100PerCall()
    {
        var ids = Enumerable.Range(1, 150)
            .Select(n => n.ToString())
            .Concat(["1", "2", " ", null!])
            .ToArray();

        var (client, handler) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.OK, """{"data":[]}"""));

        await client.GetUsersByIdsAsync(ids);

        Assert.That(handler.Requests, Has.Count.EqualTo(1));
        var requestUri = handler.Requests[0].RequestUri!.AbsoluteUri;
        var idCount = requestUri.Split('&').Count(part => part.Contains("id=", StringComparison.Ordinal));
        Assert.That(idCount, Is.EqualTo(100), "Helix принимает не более 100 id за один запрос");
    }

    [Test]
    public async Task GetUsersByIdsAsync_EmptyInput_ReturnsEmpty_WithoutHttpCall()
    {
        var (client, handler) = Build();

        var result = await client.GetUsersByIdsAsync([]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Empty);
            Assert.That(handler.Requests, Is.Empty);
        }
    }

    [Test]
    public async Task GetStreamAsync_OfflineStream_ReturnsNull()
    {
        var (client, _) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.OK, """{"data":[]}"""));

        var stream = await client.GetStreamAsync("12345");

        Assert.That(stream, Is.Null);
    }

    [Test]
    public async Task GetStreamAsync_OnlineStream_DeserializesIntoHelixStreamInfo()
    {
        const string Body = """
                            {
                              "data": [{
                                "id": "stream-1",
                                "user_id": "12345",
                                "user_login": "bobito217",
                                "user_name": "Bobito217",
                                "game_id": "509658",
                                "game_name": "Just Chatting",
                                "type": "live",
                                "title": "Поток",
                                "viewer_count": 1234,
                                "started_at": "2026-04-30T12:00:00Z",
                                "language": "ru",
                                "thumbnail_url": "https://example.com/{width}x{height}.jpg",
                                "tags": ["Russian", "Programming"],
                                "is_mature": false
                              }]
                            }
                            """;

        var (client, _) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.OK, Body));

        var stream = await client.GetStreamAsync("12345");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stream, Is.Not.Null);
            Assert.That(stream!.UserLogin, Is.EqualTo("bobito217"));
            Assert.That(stream.GameId, Is.EqualTo("509658"));
            Assert.That(stream.Title, Is.EqualTo("Поток"));
            Assert.That(stream.ViewerCount, Is.EqualTo(1234));
            Assert.That(stream.Tags, Is.EquivalentTo(new[] { "Russian", "Programming" }));
        }
    }

    [Test]
    public async Task PatchChannelAsync_SerializesBodyWithSnakeCaseProperties()
    {
        var (client, handler) = Build(StubHttpMessageHandler.ReturnsStatus(HttpStatusCode.NoContent));

        var request = new PatchChannelRequest
        {
            Title = "Новый заголовок",
            GameId = "509658",
            BroadcasterLanguage = "ru",
            Tags = ["a", "b"],
            ContentClassificationLabels =
            [
                new("DrugsIntoxication", true),
            ],
            IsBrandedContent = false,
        };

        await client.PatchChannelAsync("12345", request);

        Assert.That(handler.Requests, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Patch));
            Assert.That(handler.Requests[0].RequestUri!.AbsoluteUri,
                Does.Contain("broadcaster_id=12345"));
        }

        var body = handler.RequestBodies[0]!;
        var json = JsonDocument.Parse(body).RootElement;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(json.GetProperty("title").GetString(), Is.EqualTo("Новый заголовок"));
            Assert.That(json.GetProperty("game_id").GetString(), Is.EqualTo("509658"));
            Assert.That(json.GetProperty("broadcaster_language").GetString(), Is.EqualTo("ru"));
            Assert.That(json.GetProperty("is_branded_content").GetBoolean(), Is.False);

            var ccl = json.GetProperty("content_classification_labels");
            Assert.That(ccl[0].GetProperty("id").GetString(), Is.EqualTo("DrugsIntoxication"));
            Assert.That(ccl[0].GetProperty("is_enabled").GetBoolean(), Is.True);
        }
    }

    [Test]
    public void EnsureSuccess_NonSuccessStatus_ThrowsHelixRequestExceptionWithBodyAndStatus()
    {
        const string ErrorBody = """{"error":"Bad Request","status":400,"message":"missing user_id"}""";

        var (client, _) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.BadRequest, ErrorBody));

        var ex = Assert.ThrowsAsync<HelixRequestException>(async () => await client.GetStreamAsync("12345"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.TwitchErrorMessage, Is.EqualTo("missing user_id"),
                "TwitchErrorMessage должен парситься из стандартного error-envelope Helix");

            Assert.That(ex.ResponseBody, Is.EqualTo(ErrorBody));
        }
    }

    [Test]
    public void EnsureSuccess_NonJsonErrorBody_StillThrowsButTwitchMessageIsNull()
    {
        var (client, _) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.BadGateway, "<html>502</html>"));

        var ex = Assert.ThrowsAsync<HelixRequestException>(async () => await client.GetStreamAsync("12345"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
            Assert.That(ex.TwitchErrorMessage, Is.Null,
                "Невалидный JSON в теле – TwitchErrorMessage должен быть null, а не падать");

            Assert.That(ex.ResponseBody, Is.EqualTo("<html>502</html>"));
        }
    }

    [Test]
    public async Task SendChatMessageAsync_SuccessfulSend_DoesNotThrow()
    {
        const string Body = """
                            {"data":[{"message_id":"abc","is_sent":true,"drop_reason":null}]}
                            """;

        var (client, handler) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.OK, Body));

        await client.SendChatMessageAsync("12345", "67890", "Привет");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.Requests[0].RequestUri!.AbsoluteUri,
                Is.EqualTo(HelixBaseUrl + "helix/chat/messages"));
        }
    }

    [Test]
    public void SendChatMessageAsync_DroppedByTwitch_ThrowsHelixMessageDroppedException()
    {
        const string Body = """
                            {"data":[{"message_id":"abc","is_sent":false,"drop_reason":{"code":"msg_duplicate","message":"повторное сообщение"}}]}
                            """;

        var (client, _) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.OK, Body));

        var ex = Assert.ThrowsAsync<HelixMessageDroppedException>(async () => await client.SendChatMessageAsync("12345", "67890", "duplicate"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.ReasonCode, Is.EqualTo("msg_duplicate"));
            Assert.That(ex.ReasonMessage, Is.EqualTo("повторное сообщение"));
        }
    }

    [Test]
    public async Task CreateEventSubSubscriptionAsync_PostsCorrectRequestAndReturnsSubscriptionId()
    {
        const string Body = """
                            {
                              "data": [{
                                "id": "sub-id-123",
                                "status": "enabled",
                                "type": "stream.online",
                                "version": "1",
                                "created_at": "2026-01-01T00:00:00Z"
                              }]
                            }
                            """;

        var (client, handler) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.Accepted, Body));

        var subscriptionId = await client.CreateEventSubSubscriptionAsync("stream.online",
            "1",
            new Dictionary<string, string> { ["broadcaster_user_id"] = "12345" },
            "session-1");

        Assert.That(subscriptionId, Is.EqualTo("sub-id-123"));

        var body = handler.RequestBodies[0]!;
        var json = JsonDocument.Parse(body).RootElement;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(json.GetProperty("type").GetString(), Is.EqualTo("stream.online"));
            Assert.That(json.GetProperty("version").GetString(), Is.EqualTo("1"));
            Assert.That(json.GetProperty("condition").GetProperty("broadcaster_user_id").GetString(),
                Is.EqualTo("12345"));

            Assert.That(json.GetProperty("transport").GetProperty("method").GetString(), Is.EqualTo("websocket"));
            Assert.That(json.GetProperty("transport").GetProperty("session_id").GetString(), Is.EqualTo("session-1"));
        }
    }

    [Test]
    public void CreateEventSubSubscriptionAsync_EmptyData_ThrowsInvalidOperation()
    {
        var (client, _) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.Accepted, """{"data":[]}"""));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.CreateEventSubSubscriptionAsync("stream.online", "1",
                new Dictionary<string, string>(), "session-1"));
    }

    [TestCase(HttpStatusCode.NoContent, true)]
    [TestCase(HttpStatusCode.NotFound, false)]
    public async Task DeleteEventSubSubscriptionAsync_SendsDeleteByIdAndTreatsMissingAsNotDeleted(HttpStatusCode status, bool expected)
    {
        var (client, handler) = Build(StubHttpMessageHandler.ReturnsStatus(status));

        var deleted = await client.DeleteEventSubSubscriptionAsync("sub id/1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deleted, Is.EqualTo(expected));
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Delete));
            Assert.That(handler.Requests[0].RequestUri!.AbsoluteUri,
                Is.EqualTo(HelixBaseUrl + "helix/eventsub/subscriptions?id=sub%20id%2F1"));
        }
    }

    [Test]
    public async Task GetEventSubSubscriptionsByUserAsync_FiltersByUserIdAndFollowsCursor()
    {
        var handler = new StubHttpMessageHandler
        {
            Responder = request => new(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.Query.Contains("after=")
                        ? """{"data":[{"id":"sub-2","status":"websocket_disconnected","type":"stream.offline","condition":{"broadcaster_user_id":"42"},"transport":{"method":"websocket","session_id":"s-0"}}],"pagination":{}}"""
                        : """{"data":[{"id":"sub-1","status":"enabled","type":"stream.online","condition":{"broadcaster_user_id":"42"},"transport":{"method":"websocket","session_id":"s-1"}}],"pagination":{"cursor":"page 2"}}""",
                    System.Text.Encoding.UTF8,
                    "application/json"),
            },
        };

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient(handler, disposeHandler: false) { BaseAddress = new(HelixBaseUrl) });

        var client = new BotHelixClient(factory, NullLogger<BotHelixClient>.Instance);

        var subscriptions = await client.GetEventSubSubscriptionsByUserAsync("42");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.Requests.Select(r => r.RequestUri!.AbsoluteUri), Is.EqualTo(new[]
            {
                HelixBaseUrl + "helix/eventsub/subscriptions?user_id=42",
                HelixBaseUrl + "helix/eventsub/subscriptions?user_id=42&after=page%202",
            }));

            Assert.That(subscriptions.Select(s => (s.Id, s.Type, s.Status, s.SessionId)), Is.EqualTo(new[]
            {
                ("sub-1", "stream.online", "enabled", (string?)"s-1"),
                ("sub-2", "stream.offline", "websocket_disconnected", (string?)"s-0"),
            }));
        }
    }

    [Test]
    public void DeleteEventSubSubscriptionAsync_ServerError_ThrowsHelixRequestException()
    {
        var (client, _) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.InternalServerError,
            """{"error":"Internal Server Error","status":500,"message":"oops"}"""));

        var ex = Assert.ThrowsAsync<HelixRequestException>(async () => await client.DeleteEventSubSubscriptionAsync("sub-1"));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }

    [Test]
    public async Task CreatePollAsync_ZeroChannelPointsWhenVotingDisabled_EvenIfPerVoteSet()
    {
        const string ResponseBody = """
                                    {"data":[{
                                      "id":"poll-1",
                                      "broadcaster_id":"12345",
                                      "broadcaster_name":"Bobito217",
                                      "broadcaster_login":"bobito217",
                                      "title":"Тест?",
                                      "choices":[],
                                      "channel_points_voting_enabled":false,
                                      "channel_points_per_vote":0,
                                      "status":"ACTIVE",
                                      "duration":60,
                                      "started_at":"2026-04-30T12:00:00Z",
                                      "ended_at":null
                                    }]}
                                    """;

        var (client, handler) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.OK, ResponseBody));

        await client.CreatePollAsync(new()
        {
            BroadcasterId = "12345",
            Title = "Тест?",
            Choices = ["Да", "Нет"],
            DurationSeconds = 60,
            ChannelPointsVotingEnabled = false,
            ChannelPointsPerVote = 500,
        });

        var body = handler.RequestBodies[0]!;
        var json = JsonDocument.Parse(body).RootElement;
        Assert.That(json.GetProperty("channel_points_per_vote").GetInt32(), Is.Zero,
            "Когда голосование за channel points отключено, поле должно уйти нулём – иначе Twitch отказывает 400");
    }

    [Test]
    public async Task GetPollsAsync_NeverSendsStatusParameter()
    {
        var (client, handler) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.OK, """{"data":[]}"""));

        await client.GetPollsAsync("12345", first: 5);

        var url = handler.Requests[0].RequestUri!.AbsoluteUri;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(url, Does.Contain("broadcaster_id=12345"));
            Assert.That(url, Does.Contain("first=5"));
            Assert.That(url, Does.Not.Contain("status="),
                "Get Polls не фильтрует по статусу, параметр status слать нельзя");
        }
    }

    [Test]
    public async Task SearchCategoriesAsync_BlankQuery_ReturnsEmpty_WithoutHttpCall()
    {
        var (client, handler) = Build();

        var result = await client.SearchCategoriesAsync(" ", 10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Empty);
            Assert.That(handler.Requests, Is.Empty);
        }
    }

    [Test]
    public async Task GetGameByIdAsync_BlankId_ReturnsNull_WithoutHttpCall()
    {
        var (client, handler) = Build();

        var result = await client.GetGameByIdAsync(" ");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(handler.Requests, Is.Empty);
        }
    }

    [Test]
    public async Task GetGamesByNamesAsync_SendsOneNameParameterPerGame_AndDropsBlanks()
    {
        const string Body = """
                            {
                              "data": [
                                { "id": "509658", "name": "Just Chatting", "box_art_url": "https://cdn/{width}x{height}.jpg", "igdb_id": "" }
                              ]
                            }
                            """;

        var (client, handler) = Build(StubHttpMessageHandler.ReturnsJson(HttpStatusCode.OK, Body));

        var games = await client.GetGamesByNamesAsync(["Just Chatting", "just chatting", "  ", "Dota 2"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games, Has.Count.EqualTo(1));
            Assert.That(games[0].BoxArtUrl, Is.EqualTo("https://cdn/{width}x{height}.jpg"));
            Assert.That(handler.Requests[0].RequestUri!.AbsoluteUri,
                Is.EqualTo(HelixBaseUrl + "helix/games?name=Just%20Chatting&name=Dota%202"));
        }
    }

    [Test]
    public async Task GetGamesByNamesAsync_NoNames_ReturnsEmpty_WithoutHttpCall()
    {
        var (client, handler) = Build();

        var games = await client.GetGamesByNamesAsync(["", "   "]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games, Is.Empty);
            Assert.That(handler.Requests, Is.Empty);
        }
    }

    [Test]
    public async Task TwitchHelixClient_RequestsHttpClientUnderConfiguredName()
    {
        var stub = StubHttpMessageHandler.ReturnsJson(HttpStatusCode.OK, """{"data":[]}""");
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>())
            .Returns(_ => new(stub, false) { BaseAddress = new(HelixBaseUrl) });

        var bot = new BotHelixClient(factory, NullLogger<BotHelixClient>.Instance);
        await bot.GetUserByLoginAsync("a");

        var broadcaster = new BroadcasterHelixClient(factory, NullLogger<BroadcasterHelixClient>.Instance);
        await broadcaster.GetUserByLoginAsync("a");

        using (Assert.EnterMultipleScope())
        {
            factory.Received().CreateClient(TwitchEndpoints.HelixBotClient);
            factory.Received().CreateClient(TwitchEndpoints.HelixBroadcasterClient);
        }
    }
}
