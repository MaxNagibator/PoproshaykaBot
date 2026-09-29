using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Tests.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events.Broadcasting;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Tests.Broadcast.Profiles;

[TestFixture]
public class ChannelInformationApplierTests
{
    [SetUp]
    public void SetUp()
    {
        _channelsApi = Substitute.For<ITwitchChannelsApi>();
        _idProvider = Substitute.For<IBroadcasterIdProvider>();
        _eventBus = Substitute.For<IEventBus>();
        _confirmation = Substitute.For<IChannelUpdateConfirmation>();
        _idProvider.GetAsync(Arg.Any<CancellationToken>()).Returns("12345");
        _confirmation.AwaitAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _targetChannel = new();

        _applier = new(_channelsApi, _idProvider, _targetChannel, _confirmation, _eventBus,
            NullLogger<ChannelInformationApplier>.Instance);
    }

    private ITwitchChannelsApi _channelsApi = null!;
    private IBroadcasterIdProvider _idProvider = null!;
    private IEventBus _eventBus = null!;
    private IChannelUpdateConfirmation _confirmation = null!;
    private FakeTargetChannelProvider _targetChannel = null!;
    private ChannelInformationApplier _applier = null!;

    [Test]
    public async Task ApplyAsync_SendsFullRequest_AndPublishesApplied()
    {
        var profile = new BroadcastProfile
        {
            Name = "Just Chatting",
            Title = "Test stream",
            GameId = "509658",
            GameName = "Just Chatting",
            BroadcasterLanguage = "ru",
            Tags = ["Russian", "Chatting"],
        };

        await _applier.ApplyAsync(profile, CancellationToken.None);

        await _channelsApi.Received(1)
            .ModifyChannelInformationAsync("12345",
                Arg.Is<PatchChannelRequest>(r =>
                    r.Title == "Test stream" && r.GameId == "509658" && r.BroadcasterLanguage == "ru" && r.Tags!.SequenceEqual(new[] { "Russian", "Chatting" })),
                Arg.Any<CancellationToken>());

        await _eventBus.Received(1)
            .PublishAsync(Arg.Is<BroadcastProfileApplied>(e => e.Profile == profile),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyAsync_EmptyGameId_DoesNotSendGameId()
    {
        var profile = new BroadcastProfile
        {
            Name = "N",
            Title = "T",
            GameId = "",
        };

        await _applier.ApplyAsync(profile, CancellationToken.None);

        await _channelsApi.Received(1)
            .ModifyChannelInformationAsync("12345",
                Arg.Is<PatchChannelRequest>(r => r.GameId == null),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyAsync_NoBroadcasterId_PublishesFailed()
    {
        _idProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        var profile = new BroadcastProfile
        {
            Name = "N",
            Title = "T",
        };

        await _applier.ApplyAsync(profile, CancellationToken.None);

        await _channelsApi.DidNotReceiveWithAnyArgs().ModifyChannelInformationAsync(null!, null!, CancellationToken.None);
        await _eventBus.Received(1)
            .PublishAsync(Arg.Any<BroadcastProfileApplyFailed>(),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyAsync_ApiThrows_PublishesFailed()
    {
        _channelsApi
            .ModifyChannelInformationAsync(Arg.Any<string>(), Arg.Any<PatchChannelRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));

        var profile = new BroadcastProfile
        {
            Name = "N",
            Title = "T",
        };

        await _applier.ApplyAsync(profile, CancellationToken.None);

        await _eventBus.Received(1)
            .PublishAsync(Arg.Is<BroadcastProfileApplyFailed>(e => !e.ErrorMessage.Contains("boom") && !string.IsNullOrEmpty(e.ErrorMessage)),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyPatchAsync_OnlyTitle_SendsOnlyTitle()
    {
        await _applier.ApplyPatchAsync("Hello", null, null, CancellationToken.None);

        await _channelsApi.Received(1)
            .ModifyChannelInformationAsync("12345",
                Arg.Is<PatchChannelRequest>(r =>
                    r.Title == "Hello" && r.GameId == null && r.BroadcasterLanguage == null && r.Tags == null),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyPatchAsync_Success_PublishesPatched_NotApplied()
    {
        await _applier.ApplyPatchAsync("Hello", "509658", "Just Chatting", CancellationToken.None);

        await _eventBus.Received(1)
            .PublishAsync(Arg.Is<ChannelInformationPatched>(e =>
                    e.Title == "Hello" && e.GameId == "509658" && e.GameName == "Just Chatting"),
                Arg.Any<CancellationToken>());

        await _eventBus.DidNotReceive()
            .PublishAsync(Arg.Any<BroadcastProfileApplied>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyPatchAsync_NoBroadcasterId_PublishesPatchFailed_NotApplyFailed()
    {
        _idProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((string?)null);

        await _applier.ApplyPatchAsync("Hello", null, null, CancellationToken.None);

        await _eventBus.Received(1)
            .PublishAsync(Arg.Is<ChannelInformationPatchFailed>(e =>
                    e.Title == "Hello" && !string.IsNullOrEmpty(e.ErrorMessage)),
                Arg.Any<CancellationToken>());

        await _eventBus.DidNotReceive()
            .PublishAsync(Arg.Any<BroadcastProfileApplyFailed>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyPatchAsync_ApiThrows_PublishesPatchFailed_WithSafeMessage()
    {
        _channelsApi
            .ModifyChannelInformationAsync(Arg.Any<string>(), Arg.Any<PatchChannelRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));

        await _applier.ApplyPatchAsync("Hello", null, null, CancellationToken.None);

        await _eventBus.Received(1)
            .PublishAsync(Arg.Is<ChannelInformationPatchFailed>(e =>
                    !e.ErrorMessage.Contains("boom") && !string.IsNullOrEmpty(e.ErrorMessage)),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyAsync_Success_ReturnsTrue()
    {
        var profile = new BroadcastProfile
        {
            Name = "N",
            Title = "T",
        };

        var result = await _applier.ApplyAsync(profile, CancellationToken.None);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ApplyAsync_NoBroadcasterId_ReturnsFalse()
    {
        _idProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        var profile = new BroadcastProfile
        {
            Name = "N",
            Title = "T",
        };

        var result = await _applier.ApplyAsync(profile, CancellationToken.None);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ApplyAsync_ApiThrows_ReturnsFalse()
    {
        _channelsApi
            .ModifyChannelInformationAsync(Arg.Any<string>(), Arg.Any<PatchChannelRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));

        var profile = new BroadcastProfile
        {
            Name = "N",
            Title = "T",
        };

        var result = await _applier.ApplyAsync(profile, CancellationToken.None);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ApplyPatchAsync_Success_ReturnsTrue()
    {
        var result = await _applier.ApplyPatchAsync("Hello", null, null, CancellationToken.None);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ApplyPatchAsync_OnForeignChannel_RefusesWithoutCallingTwitch()
    {
        _targetChannel.IsDebugSession = true;
        _targetChannel.Login = "someone-else";
        _targetChannel.OwnChannel = "bobito217";

        var result = await _applier.ApplyPatchAsync("Hello", null, null, CancellationToken.None);

        Assert.That(result, Is.False);

        await _channelsApi.DidNotReceiveWithAnyArgs()
            .ModifyChannelInformationAsync(default!, default!, default);

        await _eventBus.Received(1)
            .PublishAsync(Arg.Is<ChannelInformationPatchFailed>(e => e.ErrorMessage.Contains("отладка", StringComparison.OrdinalIgnoreCase)),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApplyPatchAsync_ApiThrows_ReturnsFalse()
    {
        _channelsApi
            .ModifyChannelInformationAsync(Arg.Any<string>(), Arg.Any<PatchChannelRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));

        var result = await _applier.ApplyPatchAsync("Hello", null, null, CancellationToken.None);

        Assert.That(result, Is.False);
    }
}
