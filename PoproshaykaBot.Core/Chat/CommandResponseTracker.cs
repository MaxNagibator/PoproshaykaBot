namespace PoproshaykaBot.Core.Chat;

public sealed class CommandResponseTracker(TimeProvider timeProvider)
{
    public const int MaxTrackedMessages = 256;

    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> _byMessageId = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _order = new();

    public void Expect(string messageId, CommandResponseMark mark)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentNullException.ThrowIfNull(mark);

        var now = timeProvider.GetUtcNow();

        lock (_lock)
        {
            DropExpired(now);

            if (_byMessageId.TryGetValue(messageId, out var existing))
            {
                _order.Remove(existing);
                _byMessageId.Remove(messageId);
            }

            while (_byMessageId.Count >= MaxTrackedMessages && _order.First is { } oldest)
            {
                _order.RemoveFirst();
                _byMessageId.Remove(oldest.Value.MessageId);
            }

            _byMessageId[messageId] = _order.AddLast(new Entry(messageId, mark, now + Lifetime));
        }
    }

    public CommandResponseMark? TryConsume(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();

        lock (_lock)
        {
            DropExpired(now);

            if (!_byMessageId.Remove(messageId, out var node))
            {
                return null;
            }

            _order.Remove(node);
            return node.Value.Mark;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _byMessageId.Clear();
            _order.Clear();
        }
    }

    private void DropExpired(DateTimeOffset now)
    {
        while (_order.First is { } oldest && oldest.Value.ExpiresAt <= now)
        {
            _order.RemoveFirst();
            _byMessageId.Remove(oldest.Value.MessageId);
        }
    }

    private sealed record Entry(string MessageId, CommandResponseMark Mark, DateTimeOffset ExpiresAt);
}
