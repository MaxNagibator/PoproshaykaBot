using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Dialogs;

public partial class PollProfileEditDialogView : UserControl, IView<PollProfileEditDialogViewModel>
{
    public PollProfileEditDialogView()
    {
        InitializeComponent();
    }
}
