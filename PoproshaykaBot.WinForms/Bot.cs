using PoproshaykaBot.WinForms.Models;
using System.Globalization;
using System.Timers;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using Timer = System.Timers.Timer;

namespace PoproshaykaBot.WinForms;

public class Bot : IAsyncDisposable
{
    private readonly TwitchClient _client;
    private readonly TwitchSettings _settings;
    private readonly StatisticsCollector _statisticsCollector;
    private readonly Dictionary<string, UserInfo> _seenUsers;
    private bool _disposed;

    private string? _channel;
    private Timer? _timer;

    private int X1;

    public Bot(string accessToken, TwitchSettings settings, StatisticsCollector statisticsCollector)
    {
        _settings = settings;
        _statisticsCollector = statisticsCollector;
        _seenUsers = [];

        ConnectionCredentials credentials = new(_settings.BotUsername, accessToken);

        ClientOptions clientOptions = new()
        {
            MessagesAllowedInPeriod = _settings.MessagesAllowedInPeriod,
            ThrottlingPeriod = TimeSpan.FromSeconds(_settings.ThrottlingPeriodSeconds),
        };

        WebSocketClient customClient = new(clientOptions);
        _client = new(customClient);
        _client.Initialize(credentials, _settings.Channel);

        _client.OnLog += Client_OnLog;
        _client.OnMessageReceived += Client_OnMessageReceived;
        _client.OnConnected += Client_OnConnected;
        _client.OnJoinedChannel += Сlient_OnJoinedChannel;
    }

    public event Action<ChatMessageData>? ChatMessageReceived;

    public event Action<string>? Connected;

    public event Action<string>? ConnectionProgress;

    public event Action<string>? LogMessage;

