using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Wpf.ViewModels.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PoproshaykaBot.Wpf.Views.Controls;

public partial class GameAutocompleteBox : UserControl, IView<GameAutocompleteViewModel>
{
    public GameAutocompleteBox()
    {
        InitializeComponent();
    }

    private void OnSuggestionMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GameSuggestion suggestion }
            && DataContext is GameAutocompleteViewModel viewModel)
        {
            viewModel.ChooseCommand.Execute(suggestion);
        }
    }
}
