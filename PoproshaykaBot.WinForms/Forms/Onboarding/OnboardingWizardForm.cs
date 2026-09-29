using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.WinForms.Forms.Onboarding.Pages;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using System.Text.Json;

namespace PoproshaykaBot.WinForms.Forms.Onboarding;

public partial class OnboardingWizardForm : Form
{
    private readonly IControlFactory _controlFactory;
    private readonly SettingsManager _settingsManager;
    private readonly AccountsStore _accountsStore;
    private readonly KestrelHttpServer _kestrelHttpServer;
    private readonly BotConnectionManager _botConnectionManager;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OnboardingWizardForm> _logger;
    private readonly OnboardingContext _context;
    private readonly List<IOnboardingWizardPage> _pages = [];
    private readonly int _originalHttpServerPort;
    private readonly string _originalChannel;
    private readonly TwitchAccountSettings _originalBotAccount;
    private readonly TwitchAccountSettings _originalBroadcasterAccount;
    private int _currentPageIndex = -1;
    private bool _initialized;
    private bool _rollbackPerformed;

    public OnboardingWizardForm(
        SettingsManager settingsManager,
        AccountsStore accountsStore,
        IControlFactory controlFactory,
        KestrelHttpServer kestrelHttpServer,
        BotConnectionManager botConnectionManager,
        IEventBus eventBus,
        ILogger<OnboardingWizardForm> logger)
    {
        _controlFactory = controlFactory;
        _settingsManager = settingsManager;
        _accountsStore = accountsStore;
        _kestrelHttpServer = kestrelHttpServer;
        _botConnectionManager = botConnectionManager;
        _eventBus = eventBus;
        _logger = logger;

        _context = new(DeepClone(settingsManager.Current),
            DeepClone(accountsStore.LoadBot()),
            DeepClone(accountsStore.LoadBroadcaster()));

        _originalHttpServerPort = settingsManager.Current.Twitch.HttpServerPort;
        _originalChannel = settingsManager.Current.Twitch.Channel;
        _originalBotAccount = DeepClone(accountsStore.LoadBot());
        _originalBroadcasterAccount = DeepClone(accountsStore.LoadBroadcaster());

        InitializeComponent();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_initialized || this.IsInDesignMode())
        {
            return;
        }

        _initialized = true;

        BuildPages();
        NavigateTo(0);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        if (e.Cancel || _rollbackPerformed || DialogResult == DialogResult.OK)
        {
            return;
        }

        var portChanged = _settingsManager.Current.Twitch.HttpServerPort != _originalHttpServerPort;
        var channelChanged = !string.Equals(_settingsManager.Current.Twitch.Channel, _originalChannel, StringComparison.Ordinal);
        var accountsChanged = AccountsDifferFromOriginal();
        var botRunning = _botConnectionManager.CurrentPhase
            is BotLifecyclePhase.Connecting
            or BotLifecyclePhase.Connected
            or BotLifecyclePhase.Disconnecting;

        if (!portChanged && !channelChanged && !accountsChanged && !botRunning)
        {
            _rollbackPerformed = true;
            return;
        }

