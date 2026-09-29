using CommunityToolkit.Mvvm.ComponentModel;

namespace PoproshaykaBot.Wpf.ViewModels.Onboarding;

public abstract partial class OnboardingPageViewModelBase : ObservableObject, IOnboardingPageViewModel
{
    [ObservableProperty]
    private bool _canAdvance;

    public event EventHandler? CanAdvanceChanged;

    public abstract string PageTitle { get; }

    public virtual void OnEnter(OnboardingContext context)
    {
    }

    public virtual Task<bool> OnLeavingAsync(OnboardingContext context)
    {
        return Task.FromResult(CanAdvance);
    }

    partial void OnCanAdvanceChanged(bool value)
    {
        CanAdvanceChanged?.Invoke(this, EventArgs.Empty);
    }
}
