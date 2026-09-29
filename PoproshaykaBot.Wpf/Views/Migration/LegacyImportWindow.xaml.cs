using PoproshaykaBot.Wpf.ViewModels.Migration;
using System.Windows;

namespace PoproshaykaBot.Wpf.Views.Migration;

public partial class LegacyImportWindow : Window
{
    private readonly LegacyImportViewModel _viewModel;

    public LegacyImportWindow(LegacyImportViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        WindowChromeTheming.Attach(this);

        _viewModel.CloseRequested += OnCloseRequested;
        Closed += OnClosed;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        _viewModel.CloseRequested -= OnCloseRequested;
    }
}
