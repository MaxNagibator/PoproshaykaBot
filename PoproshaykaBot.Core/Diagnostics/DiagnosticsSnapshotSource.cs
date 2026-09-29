using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Broadcast;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.Chat;
using PoproshaykaBot.Core.Twitch.EventSub;

namespace PoproshaykaBot.Core.Diagnostics;

public sealed class DiagnosticsSnapshotSource(
    MemoryUsageSink memoryUsageSink,
    EventBusMetrics eventBusMetrics,
    [FromKeyedServices(TwitchEndpoints.EventSubBotSession)]
    EventSubConnectionHost eventSubBotSession,
    [FromKeyedServices(TwitchEndpoints.EventSubBroadcasterSession)]
    EventSubConnectionHost eventSubBroadcasterSession,
    IBotConnectionController botConnection,
    ITargetChannelProvider targetChannelProvider,
    ChatIngestionService chatIngestion,
    ChatSender chatSender,
    ObsIntegrationService obs,
    SseService sse,
    SseClientRegistry sseClients,
    BroadcastScheduler broadcastScheduler,
    StatisticsAutoSaver statisticsAutoSaver,
    ILogger<DiagnosticsSnapshotSource> logger)
{
    public DiagnosticsSnapshot Capture()
    {
        var eventSub = new[]
            {
                ReadEventSub(eventSubBotSession),
                ReadEventSub(eventSubBroadcasterSession),
            }
            .OfType<EventSubStatus>()
            .ToArray();

        var jobs = new[]
            {
                Read(nameof(ScheduledJob.Broadcast), CaptureBroadcastJob),
                Read(nameof(ScheduledJob.StatisticsAutoSave), CaptureAutoSaveJob),
            }
            .OfType<ScheduledJobStatus>()
            .ToArray();

        return new(DateTimeOffset.UtcNow,
            memoryUsageSink.Last,
            eventSub,
            Read(nameof(ChatConnectionStatus), CaptureChat),
            Read(nameof(ObsConnectionStatus), CaptureObs),
            Read(nameof(SseStatus), CaptureSse),
            Read(nameof(ChatQueueStatus), CaptureChatQueue),
            jobs,
            Read(nameof(EventBusStatistics), eventBusMetrics.Snapshot));
    }

    private static ConnectionState ToConnectionState(StreamMonitoringStatus? status)
    {
        return status switch
        {
            StreamMonitoringStatus.Connecting => ConnectionState.Connecting,
            StreamMonitoringStatus.Connected => ConnectionState.Connected,
            StreamMonitoringStatus.Reconnecting => ConnectionState.Reconnecting,
            StreamMonitoringStatus.Disconnected => ConnectionState.Disconnected,
            StreamMonitoringStatus.Failed => ConnectionState.Failed,
            _ => ConnectionState.Unknown,
        };
    }

    private static EventSubStatus CaptureEventSub(EventSubConnectionHost host)
    {
        return new(host.Role,
            ToConnectionState(host.CurrentStatus),
            host.LastMessageAt,
            host.ReconnectAttempt,
            host.ReconnectLimit);
    }

    private EventSubStatus? ReadEventSub(EventSubConnectionHost host)
    {
        return Read($"EventSub ({host.Role})", () => CaptureEventSub(host));
    }

    private T? Read<T>(string source, Func<T> capture)
        where T : class
    {
        try
        {
            return capture();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Диагностика: источник {Source} не отдал данные", source);
            return null;
        }
    }

    private ChatConnectionStatus CaptureChat()
    {
        var phase = botConnection.CurrentPhase;

        var state = phase switch
        {
            BotLifecyclePhase.Connecting => ConnectionState.Connecting,
            BotLifecyclePhase.Connected => chatIngestion.IsJoined ? ConnectionState.Connected : ConnectionState.Connecting,
            BotLifecyclePhase.Disconnecting => ConnectionState.Disconnected,
            BotLifecyclePhase.Disconnected => ConnectionState.Disconnected,
            BotLifecyclePhase.Cancelled => ConnectionState.Disconnected,
            BotLifecyclePhase.Failed => ConnectionState.Failed,
            _ => ConnectionState.Unknown,
        };

        var channel = targetChannelProvider.Current.Login;

        return new(state,
            string.IsNullOrWhiteSpace(channel) ? null : channel,
            chatIngestion.JoinedAt,
            chatIngestion.LastMessageAt);
    }

    private ObsConnectionStatus CaptureObs()
    {
        return new(ResolveObsState(), obs.LastExchangeAt);
    }

    private ConnectionState ResolveObsState()
    {
        if (obs.IsConnected)
        {
            return ConnectionState.Connected;
        }

        if (obs.CurrentStatus.ErrorMessage is not null)
        {
            return ConnectionState.Failed;
        }

        return obs.IsStatusObserved ? ConnectionState.Disconnected : ConnectionState.Unknown;
    }

    private SseStatus CaptureSse()
    {
        return new(sse.IsRunning, sseClients.Count, sse.DroppedMessageCount);
    }

    private ChatQueueStatus CaptureChatQueue()
    {
        return new(chatSender.QueueLength,
            chatSender.QueueMaxLength,
            chatSender.SentCount,
            chatSender.FailedCount,
            chatSender.LastSentAt);
    }

    private ScheduledJobStatus CaptureBroadcastJob()
    {
        var nextRunAt = broadcastScheduler.NextBroadcastTime is { } next
            ? new DateTimeOffset(DateTime.SpecifyKind(next, DateTimeKind.Local))
            : (DateTimeOffset?)null;

        return new(ScheduledJob.Broadcast,
            broadcastScheduler.Interval,
            nextRunAt,
            broadcastScheduler.LastBroadcastAt,
            broadcastScheduler.LastError);
    }

    private ScheduledJobStatus CaptureAutoSaveJob()
    {
        return new(ScheduledJob.StatisticsAutoSave,
            statisticsAutoSaver.Interval,
            statisticsAutoSaver.NextRunAt,
            statisticsAutoSaver.LastRunAt,
            statisticsAutoSaver.LastError);
    }
}
