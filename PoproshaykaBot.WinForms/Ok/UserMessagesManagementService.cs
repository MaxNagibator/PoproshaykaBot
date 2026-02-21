using PoproshaykaBot.WinForms.Chat;
using PoproshaykaBot.WinForms.Settings;

namespace PoproshaykaBot.WinForms;

public sealed class UserMessagesManagementService(
    StatisticsCollector statisticsCollector,
    TwitchChatMessenger messenger,
    SettingsManager settingsManager)
{
    public bool PunishUser(string userId, string userName, ulong removedMessagesCount, string? channel)
    {
        if (removedMessagesCount == 0)
        {
            return false;
        }

        var updated = statisticsCollector.DecrementUserMessages(userId, removedMessagesCount);

        if (!updated)
        {
            return false;
        }

        var messageSettings = settingsManager.Current.Twitch.Messages;
        if (messageSettings.PunishmentEnabled && !string.IsNullOrWhiteSpace(channel))
        {
            var message = FormatMessage(messageSettings.PunishmentMessage, userName, removedMessagesCount);
            messenger.Send(channel, message);
        }

        return true;
    }

    public bool RewardUser(string userId, string userName, ulong addedMessagesCount, string? channel)
    {
        if (addedMessagesCount == 0)
        {
            return false;
        }

        var updated = statisticsCollector.IncrementUserMessages(userId, addedMessagesCount);

        if (!updated)
        {
            return false;
        }

        var messageSettings = settingsManager.Current.Twitch.Messages;
        if (messageSettings.RewardEnabled && !string.IsNullOrWhiteSpace(channel))
        {
            var message = FormatMessage(messageSettings.RewardMessage, userName, addedMessagesCount);
            messenger.Send(channel, message);
        }

        return true;
    }

    public string GetPunishmentNotification(string userName, ulong removedMessagesCount)
    {
        var messageSettings = settingsManager.Current.Twitch.Messages;
        return FormatMessage(messageSettings.PunishmentNotification, userName, removedMessagesCount);
    }

    public string GetRewardNotification(string userName, ulong addedMessagesCount)
    {
        var messageSettings = settingsManager.Current.Twitch.Messages;
        return FormatMessage(messageSettings.RewardNotification, userName, addedMessagesCount);
    }

    private static string FormatMessage(string template, string userName, ulong count)
    {
        return template
            .Replace("{username}", userName)
            .Replace("{count}", count.ToString());
    }
}
