namespace PoproshaykaBot.Core.Settings.Migrations;

public readonly record struct SettingsMigrationResult(bool Changed, IReadOnlyList<string> SplitFiles)
{
    public static SettingsMigrationResult Unchanged { get; } = new(false, []);
}
