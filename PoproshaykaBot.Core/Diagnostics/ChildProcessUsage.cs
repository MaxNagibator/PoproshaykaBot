namespace PoproshaykaBot.Core.Diagnostics;

public sealed record ChildProcessUsage(string Name, int Count, long Bytes);
