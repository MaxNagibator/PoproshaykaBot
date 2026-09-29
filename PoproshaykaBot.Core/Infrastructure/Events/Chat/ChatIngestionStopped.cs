namespace PoproshaykaBot.Core.Infrastructure.Events.Chat;

public sealed record ChatIngestionStopped(DateTimeOffset At) : EventBase;
