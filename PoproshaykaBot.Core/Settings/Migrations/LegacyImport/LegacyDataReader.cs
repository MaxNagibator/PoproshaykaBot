using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace PoproshaykaBot.Core.Settings.Migrations.LegacyImport;

internal static class LegacyDataReader
{
    private const long MaxInspectedFileBytes = 32L * 1024 * 1024;

    private static readonly string[] TokenKeys = ["accessToken", "refreshToken"];

    public static JsonNode? TryRead(string? filePath, ILogger? logger)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        try
        {
            var info = new FileInfo(filePath);

            if (!info.Exists)
            {
                return null;
            }

            if (info.Length > MaxInspectedFileBytes)
            {
                logger?.LogWarning("Импорт данных: файл {FilePath} слишком велик для разбора ({Length} байт) – читается только его наличие",
                    filePath,
                    info.Length);

                return null;
            }

            return JsonNode.Parse(File.ReadAllText(filePath));
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Импорт данных: не удалось прочитать {FilePath} – сведения из него не показаны", filePath);
            return null;
        }
    }

    public static JsonNode? Property(JsonNode? node, string name)
    {
        if (node is not JsonObject obj)
        {
            return null;
        }

        return obj.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    public static string? ReadString(JsonNode? node, string name)
    {
        if (Property(node, name) is not JsonValue value || !value.TryGetValue(out string? text))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static ulong ReadUInt64(JsonNode? node, string name)
    {
        if (Property(node, name) is not JsonValue value)
        {
            return 0;
        }

        return value.TryGetValue(out ulong number) ? number : 0;
    }

    public static int CountItems(JsonNode? node)
    {
        return node switch
        {
            JsonArray array => array.Count,
            JsonObject obj => obj.Count,
            _ => 0,
        };
    }

    public static bool HasOAuthTokens(string directory, ILogger? logger)
    {
        var accounts = TryRead(LegacyDataCatalog.ResolveSettingsFile(directory, LegacyDataCatalog.AccountsFileName), logger);
        var settings = TryRead(LegacyDataCatalog.ResolveSettingsFile(directory, LegacyDataCatalog.SettingsFileName), logger);

        return HasTokens(accounts) || HasTokens(Property(settings, "twitch"));
    }

    public static bool HasTokens(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var pair in obj)
                {
                    if (TokenKeys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        if (pair.Value is JsonValue value && value.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text))
                        {
                            return true;
                        }

                        continue;
                    }

                    if (HasTokens(pair.Value))
                    {
                        return true;
                    }
                }

                return false;

            case JsonArray array:
                return array.Any(HasTokens);

            default:
                return false;
        }
    }
}
