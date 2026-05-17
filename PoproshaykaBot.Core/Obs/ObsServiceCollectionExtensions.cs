using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Infrastructure.Hosting;

namespace PoproshaykaBot.Core.Obs;

public static class ObsServiceCollectionExtensions
{
    public static IServiceCollection AddObsIntegration(this IServiceCollection services)
    {
        services.AddSingleton<IObsWebSocketClient, ObsWebSocketClient>();
        services.AddSingleton<ObsIntegrationService>();
        services.AddSingleton<IObsSceneController>(sp => sp.GetRequiredService<ObsIntegrationService>());
        services.AddSingleton<IAppLifetimeComponent, ObsIntegrationLifetimeAdapter>();
        services.AddSingleton<IAppLifetimeComponent, ObsSceneEventBridge>();

        return services;
    }
}
