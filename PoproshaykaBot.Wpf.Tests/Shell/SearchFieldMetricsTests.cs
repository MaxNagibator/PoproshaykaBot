using KeepShell.Testing;
using System.Windows;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Tests.Shell;

[TestFixture]
public class SearchFieldMetricsTests
{
    private const double FieldWidth = 420;
    private const double PlaceholderLeft = 43;
    private const double ClearButtonMargin = 2;

    private static readonly Thickness SearchFieldPadding = new(10, 0, 32, 0);

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Текст_поиска_стоит_там_же_где_в_каркасном_поле()
    {
        var reference = Measure(null);
        var search = Measure(SearchFieldPadding);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(search.Caret, Is.EqualTo(reference.Caret).Within(0.5),
                "Локальный Padding не должен уводить каретку от места, где текст стоит в каркасном поле с иконкой");
            Assert.That(search.Caret, Is.EqualTo(PlaceholderLeft).Within(0.5),
                $"Плейсхолдер в UserStatisticsPageView.xaml стоит на Margin.Left = {PlaceholderLeft} и обязан совпасть с началом текста");
        }
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Правый_отступ_поиска_освобождает_место_кнопке_очистки()
    {
        var search = Measure(SearchFieldPadding);
        var textRight = search.ContentRight - SearchFieldPadding.Right;

        Assert.That(textRight, Is.LessThanOrEqualTo(FieldWidth - search.IconButtonWidth - ClearButtonMargin),
            "Текст поиска должен обрываться левее кнопки очистки, лежащей поверх поля");
    }

    private static (double Caret, double ContentRight, double IconButtonWidth) Measure(Thickness? padding)
    {
        PackScheme.Ensure();

        var root = new Grid();

        foreach (var source in new[]
                 {
                     "pack://application:,,,/KeepShell;component/Resources/Themes/Light.xaml",
                     "pack://application:,,,/KeepShell;component/Resources/Themes/Tokens.xaml",
                     "pack://application:,,,/KeepShell;component/Resources/Styles/Controls.xaml",
                 })
        {
            root.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source) });
        }

        var box = new TextBox
        {
            Style = (Style)root.FindResource("TextBox.IconField"),
            Text = "Найти",
            Width = FieldWidth,
            VerticalAlignment = VerticalAlignment.Top,
        };

        if (padding is { } value)
        {
            box.Padding = value;
        }

        root.Children.Add(box);
        root.Measure(new(FieldWidth, 200));
        root.Arrange(new(0, 0, FieldWidth, 200));
        root.UpdateLayout();

        var host = (FrameworkElement)box.Template.FindName("PART_ContentHost", box);
        var contentRight = host.TransformToAncestor(box).Transform(new(host.ActualWidth, 0)).X;

        return (box.GetRectFromCharacterIndex(0).X, contentRight, (double)root.FindResource("Control.Width.Icon"));
    }
}
