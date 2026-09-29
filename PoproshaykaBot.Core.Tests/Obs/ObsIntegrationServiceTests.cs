using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Obs;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Obs;

[TestFixture]
public sealed class ObsIntegrationServiceTests
{
    [SetUp]
    public void SetUp()
    {
        _settings = new()
        {
            Twitch =
            {
                HttpServerPort = 8099,
            },
        };

        _settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        _settingsManager.Current.Returns(_settings);
        _client = new();
        _service = new(_client, _settingsManager, NullLogger<ObsIntegrationService>.Instance);
    }

    private AppSettings _settings = null!;
    private SettingsManager _settingsManager = null!;
    private FakeObsWebSocketClient _client = null!;
    private ObsIntegrationService _service = null!;

    [Test]
    public async Task ProvisionBrowserSourceAsyncCreatesBrowserSourceWithOverlayUrlWhenSourceMissingAsync()
    {
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0","obsWebSocketVersion":"5.5.2"}""");
        _client.EnqueueResponse("GetInputKindList", """{"inputKinds":["browser_source"]}""");
        _client.EnqueueResponse("GetSceneList", """{"currentProgramSceneName":"Main","scenes":[{"sceneName":"Main"}]}""");
        _client.EnqueueResponse("GetInputList", """{"inputs":[]}""");

