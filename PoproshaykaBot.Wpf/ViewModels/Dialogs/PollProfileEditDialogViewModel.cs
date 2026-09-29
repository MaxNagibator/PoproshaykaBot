using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Polls;

namespace PoproshaykaBot.Wpf.ViewModels.Dialogs;

public sealed partial class PollProfileEditDialogViewModel : ObservableObject, IDialogViewModel, IAcceptableDialog
{
    public const string NotWrittenNotice = "Профили голосований изменены до перезапуска. В файл изменения не записаны: туда только что перенесены данные предыдущей версии. Перезапустите приложение и повторите правку.";

    private readonly PollProfilesManager _manager;
    private readonly Guid _newProfileId = Guid.NewGuid();
    private Guid? _editTargetId;

    [ObservableProperty]
    private string _dialogTitle = "Создать голосование";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsProfileCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsProfileCommand))]
    private string _pollTitle = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAsProfileCommand))]
    private string _choicesText = string.Empty;

    [ObservableProperty]
    private double _durationSeconds = PollProfile.MinDurationSeconds;

    [ObservableProperty]
    private bool _channelPointsEnabled;

    [ObservableProperty]
    private double _channelPointsPerVote = PollProfile.MinChannelPointsPerVote;

    [ObservableProperty]
    private AutoTriggerItem _selectedTriggerItem;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public PollProfile? Result { get; private set; }

    public bool ShouldStartPoll { get; private set; }

    public bool ProfileNotWritten { get; private set; }

    public event EventHandler<bool>? RequestClose;

    public IReadOnlyList<AutoTriggerItem> TriggerItems { get; } =
    [
        new(PollAutoTriggerEvent.None, "без автозапуска"),
        new(PollAutoTriggerEvent.StreamOnline, "При начале стрима"),
        new(PollAutoTriggerEvent.BroadcastProfileApplied, "При применении профиля трансляции"),
    ];

    [ObservableProperty]
    private double _cooldownMinutes;

    public PollProfileEditDialogViewModel(PollProfilesManager manager)
    {
        _manager = manager;
        _selectedTriggerItem = TriggerItems[0];
    }

    public void Load(PollProfile profile)
    {
        _editTargetId = profile.Id;
        DialogTitle = "Редактировать профиль голосования";
        Name = profile.Name;
        PollTitle = profile.Title;
        ChoicesText = string.Join(Environment.NewLine, profile.Choices);
        DurationSeconds = Math.Clamp(profile.DurationSeconds,
            PollProfile.MinDurationSeconds, PollProfile.MaxDurationSeconds);
        ChannelPointsEnabled = profile.ChannelPointsVotingEnabled;
        ChannelPointsPerVote = Math.Clamp(profile.ChannelPointsPerVote,
            PollProfile.MinChannelPointsPerVote, PollProfile.MaxChannelPointsPerVote);
        SelectedTriggerItem = TriggerItems.FirstOrDefault(t => t.Event == profile.AutoTrigger.Event)
            ?? TriggerItems[0];
        CooldownMinutes = Math.Max(0, profile.AutoTrigger.CooldownMinutes);
    }

    public bool TryAccept()
    {
        if (!CanRun()) return false;
        ExecuteRun();
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void Run() => ExecuteRun();

    [RelayCommand(CanExecute = nameof(CanSaveAsProfile))]
    private void SaveAsProfile()
    {
        var profile = BuildProfile(forceName: true);

        try
        {
            ProfileNotWritten = !_manager.Upsert(profile);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
            return;
        }

        Result = profile;
        ShouldStartPoll = false;
        RequestClose?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, false);

    [RelayCommand]
    private void IncreaseDuration() =>
        DurationSeconds = Math.Min(PollProfile.MaxDurationSeconds, DurationSeconds + 15);

    [RelayCommand]
    private void DecreaseDuration() =>
        DurationSeconds = Math.Max(PollProfile.MinDurationSeconds, DurationSeconds - 15);

    [RelayCommand]
    private void IncreaseChannelPoints() =>
        ChannelPointsPerVote = Math.Min(PollProfile.MaxChannelPointsPerVote, ChannelPointsPerVote + 100);

    [RelayCommand]
    private void DecreaseChannelPoints() =>
        ChannelPointsPerVote = Math.Max(PollProfile.MinChannelPointsPerVote, ChannelPointsPerVote - 100);

    [RelayCommand]
    private void IncreaseCooldown() => CooldownMinutes = Math.Min(1440, CooldownMinutes + 1);

    [RelayCommand]
    private void DecreaseCooldown() => CooldownMinutes = Math.Max(0, CooldownMinutes - 1);

    private void ExecuteRun()
    {
        HasError = false;
        var hasName = !string.IsNullOrWhiteSpace(Name);
        var profile = BuildProfile(forceName: !hasName);

        if (hasName)
        {
            try
            {
                ProfileNotWritten = !_manager.Upsert(profile);
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
                HasError = true;
                return;
            }
        }

        Result = profile;
        ShouldStartPoll = true;
        RequestClose?.Invoke(this, true);
    }

    private bool CanRun() => IsBasicValid();

    private bool CanSaveAsProfile() => IsBasicValid() && !string.IsNullOrWhiteSpace(Name);

    private bool IsBasicValid()
    {
        if (string.IsNullOrWhiteSpace(PollTitle)) return false;
        var count = ParseChoices().Count;
        return count >= PollProfile.MinChoices && count <= PollProfile.MaxChoices;
    }

    private PollProfile BuildProfile(bool forceName)
    {
        var profileId = _editTargetId ?? _newProfileId;
        var profile = new PollProfile { Id = profileId };

        var name = Name.Trim();

        if (name.Length > 0)
        {
            profile.Name = name;
        }
        else if (forceName || string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = $"Свободное голосование {DateTime.Now:HH:mm:ss}";
        }

        profile.Title = PollTitle.Trim();
        profile.Choices = ParseChoices();
        profile.DurationSeconds = (int)Math.Clamp(DurationSeconds,
            PollProfile.MinDurationSeconds, PollProfile.MaxDurationSeconds);
        profile.ChannelPointsVotingEnabled = ChannelPointsEnabled;
        profile.ChannelPointsPerVote = (int)Math.Clamp(ChannelPointsPerVote,
            PollProfile.MinChannelPointsPerVote, PollProfile.MaxChannelPointsPerVote);
        profile.AutoTrigger.Event = SelectedTriggerItem.Event;
        profile.AutoTrigger.CooldownMinutes = (int)Math.Max(0, CooldownMinutes);

        if (SelectedTriggerItem.Event != PollAutoTriggerEvent.BroadcastProfileApplied)
        {
            profile.AutoTrigger.BroadcastProfileId = null;
        }

        return profile;
    }

    private List<string> ParseChoices()
    {
        return ChoicesText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    public sealed record AutoTriggerItem(PollAutoTriggerEvent Event, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
