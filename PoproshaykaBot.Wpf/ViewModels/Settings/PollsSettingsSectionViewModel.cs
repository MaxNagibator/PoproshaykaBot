using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class PollsSettingsSectionViewModel : ObservableObject
{
    public const string NotWrittenNotice = "Настройки опросов применены и работают до перезапуска. В файл они не записаны: туда только что перенесены данные предыдущей версии. Перезапустите приложение и сохраните их ещё раз.";

    private readonly PollsStore _store;
    private readonly TimeProvider _timeProvider;
    private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _hasChanges;

    [ObservableProperty]
    private string? _notice;

    [ObservableProperty]
    private bool _startEnabled;

    [ObservableProperty]
    private string _startTemplate = string.Empty;

    [ObservableProperty]
    private bool _progressEnabled;

    [ObservableProperty]
    private string _progressTemplate = string.Empty;

    [ObservableProperty]
    private bool _endEnabled;

    [ObservableProperty]
    private string _endTemplate = string.Empty;

    [ObservableProperty]
    private bool _terminatedEnabled;

    [ObservableProperty]
    private string _terminatedTemplate = string.Empty;

    [ObservableProperty]
    private bool _archivedEnabled;

    [ObservableProperty]
    private string _archivedTemplate = string.Empty;

    [ObservableProperty]
    private double _progressIntervalSeconds;

    [ObservableProperty]
    private double _historyMaxItems;

    [ObservableProperty]
    private bool _killSwitchToday;

    public PollsSettingsSectionViewModel(PollsStore store, TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
        LoadFromStore();
    }

    public string TabTitle => "Опросы";

    public bool SaveChanges()
    {
        var draft = _store.Load();
        ApplyToDraft(draft);
        var written = _store.Save(draft);
        HasChanges = false;
        Notice = written ? null : NotWrittenNotice;

        return written;
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Save()
    {
        SaveChanges();
    }

    [RelayCommand]
    private void Revert()
    {
        LoadFromStore();
    }

    [RelayCommand]
    private void IncreaseProgressInterval()
    {
        ProgressIntervalSeconds = Math.Min(ProgressIntervalSeconds + 1, 3600);
    }

    [RelayCommand]
    private void DecreaseProgressInterval()
    {
        ProgressIntervalSeconds = Math.Max(ProgressIntervalSeconds - 1, 15);
    }

    [RelayCommand]
    private void IncreaseHistoryMax()
    {
        HistoryMaxItems = Math.Min(HistoryMaxItems + 1, 10000);
    }

    [RelayCommand]
    private void DecreaseHistoryMax()
    {
        HistoryMaxItems = Math.Max(HistoryMaxItems - 1, 10);
    }

    partial void OnStartEnabledChanged(bool value) => MarkChanged();
    partial void OnStartTemplateChanged(string value) => MarkChanged();
    partial void OnProgressEnabledChanged(bool value) => MarkChanged();
    partial void OnProgressTemplateChanged(string value) => MarkChanged();
    partial void OnEndEnabledChanged(bool value) => MarkChanged();
    partial void OnEndTemplateChanged(string value) => MarkChanged();
    partial void OnTerminatedEnabledChanged(bool value) => MarkChanged();
    partial void OnTerminatedTemplateChanged(string value) => MarkChanged();
    partial void OnArchivedEnabledChanged(bool value) => MarkChanged();
    partial void OnArchivedTemplateChanged(string value) => MarkChanged();
    partial void OnProgressIntervalSecondsChanged(double value) => MarkChanged();
    partial void OnHistoryMaxItemsChanged(double value) => MarkChanged();
    partial void OnKillSwitchTodayChanged(bool value) => MarkChanged();

    private void MarkChanged()
    {
        if (!_isLoading)
        {
            HasChanges = true;
        }
    }

    private void LoadFromStore()
    {
        _isLoading = true;

        try
        {
            var settings = _store.Load();
            var t = settings.ChatTemplates;

            StartEnabled = t.StartEnabled;
            StartTemplate = t.StartTemplate;
            ProgressEnabled = t.ProgressEnabled;
            ProgressTemplate = t.ProgressTemplate;
            EndEnabled = t.EndEnabled;
            EndTemplate = t.EndTemplate;
            TerminatedEnabled = t.TerminatedEnabled;
            TerminatedTemplate = t.TerminatedTemplate;
            ArchivedEnabled = t.ArchivedEnabled;
            ArchivedTemplate = t.ArchivedTemplate;
            ProgressIntervalSeconds = Math.Clamp(t.ProgressAnnounceIntervalSeconds, 15, 3600);
            HistoryMaxItems = Math.Clamp(settings.HistoryMaxItems, 10, 10000);

            var todayUtc = _timeProvider.GetUtcNow().UtcDateTime.Date;
            KillSwitchToday = settings.AutoTriggerKillSwitchDateUtc?.Date == todayUtc;
        }
        finally
        {
            _isLoading = false;
        }

        HasChanges = false;
    }

    private void ApplyToDraft(PollsSettings draft)
    {
        var t = draft.ChatTemplates;

        t.StartEnabled = StartEnabled;
        t.StartTemplate = StartTemplate;
        t.ProgressEnabled = ProgressEnabled;
        t.ProgressTemplate = ProgressTemplate;
        t.EndEnabled = EndEnabled;
        t.EndTemplate = EndTemplate;
        t.TerminatedEnabled = TerminatedEnabled;
        t.TerminatedTemplate = TerminatedTemplate;
        t.ArchivedEnabled = ArchivedEnabled;
        t.ArchivedTemplate = ArchivedTemplate;
        t.ProgressAnnounceIntervalSeconds = (int)ProgressIntervalSeconds;
        draft.HistoryMaxItems = (int)HistoryMaxItems;
        draft.AutoTriggerKillSwitchDateUtc = KillSwitchToday
            ? _timeProvider.GetUtcNow().UtcDateTime.Date
            : null;
    }
}
