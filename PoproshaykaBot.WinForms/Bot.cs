using PoproshaykaBot.WinForms.Broadcast;
using PoproshaykaBot.WinForms.Chat;
using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;

namespace PoproshaykaBot.WinForms;

public class Bot : IAsyncDisposable
{
    private readonly TwitchClient _client;
    private readonly ChatHistoryManager _chatHistoryManager;
    private readonly TwitchChatMessenger _messenger;
    private readonly TwitchSettings _settings;
    private readonly StatisticsCollector _statisticsCollector;
    private readonly AudienceTracker _audienceTracker;
    private readonly ChatDecorationsProvider _chatDecorations;
    private readonly ChatCommandProcessor _commandProcessor;
    private readonly StreamStatusManager _streamStatusManager;
    private readonly BroadcastScheduler _broadcastScheduler;

    private bool _disposed;
    private bool _streamHandlersAttached;

    public Bot(
        SettingsManager settingsManager,
        StatisticsCollector statisticsCollector,
        ChatDecorationsProvider chatDecorationsProvider,
        AudienceTracker audienceTracker,
        ChatHistoryManager chatHistoryManager,
        BroadcastScheduler broadcastScheduler,
        ChatCommandProcessor commandProcessor,
        StreamStatusManager streamStatusManager,
        TwitchClient client,
        TwitchChatMessenger messenger)
    {
        _settings = settingsManager.Current.Twitch;
        _statisticsCollector = statisticsCollector;
        _audienceTracker = audienceTracker;
        _chatHistoryManager = chatHistoryManager;
        _chatDecorations = chatDecorationsProvider;
        _broadcastScheduler = broadcastScheduler;
        _commandProcessor = commandProcessor;
        _streamStatusManager = streamStatusManager;

        _client = client;
        _messenger = messenger;

        _client.OnLog += Client_OnLog;
        _client.OnMessageReceived += Client_OnMessageReceived;
        _client.OnConnected += Client_OnConnected;
        _client.OnJoinedChannel += Сlient_OnJoinedChannel;

        AttachStreamStatusHandlers();
    }

    public event Action<string>? Connected;

    public event Action<string>? ConnectionProgress;

    public event Action<string>? LogMessage;

    public string? Channel { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_client.IsConnected)
        {
            var message = "Бот уже подключен";
            ConnectionProgress?.Invoke(message);
            LogMessage?.Invoke(message);
            return;
        }

        var initMessage = "Инициализация подключения...";
        ConnectionProgress?.Invoke(initMessage);
        LogMessage?.Invoke(initMessage);

        try
        {
            var connectingMessage = "Подключение к серверу Twitch...";
            ConnectionProgress?.Invoke(connectingMessage);
            LogMessage?.Invoke(connectingMessage);

            _client.Connect();

            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;

            while (!_client.IsConnected && DateTime.UtcNow - startTime < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var waitingMessage = "Ожидание подтверждения подключения...";
                ConnectionProgress?.Invoke(waitingMessage);
                LogMessage?.Invoke(waitingMessage);
                await Task.Delay(500, cancellationToken);
            }

            if (_client.IsConnected)
            {
                var successMessage = "Подключение установлено успешно";
                ConnectionProgress?.Invoke(successMessage);
                LogMessage?.Invoke(successMessage);
            }
            else
            {
                throw new TimeoutException("Превышено время ожидания подключения к Twitch");
            }

            var statsMessage = "Инициализация статистики...";
            ConnectionProgress?.Invoke(statsMessage);
            LogMessage?.Invoke(statsMessage);
            await _statisticsCollector.StartAsync();
            _statisticsCollector.ResetBotStartTime();

            var emotesMessage = "Загрузка эмодзи и бэйджей...";
            ConnectionProgress?.Invoke(emotesMessage);
            LogMessage?.Invoke(emotesMessage);
            await _chatDecorations.LoadAsync();
            LogMessage?.Invoke($"Загружено {_chatDecorations.GlobalEmotesCount} глобальных эмодзи и {_chatDecorations.GlobalBadgeSetsCount} типов глобальных бэйджей");

            var streamMessage = "Инициализация мониторинга стрима...";
            ConnectionProgress?.Invoke(streamMessage);
            LogMessage?.Invoke(streamMessage);
            await InitializeStreamMonitoringAsync();
        }
        catch (OperationCanceledException)
        {
            var cancelMessage = "Подключение отменено пользователем";
            ConnectionProgress?.Invoke(cancelMessage);
            LogMessage?.Invoke(cancelMessage);
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Ошибка подключения: {exception.Message}";
            ConnectionProgress?.Invoke(errorMessage);
            LogMessage?.Invoke(errorMessage);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_client.IsConnected)
        {
            if (!string.IsNullOrWhiteSpace(Channel))
            {
                var messages = new List<string>();

                var collectiveFarewell = _audienceTracker.CreateCollectiveFarewell();

                if (!string.IsNullOrWhiteSpace(collectiveFarewell))
                {
                    messages.Add(collectiveFarewell);
                }

                if (_settings.Messages.DisconnectionEnabled
                    && !string.IsNullOrWhiteSpace(_settings.Messages.Disconnection))
                {
                    messages.Add(_settings.Messages.Disconnection);
                }

                if (messages.Count > 0)
                {
                    var combinedMessage = string.Join(" ", messages);
                    _messenger.Send(Channel, combinedMessage);
                }

                _audienceTracker.ClearAll();
            }
        }

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            var platformDisconnectTask = _client.IsConnected
                ? Task.Run(() => _client.Disconnect(), cancellationTokenSource.Token)
                : Task.CompletedTask;

