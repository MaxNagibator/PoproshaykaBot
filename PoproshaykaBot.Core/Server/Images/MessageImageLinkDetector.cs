namespace PoproshaykaBot.Core.Server.Images;

public sealed record MessageImageLink(Uri Uri, int StartIndex, int EndIndex);

public static class MessageImageLinkDetector
{
    private const string HttpsPrefix = "https://";
    private const char NoBreakSpace = ' ';

    private static readonly char[] TokenSeparators = [' ', '\t', '\r', '\n', NoBreakSpace];
    private static readonly char[] TrailingNoise = ['.', ',', ';', ':', '!', '?', ')', ']', '}', '"', '\'', '>', '«', '»'];
    private static readonly char[] LeadingNoise = ['(', '[', '{', '"', '\'', '<', '«'];

    public static bool TryFindFirst(string? message, IReadOnlyCollection<string>? allowedHosts, out MessageImageLink link)
    {
        link = null!;

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var position = 0;

        while (position < message.Length)
        {
            if (Array.IndexOf(TokenSeparators, message[position]) >= 0)
            {
                position++;
                continue;
            }

            var tokenEnd = message.IndexOfAny(TokenSeparators, position);

            if (tokenEnd < 0)
            {
                tokenEnd = message.Length;
            }

            var start = position;
            var end = tokenEnd;

            while (start < end && Array.IndexOf(LeadingNoise, message[start]) >= 0)
            {
                start++;
            }

            while (end > start && Array.IndexOf(TrailingNoise, message[end - 1]) >= 0)
            {
                end--;
            }

            position = tokenEnd;

            var trimmed = message[start..end];

            if (!trimmed.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (MessageImageUrlValidator.IsAllowed(trimmed, allowedHosts, out var uri))
            {
                link = new(uri, start, end - 1);
                return true;
            }
        }

        return false;
    }
}
