using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings.Migrations.LegacyImport;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Wpf.Infrastructure;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace PoproshaykaBot.Wpf.ViewModels.Migration;

public sealed partial class LegacyImportViewModel : ObservableObject
{
    private readonly IFilePicker _filePicker;
    private readonly ILogger _logger;
    private readonly bool _isSettingsEntry;
    private readonly StatisticsAutoSaver? _statisticsAutoSaver;
    private readonly SettingsWriteGate? _settingsWriteGate;
    private readonly UiSettingsWriteGuard? _uiSettingsGuard;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    [NotifyPropertyChangedFor(nameof(AuthorizationNotice))]
    private LegacyImportSourceViewModel? _selectedSource;

    [ObservableProperty]
    private bool _overwriteExisting;

    [ObservableProperty]
    private bool _neverAskAgain;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string? _pickerNotice;

    [ObservableProperty]
    private string _resultHeadline = string.Empty;

    [ObservableProperty]
    private string? _resultAuthorizationNotice;

    [ObservableProperty]
    private string? _resultUnmigratedNotice;

    [ObservableProperty]
    private string? _resultRestartNotice;

    public LegacyImportViewModel(
        IReadOnlyList<LegacyDataSummary> candidates,
        bool hasOwnData,
        bool isSettingsEntry,
        IFilePicker filePicker,
        ILogger logger,
        StatisticsAutoSaver? statisticsAutoSaver = null,
        SettingsWriteGate? settingsWriteGate = null,
        UiSettingsWriteGuard? uiSettingsGuard = null)
    {
        _filePicker = filePicker;
        _logger = logger;
        _isSettingsEntry = isSettingsEntry;
        _statisticsAutoSaver = statisticsAutoSaver;
        _settingsWriteGate = settingsWriteGate;
        _uiSettingsGuard = uiSettingsGuard;
        CanOverwriteExisting = hasOwnData;
        CanDismiss = !isSettingsEntry;

        foreach (var candidate in candidates)
        {
            Add(candidate);
        }

        Explanation = candidates.Count > 0
            ? "Похоже, бот уже работал на этом компьютере. Настройки и накопленную статистику можно перенести сюда."
            : "Если бот уже работал на этом компьютере, укажите папку с его данными – настройки и статистика переедут сюда. Либо начните с чистого листа.";

        SkipButtonText = candidates.Count > 0 ? "Пропустить" : "Начать с чистого листа";
    }

    public event EventHandler? CloseRequested;

    public ObservableCollection<LegacyImportSourceViewModel> Sources { get; } = [];

    public ObservableCollection<string> ResultCounts { get; } = [];

    public ObservableCollection<string> ResultFailures { get; } = [];

    public bool CanOverwriteExisting { get; }

    public bool CanDismiss { get; }

    public string Explanation { get; }

    public string SkipButtonText { get; }

    public LegacyImportResult? Result { get; private set; }

    public string? AuthorizationNotice => SelectedSource?.Summary.HasOAuthTokens == true
        ? "Доступ к Twitch придётся выдать заново: набор разрешений изменился, и прежний вход больше не подойдёт."
        : null;

    private bool HasSelection => SelectedSource is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ImportAsync()
    {
        if (SelectedSource is not { } source)
        {
            return;
        }

        var path = source.Path;
        var overwrite = OverwriteExisting;

        // TODO: перенос идёт на UI-потоке – окно не отвечает, пока копируются файлы, и прогресса не видно;
        // уводить в фоновую задачу с индикатором, когда появится источник, у которого статистика зрителей
        // и история стримов копируются достаточно долго, чтобы застывшее окно бросалось в глаза
        LegacyImportResult Import()
        {
            return LegacyDataImporter.Import(path, overwrite, _logger);
        }

        Task<LegacyImportResult> WithStatisticsScope()
        {
            return _statisticsAutoSaver is null
                ? Task.FromResult(Import())
                : _statisticsAutoSaver.RunExternalWriteAsync(Import, static imported => imported.ExternalWrite);
        }

        LegacyImportResult result;
        var uiPreferencesKept = false;
        _uiSettingsGuard?.Suspend();

        try
        {
            result = _settingsWriteGate is null
                ? await WithStatisticsScope().ConfigureAwait(true)
                : await _settingsWriteGate
                    .RunExternalWriteAsync(WithStatisticsScope, static imported => imported.RewrittenSettingsFiles)
                    .ConfigureAwait(true);

            uiPreferencesKept = !result.CopiedUiPreferences;
        }
        finally
        {
            _uiSettingsGuard?.Resume(rewritten: !uiPreferencesKept);
        }

        Result = result;
        ResultHeadline = LegacyImportText.BuildHeadline(result);
        Fill(ResultCounts, LegacyImportText.BuildCounts(result));
        Fill(ResultFailures, LegacyImportText.BuildFailures(result));

        ResultAuthorizationNotice = result.RequiresReauthorization
            ? "После запуска нужно будет заново разрешить доступ к Twitch – и для бота, и для стримера."
            : null;

        ResultUnmigratedNotice = LegacyImportText.DescribeUnmigrated(result);

        ResultRestartNotice = LegacyImportText.BuildRestartNotice(result, _isSettingsEntry);

        IsCompleted = true;
    }

    [RelayCommand]
    private void PickFolder()
    {
        var path = _filePicker.PickFolder("Папка с данными предыдущей версии");

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var known = Sources.FirstOrDefault(source => AreSamePath(source.Path, path));

        if (known is not null)
        {
            PickerNotice = null;
            known.IsSelected = true;
            return;
        }

        if (LegacyDataScanner.Inspect(path, _logger) is not { } summary)
        {
            PickerNotice = "В этой папке нет данных бота. Обычно они лежат в папке PoproshaykaBot.";
            return;
        }

        PickerNotice = null;
        Add(summary).IsSelected = true;
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static bool AreSamePath(string first, string second)
    {
        return string.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return path;
        }
    }

    private static void Fill(ObservableCollection<string> target, IReadOnlyList<string> lines)
    {
        target.Clear();

        foreach (var line in lines)
        {
            target.Add(line);
        }
    }

    private LegacyImportSourceViewModel Add(LegacyDataSummary summary)
    {
        var source = new LegacyImportSourceViewModel(summary);
        source.PropertyChanged += OnSourcePropertyChanged;
        Sources.Add(source);

        return source;
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LegacyImportSourceViewModel.IsSelected))
        {
            SelectedSource = Sources.FirstOrDefault(source => source.IsSelected);
        }
    }
}