            var eventSubDisconnectTask = _streamStatusManager.StopMonitoringAsync(cancellationTokenSource.Token);

            await Task.WhenAll(platformDisconnectTask, eventSubDisconnectTask)
                .WaitAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            LogMessage?.Invoke("Превышено время ожидания отключения. Принудительное завершение.");
        }
        catch (Exception exception)
        {
            LogMessage?.Invoke($"Ошибка при отключении: {exception.Message}");
        }

        try
        {
            await Task.Run(() => _statisticsCollector.StopAsync(), cancellationTokenSource.Token);
        }
        catch (Exception exception)
        {
            LogMessage?.Invoke($"Ошибка сохранения статистики: {exception.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
        {
            return;
        }

        await DisconnectAsync();
        DetachStreamStatusHandlers();

        _client.OnLog -= Client_OnLog;
        _client.OnMessageReceived -= Client_OnMessageReceived;
        _client.OnConnected -= Client_OnConnected;
        _client.OnJoinedChannel -= Сlient_OnJoinedChannel;

        _disposed = true;
    }

    private void OnStreamStarted(StreamOnlineArgs args)
    {
    }

    private void OnStreamStopped(StreamOfflineArgs args)
    {
    }

    private void UpdateStreamState(StreamStatus status)
    {
        if (status == StreamStatus.Online)
        {
            if (_settings.AutoBroadcast.AutoBroadcastEnabled && !_broadcastScheduler.IsActive)
            {
                if (!string.IsNullOrWhiteSpace(Channel))
                {
                    _broadcastScheduler.Start(Channel);
                    LogMessage?.Invoke("🔴 Стрим онлайн. Автоматически запускаю рассылку.");

                    if (_settings.AutoBroadcast.StreamStatusNotificationsEnabled
                        && !string.IsNullOrEmpty(_settings.AutoBroadcast.StreamStartMessage))
                    {
                        _messenger.Send(Channel, _settings.AutoBroadcast.StreamStartMessage);
                    }
                }
            }
        }
        else if (status == StreamStatus.Offline)
        {
            if (_settings.AutoBroadcast.AutoBroadcastEnabled && _broadcastScheduler.IsActive)
            {
                _broadcastScheduler.Stop();
                LogMessage?.Invoke("⚫ Стрим офлайн. Автоматически останавливаю рассылку.");

                if (_settings.AutoBroadcast.StreamStatusNotificationsEnabled
                    && !string.IsNullOrEmpty(_settings.AutoBroadcast.StreamStopMessage)
                    && !string.IsNullOrEmpty(Channel))
                {
                    _messenger.Send(Channel, _settings.AutoBroadcast.StreamStopMessage);
                }
            }
        }
    }

    private void OnMonitoringLogMessage(string message)
    {
        LogMessage?.Invoke($"[Monitoring] {message}");
    }

    private void OnStreamErrorOccurred(string error)
    {
        LogMessage?.Invoke($"Ошибка EventSub: {error}");
    }

    private void Client_OnLog(object? sender, OnLogArgs e)
    {
        var logMessage = $"{e.DateTime}: {e.BotUsername} - {e.Data}";
        Console.WriteLine(logMessage);
        LogMessage?.Invoke(logMessage);
    }

    private void Client_OnConnected(object? sender, OnConnectedArgs e)
    {
        var connectionMessage = "Подключен!";
        Console.WriteLine(connectionMessage);
        Connected?.Invoke(connectionMessage);
        LogMessage?.Invoke(connectionMessage);
    }

    private void Сlient_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        if (_settings.Messages.ConnectionEnabled
            && !string.IsNullOrWhiteSpace(_settings.Messages.Connection))
        {
            _messenger.Send(e.Channel, _settings.Messages.Connection);
        }

        Channel = e.Channel;

        var connectionMessage = $"Подключен к каналу {e.Channel}";
        Console.WriteLine(connectionMessage);
        Connected?.Invoke(connectionMessage);
        LogMessage?.Invoke(connectionMessage);
    }

    private void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        _statisticsCollector.TrackMessage(e.ChatMessage.UserId, e.ChatMessage.Username);

        var isFirstSeen = _audienceTracker.OnUserMessage(e.ChatMessage.UserId, e.ChatMessage.DisplayName);

        var userMessage = new ChatMessageData
        {
            Timestamp = DateTime.UtcNow,
            DisplayName = e.ChatMessage.DisplayName,
            Message = e.ChatMessage.Message,
            MessageType = ChatMessageType.UserMessage,
            Status = GetUserStatusFlags(e.ChatMessage),
            IsFirstTime = isFirstSeen,

            Emotes = _chatDecorations.ExtractEmotes(e.ChatMessage, _settings.ObsChat.EmoteSizePixels),
            Badges = e.ChatMessage.Badges,
            BadgeUrls = _chatDecorations.ExtractBadgeUrls(e.ChatMessage.Badges, _settings.ObsChat.BadgeSizePixels),
        };

        _chatHistoryManager.AddMessage(userMessage);

        var context = new CommandContext
        {
            Channel = e.ChatMessage.Channel,
            MessageId = e.ChatMessage.Id,
            UserId = e.ChatMessage.UserId,
            Username = e.ChatMessage.Username,
            DisplayName = e.ChatMessage.DisplayName,
        };

        var isCommand = _commandProcessor.TryProcess(e.ChatMessage.Message, context, out var response);

        if (_settings.Messages.WelcomeEnabled && isFirstSeen)
        {
            var welcomeMessage = _audienceTracker.CreateWelcome(e.ChatMessage.DisplayName);

            if (!string.IsNullOrWhiteSpace(welcomeMessage))
            {
                if (isCommand && response != null)
                {
                    response = response with { Text = $"{welcomeMessage} {response.Text}" };
                }
                else if (!isCommand)
                {
                    _messenger.Reply(e.ChatMessage.Channel, e.ChatMessage.Id, welcomeMessage);
                }
            }
        }

        if (response != null)
        {
            switch (response.Delivery)
            {
                case DeliveryType.Reply:
                    _messenger.Reply(context.Channel, response.ReplyToMessageId ?? context.MessageId, response.Text);
                    break;

                case DeliveryType.Normal:
                default:
                    _messenger.Send(context.Channel, response.Text);
                    break;
            }
        }

        LogMessage?.Invoke(e.ChatMessage.DisplayName + ": " + e.ChatMessage.Message);
    }

    private static UserStatus GetUserStatusFlags(ChatMessage chatMessage)
    {
        var status = UserStatus.None;

        if (chatMessage.IsBroadcaster)
        {
            status |= UserStatus.Broadcaster;
        }

        if (chatMessage.IsModerator)
        {
            status |= UserStatus.Moderator;
        }

        if (chatMessage.IsVip)
        {
            status |= UserStatus.Vip;
        }

        if (chatMessage.IsSubscriber)
        {
            status |= UserStatus.Subscriber;
        }

        return status;
    }

    private void AttachStreamStatusHandlers()
    {
        if (_streamHandlersAttached)
        {
            return;
        }

        _streamStatusManager.StreamStarted += OnStreamStarted;
        _streamStatusManager.StreamStopped += OnStreamStopped;
        _streamStatusManager.StreamStatusChanged += UpdateStreamState;
        _streamStatusManager.MonitoringLogMessage += OnMonitoringLogMessage;
        _streamStatusManager.ErrorOccurred += OnStreamErrorOccurred;
        _streamHandlersAttached = true;
    }

    private void DetachStreamStatusHandlers()
    {
        if (!_streamHandlersAttached)
        {
            return;
        }

        _streamStatusManager.StreamStarted -= OnStreamStarted;
        _streamStatusManager.StreamStopped -= OnStreamStopped;
        _streamStatusManager.StreamStatusChanged -= UpdateStreamState;
        _streamStatusManager.MonitoringLogMessage -= OnMonitoringLogMessage;
        _streamStatusManager.ErrorOccurred -= OnStreamErrorOccurred;
        _streamHandlersAttached = false;
    }

    private async Task InitializeStreamMonitoringAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.ClientId))
            {
                LogMessage?.Invoke("Client ID не установлен. Мониторинг стрима недоступен.");
                return;
            }

            if (string.IsNullOrEmpty(_settings.AccessToken))
            {
                LogMessage?.Invoke("Access Token не установлен. Мониторинг стрима недоступен.");
                return;
            }

            await _streamStatusManager.InitializeAsync();
            await _streamStatusManager.StartMonitoringAsync(_settings.Channel);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка инициализации мониторинга стрима: {ex.Message}");
        }
    }
}
