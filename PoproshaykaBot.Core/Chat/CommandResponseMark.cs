using PoproshaykaBot.Core.Chat.Commands;

namespace PoproshaykaBot.Core.Chat;

public sealed record CommandResponseMark(string Canonical, CommandResponseTarget Target);
