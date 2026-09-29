using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class MessageEntryViewModel : ObservableObject
{
    private readonly bool _defaultEnabled;
    private readonly string _defaultText;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _text = string.Empty;

    public MessageEntryViewModel(string label, bool hasEnabledToggle, bool defaultEnabled, string defaultText)
    {
        Label = label;
        HasEnabledToggle = hasEnabledToggle;
        _defaultEnabled = defaultEnabled;
        _defaultText = defaultText;
        Placeholder = defaultText;
        _isEnabled = defaultEnabled;
        _text = defaultText;
    }

    public string Label { get; }

    public bool HasEnabledToggle { get; }

    public string Placeholder { get; }

    [RelayCommand]
    private void Reset()
    {
        IsEnabled = _defaultEnabled;
        Text = _defaultText;
    }
}
