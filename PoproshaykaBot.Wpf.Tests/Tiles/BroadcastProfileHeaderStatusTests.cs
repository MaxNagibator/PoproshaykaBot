using KeepShell.Services;
using KeepShell.Services.Modal;
using KeepShell.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Broadcasting;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Wpf.Infrastructure.Dashboard;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.IO;

namespace PoproshaykaBot.Wpf.Tests.Tiles;

[TestFixture]
public class BroadcastProfileHeaderStatusTests
{
    [TestCase(false, false, false, null, StatusSeverity.None, TestName = "Ничего_не_применено_шапка_молчит")]
    [TestCase(false, true, false, "Утро", StatusSeverity.Success, TestName = "Применённый_профиль_назван_зелёным")]
    [TestCase(false, true, true, "Утро · расходится с каналом", StatusSeverity.Warning, TestName = "Расхождение_с_каналом_названо_жёлтым")]
    [TestCase(true, true, true, "Применяю «Вечер»…", StatusSeverity.Info, TestName = "Идущее_применение_сильнее_применённого")]
    [TestCase(true, false, false, "Применяю «Вечер»…", StatusSeverity.Info, TestName = "Идущее_применение_без_применённого")]
    public void Статус_шапки_плитки_профилей(bool eveningInFlight, bool morningActive, bool morningDrift, string? text, StatusSeverity severity)
    {
        var morning = Item("Утро", morningActive, morningDrift);
        var evening = Item("Вечер", false, false);
        evening.IsApplyInFlight = eveningInFlight;

        var status = BroadcastProfilesTileViewModel.DescribeProfileStatus([morning, evening]);

        if (text is null)
        {
            Assert.That(status, Is.Null, "Без применённого профиля шапке нечего сообщать");
            return;
        }

        Assert.That(status, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(status!.Text, Is.EqualTo(text));
            Assert.That(status.Severity, Is.EqualTo(severity));
            Assert.That(status.Description, Does.Contain(eveningInFlight ? "«Вечер»" : "«Утро»"),
                "Диктор и подсказка называют профиль полным предложением");
        });
    }

    [TestCase(300, 150, 80, 150, 150, TestName = "Влезают_оба_статус_берёт_остаток")]
    [TestCase(190, 150, 78, 111, 79,TestName = "Короткий_статус_целиком_заголовок_режется")]
    [TestCase(190, 150, 200, 95, 95, TestName = "Длинный_статус_не_режет_заголовок_ниже_половины")]
    [TestCase(190, 60, 300, 60, 130, TestName = "Короткий_заголовок_отдаёт_статусу_всё_остальное")]
    [TestCase(0, 150, 80, 0, 0, TestName = "Нулевая_ширина_без_отрицательных_слотов")]
    public void Строка_шапки_делит_ширину_заголовка_и_статуса(double available, double title, double status, double expectedTitle, double expectedStatus)
    {
        var (titleWidth, statusWidth) = TileHeaderLine.Split(available, title, status);

        Assert.Multiple(() =>
        {
            Assert.That(titleWidth, Is.EqualTo(expectedTitle).Within(0.01));
            Assert.That(statusWidth, Is.EqualTo(expectedStatus).Within(0.01));
            Assert.That(titleWidth + statusWidth, Is.LessThanOrEqualTo(Math.Max(available, 0) + 0.01),
                "Строка шапки не берёт больше выданного – иначе вытолкнула бы кнопки плитки");
        });
    }

    [TestCase(false, false, "", TestName = "Неприменённый_профиль_не_несёт_статуса")]
    [TestCase(true, false, "Применён", TestName = "Применённый_профиль_назван_для_диктора")]
    [TestCase(true, true, "Применён, расходится с каналом", TestName = "Расхождение_с_каналом_названо_для_диктора")]
    public void Статус_карточки_для_диктора(bool isActive, bool hasDrift, string expected)
    {
        var item = Item("Утро", isActive, hasDrift);

        Assert.That(item.ActiveStatusText, Is.EqualTo(expected));
    }

    [Test]
    public void Смена_состояния_карточки_поднимает_статус_для_диктора()
    {
        var item = Item("Утро", false, false);
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.IsActive = true;
        item.HasDrift = true;

        Assert.That(raised.Count(name => name == nameof(BroadcastProfileItemViewModel.ActiveStatusText)), Is.EqualTo(2));
    }

    [Test]
    public void Перезагрузка_карточек_посреди_применения_сохраняет_применяю_в_шапке()
    {
        var path = Path.Combine(Path.GetTempPath(), $"broadcast-profiles-{Guid.NewGuid():N}.json");

        try
        {
            var bus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
            var store = new BroadcastProfilesStore(null, path);
            var manager = new BroadcastProfilesManager(store, null!, bus, TimeProvider.System, NullLogger<BroadcastProfilesManager>.Instance);
            manager.Upsert(new() { Name = "Вечер" });

            using var tile = new BroadcastProfilesTileViewModel(manager, store, new IdleStreamStatus(), null!, null!, null!, new SilentDialogService(), null!, bus);
            var profile = manager.GetAll()[0];

            bus.PublishAsync(new BroadcastProfileApplying(profile)).GetAwaiter().GetResult();
            bus.PublishAsync(new BroadcastProfilesChanged()).GetAwaiter().GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(tile.Items[0].IsApplyInFlight, Is.True, "Кнопка карточки не теряет «Применяю…» на перезагрузке");
                Assert.That(tile.HeaderStatus?.Text, Is.EqualTo("Применяю «Вечер»…"));
            });

            bus.PublishAsync(new BroadcastProfileApplied(profile)).GetAwaiter().GetResult();

            Assert.That(tile.Items.Any(item => item.IsApplyInFlight), Is.False, "После применения признак не залипает");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class IdleStreamStatus : IStreamStatus
    {
        public StreamStatus CurrentStatus => StreamStatus.Unknown;

        public StreamInfo? CurrentStream => null;

        public Task RefreshLiveSnapshotAsync() => Task.CompletedTask;
    }

    private sealed class SilentDialogService : IDialogService
    {
        public Task<bool> ShowAsync(IDialogViewModel viewModel) => Task.FromResult(true);

        public Task<bool> ReplaceAsync(IDialogViewModel viewModel) => ShowAsync(viewModel);

        public bool Confirm(string title, string message, bool defaultYes = false) => true;

        public bool ConfirmWarning(string title, string message, bool defaultYes = false) => true;

        public void Info(string title, string message)
        {
        }

        public void Warning(string title, string message)
        {
        }

        public void Error(string title, string message) => throw new InvalidOperationException(message);
    }

    private static BroadcastProfileItemViewModel Item(string name, bool isActive, bool hasDrift)
    {
        return new(
            new BroadcastProfile { Name = name },
            isActive,
            hasDrift,
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => { },
            _ => { });
    }
}
