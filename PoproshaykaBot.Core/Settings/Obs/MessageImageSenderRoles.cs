namespace PoproshaykaBot.Core.Settings.Obs;

[Flags]
public enum MessageImageSenderRoles
{
    None = 0,
    Broadcaster = 1,
    Moderator = 2,
    Vip = 4,
    Subscriber = 8,
    Everyone = 16,
}