        var result = await _service.ProvisionBrowserSourceAsync(new()
        {
            SourceName = "PoproshaykaBot Chat",
            Width = 1280,
            Height = 720,
        }, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Created, Is.True);
            Assert.That(result.Url, Is.EqualTo("http://localhost:8099/chat"));
        }

        var createRequest = _client.Requests.Single(r =>
            string.Equals(r.RequestType, "CreateInput", StringComparison.Ordinal));

        var data = createRequest.RequestData!.Value;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data.GetProperty("sceneName").GetString(), Is.EqualTo("Main"));
            Assert.That(data.GetProperty("inputName").GetString(), Is.EqualTo("PoproshaykaBot Chat"));
            Assert.That(data.GetProperty("inputKind").GetString(), Is.EqualTo("browser_source"));
            Assert.That(data.GetProperty("inputSettings").GetProperty("url").GetString(), Is.EqualTo("http://localhost:8099/chat"));
            Assert.That(data.GetProperty("inputSettings").GetProperty("width").GetInt32(), Is.EqualTo(1280));
            Assert.That(data.GetProperty("inputSettings").GetProperty("height").GetInt32(), Is.EqualTo(720));
        }
    }

    [Test]
    public async Task ProvisionBrowserSourceAsyncUpdatesInputSettingsWhenBrowserSourceExistsAsync()
    {
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0","obsWebSocketVersion":"5.5.2"}""");
        _client.EnqueueResponse("GetInputKindList", """{"inputKinds":["browser_source"]}""");
        _client.EnqueueResponse("GetSceneList", """{"currentProgramSceneName":"Main","scenes":[{"sceneName":"Main"}]}""");
        _client.EnqueueResponse("GetInputList", """{"inputs":[{"inputName":"PoproshaykaBot Chat","inputKind":"browser_source"}]}""");

        var result = await _service.ProvisionBrowserSourceAsync(new()
        {
            SourceName = "PoproshaykaBot Chat",
            SceneName = "Main",
        }, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Created, Is.False);
            Assert.That(_client.Requests.Exists(r =>
                string.Equals(r.RequestType, "CreateInput", StringComparison.Ordinal)), Is.False);
        }

        var updateRequest = _client.Requests.Single(r =>
            string.Equals(r.RequestType, "SetInputSettings", StringComparison.Ordinal));

        var data = updateRequest.RequestData!.Value;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data.GetProperty("inputName").GetString(), Is.EqualTo("PoproshaykaBot Chat"));
            Assert.That(data.GetProperty("inputSettings").GetProperty("url").GetString(), Is.EqualTo("http://localhost:8099/chat"));
        }
    }

    [Test]
    public async Task GetDashboardSnapshotAsyncReturnsObsSummaryAndMicrophoneStateAsync()
    {
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0","obsWebSocketVersion":"5.5.2"}""");
        _client.EnqueueResponse("GetCurrentProgramScene", """{"currentProgramSceneName":"Main"}""");
        _client.EnqueueResponse("GetStreamStatus", """{"outputActive":true}""");
        _client.EnqueueResponse("GetRecordStatus", """{"outputActive":false}""");
        _client.EnqueueResponse("GetInputList", """
                                                {
                                                  "inputs": [
                                                    {
                                                      "inputName": "Desktop Audio",
                                                      "inputKind": "wasapi_output_capture",
                                                      "unversionedInputKind": "wasapi_output_capture"
                                                    },
                                                    {
                                                      "inputName": "Mic/Aux",
                                                      "inputKind": "wasapi_input_capture",
                                                      "unversionedInputKind": "wasapi_input_capture"
                                                    }
                                                  ]
                                                }
                                                """);

        _client.EnqueueResponse("GetInputMute", """{"inputMuted":true}""");
        _client.EnqueueResponse("GetInputVolume", """{"inputVolumeDb":-12.5,"inputVolumeMul":0.42}""");

        var snapshot = await _service.GetDashboardSnapshotAsync(new()
        {
            Enabled = true,
        }, true, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.IsConnected, Is.True);
            Assert.That(snapshot.CurrentSceneName, Is.EqualTo("Main"));
            Assert.That(snapshot.IsStreaming, Is.True);
            Assert.That(snapshot.IsRecording, Is.False);
            Assert.That(snapshot.AudioSources, Has.Count.EqualTo(1));
            Assert.That(snapshot.AudioSources[0].Name, Is.EqualTo("Mic/Aux"));
            Assert.That(snapshot.AudioSources[0].IsMuted, Is.True);
            Assert.That(snapshot.AudioSources[0].VolumeDecibels, Is.EqualTo(-12.5).Within(0.01));
        }

        var muteRequest = _client.Requests.Single(r =>
            string.Equals(r.RequestType, "GetInputMute", StringComparison.Ordinal));

        Assert.That(muteRequest.RequestData!.Value.GetProperty("inputName").GetString(), Is.EqualTo("Mic/Aux"));
    }

    [Test]
    public async Task GetDashboardSnapshotAsyncReportsStreamAndPausedRecordingTimecodesAsync()
    {
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0","obsWebSocketVersion":"5.5.2"}""");
        _client.EnqueueResponse("GetCurrentProgramScene", """{"currentProgramSceneName":"Main"}""");
        _client.EnqueueResponse("GetStreamStatus", """{"outputActive":true,"outputTimecode":"01:23:45.000"}""");
        _client.EnqueueResponse("GetRecordStatus", """{"outputActive":true,"outputPaused":true,"outputTimecode":"00:12:34.500"}""");
        _client.EnqueueResponse("GetInputList", """{"inputs":[]}""");

        var snapshot = await _service.GetDashboardSnapshotAsync(new()
        {
            Enabled = true,
        }, true, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.IsStreaming, Is.True);
            Assert.That(snapshot.StreamTimecode, Is.EqualTo("01:23:45.000"));
            Assert.That(snapshot.IsRecording, Is.True);
            Assert.That(snapshot.IsRecordingPaused, Is.True);
            Assert.That(snapshot.RecordTimecode, Is.EqualTo("00:12:34.500"));
        }
    }

    [Test]
    public async Task GetDashboardSnapshotAsyncUsesConfiguredMicrophoneInputNameAsync()
    {
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0","obsWebSocketVersion":"5.5.2"}""");
        _client.EnqueueResponse("GetCurrentProgramScene", """{"currentProgramSceneName":"Main"}""");
        _client.EnqueueResponse("GetStreamStatus", """{"outputActive":false}""");
        _client.EnqueueResponse("GetRecordStatus", """{"outputActive":false}""");
        _client.EnqueueResponse("GetInputList", """
                                                {
                                                  "inputs": [
                                                    {
                                                      "inputName": "Mic/Aux",
                                                      "inputKind": "wasapi_input_capture",
                                                      "unversionedInputKind": "wasapi_input_capture"
                                                    },
                                                    {
                                                      "inputName": "Line In",
                                                      "inputKind": "wasapi_output_capture",
                                                      "unversionedInputKind": "wasapi_output_capture"
                                                    }
                                                  ]
                                                }
                                                """);

        _client.EnqueueResponse("GetInputMute", """{"inputMuted":false}""");
        _client.EnqueueResponse("GetInputVolume", """{"inputVolumeDb":-6.0,"inputVolumeMul":0.7}""");

        var snapshot = await _service.GetDashboardSnapshotAsync(new()
        {
            Enabled = true,
            DashboardMicrophoneName = "Line In",
        }, true, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.AudioSources, Has.Count.EqualTo(1));
            Assert.That(snapshot.AudioSources[0].Name, Is.EqualTo("Line In"));
            Assert.That(snapshot.AudioSources[0].IsMuted, Is.False);
            Assert.That(snapshot.AudioSources[0].VolumeDecibels, Is.EqualTo(-6.0).Within(0.01));
        }

        var muteRequest = _client.Requests.Single(r =>
            string.Equals(r.RequestType, "GetInputMute", StringComparison.Ordinal));

        Assert.That(muteRequest.RequestData!.Value.GetProperty("inputName").GetString(), Is.EqualTo("Line In"));
    }

    [Test]
    public async Task GetDashboardSnapshotAsyncReturnsAllConfiguredAudioSourcesInOrderAsync()
    {
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0","obsWebSocketVersion":"5.5.2"}""");
        _client.EnqueueResponse("GetCurrentProgramScene", """{"currentProgramSceneName":"Main"}""");
        _client.EnqueueResponse("GetStreamStatus", """{"outputActive":false}""");
        _client.EnqueueResponse("GetRecordStatus", """{"outputActive":false}""");
        _client.EnqueueResponse("GetInputList", """
                                                {
                                                  "inputs": [
                                                    {
                                                      "inputName": "Mic/Aux",
                                                      "inputKind": "wasapi_input_capture",
                                                      "unversionedInputKind": "wasapi_input_capture"
                                                    },
                                                    {
                                                      "inputName": "Desktop Audio",
                                                      "inputKind": "wasapi_output_capture",
                                                      "unversionedInputKind": "wasapi_output_capture"
                                                    }
                                                  ]
                                                }
                                                """);

        _client.EnqueueResponse("GetInputMute", """{"inputMuted":false}""");
        _client.EnqueueResponse("GetInputVolume", """{"inputVolumeDb":-3.0,"inputVolumeMul":0.8}""");
        _client.EnqueueResponse("GetInputMute", """{"inputMuted":true}""");
        _client.EnqueueResponse("GetInputVolume", """{"inputVolumeDb":-18.0,"inputVolumeMul":0.1}""");

        var snapshot = await _service.GetDashboardSnapshotAsync(new()
        {
            Enabled = true,
            DashboardSourceNames = ["Desktop Audio", "Mic/Aux"],
        }, true, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.AudioSources, Has.Count.EqualTo(2));
            Assert.That(snapshot.AudioSources[0].Name, Is.EqualTo("Desktop Audio"));
            Assert.That(snapshot.AudioSources[0].IsMuted, Is.False);
            Assert.That(snapshot.AudioSources[0].VolumeDecibels, Is.EqualTo(-3.0).Within(0.01));
            Assert.That(snapshot.AudioSources[1].Name, Is.EqualTo("Mic/Aux"));
            Assert.That(snapshot.AudioSources[1].IsMuted, Is.True);
            Assert.That(snapshot.AudioSources[1].VolumeDecibels, Is.EqualTo(-18.0).Within(0.01));
        }

        var muteInputNames = _client.Requests
            .Where(request => string.Equals(request.RequestType, "GetInputMute", StringComparison.Ordinal))
            .Select(request => request.RequestData!.Value.GetProperty("inputName").GetString())
            .ToArray();

        Assert.That(muteInputNames, Is.EqualTo(new[] { "Desktop Audio", "Mic/Aux" }));
    }

    [Test]
    public async Task GetDashboardSnapshotAsyncDoesNotConnectWhenConnectIsNotRequestedAsync()
    {
        var snapshot = await _service.GetDashboardSnapshotAsync(new()
        {
            Enabled = true,
        }, false, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.IsConnected, Is.False);
            Assert.That(_client.ConnectCount, Is.Zero);
            Assert.That(_client.Requests, Is.Empty);
        }
    }

    [Test]
    public async Task RefreshConfiguredChatSourcesAsyncSendsRefreshForEachConfiguredSourceAsync()
    {
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0","obsWebSocketVersion":"5.5.2"}""");

        var refreshed = await _service.RefreshConfiguredChatSourcesAsync(new()
        {
            Enabled = true,
            ChatRefreshSources = ["Chat Overlay", "Secondary Chat"],
        }, CancellationToken.None);

        Assert.That(refreshed, Is.EqualTo(2));

        var refreshRequests = _client.Requests
            .Where(request => string.Equals(request.RequestType, "PressInputPropertiesButton", StringComparison.Ordinal))
            .Select(request => request.RequestData!.Value.GetProperty("inputName").GetString())
            .ToArray();

        Assert.That(refreshRequests, Is.EqualTo(["Chat Overlay", "Secondary Chat"]));

        var firstButton = _client.Requests
            .First(request => string.Equals(request.RequestType, "PressInputPropertiesButton", StringComparison.Ordinal))
            .RequestData!.Value.GetProperty("propertyName")
            .GetString();

        Assert.That(firstButton, Is.EqualTo("refreshnocache"));
    }

    [Test]
    public async Task RefreshConfiguredChatSourcesAsyncFallsBackToSourceNameWhenListEmptyAsync()
    {
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0","obsWebSocketVersion":"5.5.2"}""");

        var refreshed = await _service.RefreshConfiguredChatSourcesAsync(new()
        {
            Enabled = true,
            SourceName = "PoproshaykaBot Chat",
        }, CancellationToken.None);

        Assert.That(refreshed, Is.EqualTo(1));

        var refreshRequest = _client.Requests.Single(request =>
            string.Equals(request.RequestType, "PressInputPropertiesButton", StringComparison.Ordinal));

        Assert.That(refreshRequest.RequestData!.Value.GetProperty("inputName").GetString(),
            Is.EqualTo("PoproshaykaBot Chat"));
    }

    [Test]
    public async Task RefreshConfiguredChatSourcesAsyncReturnsZeroWhenNoSourcesAndNoFallbackAsync()
    {
        var refreshed = await _service.RefreshConfiguredChatSourcesAsync(new()
        {
            Enabled = true,
            SourceName = string.Empty,
        }, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refreshed, Is.Zero);
            Assert.That(_client.ConnectCount, Is.Zero);
            Assert.That(_client.Requests, Is.Empty);
        }
    }

    [Test]
    public async Task ListBrowserSourceNamesAsyncReturnsOnlyBrowserSourceInputsAsync()
    {
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0","obsWebSocketVersion":"5.5.2"}""");
        _client.EnqueueResponse("GetInputList", """
                                                {
                                                  "inputs": [
                                                    {"inputName": "Mic/Aux", "inputKind": "wasapi_input_capture", "unversionedInputKind": "wasapi_input_capture"},
                                                    {"inputName": "Chat Overlay", "inputKind": "browser_source", "unversionedInputKind": "browser_source"},
                                                    {"inputName": "Donations", "inputKind": "browser_source_v2", "unversionedInputKind": "browser_source"}
                                                  ]
                                                }
                                                """);

        var names = await _service.ListBrowserSourceNamesAsync(new()
        {
            Enabled = true,
        }, CancellationToken.None);

        Assert.That(names, Is.EqualTo(["Chat Overlay", "Donations"]));
    }

    [Test]
    public async Task ListInputNamesAsyncReturnsInputNamesFromObsAsync()
    {
        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0","obsWebSocketVersion":"5.5.2"}""");
        _client.EnqueueResponse("GetInputList", """
                                                {
                                                  "inputs": [
                                                    {"inputName": "Mic/Aux", "inputKind": "wasapi_input_capture"},
                                                    {"inputName": "Desktop Audio", "inputKind": "wasapi_output_capture"}
                                                  ]
                                                }
                                                """);

        var inputNames = await _service.ListInputNamesAsync(new()
        {
            Enabled = true,
        }, CancellationToken.None);

        Assert.That(inputNames, Is.EqualTo(new[] { "Desktop Audio", "Mic/Aux" }));
    }

    [Test]
    public async Task Подключение_без_ответа_OBS_не_ставит_время_последнего_обмена()
    {
        var obsSettings = new ObsIntegrationSettings
        {
            Enabled = true,
        };

        await _service.ConnectAsync(obsSettings, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_service.CurrentStatus.IsConnected, Is.True);
            Assert.That(_service.LastExchangeAt, Is.Null);
        }

        _client.EnqueueResponse("GetVersion", """{"obsVersion":"31.0.0","obsWebSocketVersion":"5.5.2"}""");

        await _service.ConnectAsync(obsSettings, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_service.LastExchangeAt, Is.Not.Null);
            Assert.That(_service.LastExchangeAt, Is.EqualTo(_client.LastExchangeAt));
        }
    }

    private sealed class FakeObsWebSocketClient : IObsWebSocketClient
    {
        private readonly Dictionary<string, Queue<JsonElement?>> _responses = new(StringComparer.Ordinal);

        public event EventHandler<ObsWebSocketEventArgs>? EventReceived
        {
            add { }
            remove { }
        }

        public bool IsConnected { get; private set; }

        public DateTimeOffset? LastExchangeAt { get; private set; }

        public int ConnectCount { get; private set; }

        public List<RequestRecord> Requests { get; } = [];

        public Task<ObsConnectionSnapshot> ConnectAsync(ObsConnectionOptions options, CancellationToken cancellationToken)
        {
            ConnectCount++;
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
            Requests.Add(new(requestType, requestData is null ? null : JsonSerializer.SerializeToElement(requestData)));

            if (_responses.TryGetValue(requestType, out var queue) && queue.Count > 0)
            {
                LastExchangeAt = DateTimeOffset.UtcNow;
                return Task.FromResult(queue.Dequeue());
            }

            return Task.FromResult<JsonElement?>(null);
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

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed record RequestRecord(string RequestType, JsonElement? RequestData);
}