        e.Cancel = true;
        _ = RollbackAndCloseAsync();
    }

    private async void OnNextButtonClicked(object? sender, EventArgs e)
    {
        if (_currentPageIndex < 0 || _currentPageIndex >= _pages.Count)
        {
            return;
        }

        var current = _pages[_currentPageIndex];

        SetButtonsEnabled(false);
        try
        {
            var canLeave = await current.OnLeavingAsync(_context);
            if (!canLeave)
            {
                return;
            }
        }
        finally
        {
            SetButtonsEnabled(true);
        }

        if (_currentPageIndex >= _pages.Count - 1)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        NavigateTo(_currentPageIndex + 1);
    }

    private void OnBackButtonClicked(object? sender, EventArgs e)
    {
        if (_currentPageIndex <= 0)
        {
            return;
        }

        NavigateTo(_currentPageIndex - 1);
    }

    private void OnCancelButtonClicked(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void OnCurrentPageCanAdvanceChanged(object? sender, EventArgs e)
    {
        UpdateButtonStates();
    }

    private static bool AccountsEqual(TwitchAccountSettings left, TwitchAccountSettings right)
    {
        return string.Equals(left.AccessToken, right.AccessToken, StringComparison.Ordinal)
               && string.Equals(left.RefreshToken, right.RefreshToken, StringComparison.Ordinal)
               && string.Equals(left.Login, right.Login, StringComparison.Ordinal)
               && string.Equals(left.UserId, right.UserId, StringComparison.Ordinal);
    }

    private static T DeepClone<T>(T source) where T : class, new()
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<T>(json) ?? new();
    }

    private bool AccountsDifferFromOriginal()
    {
        var liveBot = _accountsStore.LoadBot();
        var liveBroadcaster = _accountsStore.LoadBroadcaster();
        return !AccountsEqual(liveBot, _originalBotAccount)
               || !AccountsEqual(liveBroadcaster, _originalBroadcasterAccount);
    }

    private async Task RollbackAndCloseAsync()
    {
        await StopBotIfRunningAsync();
        RollbackChannel();
        await RollbackHttpServerPortAsync();
        await RollbackAccountsAsync();

        if (!IsDisposed)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private async Task StopBotIfRunningAsync()
    {
        var phase = _botConnectionManager.CurrentPhase;
        if (phase is BotLifecyclePhase.Idle or BotLifecyclePhase.Disconnected)
        {
            return;
        }

        try
        {
            await _botConnectionManager.ShutdownAsync();
            _logger.LogInformation("Бот остановлен после отмены мастера первичной настройки");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось остановить бота при откате мастера");
        }
    }

    private async Task RollbackAccountsAsync()
    {
        if (!AccountsDifferFromOriginal())
        {
            return;
        }

        try
        {
            _accountsStore.SaveAll(_originalBotAccount, _originalBroadcasterAccount);
            await _eventBus.PublishAsync(new TwitchAuthorizationRefreshed(TwitchOAuthRole.Bot));
            await _eventBus.PublishAsync(new TwitchAuthorizationRefreshed(TwitchOAuthRole.Broadcaster));
            _logger.LogInformation("Аккаунты Twitch откачены на исходные значения после отмены мастера");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось восстановить аккаунты Twitch после отмены мастера");
        }
    }

    private void RollbackChannel()
    {
        var liveChannel = _settingsManager.Current.Twitch.Channel;
        if (string.Equals(liveChannel, _originalChannel, StringComparison.Ordinal))
        {
            return;
        }

        _settingsManager.UpdateCurrent(settings => settings.Twitch.Channel = _originalChannel);
        _logger.LogInformation("Канал откачен на исходное значение «{Channel}» после отмены мастера", _originalChannel);
    }

    private async Task RollbackHttpServerPortAsync()
    {
        if (_rollbackPerformed)
        {
            return;
        }

        _rollbackPerformed = true;

        var currentPort = _settingsManager.Current.Twitch.HttpServerPort;

        if (currentPort == _originalHttpServerPort)
        {
            return;
        }

        try
        {
            if (_kestrelHttpServer.IsRunning)
            {
                await _kestrelHttpServer.StopAsync();
            }

            _settingsManager.UpdateCurrent(settings => settings.Twitch.HttpServerPort = _originalHttpServerPort);

            await _kestrelHttpServer.StartAsync();

            _logger.LogInformation("HTTP сервер откачен на исходный порт {Port} после отмены мастера", _originalHttpServerPort);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось откатить HTTP сервер на порт {Port} после отмены мастера", _originalHttpServerPort);
        }
    }

    private void BuildPages()
    {
        _pages.Add(_controlFactory.Create<WelcomePage>());
        _pages.Add(_controlFactory.Create<CredentialsPage>());
        _pages.Add(_controlFactory.Create<HttpServerCheckPage>());

        var botAuth = _controlFactory.Create<AuthorizationPage>();
        botAuth.Role = TwitchOAuthRole.Bot;
        _pages.Add(botAuth);

        var broadcasterAuth = _controlFactory.Create<AuthorizationPage>();
        broadcasterAuth.Role = TwitchOAuthRole.Broadcaster;
        _pages.Add(broadcasterAuth);

        _pages.Add(_controlFactory.Create<BotConnectionPage>());
        _pages.Add(_controlFactory.Create<HealthCheckPage>());
        _pages.Add(_controlFactory.Create<CompletionPage>());
    }

    private void NavigateTo(int index)
    {
        if (index < 0 || index >= _pages.Count)
        {
            return;
        }

        if (_currentPageIndex >= 0 && _currentPageIndex < _pages.Count)
        {
            var prev = _pages[_currentPageIndex];
            prev.CanAdvanceChanged -= OnCurrentPageCanAdvanceChanged;
        }

        _pageContainer.Controls.Clear();

        var page = _pages[index];

        if (page is Control control)
        {
            control.Dock = DockStyle.Fill;
            _pageContainer.Controls.Add(control);
        }
        else
        {
            throw new InvalidOperationException("Onboarding wizard page must be a Control");
        }

        page.OnEnter(_context);
        page.CanAdvanceChanged += OnCurrentPageCanAdvanceChanged;

        _currentPageIndex = index;
        _headerLabel.Text = page.PageTitle;
        _stepLabel.Text = $"Шаг {index + 1} из {_pages.Count}";

        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var isFirst = _currentPageIndex == 0;
        var isLast = _currentPageIndex == _pages.Count - 1;
        var page = _pages[_currentPageIndex];

        _backButton.Enabled = !isFirst;
        _nextButton.Enabled = page.CanAdvance;
        _nextButton.Text = isLast ? "Готово" : "Далее →";
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _backButton.Enabled = enabled && _currentPageIndex > 0;
        _nextButton.Enabled = enabled && _pages[_currentPageIndex].CanAdvance;
        _cancelButton.Enabled = enabled;
    }
}
