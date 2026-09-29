using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Dialogs;

public partial class ColorPickerDialogView : UserControl, IView<ColorPickerDialogViewModel>
{
    public ColorPickerDialogView()
    {
        InitializeComponent();
    }
}
