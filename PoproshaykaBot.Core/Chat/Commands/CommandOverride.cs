using System.Text.Json.Serialization;

namespace PoproshaykaBot.Core.Chat.Commands;

public sealed class CommandOverride
{
    public bool Enabled { get; set; } = true;

    [JsonConverter(typeof(CommandResponseTargetJsonConverter))]
    public CommandResponseTarget? ResponseTarget { get; set; }

    public CommandAccessLevel? Access { get; set; }
}
