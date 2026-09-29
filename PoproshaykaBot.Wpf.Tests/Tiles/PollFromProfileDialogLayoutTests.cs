using PoproshaykaBot.Wpf.Tests.Support;
using PoproshaykaBot.Wpf.Views.Dialogs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PoproshaykaBot.Wpf.Tests.Tiles;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class PollFromProfileDialogLayoutTests
{
    private const double Step = 100;
    private const double Tolerance = 0.5;

    private static readonly Size Area = new(640, 400);

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

    [TestCase(1000, 2)]
    [TestCase(-1000, 0)]
    public void Разделитель_списка_профилей_упирается_в_пол_соседа_и_не_растит_диалог(double change, int squeezed)
    {
        var view = new PollFromProfileDialogView();

        Arrange(view);

        var splitter = Descendants<GridSplitter>(view).Single();
        var columns = ((Grid)splitter.Parent).ColumnDefinitions;
        var before = columns.Sum(column => column.ActualWidth);

        splitter.RaiseEvent(new DragStartedEventArgs(0, 0));

        for (var remaining = Math.Abs(change); remaining > 0; remaining -= Step)
        {
            splitter.RaiseEvent(new DragDeltaEventArgs(Math.Sign(change) * Math.Min(remaining, Step), 0));
            Arrange(view);
        }

        splitter.RaiseEvent(new DragCompletedEventArgs(0, 0, false));

        var after = columns.Sum(column => column.ActualWidth);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after, Is.EqualTo(before).Within(Tolerance),
                $"Разделитель только делит место: колонка ушла за край диалога на {after - before:F1} DIP");
            Assert.That(columns[squeezed].ActualWidth, Is.EqualTo(columns[squeezed].MinWidth).Within(Tolerance),
                "Ход кончается на полу сжимаемой колонки");
        }
    }

    private static void Arrange(FrameworkElement view)
    {
        view.Measure(Area);
        view.Arrange(new(Area));
        view.UpdateLayout();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }
}
