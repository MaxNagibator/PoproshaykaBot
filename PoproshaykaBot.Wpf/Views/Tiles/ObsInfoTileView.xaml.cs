using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Tiles;

public partial class ObsInfoTileView : UserControl, IView<ObsInfoTileViewModel>
{
    public ObsInfoTileView()
    {
        InitializeComponent();
    }
}
