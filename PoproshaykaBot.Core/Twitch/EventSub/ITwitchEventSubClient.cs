using System.Text.Json;

namespace PoproshaykaBot.Core.Twitch.EventSub;

public delegate Task EventSubAsyncHandler<in TArgs>(TArgs args, CancellationToken cancellationToken);

public interface ITwitchEventSubClient
{
    event EventSubAsyncHandler<EventSubSessionWelcomeArgs> OnSessionWelcome;

    event EventSubAsyncHandler<EventSubNotificationArgs> OnNotification;

    event EventSubAsyncHandler<EventSubReconnectArgs> OnSessionReconnect;

    event EventSubAsyncHandler<EventSubRevocationArgs> OnRevocation;

    event EventSubAsyncHandler<EventSubDisconnectedArgs> OnDisconnected;
    string? SessionId { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

public sealed record EventSubSessionWelcomeArgs(string SessionId, int? KeepaliveTimeoutSeconds);

public sealed record EventSubNotificationArgs(
    string SubscriptionType,
    string SubscriptionVersion,
    string MessageId,
    DateTime MessageTimestamp,
    JsonElement Payload);

public sealed record EventSubReconnectArgs(string ReconnectUrl, string OldSessionId, string NewSessionId);

public sealed record EventSubRevocationArgs(string SubscriptionId, string SubscriptionType, string Status);

public sealed record EventSubDisconnectedArgs(string Reason);
