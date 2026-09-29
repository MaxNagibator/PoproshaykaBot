using System.Diagnostics.CodeAnalysis;

namespace PoproshaykaBot.Core.Debugging;

public static class ChannelLogin
{
    private const int MaxLength = 25;

    public static bool TryNormalize(string? raw, [NotNullWhen(true)] out string? login)
    {
        login = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim();

        foreach (var prefix in new[] { "https://", "http://" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..];
            }
        }

        foreach (var prefix in new[] { "www.twitch.tv/", "twitch.tv/", "m.twitch.tv/" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..];
            }
        }

        var slash = value.IndexOf('/', StringComparison.Ordinal);

        if (slash >= 0)
        {
            value = value[..slash];
        }

        var query = value.IndexOf('?', StringComparison.Ordinal);

        if (query >= 0)
        {
            value = value[..query];
        }

        value = value.TrimStart('@').Trim();

        if (value.Length is 0 or > MaxLength)
        {
            return false;
        }

        foreach (var symbol in value)
        {
            if (!char.IsAsciiLetterOrDigit(symbol) && symbol != '_')
            {
                return false;
            }
        }

        login = value.ToLowerInvariant();
        return true;
    }
}
