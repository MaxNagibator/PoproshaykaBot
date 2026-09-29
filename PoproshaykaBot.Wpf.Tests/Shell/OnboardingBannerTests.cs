using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Onboarding;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.ViewModels;
using System.IO;

namespace PoproshaykaBot.Wpf.Tests.Shell;

[TestFixture]
public class OnboardingBannerTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"poproshayka-banner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Test]
    public void Ненастроенный_профиль_поднимает_баннер()
    {
        using var banner = CreateBanner();

        Assert.Multiple(() =>
        {
            Assert.That(banner.IsVisible, Is.True);
            Assert.That(banner.Text, Does.StartWith("Бот не готов к работе"));
        });
    }

    [Test]
    public void Подавленный_баннер_не_возвращается_обновлением()
    {
        using var banner = CreateBanner();

        banner.Suppress();
        banner.Refresh();

        Assert.That(banner.IsVisible, Is.False);
    }

    private OnboardingBannerViewModel CreateBanner()
    {
        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, Path.Combine(_directory, "settings.json"));
        var accounts = new AccountsStore(filePath: Path.Combine(_directory, "accounts.json"));
        var checklist = new OnboardingChecklist(settings, accounts);
        var bus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);

        return new(checklist, new FakeWizardLauncher(), bus);
    }

    private sealed class FakeWizardLauncher : IOnboardingWizardLauncher
    {
        public event EventHandler? Closed;

        public bool HasBeenShown { get; private set; }

        public void Show()
        {
            HasBeenShown = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
