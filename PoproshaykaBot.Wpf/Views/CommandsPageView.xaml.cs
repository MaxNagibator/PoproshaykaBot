using PoproshaykaBot.Wpf.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Threading;

namespace PoproshaykaBot.Wpf.Views;

public partial class CommandsPageView : IView<CommandsPageViewModel>
{
    private static readonly TimeSpan UsageInterval = TimeSpan.FromSeconds(5);

    private readonly DispatcherTimer _usageTimer;

    private CommandsPageViewModel? _viewModel;

    public CommandsPageView()
    {
        InitializeComponent();

        _usageTimer = new(DispatcherPriority.Background, Dispatcher)
        {
            Interval = UsageInterval,
        };

        _usageTimer.Tick += OnUsageTick;

        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as CommandsPageViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        _viewModel?.RefreshUsage();
        _usageTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _usageTimer.Stop();

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnUsageTick(object? sender, EventArgs e)
    {
        _viewModel?.RefreshUsage();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(CommandsPageViewModel.Notice), StringComparison.Ordinal)
            || string.IsNullOrEmpty(_viewModel?.Notice))
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(AnnounceNotice));
    }

    private void AnnounceNotice()
    {
        var peer = UIElementAutomationPeer.FromElement(NoticeText)
            ?? UIElementAutomationPeer.CreatePeerForElement(NoticeText);

        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
