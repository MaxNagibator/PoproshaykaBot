using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Core.Broadcast;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class AutoBroadcastSettingsViewModel : ObservableObject, IDisposable
{
    private static readonly AutoBroadcastSettings DefaultSettings = new();

    [ObservableProperty]
    private bool _autoBroadcastEnabled;

    [ObservableProperty]
    private bool _streamStatusNotificationsEnabled;

    [ObservableProperty]
    private string _streamStartMessage = "";

    [ObservableProperty]
    private string _streamStopMessage = "";

    [ObservableProperty]
    private string _streamEndStatsMessage = "";

    [ObservableProperty]
    private int _broadcastIntervalMinutes;

    [ObservableProperty]
    private string _broadcastMessageTemplate = "";

    public AutoBroadcastSettingsViewModel()
    {
    }

    public string StreamStartMessagePlaceholder => DefaultSettings.StreamStartMessage;

    public string StreamStopMessagePlaceholder => DefaultSettings.StreamStopMessage;

    public string StreamEndStatsMessagePlaceholder => DefaultSettings.StreamEndStatsMessage;

    public string BroadcastMessageTemplatePlaceholder => DefaultSettings.BroadcastMessageTemplate;

    public void LoadSettings(AutoBroadcastSettings settings)
    {
        AutoBroadcastEnabled = settings.AutoBroadcastEnabled;
        StreamStatusNotificationsEnabled = settings.StreamStatusNotificationsEnabled;
        StreamStartMessage = settings.StreamStartMessage;
        StreamStopMessage = settings.StreamStopMessage;
        StreamEndStatsMessage = settings.StreamEndStatsMessage;
        BroadcastIntervalMinutes = Math.Max(1, settings.BroadcastIntervalMinutes);
        BroadcastMessageTemplate = settings.BroadcastMessageTemplate;
    }

    public void SaveSettings(AutoBroadcastSettings settings)
    {
        settings.AutoBroadcastEnabled = AutoBroadcastEnabled;
        settings.StreamStatusNotificationsEnabled = StreamStatusNotificationsEnabled;
        settings.StreamStartMessage = StreamStartMessage.Trim();
        settings.StreamStopMessage = StreamStopMessage.Trim();
        settings.StreamEndStatsMessage = StreamEndStatsMessage.Trim();
        settings.BroadcastIntervalMinutes = Math.Max(1, BroadcastIntervalMinutes);
        settings.BroadcastMessageTemplate = BroadcastMessageTemplate.Trim();
    }

    public void Dispose()
    {
    }
}
