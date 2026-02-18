using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.WinForms.Broadcast;
using PoproshaykaBot.WinForms.Chat;
using PoproshaykaBot.WinForms.Chat.Commands;
using PoproshaykaBot.WinForms.Settings;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace PoproshaykaBot.WinForms.Services;

public sealed class BotFactory(IServiceProvider serviceProvider)
{
    public Bot Create(string accessToken)
    {
        var settingsManager = serviceProvider.GetRequiredService<SettingsManager>();
        var statistics = serviceProvider.GetRequiredService<StatisticsCollector>();
        var twitchApi = serviceProvider.GetRequiredService<TwitchAPI>();
        var chatDecorationsProvider = serviceProvider.GetRequiredService<ChatDecorationsProvider>();
        var chatHistoryManager = serviceProvider.GetRequiredService<ChatHistoryManager>();
        var streamStatusManager = serviceProvider.GetRequiredService<StreamStatusManager>();
        var userRankService = serviceProvider.GetRequiredService<UserRankService>();
        var messenger = serviceProvider.GetRequiredService<TwitchChatMessenger>();
        var broadcastScheduler = serviceProvider.GetRequiredService<BroadcastScheduler>();

        var settings = settingsManager.Current.Twitch;
        var credentials = new ConnectionCredentials(settings.BotUsername, accessToken);

        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = settings.MessagesAllowedInPeriod,
            ThrottlingPeriod = TimeSpan.FromSeconds(settings.ThrottlingPeriodSeconds),
            DisconnectWait = 0,
        };

        var wsClient = new WebSocketClient(clientOptions);
        var twitchClient = new TwitchClient(wsClient);
        twitchClient.Initialize(credentials, settings.Channel);

        twitchApi.Settings.ClientId = settings.ClientId;
        twitchApi.Settings.AccessToken = accessToken;

        var audienceTracker = new AudienceTracker(settingsManager);

        var commands = new List<IChatCommand>
        {
            new HelloCommand(),
            new DonateCommand(settingsManager),
            new HowManyMessagesCommand(statistics, userRankService),
            new BotStatsCommand(statistics),
            new TopUsersCommand(statistics, userRankService),
            new MyProfileCommand(statistics, userRankService),
            new ActiveUsersCommand(audienceTracker),
            new ByeCommand(audienceTracker),
            new StreamInfoCommand(streamStatusManager),
            new TrumpCommand(settingsManager),
            new RanksCommand(settingsManager),
            new RankCommand(statistics, userRankService),
        };

        var commandProcessor = new ChatCommandProcessor(commands);
        commandProcessor.Register(new HelpCommand(commandProcessor.GetAllCommands));

        return new(settingsManager,
            statistics,
            twitchApi,
            chatDecorationsProvider,
            audienceTracker,
            chatHistoryManager,
            broadcastScheduler,
            commandProcessor,
            streamStatusManager,
            twitchClient,
            messenger);
    }
}
