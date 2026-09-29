using KeepShell.Testing;
using PoproshaykaBot.Wpf.Views.Tiles;
using System.Windows;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Tests.Tiles;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class TileStatusLineTests
{
    private const string Status = "✗ Не удалось запустить голосование";

    private static readonly string[] Dictionaries =
    [
        "pack://application:,,,/KeepShell;component/Resources/Themes/Light.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Themes/Tokens.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Converters.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Styles/Controls.xaml",
        "pack://application:,,,/PoproshaykaBot.Wpf;component/Infrastructure/Converters/AppConverters.xaml",
    ];

    [OneTimeSetUp]
    public void EnsureApplication()
    {
        TestResources.Ensure(Dictionaries);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Статус_опросов_виден_с_опросом_и_без(bool hasSnapshot)
    {
        var view = new PollsTileView { DataContext = new PollsStub(hasSnapshot, Status) };

        Assert.That(StatusHeight(view), Is.GreaterThan(0),
            "Ошибка запуска опроса без активного опроса иначе не видна вовсе – строка статуса пряталась вместе с панелью опроса");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Статус_профилей_виден_при_пустом_и_непустом_списке(bool hasProfiles)
    {
        var view = new BroadcastProfilesTileView { DataContext = new ProfilesStub(hasProfiles, Status) };

        Assert.That(StatusHeight(view), Is.GreaterThan(0),
            "Сообщение плитки профилей при пустом списке иначе не видно – строка статуса пряталась вместе со списком карточек");
    }

    [Test]
    public void Пустой_статус_места_не_занимает()
    {
        Assert.Multiple(() =>
        {
            Assert.That(StatusHeight(new PollsTileView { DataContext = new PollsStub(false, string.Empty) }), Is.Zero);
            Assert.That(StatusHeight(new BroadcastProfilesTileView { DataContext = new ProfilesStub(false, string.Empty) }), Is.Zero);
        });
    }

    private static double StatusHeight(UserControl view)
    {
        const double width = 380;

        view.Measure(new(width, double.PositiveInfinity));
        view.Arrange(new(0, 0, width, view.DesiredSize.Height));
        view.UpdateLayout();

        var status = (FrameworkElement)view.FindName("StatusLine");

        return status.ActualHeight;
    }

    public sealed class PollsStub(bool hasSnapshot, string status)
    {
        public bool HasSnapshot { get; } = hasSnapshot;
        public bool IsActive { get; } = hasSnapshot;
        public string StatusMessage { get; } = status;
        public bool HasStatusMessage { get; } = status.Length > 0;
        public string? UnsavedNotice => null;
        public string FooterText => "Идёт голосование";
        public object[] Choices { get; } = [];
    }

    public sealed class ProfilesStub(bool hasProfiles, string status)
    {
        public bool HasProfiles { get; } = hasProfiles;
        public string StatusMessage { get; } = status;
        public bool HasStatusMessage { get; } = status.Length > 0;
        public bool IsStatusError => true;
        public string? UnsavedNotice => null;
        public object[] Items { get; } = [];
    }
}
