using PoproshaykaBot.Core.Broadcast;

namespace PoproshaykaBot.Core.Statistics;

public static class StreamSessionMerge
{
    public static readonly TimeSpan StartTolerance = StreamSessionStatisticsHandler.StreamMatchTolerance;

    public static StreamSessionMergeResult Merge(IReadOnlyList<StreamSessionRecord> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        var groupOf = new int[sessions.Count];
        Array.Fill(groupOf, -1);

        var groups = new List<List<int>>();
        var bounds = new List<(DateTimeOffset StartedAt, DateTimeOffset EndedAt)>();
        var openGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var candidates = Enumerable.Range(0, sessions.Count)
            .Where(index => !sessions[index].IsHidden)
            .OrderBy(index => sessions[index].StartedAt)
            .ThenBy(index => index);

        foreach (var index in candidates)
        {
            var record = sessions[index];
            var startedAt = record.StartedAt;
            var endedAt = record.EndedAt > startedAt ? record.EndedAt : startedAt;

            if (openGroups.TryGetValue(record.Channel, out var openIndex) && Joins(bounds[openIndex], startedAt))
            {
                groups[openIndex].Add(index);
                groupOf[index] = openIndex;

                bounds[openIndex] = (bounds[openIndex].StartedAt,
                    endedAt > bounds[openIndex].EndedAt ? endedAt : bounds[openIndex].EndedAt);

                continue;
            }

            groups.Add([index]);
            bounds.Add((startedAt, endedAt));
            openGroups[record.Channel] = groups.Count - 1;
            groupOf[index] = groups.Count - 1;
        }

        var merged = new List<StreamSessionRecord>(sessions.Count);
        var emitted = new HashSet<int>();
        var mergedPairs = 0;

        for (var index = 0; index < sessions.Count; index++)
        {
            var groupIndex = groupOf[index];

            if (groupIndex < 0)
            {
                merged.Add(sessions[index]);
                continue;
            }

            if (!emitted.Add(groupIndex))
            {
                continue;
            }

            var group = groups[groupIndex];

            if (group.Count == 1)
            {
                merged.Add(sessions[group[0]]);
                continue;
            }

            mergedPairs += group.Count - 1;
            merged.Add(Combine(group.ConvertAll(member => sessions[member])));
        }

        return new(merged, mergedPairs);
    }

    private static bool Joins((DateTimeOffset StartedAt, DateTimeOffset EndedAt) group, DateTimeOffset startedAt)
    {
        return startedAt <= group.EndedAt || (startedAt - group.StartedAt).Duration() <= StartTolerance;
    }

    private static StreamSessionRecord Combine(IReadOnlyList<StreamSessionRecord> group)
    {
        var primary = group[0];
        var endedAt = primary.StartedAt;
        long messageCount = 0;
        var peakViewers = 0;

        foreach (var record in group)
        {
            if (record.EndedAt > endedAt)
            {
                endedAt = record.EndedAt;
            }

            messageCount += record.MessageCount;

            if (record.PeakViewers > peakViewers)
            {
                peakViewers = record.PeakViewers;
            }
        }

        var chatters = MergeChatters(group);

        return new()
        {
            Id = primary.Id,
            Channel = primary.Channel,
            StartedAt = primary.StartedAt,
            EndedAt = endedAt,
            Title = LongestNonEmpty(group, record => record.Title),
            Game = LongestNonEmpty(group, record => record.Game),
            MessageCount = messageCount,
            ChatterCount = chatters.Count > 0 ? chatters.Count : group.Max(record => record.ChatterCount),
            PeakViewers = peakViewers,
            AverageViewers = WeightedAverageViewers(group),
            Chatters = chatters,
            Segments = MergeSegments(group),
            TrackedIntervals = group.All(record => record.TrackedIntervals != null)
                ? StreamSessionInterval.Normalize(group.SelectMany(record => record.TrackedIntervals!), primary.StartedAt, endedAt)
                : null,
        };
    }

