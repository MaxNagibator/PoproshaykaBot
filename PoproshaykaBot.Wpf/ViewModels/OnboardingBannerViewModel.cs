using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;
using PoproshaykaBot.Core.Settings.Onboarding;
using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed partial class OnboardingBannerViewModel : ObservableObject, IDisposable
{
    private readonly OnboardingChecklist _checklist;
    private readonly IOnboardingWizardLauncher _wizardLauncher;
    private readonly List<IDisposable> _subscriptions = [];

    private bool _suppressed;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _text = string.Empty;

    public OnboardingBannerViewModel(
        OnboardingChecklist checklist,
        IOnboardingWizardLauncher wizardLauncher,
        IEventBus eventBus)
    {
        _checklist = checklist;
        _wizardLauncher = wizardLauncher;
        _wizardLauncher.Closed += OnWizardClosed;

        _subscriptions.Add(eventBus.SubscribeOnUi<BotLifecyclePhaseChanged>(_ => Refresh()));
        _subscriptions.Add(eventBus.SubscribeOnUi<TwitchAuthorizationRefreshed>(_ => Refresh()));

        Refresh();
    }

    public void Suppress()
    {
        _suppressed = true;
        IsVisible = false;
    }

    public void Refresh()
    {
        if (_suppressed)
        {
            IsVisible = false;
            return;
        }

        var missing = _checklist.GetMissingItems();

        if (missing.Count == 0)
        {
            IsVisible = false;
            return;
        }

        Text = $"Бот не готов к работе. Не настроено: {string.Join(", ", missing)}.";
        IsVisible = true;
    }

    public void Dispose()
    {
        _wizardLauncher.Closed -= OnWizardClosed;

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
    }

    [RelayCommand]
    private void OpenWizard()
    {
        _wizardLauncher.Show();
    }

    private void OnWizardClosed(object? sender, EventArgs e)
    {
        Refresh();
    }
}
