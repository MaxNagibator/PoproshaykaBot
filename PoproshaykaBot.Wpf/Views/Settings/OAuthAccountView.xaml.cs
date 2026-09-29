using PoproshaykaBot.Wpf.ViewModels.Settings;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Settings;

public partial class OAuthAccountView : UserControl, IView<OAuthAccountViewModel>
{
    public OAuthAccountView()
    {
        InitializeComponent();
    }
}
