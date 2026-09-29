using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace PoproshaykaBot.Wpf.Tests.Users;

[TestFixture]
public class RatingListVirtualizationTests
{
    private const int RowCount = 421;
    private const string RowTemplate = """
        <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='Auto' SharedSizeGroup='RatingPosition' />
                    <ColumnDefinition Width='*' />
                    <ColumnDefinition Width='Auto' SharedSizeGroup='RatingPoints' />
                </Grid.ColumnDefinitions>
                <TextBlock Text='{Binding}' />
                <TextBlock Grid.Column='2' Text='{Binding}' />
            </Grid>
        </DataTemplate>
        """;

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Shared_size_scope_keeps_the_rating_list_virtualized()
    {
        var scope = new Grid();
        Grid.SetIsSharedSizeScope(scope, true);

        var list = new ListBox
        {
            ItemsSource = Enumerable.Range(1, RowCount).Select(number => number.ToString("N0", CultureInfo.InvariantCulture)).ToList(),
            ItemTemplate = (DataTemplate)XamlReader.Parse(RowTemplate),
            ItemContainerStyle = new(typeof(ListBoxItem))
            {
                Setters = { new Setter(FrameworkElement.HeightProperty, 40d) },
            },
        };

        VirtualizingStackPanel.SetIsVirtualizing(list, true);
        VirtualizingStackPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(list, true);

        scope.Children.Add(list);
        scope.Measure(new(600, 400));
        scope.Arrange(new(0, 0, 600, 400));
        scope.UpdateLayout();

        var panel = FindPanel(list);

        Assert.Multiple(() =>
        {
            Assert.That(panel, Is.Not.Null, "Панель списка рейтинга обязана быть виртуализующей.");
            Assert.That(panel!.Children.Count, Is.LessThan(RowCount / 4),
                "Общая группа ширин в шаблоне строки не должна заставлять список материализовать все записи.");
        });
    }

    private static VirtualizingStackPanel? FindPanel(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            if (child is VirtualizingStackPanel panel)
            {
                return panel;
            }

            if (FindPanel(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
