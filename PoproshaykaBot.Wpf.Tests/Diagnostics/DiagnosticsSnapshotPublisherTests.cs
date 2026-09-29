using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Diagnostics;
using PoproshaykaBot.Wpf.Infrastructure.Diagnostics;
using PoproshaykaBot.Wpf.ViewModels.Diagnostics;

namespace PoproshaykaBot.Wpf.Tests.Diagnostics;

[TestFixture]
public class DiagnosticsSnapshotPublisherTests
{
    [Test]
    public void Повторная_активация_карточки_не_даёт_второго_счёта()
    {
        var dispatcher = new ManualUiDispatcher();
        var captures = 0;

        var publisher = Create(dispatcher, () =>
        {
            captures++;

            return DiagnosticsSnapshots.Full();
        });

        var card = new ChatDiagnosticsCardViewModel(publisher);

        card.SetActive(true);
        card.SetActive(true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(publisher.ListenerCount, Is.EqualTo(1), "Вторая активация той же карточки не должна давать второго подписчика");
            Assert.That(captures, Is.EqualTo(1), "Активация карточки стоит ровно одного опроса владельцев");
            Assert.That(publisher.IsRunning, Is.True);
        }

        card.SetActive(false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(publisher.ListenerCount, Is.Zero);
            Assert.That(publisher.IsRunning, Is.False, "Нулевой счётчик активных карточек обязан гасить таймер");
        }
    }

    [Test]
    public void Семь_карточек_дают_один_опрос_за_тик()
    {
        var dispatcher = new ManualUiDispatcher();
        var captures = 0;

        var publisher = Create(dispatcher, () =>
        {
            captures++;

            return DiagnosticsSnapshots.Full();
        });

        var cards = new DiagnosticsCardViewModel[]
        {
            new EventSubDiagnosticsCardViewModel(publisher),
            new ChatDiagnosticsCardViewModel(publisher),
            new ObsDiagnosticsCardViewModel(publisher),
            new SseDiagnosticsCardViewModel(publisher),
            new QueueDiagnosticsCardViewModel(publisher),
            new EventBusDiagnosticsCardViewModel(publisher),
        };

        foreach (var card in cards)
        {
            card.SetActive(true);
        }

        var afterActivation = captures;

        dispatcher.Timers[0].Tick();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterActivation, Is.EqualTo(1), "Опрос заводит только первая активная карточка, остальные берут готовый снимок");
            Assert.That(captures, Is.EqualTo(2), "Тик страницы стоит одного опроса на все карточки");
            Assert.That(cards[1].Rows, Is.Not.Empty, "Карточка обязана получить снимок тика");
        }
    }

    [Test]
    public void Сорвавшийся_опрос_оставляет_прежний_снимок_и_не_роняет_тик()
    {
        var dispatcher = new ManualUiDispatcher();
        var fail = false;
        var publisher = Create(dispatcher, () => fail ? throw new InvalidOperationException("источник недоступен") : DiagnosticsSnapshots.Full());
        var card = new ChatDiagnosticsCardViewModel(publisher);

        card.SetActive(true);
        var rows = card.Rows;
        fail = true;

        Assert.DoesNotThrow(() => dispatcher.Timers[0].Tick());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(publisher.Last, Is.Not.Null, "Сорвавшийся опрос не должен стирать последний удачный снимок");
            Assert.That(card.Rows, Is.SameAs(rows), "Карточка обязана остаться на прежних числах, а не опустеть");
        }
    }

    [Test]
    public void Карточка_ушедшей_страницы_не_держит_таймер_за_соседа()
    {
        var dispatcher = new ManualUiDispatcher();
        var publisher = Create(dispatcher, DiagnosticsSnapshots.Full);
        var first = new ChatDiagnosticsCardViewModel(publisher);
        var second = new ObsDiagnosticsCardViewModel(publisher);

        first.SetActive(true);
        second.SetActive(true);
        first.SetActive(false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(publisher.IsRunning, Is.True, "Пока жива вторая карточка, таймер обязан идти");
            Assert.That(publisher.ListenerCount, Is.EqualTo(1));
        }

        second.SetActive(false);
        second.SetActive(false);

        Assert.That(publisher.IsRunning, Is.False, "Повторное гашение не должно уводить счётчик в минус и оставлять таймер живым");
    }

    private static DiagnosticsSnapshotPublisher Create(ManualUiDispatcher dispatcher, Func<DiagnosticsSnapshot> capture)
    {
        return new(dispatcher, capture, NullLogger<DiagnosticsSnapshotPublisher>.Instance);
    }
}
