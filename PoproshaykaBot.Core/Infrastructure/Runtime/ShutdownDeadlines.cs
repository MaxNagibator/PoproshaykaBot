namespace PoproshaykaBot.Core.Infrastructure.Runtime;

public static class ShutdownDeadlines
{
    public static readonly TimeSpan SoftDeadline = TimeSpan.FromSeconds(8);

    public static readonly TimeSpan HardDeadline = TimeSpan.FromSeconds(12);
}
