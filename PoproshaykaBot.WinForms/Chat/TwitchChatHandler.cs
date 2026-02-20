using PoproshaykaBot.WinForms.Models;
using PoproshaykaBot.WinForms.Settings;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace PoproshaykaBot.WinForms.Chat;

public sealed class TwitchChatHandler : IChannelProvider
{
    private readonly TwitchClient _client;
    private readonly SettingsManager _settingsManager;
    private readonly StatisticsCollector _statisticsCollector;
    private readonly AudienceTracker _audienceTracker;
    private readonly ChatHistoryManager _chatHistoryManager;
    private readonly ChatDecorationsProvider _chatDecorations;
    private readonly ChatCommandProcessor _commandProcessor;
    private readonly TwitchChatMessenger _messenger;

    public TwitchChatHandler(
        TwitchClient twitchClient,
        SettingsManager settingsManager,
        StatisticsCollector statisticsCollector,
        AudienceTracker audienceTracker,
        ChatHistoryManager chatHistoryManager,
        ChatDecorationsProvider chatDecorationsProvider,
        ChatCommandProcessor commandProcessor,
        TwitchChatMessenger messenger)
    {
        _client = twitchClient;
        _settingsManager = settingsManager;
        _statisticsCollector = statisticsCollector;
        _audienceTracker = audienceTracker;
        _chatHistoryManager = chatHistoryManager;
        _chatDecorations = chatDecorationsProvider;
        _commandProcessor = commandProcessor;
        _messenger = messenger;

        _client.OnLog += Client_OnLog;
        _client.OnConnected += Client_OnConnected;
        _client.OnJoinedChannel += Client_OnJoinedChannel;
        _client.OnMessageReceived += Client_OnMessageReceived;
    }

    public event Action<string>? LogMessage;

    public event Action<string>? Connected;

    public string? Channel { get; private set; }

    public void Reset()
    {
        Channel = null;
        _audienceTracker.ClearAll();
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

    private void Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        var settings = _settingsManager.Current.Twitch;

        if (settings.Messages.ConnectionEnabled
            && !string.IsNullOrWhiteSpace(settings.Messages.Connection))
        {
            _messenger.Send(e.Channel, settings.Messages.Connection);
        }

        Channel = e.Channel;

        var connectionMessage = $"Подключен к каналу {e.Channel}";
        Console.WriteLine(connectionMessage);
        Connected?.Invoke(connectionMessage);
        LogMessage?.Invoke(connectionMessage);
    }

    private void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var settings = _settingsManager.Current.Twitch;

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

            Emotes = _chatDecorations.ExtractEmotes(e.ChatMessage, settings.ObsChat.EmoteSizePixels),
            Badges = e.ChatMessage.Badges,
            BadgeUrls = _chatDecorations.ExtractBadgeUrls(e.ChatMessage.Badges, settings.ObsChat.BadgeSizePixels),
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

        if (settings.Messages.WelcomeEnabled && isFirstSeen)
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
}
