using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Statistics;
using Serilog;

namespace PoproshaykaBot.Wpf.Bootstrap;

public static class StatisticsBootstrap
{
    public static void LoadUserStatistics(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Task.Run(() => LoadUserStatisticsAsync(services)).GetAwaiter().GetResult();
    }

    public static async Task LoadUserStatisticsAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        try
        {
            await services.GetRequiredService<UserStatisticsLoader>().EnsureLoadedAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Log.ForContext(typeof(StatisticsBootstrap))
                .Warning(exception, "Статистика пользователей не прочитана – страница «Пользователи» останется пустой");
        }
    }
}
