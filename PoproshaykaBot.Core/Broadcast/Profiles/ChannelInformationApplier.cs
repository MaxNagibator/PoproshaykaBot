using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Broadcasting;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Broadcast.Profiles;

public sealed class ChannelInformationApplier(
    ITwitchChannelsApi channelsApi,
    IBroadcasterIdProvider idProvider,
    ITargetChannelProvider targetChannelProvider,
    IChannelUpdateConfirmation confirmation,
    IEventBus eventBus,
    ILogger<ChannelInformationApplier> logger)
    : IChannelInformationApplier
{
    private static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromSeconds(8);

    private const string ForeignChannelMessage = "Идёт отладка на чужом канале – название и категорию менять нельзя.";

    public async Task<bool> ApplyAsync(BroadcastProfile profile, CancellationToken cancellationToken)
    {
        if (targetChannelProvider.Current.IsForeign)
        {
            logger.LogInformation("Профиль {Profile} не применён: {Reason}", profile.Name, ForeignChannelMessage);
            await eventBus.PublishAsync(new BroadcastProfileApplyFailed(profile, ForeignChannelMessage), cancellationToken);

            return false;
        }

        var broadcasterId = await idProvider.GetAsync(cancellationToken);

        if (broadcasterId == null)
        {
            await eventBus.PublishAsync(new BroadcastProfileApplyFailed(profile, "Не удалось определить канал"),
                cancellationToken);

            return false;
        }

        var request = new PatchChannelRequest
        {
            Title = string.IsNullOrEmpty(profile.Title) ? null : profile.Title,
            GameId = string.IsNullOrEmpty(profile.GameId) ? null : profile.GameId,
            BroadcasterLanguage = string.IsNullOrEmpty(profile.BroadcasterLanguage) ? null : profile.BroadcasterLanguage,
            Tags = profile.Tags.ToArray(),
        };

        var confirmationTask = confirmation.AwaitAsync(request.Title, request.GameId, ConfirmationTimeout, cancellationToken);

        try
        {
            await channelsApi.ModifyChannelInformationAsync(broadcasterId, request, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось применить профиль {Profile}", profile.Name);
            await eventBus.PublishAsync(new BroadcastProfileApplyFailed(profile, HelixErrorMessages.SafeMessage(exception)),
                cancellationToken);

            return false;
        }

        var confirmed = await confirmationTask;

        if (!confirmed)
        {
            logger.LogInformation("Не получено подтверждение channel.update для профиля {Profile}, публикуем Applied по успешному Helix-ответу",
                profile.Name);
        }

        await eventBus.PublishAsync(new BroadcastProfileApplied(profile), cancellationToken);
        return true;
    }

    public async Task<bool> ApplyPatchAsync(string? title, string? gameId, string? gameName, CancellationToken cancellationToken)
    {
        if (targetChannelProvider.Current.IsForeign)
        {
            logger.LogInformation("Патч канала не применён: {Reason}", ForeignChannelMessage);
            await eventBus.PublishAsync(new ChannelInformationPatchFailed(title, gameId, gameName, ForeignChannelMessage),
                cancellationToken);

            return false;
        }

        var broadcasterId = await idProvider.GetAsync(cancellationToken);

        if (broadcasterId == null)
        {
            await eventBus.PublishAsync(new ChannelInformationPatchFailed(title, gameId, gameName, "Не удалось определить канал"),
                cancellationToken);

            return false;
        }

        var request = new PatchChannelRequest
        {
            Title = title,
            GameId = gameId,
            BroadcasterLanguage = null,
            Tags = null,
        };

        var confirmationTask = confirmation.AwaitAsync(title, gameId, ConfirmationTimeout, cancellationToken);

        try
        {
            await channelsApi.ModifyChannelInformationAsync(broadcasterId, request, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось применить патч канала");
            await eventBus.PublishAsync(new ChannelInformationPatchFailed(title, gameId, gameName, HelixErrorMessages.SafeMessage(exception)),
                cancellationToken);

            return false;
        }

        var confirmed = await confirmationTask;

        if (!confirmed)
        {
            logger.LogInformation("Не получено подтверждение channel.update для ad-hoc патча, публикуем Patched по успешному Helix-ответу");
        }

        await eventBus.PublishAsync(new ChannelInformationPatched(title, gameId, gameName), cancellationToken);
        return true;
    }
}
