using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Tests.Twitch.Helix;
using PoproshaykaBot.Core.Twitch;
using System.Net;
using System.Text;

namespace PoproshaykaBot.Core.Tests.Debugging;

[TestFixture]
public class ChannelLiveStatusReaderTests
{
    private const string AppToken = "app-token-1";

    private const string LiveResponse = """
        {"data":[{"id":"1","user_id":"10","user_login":"dunduk","user_name":"Dunduk","game_id":"509658","game_name":"Just Chatting",
        "type":"live","title":"Утренний стрим","viewer_count":1234,"started_at":"2026-09-25T08:30:00Z","language":"ru",
        "thumbnail_url":"","tags":[],"is_mature":false}],"pagination":{}}
        """;

    [Test]
    public async Task Идущий_стрим_приходит_в_эфире_с_зрителями_а_отсутствующий_в_ответе_канал_не_в_эфире()
    {
        var (reader, stub, factory) = Create(request => IsTokenRequest(request)
            ? Json(HttpStatusCode.OK, $$"""{"access_token":"{{AppToken}}","expires_in":5000000,"token_type":"bearer"}""")
            : Json(HttpStatusCode.OK, LiveResponse));

        var report = await reader.ReadAsync(["Dunduk", "@quietchannel"]);

        var streamsRequest = stub.Requests.Single(request => !IsTokenRequest(request));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.UnknownReason, Is.Null);
            Assert.That(report.Channels, Has.Count.EqualTo(2));
            Assert.That(report.Channels[0], Is.EqualTo(new ChannelLiveStatus("dunduk", ChannelLiveState.Live, 1234, "Утренний стрим", "Just Chatting",
                new DateTimeOffset(2026, 9, 25, 8, 30, 0, TimeSpan.Zero))));

            Assert.That(report.Channels[1], Is.EqualTo(new ChannelLiveStatus("quietchannel", ChannelLiveState.Offline)));
            Assert.That(streamsRequest.RequestUri!.Query, Does.Contain("user_login=dunduk&user_login=quietchannel"),
                "состояние всех каналов берётся одним запросом");

            Assert.That(streamsRequest.Headers.Authorization?.Parameter, Is.EqualTo(AppToken),
                "запрос идёт под ключом приложения, а не под сохранённым токеном бота или стримера");

