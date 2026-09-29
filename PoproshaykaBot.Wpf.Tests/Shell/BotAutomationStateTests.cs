using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Wpf.Mcp;
using System.IO;

namespace PoproshaykaBot.Wpf.Tests.Shell;

[TestFixture]
public sealed class BotAutomationStateTests
{
    private static readonly DateTime StartedAt = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void GetState_ForeignDebugSession_ProjectsChannelAndPhase()
    {
        var connection = new FakeBotConnectionController
        {
            CurrentPhase = BotLifecyclePhase.Connecting,
            IsBusy = true,
        };

        var target = new TargetChannelState("mrbeast", "poproshayka", true, false, true);
        var automation = CreateAutomation(connection: connection, target: target);

        var state = automation.GetState();

        Assert.Multiple(() =>
        {
            Assert.That(state.Phase, Is.EqualTo(BotLifecyclePhase.Connecting));
            Assert.That(state.IsBusy, Is.True);
            Assert.That(state.TargetChannel.Login, Is.EqualTo("mrbeast"));
            Assert.That(state.TargetChannel.OwnChannel, Is.EqualTo("poproshayka"));
            Assert.That(state.TargetChannel.IsDebugSession, Is.True);
            Assert.That(state.TargetChannel.IsForeign, Is.True);
            Assert.That(state.TargetChannel.IsSendingAllowed, Is.False);
            Assert.That(state.TargetChannel.IsProfileIsolated, Is.True);
        });
    }

    [TestCase(StreamStatus.Online, true, TestName = "GetState_OnlineStream_KeepsStartedAt")]
    [TestCase(StreamStatus.Offline, false, TestName = "GetState_OfflineStreamWithStaleSnapshot_DropsStartedAt")]
    [TestCase(StreamStatus.Unknown, false, TestName = "GetState_UnknownStatusWithSnapshot_DropsStartedAt")]
    public void GetState_StreamSnapshot_ProjectsStartedAtOnlyWhenOnline(StreamStatus status, bool expectsStartedAt)
    {
        var stream = new StreamInfo
        {
            Title = "Стрим",
            GameName = "Just Chatting",
            ViewerCount = 42,
            StartedAt = StartedAt,
        };

        var automation = CreateAutomation(streamStatus: new FakeStreamStatus(status, stream));

        var state = automation.GetState();

        Assert.Multiple(() =>
        {
            Assert.That(state.Stream.Status, Is.EqualTo(status));
            Assert.That(state.Stream.Title, Is.EqualTo("Стрим"));
            Assert.That(state.Stream.GameName, Is.EqualTo("Just Chatting"));
            Assert.That(state.Stream.ViewerCount, Is.EqualTo(42));
            Assert.That(state.Stream.StartedAt, expectsStartedAt ? Is.EqualTo(StartedAt) : Is.Null);
        });
    }

    [Test]
    public void GetState_NoStreamSnapshot_LeavesStreamFieldsEmpty()
    {
        var automation = CreateAutomation(streamStatus: new FakeStreamStatus(StreamStatus.Offline, null));

        var state = automation.GetState();

        Assert.Multiple(() =>
        {
            Assert.That(state.Stream.Title, Is.Null);
            Assert.That(state.Stream.GameName, Is.Null);
            Assert.That(state.Stream.ViewerCount, Is.Null);
            Assert.That(state.Stream.StartedAt, Is.Null);
        });
    }

    [TestCase(true, 9000, 9000, "http://localhost:9000/chat", TestName = "GetState_ServerRunning_ReportsChatUrl")]
    [TestCase(false, 9000, 9000, null, TestName = "GetState_ServerStoppedWithKnownPort_KeepsPortWithoutUrl")]
    [TestCase(false, null, 8081, null, TestName = "GetState_ServerNeverStarted_FallsBackToSettingsPort")]
    public void GetState_HttpServer_ProjectsPortAndChatUrl(bool isRunning, int? serverPort, int expectedPort, string? expectedChatUrl)
    {
        var automation = CreateAutomation(httpServer: new FakeHttpServerStatus(isRunning, serverPort), settingsPort: 8081);

        var state = automation.GetState();

        Assert.Multiple(() =>
        {
            Assert.That(state.HttpServer.IsRunning, Is.EqualTo(isRunning));
            Assert.That(state.HttpServer.Port, Is.EqualTo(expectedPort));
            Assert.That(state.HttpServer.ChatUrl, Is.EqualTo(expectedChatUrl));
        });
    }

    [Test]
    public void GetState_ObsDisconnectedWithError_CarriesErrorMessage()
    {
        var obs = new FakeObsSceneController(ObsConnectionSnapshot.Disconnected("Соединение отклонено"));
        var automation = CreateAutomation(obs: obs);

        var state = automation.GetState();

        Assert.Multiple(() =>
        {
            Assert.That(state.Obs.IsConnected, Is.False);
            Assert.That(state.Obs.Version, Is.Null);
            Assert.That(state.Obs.LastError, Is.EqualTo("Соединение отклонено"));
        });
    }

