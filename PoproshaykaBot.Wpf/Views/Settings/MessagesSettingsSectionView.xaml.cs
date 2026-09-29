using PoproshaykaBot.Wpf.ViewModels.Settings;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Settings;

public partial class MessagesSettingsSectionView : UserControl, IView<MessagesSettingsSectionViewModel>
{
    public MessagesSettingsSectionView()
    {
        InitializeComponent();
    }
}
