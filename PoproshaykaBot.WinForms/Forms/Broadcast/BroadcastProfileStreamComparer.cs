using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Streaming;
using System.Text.RegularExpressions;

namespace PoproshaykaBot.WinForms.Forms.Broadcast;

internal static class BroadcastProfileStreamComparer
{
    private static readonly TimeSpan TitleMatchTimeout = TimeSpan.FromMilliseconds(100);

    public static bool ProfileDivergesFromStream(BroadcastProfile profile, StreamInfo? stream)
    {
        if (stream is null or { IsBare: true })
        {
            return false;
        }

        if (!TitleMatches(profile.Title?.Trim() ?? string.Empty, stream.Title?.Trim() ?? string.Empty))
        {
            return true;
        }

        if (!string.Equals(profile.GameId ?? string.Empty, stream.GameId ?? string.Empty, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    public static bool TitleMatches(string profileTitle, string streamTitle)
    {
        const string Placeholder = "{n}";

        if (!profileTitle.Contains(Placeholder, StringComparison.Ordinal))
        {
            return string.Equals(profileTitle, streamTitle, StringComparison.Ordinal);
        }

        var pattern = "^"
                      + Regex.Escape(profileTitle)
                          .Replace(Regex.Escape(Placeholder), "\\d+")
                      + "$";

        return Regex.IsMatch(streamTitle, pattern, RegexOptions.None, TitleMatchTimeout);
    }
}
