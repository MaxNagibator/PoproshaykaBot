namespace PoproshaykaBot.Core.Infrastructure.Events.Update;

public sealed record UpdatePrepared(string Version) : EventBase;
