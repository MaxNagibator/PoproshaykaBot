using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Chat;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Twitch.Chat;
using PoproshaykaBot.Core.Users;

namespace PoproshaykaBot.Core.Chat;

public sealed class TwitchChatHandler :
    IChannelProvider,
    IEventHandler<RawChatMessageReceived>,
    IEventHandler<BotJoinedChannel>,
    IEventSubscriber,
    IDisposable
{
    private const string BotFallbackDisplayName = "Бот";
    private const string LocalMessageIdPrefix = "local-";

    private readonly SettingsManager _settingsManager;
    private readonly ObsChatStore _obsChatStore;
    private readonly CommandSettingsStore _commandSettingsStore;
    private readonly AccountsStore _accountsStore;
    private readonly AudienceTracker _audienceTracker;
    private readonly ChatDecorationsProvider _chatDecorations;
    private readonly ChatCommandProcessor _commandProcessor;
    private readonly CommandResponseTracker _commandResponseTracker;
    private readonly TwitchChatMessenger _messenger;
    private readonly IEventBus _eventBus;
    private readonly ILogger<TwitchChatHandler> _logger;
    private readonly List<IDisposable> _subscriptions;
    private int _whisperScopeMissingReported;

    public TwitchChatHandler(
        SettingsManager settingsManager,
        ObsChatStore obsChatStore,
        CommandSettingsStore commandSettingsStore,
        AccountsStore accountsStore,
        AudienceTracker audienceTracker,
        ChatDecorationsProvider chatDecorationsProvider,
        ChatCommandProcessor commandProcessor,
        CommandResponseTracker commandResponseTracker,
        TwitchChatMessenger messenger,
        IEventBus eventBus,
        ILogger<TwitchChatHandler> logger)
    {
        _settingsManager = settingsManager;
        _obsChatStore = obsChatStore;
        _commandSettingsStore = commandSettingsStore;
        _accountsStore = accountsStore;
        _audienceTracker = audienceTracker;
        _chatDecorations = chatDecorationsProvider;
        _commandProcessor = commandProcessor;
        _commandResponseTracker = commandResponseTracker;
        _messenger = messenger;
        _eventBus = eventBus;
        _logger = logger;

        _subscriptions =
        [
            eventBus.Subscribe<RawChatMessageReceived>(this),
            eventBus.Subscribe<BotJoinedChannel>(this),
        ];
    }

    public string? Channel { get; private set; }

    public void Reset()
    {
        Channel = null;
        _audienceTracker.ClearAll();
        _commandResponseTracker.Clear();
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
    }

    public Task HandleAsync(BotJoinedChannel @event, CancellationToken cancellationToken)
    {
        Channel = @event.Channel;
        return Task.CompletedTask;
    }

    public async Task HandleAsync(RawChatMessageReceived @event, CancellationToken cancellationToken)
    {
        var chatMessage = @event.Message;

        Channel ??= chatMessage.Channel;

        var settings = _settingsManager.Current.Twitch;

        var isFirstSeen = !chatMessage.IsBot
                          && _audienceTracker.OnUserMessage(chatMessage.UserId, chatMessage.DisplayName);

        var status = GetUserStatusFlags(chatMessage);

        var badgesKvp = chatMessage.Badges
            .Select(b => new KeyValuePair<string, string>(b.SetId, b.BadgeId))
            .ToList();

        var historyEntry = new ChatMessageData
        {
            MessageId = chatMessage.Id,
            Timestamp = @event.Timestamp.UtcDateTime,
            UserId = chatMessage.UserId,
            DisplayName = chatMessage.DisplayName,
            Message = chatMessage.Message,
            MessageType = chatMessage.IsBot ? ChatMessageType.BotResponse : ChatMessageType.UserMessage,
            Status = status,
            IsFirstTime = isFirstSeen,
            Color = chatMessage.Color,

            Emotes = _chatDecorations.ExtractEmotes(chatMessage, _obsChatStore.Load().EmoteSizePixels),
            Badges = badgesKvp,
            BadgeUrls = _chatDecorations.ExtractBadgeUrls(chatMessage.Badges, _obsChatStore.Load().BadgeSizePixels),
        };

        var commandResponse = chatMessage.IsBot ? _commandResponseTracker.TryConsume(chatMessage.Id) : null;

        _ = _eventBus.PublishAsync(new ChatMessageReceived(chatMessage.Channel,
            chatMessage.Id,
            chatMessage.UserId,
            chatMessage.Username,
            chatMessage.DisplayName,
            chatMessage.Message,
            status,
            isFirstSeen,
            historyEntry,
            chatMessage.IsBot,
            commandResponse), cancellationToken);

        if (chatMessage.IsBot)
        {
            _logger.LogDebug("[Бот, эхо] {DisplayName}: {Message}", chatMessage.DisplayName, chatMessage.Message);
            return;
        }

        var context = new CommandContext
        {
            Channel = chatMessage.Channel,
            MessageId = chatMessage.Id,
            UserId = chatMessage.UserId,
            Username = chatMessage.Username,
            DisplayName = chatMessage.DisplayName,
            IsBroadcaster = chatMessage.IsBroadcaster,
            IsModerator = chatMessage.IsModerator,
        };

        var commandResult = await _commandProcessor.TryProcessAsync(chatMessage.Message, context, cancellationToken);
        var response = commandResult.Response;
        var canonical = commandResult.Canonical ?? string.Empty;

        var target = response != null
            ? _commandSettingsStore.Load().ResolveResponseTarget(canonical)
            : CommandResponseTarget.None;

        if (settings.Messages.WelcomeEnabled && isFirstSeen)
        {
            var welcomeMessage = _audienceTracker.CreateWelcome(chatMessage.DisplayName);

            if (!string.IsNullOrWhiteSpace(welcomeMessage))
            {
                if (response != null && target.GoesToChat())
                {
                    response = response with { Text = $"{welcomeMessage} {response.Text}" };
                }
                else
                {
                    _messenger.Reply(chatMessage.Id, welcomeMessage);
                }
            }
        }

        if (response != null)
        {
            await DeliverCommandResponseAsync(response,
                canonical,
                target,
                chatMessage,
                context,
                cancellationToken);
        }

        _logger.LogDebug("[Бот] сообщение от {DisplayName} обработано ({Length} симв.)", chatMessage.DisplayName, chatMessage.Message.Length);
    }

    private async Task DeliverCommandResponseAsync(
        OutgoingMessage response,
        string canonical,
        CommandResponseTarget target,
        ChatMessage source,
        CommandContext context,
        CancellationToken cancellationToken)
    {
        if (target == CommandResponseTarget.None)
        {
            _logger.LogDebug("Ответ команды {Canonical} не доставлен: цель ответа – молча", canonical);
            return;
        }

        var mark = new CommandResponseMark(canonical, target);

        if (target.WhispersToCaller())
        {
            if (TwitchScopes.IsGranted(_accountsStore.LoadBot(), TwitchScopes.UserManageWhispers))
            {
                Volatile.Write(ref _whisperScopeMissingReported, 0);
                _messenger.Whisper(context.UserId, context.MessageId, response.Text, mark);
            }
            else
            {
                if (Interlocked.Exchange(ref _whisperScopeMissingReported, 1) == 0)
                {
                    _logger.LogWarning("Ответ команды {Canonical} не отправлен шёпотом: у бота нет права {Scope} (необязательное, включается повторной авторизацией бота) – ответ уходит реплаем на сообщение вызвавшего, повторы этой строки пишутся в Debug",
                        canonical,
                        TwitchScopes.UserManageWhispers);
                }
                else
                {
                    _logger.LogDebug("Ответ команды {Canonical} уходит реплаем вместо шёпота: у бота нет права {Scope}",
                        canonical,
                        TwitchScopes.UserManageWhispers);
                }

                _messenger.Reply(context.MessageId, response.Text, mark);
            }

            return;
        }

        if (target.GoesToChat())
        {
            var replyToMessageId = target.RepliesToCaller()
                ? context.MessageId
                : response.Delivery == DeliveryType.Reply
                    ? response.ReplyToMessageId ?? context.MessageId
                    : null;

            if (string.IsNullOrEmpty(replyToMessageId))
            {
                _messenger.Send(response.Text, mark);
            }
            else
            {
                _messenger.Reply(replyToMessageId, response.Text, mark);
            }

            return;
        }

        await PublishOverlayOnlyResponseAsync(response, source, mark, cancellationToken);
    }

    private Task PublishOverlayOnlyResponseAsync(
        OutgoingMessage response,
        ChatMessage source,
        CommandResponseMark mark,
        CancellationToken cancellationToken)
    {
        var botAccount = _accountsStore.LoadBot();
        var displayName = string.IsNullOrWhiteSpace(botAccount.Login) ? BotFallbackDisplayName : botAccount.Login;
        var messageId = $"{LocalMessageIdPrefix}{Guid.NewGuid():N}";

        var historyEntry = new ChatMessageData
        {
            MessageId = messageId,
            Timestamp = DateTime.UtcNow,
            UserId = botAccount.UserId,
            DisplayName = displayName,
            Message = response.Text,
            MessageType = ChatMessageType.BotResponse,
            Status = UserStatus.None,
        };

        return _eventBus.PublishAsync(new ChatMessageReceived(source.Channel,
            messageId,
            botAccount.UserId,
            displayName,
            displayName,
            response.Text,
            UserStatus.None,
            false,
            historyEntry,
            true,
            mark), cancellationToken);
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