    [Test]
    public void GetState_ObsConnected_CarriesVersion()
    {
        var obs = new FakeObsSceneController(new(true, "30.1.2", "5.4.2", null));
        var automation = CreateAutomation(obs: obs);

        var state = automation.GetState();

        Assert.Multiple(() =>
        {
            Assert.That(state.Obs.IsConnected, Is.True);
            Assert.That(state.Obs.Version, Is.EqualTo("30.1.2"));
            Assert.That(state.Obs.LastError, Is.Null);
        });
    }

    [TestCase(BotLifecyclePhase.Connected, 1, BotLifecyclePhase.Disconnected, TestName = "DisconnectAsync_ConnectFinishedBeforeCancel_StopsBot")]
    [TestCase(BotLifecyclePhase.Cancelled, 0, BotLifecyclePhase.Cancelled, TestName = "DisconnectAsync_ConnectCancelled_DoesNotStop")]
    [TestCase(BotLifecyclePhase.Failed, 0, BotLifecyclePhase.Failed, TestName = "DisconnectAsync_ConnectFailed_DoesNotStop")]
    public async Task DisconnectAsync_WhileConnecting_RereadsPhaseAfterWait(BotLifecyclePhase phaseAfterWait, int expectedStops, BotLifecyclePhase expectedPhase)
    {
        var connection = new FakeBotConnectionController
        {
            CurrentPhase = BotLifecyclePhase.Connecting,
            IsBusy = true,
            PhaseAfterWait = phaseAfterWait,
        };

        var automation = CreateAutomation(connection: connection);

        var phase = await automation.DisconnectAsync();

        Assert.Multiple(() =>
        {
            Assert.That(connection.CancelRequested, Is.True);
            Assert.That(connection.StopCalls, Is.EqualTo(expectedStops));
            Assert.That(phase, Is.EqualTo(expectedPhase));
        });
    }

    [TestCase(BotLifecyclePhase.Idle)]
    [TestCase(BotLifecyclePhase.Disconnected)]
    [TestCase(BotLifecyclePhase.Disconnecting)]
    public void DisconnectAsync_NothingToDisconnect_Throws(BotLifecyclePhase current)
    {
        var connection = new FakeBotConnectionController { CurrentPhase = current };
        var automation = CreateAutomation(connection: connection);

        Assert.ThrowsAsync<InvalidOperationException>(() => automation.DisconnectAsync());
        Assert.That(connection.StopCalls, Is.Zero);
    }

    private static BotAutomation CreateAutomation(
        FakeBotConnectionController? connection = null,
        TargetChannelState? target = null,
        IStreamStatus? streamStatus = null,
        IHttpServerStatus? httpServer = null,
        IObsSceneController? obs = null,
        int settingsPort = 8080)
    {
        return new(
            connection ?? new FakeBotConnectionController(),
            new FakeTargetChannelProvider(target ?? new("poproshayka", "poproshayka", false, true)),
            streamStatus ?? new FakeStreamStatus(StreamStatus.Offline, null),
            httpServer ?? new FakeHttpServerStatus(false, null),
            obs ?? new FakeObsSceneController(ObsConnectionSnapshot.Disconnected()),
            new FakeSettingsManager(settingsPort),
            NullLogger<BotAutomation>.Instance);
    }

    private sealed class FakeBotConnectionController : IBotConnectionController
    {
        public bool IsBusy { get; init; }

        public BotLifecyclePhase CurrentPhase { get; set; } = BotLifecyclePhase.Idle;

        public BotLifecyclePhase? PhaseAfterWait { get; init; }

        public bool CancelRequested { get; private set; }

        public int StopCalls { get; private set; }

        public void StartConnection()
        {
        }

        public void CancelConnection()
        {
            CancelRequested = true;
        }

        public Task WaitForConnectionAsync()
        {
            if (PhaseAfterWait is { } phase)
            {
                CurrentPhase = phase;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(BotStopMode mode)
        {
            StopCalls++;
            CurrentPhase = BotLifecyclePhase.Disconnected;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTargetChannelProvider(TargetChannelState current) : ITargetChannelProvider
    {
        public TargetChannelState Current { get; } = current;

        public void BeginSession()
        {
        }

        public void EndSession()
        {
        }
    }

    private sealed class FakeStreamStatus(StreamStatus status, StreamInfo? stream) : IStreamStatus
    {
        public StreamStatus CurrentStatus { get; } = status;

        public StreamInfo? CurrentStream { get; } = stream;

        public Task RefreshLiveSnapshotAsync() => Task.CompletedTask;
    }

    private sealed class FakeHttpServerStatus(bool isRunning, int? port) : IHttpServerStatus
    {
        public bool IsRunning { get; } = isRunning;

        public int? Port { get; } = port;
    }

    private sealed class FakeObsSceneController(ObsConnectionSnapshot status) : IObsSceneController
    {
        public bool IsConnected => CurrentStatus.IsConnected;

        public ObsConnectionSnapshot CurrentStatus { get; } = status;

        public Task<string?> GetCurrentSceneAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task SetCurrentSceneAsync(string sceneName, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSettingsManager : SettingsManager
    {
        private readonly AppSettings _settings;

        public FakeSettingsManager(int httpServerPort)
            : base(NullLogger<SettingsManager>.Instance, Path.Combine(Path.GetTempPath(), "poproshayka-tests", "settings.json"))
        {
            _settings = new();
            _settings.Twitch.HttpServerPort = httpServerPort;
        }

        public override AppSettings Current => _settings;
    }
}
