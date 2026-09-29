using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Wpf.ViewModels.Tiles;

namespace PoproshaykaBot.Wpf.Tests.Tiles;

[TestFixture]
public class ObsTileTests
{
    private const long TotalFrames = 1000;

    [TestCase(0L, null, "Кадры не теряются", TestName = "Дропов_нет_нейтрально")]
    [TestCase(3L, null, "Пропущено кадров: 3 (0,3 %)", TestName = "Доли_процента_нейтрально")]
    [TestCase(10L, "Warning", "Пропущено кадров: 10 (1 %)", TestName = "Один_процент_жёлтый")]
    [TestCase(30L, "Warning", "Пропущено кадров: 30 (3 %)", TestName = "Три_процента_жёлтый")]
    [TestCase(50L, "Warning", "Пропущено кадров: 50 (5 %)", TestName = "Пять_процентов_ещё_жёлтый")]
    [TestCase(70L, "Error", "Пропущено кадров: 70 (7 %)", TestName = "Семь_процентов_красный")]
    public void Дропы_окрашены_по_порогу(long skipped, string? severity, string text)
    {
        var health = ObsStreamHealth.Describe(true, 0D, skipped, TotalFrames);

        Assert.That(health, Is.Not.Null);
        Assert.That(health!.Value, Is.EqualTo((text, severity)));
    }

    [Test]
    public void Перегрузка_сети_видна_когда_дропы_нейтральны_и_не_заслоняет_красные()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ObsStreamHealth.Describe(true, 0.2D, 3L, TotalFrames),
                Is.EqualTo(("Сеть перегружена: 20 %", (string?)"Warning")));
            Assert.That(ObsStreamHealth.Describe(true, 0.2D, 70L, TotalFrames)?.Severity, Is.EqualTo("Error"));
            Assert.That(ObsStreamHealth.Describe(false, 0.2D, 70L, TotalFrames), Is.Null,
                "Без эфира о здоровье эфира говорить нечего");
        }
    }

    [TestCase(ObsSourceMeterState.Active, "-3,5 дБ", true)]
    [TestCase(ObsSourceMeterState.Muted, "выключен", false)]
    [TestCase(ObsSourceMeterState.Missing, "не найден в OBS", false)]
    public void Состояние_аудиоисточника_названо_один_раз(ObsSourceMeterState state, string stateText, bool showsLevel)
    {
        const string name = "Звук рабочего стола";
        var meter = new ObsSourceMeterViewModel(name);

        switch (state)
        {
            case ObsSourceMeterState.Active:
                meter.ShowActive(name, -3.5D);
                break;
            case ObsSourceMeterState.Muted:
                meter.ShowMuted(name);
                break;
            default:
                meter.ShowMissing(name);
                break;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(meter.State, Is.EqualTo(state));
            Assert.That(meter.DisplayName, Is.EqualTo(name), "Имя источника идёт как есть, без пометок состояния");
            Assert.That(meter.StateText, Is.EqualTo(stateText));
            Assert.That(meter.ShowsLevel, Is.EqualTo(showsLevel),
                "Полоса уровня есть только у включённого источника – у выключенного пустая полоса повторяла бы подпись");
        }
    }

    [TestCase(ObsOutputCardKind.Stream, true, false, "В эфире", true, false)]
    [TestCase(ObsOutputCardKind.Stream, false, false, "Эфир не идёт", false, false)]
    [TestCase(ObsOutputCardKind.Record, true, false, "Идёт запись", true, true)]
    [TestCase(ObsOutputCardKind.Record, true, true, "Запись на паузе", true, true)]
    [TestCase(ObsOutputCardKind.Record, false, false, "Запись не идёт", false, false)]
    public void Карточка_выхода_называет_состояние_одной_строкой(
        ObsOutputCardKind kind,
        bool active,
        bool paused,
        string title,
        bool running,
        bool showsPause)
    {
        var card = new ObsOutputCardViewModel(kind, null!, NullLogger.Instance);

        card.ApplySnapshot(active, paused, "00:00:00.000", null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card.Title, Is.EqualTo(title));
            Assert.That(card.IsRunning, Is.EqualTo(running), "Таймер виден только у идущего выхода");
            Assert.That(card.ShowsSecondaryButton, Is.EqualTo(showsPause), "Пауза есть только у идущей записи");
        }
    }
}
