using PoproshaykaBot.Core.Settings.Migrations.LegacyImport;

namespace PoproshaykaBot.Wpf.Infrastructure;

public interface ILegacyImportDialog
{
    LegacyImportResult? Show();
}
