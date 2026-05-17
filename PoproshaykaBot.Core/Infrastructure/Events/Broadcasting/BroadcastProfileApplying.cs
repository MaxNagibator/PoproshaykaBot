using PoproshaykaBot.Core.Broadcast.Profiles;

namespace PoproshaykaBot.Core.Infrastructure.Events.Broadcasting;

public sealed record BroadcastProfileApplying(BroadcastProfile Profile) : EventBase;
