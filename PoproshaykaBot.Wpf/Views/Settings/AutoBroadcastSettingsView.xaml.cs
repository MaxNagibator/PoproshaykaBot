using PoproshaykaBot.Wpf.ViewModels.Settings;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Settings;

public partial class AutoBroadcastSettingsView : UserControl, IView<AutoBroadcastSettingsViewModel>
{
    public AutoBroadcastSettingsView()
    {
        InitializeComponent();
    }
}
