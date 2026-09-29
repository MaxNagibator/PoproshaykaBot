using KeepShell.Bootstrap;
using KeepShell.Testing;
using PoproshaykaBot.Wpf.Bootstrap;
using System.Windows;

namespace PoproshaykaBot.Wpf.Tests.Settings;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class ShortFieldWidthTests
{
    private static readonly string[] Dictionaries =
    [
        "pack://application:,,,/KeepShell;component/Resources/Themes/Light.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Themes/Tokens.xaml",
    ];

    [OneTimeSetUp]
    public void EnsureApplication()
    {
        TestResources.Ensure(Dictionaries);
    }

    [TestCase(1.0, 240)]
    [TestCase(1.6, 384)]
    [TestCase(0.8, 192)]
    public void Short_field_width_follows_the_font_scale(double scale, double expected)
    {
        AppScaledSizes.Register(Application.Current);

        try
        {
            FontScaleManager.Apply(scale);

            Assert.That(Application.Current.Resources[AppScaledSizes.ShortFieldWidthKey], Is.EqualTo(expected),
                "Короткое поле настроек обязано расти вместе со шрифтом, иначе при 1.6 слаг репозитория уходит в прокрутку внутри поля.");
        }
        finally
        {
            FontScaleManager.Apply(FontScaleManager.DefaultScale);
        }
    }
}
