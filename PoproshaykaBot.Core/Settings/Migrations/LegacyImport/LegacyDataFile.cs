namespace PoproshaykaBot.Core.Settings.Migrations.LegacyImport;

public sealed record LegacyDataFile(
    string Name,
    string SourcePath,
    string RelativeTargetPath,
    long SizeBytes,
    DateTime ModifiedAtUtc);
