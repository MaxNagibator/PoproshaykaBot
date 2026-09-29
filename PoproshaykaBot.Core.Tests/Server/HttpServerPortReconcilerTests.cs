using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;

namespace PoproshaykaBot.Core.Tests.Server;

[TestFixture]
public class HttpServerPortReconcilerTests
{
    [TestCase("http://localhost:8080", 8080)]
    [TestCase("https://localhost", 443)]
    [TestCase("http://localhost", 80)]
    public void Reconcile_MatchingPort_KeepsSettingsUntouched(string redirectUri, int port)
    {
        var settingsManager = CreateSettingsManager(redirectUri, port);

        var result = HttpServerPortReconciler.Reconcile(settingsManager, NullLogger.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.Notice, Is.Null);
            Assert.That(settingsManager.Current.Twitch.HttpServerPort, Is.EqualTo(port));
        });

        settingsManager.DidNotReceive().Mutate(Arg.Any<Action<AppSettings>>());
    }

    [Test]
    public void Reconcile_PortConflict_RewritesServerPortAndSaves()
    {
        var settingsManager = CreateSettingsManager("http://localhost:3000", 8080);

        var result = HttpServerPortReconciler.Reconcile(settingsManager, NullLogger.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsResolved, Is.True);
            Assert.That(settingsManager.Current.Twitch.HttpServerPort, Is.EqualTo(3000));
            Assert.That(result.Notice, Is.Not.Null);
            Assert.That(result.Notice!.Severity, Is.EqualTo(PortReconcileSeverity.Information));
            Assert.That(result.Notice.Message, Does.Contain("3000").And.Contain("8080"));
        });

        settingsManager.Received(1).Mutate(Arg.Any<Action<AppSettings>>());
    }

    [Test]
    public void Закрытый_гейт_записи_не_выдаётся_за_сохранённый_порт()
    {
        var settingsManager = CreateSettingsManager("http://localhost:3000", 8080, written: false);

        var result = HttpServerPortReconciler.Reconcile(settingsManager, NullLogger.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsResolved, Is.True, "Сервер поднимается на новом порту и в этом запуске работает");
            Assert.That(settingsManager.Current.Twitch.HttpServerPort, Is.EqualTo(3000));
            Assert.That(result.Notice, Is.Not.Null);
            Assert.That(result.Notice!.Severity, Is.EqualTo(PortReconcileSeverity.Warning),
                "Незаписанный файл – это не рядовое уведомление");
            Assert.That(result.Notice.Message, Does.Contain("не записан").And.Contain("Перезапустите"));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not a uri")]
    public void Reconcile_InvalidRedirectUri_FailsWithoutSaving(string redirectUri)
    {
        var settingsManager = CreateSettingsManager(redirectUri, 8080);

        var result = HttpServerPortReconciler.Reconcile(settingsManager, NullLogger.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Notice, Is.Not.Null);
            Assert.That(result.Notice!.Severity, Is.EqualTo(PortReconcileSeverity.Error));
            Assert.That(settingsManager.Current.Twitch.HttpServerPort, Is.EqualTo(8080));
        });

        settingsManager.DidNotReceive().Mutate(Arg.Any<Action<AppSettings>>());
    }

    private static SettingsManager CreateSettingsManager(string redirectUri, int httpServerPort, bool written = true)
    {
        var settings = new AppSettings();
        settings.Twitch.RedirectUri = redirectUri;
        settings.Twitch.HttpServerPort = httpServerPort;

        var settingsManager = Substitute.For<SettingsManager>(NullLogger<SettingsManager>.Instance, null, null);
        settingsManager.Current.Returns(settings);

        settingsManager
            .Mutate(Arg.Any<Action<AppSettings>>())
            .Returns(call =>
            {
                call.Arg<Action<AppSettings>>()(settings);
                return written;
            });

        return settingsManager;
    }
}
