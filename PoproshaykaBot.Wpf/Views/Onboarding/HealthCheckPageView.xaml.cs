using PoproshaykaBot.Wpf.ViewModels.Onboarding;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Onboarding;

public partial class HealthCheckPageView : UserControl, IView<HealthCheckPageViewModel>
{
    public HealthCheckPageView()
    {
        InitializeComponent();
    }
}
