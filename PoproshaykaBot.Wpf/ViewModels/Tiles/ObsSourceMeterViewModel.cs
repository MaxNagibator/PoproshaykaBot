using System;
using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.ViewModels.Tiles;

public enum ObsSourceMeterState
{
    None = 0,
    Active = 1,
    Muted = 2,
    Missing = 3,
}

public sealed partial class ObsSourceMeterViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayName = "–";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLevel))]
    private ObsSourceMeterState _state;

    [ObservableProperty]
    private string _stateText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LevelSeverity))]
    private double _level;

    public ObsSourceMeterViewModel(string sourceName)
    {
        SourceName = sourceName;
        DisplayName = string.IsNullOrWhiteSpace(sourceName) ? "–" : sourceName.Trim();
    }

    public string SourceName { get; }

    public bool Muted { get; set; }

    public bool Found { get; set; }

    public bool ShowsLevel => State == ObsSourceMeterState.Active;

    public string? LevelSeverity => Level switch
    {
        >= 0.9D => "Error",
        >= 0.68D => "Warning",
        _ => "Success",
    };

    public void ShowActive(string displayName, double? volumeDecibels)
    {
        SetName(displayName);
        State = ObsSourceMeterState.Active;
        StateText = volumeDecibels.HasValue
            ? string.Create(UiCulture.Russian, $"{volumeDecibels.Value:0.#} дБ")
            : string.Empty;
    }

    public void ShowMuted(string displayName)
    {
        SetName(displayName);
        State = ObsSourceMeterState.Muted;
        StateText = "выключен";
        Level = 0D;
    }

    public void ShowMissing(string displayName)
    {
        SetName(displayName);
        State = ObsSourceMeterState.Missing;
        StateText = "не найден в OBS";
        Level = 0D;
    }

    public void ApplyLevel(double level)
    {
        Level = Math.Clamp(level, 0D, 1D);
    }

    private void SetName(string displayName)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "–" : displayName.Trim();
    }
}
