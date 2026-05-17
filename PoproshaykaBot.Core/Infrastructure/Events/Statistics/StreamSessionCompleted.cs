using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Core.Infrastructure.Events.Statistics;

public sealed record StreamSessionCompleted(StreamSessionRecord Session) : EventBase;
