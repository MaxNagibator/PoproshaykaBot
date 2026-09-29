using Microsoft.Extensions.DependencyInjection;

namespace PoproshaykaBot.Core.Debugging;

public static class DebuggingServiceCollectionExtensions
{
    public static IServiceCollection AddDebugChannel(this IServiceCollection services, DebugChannelOverride commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        services.AddSingleton(commandLine);
        services.AddSingleton<ITargetChannelProvider, TargetChannelProvider>();
        services.AddSingleton<ChannelLiveStatusReader>();
        return services;
    }
}
