using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Polling;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using System.Collections.ObjectModel;

namespace PoproshaykaBot.Wpf.ViewModels.Tiles;

public sealed partial class PollsTileViewModel : DashboardTileViewModel, IDisposable
{
    private readonly IPollController _controller;
    private readonly PollProfilesManager _profiles;
    private readonly IDialogService _dialogService;
    private readonly List<IDisposable> _subs = [];
    private readonly IUiTimer _liveTimer;

    [ObservableProperty]
    private PollSnapshot? _currentSnapshot;

    [ObservableProperty]
    private bool _hasSnapshot;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _footerText = string.Empty;

    [ObservableProperty]
    private string _footerStatusKey = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private string? _unsavedNotice;

    public ObservableCollection<PollChoiceRowViewModel> Choices { get; } = [];

    public override PackIconLucideKind Icon => PackIconLucideKind.ChartColumn;

    public PollsTileViewModel(
        IPollController controller,
        PollProfilesManager profiles,
        PollSnapshotStore snapshotStore,
        IEventBus bus,
        IDialogService dialogService,
        IUiDispatcher uiDispatcher) : base("polls-control", "Опросы", maxWidth: 500, maxHeight: 320, minHeight: 144)
    {
        _controller = controller;
        _profiles = profiles;
        _dialogService = dialogService;

        HeaderActions.Add(new ToolbarItemViewModel(PackIconLucideKind.Plus, CreateAdHocCommand, toolTip: "Создать опрос"));
        HeaderActions.Add(new ToolbarItemViewModel(PackIconLucideKind.List, CreateFromProfileCommand, toolTip: "Из профиля…"));

        _liveTimer = uiDispatcher.CreateTimer(TimeSpan.FromSeconds(1), OnLiveTimerTick);
        _liveTimer.Start();

        _subs.Add(bus.SubscribeOnUi<PollStarted>(@event =>
        {
            ClearStatus();
            ApplySnapshot(@event.Snapshot);
        }));
        _subs.Add(bus.SubscribeOnUi<PollProgressed>(@event => ApplySnapshot(@event.Snapshot)));
        _subs.Add(bus.SubscribeOnUi<PollFinalized>(@event => ApplySnapshot(@event.Snapshot)));
        _subs.Add(bus.SubscribeOnUi<PollTerminated>(@event => ApplySnapshot(@event.Snapshot)));
        _subs.Add(bus.SubscribeOnUi<PollArchived>(@event => ApplySnapshot(@event.Snapshot)));
        _subs.Add(bus.SubscribeOnUi<PollStartFailed>(@event => ShowStatus($"✗ {@event.SafeMessage}")));

        var initial = snapshotStore.Current;

        if (initial != null)
        {
            ApplySnapshot(initial);
        }
    }

    [RelayCommand]
    private async Task CreateAdHocAsync()
    {
        var dialog = new PollProfileEditDialogViewModel(_profiles);

        if (!await _dialogService.ShowAsync(dialog))
        {
            return;
        }

        if (dialog.ProfileNotWritten)
        {
            UnsavedNotice = PollProfileEditDialogViewModel.NotWrittenNotice;
        }

        if (dialog.ShouldStartPoll && dialog.Result is not null)
        {
            await StartPollAsync(dialog.Result);
        }
    }

    [RelayCommand]
    private async Task CreateFromProfileAsync()
    {
        var dialog = new PollFromProfileDialogViewModel(_profiles, _dialogService);
        var accepted = await _dialogService.ShowAsync(dialog);

        if (dialog.ProfilesNotWritten)
        {
            UnsavedNotice = PollProfileEditDialogViewModel.NotWrittenNotice;
        }

        if (!accepted || dialog.Result is null)
        {
            return;
        }

        await StartPollAsync(dialog.Result);
    }

