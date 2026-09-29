using KeepShell.Bootstrap;
using KeepShell.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Users;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.Views;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Tests.Users;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class UserStatisticsBalanceInputTests
{
    private const double PageHeight = 640;
    private const double NarrowPage = 640;
    private const double InspectorFloor = 300;
    private const string FontSizePrefix = "Font.Size.";

    private static readonly string[] Dictionaries =
    [
        "pack://application:,,,/KeepShell;component/Resources/Themes/Light.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Themes/Tokens.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Converters.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Styles/Controls.xaml",
        "pack://application:,,,/PoproshaykaBot.Wpf;component/Infrastructure/Converters/AppConverters.xaml",
        "pack://application:,,,/PoproshaykaBot.Wpf;component/Resources/TableStyles.xaml",
        "pack://application:,,,/PoproshaykaBot.Wpf;component/Resources/RatingStyles.xaml",
    ];

    private string _directory = null!;

    [OneTimeSetUp]
    public void EnsureApplication()
    {
        TestResources.Ensure(Dictionaries);
    }

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "poproshayka-balance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestCase("", 0)]
    [TestCase("   ", 0)]
    [TestCase("-", 0)]
    [TestCase("50", 50)]
    [TestCase("+50", 50)]
    [TestCase("−20", -20)]
    [TestCase("-20", -20)]
    [TestCase("1 000", 1000)]
    [TestCase("1 000", 1000)]
    [TestCase("-1000000", -1_000_000)]
    public void Допустимый_ввод_читается_без_ошибки(string text, long expected)
    {
        var page = CreatePage();
        page.AdjustmentText = text;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.HasAdjustmentError, Is.False, page.AdjustmentError);
            Assert.That(page.AdjustmentAmount, Is.EqualTo(expected));
        }
    }

    [TestCase("abc")]
    [TestCase("1,5")]
    [TestCase("1.5")]
    [TestCase("--5")]
    [TestCase("5-")]
    [TestCase("1000001")]
    [TestCase("99999999999999999999999")]
    public void Недопустимый_ввод_называет_ошибку_и_не_обещает_прежнюю_сумму(string text)
    {
        var page = CreatePage();
        page.TrySelectAt(0);
        page.SetAdjustmentCommand.Execute(600d);

        page.AdjustmentText = text;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.HasAdjustmentError, Is.True);
            Assert.That(page.AdjustmentError, Is.Not.Empty);
            Assert.That(page.AdjustmentAmount, Is.Zero);
            Assert.That(page.ActionButtonText, Is.EqualTo("Изменить баланс"));
            Assert.That(page.ApplyAdjustmentCommand.CanExecute(null), Is.False);
            Assert.That(page.IncreaseAdjustmentCommand.CanExecute(null), Is.False);
            Assert.That(page.DecreaseAdjustmentCommand.CanExecute(null), Is.False);
        }
    }

    [Test]
    public void Пустой_ввод_это_пустое_состояние_а_не_ошибка()
    {
        var page = CreatePage();
        page.TrySelectAt(0);
        page.SetAdjustmentCommand.Execute(600d);

        page.AdjustmentText = string.Empty;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.HasAdjustmentError, Is.False);
            Assert.That(page.ActionButtonText, Is.EqualTo("Изменить баланс"));
            Assert.That(page.ApplyAdjustmentCommand.CanExecute(null), Is.False);
            Assert.That(page.IncreaseAdjustmentCommand.CanExecute(null), Is.True);
        }

        page.IncreaseAdjustmentCommand.Execute(null);

        Assert.That(page.AdjustmentText, Is.EqualTo("1"));
    }

    [Test]
    public void Быстрая_сумма_заменяет_ошибочный_ввод()
    {
        var page = CreatePage();
        page.TrySelectAt(0);
        page.AdjustmentText = "abc";

        page.SetAdjustmentCommand.Execute(-100d);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.HasAdjustmentError, Is.False);
            Assert.That(page.AdjustmentAmount, Is.EqualTo(-100));
            Assert.That(page.ApplyAdjustmentCommand.CanExecute(null), Is.True);
        }
    }

    [TestCase(1.0)]
    [TestCase(1.6)]
    public void Поле_и_ошибка_не_выходят_за_инспектор_и_не_наезжают_на_кнопку(double scale)
    {
        try
        {
            ApplyScale(scale);

            var page = CreatePage();
            var view = new UserStatisticsPageView { DataContext = page };

            page.TrySelectAt(0);
            page.AdjustmentText = "abc";
            Arrange(view, NarrowPage);

            var card = (FrameworkElement)view.FindName("InspectorCard")!;
            var content = new Rect(card.RenderSize);
            var box = BoundsIn(card, (FrameworkElement)view.FindName("AdjustmentBox")!);
            var error = BoundsIn(card, (FrameworkElement)view.FindName("AdjustmentErrorText")!);
            var button = BoundsIn(card, (FrameworkElement)view.FindName("ApplyAdjustmentButton")!);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(card.ActualWidth, Is.EqualTo(InspectorFloor).Within(0.5), "Инспектор стоит на своём полу");
                Assert.That(content.Contains(box), Is.True, $"Поле {box} за карточкой {content}");
                Assert.That(error.Height, Is.GreaterThan(0), "Ошибка видна");
                Assert.That(content.Contains(error), Is.True, $"Ошибка {error} за карточкой {content}");
                Assert.That(error.Bottom, Is.LessThanOrEqualTo(button.Top), "Ошибка не наезжает на кнопку");
            }
        }
        finally
        {
            ApplyScale(FontScaleManager.DefaultScale);
        }
    }

    private static Rect BoundsIn(FrameworkElement ancestor, FrameworkElement element)
    {
        return element.TransformToAncestor(ancestor).TransformBounds(new(element.RenderSize));
    }

    private static void ApplyScale(double scale)
    {
        FontScaleManager.Apply(scale);

        var tokens = new ResourceDictionary { Source = new(Dictionaries[1]) };
        var resources = Application.Current.Resources;

        foreach (var key in tokens.Keys.OfType<string>().Where(key => key.StartsWith(FontSizePrefix, StringComparison.Ordinal)))
        {
            if (scale == FontScaleManager.DefaultScale)
            {
                resources.Remove(key);
            }
            else
            {
                resources[key] = (double)tokens[key] * scale;
            }
        }
    }

    private static void Arrange(FrameworkElement view, double width)
    {
        var area = new Size(width, PageHeight);

        view.Measure(area);
        view.Arrange(new(area));
        view.UpdateLayout();
    }

    [Test]
    public void Свёрнутый_баланс_прячет_тело_оставляет_заголовок_и_запоминается()
    {
        var settings = new MemorySettings();
        var page = CreatePage(settings);
        var view = new UserStatisticsPageView { DataContext = page };

        page.TrySelectAt(0);
        page.ToggleBalanceCommand.Execute(null);
        Arrange(view, NarrowPage);

        var body = (FrameworkElement)view.FindName("BalanceBody")!;
        var toggle = (FrameworkElement)view.FindName("BalanceToggle")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.IsBalanceExpanded, Is.False);
            Assert.That(body.Visibility, Is.EqualTo(Visibility.Collapsed));
            Assert.That(toggle.ActualHeight, Is.GreaterThan(0), "Заголовок с шевроном остаётся на месте");
            Assert.That(CreatePage(settings).IsBalanceExpanded, Is.False, "Свёрнутость переживает новую вью-модель");
            Assert.That(CreatePage().IsBalanceExpanded, Is.True, "По умолчанию блок раскрыт");
        }
    }

    private UserStatisticsPageViewModel CreatePage(MemorySettings? settings = null)
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, Path.Combine(_directory, "settings.json"));
        var statistics = new UserStatisticsRepository(NullLogger<UserStatisticsRepository>.Instance);
        var eventBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);

        statistics.ReplaceAll(
        [
            new() { UserId = "1", Name = "koshied", MessageCount = 1_234, BonusPoints = 600 },
            new() { UserId = "2", Name = "новичок", MessageCount = 3 },
        ]);

        return new(
            statistics,
            null!,
            new UserRankService(settingsManager),
            new UserPointsManagementService(statistics, settingsManager, eventBus),
            new FakeChannelProvider(),
            settingsManager,
            null!,
            eventBus,
            settings ?? new MemorySettings());
    }

    private sealed class FakeChannelProvider : IChannelProvider
    {
        public string? Channel => "test";
    }
}
