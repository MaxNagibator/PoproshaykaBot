using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Core.Chat;

public sealed class ChatCommandProcessor
{
    public const string DefaultPrefix = "!";

    private readonly string _unknownCommandsFilePath;
    private readonly Dictionary<string, IChatCommand> _tokenToCommand;
    private readonly CommandSettingsStore _commandSettingsStore;
    private readonly CommandUsageRepository _usageRepository;
    private readonly ILogger<ChatCommandProcessor> _logger;

    public ChatCommandProcessor(
        IEnumerable<IChatCommand> commands,
        CommandSettingsStore commandSettingsStore,
        CommandUsageRepository usageRepository,
        ILogger<ChatCommandProcessor> logger,
        string prefix = DefaultPrefix)
    {
        _commandSettingsStore = commandSettingsStore;
        _usageRepository = usageRepository;
        _logger = logger;

        Directory.CreateDirectory(AppPaths.BaseDirectory);
        _unknownCommandsFilePath = AppPaths.Combine("unknown_commands.txt");

        Prefix = string.IsNullOrWhiteSpace(prefix) ? DefaultPrefix : prefix;
        _tokenToCommand = new(StringComparer.OrdinalIgnoreCase);

        foreach (var command in commands)
        {
            Register(command);
        }
    }

    public string Prefix { get; }

    public void Register(IChatCommand command)
    {
        RegisterToken(command.Canonical, command);

        foreach (var alias in command.Aliases.Where(a => !string.IsNullOrWhiteSpace(a)))
        {
            RegisterToken(alias, command);
        }
    }

    public IReadOnlyCollection<IChatCommand> GetAllCommands()
    {
        return _tokenToCommand
            .Values
            .GroupBy(x => x.Canonical, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    public IReadOnlyCollection<IChatCommand> GetEnabledCommands()
    {
        var settings = _commandSettingsStore.Load();

        return GetAllCommands()
            .Where(command => settings.IsEnabled(command.Canonical))
            .ToList();
    }

    public async Task<ChatCommandResult> TryProcessAsync(string messageText, CommandContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return ChatCommandResult.NotHandled;
        }

        var trimmed = messageText.Trim();

        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return ChatCommandResult.NotHandled;
        }

        var afterPrefix = trimmed[Prefix.Length..].Trim();

        if (string.IsNullOrEmpty(afterPrefix))
        {
            return ChatCommandResult.NotHandled;
        }

        var spaceIdx = afterPrefix.IndexOf(' ');
        var token = spaceIdx >= 0 ? afterPrefix[..spaceIdx] : afterPrefix;
        var argsString = spaceIdx >= 0 ? afterPrefix[(spaceIdx + 1)..] : string.Empty;

        if (!_tokenToCommand.TryGetValue(token, out var command))
        {
            LogUnknown(messageText, context);
            return ChatCommandResult.NotHandled;
        }

        var settings = _commandSettingsStore.Load();

        if (!settings.IsEnabled(command.Canonical))
        {
            _logger.LogDebug("Команда {Canonical} выключена в настройках, сообщение от {Username} пропущено",
                command.Canonical,
                context.Username);

            return ChatCommandResult.NotHandled;
        }

        var args = string.IsNullOrWhiteSpace(argsString)
            ? []
            : argsString.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var enrichedContext = new CommandContext
        {
            Channel = context.Channel,
            MessageId = context.MessageId,
            UserId = context.UserId,
            Username = context.Username,
            DisplayName = context.DisplayName,
            Arguments = args,
            IsBroadcaster = context.IsBroadcaster,
            IsModerator = context.IsModerator,
        };

        var requiredLevel = settings.ResolveAccessLevel(command.Canonical);

        if (!requiredLevel.Allows(enrichedContext))
        {
            _logger.LogDebug("Команда {Canonical} отклонена для {Username}: настройка прав требует {Level}",
                command.Canonical,
                enrichedContext.Username,
                requiredLevel);

            return ChatCommandResult.NotHandled;
        }

        if (!command.CanExecute(enrichedContext))
        {
            _logger.LogDebug("Команда {Canonical} отклонена для {Username}: условия выполнения не соблюдены",
                command.Canonical,
                enrichedContext.Username);

            return ChatCommandResult.NotHandled;
        }

        try
        {
            var response = await command.ExecuteAsync(enrichedContext, cancellationToken);

            _usageRepository.Track(command.Canonical, enrichedContext.DisplayName);

            if (response is null)
            {
                _logger.LogInformation("Команда {Canonical} выполнена для {Username}, ответа нет", command.Canonical, enrichedContext.Username);
            }
            else
            {
                _logger.LogInformation("Команда {Canonical} выполнена для {Username}, ответ {Delivery} длиной {Length} символов",
                    command.Canonical,
                    enrichedContext.Username,
                    response.Delivery,
                    response.Text.Length);
            }

            return ChatCommandResult.Succeeded(command.Canonical, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Команда {Canonical} упала на сообщении от {Username}", command.Canonical, enrichedContext.Username);
            return ChatCommandResult.Failed(command.Canonical);
        }
    }

    private void RegisterToken(string token, IChatCommand command)
    {
        if (_tokenToCommand.TryGetValue(token, out var owner) && !ReferenceEquals(owner, command))
        {
            _logger.LogWarning("Токен {Token} команды {Canonical} уже занят командой {Owner}, остаётся за ней",
                token,
                command.Canonical,
                owner.Canonical);

            return;
        }

        _tokenToCommand[token] = command;
    }

    private void LogUnknown(string originalText, CommandContext context)
    {
        try
        {
            var line = $"{DateTime.UtcNow:O}\t{context.Channel}\t{context.UserId}\t{context.Username}\t{originalText}{Environment.NewLine}";
            File.AppendAllText(_unknownCommandsFilePath, line);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось записать неизвестную команду в {File}", _unknownCommandsFilePath);
        }
    }
}

public readonly record struct ChatCommandResult(bool IsCommand, OutgoingMessage? Response, string? Canonical, bool Executed)
{
    public static ChatCommandResult NotHandled { get; } = new(false, null, null, false);

    public static ChatCommandResult Succeeded(string canonical, OutgoingMessage? response)
    {
        return new(true, response, canonical, true);
    }

    public static ChatCommandResult Failed(string canonical)
    {
        return new(true, null, canonical, false);
    }
}

public enum DeliveryType
{
    Normal = 0,
    Reply = 1,
}

public sealed class CommandContext
{
    public string Channel { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public bool IsBroadcaster { get; init; }
    public bool IsModerator { get; init; }
}

public sealed record OutgoingMessage
{
    public string Text { get; init; } = string.Empty;
    public DeliveryType Delivery { get; init; }
    public string? ReplyToMessageId { get; init; }

    public static OutgoingMessage Normal(string text)
    {
        return new()
        {
            Text = text,
            Delivery = DeliveryType.Normal,
        };
    }

    public static OutgoingMessage Reply(string text, string? replyToMessageId)
    {
        return new()
        {
            Text = text,
            Delivery = DeliveryType.Reply,
            ReplyToMessageId = replyToMessageId,
        };
    }
}
