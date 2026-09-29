using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Wpf.Bootstrap;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed partial class ShellPreferences : ShellPreferencesBase
{
    [ObservableProperty]
    private bool _navCollapsed;

    public ShellPreferences(ISettingsStore settings)
        : base(settings, new(SettingsKeys.ShowPageHeader, SettingsKeys.EnableToastNotifications, SettingsKeys.FontScale))
    {
        SuppressPersist = true;
        NavCollapsed = Settings.GetBool(SettingsKeys.NavCollapsed);
        SuppressPersist = false;
    }

    public string? LastPage
    {
        get => Settings.GetStringValue(SettingsKeys.LastPage);
        set => Settings.SetValue(SettingsKeys.LastPage, value ?? string.Empty);
    }

    partial void OnNavCollapsedChanged(bool value)
    {
        PersistBool(SettingsKeys.NavCollapsed, value);
    }
}
