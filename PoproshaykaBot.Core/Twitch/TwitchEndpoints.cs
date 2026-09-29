namespace PoproshaykaBot.Core.Twitch;

public static class TwitchEndpoints
{
    public const string IdBaseUrl = "https://id.twitch.tv/";
    public const string HelixBaseUrl = "https://api.twitch.tv/";
    public const string OAuthAuthorize = "https://id.twitch.tv/oauth2/authorize";
    public const string OAuthToken = "https://id.twitch.tv/oauth2/token";
    public const string OAuthValidate = "https://id.twitch.tv/oauth2/validate";
    public const string OAuthRevoke = "https://id.twitch.tv/oauth2/revoke";
    public const string EventSubWebSocket = "wss://eventsub.wss.twitch.tv/ws";
    public const string HelixUsers = "helix/users";
    public const string HelixStreams = "helix/streams";
    public const string HelixChannels = "helix/channels";
    public const string HelixGames = "helix/games";
    public const string HelixSearchCategories = "helix/search/categories";
    public const string HelixChatMessages = "helix/chat/messages";
    public const string HelixChatBadgesGlobal = "helix/chat/badges/global";
    public const string HelixChatEmotesGlobal = "helix/chat/emotes/global";
    public const string HelixWhispers = "helix/whispers";
    public const string HelixEventSubSubscriptions = "helix/eventsub/subscriptions";
    public const string HelixPolls = "helix/polls";
    public const string BoxArtClient = "twitch-box-art";
    public const string HelixBotClient = "twitch-helix-bot";
    public const string HelixBroadcasterClient = "twitch-helix-broadcaster";
    public const string HelixAppClient = "twitch-helix-app";
    public const string IdHttpClientName = "twitch-id";
    public const string EventSubBotSession = "eventsub-bot";
    public const string EventSubBroadcasterSession = "eventsub-broadcaster";
}
