using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.GameArt;
using PoproshaykaBot.Core.Update;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PoproshaykaBot.Core.Infrastructure.Runtime;

public static class StartupReport
{
    public static void LogEnvironment(ILogger logger, IReadOnlyList<string> arguments, string? build = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(arguments);

        logger.LogInformation("Запуск версии {Version}{Build}: ОС {OperatingSystem}, среда {Framework}, процесс {ProcessId}, аргументы {Arguments}",
            ResolveVersion(),
            string.IsNullOrWhiteSpace(build) ? string.Empty : $" ({build.Trim()})",
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            Environment.ProcessId,
            arguments.Count == 0 ? "–" : string.Join(' ', arguments));

        logger.LogInformation("Хранение данных: режим {Mode}, базовая директория {BaseDirectory}, mutex экземпляра {MutexName}",
            ResolveStorageMode(),
            AppPaths.BaseDirectory,
            SingleInstanceGate.BuildMutexName());
    }

    public static void LogConfiguration(ILogger logger, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(services);

        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        if (services.GetService<IUpdateEnvironment>() is { } updateEnvironment)
        {
            logger.LogInformation("Обновления: тип сборки {UpdateKind}, репозиторий {Repository}, архитектура {Architecture}",
                updateEnvironment.Kind,
                updateEnvironment.RepositorySlug,
                updateEnvironment.ArchitectureMoniker);
        }

        if (services.GetService<SettingsManager>() is { } settingsManager)
        {
            LogSnapshot(logger, nameof(AppSettings), SettingsDescriber.Describe(settingsManager.Current));
        }

        if (services.GetService<AccountsStore>() is { } accountsStore)
        {
            LogSnapshot(logger, "Account.Bot", SettingsDescriber.Describe(accountsStore.Load(TwitchOAuthRole.Bot)));
            LogSnapshot(logger, "Account.Broadcaster", SettingsDescriber.Describe(accountsStore.Load(TwitchOAuthRole.Broadcaster)));
        }

        if (services.GetService<ObsIntegrationStore>() is { } obsIntegrationStore)
        {
            LogSnapshot(logger, "ObsIntegrationSettings", SettingsDescriber.Describe(obsIntegrationStore.Load()));
        }

        if (services.GetService<ObsChatStore>() is { } obsChatStore)
        {
            LogSnapshot(logger, "ObsChatSettings", SettingsDescriber.Describe(obsChatStore.Load()));
        }

        if (services.GetService<UpdateStore>() is { } updateStore)
        {
            LogSnapshot(logger, "UpdateSettings", SettingsDescriber.Describe(updateStore.Load()));
        }

        if (services.GetService<DebugChannelStore>() is { } debugChannelStore)
        {
            LogSnapshot(logger, "DebugChannelSettings", SettingsDescriber.Describe(debugChannelStore.Load()));
        }

        if (services.GetService<BroadcastProfilesStore>() is { } broadcastProfilesStore)
        {
            LogSnapshot(logger, "BroadcastProfilesSettings", SettingsDescriber.Describe(broadcastProfilesStore.Load()));
        }

        if (services.GetService<PollsStore>() is { } pollsStore)
        {
            LogSnapshot(logger, "PollsSettings", SettingsDescriber.Describe(pollsStore.Load()));
        }

        if (services.GetService<CommandSettingsStore>() is { } commandSettingsStore)
        {
            LogSnapshot(logger, "CommandSettings", SettingsDescriber.Describe(commandSettingsStore.Load()));
        }

        if (services.GetService<IGameBoxArtCache>() is { } boxArtCache)
        {
            LogBoxArtCache(logger, boxArtCache.GetStatus());
        }
    }

    private static void LogBoxArtCache(ILogger logger, GameBoxArtCacheStatus status)
    {
        logger.LogInformation(
            "Кеш обложек игр: каталог {Directory}, обложек {Images}, неизвестных категорий {Misses}, "
            + "повтор запроса через {MissLifetimeDays} д, только из кеша {CacheOnly}",
            status.Directory,
            status.ImageCount,
            status.MissCount,
            (int)status.MissLifetime.TotalDays,
            status.CacheOnly ? "да" : "нет");
    }

    private static void LogSnapshot(ILogger logger, string store, string description)
    {
        logger.LogInformation("Настройки {Store}: {Description}", store, description);
    }

    private static string ResolveStorageMode()
    {
        if (AppPaths.IsBaseDirectoryOverridden)
        {
            return "override";
        }

        return AppPaths.IsPortable ? "portable" : "AppData";
    }

    private static string ResolveVersion()
    {
        var assembly = Assembly.GetEntryAssembly();

        if (assembly is null)
        {
            return "–";
        }

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational;
        }

        var location = assembly.Location;

        return string.IsNullOrEmpty(location)
            ? assembly.GetName().Version?.ToString() ?? "–"
            : FileVersionInfo.GetVersionInfo(location).FileVersion ?? "–";
    }
}
