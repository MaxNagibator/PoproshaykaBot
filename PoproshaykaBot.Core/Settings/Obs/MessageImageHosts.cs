using System.Net;

namespace PoproshaykaBot.Core.Settings.Obs;

public static class MessageImageHosts
{
    public const int MaxHostCount = 64;
    public const int MaxHostLength = 253;

    public static List<string> Normalize(IEnumerable<string>? hosts)
    {
        var result = new List<string>();

        if (hosts == null)
        {
            return result;
        }

        foreach (var raw in hosts)
        {
            if (!TryNormalizeHost(raw, out var host))
            {
                continue;
            }

            if (result.Contains(host, StringComparer.Ordinal))
            {
                continue;
            }

            result.Add(host);

            if (result.Count == MaxHostCount)
            {
                break;
            }
        }

        return result;
    }

    public static bool TryNormalizeHost(string? raw, out string host)
    {
        host = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim().TrimEnd('.').ToLowerInvariant();

        if (trimmed.Length is 0 or > MaxHostLength)
        {
            return false;
        }

        if (IPAddress.TryParse(trimmed, out _))
        {
            return false;
        }

        if (trimmed.IndexOf('.', StringComparison.Ordinal) < 0)
        {
            return false;
        }

        foreach (var symbol in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(symbol) && symbol is not ('.' or '-'))
            {
                return false;
            }
        }

        if (trimmed.StartsWith('.') || trimmed.StartsWith('-') || trimmed.EndsWith('-'))
        {
            return false;
        }

        host = trimmed;
        return true;
    }

    public static bool IsAllowed(string? host, IReadOnlyCollection<string>? allowedHosts)
    {
        if (allowedHosts == null || allowedHosts.Count == 0)
        {
            return false;
        }

        if (!TryNormalizeHost(host, out var normalized))
        {
            return false;
        }

        foreach (var allowed in allowedHosts)
        {
            if (TryNormalizeHost(allowed, out var allowedNormalized)
                && string.Equals(allowedNormalized, normalized, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
