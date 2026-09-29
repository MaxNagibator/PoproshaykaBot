namespace PoproshaykaBot.Core.Chat.Commands;

public static class CommandRenames
{
    public static IReadOnlyList<(string From, string To)> All { get; } =
    [
        ("profile", "пресет"),
    ];

    public static IReadOnlyList<(string From, string To)> MoveRenamed<T>(IDictionary<string, T> entries, Action<T, string>? rename = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(entries);

        List<(string From, string To)>? moved = null;

        foreach (var (from, to) in All)
        {
            if (!entries.TryGetValue(from, out var entry) || entry is null)
            {
                continue;
            }

            if (entries.TryGetValue(to, out var existing) && existing is not null)
            {
                continue;
            }

            entries.Remove(from);
            rename?.Invoke(entry, to);
            entries[to] = entry;
            (moved ??= []).Add((from, to));
        }

        return moved ?? [];
    }
}
