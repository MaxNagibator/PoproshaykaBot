using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Dialogs;

public partial class PollFromProfileDialogView : UserControl, IView<PollFromProfileDialogViewModel>
{
    public PollFromProfileDialogView()
    {
        InitializeComponent();
    }
}
