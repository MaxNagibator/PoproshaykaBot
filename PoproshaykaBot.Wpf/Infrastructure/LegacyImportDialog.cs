using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Settings.Migrations.LegacyImport;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.ViewModels.Migration;
using PoproshaykaBot.Wpf.Views.Migration;

namespace PoproshaykaBot.Wpf.Infrastructure;

public sealed class LegacyImportDialog(
    IFilePicker filePicker,
    StatisticsAutoSaver statisticsAutoSaver,
    SettingsWriteGate settingsWriteGate,
    UiSettingsWriteGuard uiSettingsGuard,
    ILogger<LegacyImportDialog> logger) : ILegacyImportDialog
{
    public LegacyImportResult? Show()
    {
        if (App.IsHeadless)
        {
            logger.LegacyImportSuppressedHeadless();
            return null;
        }

        var candidates = LegacyDataScanner.Scan(logger);
        var hasOwnData = LegacyDataScanner.Inspect(AppPaths.BaseDirectory, logger) is not null;

        logger.LegacyImportOpenedFromSettings(candidates.Count);

        var viewModel = new LegacyImportViewModel(
            candidates,
            hasOwnData,
            isSettingsEntry: true,
            filePicker,
            logger,
            statisticsAutoSaver,
            settingsWriteGate,
            uiSettingsGuard);

        var window = new LegacyImportWindow(viewModel)
        {
            Owner = DialogOwner.Resolve(),
        };

        window.ShowDialog();

        return viewModel.Result;
    }
}
