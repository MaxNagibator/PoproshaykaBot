using KeepShell.Diagnostics;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Wpf.Infrastructure.Diagnostics;

public sealed class BotDiagnosticsSecretSource(
    SettingsManager settings,
    AccountsStore accounts,
    ObsIntegrationStore obsIntegration,
    ILogger<BotDiagnosticsSecretSource> logger) : IDiagnosticsSecretSource
{
    private static readonly string[] BotKeyNames =
        ["clientsecret", "client_secret", "accesstoken", "access_token", "refreshtoken", "refresh_token", "password"];

    public DiagnosticsSecretRules Collect()
    {
        var values = new List<string>();
        var complete = Read(values, () => [settings.Current.Twitch.ClientSecret], "настройки Twitch");

        complete &= Read(values, () => [obsIntegration.Load().Password], "настройки OBS");

        foreach (var role in Enum.GetValues<TwitchOAuthRole>())
        {
            complete &= Read(values, () =>
            {
                var account = accounts.Load(role);

                return [account.AccessToken, account.RefreshToken];
            }, $"токены роли {role}");
        }

        return new(BotKeyNames, [.. values.Where(static value => !string.IsNullOrWhiteSpace(value))])
        {
            Complete = complete,
        };
    }

    private bool Read(List<string> values, Func<IReadOnlyList<string>> read, string source)
    {
        try
        {
            values.AddRange(read());

            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Секреты не собрались из источника {Source}, пакет диагностики будет помечен неполным", source);

            return false;
        }
    }
}
