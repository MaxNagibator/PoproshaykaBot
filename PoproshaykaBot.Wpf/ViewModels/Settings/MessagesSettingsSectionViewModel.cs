using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Core.Chat;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class MessagesSettingsSectionViewModel : ObservableObject, IDisposable
{
    private static readonly MessageSettings Defaults = new();

    public MessagesSettingsSectionViewModel()
    {
        Entries =
        [
            new("Приветствие", true, Defaults.WelcomeEnabled, Defaults.Welcome),
            new("Прощание", true, Defaults.FarewellEnabled, Defaults.Farewell),
            new("Подключение бота", true, Defaults.ConnectionEnabled, Defaults.Connection),
            new("Отключение бота", true, Defaults.DisconnectionEnabled, Defaults.Disconnection),
            new("Сообщение !donate", false, true, Defaults.DonateCommandMessage),
        ];
    }

    public IReadOnlyList<MessageEntryViewModel> Entries { get; }

    public void LoadSettings(MessageSettings settings)
    {
        Entries[0].IsEnabled = settings.WelcomeEnabled;
        Entries[0].Text = settings.Welcome;

        Entries[1].IsEnabled = settings.FarewellEnabled;
        Entries[1].Text = settings.Farewell;

        Entries[2].IsEnabled = settings.ConnectionEnabled;
        Entries[2].Text = settings.Connection;

        Entries[3].IsEnabled = settings.DisconnectionEnabled;
        Entries[3].Text = settings.Disconnection;

        Entries[4].Text = settings.DonateCommandMessage;
    }

    public void SaveSettings(MessageSettings settings)
    {
        settings.WelcomeEnabled = Entries[0].IsEnabled;
        settings.Welcome = TrimOrDefault(Entries[0].Text, Defaults.Welcome);

        settings.FarewellEnabled = Entries[1].IsEnabled;
        settings.Farewell = TrimOrDefault(Entries[1].Text, Defaults.Farewell);

        settings.ConnectionEnabled = Entries[2].IsEnabled;
        settings.Connection = TrimOrDefault(Entries[2].Text, Defaults.Connection);

        settings.DisconnectionEnabled = Entries[3].IsEnabled;
        settings.Disconnection = TrimOrDefault(Entries[3].Text, Defaults.Disconnection);

        settings.DonateCommandMessage = TrimOrDefault(Entries[4].Text, Defaults.DonateCommandMessage);
    }

    public void Dispose()
    {
    }

    private static string TrimOrDefault(string value, string fallback)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 ? trimmed : fallback;
    }
}
