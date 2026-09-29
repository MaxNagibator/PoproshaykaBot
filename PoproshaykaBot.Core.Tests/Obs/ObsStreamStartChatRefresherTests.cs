using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Obs;

[TestFixture]
public sealed class ObsStreamStartChatRefresherTests
{
    [SetUp]
    public void SetUp()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"obs-integration-refresher-{Guid.NewGuid():N}.json");
        _eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        _store = new(_eventBus, null, _tempFile);

        _client = new();
        _settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        _settingsManager.Current.Returns(new AppSettings
        {
            Twitch =
            {
                HttpServerPort = 8099,
            },
        });

        _integration = new(_client, _settingsManager, NullLogger<ObsIntegrationService>.Instance);
        _refresher = new(_client, _store, _integration, NullLogger<ObsStreamStartChatRefresher>.Instance);
    }

    [TearDown]
    public async Task TearDownAsync()
    {
        _integration.Dispose();
        await _client.DisposeAsync();
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    private string _tempFile = null!;
    private InMemoryEventBus _eventBus = null!;
    private ObsIntegrationStore _store = null!;
    private FakeObsWebSocketClient _client = null!;
    private SettingsManager _settingsManager = null!;
    private ObsIntegrationService _integration = null!;
    private ObsStreamStartChatRefresher _refresher = null!;

    [Test]
    public async Task DoesNotRefreshWhenSettingDisabledAsync()
    {
        SaveSettings(true, false, "Chat Overlay");
        _client.MarkConnected();

        await _refresher.StartAsync(CancellationToken.None);
        _client.RaiseStreamStateChanged(active: true);
        await _refresher.StopAsync(CancellationToken.None);

        Assert.That(_client.PressButtonRequests, Is.Empty);
    }

    [Test]
    public async Task DoesNotRefreshWhenIntegrationDisabledAsync()
    {
        SaveSettings(false, true, "Chat Overlay");
        _client.MarkConnected();

        await _refresher.StartAsync(CancellationToken.None);
        _client.RaiseStreamStateChanged(active: true);
        await _refresher.StopAsync(CancellationToken.None);

        Assert.That(_client.PressButtonRequests, Is.Empty);
    }

    [Test]
    public async Task RefreshesEachConfiguredSourceOnStreamStartedAsync()
    {
        SaveSettings(true, true, "Chat Overlay", "Secondary Chat");
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0"}""");
        _client.MarkConnected();

        await _refresher.StartAsync(CancellationToken.None);
        _client.RaiseStreamStateChanged(active: true);
        await WaitForRefreshAsync(expectedRequests: 2);
        await _refresher.StopAsync(CancellationToken.None);

        Assert.That(_client.PressButtonRequests, Is.EqualTo(new[] { "Chat Overlay", "Secondary Chat" }));
    }

    [Test]
    public async Task DoesNotRefreshOnRepeatedActiveEventsAsync()
    {
        SaveSettings(true, true, "Chat Overlay");
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0"}""");
        _client.MarkConnected();

        await _refresher.StartAsync(CancellationToken.None);
        _client.RaiseStreamStateChanged(active: true);
        await WaitForRefreshAsync(expectedRequests: 1);
        _client.RaiseStreamStateChanged(active: true);
        _client.RaiseStreamStateChanged(active: true);
        await _refresher.StopAsync(CancellationToken.None);

        Assert.That(_client.PressButtonRequests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RefreshesAgainAfterStreamStoppedAndStartedAsync()
    {
        SaveSettings(true, true, "Chat Overlay");
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0"}""");
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0"}""");
        _client.MarkConnected();

        await _refresher.StartAsync(CancellationToken.None);
        _client.RaiseStreamStateChanged(active: true);
        await WaitForRefreshAsync(expectedRequests: 1);
        _client.RaiseStreamStateChanged(active: false);
        _client.RaiseStreamStateChanged(active: true);
        await WaitForRefreshAsync(expectedRequests: 2);
        await _refresher.StopAsync(CancellationToken.None);

        Assert.That(_client.PressButtonRequests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task DoesNotRefreshOnStopEventAsync()
    {
        SaveSettings(true, true, "Chat Overlay");
        _client.MarkConnected();

        await _refresher.StartAsync(CancellationToken.None);
        _client.RaiseStreamStateChanged(active: false);
        await _refresher.StopAsync(CancellationToken.None);

        Assert.That(_client.PressButtonRequests, Is.Empty);
    }

    private void SaveSettings(bool enabled, bool autoRefresh, params string[] sources)
    {
        _store.Save(new()
        {
            Enabled = enabled,
            RefreshChatSourcesOnStreamStart = autoRefresh,
            ChatRefreshSources = [.. sources],
        });
    }

    private async Task WaitForRefreshAsync(int expectedRequests)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (_client.PressButtonRequests.Count < expectedRequests && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    private sealed class FakeObsWebSocketClient : IObsWebSocketClient
    {
        private readonly Dictionary<string, Queue<JsonElement?>> _responses = new(StringComparer.Ordinal);
        private readonly List<string> _pressButtonRequests = [];
        private readonly object _lock = new();

        public event EventHandler<ObsWebSocketEventArgs>? EventReceived;

        public bool IsConnected { get; private set; }

        public DateTimeOffset? LastExchangeAt { get; private set; }

        public IReadOnlyList<string> PressButtonRequests
        {
            get
            {
                lock (_lock)
                {
                    return [.. _pressButtonRequests];
                }
            }
        }

        public void MarkConnected()
        {
            IsConnected = true;
        }

        public void RaiseStreamStateChanged(bool active)
        {
            var json = JsonSerializer.SerializeToElement(new { outputActive = active });
            EventReceived?.Invoke(this, new("StreamStateChanged", 0, json));
        }

        public void EnqueueResponse(string requestType, string json)
        {
            if (!_responses.TryGetValue(requestType, out var queue))
            {
                queue = new();
                _responses[requestType] = queue;
            }

            using var document = JsonDocument.Parse(json);
            queue.Enqueue(document.RootElement.Clone());
        }

        public Task<ObsConnectionSnapshot> ConnectAsync(ObsConnectionOptions options, CancellationToken cancellationToken)
        {
            IsConnected = true;
            return Task.FromResult(new ObsConnectionSnapshot(true, "31.0.0", "5.5.2", null));
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task<JsonElement?> SendRequestAsync(string requestType, object? requestData, CancellationToken cancellationToken)
        {
            if (string.Equals(requestType, "PressInputPropertiesButton", StringComparison.Ordinal) && requestData is not null)
            {
                var data = JsonSerializer.SerializeToElement(requestData);
                if (data.TryGetProperty("inputName", out var inputName) && inputName.ValueKind == JsonValueKind.String)
                {
                    lock (_lock)
                    {
                        _pressButtonRequests.Add(inputName.GetString()!);
                    }
                }
            }

            if (_responses.TryGetValue(requestType, out var queue) && queue.Count > 0)
            {
                LastExchangeAt = DateTimeOffset.UtcNow;
                return Task.FromResult(queue.Dequeue());
            }

            return Task.FromResult<JsonElement?>(null);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
