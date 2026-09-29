using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Server.Endpoints;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using System.Net;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Server.Endpoints;

[TestFixture]
public sealed class ChatHistoryEndpointTests
{
    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "poproshayka-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _bus = new(NullLogger<InMemoryEventBus>.Instance);

        _settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        _settings = new();
        _settingsManager.Current.Returns(_settings);

        _historyManager = new(_settingsManager, _bus);
        _obsChatStore = new(_bus, NullLogger<ObsChatStore>.Instance, Path.Combine(_tempDir, "obs-chat.json"));
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    private string _tempDir = null!;
    private InMemoryEventBus _bus = null!;
    private SettingsManager _settingsManager = null!;
    private AppSettings _settings = null!;
    private ChatHistoryManager _historyManager = null!;
    private ObsChatStore _obsChatStore = null!;

    private Task<EndpointTestServer> CreateServerAsync()
    {
        return EndpointTestServer.CreateAsync(services =>
            {
                services.AddRouting();
                services.AddSingleton(_historyManager);
                services.AddSingleton(_obsChatStore);
            },
            sp => new ChatHistoryEndpoint(sp.GetRequiredService<ChatHistoryManager>(),
                sp.GetRequiredService<ObsChatStore>()));
    }

    private static ChatMessageData NewMessage(string id, DateTime timestamp, string text = "msg")
    {
        return new()
        {
            MessageId = id,
            Timestamp = timestamp,
            UserId = "u",
            DisplayName = "user",
            Message = text,
        };
    }

    [Test]
    public async Task GetHistory_EmptyHistory_ReturnsEmptyArray()
    {
        using var server = await CreateServerAsync();
        using var client = server.CreateClient();

        using var response = await client.GetAsync("/api/history");

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
            Assert.That(doc.GetArrayLength(), Is.Zero);
        }
    }

    [Test]
    public async Task GetHistory_TruncatesToMaxMessages()
    {
        _obsChatStore.Save(new()
        {
            MaxMessages = 3,
            EnableMessageFadeOut = false,
        });

        for (var i = 0; i < 10; i++)
        {
            _historyManager.AddMessage(NewMessage($"m{i}", DateTime.UtcNow.AddSeconds(-(10 - i)), $"msg{i}"));
        }

        using var server = await CreateServerAsync();
        using var client = server.CreateClient();

        using var response = await client.GetAsync("/api/history");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;

        Assert.That(doc.GetArrayLength(), Is.EqualTo(3),
            "Endpoint обязан отдавать только последние MaxMessages, иначе OBS-overlay получит лишний контент при перезагрузке");
    }

    [Test]
    public async Task GetHistory_FadeOutEnabled_FiltersOldMessages()
    {
        _obsChatStore.Save(new()
        {
            MaxMessages = 100,
            EnableMessageFadeOut = true,
            MessageLifetimeSeconds = 60,
        });

        var now = DateTime.UtcNow;
        _historyManager.AddMessage(NewMessage("old", now.AddMinutes(-5), "old"));
        _historyManager.AddMessage(NewMessage("fresh", now.AddSeconds(-10), "fresh"));

        using var server = await CreateServerAsync();
        using var client = server.CreateClient();

        using var response = await client.GetAsync("/api/history");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(doc.GetArrayLength(), Is.EqualTo(1),
                "Старые сообщения за пределами MessageLifetimeSeconds должны отфильтроваться при включённом fade-out");

            var only = doc[0];
            Assert.That(only.GetProperty("message").GetString(), Is.EqualTo("fresh"));
        }
    }
}
