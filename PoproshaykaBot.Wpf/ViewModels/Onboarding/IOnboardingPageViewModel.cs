namespace PoproshaykaBot.Wpf.ViewModels.Onboarding;

public interface IOnboardingPageViewModel
{
    event EventHandler? CanAdvanceChanged;

    string PageTitle { get; }

    bool CanAdvance { get; }

    void OnEnter(OnboardingContext context);

    Task<bool> OnLeavingAsync(OnboardingContext context);
}
