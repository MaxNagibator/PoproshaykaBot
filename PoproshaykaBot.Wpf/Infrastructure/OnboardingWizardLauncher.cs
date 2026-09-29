using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Views.Onboarding;
using System.Windows;

namespace PoproshaykaBot.Wpf.Infrastructure;

public sealed class OnboardingWizardLauncher(
    IServiceProvider services,
    ILogger<OnboardingWizardLauncher> logger) : IOnboardingWizardLauncher
{
    private bool _isOpen;

    public event EventHandler? Closed;

    public bool HasBeenShown { get; private set; }

    public void Show()
    {
        if (_isOpen)
        {
            return;
        }

        _isOpen = true;
        HasBeenShown = true;

        try
        {
            var wizard = services.GetRequiredService<OnboardingWizardWindow>();
            wizard.Owner = Application.Current.MainWindow;

            if (App.IsHeadless)
            {
                OffScreenWindow.Prepare(wizard, wizard.Width, wizard.Height);
            }

            wizard.ShowDialog();
        }
        catch (Exception exception)
        {
            logger.OnboardingWizardShowFailed(exception);
        }
        finally
        {
            _isOpen = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
