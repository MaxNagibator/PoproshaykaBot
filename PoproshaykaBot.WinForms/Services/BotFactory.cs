using PoproshaykaBot.WinForms.Broadcast;
using PoproshaykaBot.WinForms.Chat;
using PoproshaykaBot.WinForms.Chat.Commands;
using PoproshaykaBot.WinForms.Settings;
using TwitchLib.Client;
using TwitchLib.Client.Models;

namespace PoproshaykaBot.WinForms.Services;

public sealed class BotFactory(
    SettingsManager settingsManager,
    StatisticsCollector statisticsCollector,
    ChatDecorationsProvider chatDecorationsProvider,
    ChatHistoryManager chatHistoryManager,
    StreamStatusManager streamStatusManager,
    UserRankService userRankService,
    TwitchChatMessenger messenger,
    BroadcastScheduler broadcastScheduler,
    TwitchClient twitchClient)
{
    public Bot Create()
    {
        var settings = settingsManager.Current.Twitch;
        var credentials = new ConnectionCredentials(settings.BotUsername, settings.AccessToken);
        twitchClient.Initialize(credentials, settings.Channel);

        var audienceTracker = new AudienceTracker(settingsManager);

        var commands = new List<IChatCommand>
        {
            new HelloCommand(),
            new DonateCommand(settingsManager),
            new HowManyMessagesCommand(statisticsCollector, userRankService),
            new BotStatsCommand(statisticsCollector),
            new TopUsersCommand(statisticsCollector, userRankService),
            new MyProfileCommand(statisticsCollector, userRankService),
            new ActiveUsersCommand(audienceTracker),
            new ByeCommand(audienceTracker),
            new StreamInfoCommand(streamStatusManager),
            new TrumpCommand(settingsManager),
            new RanksCommand(settingsManager),
            new RankCommand(statisticsCollector, userRankService),
        };

        var commandProcessor = new ChatCommandProcessor(commands);
        commandProcessor.Register(new HelpCommand(commandProcessor.GetAllCommands));

        return new(settingsManager,
            statisticsCollector,
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
