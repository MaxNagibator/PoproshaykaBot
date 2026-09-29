using PoproshaykaBot.Core.Twitch.EventSub;
using System.Net.WebSockets;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Twitch.EventSub;

[TestFixture]
public sealed class TwitchEventSubClientMigrationTests
{
    [Test]
    public async Task SessionReconnect_OpensSecondSocketAndKeepsSessionAlive()
    {
        await using var first = new FakeEventSubEndpoint();
        await using var second = new FakeEventSubEndpoint();
        await using var client = new TwitchEventSubClient(NullLogger<TwitchEventSubClient>.Instance)
        {
            BaseUrl = first.Uri.OriginalString,
        };

        var welcomes = new List<EventSubSessionWelcomeArgs>();
        var reconnects = new List<EventSubReconnectArgs>();
        var disconnects = new List<EventSubDisconnectedArgs>();
        var welcomeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reconnectSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notificationSignal = new TaskCompletionSource<EventSubNotificationArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        client.OnSessionWelcome += (args, _) =>
        {
            welcomes.Add(args);
            welcomeSignal.TrySetResult();
            return Task.CompletedTask;
        };

        client.OnSessionReconnect += (args, _) =>
        {
            reconnects.Add(args);
            reconnectSignal.TrySetResult();
            return Task.CompletedTask;
        };

        client.OnDisconnected += (args, _) =>
        {
            disconnects.Add(args);
            return Task.CompletedTask;
        };

        client.OnNotification += (args, _) =>
        {
            notificationSignal.TrySetResult(args);
            return Task.CompletedTask;
        };

        await client.StartAsync();

        await first.Connected.WaitAsync(Timeout);
        await first.SendAsync(SessionMessage("session_welcome", "session-1"));
        await welcomeSignal.Task.WaitAsync(Timeout);

        await first.SendAsync(SessionMessage("session_reconnect", "session-1", second.Uri.OriginalString));

        await second.Connected.WaitAsync(Timeout);
        await second.SendAsync(SessionMessage("session_welcome", "session-2"));
        await reconnectSignal.Task.WaitAsync(Timeout);

        var firstCloseStatus = await first.ClosedByClient.WaitAsync(Timeout);

        await second.SendAsync(NotificationMessage());
        var notification = await notificationSignal.Task.WaitAsync(Timeout);

        Assert.Multiple(() =>
        {
            Assert.That(welcomes, Has.Count.EqualTo(1),
                "session_welcome на сокете миграции не должен выглядеть как новая сессия – иначе подписки пересоздаются");

            Assert.That(reconnects, Has.Count.EqualTo(1));
            Assert.That(reconnects[0].OldSessionId, Is.EqualTo("session-1"));
            Assert.That(reconnects[0].NewSessionId, Is.EqualTo("session-2"));
            Assert.That(reconnects[0].ReconnectUrl, Is.EqualTo(second.Uri.OriginalString));
            Assert.That(client.SessionId, Is.EqualTo("session-2"));
            Assert.That(firstCloseStatus, Is.EqualTo(WebSocketCloseStatus.NormalClosure),
                "старый сокет закрывает клиент сразу после welcome на новом");

            Assert.That(disconnects, Is.Empty, "закрытие старого сокета при миграции не является разрывом");
            Assert.That(notification.SubscriptionType, Is.EqualTo("stream.online"));
        });
    }

    [Test]
    public async Task MigrationSocketClosedBeforeWelcome_ReportsDisconnectOnlyAfterOldSocketDies()
    {
        await using var first = new FakeEventSubEndpoint();
        await using var second = new FakeEventSubEndpoint();
        await using var client = new TwitchEventSubClient(NullLogger<TwitchEventSubClient>.Instance)
        {
            BaseUrl = first.Uri.OriginalString,
        };

        var disconnects = new List<EventSubDisconnectedArgs>();
        var welcomeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnectSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        client.OnSessionWelcome += (_, _) =>
        {
            welcomeSignal.TrySetResult();
            return Task.CompletedTask;
        };

        client.OnDisconnected += (args, _) =>
        {
            disconnects.Add(args);
            disconnectSignal.TrySetResult();
            return Task.CompletedTask;
        };

        await client.StartAsync();

        await first.Connected.WaitAsync(Timeout);
        await first.SendAsync(SessionMessage("session_welcome", "session-1"));
        await welcomeSignal.Task.WaitAsync(Timeout);

        await first.SendAsync(SessionMessage("session_reconnect", "session-1", second.Uri.OriginalString));

        await second.Connected.WaitAsync(Timeout);
        await second.CloseFromServerAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        Assert.Multiple(() =>
        {
            Assert.That(disconnects, Is.Empty, "несостоявшаяся миграция не разрывает живой первый сокет");
            Assert.That(client.SessionId, Is.EqualTo("session-1"));
        });

        await first.CloseFromServerAsync();
        await disconnectSignal.Task.WaitAsync(Timeout);
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        Assert.That(disconnects, Has.Count.EqualTo(1));
    }

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static string SessionMessage(string messageType, string sessionId, string? reconnectUrl = null)
    {
        return Envelope(messageType,
            null,
            new
            {
                session = new
                {
                    id = sessionId,
                    status = reconnectUrl is null ? "connected" : "reconnecting",
                    keepalive_timeout_seconds = 60,
                    reconnect_url = reconnectUrl,
                    connected_at = DateTime.UtcNow,
                },
            });
    }

    private static string NotificationMessage()
    {
        return Envelope("notification",
            "stream.online",
            new
            {
                @event = new
                {
                    id = "stream-1",
                    broadcaster_user_id = "12345",
                },
            });
    }

    private static string Envelope(string messageType, string? subscriptionType, object payload)
    {
        return JsonSerializer.Serialize(new
        {
            metadata = new
            {
                message_id = Guid.NewGuid().ToString("N"),
                message_type = messageType,
                message_timestamp = DateTime.UtcNow,
                subscription_type = subscriptionType,
                subscription_version = subscriptionType is null ? null : "1",
            },
            payload,
        });
    }
}
