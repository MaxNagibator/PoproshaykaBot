using PoproshaykaBot.Wpf.ViewModels.Onboarding;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Onboarding;

public partial class AuthorizationPageView : UserControl, IView<BotAuthorizationPageViewModel>, IView<BroadcasterAuthorizationPageViewModel>
{
    public AuthorizationPageView()
    {
        InitializeComponent();
    }
}
