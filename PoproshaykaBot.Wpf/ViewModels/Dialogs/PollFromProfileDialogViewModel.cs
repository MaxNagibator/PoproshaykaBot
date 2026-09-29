using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Polls;
using System.Collections.ObjectModel;

namespace PoproshaykaBot.Wpf.ViewModels.Dialogs;

public sealed partial class PollFromProfileDialogViewModel : ObservableObject, IDialogViewModel, IAcceptableDialog
{
    private readonly PollProfilesManager _manager;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyPropertyChangedFor(nameof(PreviewText))]
    private PollProfile? _selectedProfile;

    [ObservableProperty]
    private string? _notice;

    public ObservableCollection<PollProfile> Profiles { get; } = [];

    public bool ProfilesNotWritten => Notice is not null;

    public bool IsEmpty => Profiles.Count == 0;

    public string PreviewText => BuildPreviewText(_selectedProfile);

    public PollProfile? Result { get; private set; }

    public event EventHandler<bool>? RequestClose;

    public PollFromProfileDialogViewModel(PollProfilesManager manager, IDialogService dialogService)
    {
        _manager = manager;
        _dialogService = dialogService;
        LoadProfiles(null);
    }

    public bool TryAccept()
    {
        if (_selectedProfile is null)
        {
            return false;
        }

        Result = _selectedProfile;
        RequestClose?.Invoke(this, true);
        return true;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Launch()
    {
        Result = _selectedProfile;
        RequestClose?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (_selectedProfile is null)
        {
            return;
        }

        var editVm = new PollProfileEditDialogViewModel(_manager);
        editVm.Load(_selectedProfile);

        if (!await _dialogService.ShowAsync(editVm))
        {
            return;
        }

        if (editVm.ProfileNotWritten)
        {
            ReportWrite(false);
        }

        if (editVm.ShouldStartPoll)
        {
            Result = editVm.Result;
            RequestClose?.Invoke(this, true);
        }
        else
        {
            LoadProfiles(editVm.Result?.Id);
        }
    }

    [RelayCommand]
    private async Task CreateProfileAsync()
    {
        var editVm = new PollProfileEditDialogViewModel(_manager);

        if (!await _dialogService.ShowAsync(editVm))
        {
            return;
        }

        if (editVm.ProfileNotWritten)
        {
            ReportWrite(false);
        }

        if (editVm.ShouldStartPoll)
        {
            Result = editVm.Result;
            RequestClose?.Invoke(this, true);
            return;
        }

        LoadProfiles(editVm.Result?.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (_selectedProfile is null)
        {
            return;
        }

        var confirmed = _dialogService.ConfirmWarning(
            "Удаление профиля",
            $"Удалить профиль «{_selectedProfile.Name}»?");

        if (!confirmed)
        {
            return;
        }

        ReportWrite(_manager.Remove(_selectedProfile.Id));
        LoadProfiles(null);
    }

    private void ReportWrite(bool written)
    {
        Notice = written ? null : PollProfileEditDialogViewModel.NotWrittenNotice;
        OnPropertyChanged(nameof(ProfilesNotWritten));
    }

    private bool HasSelection() => _selectedProfile != null;

    private void LoadProfiles(Guid? preserveId)
    {
        var all = _manager.GetAll();

        Profiles.Clear();

        foreach (var p in all)
        {
            Profiles.Add(p);
        }

        OnPropertyChanged(nameof(IsEmpty));

        if (Profiles.Count == 0)
        {
            SelectedProfile = null;
            return;
        }

        if (preserveId.HasValue)
        {
            var match = Profiles.FirstOrDefault(p => p.Id == preserveId.Value);

            if (match != null)
            {
                SelectedProfile = match;
                return;
            }
        }

        SelectedProfile = Profiles[0];
    }

    private static string BuildPreviewText(PollProfile? profile)
    {
        if (profile is null)
        {
            return "Нет выбранного профиля.";
        }

        var lines = new List<string>
        {
            $"Вопрос: «{profile.Title}»",
            $"Варианты ({profile.Choices.Count}):",
        };

        lines.AddRange(profile.Choices.Select(c => $"  • {c}"));
        lines.Add($"Длительность: {profile.DurationSeconds} сек");

        if (profile.ChannelPointsVotingEnabled)
        {
            lines.Add($"Баллы канала: {profile.ChannelPointsPerVote} за голос");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
