using FlaUI.Core.AutomationElements;

namespace PoproshaykaBot.Wpf.Tests.Smoke;

[TestFixture]
[NonParallelizable]
public class MainWindowSmokeTests
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

    private const string ExpectedTitle = "PoproshaykaBot";
    private static readonly TimeSpan ContentAppearTimeout = TimeSpan.FromSeconds(15);

    private SmokeTestSession? _session;

    [Test]
    public void MainWindow_ShouldLaunch()
    {
        Assert.That(_session!.MainWindow.IsAvailable, Is.True, "Главное окно должно быть доступно после запуска");
    }

    [Test]
    public void MainWindow_ShouldHaveBotTitle()
    {
        var title = _session!.MainWindow.Title;
        Assert.That(title, Is.EqualTo(ExpectedTitle),
            $"Заголовок окна должен быть '{ExpectedTitle}', получено: '{title}'");
    }

    [Test]
    public void MainWindow_ShouldBeResizable()
    {
        Assert.That(_session!.MainWindow.Patterns.Transform.IsSupported, Is.True,
            "Главное окно должно поддерживать Transform pattern (resize/move)");
    }

    [Test]
    public void MainWindow_ShouldHaveNonZeroBounds()
    {
        var bounds = _session!.MainWindow.BoundingRectangle;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(bounds.Width, Is.GreaterThan(0), "Ширина окна должна быть положительной");
            Assert.That(bounds.Height, Is.GreaterThan(0), "Высота окна должна быть положительной");
        }
    }

    [Test]
    public void MainWindow_ShouldRenderShellNavigation()
    {
        var navItem = WaitForDescendantByName("Обзор");

        Assert.That(navItem, Is.Not.Null,
            "Навигация оболочки должна содержать раздел 'Обзор' после запуска");
    }

    [Test]
    public void MainWindow_ShouldShowDashboard_WithTiles()
    {
        var streamTile = WaitForDescendantByName("Информация о стриме");
        var broadcastTile = WaitForDescendantByName("Рассылка");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(streamTile, Is.Not.Null, "Заголовок тайла стрима должен быть в дереве UI");
            Assert.That(broadcastTile, Is.Not.Null, "Заголовок тайла рассылки должен быть в дереве UI");
        }
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
