using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Wpf.Bootstrap;

namespace PoproshaykaBot.Wpf.Mcp;

public sealed class BotAutomation(
    IBotConnectionController connectionManager,
    ITargetChannelProvider targetChannelProvider,
    IStreamStatus streamStatus,
    IHttpServerStatus httpServer,
    IObsSceneController obsIntegration,
    SettingsManager settingsManager,
    ILogger<BotAutomation> logger)
{
    public BotStateDto GetState()
    {
        var target = targetChannelProvider.Current;
        var stream = streamStatus.CurrentStream;
        var obs = obsIntegration.CurrentStatus;
        var port = httpServer.Port ?? settingsManager.Current.Twitch.HttpServerPort;

        return new BotStateDto(
            AppInfo.Version,
            connectionManager.CurrentPhase,
            connectionManager.IsBusy,
            new BotStateDto.TargetChannelDto(
                target.Login,
                target.OwnChannel,
                target.IsDebugSession,
                target.IsForeign,
                target.IsSendingAllowed,
                target.IsProfileIsolated),
            new BotStateDto.StreamStateDto(
                streamStatus.CurrentStatus,
                stream?.Title,
                stream?.GameName,
                stream?.ViewerCount,
                streamStatus.CurrentStatus == StreamStatus.Online ? stream?.StartedAt : null),
            new BotStateDto.HttpServerStateDto(httpServer.IsRunning, port, httpServer.IsRunning ? $"http://localhost:{port}/chat" : null),
            new BotStateDto.ObsStateDto(obs.IsConnected, obs.ObsVersion, obs.ErrorMessage));
    }

    public BotLifecyclePhase Connect()
    {
        var phase = connectionManager.CurrentPhase;

        if (phase is BotLifecyclePhase.Connecting or BotLifecyclePhase.Connected)
        {
            throw new InvalidOperationException($"Бот уже в фазе {phase} – подключать нечего.");
        }

        if (phase == BotLifecyclePhase.Disconnecting)
        {
            throw new InvalidOperationException("Бот сейчас отключается – дождитесь фазы Disconnected и повторите.");
        }

        logger.McpBotConnectRequested();

        try
        {
            connectionManager.StartConnection();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException("Подключение уже запущено кем-то другим – проверьте фазу через get_bot_state.", exception);
        }

        return connectionManager.CurrentPhase;
    }

    public async Task<BotLifecyclePhase> DisconnectAsync()
    {
        var phase = connectionManager.CurrentPhase;

        if (phase == BotLifecyclePhase.Connecting)
        {
            logger.McpBotDisconnectRequested();
            connectionManager.CancelConnection();
            await connectionManager.WaitForConnectionAsync();

            phase = connectionManager.CurrentPhase;

            if (phase != BotLifecyclePhase.Connected)
            {
                return phase;
            }
        }
        else if (phase == BotLifecyclePhase.Connected)
        {
            logger.McpBotDisconnectRequested();
        }
        else
        {
            throw new InvalidOperationException($"Бот в фазе {phase} – отключать нечего.");
        }

        await connectionManager.StopAsync(BotStopMode.Graceful);

        return connectionManager.CurrentPhase;
    }
}
