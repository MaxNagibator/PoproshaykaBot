using System.Text.RegularExpressions;

namespace PoproshaykaBot.Core.Server;

public static partial class QueryStringMasker
{
    public static string Mask(string queryString)
    {
        if (string.IsNullOrEmpty(queryString))
        {
            return queryString;
        }

        return SecretPattern().Replace(queryString, m => $"{m.Groups[1].Value}=***");
    }

    [GeneratedRegex(@"(?i)(?<=^|[?&])(code|access_token|refresh_token|u)=([^&]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();
}
