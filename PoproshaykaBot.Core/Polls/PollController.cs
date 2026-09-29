using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Polling;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.Helix;

namespace PoproshaykaBot.Core.Polls;

public sealed class PollController(
    [FromKeyedServices(TwitchEndpoints.HelixBroadcasterClient)]
    ITwitchHelixClient helix,
    IBroadcasterIdProvider broadcasterIdProvider,
    PollSnapshotStore snapshotStore,
    PollsAvailabilityService availability,
    IEventBus eventBus,
    ILogger<PollController> logger)
    : IPollController
{
    private readonly SemaphoreSlim _startLock = new(1, 1);

    public async Task<PollSnapshot?> StartAsync(PollProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await _startLock.WaitAsync(cancellationToken);

        try
        {
            if (snapshotStore.Current is { Status: PollSnapshotStatus.Active })
            {
                await eventBus.PublishAsync(new PollStartFailed(profile.Id, profile.Name, "Уже идёт другое голосование"),
                    cancellationToken);

                return null;
            }

            var availabilityResult = await availability.GetAsync(cancellationToken);

            if (!availabilityResult.IsAvailable)
            {
                var reason = availabilityResult.UnavailableReason ?? "Голосования недоступны на этом канале";
                await eventBus.PublishAsync(new PollStartFailed(profile.Id, profile.Name, reason), cancellationToken);
                return null;
            }

            var broadcasterId = await broadcasterIdProvider.GetAsync(cancellationToken);

            if (string.IsNullOrEmpty(broadcasterId))
            {
                await eventBus.PublishAsync(new PollStartFailed(profile.Id, profile.Name, "Не удалось определить broadcaster id"),
                    cancellationToken);

                return null;
            }

            var channelContext = await TryGetChannelContextAsync(broadcasterId, cancellationToken);
            var title = ExpandPlaceholders(profile.Title, channelContext);
            var choices = profile.Choices.Select(c => ExpandPlaceholders(c, channelContext)).ToArray();

            var request = new CreatePollRequest
            {
                BroadcasterId = broadcasterId,
                Title = title,
                Choices = choices,
                DurationSeconds = profile.DurationSeconds,
                ChannelPointsVotingEnabled = profile.ChannelPointsVotingEnabled,
                ChannelPointsPerVote = profile.ChannelPointsPerVote,
            };

            try
            {
                var info = await helix.CreatePollAsync(request, cancellationToken);
                var snapshot = PollEventSubMapper.FromCreatedPoll(info, profile.Id);

                snapshotStore.Set(snapshot);
                await eventBus.PublishAsync(new PollStarted(snapshot), cancellationToken);

                logger.LogInformation("PollController: голосование {PollId} запущено по профилю {ProfileName}",
                    snapshot.PollId, profile.Name);

                return snapshot;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var safeMessage = HelixErrorMessages.SafeMessage(ex);
                logger.LogError(ex, "PollController: не удалось запустить голосование по профилю {ProfileName}", profile.Name);
                await eventBus.PublishAsync(new PollStartFailed(profile.Id, profile.Name, safeMessage), cancellationToken);
                return null;
            }
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async Task<bool> EndAsync(bool showResult, CancellationToken cancellationToken)
    {
        var current = snapshotStore.Current;

        if (current is null || current.Status != PollSnapshotStatus.Active)
        {
            return false;
        }

        var broadcasterId = await broadcasterIdProvider.GetAsync(cancellationToken);

        if (string.IsNullOrEmpty(broadcasterId))
        {
            logger.LogWarning("PollController.EndAsync: не удалось определить broadcaster id");
            return false;
        }

        try
        {
            await helix.EndPollAsync(new()
                {
                    BroadcasterId = broadcasterId,
                    PollId = current.PollId,
                    ShowResult = showResult,
                },
                cancellationToken);

            logger.LogInformation("PollController: голосование {PollId} завершено (showResult={ShowResult})",
                current.PollId, showResult);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PollController: ошибка завершения голосования {PollId}", current.PollId);
            return false;
        }
    }

    private static string ExpandPlaceholders(string template, ChannelContext context)
    {
        return MessageTemplate.For(template)
            .With("game", context.Game)
            .With("title", context.Title)
            .Render();
    }

    private async Task<ChannelContext> TryGetChannelContextAsync(string broadcasterId, CancellationToken cancellationToken)
    {
        try
        {
            var info = await helix.GetChannelInfoAsync(broadcasterId, cancellationToken);

            return info is null
                ? ChannelContext.Empty
                : new(info.Title ?? string.Empty, info.GameName ?? string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PollController: не удалось получить текущую информацию канала для плейсхолдеров");
            return ChannelContext.Empty;
        }
    }

    private sealed record ChannelContext(string Title, string Game)
    {
        public static readonly ChannelContext Empty = new(string.Empty, string.Empty);
    }
}
