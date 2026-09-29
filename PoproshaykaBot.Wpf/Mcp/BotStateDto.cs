using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Streaming;

namespace PoproshaykaBot.Wpf.Mcp;

public sealed record BotStateDto(
    string AppVersion,
    BotLifecyclePhase Phase,
    bool IsBusy,
    BotStateDto.TargetChannelDto TargetChannel,
    BotStateDto.StreamStateDto Stream,
    BotStateDto.HttpServerStateDto HttpServer,
    BotStateDto.ObsStateDto Obs)
{
    public sealed record TargetChannelDto(
        string Login,
        string OwnChannel,
        bool IsDebugSession,
        bool IsForeign,
        bool IsSendingAllowed,
        bool IsProfileIsolated);

    public sealed record StreamStateDto(
        StreamStatus Status,
        string? Title,
        string? GameName,
        int? ViewerCount,
        DateTime? StartedAt);

    public sealed record HttpServerStateDto(bool IsRunning, int Port, string? ChatUrl);

    public sealed record ObsStateDto(bool IsConnected, string? Version, string? LastError);
}
