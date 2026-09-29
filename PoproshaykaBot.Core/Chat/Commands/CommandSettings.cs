using System.Text.Json.Serialization;

namespace PoproshaykaBot.Core.Chat.Commands;

public sealed class CommandSettings
{
    public const CommandResponseTarget ChatAndOverlay = CommandResponseTarget.Chat | CommandResponseTarget.Overlay;

    public const CommandResponseTarget CallerOnly = CommandResponseTarget.Chat | CommandResponseTarget.Caller;

    public const CommandResponseTarget WhisperToCaller = CallerOnly | CommandResponseTarget.Whisper;

    public static CommandResponseTarget KnownTargets { get; } = Enum
        .GetValues<CommandResponseTarget>()
        .Aggregate(CommandResponseTarget.None, static (mask, value) => mask | value);

    [JsonConverter(typeof(CommandResponseTargetJsonConverter))]
    public CommandResponseTarget DefaultResponseTarget { get; set; } = ChatAndOverlay;

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, CommandOverride> Commands { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled(string canonical)
    {
        return !TryGetOverride(canonical, out var commandOverride) || commandOverride.Enabled;
    }

    public CommandResponseTarget ResolveResponseTarget(string canonical)
    {
        if (TryGetOverride(canonical, out var commandOverride) && commandOverride.ResponseTarget is { } target)
        {
            return target & KnownTargets;
        }

        return DefaultResponseTarget & KnownTargets;
    }

    public CommandAccessLevel? ReadAccess(string canonical)
    {
        return TryGetOverride(canonical, out var commandOverride) && commandOverride.Access is { } access
            ? CommandAccessLevels.Clamp(access)
            : null;
    }

    public CommandAccessLevel ResolveAccessLevel(string canonical)
    {
        return ReadAccess(canonical) ?? CommandAccessLevel.Everyone;
    }

    private bool TryGetOverride(string canonical, out CommandOverride commandOverride)
    {
        if (string.IsNullOrWhiteSpace(canonical))
        {
            commandOverride = null!;
            return false;
        }

        return Commands.TryGetValue(canonical, out commandOverride!) && commandOverride is not null;
    }
}