    public bool IsBroadcastActive => _timer is { Enabled: true };

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_client.IsConnected)
        {
            ConnectionProgress?.Invoke("Бот уже подключен");
            return;
        }

        ConnectionProgress?.Invoke("Инициализация подключения...");

        try
        {
            ConnectionProgress?.Invoke("Подключение к серверу Twitch...");

            _client.Connect();

            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;

            while (_client.IsConnected == false && DateTime.UtcNow - startTime < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ConnectionProgress?.Invoke("Ожидание подтверждения подключения...");
                await Task.Delay(500, cancellationToken);
            }

            if (_client.IsConnected)
            {
                ConnectionProgress?.Invoke("Подключение установлено успешно");
            }
            else
            {
                throw new TimeoutException("Превышено время ожидания подключения к Twitch");
            }

            ConnectionProgress?.Invoke("Инициализация статистики...");
            await _statisticsCollector.StartAsync();
            _statisticsCollector.ResetBotStartTime();
        }
        catch (OperationCanceledException)
        {
            ConnectionProgress?.Invoke("Подключение отменено пользователем");
            throw;
        }
        catch (Exception exception)
        {
            ConnectionProgress?.Invoke($"Ошибка подключения: {exception.Message}");
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        _timer?.Dispose();

        if (_client.IsConnected)
        {
            if (string.IsNullOrWhiteSpace(_channel) == false)
            {
                var messages = new List<string>();

                if (_settings.Messages.FarewellEnabled
                    && string.IsNullOrWhiteSpace(_settings.Messages.Farewell) == false)
                {
                    var userNames = string.Join(", ", _seenUsers.Values.Select(u => u.DisplayName));
                    var farewellMessage = _settings.Messages.Farewell.Replace("{username}", userNames);
                    messages.Add(farewellMessage);
                }

                if (_settings.Messages.DisconnectionEnabled
                    && string.IsNullOrWhiteSpace(_settings.Messages.Disconnection) == false)
                {
                    messages.Add(_settings.Messages.Disconnection);
                }

                if (messages.Count > 0)
                {
                    var combinedMessage = string.Join(" ", messages);
                    _client.SendMessage(_channel, combinedMessage);
                }
            }

            _client.Disconnect();
        }

        await _statisticsCollector.StopAsync();
    }

    public void StartBroadcast()
    {
        if (string.IsNullOrWhiteSpace(_channel))
        {
            return;
        }

        if (_timer == null)
        {
            _timer = new();
            _timer.Interval = 900_000;
            _timer.Elapsed += _timer_Elapsed;
        }

        if (_timer.Enabled == false)
        {
            _timer.Start();
        }
    }

    public void StopBroadcast()
    {
        _timer?.Stop();
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
        await _statisticsCollector.DisposeAsync();
        _disposed = true;
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
    }

    private void Сlient_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        if (_settings.Messages.ConnectionEnabled
            && string.IsNullOrWhiteSpace(_settings.Messages.Connection) == false)
        {
            _client.SendMessage(e.Channel, _settings.Messages.Connection);
        }

        _channel = e.Channel;
        X1 = 0;
        StartBroadcast();

        var connectionMessage = $"Подключен к каналу {e.Channel}";
        Console.WriteLine(connectionMessage);
        Connected?.Invoke(connectionMessage);
    }

    private void _timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_channel))
        {
            return;
        }

        X1++;
        _client.SendMessage(_channel, "Присылайте деняк, пожалуйста, " + X1 + " раз прошу. https://bob217.ru/donate/");
    }

    private void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        _statisticsCollector.TrackMessage(e.ChatMessage.UserId, e.ChatMessage.Username);

        var userMessage = new ChatMessageData
        {
            Timestamp = DateTime.UtcNow,
            DisplayName = e.ChatMessage.DisplayName,
            Message = e.ChatMessage.Message,
            MessageType = ChatMessageType.UserMessage,
            Status = GetUserStatusFlags(e.ChatMessage),
        };

        ChatMessageReceived?.Invoke(userMessage);

        string? botResponse = null;

        var userInfo = new UserInfo(e.ChatMessage.UserId, e.ChatMessage.DisplayName);

        if (_settings.Messages.WelcomeEnabled && _seenUsers.TryAdd(e.ChatMessage.UserId, userInfo))
        {
            var welcomeMessage = _settings.Messages.Welcome.Replace("{username}", e.ChatMessage.DisplayName);
            _client.SendReply(e.ChatMessage.Channel, e.ChatMessage.Id, welcomeMessage);

            var welcomeResponse = new ChatMessageData
            {
                Timestamp = DateTime.UtcNow,
                DisplayName = _settings.BotUsername,
                Message = welcomeMessage,
                MessageType = ChatMessageType.BotResponse,
                Status = UserStatus.None,
            };

            ChatMessageReceived?.Invoke(welcomeResponse);
        }

        switch (e.ChatMessage.Message.ToLower())
        {
            case "!привет":
                // TODO: Обработать дублирование привета
                botResponse = $"Привет, {e.ChatMessage.Username}!";
                _client.SendMessage(e.ChatMessage.Channel, botResponse);
                break;

            case "!деньги":
                botResponse = "Принимаем криптой, СБП, куаркод справа снизу, подробнее можно узнать в телеге https://t.me/bobito217";
                _client.SendMessage(e.ChatMessage.Channel, botResponse);
                break;

            case "!сколькосообщений":
                {
                    var userStats = _statisticsCollector.GetUserStatistics(e.ChatMessage.UserId);
                    var messageCount = userStats?.MessageCount ?? 0;
                    botResponse = $"У тебя {FormatNumber(messageCount)} сообщений";
                    _client.SendReply(e.ChatMessage.Channel, e.ChatMessage.Id, botResponse);
                    break;
                }

            case "!статистикабота":
                {
                    var botStats = _statisticsCollector.GetBotStatistics();
                    var uptime = FormatTimeSpan(botStats.TotalUptime);
                    var totalMessages = FormatNumber(botStats.TotalMessagesProcessed);
                    var startTime = FormatDateTime(botStats.BotStartTime);

                    botResponse = $"📊 Статистика бота: Обработано {totalMessages} сообщений | Время работы: {uptime} | Запущен: {startTime}";

                    _client.SendMessage(e.ChatMessage.Channel, botResponse);
                    break;
                }

            case "!топпользователи":
                {
                    var topUsers = _statisticsCollector.GetTopUsers(5);

                    if (topUsers.Count == 0)
                    {
                        botResponse = "Пока нет данных о пользователях";
                        _client.SendMessage(e.ChatMessage.Channel, botResponse);
                        break;
                    }

                    var response = "🏆 Топ-5 активных пользователей: ";

                    for (var i = 0; i < topUsers.Count; i++)
                    {
                        var user = topUsers[i];
                        response += $"{i + 1}. {user.Name} ({FormatNumber(user.MessageCount)})";

                        if (i < topUsers.Count - 1)
                        {
                            response += " | ";
                        }
                    }

                    botResponse = response;
                    _client.SendMessage(e.ChatMessage.Channel, botResponse);
                    break;
                }

            case "!мойпрофиль":
                {
                    var userStats = _statisticsCollector.GetUserStatistics(e.ChatMessage.UserId);

                    if (userStats == null)
                    {
                        botResponse = "У тебя пока нет статистики";
                        _client.SendReply(e.ChatMessage.Channel, e.ChatMessage.Id, botResponse);
                        break;
                    }

                    var messageCount = FormatNumber(userStats.MessageCount);
                    var firstSeen = FormatDateTime(userStats.FirstSeen);
                    var lastSeen = FormatDateTime(userStats.LastSeen);

                    botResponse = $"👤 Твой профиль: {messageCount} сообщений | Впервые: {firstSeen} | Последний раз: {lastSeen}";
                    _client.SendReply(e.ChatMessage.Channel, e.ChatMessage.Id, botResponse);
                    break;
                }

            case "!пользователи":
            case "!чат":
                if (_seenUsers.Count == 0)
                {
                    botResponse = "В чате пока нет активных пользователей";
                    _client.SendMessage(e.ChatMessage.Channel, botResponse);
                    break;
                }

                var userNames = _seenUsers.Values.Select(x => x.DisplayName).ToList();
                var userCount = userNames.Count;

                if (userCount <= 10)
                {
                    var userList = string.Join(", ", userNames);
                    botResponse = $"👥 Активные пользователи чата ({userCount}): {userList}";
                }
                else
                {
                    var firstUsers = userNames.Take(8).ToList();
                    var userList = string.Join(", ", firstUsers);
                    botResponse = $"👥 Активные пользователи чата ({userCount}): {userList} и ещё {userCount - 8}";
                }

                _client.SendMessage(e.ChatMessage.Channel, botResponse);
                break;

            case "!пока":
                botResponse = _settings.Messages.Farewell.Replace("{username}", e.ChatMessage.DisplayName);
                _client.SendMessage(e.ChatMessage.Channel, botResponse);
                _seenUsers.Remove(e.ChatMessage.UserId);
                break;
        }

        if (string.IsNullOrEmpty(botResponse) == false)
        {
            var responseMessage = new ChatMessageData
            {
                Timestamp = DateTime.UtcNow,
                DisplayName = _settings.BotUsername,
                Message = botResponse,
                MessageType = ChatMessageType.BotResponse,
                Status = UserStatus.None,
            };

            ChatMessageReceived?.Invoke(responseMessage);
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

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
        {
            return $"{(int)timeSpan.TotalDays} дн. {timeSpan.Hours} ч. {timeSpan.Minutes} мин.";
        }

        if (timeSpan.TotalHours >= 1)
        {
            return $"{timeSpan.Hours} ч. {timeSpan.Minutes} мин.";
        }

        return $"{timeSpan.Minutes} мин. {timeSpan.Seconds} сек.";
    }

    private static string FormatNumber(ulong number)
    {
        return number.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));
    }

    private static string FormatDateTime(DateTime dateTime)
    {
        var moscowTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
        var moscowTime = TimeZoneInfo.ConvertTimeFromUtc(dateTime, moscowTimeZone);

        return moscowTime.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU")) + " по МСК";
    }


}

public record UserInfo(string UserId, string DisplayName);
