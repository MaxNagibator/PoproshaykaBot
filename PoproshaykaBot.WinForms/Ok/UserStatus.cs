namespace PoproshaykaBot.WinForms.Models;

[Flags]
public enum UserStatus
{
    /// <summary>
    /// Обычный пользователь
    /// </summary>
    None = 0,

    /// <summary>
    /// Владелец канала/стример
    /// </summary>
    Broadcaster = 1 << 0,

    /// <summary>
    /// Модератор канала
    /// </summary>
    Moderator = 1 << 1,

    /// <summary>
    /// VIP пользователь
    /// </summary>
    Vip = 1 << 2,

    /// <summary>
    /// Подписчик канала
    /// </summary>
    Subscriber = 1 << 3,
}
