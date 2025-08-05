namespace PoproshaykaBot.WinForms.Settings;

public class AppSettings
{
    public TwitchSettings Twitch { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
}

public class TwitchSettings
{
    public string BotUsername { get; set; } = "thevsakeeper";

    public string Channel { get; set; } = "bobito217";

    public int MessagesAllowedInPeriod { get; set; } = 750;

    public int ThrottlingPeriodSeconds { get; set; } = 30;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = "http://localhost:8080";

    public string[] Scopes { get; set; } = ["chat:read", "chat:edit"];

    public int HttpServerPort { get; set; } = 8080;

    public bool HttpServerEnabled { get; set; } = true;

    public bool ObsOverlayEnabled { get; set; } = true;

    public MessageSettings Messages { get; set; } = new();

    public ObsChatSettings ObsChat { get; set; } = new();
}

public class MessageSettings
{
    public bool WelcomeEnabled { get; set; } = false;

    public string Welcome { get; set; } = "Добро пожаловать в чат, {username}! 👋";

    public bool FarewellEnabled { get; set; } = false;

    public string Farewell { get; set; } = "До свидания, {username}! Увидимся позже! ❤️";

    public bool ConnectionEnabled { get; set; } = true;

    public string Connection { get; set; } = "ЭЩКЕРЕ";

    public bool DisconnectionEnabled { get; set; } = true;

    public string Disconnection { get; set; } = "Пока-пока! ❤️";
}

public class UiSettings
{
    public bool ShowLogsPanel { get; set; } = true;
    public bool ShowChatPanel { get; set; } = true;
}

public class ObsChatSettings
{
    public string BackgroundColor { get; set; } = "rgba(0, 0, 0, 0.7)";
    public string TextColor { get; set; } = "#ffffff";
    public string UsernameColor { get; set; } = "#9146ff";
    public string SystemMessageColor { get; set; } = "#ffcc00";
    public string TimestampColor { get; set; } = "#999999";

    public string FontFamily { get; set; } = "Arial, sans-serif";
    public int FontSize { get; set; } = 14;
    public bool FontBold { get; set; } = false;

    public int Padding { get; set; } = 5;
    public int Margin { get; set; } = 5;
    public int BorderRadius { get; set; } = 5;

    public int AnimationDuration { get; set; } = 300;
    public bool EnableAnimations { get; set; } = true;

    public int MaxMessages { get; set; } = 50;
    public bool ShowTimestamp { get; set; } = true;

    public EmoteSize EmoteSize { get; set; } = EmoteSize.Medium;
    public BadgeSize BadgeSize { get; set; } = BadgeSize.Small;
}

public enum EmoteSize
{
    Small = 1,
    Medium = 2,
    Large = 4,
}

public enum BadgeSize
{
    Small = 1,
    Medium = 2,
    Large = 4,
}
