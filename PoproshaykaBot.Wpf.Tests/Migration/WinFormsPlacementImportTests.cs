using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.Tests.Migration;

[TestFixture]
public class WinFormsPlacementImportTests
{
    [TestCase(1.0, 1600, 900)]
    [TestCase(1.25, 1280, 720)]
    [TestCase(1.5, 1066.6666666666667, 600)]
    public void ToDeviceIndependent_divides_pixels_by_scale(double scale, double expectedWidth, double expectedHeight)
    {
        var saved = new MainWindowSettings { X = 200, Y = 100, Width = 1600, Height = 900 };

        var bounds = WinFormsPlacementImport.ToDeviceIndependent(saved, scale);

        Assert.Multiple(() =>
        {
            Assert.That(bounds.Width, Is.EqualTo(expectedWidth).Within(0.001));
            Assert.That(bounds.Height, Is.EqualTo(expectedHeight).Within(0.001));
            Assert.That(bounds.X, Is.EqualTo(200 / scale).Within(0.001));
            Assert.That(bounds.Y, Is.EqualTo(100 / scale).Within(0.001));
        });
    }

    [TestCase(0.0)]
    [TestCase(-2.0)]
    [TestCase(double.NaN)]
    public void ToDeviceIndependent_keeps_pixels_when_scale_is_not_usable(double scale)
    {
        var saved = new MainWindowSettings { X = -50, Y = 10, Width = 1280, Height = 720 };

        var bounds = WinFormsPlacementImport.ToDeviceIndependent(saved, scale);

        Assert.Multiple(() =>
        {
            Assert.That(bounds.X, Is.EqualTo(-50));
            Assert.That(bounds.Y, Is.EqualTo(10));
            Assert.That(bounds.Width, Is.EqualTo(1280));
            Assert.That(bounds.Height, Is.EqualTo(720));
        });
    }

    [Test]
    public void GetSystemScale_returns_positive_ratio()
    {
        Assert.That(WinFormsPlacementImport.GetSystemScale(), Is.GreaterThan(0));
    }
}
