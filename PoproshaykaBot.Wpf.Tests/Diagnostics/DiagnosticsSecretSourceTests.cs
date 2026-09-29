using KeepShell.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Wpf.Infrastructure.Diagnostics;
using System.IO;

namespace PoproshaykaBot.Wpf.Tests.Diagnostics;

[TestFixture]
public class DiagnosticsSecretSourceTests
{
    private const string ClientSecret = "client-secret-0123456789";
    private const string BotAccessToken = "bot-access-0123456789";
    private const string BotRefreshToken = "bot-refresh-0123456789";
    private const string BroadcasterAccessToken = "broadcaster-access-0123456789";
    private const string ObsPassword = "obs-password-0123456789";

    private string _directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PoproshaykaBot-secrets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void Живые_секреты_вымарываются_из_пакета()
    {
        var secrets = new DiagnosticsSecrets(DiagnosticsSecretRules.Merge([DiagnosticsSecretRules.Common, Create().Collect()]));

        var text = secrets.Redact(
            $"токен {BotAccessToken} уехал в строку журнала без имени поля"
            + Environment.NewLine
            + $"\"ClientSecret\": \"{ClientSecret}\", \"Password\": \"{ObsPassword}\", \"RefreshToken\": \"{BotRefreshToken}\""
            + Environment.NewLine
            + $"broadcaster={BroadcasterAccessToken}");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Not.Contain(ClientSecret));
            Assert.That(text, Does.Not.Contain(BotAccessToken), "Токен без имени поля ловится только по значению – ради этого источник их и отдаёт");
            Assert.That(text, Does.Not.Contain(BotRefreshToken));
            Assert.That(text, Does.Not.Contain(BroadcasterAccessToken));
            Assert.That(text, Does.Not.Contain(ObsPassword));
            Assert.That(text, Does.Contain("уехал в строку журнала"), "Вымарывание не должно съедать соседний текст");
        }
    }

    [Test]
    public void Отказ_одного_источника_не_уносит_прочитанные_секреты()
    {
        var settings = new ThrowingSettingsManager(Path.Combine(_directory, "settings.json"));
        var source = new BotDiagnosticsSecretSource(settings, CreateAccounts(), CreateObs(), NullLogger<BotDiagnosticsSecretSource>.Instance);

        var rules = source.Collect();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rules.Complete, Is.False, "Пакет не вправе обещать вымарывание, которого не было");
            Assert.That(rules.Values, Does.Contain(BotAccessToken), "Токен прочитался – его вымарывают, даже если соседний источник отказал");
            Assert.That(rules.Values, Does.Contain(BroadcasterAccessToken));
            Assert.That(rules.Values, Does.Contain(ObsPassword));
            Assert.That(rules.Values, Does.Not.Contain(ClientSecret));
        }
    }

    private BotDiagnosticsSecretSource Create()
    {
        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, Path.Combine(_directory, "settings.json"));

        settings.Mutate(current => current.Twitch.ClientSecret = ClientSecret);

        return new(settings, CreateAccounts(), CreateObs(), NullLogger<BotDiagnosticsSecretSource>.Instance);
    }

    private AccountsStore CreateAccounts()
    {
        var accounts = new AccountsStore(filePath: Path.Combine(_directory, "accounts.json"));

        accounts.SaveAll(
            new()
            {
                Login = "bot",
                AccessToken = BotAccessToken,
                RefreshToken = BotRefreshToken,
            },
            new()
            {
                Login = "broadcaster",
                AccessToken = BroadcasterAccessToken,
                RefreshToken = string.Empty,
            });

        return accounts;
    }

    private ObsIntegrationStore CreateObs()
    {
        var obs = new ObsIntegrationStore(new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance),
            filePath: Path.Combine(_directory, "obs-integration.json"));

        obs.Save(new ObsIntegrationSettings { Password = ObsPassword });

        return obs;
    }

    private sealed class ThrowingSettingsManager(string filePath)
        : SettingsManager(NullLogger<SettingsManager>.Instance, filePath)
    {
        public override AppSettings Current => throw new InvalidOperationException("настройки прочитать не удалось");
    }
}
