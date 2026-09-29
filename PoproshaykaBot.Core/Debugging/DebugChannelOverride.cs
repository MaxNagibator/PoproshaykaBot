namespace PoproshaykaBot.Core.Debugging;

public sealed record DebugChannelOverride(string? Channel, bool AllowSending, string? RejectedChannel)
{
    public const string ChannelArgument = "--debug-channel";
    public const string AllowSendingArgument = "--allow-send";

    public static DebugChannelOverride None { get; } = new(null, false, null);

    public static DebugChannelOverride Parse(IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0)
        {
            return None;
        }

        string? raw = null;
        var allowSending = false;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];

            if (string.Equals(argument, AllowSendingArgument, StringComparison.OrdinalIgnoreCase))
            {
                allowSending = true;
                continue;
            }

            if (string.Equals(argument, ChannelArgument, StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < args.Count && !args[index + 1].StartsWith('-'))
                {
                    raw = args[index + 1];
                    index++;
                }
                else
                {
                    raw = string.Empty;
                }

                continue;
            }

            var inlinePrefix = ChannelArgument + "=";

            if (argument.StartsWith(inlinePrefix, StringComparison.OrdinalIgnoreCase))
            {
                raw = argument[inlinePrefix.Length..];
            }
        }

        if (raw is null)
        {
            return allowSending ? new(null, true, null) : None;
        }

        return ChannelLogin.TryNormalize(raw, out var login)
            ? new(login, allowSending, null)
            : new(null, allowSending, raw);
    }
}
