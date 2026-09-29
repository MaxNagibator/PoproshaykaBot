namespace PoproshaykaBot.Core.Settings.Migrations.LegacyImport;

public sealed record LegacyImportSkip(string RelativeTargetPath, LegacyImportSkipReason Reason);
