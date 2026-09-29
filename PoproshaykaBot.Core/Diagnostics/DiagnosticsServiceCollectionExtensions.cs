using Microsoft.Extensions.DependencyInjection;

namespace PoproshaykaBot.Core.Diagnostics;

public static class DiagnosticsServiceCollectionExtensions
{
    public static IServiceCollection AddDiagnostics(this IServiceCollection services)
    {
        services.AddSingleton<MemoryUsageSink>();
        services.AddSingleton<DiagnosticsSnapshotSource>();

        return services;
    }
}
