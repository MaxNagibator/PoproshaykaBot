using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Infrastructure.Hosting;

namespace PoproshaykaBot.Core.Statistics;

public static class StatisticsServiceCollectionExtensions
{
    public static IServiceCollection AddStatistics(this IServiceCollection services)
    {
        services.AddSingleton<UserStatisticsRepository>();
        services.AddSingleton<IUserStatisticsRepository>(sp => sp.GetRequiredService<UserStatisticsRepository>());

        services.AddSingleton<BotStatisticsRepository>();
        services.AddSingleton<IBotStatisticsRepository>(sp => sp.GetRequiredService<BotStatisticsRepository>());

        services.AddSingleton<CommandUsageRepository>();

        services.AddSingleton<StatisticsFileStore>();
        services.AddSingleton<StreamSessionHistoryStore>();
        services.AddSingleton<ActiveStreamSessionStore>();

        services.AddSingleton<UserStatisticsLoader>();

        services.AddSingleton<StatisticsAutoSaver>();
        services.AddSingleton<IHostedComponent>(sp => sp.GetRequiredService<StatisticsAutoSaver>());

        return services;
    }
}
