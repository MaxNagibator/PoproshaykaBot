using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Chat.Display;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Twitch.Chat;
using PoproshaykaBot.Core.Users;

namespace PoproshaykaBot.Core.Chat;

public static class ChatServiceCollectionExtensions
{
    public static IServiceCollection AddChatPipeline(this IServiceCollection services)
    {
        services.AddSingleton<ChatIngestionService>();
        services.AddSingleton<IHostedComponent>(sp => sp.GetRequiredService<ChatIngestionService>());
        services.AddSingleton<ChatSender>();
        services.AddSingleton<IHostedComponent>(sp => sp.GetRequiredService<ChatSender>());

        services.AddSingleton<ChatHistoryManager>();
        services.AddSingleton<ChatDisplayStore>();
        services.AddSingleton<ChatDecorationsProvider>();
        services.AddSingleton<UserRankService>();
        services.AddSingleton<UserPointsManagementService>();
        services.AddSingleton<UserProfileImageProvider>();

        services.AddSingleton<TwitchChatMessenger>();
        services.AddSingleton<IChatMessenger>(sp => sp.GetRequiredService<TwitchChatMessenger>());

        services.AddSingleton<AudienceTracker>();

        RegisterChatCommands(services);

        services.AddSingleton<CommandResponseTracker>();

        services.AddSingleton<ChatCommandProcessor>(sp =>
        {
            var commands = sp.GetServices<IChatCommand>().ToList();
            var commandSettingsStore = sp.GetRequiredService<CommandSettingsStore>();
            var usageRepository = sp.GetRequiredService<CommandUsageRepository>();
            var logger = sp.GetRequiredService<ILogger<ChatCommandProcessor>>();
            var processor = new ChatCommandProcessor(commands, commandSettingsStore, usageRepository, logger);
            processor.Register(new HelpCommand(processor.GetEnabledCommands, processor.Prefix));
            return processor;
        });

        services.AddSingleton<TwitchChatHandler>();
        services.AddSingleton<IChannelProvider>(sp => sp.GetRequiredService<TwitchChatHandler>());

        return services;
    }

    private static void RegisterChatCommands(IServiceCollection services)
    {
        services.AddSingleton<IChatCommand, HelloCommand>();
        services.AddSingleton<IChatCommand, DonateCommand>();
        services.AddSingleton<IChatCommand, HowManyMessagesCommand>();
        services.AddSingleton<IChatCommand, BotStatsCommand>();
        services.AddSingleton<IChatCommand, TopUsersCommand>();
        services.AddSingleton<IChatCommand, MyProfileCommand>();
        services.AddSingleton<IChatCommand, ActiveUsersCommand>();
        services.AddSingleton<IChatCommand, ByeCommand>();
        services.AddSingleton<IChatCommand, StreamInfoCommand>();
        services.AddSingleton<IChatCommand, TrumpCommand>();
        services.AddSingleton<IChatCommand, RanksCommand>();
        services.AddSingleton<IChatCommand, RankCommand>();
        services.AddSingleton<IChatCommand, ProfileCommand>();
        services.AddSingleton<IChatCommand, TitleCommand>();
        services.AddSingleton<IChatCommand, GameCommand>();
        services.AddSingleton<IChatCommand, LastStreamCommand>();
        services.AddSingleton<IChatCommand, StreamsCountCommand>();
        services.AddSingleton<IChatCommand, PeakStreamCommand>();
        services.AddSingleton<IChatCommand, WasThereCommand>();
        services.AddSingleton<IChatCommand, TopStreamsCommand>();
    }
}
