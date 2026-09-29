using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Tests.Polls;

namespace PoproshaykaBot.Core.Tests.Chat;

[TestFixture]
public sealed class CommandResponseTrackerTests
{
    [SetUp]
    public void SetUp()
    {
        _time = new()
        {
            UtcNow = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero),
        };

        _tracker = new(_time);
    }

    private TestTimeProvider _time = null!;
    private CommandResponseTracker _tracker = null!;

    [Test]
    public void Два_одинаковых_текста_подряд_сопоставляются_каждый_со_своим_эхом()
    {
        var first = new CommandResponseMark("ранг", CommandResponseTarget.Chat);
        var second = new CommandResponseMark("ранг", CommandResponseTarget.Overlay);

        _tracker.Expect("msg-1", first);
        _tracker.Expect("msg-2", second);

        Assert.Multiple(() =>
        {
            Assert.That(_tracker.TryConsume("msg-1"), Is.EqualTo(first));
            Assert.That(_tracker.TryConsume("msg-2"), Is.EqualTo(second));
        });
    }

    [Test]
    public void Эхо_сопоставляется_один_раз()
    {
        _tracker.Expect("msg-1", new("ранг", CommandResponseTarget.Chat));

        Assert.Multiple(() =>
        {
            Assert.That(_tracker.TryConsume("msg-1"), Is.Not.Null);
            Assert.That(_tracker.TryConsume("msg-1"), Is.Null, "Повторное эхо того же id – уже не ответ команды");
        });
    }

    [Test]
    public void Эхо_позже_срока_проходит_как_обычный_ответ_бота()
    {
        _tracker.Expect("msg-1", new("ранг", CommandResponseTarget.Chat));

        _time.UtcNow += CommandResponseTracker.Lifetime + TimeSpan.FromSeconds(1);

        Assert.That(_tracker.TryConsume("msg-1"), Is.Null);
    }

    [Test]
    public void Эхо_в_пределах_срока_ещё_сопоставляется()
    {
        _tracker.Expect("msg-1", new("ранг", CommandResponseTarget.Chat));

        _time.UtcNow += CommandResponseTracker.Lifetime - TimeSpan.FromSeconds(1);

        Assert.That(_tracker.TryConsume("msg-1"), Is.Not.Null);
    }

    [Test]
    public void Потолок_набора_вытесняет_самую_старую_запись()
    {
        for (var index = 0; index <= CommandResponseTracker.MaxTrackedMessages; index++)
        {
            _tracker.Expect($"msg-{index}", new("ранг", CommandResponseTarget.Chat));
        }

        Assert.Multiple(() =>
        {
            Assert.That(_tracker.TryConsume("msg-0"), Is.Null, "Самая старая запись должна быть вытеснена");
            Assert.That(_tracker.TryConsume("msg-1"), Is.Not.Null);
            Assert.That(_tracker.TryConsume($"msg-{CommandResponseTracker.MaxTrackedMessages}"), Is.Not.Null);
        });
    }

    [Test]
    public void Отключение_бота_очищает_набор()
    {
        _tracker.Expect("msg-1", new("ранг", CommandResponseTarget.Chat));
        _tracker.Clear();

        Assert.That(_tracker.TryConsume("msg-1"), Is.Null);
    }
}
