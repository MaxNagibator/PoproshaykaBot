using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace PoproshaykaBot.Wpf.Views;

public partial class MainWindow : Window
{
    private static readonly WindowPlacementKeys WindowKeys = new(
        SettingsKeys.WindowLeft, SettingsKeys.WindowTop, SettingsKeys.WindowWidth,
        SettingsKeys.WindowHeight, SettingsKeys.WindowMaximized);

    private readonly ShellViewModel _viewModel;
    private readonly ISettingsStore _settings;
    private bool _closeConfirmed;

    public MainWindow(ShellViewModel viewModel, ISettingsStore settings, DashboardLayoutStore layoutStore)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;

        WindowChromeTheming.Attach(this);

        Closing += OnClosing;

        ImportWinFormsPlacement(layoutStore);

        if (!App.IsGallery)
        {
            WindowPlacement.Restore(this, _settings, WindowKeys);
        }
    }

    private void ImportWinFormsPlacement(DashboardLayoutStore layoutStore)
    {
        if (App.IsHeadless)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_settings.GetStringValue(SettingsKeys.WindowWidth)))
        {
            return;
        }

        if (layoutStore.LoadMainWindow() is not { Width: > 0, Height: > 0 } saved)
        {
            return;
        }

        // TODO: масштаб берётся системный – на конфигурации со смешанным DPI окно приедет по
        //  масштабу основного монитора; уточнять, когда пройдёт прогон на такой машине
        var bounds = WinFormsPlacementImport.ToDeviceIndependent(saved, WinFormsPlacementImport.GetSystemScale());

        _settings.SetDouble(SettingsKeys.WindowLeft, bounds.X);
        _settings.SetDouble(SettingsKeys.WindowTop, bounds.Y);
        _settings.SetDouble(SettingsKeys.WindowWidth, bounds.Width);
        _settings.SetDouble(SettingsKeys.WindowHeight, bounds.Height);
        _settings.SetBool(SettingsKeys.WindowMaximized, saved.Maximized);
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeConfirmed)
        {
            return;
        }

        if (App.IsFatalShutdown)
        {
            return;
        }

        e.Cancel = true;

        if (await _viewModel.RequestCloseAsync())
        {
            _closeConfirmed = true;

            if (!App.IsHeadless)
            {
                WindowPlacement.Save(this, _settings, WindowKeys);
            }

            Dispatcher.BeginInvoke(() => Close());
        }
    }
}
