using PoproshaykaBot.Wpf.ViewModels.Settings;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Settings;

public partial class PollsSettingsSectionView : UserControl, IView<PollsSettingsSectionViewModel>
{
    public PollsSettingsSectionView()
    {
        InitializeComponent();
    }
}
