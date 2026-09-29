using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Settings.Migrations.LegacyImport;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.ViewModels.Migration;
using PoproshaykaBot.Wpf.Views.Migration;
using Serilog;
using Serilog.Extensions.Logging;
using System.IO;

namespace PoproshaykaBot.Wpf;

public partial class App
{
    private static ISettingsStore OfferLegacyDataImport(ISettingsStore uiSettings, string uiSettingsPath)
    {
        if (uiSettings.GetBool(SettingsKeys.LegacyImportDismissed))
        {
            return uiSettings;
        }

        using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var logger = loggerFactory.CreateLogger(nameof(LegacyDataImporter));

        try
        {
            var candidates = LegacyDataScanner.Scan(logger);
            var hasOwnData = LegacyDataScanner.Inspect(AppPaths.BaseDirectory, logger) is not null;

            if (candidates.Count == 0 && hasOwnData)
            {
                return uiSettings;
            }

            var viewModel = new LegacyImportViewModel(candidates, hasOwnData, isSettingsEntry: false, new FilePicker(), logger);
            var window = new LegacyImportWindow(viewModel);

            HostLog.Information("Предложен перенос данных предыдущей версии: источников – {Count}", candidates.Count);

            window.ShowDialog();

            if (viewModel.Result is null)
            {
                HostLog.Information("Пользователь отказался от переноса данных предыдущей версии");
            }

            if (viewModel.NeverAskAgain)
            {
                uiSettings.SetBool(SettingsKeys.LegacyImportDismissed, true);
                HostLog.Information("Предложение переноса данных отключено пользователем");
            }

            return ReloadUiSettingsIfImported(uiSettings, uiSettingsPath, viewModel.Result);
        }
        catch (Exception exception)
        {
            HostLog.Error(exception, "Диалог переноса данных предыдущей версии не открылся");
            return uiSettings;
        }
    }

    private static ISettingsStore ReloadUiSettingsIfImported(ISettingsStore uiSettings, string uiSettingsPath, LegacyImportResult? result)
    {
        if (result?.CopiedUiPreferences != true)
        {
            return uiSettings;
        }

        uiSettings.Close();

        if (uiSettings is IDisposable disposable)
        {
            disposable.Dispose();
        }

        ISettingsStore reloaded = new SettingsStore(uiSettingsPath);

        var themeKey = reloaded.GetStringValue(SettingsKeys.Theme);
        ThemeManager.Apply(string.IsNullOrWhiteSpace(themeKey) ? AppThemes.LightKey : themeKey);
        FontScaleManager.Apply(reloaded.GetDouble(SettingsKeys.FontScale, FontScaleManager.DefaultScale));

        HostLog.Information("Перенесённые настройки вида перечитаны из {Path}", uiSettingsPath);

        return reloaded;
    }
}
