using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Obs;

internal sealed class ObsIntegrationLifetimeAdapter(
    ObsIntegrationStore store,
    ObsIntegrationService integration,
    ILogger<ObsIntegrationLifetimeAdapter> logger)
    : IAppLifetimeComponent
{
    public string Name => "OBS интеграция";

    public int StartOrder => 10;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = store.Load();
        if (!settings.Enabled || !settings.AutoConnect)
        {
            logger.LogInformation("OBS интеграция отключена или автоподключение выключено");
            return;
        }

        var snapshot = await integration.ConnectAsync(settings, cancellationToken).ConfigureAwait(false);
        if (!snapshot.IsConnected)
        {
            logger.LogWarning("OBS интеграция не подключена: {Message}", snapshot.ErrorMessage);
            return;
        }

        if (!settings.AutoProvisionBrowserSource)
        {
            return;
        }

        try
        {
            await integration.ProvisionBrowserSourceAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось автоматически создать или обновить Browser Source в OBS");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return integration.DisconnectAsync(cancellationToken);
    }
}
