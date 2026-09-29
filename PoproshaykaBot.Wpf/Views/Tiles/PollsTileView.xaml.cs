using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Tiles;

public partial class PollsTileView : UserControl, IView<PollsTileViewModel>
{
    public PollsTileView()
    {
        InitializeComponent();
    }
}
