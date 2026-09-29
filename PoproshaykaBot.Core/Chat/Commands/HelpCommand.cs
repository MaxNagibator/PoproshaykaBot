namespace PoproshaykaBot.Core.Chat.Commands;

public sealed class HelpCommand : IChatCommand
{
    private readonly Func<IReadOnlyCollection<IChatCommand>> _getAvailableCommands;
    private readonly string _prefix;

    public HelpCommand(Func<IReadOnlyCollection<IChatCommand>> getAvailableCommands, string prefix)
    {
        ArgumentNullException.ThrowIfNull(getAvailableCommands);

        _getAvailableCommands = getAvailableCommands;
        _prefix = string.IsNullOrWhiteSpace(prefix) ? ChatCommandProcessor.DefaultPrefix : prefix;
    }

    public string Canonical => "помощь";
    public IReadOnlyCollection<string> Aliases => ["help", "h"];
    public string Description => "список команд";

    public bool CanExecute(CommandContext context)
    {
        return true;
    }

    public Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var allCommands = _getAvailableCommands();

        if (context.Arguments.Count > 0)
        {
            var targetToken = context.Arguments[0];

            while (targetToken.StartsWith(_prefix, StringComparison.Ordinal))
            {
                targetToken = targetToken[_prefix.Length..];
            }

            var command = allCommands.FirstOrDefault(x =>
                string.Equals(x.Canonical, targetToken, StringComparison.OrdinalIgnoreCase)
                || x.Aliases.Any(a => string.Equals(a, targetToken, StringComparison.OrdinalIgnoreCase)));

            if (command != null)
            {
                var aliases = command.Aliases.Count > 0
                    ? $" (алиасы: {string.Join(", ", command.Aliases.Select(x => _prefix + x))})"
                    : string.Empty;

                var text = $"❓ {_prefix}{command.Canonical}: {command.Description}{aliases}";
                return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply(text, context.MessageId));
            }
        }

        var commandNames = allCommands
            .OrderBy(x => x.Canonical, StringComparer.OrdinalIgnoreCase)
            .Select(x => _prefix + x.Canonical)
            .ToList();

        if (commandNames.Count == 0)
        {
            return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply("Команды недоступны", context.MessageId));
        }

        var responseText = "📋 Команды: " + string.Join(", ", commandNames);
        return Task.FromResult<OutgoingMessage?>(OutgoingMessage.Reply(responseText, context.MessageId));
    }
}
