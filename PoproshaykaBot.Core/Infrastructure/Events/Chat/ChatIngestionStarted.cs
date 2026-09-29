namespace PoproshaykaBot.Core.Infrastructure.Events.Chat;

public sealed record ChatIngestionStarted(DateTimeOffset At) : EventBase;
