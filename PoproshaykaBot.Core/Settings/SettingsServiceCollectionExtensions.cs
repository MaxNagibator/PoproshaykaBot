using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Settings.Onboarding;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Settings;

public static class SettingsServiceCollectionExtensions
{
    public static IServiceCollection AddSettingsStores(this IServiceCollection services)
    {
        services.AddSingleton<SettingsManager>();
        services.AddSingleton<AccountsStore>();
        services.AddSingleton<BroadcastProfilesStore>();
        services.AddSingleton<PollsStore>();
        services.AddSingleton<RecentCategoriesStore>();
        services.AddSingleton<DashboardLayoutStore>();
        services.AddSingleton<ObsChatStore>();
        services.AddSingleton<ObsIntegrationStore>();
        services.AddSingleton<OnboardingChecklist>();
        return services;
    }
}
