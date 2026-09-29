using KeepShell.Testing;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.Views;
using PoproshaykaBot.Wpf.Views.Settings;
using System.Windows;
using System.Windows.Media;

namespace PoproshaykaBot.Wpf.Tests.Settings;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class SettingsBlocksTests
{
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

    [OneTimeSetUp]
    public void EnsureApplication()
    {
        TestResources.Ensure(Dictionaries);
    }

    [Test]
    public void Каждому_подпункту_рейла_отвечает_якорь_блока_в_разметке()
    {
        var expected = SettingsPageViewModel.CreateSections().Items
            .SelectMany(section => section.Children.Select(child => $"{section.Key}/{child.Key}"))
            .ToArray();

        var view = new SettingsPageView();
        view.Measure(new(1100, 720));
        view.Arrange(new(new Point(), new Size(1100, 720)));

        var anchors = new List<string>();
        Collect(view, anchors);

        Assert.That(anchors, Is.EquivalentTo(expected),
            "Ключи блоков разметки разошлись с подпунктами рейла: прокрутка к блоку и подсветка подпункта молча перестают работать.");
    }

    [TestCase(0, false, "channel", TestName = "Спай_в_начале_карточки_берёт_первый_блок")]
    [TestCase(100, false, "channel", TestName = "Спай_держит_блок_пока_следующий_заголовок_ниже_кромки")]
    [TestCase(180, false, "limits", TestName = "Спай_переходит_на_блок_чуть_раньше_его_кромки")]
    [TestCase(420, false, "messages", TestName = "Спай_берёт_последний_заголовок_выше_кромки")]
    public void Видимый_блок_считается_по_верхнему_краю_карточки(double offset, bool atBottom, string expected)
    {
        (string Key, double Top)[] blocks = [("channel", 0), ("limits", 200), ("messages", 420)];

        Assert.That(SettingsBlock.VisibleAt(blocks, offset, atBottom), Is.EqualTo(expected));
    }

    [Test]
    public void Дно_прокрутки_подсвечивает_последний_блок_даже_если_его_заголовок_выше_кромки()
    {
        (string Key, double Top)[] blocks = [("channel", 0), ("limits", 200), ("messages", 420)];

        Assert.That(SettingsBlock.VisibleAt(blocks, 300, true), Is.EqualTo("messages"),
            "Короткий последний блок иначе остался бы неподсвеченным: до его верхнего края прокрутка не доходит.");
    }

    private static void Collect(DependencyObject root, List<string> anchors)
    {
        if (root is FrameworkElement element && SettingsBlock.GetKey(element) is { Length: > 0 } key)
        {
            anchors.Add(key);
        }

        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var index = 0; index < count; index++)
        {
            Collect(VisualTreeHelper.GetChild(root, index), anchors);
        }
    }
}
