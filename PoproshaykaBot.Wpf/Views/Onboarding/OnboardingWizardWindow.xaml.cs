using PoproshaykaBot.Wpf.ViewModels.Onboarding;
using System.ComponentModel;
using System.Windows;

namespace PoproshaykaBot.Wpf.Views.Onboarding;

public partial class OnboardingWizardWindow : Window
{
    private readonly OnboardingWizardViewModel _viewModel;
    private bool _closeConfirmed;

    public OnboardingWizardWindow(OnboardingWizardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        WindowChromeTheming.Attach(this);

        _viewModel.CloseRequested += OnCloseRequested;
        Closing += OnClosing;
    }

    private void OnCloseRequested()
    {
        if (_closeConfirmed)
        {
            return;
        }

        Close();
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeConfirmed)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            return;
        }

        e.Cancel = true;

        if (await _viewModel.RequestCloseAsync())
        {
            _closeConfirmed = true;
            Dispatcher.BeginInvoke(() => Close());
        }
    }
}
