using PoproshaykaBot.Wpf.ViewModels.Onboarding;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Onboarding;

public partial class WelcomePageView : UserControl, IView<WelcomePageViewModel>
{
    public WelcomePageView()
    {
        InitializeComponent();
    }
}
