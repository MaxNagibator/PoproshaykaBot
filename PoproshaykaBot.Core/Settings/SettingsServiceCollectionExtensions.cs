using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Onboarding;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Settings;

public static class SettingsServiceCollectionExtensions
{
    public static IServiceCollection AddSettingsStores(this IServiceCollection services)
    {
        services.AddSingleton<SettingsWriteGate>();
        services.AddSingleton<SettingsManager>();
        services.AddSingleton<AccountsStore>();
        services.AddSingleton<CommandSettingsStore>();
        services.AddSingleton<BroadcastProfilesStore>();
        services.AddSingleton<PollsStore>();
        services.AddSingleton<RecentCategoriesStore>();
        services.AddSingleton<DashboardLayoutStore>();
        services.AddSingleton<DashboardLayoutCoordinator>();
        services.AddSingleton<ObsChatStore>();
        services.AddSingleton<ObsIntegrationStore>();
        services.AddSingleton<UpdateStore>();
        services.AddSingleton<DebugChannelStore>();
        services.AddSingleton<OnboardingChecklist>();
        return services;
    }
}
