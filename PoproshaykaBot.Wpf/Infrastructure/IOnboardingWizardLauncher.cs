namespace PoproshaykaBot.Wpf.Infrastructure;

public interface IOnboardingWizardLauncher
{
    event EventHandler? Closed;

    bool HasBeenShown { get; }

    void Show();
}
