using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PoproshaykaBot.Wpf.ViewModels.Dialogs;

public sealed partial class ChatBlockersDialogViewModel : ObservableObject, IDialogViewModel
{
    [ObservableProperty]
    private string _selectors;

    public ChatBlockersDialogViewModel(string selectors)
    {
        _selectors = selectors;
    }

    public event EventHandler<bool>? RequestClose;

    public string Title => "Блокировка баннеров чата";

    public string Hint =>
        "По одному CSS-селектору в строке. Строки, начинающиеся с #, – комментарии. Встроенные блокираторы работают всегда.";

    [RelayCommand]
    private void Confirm()
    {
        RequestClose?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }
}
