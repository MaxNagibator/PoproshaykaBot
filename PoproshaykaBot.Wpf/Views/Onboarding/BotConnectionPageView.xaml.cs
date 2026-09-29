using PoproshaykaBot.Wpf.ViewModels.Onboarding;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Onboarding;

public partial class BotConnectionPageView : UserControl, IView<BotConnectionPageViewModel>
{
    public BotConnectionPageView()
    {
        InitializeComponent();
    }
}
