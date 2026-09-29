using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Broadcast.Profiles;

namespace PoproshaykaBot.Core.Chat.Commands;

public sealed class GameCommand(
    IChannelInformationApplier applier,
    IGameCategoryResolver resolver,
    ILogger<GameCommand> logger)
    : IChatCommand
{
    private const int MaxCandidatesInReply = 3;
    private const int MaxQueryInReply = 40;

    public string Canonical => "game";
    public IReadOnlyCollection<string> Aliases => ["игра", "категория"];
    public string Description => "обновить категорию трансляции";

    public bool CanExecute(CommandContext context)
    {
        return context.IsBroadcaster || context.IsModerator;
    }

    public async Task<OutgoingMessage?> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (context.Arguments.Count == 0)
        {
            return OutgoingMessage.Reply("Использование: !game <название>", context.MessageId);
        }

        var query = string.Join(" ", context.Arguments);

        try
        {
            var resolution = await resolver.ResolveAsync(query, cancellationToken);
            var suggestion = resolution.Match;

            if (suggestion == null)
            {
                logger.LogInformation("Категория '{Query}' не найдена, похожих в поиске: {Count}", query, resolution.Candidates.Count);

                var shown = query.Length > MaxQueryInReply ? query[..MaxQueryInReply] + "…" : query;

                if (resolution.Candidates.Count == 0)
                {
                    return OutgoingMessage.Reply($"Категория '{shown}' не найдена", context.MessageId);
                }

                var similar = string.Join(", ", resolution.Candidates.Take(MaxCandidatesInReply).Select(x => x.Name));
                return OutgoingMessage.Reply($"Категория '{shown}' не найдена. Может быть: {similar}", context.MessageId);
            }

            await applier.ApplyPatchAsync(null,
                suggestion.Id,
                suggestion.Name,
                cancellationToken);

            return OutgoingMessage.Reply($"Категория обновлена: {suggestion.Name}", context.MessageId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось обновить категорию");
            return OutgoingMessage.Reply("Не удалось обновить категорию", context.MessageId);
        }
    }
}