    private static List<StreamSessionSegment> MergeSegments(IReadOnlyList<StreamSessionRecord> group)
    {
        var merged = new List<StreamSessionSegment>();

        StreamSessionSegment? tail = null;
        double weightedViewers = 0;
        double weightTicks = 0;
        var folded = false;

        var ordered = group
            .SelectMany(record => record.Segments)
            .OrderBy(segment => segment.StartedAt)
            .ThenBy(segment => segment.EndedAt);

        foreach (var source in ordered)
        {
            var startedAt = source.StartedAt;
            var endedAt = source.EndedAt > startedAt ? source.EndedAt : startedAt;

            if (tail != null && endedAt <= tail.EndedAt)
            {
                tail.MessageCount += source.MessageCount;
                tail.PeakViewers = Math.Max(tail.PeakViewers, source.PeakViewers);

                weightedViewers += source.AverageViewers * (double)(endedAt - startedAt).Ticks;
                weightTicks += (endedAt - startedAt).Ticks;
                folded = true;

                continue;
            }

            CloseTail();

            if (tail != null && startedAt < tail.EndedAt)
            {
                startedAt = tail.EndedAt;
            }

            tail = new()
            {
                StartedAt = startedAt,
                EndedAt = endedAt,
                Title = source.Title,
                Game = source.Game,
                MessageCount = source.MessageCount,
                PeakViewers = source.PeakViewers,
                AverageViewers = source.AverageViewers,
            };

            weightedViewers = source.AverageViewers * (double)(endedAt - source.StartedAt).Ticks;
            weightTicks = (endedAt - source.StartedAt).Ticks;
            folded = false;

            merged.Add(tail);
        }

        CloseTail();

        return merged;

        void CloseTail()
        {
            if (tail == null || !folded || weightTicks <= 0)
            {
                return;
            }

            tail.AverageViewers = (int)Math.Round(weightedViewers / weightTicks, MidpointRounding.AwayFromZero);
        }
    }

    private static string? LongestNonEmpty(IReadOnlyList<StreamSessionRecord> group, Func<StreamSessionRecord, string?> selector)
    {
        return group
            .Where(record => !string.IsNullOrWhiteSpace(selector(record)))
            .OrderByDescending(record => record.Duration)
            .Select(selector)
            .FirstOrDefault();
    }

    private static int WeightedAverageViewers(IReadOnlyList<StreamSessionRecord> group)
    {
        double weighted = 0;
        double totalTicks = 0;

        foreach (var record in group)
        {
            var ticks = (double)record.Duration.Ticks;

            if (ticks <= 0)
            {
                continue;
            }

            weighted += record.AverageViewers * ticks;
            totalTicks += ticks;
        }

        if (totalTicks > 0)
        {
            return (int)Math.Round(weighted / totalTicks, MidpointRounding.AwayFromZero);
        }

        var known = group
            .Select(record => record.AverageViewers)
            .Where(average => average != 0)
            .ToList();

        return known.Count > 0 ? (int)Math.Round(known.Average(), MidpointRounding.AwayFromZero) : 0;
    }

    private static List<StreamSessionChatter> MergeChatters(IReadOnlyList<StreamSessionRecord> group)
    {
        var merged = new Dictionary<(bool HasUserId, string Key), StreamSessionChatter>();

        foreach (var record in group)
        {
            foreach (var chatter in record.Chatters)
            {
                var hasUserId = !string.IsNullOrEmpty(chatter.UserId);
                var key = (hasUserId, hasUserId ? chatter.UserId : chatter.DisplayName);

                if (merged.TryGetValue(key, out var existing))
                {
                    existing.MessageCount += chatter.MessageCount;

                    if (!string.IsNullOrEmpty(chatter.DisplayName))
                    {
                        existing.DisplayName = chatter.DisplayName;
                    }

                    continue;
                }

                merged[key] = new()
                {
                    UserId = chatter.UserId,
                    DisplayName = chatter.DisplayName,
                    MessageCount = chatter.MessageCount,
                };
            }
        }

        return merged.Values
            .OrderByDescending(chatter => chatter.MessageCount)
            .ThenBy(chatter => chatter.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
