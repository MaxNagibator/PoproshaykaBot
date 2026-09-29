using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Chat.Handlers;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Chat;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Tests.Debugging;
using PoproshaykaBot.Core.Users;

namespace PoproshaykaBot.Core.Tests.Chat.Handlers;

[TestFixture]
public class StatisticsTrackingHandlerTests
{
    [SetUp]
    public void SetUp()
    {
        _userStatistics = Substitute.For<IUserStatisticsRepository>();
        _botStatistics = Substitute.For<IBotStatisticsRepository>();
        _eventBus = new(NullLogger<InMemoryEventBus>.Instance);
        _targetChannel = FakeTargetChannelProvider.Foreign("someone-else", "bobito217");
    }

    [TearDown]
    public void TearDown()
    {
        _handler?.Dispose();
    }

    private IUserStatisticsRepository _userStatistics = null!;
    private IBotStatisticsRepository _botStatistics = null!;
    private InMemoryEventBus _eventBus = null!;
    private FakeTargetChannelProvider _targetChannel = null!;
    private StatisticsTrackingHandler? _handler;

    [TestCase(false, false, TestName = "Чужой канал в общем профиле – статистика не пишется")]
    [TestCase(true, true, TestName = "Чужой канал в отдельном профиле – статистика пишется")]
    public async Task HandleAsync_OnForeignChannel_RecordsOnlyInAnIsolatedProfile(bool isProfileIsolated, bool expectTracked)
    {
        _targetChannel.IsProfileIsolated = isProfileIsolated;
        _handler = new(_userStatistics, _botStatistics, _targetChannel, _eventBus);

        await _handler.HandleAsync(Message(), CancellationToken.None);

        _userStatistics.Received(expectTracked ? 1 : 0).TrackMessage("42", "viewer");
        _botStatistics.Received(expectTracked ? 1 : 0).IncrementMessagesProcessed();
    }

    private static ChatMessageReceived Message()
    {
        return new("someone-else",
            "msg-1",
            "42",
            "viewer",
            "Viewer",
            "привет",
            UserStatus.None,
            false,
            new());
    }
}