    [RelayCommand]
    private async Task EndPollAsync()
    {
        var snapshot = CurrentSnapshot;

        if (snapshot is null || snapshot.Status != PollSnapshotStatus.Active)
        {
            return;
        }

        var confirmed = _dialogService.ConfirmWarning(
            "Завершить голосование",
            $"Завершить голосование «{snapshot.Title}» и показать результат зрителям?");

        if (!confirmed)
        {
            return;
        }

        try
        {
            var ok = await _controller.EndAsync(true, CancellationToken.None);

            if (!ok)
            {
                ShowStatus("✗ Не удалось завершить голосование");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"✗ {ex.Message}");
        }
    }

    private async Task StartPollAsync(PollProfile profile)
    {
        ShowStatus("Запуск голосования…");

        try
        {
            await _controller.StartAsync(profile, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ShowStatus($"✗ {ex.Message}");
        }
    }

    private void OnLiveTimerTick()
    {
        var snapshot = CurrentSnapshot;

        if (snapshot?.Status == PollSnapshotStatus.Active)
        {
            FooterText = BuildActiveFooter(snapshot);
        }
    }

    private void ApplySnapshot(PollSnapshot snapshot)
    {
        CurrentSnapshot = snapshot;
        HasSnapshot = true;
        IsActive = snapshot.Status == PollSnapshotStatus.Active;

        var totalVotes = snapshot.TotalVotes;
        var leaderVotes = snapshot.Choices.Count > 0 ? snapshot.Choices.Max(c => c.Votes) : 0;

        Choices.Clear();

        foreach (var choice in snapshot.Choices)
        {
            var percent = totalVotes > 0
                ? (int)Math.Round(choice.Votes * 100.0 / totalVotes)
                : 0;

            Choices.Add(new PollChoiceRowViewModel(
                choice.Title,
                choice.Votes,
                percent,
                choice.Votes == leaderVotes && leaderVotes > 0));
        }

        FooterText = BuildFooterText(snapshot);
        FooterStatusKey = ResolveFooterStatusKey(snapshot.Status);
    }

    private static string BuildFooterText(PollSnapshot snapshot)
    {
        return snapshot.Status switch
        {
            PollSnapshotStatus.Active => BuildActiveFooter(snapshot),
            PollSnapshotStatus.Completed => BuildCompletedFooter(snapshot),
            PollSnapshotStatus.Terminated => $"Прервано – {snapshot.TotalVotes} голос.",
            PollSnapshotStatus.Archived => $"Архив – {snapshot.TotalVotes} голос.",
            PollSnapshotStatus.Moderated => "На модерации",
            PollSnapshotStatus.Invalid => "Некорректное состояние",
            _ => string.Empty,
        };
    }

    private static string BuildActiveFooter(PollSnapshot snapshot)
    {
        var remaining = snapshot.EndsAtUtc - DateTime.UtcNow;

        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        return $"Идёт – {(int)remaining.TotalMinutes}:{remaining.Seconds:D2} – {snapshot.TotalVotes} голос.";
    }

    private static string BuildCompletedFooter(PollSnapshot snapshot)
    {
        if (snapshot.LeaderIsTie)
        {
            return $"Завершено (ничья) – {snapshot.TotalVotes} голос.";
        }

        if (snapshot.Leader != null)
        {
            return $"Завершено: «{snapshot.Leader.Title}» – {snapshot.TotalVotes} голос.";
        }

        return $"Завершено – {snapshot.TotalVotes} голос.";
    }

    private static string ResolveFooterStatusKey(PollSnapshotStatus status)
    {
        return status switch
        {
            PollSnapshotStatus.Active => "active",
            PollSnapshotStatus.Completed => "ok",
            PollSnapshotStatus.Invalid => "error",
            PollSnapshotStatus.Moderated => "error",
            _ => string.Empty,
        };
    }


    private void ShowStatus(string message)
    {
        StatusMessage = message;
        HasStatusMessage = true;
    }

    private void ClearStatus()
    {
        StatusMessage = string.Empty;
        HasStatusMessage = false;
    }

    public void Dispose()
    {
        _liveTimer.Stop();

        foreach (var sub in _subs)
        {
            sub.Dispose();
        }

        _subs.Clear();
    }
}
