using PoproshaykaBot.Wpf.ViewModels.Settings;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Settings;

public partial class OAuthSettingsView : UserControl, IView<OAuthSettingsViewModel>
{
    public OAuthSettingsView()
    {
        InitializeComponent();
    }
}
