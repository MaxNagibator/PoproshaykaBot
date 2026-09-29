namespace PoproshaykaBot.Core.Settings.Migrations.LegacyImport;

public enum LegacyImportSkipReason
{
    None = 0,
    TargetExists = 1,
    SameLocation = 2,
}
