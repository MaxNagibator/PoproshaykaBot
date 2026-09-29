using KeepShell.Bootstrap;
using KeepShell.Testing;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.ViewModels;

namespace PoproshaykaBot.Wpf.Tests.Shell;

[TestFixture]
public class ShellPreferencesTests
{
    [Test]
    public void Loads_existing_values_without_rewriting()
    {
        var store = new MemorySettings();
        store.SetValue(SettingsKeys.NavCollapsed, "true");

        var prefs = new ShellPreferences(store);

        Assert.That(prefs.NavCollapsed, Is.True);
    }

    [Test]
    public void Persists_changes()
    {
        var store = new MemorySettings();
        var prefs = new ShellPreferences(store);

        prefs.NavCollapsed = true;

        Assert.That(store.GetStringValue(SettingsKeys.NavCollapsed), Is.EqualTo("true"));
    }
}
