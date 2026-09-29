using FlaUI.Core.AutomationElements;

namespace PoproshaykaBot.Wpf.Tests.Smoke;

[TestFixture]
[NonParallelizable]
public class DashboardEditModeSmokeTests
{
    [SetUp]
    public void Setup()
    {
        _session = SmokeTestSession.Launch(new() { SeedConfiguredSettings = true });
    }

    [TearDown]
    public void TearDown()
    {
        _session?.Dispose();
        _session = null;
    }

    private static readonly TimeSpan ContentAppearTimeout = TimeSpan.FromSeconds(15);

    private SmokeTestSession? _session;

    [Test]
    public void EditMode_OpensAndCloses_FromTheDashboard()
    {
        var entry = WaitForDescendantByName("Настроить панель");

        Assert.That(entry, Is.Not.Null, "Кнопка входа в режим правки должна быть на панели.");

        entry!.AsButton().Invoke();

        var done = WaitForDescendantByName("Выйти из режима правки панели");
        var splitter = WaitForDescendantByName("Разделитель по вертикали");
        var horizontal = WaitForDescendantByName("Разделитель по горизонтали");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(done, Is.Not.Null, "В режиме правки над панелью появляется полоска с кнопкой выхода.");
            Assert.That(splitter, Is.Not.Null, "Между соседями узла обязан появиться разделитель – ради него режим и заведён.");
            Assert.That(horizontal, Is.Not.Null, "Внутри колонки плитки делит разделитель по горизонтали – иначе высота не правится.");
            Assert.That(WaitForDescendantByName("Добавить плитку"), Is.Not.Null);
        }

        done!.AsButton().Invoke();

        Assert.That(
            WaitForDescendantByName("Настроить панель"),
            Is.Not.Null,
            "После выхода панель возвращается в обычный вид с кнопкой входа.");
    }

    private AutomationElement? WaitForDescendantByName(string name)
    {
        var deadline = DateTime.UtcNow + ContentAppearTimeout;

        while (DateTime.UtcNow < deadline)
        {
            var match = _session!.MainWindow.FindFirstDescendant(cf => cf.ByName(name));

            if (match is not null)
            {
                return match;
            }

            Thread.Sleep(150);
        }

        return null;
    }
}
