using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Debugging;

public sealed class TargetChannelProvider(
    SettingsManager settingsManager,
    DebugChannelStore debugChannelStore,
    DebugChannelOverride commandLine,
    ILogger<TargetChannelProvider>? logger = null,
    TimeProvider? timeProvider = null)
    : ITargetChannelProvider
{
    private enum TargetChannelSource
    {
        None = 0,
        CommandLine = 1,
        DebugChannelStore = 2,
        OwnChannel = 3,
        OwnChannelAfterInvalidValue = 4,
    }

    private readonly object _syncLock = new();

    private TargetChannelState? _reported;
    private TargetChannelState? _session;

    public TargetChannelState Current
    {
        get
        {
            lock (_syncLock)
            {
                if (_session is { } pinned)
                {
                    return pinned;
                }
            }

            var (state, rejected, source) = Resolve();
            Report(state, rejected, source);
            return state;
        }
    }

    public void BeginSession()
    {
        var (state, rejected, source) = Resolve();
        Report(state, rejected, source);

        lock (_syncLock)
        {
            _session = state;
        }

        if (state.IsForeign)
        {
            RememberRecent(state.Login);
        }
    }

    public void EndSession()
    {
        lock (_syncLock)
        {
            _session = null;
        }
    }

    private (TargetChannelState State, string? Rejected, TargetChannelSource Source) Resolve()
    {
        var own = settingsManager.Current.Twitch.Channel ?? string.Empty;
        var isolated = AppPaths.IsBaseDirectoryOverridden;

        if (commandLine.Channel is { Length: > 0 } fromCommandLine)
        {
            return (new(fromCommandLine, own, true, commandLine.AllowSending, isolated), null, TargetChannelSource.CommandLine);
        }

        if (commandLine.RejectedChannel is { } rejectedArgument)
        {
            return (new(own, own, true, commandLine.AllowSending, isolated), rejectedArgument, TargetChannelSource.OwnChannelAfterInvalidValue);
        }

        var stored = debugChannelStore.Load();

        if (!stored.IsEnabled)
        {
            return (new(own, own, false, true, isolated), null, TargetChannelSource.OwnChannel);
        }

        if (ChannelLogin.TryNormalize(stored.Channel, out var fromSettings))
        {
            return (new(fromSettings, own, true, stored.AllowSending || commandLine.AllowSending, isolated), null, TargetChannelSource.DebugChannelStore);
        }

        return (new(own, own, true, commandLine.AllowSending, isolated), stored.Channel ?? string.Empty, TargetChannelSource.OwnChannelAfterInvalidValue);
    }

    private void RememberRecent(string login)
    {
        try
        {
            debugChannelStore.RecordRecent(login, (timeProvider ?? TimeProvider.System).GetUtcNow());
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Канал {Channel} не записан в недавние каналы отладки", login);
        }
    }

    private static string DescribeSource(TargetChannelSource source)
    {
        return source switch
        {
            TargetChannelSource.CommandLine => "аргумент --debug-channel",
            TargetChannelSource.DebugChannelStore => "настройка отладки debug-channel.json",
            TargetChannelSource.OwnChannelAfterInvalidValue => "свой канал из настроек после нераспознанного значения",
            _ => "свой канал из настроек",
        };
    }

    private void Report(TargetChannelState state, string? rejected, TargetChannelSource source)
    {
        lock (_syncLock)
        {
            if (_reported == state)
            {
                return;
            }

            _reported = state;

            if (rejected is not null)
            {
                logger?.LogWarning("Отладка включена, но канал {Value} не распознан: бот остаётся на канале {OwnChannel} и молчит",
                    rejected.Length == 0 ? "(значение не указано)" : rejected,
                    state.OwnChannel);
            }

            if (!state.IsDebugSession)
            {
                logger?.LogInformation("Канал бота {Channel} выбран по источнику: {Source}", state.Login, DescribeSource(source));
                return;
            }

            logger?.LogWarning("Режим отладки: бот работает на канале {Channel} (свой канал {OwnChannel}, источник: {Source}), отправка сообщений {Sending}, чужой канал {IsForeign}",
                state.Login,
                state.OwnChannel,
                DescribeSource(source),
                state.IsSendingAllowed ? "разрешена" : "выключена",
                state.IsForeign);
        }
    }
}
