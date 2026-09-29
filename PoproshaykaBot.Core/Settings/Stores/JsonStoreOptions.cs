using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PoproshaykaBot.Core.Settings.Stores;

public static class JsonStoreOptions
{
    public static JsonSerializerOptions Default { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { JsonStoreNullDefaults.Apply },
        },
        Converters =
        {
            new ColorJsonConverter(),
            new JsonStringEnumConverter(allowIntegerValues: true),
        },
    };
}
