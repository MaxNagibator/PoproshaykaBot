using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Tiles;

public partial class StreamInfoTileView : UserControl, IView<StreamInfoTileViewModel>
{
    public StreamInfoTileView()
    {
        InitializeComponent();
    }
}
