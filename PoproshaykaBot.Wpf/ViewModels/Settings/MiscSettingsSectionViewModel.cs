using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class MiscSettingsSectionViewModel : ObservableObject, IDisposable
{
    private static readonly JsonSerializerOptions ImportJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly SettingsManager _settingsManager;
    private readonly BroadcastProfilesManager _broadcastProfiles;
    private readonly IDialogService _dialogService;
    private readonly IShellLauncher _shellLauncher;
    private readonly IFilePicker _filePicker;
    private readonly ILegacyImportDialog _legacyImportDialog;

    public MiscSettingsSectionViewModel(
        SettingsManager settingsManager,
        BroadcastProfilesManager broadcastProfiles,
        IDialogService dialogService,
        IShellLauncher shellLauncher,
        IFilePicker filePicker,
        ILegacyImportDialog legacyImportDialog)
    {
        _settingsManager = settingsManager;
        _broadcastProfiles = broadcastProfiles;
        _dialogService = dialogService;
        _shellLauncher = shellLauncher;
        _filePicker = filePicker;
        _legacyImportDialog = legacyImportDialog;
    }

    public void Dispose()
    {
    }

    [RelayCommand]
    private void ShowAbout()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Неизвестно";

        _dialogService.Info("О программе", $"PoproshaykaBot\n\nВерсия: {version}\n\nTwitch бот для стримеров");
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        var dir = AppPaths.BaseDirectory;

        try
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch (Exception ex)
        {
            _dialogService.Error("Ошибка", $"Ошибка открытия папки с настройками: {ex.Message}");
            return;
        }

        if (!_shellLauncher.Open(dir))
        {
            _dialogService.Error("Ошибка", "Не удалось открыть папку с настройками.");
        }
    }

    [RelayCommand]
    private void ImportLegacyData()
    {
        _legacyImportDialog.Show();
    }

    [RelayCommand]
    private void ResetAllSettings()
    {
        if (!_dialogService.ConfirmWarning("Сброс настроек", "Вы уверены, что хотите сбросить все настройки к значениям по умолчанию?\n\nЭто действие нельзя отменить."))
        {
            return;
        }

        try
        {
            var written = _settingsManager.SaveSettings(new AppSettings());

            _dialogService.Info("Сброс настроек", written
                ? "Настройки сброшены к значениям по умолчанию.\n\nПерезапустите приложение для применения изменений."
                : "Настройки сброшены только в этом сеансе. В файл они не записаны: туда только что перенесены данные предыдущей версии. Перезапустите приложение и повторите сброс.");
        }
        catch (Exception ex)
        {
            _dialogService.Error("Ошибка", $"Ошибка сброса настроек: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ImportBroadcastProfiles()
    {
        var path = _filePicker.PickFile(new("Импорт профилей трансляций из JSON", "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*"));

        if (path is null)
        {
            return;
        }

        List<ExternalTwitchProfile>? incoming;

        try
        {
            var json = File.ReadAllText(path);
            incoming = JsonSerializer.Deserialize<List<ExternalTwitchProfile>>(json, ImportJsonOptions);
        }
        catch (Exception ex)
        {
            _dialogService.Error("Ошибка импорта", $"Не удалось прочитать файл: {ex.Message}");
            return;
        }

        if (incoming is null || incoming.Count == 0)
        {
            _dialogService.Info("Импорт", "В файле нет профилей.");
            return;
        }

        var imported = 0;
        var skipped = 0;
        var written = true;
        var existingNames = new HashSet<string>(
            _broadcastProfiles.GetAll().Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var external in incoming)
        {
            if (string.IsNullOrWhiteSpace(external.Name))
            {
                skipped++;
                continue;
            }

            var name = external.Name;
            var suffix = 2;

            while (existingNames.Contains(name))
            {
                name = $"{external.Name} ({suffix++})";
            }

            var profile = new BroadcastProfile
            {
                Name = name,
                Title = external.Title ?? string.Empty,
                GameId = external.CategoryId ?? string.Empty,
            };

            try
            {
                written &= _broadcastProfiles.Upsert(profile);
                existingNames.Add(name);
                imported++;
            }
            catch (InvalidOperationException)
            {
                skipped++;
            }
        }

        var counts = $"Импортировано: {imported}. Пропущено: {skipped}.";

        if (!written)
        {
            _dialogService.Warning("Импорт профилей", $"{counts} {BroadcastProfilesTileViewModel.NotWrittenNotice}");
            return;
        }

        _dialogService.Info("Импорт профилей", counts);
    }

    private sealed record ExternalTwitchProfile(string? Name, string? Title, string? CategoryId);
}
