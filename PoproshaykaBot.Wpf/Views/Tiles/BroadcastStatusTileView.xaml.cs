using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Tiles;

public partial class BroadcastStatusTileView : UserControl, IView<BroadcastStatusTileViewModel>
{
    public BroadcastStatusTileView()
    {
        InitializeComponent();
    }
}
