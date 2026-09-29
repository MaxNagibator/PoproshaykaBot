using PoproshaykaBot.Wpf.ViewModels.Settings;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Settings;

public partial class HttpServerSectionView : UserControl, IView<HttpServerSectionViewModel>
{
    public HttpServerSectionView()
    {
        InitializeComponent();
    }
}
