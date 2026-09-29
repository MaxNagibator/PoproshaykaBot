using PoproshaykaBot.Wpf.ViewModels;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views;

public partial class OnboardingBannerView : UserControl, IView<OnboardingBannerViewModel>
{
    public OnboardingBannerView()
    {
        InitializeComponent();
    }
}
