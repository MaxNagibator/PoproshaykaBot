using CommunityToolkit.Mvvm.ComponentModel;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class ObsSourceItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isChecked;

    public ObsSourceItemViewModel(string name, bool isChecked)
    {
        Name = name;
        _isChecked = isChecked;
    }

    public string Name { get; }
}
