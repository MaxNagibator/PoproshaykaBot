namespace PoproshaykaBot.Core.Chat.Commands;

/// <summary>
/// Интерфейс команды чата.
/// </summary>
public interface IChatCommand
{
    /// <summary>
    /// Каноническое имя команды (без префикса).
    /// </summary>
    string Canonical { get; }

    /// <summary>
    /// Алиасы команды (без префикса).
    /// </summary>
    IReadOnlyCollection<string> Aliases { get; }

    /// <summary>
    /// Краткое описание для помощи.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Отвечает ли команда по существу только пользователям из особого списка настроек
    /// (<see cref="SpecialCommandsSettings.AllowedUsers"/>), пропуская остальных через <see cref="CanExecute"/>.
    /// </summary>
    bool IsRestrictedToAllowedUsers => false;

    /// <summary>
    /// Проверяет, можно ли выполнить команду в текущем контексте.
    /// </summary>
    bool CanExecute(CommandContext context);

    /// <summary>
    /// Выполняет команду и возвращает исходящее сообщение.
    /// </summary>
    Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken);
}
