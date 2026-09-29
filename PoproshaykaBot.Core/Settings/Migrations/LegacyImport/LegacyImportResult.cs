using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Core.Settings.Migrations.LegacyImport;

public sealed class LegacyImportResult
{
    public required string SourcePath { get; init; }

    public required string TargetPath { get; init; }

    public required IReadOnlyList<string> CopiedFiles { get; init; }

    public required IReadOnlyList<LegacyImportSkip> SkippedFiles { get; init; }

    public required IReadOnlyList<LegacyImportFailure> Failures { get; init; }

    public required bool RequiresReauthorization { get; init; }

    public bool IsInPlaceMigration { get; init; }

    public IReadOnlyList<string> UnmigratedLegacyFiles { get; init; } = [];

    public IReadOnlyList<string> MigratedSettingsFiles { get; init; } = [];

    public bool CopiedStatistics => Copied(LegacyDataCatalog.UserStatisticsFileName)
        || Copied(LegacyDataCatalog.BotStatisticsFileName);

    public bool CopiedStreamHistory => Copied(LegacyDataCatalog.StreamSessionsFileName);

    public bool CopiedPollHistory => Copied(LegacyDataCatalog.PollsHistoryFileName);

    public IReadOnlyList<string> CopiedSettingsFiles =>
        [.. LegacyDataCatalog.StoreOwnedSettingsFiles.Where(CopiedSettingsFile)];

    public bool CopiedSettings => CopiedSettingsFiles.Count > 0;

    public bool CopiedUiPreferences => CopiedSettingsFile(LegacyDataCatalog.UiPreferencesFileName);

    public IReadOnlyList<string> RewrittenSettingsFiles =>
        [.. CopiedSettingsFiles.Union(MigratedSettingsFiles, StringComparer.OrdinalIgnoreCase)];

    public StatisticsExternalWrite ExternalWrite => new(CopiedStatistics, CopiedStreamHistory, CopiedPollHistory);

    private bool Copied(string fileName)
    {
        return CopiedFiles.Any(file => string.Equals(file, fileName, StringComparison.OrdinalIgnoreCase));
    }

    private bool CopiedSettingsFile(string fileName)
    {
        return CopiedFiles.Any(file => string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase));
    }
}
