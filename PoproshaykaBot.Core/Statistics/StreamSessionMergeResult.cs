namespace PoproshaykaBot.Core.Statistics;

public readonly record struct StreamSessionMergeResult(IReadOnlyList<StreamSessionRecord> Sessions, int MergedPairs);
