using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoproshaykaBot.Core.Chat.Commands;

public sealed class CommandResponseTargetJsonConverter : JsonConverter<CommandResponseTarget>
{
    public override CommandResponseTarget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.TryGetInt32(out var number)
                ? (CommandResponseTarget)number
                : throw new JsonException("Цель ответа команды вне диапазона Int32");
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Цель ответа команды ожидается числом или строкой, получено {reader.TokenType}");
        }

        var target = CommandResponseTarget.None;

        foreach (var part in (reader.GetString() ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse<CommandResponseTarget>(part, true, out var flag))
            {
                target |= flag;
            }
        }

        return target;
    }

    public override void Write(Utf8JsonWriter writer, CommandResponseTarget value, JsonSerializerOptions options)
    {
        if (value.HasFlag(CommandResponseTarget.Whisper))
        {
            value |= CommandResponseTarget.Caller;
        }

        if (value.HasFlag(CommandResponseTarget.Caller))
        {
            value |= CommandResponseTarget.Chat;
        }

        writer.WriteNumberValue((int)value);
    }
}
