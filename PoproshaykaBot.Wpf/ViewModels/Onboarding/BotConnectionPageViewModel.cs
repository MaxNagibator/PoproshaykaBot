using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.Bootstrap;
using System.Net.Http;

namespace PoproshaykaBot.Wpf.ViewModels.Onboarding;

public sealed partial class BotConnectionPageViewModel : OnboardingPageViewModelBase, IDisposable
{
    private const string SettingsNotWrittenDetails = "Настройки применены и работают до перезапуска. В файл они не записаны: туда только что перенесены данные предыдущей версии. Закройте приложение, запустите его заново и пройдите настройку ещё раз.";

    private const string AccountsNotWrittenDetails = "Вход в Twitch применён и работает до перезапуска. В файл он не записан: туда только что перенесены данные предыдущей версии. Закройте приложение, запустите его заново и пройдите настройку ещё раз.";

    private readonly IBotConnectionController _botConnectionManager;
    private readonly IEventBus _eventBus;
    private readonly SettingsManager _settingsManager;
    private readonly AccountsStore _accountsStore;
    private readonly ILogger<BotConnectionPageViewModel> _logger;
    private readonly List<IDisposable> _subs = [];

    private OnboardingContext? _context;
    private string _joinedChannel = string.Empty;
    private string _lastStatusMessage = string.Empty;

    [ObservableProperty]
    private BotLifecyclePhase _phase = BotLifecyclePhase.Idle;

    [ObservableProperty]
    private string _statusText = "Готовимся к подключению...";

    [ObservableProperty]
    private string _detailsText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    private bool _showRetry;

    public BotConnectionPageViewModel(
        IBotConnectionController botConnectionManager,
        IEventBus eventBus,
        SettingsManager settingsManager,
        AccountsStore accountsStore,
        ILogger<BotConnectionPageViewModel> logger)
    {
        _botConnectionManager = botConnectionManager;
        _eventBus = eventBus;
        _settingsManager = settingsManager;
        _accountsStore = accountsStore;
        _logger = logger;

        _subs.Add(eventBus.SubscribeOnUi<BotLifecyclePhaseChanged>(OnPhaseChanged));
        _subs.Add(eventBus.SubscribeOnUi<BotConnectionStatusUpdated>(OnStatusUpdated));
        _subs.Add(eventBus.SubscribeOnUi<BotJoinedChannel>(OnJoinedChannel));
    }

    public override string PageTitle => "Подключение бота";

    public override void OnEnter(OnboardingContext context)
    {
        _context = context;
        Phase = _botConnectionManager.CurrentPhase;
        CanAdvance = Phase == BotLifecyclePhase.Connected;

        try
        {
            if (!_settingsManager.SaveSettings(context.Settings))
            {
                _logger.BotConnectionSettingsNotWritten();
                ApplyPhase(BotLifecyclePhase.Failed, SettingsNotWrittenDetails);
                StatusText = "Настройки не записаны в файл";
                ShowRetry = false;
                return;
            }

            if (!_accountsStore.SaveAll(context.BotAccount, context.BroadcasterAccount))
            {
                _logger.BotConnectionAccountsNotWritten();
                ApplyPhase(BotLifecyclePhase.Failed, AccountsNotWrittenDetails);
                StatusText = "Вход в Twitch не записан в файл";
                ShowRetry = false;
                return;
            }
        }
        catch (Exception exception)
        {
            _logger.BotConnectionSaveSettingsFailed(exception);
            ApplyPhase(BotLifecyclePhase.Failed, exception.Message);
            ShowRetry = true;
            return;
        }

        _ = _eventBus.PublishAsync(new TwitchAuthorizationRefreshed(TwitchOAuthRole.Bot));
        _ = _eventBus.PublishAsync(new TwitchAuthorizationRefreshed(TwitchOAuthRole.Broadcaster));

        ApplyPhase(Phase);

        if (_botConnectionManager.IsBusy || Phase == BotLifecyclePhase.Connected)
        {
            return;
        }

        TryStartConnection();
    }

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private void Retry()
    {
        ShowRetry = false;
        TryStartConnection();
    }

    private bool CanRetry() => ShowRetry;

    private void OnPhaseChanged(BotLifecyclePhaseChanged @event)
    {
        Phase = @event.Phase;

        if (@event.Phase == BotLifecyclePhase.Failed && @event.Exception is not null)
        {
            _lastStatusMessage = SafeFailureMessage(@event.Exception);
        }

        ApplyPhase(@event.Phase);
    }

    private void OnStatusUpdated(BotConnectionStatusUpdated @event)
    {
        _lastStatusMessage = @event.Message;
        DetailsText = @event.Message;
    }

    private void OnJoinedChannel(BotJoinedChannel @event)
    {
        _joinedChannel = @event.Channel;
        ApplyPhase(Phase);
    }

    private void ApplyPhase(BotLifecyclePhase phase, string? overrideDetails = null)
    {
        Phase = phase;

        switch (phase)
        {
            case BotLifecyclePhase.Connecting:
                StatusText = "Подключение...";
                DetailsText = overrideDetails ?? _lastStatusMessage;
                ShowRetry = false;
                CanAdvance = false;
                break;

            case BotLifecyclePhase.Connected:
                var channel = string.IsNullOrWhiteSpace(_joinedChannel)
                    ? _context?.Settings.Twitch.Channel ?? string.Empty
                    : _joinedChannel;
                var suffix = string.IsNullOrWhiteSpace(channel) ? string.Empty : $" к чату @{channel}";
                StatusText = $"Бот подключён{suffix}";
                DetailsText = "Можно идти дальше.";
                ShowRetry = false;
                CanAdvance = true;
                break;

            case BotLifecyclePhase.Disconnecting:
                StatusText = "Отключение...";
                DetailsText = overrideDetails ?? _lastStatusMessage;
                ShowRetry = false;
                CanAdvance = false;
                break;

            case BotLifecyclePhase.Disconnected:
                StatusText = "Бот отключён";
                DetailsText = "Нажмите «Повторить подключение».";
                ShowRetry = true;
                CanAdvance = false;
                break;

            case BotLifecyclePhase.Cancelled:
                StatusText = "Подключение отменено";
                DetailsText = "Нажмите «Повторить подключение».";
                ShowRetry = true;
                CanAdvance = false;
                break;

            case BotLifecyclePhase.Failed:
                StatusText = "Не удалось подключиться";
                DetailsText = overrideDetails ?? _lastStatusMessage;
                ShowRetry = true;
                CanAdvance = false;
                break;

            default:
                StatusText = "Готовимся к подключению...";
                DetailsText = overrideDetails ?? _lastStatusMessage;
                ShowRetry = false;
                CanAdvance = false;
                break;
        }
    }

    private void TryStartConnection()
    {
        try
        {
            _botConnectionManager.StartConnection();
        }
        catch (InvalidOperationException exception)
        {
            _logger.BotConnectionStartRejected(exception);
        }
    }

    private static string SafeFailureMessage(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => "Подключение отменено",
            HttpRequestException => "Ошибка сети при подключении к Twitch",
            InvalidOperationException invalid => invalid.Message,
            _ => "Неизвестная ошибка подключения",
        };
    }

    public void Dispose()
    {
        foreach (var sub in _subs)
        {
            sub.Dispose();
        }
    }
}
