using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Tiles;

public partial class ObsOutputCardView : UserControl, IView<ObsOutputCardViewModel>
{
    public ObsOutputCardView()
    {
        InitializeComponent();
    }
}
