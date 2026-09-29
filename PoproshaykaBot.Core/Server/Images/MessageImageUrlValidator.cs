using PoproshaykaBot.Core.Settings.Obs;

namespace PoproshaykaBot.Core.Server.Images;

public static class MessageImageUrlValidator
{
    public const int MaxUrlLength = 2048;

    public static readonly IReadOnlyList<string> AllowedExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp"];

    public static readonly IReadOnlyList<string> AllowedMediaTypes = ["image/png", "image/jpeg", "image/gif", "image/webp"];

    public static bool TryParse(string? candidate, out Uri uri)
    {
        uri = null!;

        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > MaxUrlLength)
        {
            return false;
        }

        if (!Uri.TryCreate(candidate.Trim(), UriKind.Absolute, out var parsed) || !IsSafeEndpoint(parsed))
        {
            return false;
        }

        if (!HasImageExtension(parsed))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    public static bool TryParseRedirect(Uri current, Uri? location, out Uri next)
    {
        ArgumentNullException.ThrowIfNull(current);

        next = null!;

        if (location == null)
        {
            return false;
        }

        if (!Uri.TryCreate(current, location, out var resolved) || resolved.AbsoluteUri.Length > MaxUrlLength)
        {
            return false;
        }

        if (!IsSafeEndpoint(resolved))
        {
            return false;
        }

        if (!string.Equals(resolved.Host, current.Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        next = resolved;
        return true;
    }

    public static bool IsAllowedMediaType(string? mediaType)
    {
        return !string.IsNullOrEmpty(mediaType)
            && AllowedMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSafeEndpoint(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            && string.IsNullOrEmpty(uri.UserInfo)
            && uri.HostNameType is UriHostNameType.Dns
            && uri.IsDefaultPort;
    }

    public static bool IsAllowed(string? candidate, IReadOnlyCollection<string>? allowedHosts, out Uri uri)
    {
        if (!TryParse(candidate, out uri))
        {
            return false;
        }

        if (MessageImageHosts.IsAllowed(uri.Host, allowedHosts))
        {
            return true;
        }

        uri = null!;
        return false;
    }

    private static bool HasImageExtension(Uri uri)
    {
        var extension = Path.GetExtension(uri.AbsolutePath);

        return !string.IsNullOrEmpty(extension)
            && AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
