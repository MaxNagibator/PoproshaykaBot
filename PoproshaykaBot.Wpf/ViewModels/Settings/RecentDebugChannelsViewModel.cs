using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Wpf.Bootstrap;
using System.Collections.ObjectModel;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class RecentDebugChannelsViewModel : ObservableObject
{
    private readonly DebugChannelStore _store;
    private readonly ChannelLiveStatusReader _reader;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly Action<string> _choose;

    private ChannelLiveStatusReport? _lastReport;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isRefreshing;

    [ObservableProperty]
    private string? _note;

    public RecentDebugChannelsViewModel(
        DebugChannelStore store,
        ChannelLiveStatusReader reader,
        TimeProvider timeProvider,
        ILogger logger,
        Action<string> choose)
    {
        _store = store;
        _reader = reader;
        _timeProvider = timeProvider;
        _logger = logger;
        _choose = choose;
    }

    public ObservableCollection<RecentDebugChannelRowViewModel> Rows { get; } = [];

    public bool HasRows => Rows.Count > 0;

    public void Reload()
    {
        var now = _timeProvider.GetUtcNow();

        Rows.Clear();

        foreach (var channel in _store.LoadRecent())
        {
            var row = new RecentDebugChannelRowViewModel(channel.Login, channel.LastUsedAt, now);
            ApplyReport(row);
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(HasRows));
        RefreshCommand.NotifyCanExecuteChanged();
    }

    public void OnShown()
    {
        Reload();

        if (!App.IsHeadless && RefreshCommand.CanExecute(null))
        {
            _ = RefreshCommand.ExecuteAsync(null);
        }
    }

    private bool CanRefresh()
    {
        return !IsRefreshing && HasRows;
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        Note = "Проверяем, кто в эфире…";

        try
        {
            var report = await _reader.ReadAsync(Rows.Select(row => row.Login).ToArray());
            _lastReport = report;

            foreach (var row in Rows)
            {
                ApplyReport(row);
            }

            Note = report.UnknownReason is { } reason ? $"Состояние неизвестно: {reason}" : null;
        }
        catch (Exception exception)
        {
            _logger.RecentDebugChannelsStatusFailed(exception);
            Note = "Состояние неизвестно: проверка сорвалась. Подробности – в журнале.";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void Use(RecentDebugChannelRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        _logger.RecentDebugChannelChosen(row.Login);
        _choose(row.Login);
    }

    [RelayCommand]
    private void Remove(RecentDebugChannelRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            _store.RemoveRecent(row.Login);
            _logger.RecentDebugChannelRemoved(row.Login);
        }
        catch (Exception exception)
        {
            _logger.RecentDebugChannelRemoveFailed(exception, row.Login);
            Reload();
            Note = "Канал не убран из списка: файл настроек отладки не записался. Подробности – в журнале.";
            return;
        }

        Reload();
    }

    private void ApplyReport(RecentDebugChannelRowViewModel row)
    {
        var status = _lastReport?.Channels.FirstOrDefault(channel => string.Equals(channel.Login, row.Login, StringComparison.OrdinalIgnoreCase));
        row.Apply(status, _lastReport?.UnknownReason);
    }
}
