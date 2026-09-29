using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepShell.Services;
using KeepShell.ViewModels;
using MahApps.Metro.IconPacks;
using PoproshaykaBot.Core.Broadcast;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Broadcasting;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.ViewModels.Tiles;

public sealed partial class BroadcastStatusTileViewModel : DashboardTileViewModel, IDisposable
{
    private readonly BroadcastScheduler _scheduler;
    private readonly SettingsManager _settings;
    private readonly IStreamStatus _streamStatus;
    private readonly IChannelProvider _channelProvider;
    private readonly IDialogService _dialogService;
    private readonly List<IDisposable> _subscriptions = [];

    private readonly ToolbarItemViewModel _startItem;
    private readonly ToolbarItemViewModel _stopItem;
    private readonly ToolbarItemViewModel _modeToggleItem;
    private readonly ToolbarItemViewModel _sendNowItem;

    [ObservableProperty]
    private string _statusText = "Неактивна";

    [ObservableProperty]
    private StatusSeverity _statusSeverity = StatusSeverity.Error;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _modeText = "Ручной";

    [ObservableProperty]
    private int _sentCount;

    [ObservableProperty]
    private string _nextBroadcastText = "–";

    public BroadcastStatusTileViewModel(
        BroadcastScheduler scheduler,
        SettingsManager settings,
        IStreamStatus streamStatus,
        IChannelProvider channelProvider,
        IEventBus eventBus,
        IDialogService dialogService)
        : base("broadcast-status", "Рассылка", maxWidth: 360, minHeight: 160)
    {
        _scheduler = scheduler;
        _settings = settings;
        _streamStatus = streamStatus;
        _channelProvider = channelProvider;
        _dialogService = dialogService;

        _startItem = new ToolbarItemViewModel(PackIconLucideKind.Play, StartCommand, "Запустить рассылку");
        _stopItem = new ToolbarItemViewModel(PackIconLucideKind.Square, StopCommand, "Остановить рассылку");
        _modeToggleItem = new ToolbarItemViewModel(PackIconLucideKind.Repeat, ModeToggleCommand, "Переключить режим");
        _sendNowItem = new ToolbarItemViewModel(PackIconLucideKind.Send, SendNowCommand, "Отправить сейчас");

        HeaderActions.Add(_startItem);
        HeaderActions.Add(_stopItem);
        HeaderActions.Add(_modeToggleItem);
        HeaderActions.Add(_sendNowItem);

        _subscriptions.Add(eventBus.SubscribeOnUi<BroadcastSchedulerStateChanged>(_ => RefreshState()));
        _subscriptions.Add(eventBus.SubscribeOnUi<StreamWentOnline>(_ => RefreshState()));
        _subscriptions.Add(eventBus.SubscribeOnUi<StreamWentOffline>(_ => RefreshState()));
        _subscriptions.Add(eventBus.SubscribeOnUi<StreamMetadataResolved>(_ => RefreshState()));
        _subscriptions.Add(eventBus.SubscribeOnUi<BotLifecyclePhaseChanged>(_ => RefreshState()));

        RefreshState();
    }

    public override PackIconLucideKind Icon => PackIconLucideKind.Megaphone;

    public override bool SizesToContent => true;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        var channel = _channelProvider.Channel;
        if (!string.IsNullOrEmpty(channel))
        {
            _scheduler.Start(channel);
        }

        RefreshState();
    }

    private bool CanStart()
    {
        return !IsActive && !_settings.Current.Twitch.AutoBroadcast.AutoBroadcastEnabled;
    }

    [RelayCommand]
    private void Stop()
    {
        _scheduler.Stop();
        RefreshState();
    }

    [RelayCommand]
    private void ModeToggle()
    {
        var willEnableAuto = !_settings.Current.Twitch.AutoBroadcast.AutoBroadcastEnabled;

        if (willEnableAuto && _scheduler.IsActive && _streamStatus.CurrentStatus != StreamStatus.Online)
        {
            var confirmed = _dialogService.Confirm(
                "Переключение режима",
                "При переключении в автоматический режим активная рассылка будет остановлена, так как стрим сейчас оффлайн.\n\nПродолжить?");

            if (!confirmed)
            {
                return;
            }
        }

        _settings.Mutate(settings => settings.Twitch.AutoBroadcast.AutoBroadcastEnabled = willEnableAuto);

        if (willEnableAuto)
        {
            if (_streamStatus.CurrentStatus == StreamStatus.Online)
            {
                var channel = _channelProvider.Channel;
                if (!_scheduler.IsActive && !string.IsNullOrEmpty(channel))
                {
                    _scheduler.Start(channel);
                }
            }
            else
            {
                _scheduler.Stop();
            }
        }

        RefreshState();
    }

    [RelayCommand(CanExecute = nameof(CanSendNow))]
    private async Task SendNowAsync()
    {
        _sendNowItem.IsEnabled = false;
        await _scheduler.ManualSendAsync();
        _sendNowItem.IsEnabled = IsActive;
    }

    private bool CanSendNow() => IsActive;

    private void RefreshState()
    {
        var isActive = _scheduler.IsActive;
        var isAuto = _settings.Current.Twitch.AutoBroadcast.AutoBroadcastEnabled;
        var streamOnline = _streamStatus.CurrentStatus == StreamStatus.Online;

        IsActive = isActive;
        ModeText = isAuto ? "Авто" : "Ручной";
        SentCount = _scheduler.SentMessagesCount;

        var nextTime = _scheduler.NextBroadcastTime;
        NextBroadcastText = nextTime.HasValue ? nextTime.Value.ToString("HH:mm:ss") : "–";

        (StatusText, StatusSeverity) = ResolveStatus(isActive, isAuto, streamOnline);

        _startItem.IsVisible = !isActive;
        _startItem.IsEnabled = !isAuto && !string.IsNullOrEmpty(_channelProvider.Channel);
        _stopItem.IsVisible = isActive;
        _sendNowItem.IsEnabled = isActive;

        StartCommand.NotifyCanExecuteChanged();
        SendNowCommand.NotifyCanExecuteChanged();
    }

    private static (string text, StatusSeverity severity) ResolveStatus(bool isActive, bool isAuto, bool streamOnline)
    {
        if (isActive)
        {
            return ("Активна", StatusSeverity.Success);
        }

        if (isAuto && !streamOnline)
        {
            return ("Ожидание стрима", StatusSeverity.Warning);
        }

        return ("Неактивна", StatusSeverity.Error);
    }

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }

        _subscriptions.Clear();
    }
}
