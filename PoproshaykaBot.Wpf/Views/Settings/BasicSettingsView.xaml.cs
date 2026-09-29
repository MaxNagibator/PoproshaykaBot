using PoproshaykaBot.Wpf.ViewModels.Settings;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Settings;

public partial class BasicSettingsView : UserControl, IView<BasicSettingsSectionViewModel>
{
    public BasicSettingsView()
    {
        InitializeComponent();
    }
}
