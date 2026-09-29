using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Polling;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Polls;

public class PollProfilesManager(
    PollsStore pollsStore,
    IEventBus eventBus,
    ILogger<PollProfilesManager> logger)
{
    public virtual PollProfile? FindByName(string name)
    {
        return pollsStore.Load()
            .Profiles
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public virtual PollProfile? Find(Guid id)
    {
        return pollsStore.Load().Profiles.FirstOrDefault(p => p.Id == id);
    }

    public virtual IReadOnlyList<PollProfile> GetAll()
    {
        return pollsStore.Load().Profiles.ToList();
    }

    public virtual bool Upsert(PollProfile profile)
    {
        Validate(profile);

        var written = pollsStore.Mutate(polls =>
        {
            var profiles = polls.Profiles;

            var duplicate = profiles.FirstOrDefault(p =>
                p.Id != profile.Id && string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
            {
                throw new InvalidOperationException($"Профиль голосования с именем '{profile.Name}' уже существует");
            }

            var existing = profiles.FirstOrDefault(p => p.Id == profile.Id);

            if (existing == null)
            {
                profiles.Add(profile);
            }
            else
            {
                var index = profiles.IndexOf(existing);
                profiles[index] = profile;
            }
        });

        logger.LogDebug("Upsert профиля голосования {ProfileName} (Id={ProfileId})", profile.Name, profile.Id);
        _ = eventBus.PublishAsync(new PollProfilesChanged());

        return written;
    }

    public virtual bool ReplaceAll(IEnumerable<PollProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        var snapshot = profiles.ToList();

        foreach (var profile in snapshot)
        {
            Validate(profile);
        }

        var seenIds = new HashSet<Guid>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in snapshot)
        {
            if (!seenIds.Add(profile.Id))
            {
                throw new InvalidOperationException($"Дублирующийся id профиля голосования: {profile.Id}");
            }

            if (!seenNames.Add(profile.Name))
            {
                throw new InvalidOperationException($"Профиль голосования с именем '{profile.Name}' уже существует");
            }
        }

        var written = pollsStore.Mutate(polls =>
        {
            polls.Profiles.Clear();
            polls.Profiles.AddRange(snapshot);
        });

        logger.LogInformation("ReplaceAll профилей голосований ({Count})", snapshot.Count);
        _ = eventBus.PublishAsync(new PollProfilesChanged());

        return written;
    }

    public virtual bool Remove(Guid id)
    {
        var removed = 0;
        var written = pollsStore.Mutate(polls => removed = polls.Profiles.RemoveAll(p => p.Id == id));

        if (removed > 0)
        {
            logger.LogInformation("Удалён профиль голосования Id={ProfileId}", id);
        }
        else
        {
            logger.LogDebug("Запрос на удаление профиля голосования Id={ProfileId} – записей не найдено", id);
        }

        _ = eventBus.PublishAsync(new PollProfilesChanged());

        return written;
    }

    private static void Validate(PollProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new InvalidOperationException("Имя профиля не может быть пустым");
        }

        if (profile.Choices.Count < PollProfile.MinChoices)
        {
            throw new InvalidOperationException($"Должно быть минимум {PollProfile.MinChoices} варианта");
        }

        if (profile.Choices.Count > PollProfile.MaxChoices)
        {
            throw new InvalidOperationException($"Должно быть максимум {PollProfile.MaxChoices} вариантов");
        }

        if (profile.Choices.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Пустые варианты не допускаются");
        }

        if (profile.DurationSeconds < PollProfile.MinDurationSeconds || profile.DurationSeconds > PollProfile.MaxDurationSeconds)
        {
            throw new InvalidOperationException($"Длительность должна быть от {PollProfile.MinDurationSeconds} до {PollProfile.MaxDurationSeconds} секунд");
        }

        if (profile.ChannelPointsVotingEnabled && profile.ChannelPointsPerVote < PollProfile.MinChannelPointsPerVote)
        {
            throw new InvalidOperationException($"Стоимость голоса Channel Points должна быть не меньше {PollProfile.MinChannelPointsPerVote}");
        }

        if (profile.ChannelPointsVotingEnabled && profile.ChannelPointsPerVote > PollProfile.MaxChannelPointsPerVote)
        {
            throw new InvalidOperationException($"Стоимость голоса Channel Points не может превышать {PollProfile.MaxChannelPointsPerVote}");
        }
    }
}
