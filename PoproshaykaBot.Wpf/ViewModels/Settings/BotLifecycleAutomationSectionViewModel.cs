using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Core.Streaming;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class BotLifecycleAutomationSectionViewModel : ObservableObject, IDisposable
{
    [ObservableProperty]
    private bool _autoConnectOnStreamOnline;

    [ObservableProperty]
    private bool _autoDisconnectOnStreamOffline;

    public BotLifecycleAutomationSectionViewModel()
    {
    }

    public void LoadSettings(BotLifecycleAutomationSettings settings)
    {
        AutoConnectOnStreamOnline = settings.AutoConnectOnStreamOnline;
        AutoDisconnectOnStreamOffline = settings.AutoDisconnectOnStreamOffline;
    }

    public void SaveSettings(BotLifecycleAutomationSettings settings)
    {
        settings.AutoConnectOnStreamOnline = AutoConnectOnStreamOnline;
        settings.AutoDisconnectOnStreamOffline = AutoDisconnectOnStreamOffline;
    }

    public void Dispose()
    {
    }
}
