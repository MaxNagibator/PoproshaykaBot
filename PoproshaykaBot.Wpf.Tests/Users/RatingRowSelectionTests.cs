using KeepShell.Testing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PoproshaykaBot.Wpf.Tests.Users;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class RatingRowSelectionTests
{
    private const double ListWidth = 400;
    private const double RowHeight = 40;
    private const int MinimumChannelDistance = 60;

    private const string RowTemplate = """
        <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
            <TextBlock Text='{Binding Summary}' />
        </DataTemplate>
        """;

    private static readonly string[] Dictionaries =
    [
        "pack://application:,,,/KeepShell;component/Resources/Themes/Light.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Themes/Tokens.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Converters.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Styles/Controls.xaml",
        "pack://application:,,,/PoproshaykaBot.Wpf;component/Resources/RatingStyles.xaml",
    ];

    [OneTimeSetUp]
    public void EnsureApplication()
    {
        TestResources.Ensure(Dictionaries);
    }

    [TestCase("Light")]
    [TestCase("Dark")]
    public void Выбранная_строка_с_полной_долей_отличима_от_невыбранной(string theme)
    {
        var root = new Grid { Width = ListWidth };
        root.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new($"pack://application:,,,/KeepShell;component/Resources/Themes/{theme}.xaml"),
        });

        var list = new ListBox
        {
            ItemsSource = new[] { new Row(1, "лидер"), new Row(1, "второй лидер") },
            ItemContainerStyle = (Style)Application.Current.FindResource("RatingRowItem"),
            ItemTemplate = (DataTemplate)XamlReader.Parse(RowTemplate),
            BorderThickness = new(0),
            Padding = new(0),
        };

        root.Children.Add(list);
        list.SelectedIndex = 0;

        root.Measure(new(ListWidth, RowHeight * 4));
        root.Arrange(new(0, 0, ListWidth, RowHeight * 4));
        root.UpdateLayout();

        var selected = EdgeColor(root, list, 0);
        var plain = EdgeColor(root, list, 1);
        var distance = Math.Abs(selected.R - plain.R) + Math.Abs(selected.G - plain.G) + Math.Abs(selected.B - plain.B);

        Assert.That(distance, Is.GreaterThanOrEqualTo(MinimumChannelDistance),
            $"Тема {theme}: у левого края выбранная строка {selected} против невыбранной {plain} – заливка доли 100 % закрыла отметку выбора");
    }

    private static Color EdgeColor(FrameworkElement root, ListBox list, int index)
    {
        var container = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(index)!;
        var origin = container.TransformToAncestor(root).Transform(default);
        var x = (int)origin.X + 1;
        var y = (int)(origin.Y + container.ActualHeight / 2);

        var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);

        var pixel = new byte[4];
        bitmap.CopyPixels(new(x, y, 1, 1), pixel, 4, 0);

        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    public sealed record Row(double Share, string Summary);
}
