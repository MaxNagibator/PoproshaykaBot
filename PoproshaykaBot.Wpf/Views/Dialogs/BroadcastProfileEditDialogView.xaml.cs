using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using System.Windows;

namespace PoproshaykaBot.Wpf.Views.Dialogs;

public partial class BroadcastProfileEditDialogView : IView<BroadcastProfileEditDialogViewModel>
{
    public BroadcastProfileEditDialogView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BroadcastProfileEditDialogViewModel vm)
            await vm.LoadObsScenesAsync();
    }
}
