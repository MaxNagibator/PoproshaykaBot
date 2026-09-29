using PoproshaykaBot.Core.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Tests.Twitch.EventSub;

[TestFixture]
public sealed class EventSubSubscriptionLedgerTests
{
    [SetUp]
    public void SetUp()
    {
        _deleted = [];
        _helix = Substitute.For<ITwitchHelixClient>();

        _helix.DeleteEventSubSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var token = call.Arg<CancellationToken>();

                if (token.IsCancellationRequested)
                {
                    return Task.FromCanceled<bool>(token);
                }

                if (_failDeletes)
                {
                    return Task.FromException<bool>(new HttpRequestException("сбой сети"));
                }

                lock (_deleted)
                {
                    _deleted.Add(call.Arg<string>());
                }

                return Task.FromResult(true);
            });

        _ledger = new(_helix, NullLogger.Instance);
    }

    private const string Type = "stream.online";

    private ITwitchHelixClient _helix = null!;
    private EventSubSubscriptionLedger _ledger = null!;
    private List<string> _deleted = null!;
    private volatile bool _failDeletes;

    [TestCase(EventSubSubscribeOutcome.TakenByOtherSession, TestName = "Снятие набора типа удаляет вытесненную подписку")]
    [TestCase(EventSubSubscribeOutcome.Created, TestName = "Замена набора типа удаляет вытесненную подписку")]
    public async Task Реестр_не_теряет_вытесненный_id_без_удаления(EventSubSubscribeOutcome next)
    {
        var generation = _ledger.Generation;

        await _ledger.RecordAsync(generation, Type, new(EventSubSubscribeOutcome.Created, ["sub-first"]));

        var replacement = next == EventSubSubscribeOutcome.Created
            ? new EventSubSubscribeResult(next, ["sub-second"])
            : new EventSubSubscribeResult(next, []);

        Assert.That(await _ledger.RecordAsync(generation, Type, replacement), Is.True);
        Assert.That(_deleted, Is.EqualTo(new[] { "sub-first" }));
    }

    [Test]
    public async Task Отозванный_id_забывается_без_удаления()
    {
        var generation = _ledger.Generation;

        await _ledger.RecordAsync(generation, Type, new(EventSubSubscribeOutcome.Created, ["sub-revoked"]));
        _ledger.Forget(Type, "sub-revoked");

        Assert.That(_ledger.Holds(Type), Is.False);

        await _ledger.RecordAsync(generation, Type, new(EventSubSubscribeOutcome.Created, ["sub-restored"]));

        Assert.That(_deleted, Is.Empty);
    }

    [Test]
    public async Task Отозванный_до_записи_id_не_вытесняет_восстановленную_подписку()
    {
        var generation = _ledger.Generation;

        _ledger.Forget(Type, "sub-retried");
        await _ledger.RecordAsync(generation, Type, new(EventSubSubscribeOutcome.Created, ["sub-restored"]));

        var recorded = await _ledger.RecordAsync(generation, Type, new(EventSubSubscribeOutcome.Created, ["sub-retried"]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded, Is.False);
            Assert.That(_ledger.Holds(Type), Is.True);
            Assert.That(_deleted, Is.Empty);
        }

        await _ledger.CloseAsync(CancellationToken.None);

        Assert.That(_deleted, Is.EqualTo(new[] { "sub-restored" }));
    }

    [TestCase(false, TestName = "Опоздавшая подписка остановленного подписчика удаляется своим токеном")]
    [TestCase(true, TestName = "Опоздавшая подписка прошлой сессии удаляется своим токеном и не считается удержанной")]
    public async Task Опоздавшая_подписка_прошлого_поколения_удаляется(bool newSession)
    {
        var stale = _ledger.Generation;

        if (newSession)
        {
            await _ledger.BeginSessionAsync(CancellationToken.None);
        }
        else
        {
            await _ledger.CloseAsync(CancellationToken.None);
        }

        var recorded = await _ledger.RecordAsync(stale, Type, new(EventSubSubscribeOutcome.Created, ["sub-late"]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded, Is.False);
            Assert.That(_deleted, Is.EqualTo(new[] { "sub-late" }));
            Assert.That(_ledger.Holds(Type), Is.False);
        }
    }

    [TestCase(false, TestName = "Подписка прошлой сессии, которую не удалось удалить на welcome, удаляется при остановке")]
    [TestCase(true, TestName = "Подписка прошлой сессии, удаление которой отменено на welcome, удаляется при остановке")]
    public async Task Неудалённая_подписка_прошлой_сессии_остаётся_на_удаление(bool cancelled)
    {
        await _ledger.RecordAsync(_ledger.Generation, Type, new(EventSubSubscribeOutcome.Created, ["sub-previous"]));

        using var cts = new CancellationTokenSource();

        if (cancelled)
        {
            await cts.CancelAsync();
        }
        else
        {
            _failDeletes = true;
        }

        await _ledger.BeginSessionAsync(cts.Token);
        _failDeletes = false;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_deleted, Is.Empty);
            Assert.That(_ledger.Holds(Type), Is.False);
        }

        await _ledger.CloseAsync(CancellationToken.None);

        Assert.That(_deleted, Is.EqualTo(new[] { "sub-previous" }));
    }

    [Test]
    public async Task Вытесненная_подписка_которую_не_удалось_удалить_удаляется_на_следующем_welcome()
    {
        var generation = _ledger.Generation;

        await _ledger.RecordAsync(generation, Type, new(EventSubSubscribeOutcome.Created, ["sub-first"]));

        _failDeletes = true;
        await _ledger.RecordAsync(generation, Type, new(EventSubSubscribeOutcome.Created, ["sub-second"]));
        _failDeletes = false;

        Assert.That(_deleted, Is.Empty);

        await _ledger.BeginSessionAsync(CancellationToken.None);

        Assert.That(_deleted, Is.EquivalentTo(new[] { "sub-first", "sub-second" }));
    }

    [Test]
    public async Task Новая_сессия_удаляет_подписки_прошлой_и_начинает_с_пустого_набора()
    {
        await _ledger.RecordAsync(_ledger.Generation, Type, new(EventSubSubscribeOutcome.Created, ["sub-previous"]));

        await _ledger.BeginSessionAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_deleted, Is.EqualTo(new[] { "sub-previous" }));
            Assert.That(_ledger.Holds(Type), Is.False);
        }
    }
}
