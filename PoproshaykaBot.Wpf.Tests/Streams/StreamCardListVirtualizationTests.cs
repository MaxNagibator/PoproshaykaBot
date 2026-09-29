using PoproshaykaBot.Wpf.Views;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace PoproshaykaBot.Wpf.Tests.Streams;

[TestFixture]
public class StreamCardListVirtualizationTests
{
    private const int CardCount = 317;

    private const string CardTemplate = """
        <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                      xmlns:infrastructure='clr-namespace:PoproshaykaBot.Wpf.Infrastructure;assembly=PoproshaykaBot.Wpf'>
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height='*' />
                    <RowDefinition Height='Auto' />
                </Grid.RowDefinitions>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='Auto' />
                    <ColumnDefinition Width='*' />
                    <ColumnDefinition Width='Auto' />
                </Grid.ColumnDefinitions>

                <Border Width='52' Height='72' />

                <TextBlock Grid.Column='1' Text='{Binding}' />

                <StackPanel Grid.Column='2' Orientation='Horizontal'>
                    <TextBlock Text='{Binding}' />
                </StackPanel>

                <ItemsControl Grid.Row='1' Grid.ColumnSpan='3' Height='6' ItemsSource='{Binding}'>
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate>
                            <infrastructure:ProportionalPanel Gap='2' />
                        </ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                </ItemsControl>
            </Grid>
        </DataTemplate>
        """;

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Список_карточек_материализует_только_видимые()
    {
        var list = new ListBox
        {
            ItemsSource = Enumerable.Range(1, CardCount).Select(number => number.ToString("N0", CultureInfo.InvariantCulture)).ToList(),
            ItemTemplate = (DataTemplate)XamlReader.Parse(CardTemplate),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemContainerStyle = new(typeof(ListBoxItem))
            {
                Setters =
                {
                    new Setter(FrameworkElement.MinHeightProperty, StreamHistoryPageView.CardHeight),
                    new Setter(Control.PaddingProperty, new Thickness(12, 8, 12, 8)),
                },
            },
        };

        VirtualizingStackPanel.SetIsVirtualizing(list, true);
        VirtualizingStackPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(list, true);

        var host = new Grid { Children = { list } };

        host.Measure(new(1024, 420));
        host.Arrange(new(0, 0, 1024, 420));
        host.UpdateLayout();

        var panel = FindPanel(list);

        Assert.Multiple(() =>
        {
            Assert.That(panel, Is.Not.Null, "Панель списка карточек обязана быть виртуализующей.");
            Assert.That(panel!.Children.Count, Is.LessThan(CardCount / 4),
                "Шаблон карточки не должен заставлять список материализовать все 317 сессий стенда.");
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
