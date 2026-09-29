using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Twitch.Auth;
using System.ComponentModel.DataAnnotations;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class BasicSettingsSectionViewModel : ObservableValidator, IDisposable
{
    private static readonly TwitchSettings DefaultSettings = new();

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Укажите канал: без него чат и подключение бота не работают")]
    private string _channel = string.Empty;

    [ObservableProperty]
    private TwitchOAuthRole _chatDisplayAccount;

    public BasicSettingsSectionViewModel()
    {
        ChatAccountOptions =
        [
            new(TwitchOAuthRole.Bot, "бота"),
            new(TwitchOAuthRole.Broadcaster, "стримера"),
        ];
    }

    public IReadOnlyList<ChatAccountOption> ChatAccountOptions { get; }

    public string ChannelPlaceholder => DefaultSettings.Channel;

    public void LoadSettings(TwitchSettings settings)
    {
        Channel = settings.Channel;
        ChatDisplayAccount = settings.ChatDisplayAccount;
    }

    public void SaveSettings(TwitchSettings settings)
    {
        settings.Channel = Channel.Trim();
        settings.ChatDisplayAccount = ChatDisplayAccount;
    }

    public string GetChannel()
    {
        return Channel.Trim();
    }

    public void Dispose()
    {
    }

    [RelayCommand]
    private void ResetChannel()
    {
        Channel = DefaultSettings.Channel;
    }
}

public readonly record struct ChatAccountOption(TwitchOAuthRole Role, string Display);
