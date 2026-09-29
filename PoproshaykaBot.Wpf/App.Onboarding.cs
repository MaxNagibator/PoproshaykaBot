using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Settings.Onboarding;
using PoproshaykaBot.Wpf.Infrastructure;
using System.Windows.Threading;

namespace PoproshaykaBot.Wpf;

public partial class App
{
    private bool _onboardingLaunched;

    private void MaybeLaunchOnboardingWizard()
    {
        if (_onboardingLaunched || _services is null)
        {
            return;
        }

        var checklist = _services.GetRequiredService<OnboardingChecklist>();

        if (!checklist.RequiresWizard)
        {
            return;
        }

        _onboardingLaunched = true;

        Dispatcher.BeginInvoke(ShowStartupWizard, DispatcherPriority.ContextIdle);
    }

    private void ShowStartupWizard()
    {
        var launcher = _services!.GetRequiredService<IOnboardingWizardLauncher>();

        if (launcher.HasBeenShown)
        {
            return;
        }

        launcher.Show();
    }
}