            factory.DidNotReceive().CreateClient(TwitchEndpoints.HelixBotClient);
            factory.DidNotReceive().CreateClient(TwitchEndpoints.HelixBroadcasterClient);
        }
    }

    [Test]
    public async Task Ответ_401_даёт_неизвестно_и_следующий_запрос_берёт_новый_ключ_приложения()
    {
        var streamsCalls = 0;

        var (reader, stub, _) = Create(request =>
        {
            if (IsTokenRequest(request))
            {
                return Json(HttpStatusCode.OK, $$"""{"access_token":"{{AppToken}}","expires_in":5000000}""");
            }

            streamsCalls++;
            return streamsCalls == 1 ? new(HttpStatusCode.Unauthorized) : Json(HttpStatusCode.OK, """{"data":[]}""");
        });

        var rejected = await reader.ReadAsync(["dunduk"]);
        var retried = await reader.ReadAsync(["dunduk"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rejected.UnknownReason, Is.EqualTo(ChannelLiveStatusReader.TokenRejectedReason));
            Assert.That(rejected.Channels.Single().State, Is.EqualTo(ChannelLiveState.Unknown));
            Assert.That(retried.Channels.Single().State, Is.EqualTo(ChannelLiveState.Offline));
            Assert.That(stub.Requests.Count(IsTokenRequest), Is.EqualTo(2), "отклонённый ключ приложения забывается и запрашивается заново");
        }
    }

    [Test]
    public void Клиент_ключа_приложения_собран_без_обработчика_который_стирает_токен_на_401()
    {
        var services = new ServiceCollection();
        services.AddTwitchClients();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Get(TwitchEndpoints.HelixAppClient).HttpMessageHandlerBuilderActions, Is.Empty,
                "обработчик авторизации Helix на 401 очищает сохранённый токен роли – у клиента состояния каналов его быть не должно");

            Assert.That(options.Get(TwitchEndpoints.HelixBroadcasterClient).HttpMessageHandlerBuilderActions, Is.Not.Empty,
                "контрольная сторона: у клиента стримера обработчик есть, иначе проверка выше ничего не доказывает");
        }
    }

    [TestCase(HttpStatusCode.BadRequest, ChannelLiveStatusReader.CredentialsRejectedReason)]
    [TestCase(HttpStatusCode.Forbidden, ChannelLiveStatusReader.CredentialsRejectedReason)]
    [TestCase(HttpStatusCode.ServiceUnavailable, ChannelLiveStatusReader.NetworkFailedReason)]
    public async Task Отказ_выдачи_ключа_приложения_даёт_неизвестно_с_причиной(HttpStatusCode status, string reason)
    {
        var (reader, stub, _) = Create(_ => new(status));

        var report = await reader.ReadAsync(["dunduk", "other"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.UnknownReason, Is.EqualTo(reason));
            Assert.That(report.Channels.Select(channel => channel.State), Is.All.EqualTo(ChannelLiveState.Unknown));
            Assert.That(stub.Requests, Has.All.Matches<HttpRequestMessage>(IsTokenRequest), "без ключа приложения Helix не вызывается");
        }
    }

    [TestCase("""{}""", true)]
    [TestCase("""{"data":null}""", true)]
    [TestCase("""не json""", true)]
    [TestCase("""{"access_token":""}""", false)]
    public async Task Неожиданный_ответ_Twitch_даёт_неизвестно_а_не_не_в_эфире(string body, bool fromStreams)
    {
        var (reader, _, _) = Create(request => IsTokenRequest(request) && fromStreams
            ? Json(HttpStatusCode.OK, $$"""{"access_token":"{{AppToken}}","expires_in":5000000}""")
            : Json(HttpStatusCode.OK, body));

        var report = await reader.ReadAsync(["dunduk"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.UnknownReason, Is.EqualTo(ChannelLiveStatusReader.UnexpectedResponseReason));
            Assert.That(report.Channels.Single(), Is.EqualTo(new ChannelLiveStatus("dunduk", ChannelLiveState.Unknown)));
        }
    }

    [Test]
    public async Task Сетевой_отказ_даёт_неизвестно_без_исключения()
    {
        var (reader, _, _) = Create(_ => throw new HttpRequestException("Нет сети"));

        var report = await reader.ReadAsync(["dunduk"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.UnknownReason, Is.EqualTo(ChannelLiveStatusReader.NetworkFailedReason));
            Assert.That(report.Channels.Single(), Is.EqualTo(new ChannelLiveStatus("dunduk", ChannelLiveState.Unknown)));
        }
    }

    [Test]
    public async Task Без_ключей_приложения_в_сеть_не_ходит()
    {
        var (reader, stub, _) = Create(_ => Json(HttpStatusCode.OK, "{}"), clientSecret: "");

        var report = await reader.ReadAsync(["dunduk"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.UnknownReason, Is.EqualTo(ChannelLiveStatusReader.MissingCredentialsReason));
            Assert.That(stub.Requests, Is.Empty);
        }
    }

    private static bool IsTokenRequest(HttpRequestMessage request)
    {
        return request.RequestUri?.AbsoluteUri == TwitchEndpoints.OAuthToken;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        return new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    private static (ChannelLiveStatusReader Reader, StubHttpMessageHandler Stub, IHttpClientFactory Factory) Create(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        string clientSecret = "secret")
    {
        var stub = new StubHttpMessageHandler { Responder = responder };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>())
            .Returns(_ => new(stub, false) { BaseAddress = new(TwitchEndpoints.HelixBaseUrl) });

        var settings = new AppSettings
        {
            Twitch =
            {
                ClientId = "client-id",
                ClientSecret = clientSecret,
            },
        };

        var settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        settingsManager.Current.Returns(settings);

        var reader = new ChannelLiveStatusReader(factory, settingsManager, TimeProvider.System, NullLogger<ChannelLiveStatusReader>.Instance);
        return (reader, stub, factory);
    }
}
