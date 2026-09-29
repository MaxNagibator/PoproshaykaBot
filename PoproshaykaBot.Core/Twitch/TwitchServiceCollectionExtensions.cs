using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.EventSub;
using PoproshaykaBot.Core.Twitch.Helix;
using PoproshaykaBot.Core.Twitch.Onboarding;

namespace PoproshaykaBot.Core.Twitch;

public static class TwitchServiceCollectionExtensions
{
    public static IServiceCollection AddTwitchClients(this IServiceCollection services)
    {
        services.AddTransient<BotTwitchAuthHandler>();
        services.AddTransient<BroadcasterTwitchAuthHandler>();

        services.AddHttpClient(TwitchEndpoints.HelixBotClient, client =>
            {
                client.BaseAddress = new(TwitchEndpoints.HelixBaseUrl);
            })
            .AddHttpMessageHandler<BotTwitchAuthHandler>();

        services.AddHttpClient(TwitchEndpoints.HelixBroadcasterClient, client =>
            {
                client.BaseAddress = new(TwitchEndpoints.HelixBaseUrl);
            })
            .AddHttpMessageHandler<BroadcasterTwitchAuthHandler>();

        services.AddHttpClient(TwitchEndpoints.HelixAppClient, client =>
        {
            client.BaseAddress = new(TwitchEndpoints.HelixBaseUrl);
        });

        services.AddKeyedSingleton<ITwitchHelixClient, BotHelixClient>(TwitchEndpoints.HelixBotClient);
        services.AddKeyedSingleton<ITwitchHelixClient, BroadcasterHelixClient>(TwitchEndpoints.HelixBroadcasterClient);

        services.AddKeyedSingleton<ITwitchEventSubClient, TwitchEventSubClient>(TwitchEndpoints.EventSubBotSession);
        services.AddKeyedSingleton<ITwitchEventSubClient, TwitchEventSubClient>(TwitchEndpoints.EventSubBroadcasterSession);

        services.AddSingleton<OAuthStatusReporter>();
        services.AddSingleton<OAuthTokenClient>();
        services.AddSingleton<OAuthAccountWriter>();
        services.AddSingleton<OAuthFlowCoordinator>();
        services.AddSingleton<OAuthTokenRefresher>();
        services.AddSingleton<ITwitchOAuthService, TwitchOAuthService>();
        services.AddSingleton<IClientCredentialsValidator, ClientCredentialsValidator>();

        services.AddSingleton<IBroadcasterIdProvider, BroadcasterIdProvider>();
        services.AddSingleton<IOnboardingChannelValidator, OnboardingChannelValidator>();

        return services;
    }
}
