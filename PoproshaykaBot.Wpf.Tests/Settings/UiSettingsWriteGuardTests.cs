using KeepShell.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;
using System.IO;

namespace PoproshaykaBot.Wpf.Tests.Settings;

[TestFixture]
public class UiSettingsWriteGuardTests
{
    private const string ThemeKey = "ui.shell.theme";

    private string _root = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "poproshayka-ui-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _path = Path.Combine(_root, "ui-preferences.toml");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    [Test]
    public void Перенос_принёсший_файл_вида_отнимает_право_записи_до_перезапуска()
    {
        var guard = new UiSettingsWriteGuard(new SettingsStore(_path));
        guard.SetValue(ThemeKey, "light");

        guard.Suspend();
        WriteImportedFile("dark");
        guard.Resume(rewritten: true);

        guard.SetValue(ThemeKey, "contrast");
        guard.Suspend();
        guard.Resume(rewritten: false);
        guard.SetValue(ThemeKey, "sepia");
        guard.Close();

        Assert.Multiple(() =>
        {
            Assert.That(guard.IsRevoked, Is.True, "Следующий перенос без файла вида права не возвращает");
            Assert.That(guard.GetStringValue(ThemeKey), Is.EqualTo("sepia"), "Смена темы действует в памяти");
            Assert.That(new SettingsStore(_path).GetStringValue(ThemeKey), Is.EqualTo("dark"),
                "Принесённый файл не переписан ни сменой темы, ни закрытием");
        });
    }

    [Test]
    public void Перенос_без_файла_вида_возвращает_право_и_дописывает_принятое()
    {
        var guard = new UiSettingsWriteGuard(new SettingsStore(_path));
        guard.SetValue(ThemeKey, "light");

        guard.Suspend();
        guard.SetValue(ThemeKey, "dark");
        guard.Resume(rewritten: false);
        guard.Close();

        Assert.Multiple(() =>
        {
            Assert.That(guard.IsRevoked, Is.False);
            Assert.That(new SettingsStore(_path).GetStringValue(ThemeKey), Is.EqualTo("dark"),
                "Правка, принятая во время переноса, доезжает до файла после него");
        });
    }

    [Test]
    public void Правка_до_переноса_ложится_на_диск_раньше_копирования()
    {
        var guard = new UiSettingsWriteGuard(new SettingsStore(_path));
        guard.SetValue(ThemeKey, "light");

        guard.Suspend();

        Assert.That(new SettingsStore(_path).GetStringValue(ThemeKey), Is.EqualTo("light"),
            "Отложенная запись не должна дождаться копирования и лечь поверх принесённого");
    }

    [TestCase(true, "dark")]
    [TestCase(false, "light")]
    public void Сорвавшаяся_досылка_не_ложится_поверх_принесённого_файла_и_не_теряется_без_него(bool rewritten, string expected)
    {
        var guard = new UiSettingsWriteGuard(new SettingsStore(_path));
        guard.SetValue(ThemeKey, "system");
        guard.Flush();
        guard.SetValue(ThemeKey, "light");

        using (new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            guard.Suspend();
        }

        if (rewritten)
        {
            WriteImportedFile("dark");
        }

        guard.Resume(rewritten);
        guard.Flush();
        guard.Close();

        Assert.Multiple(() =>
        {
            Assert.That(new SettingsStore(_path).GetStringValue(ThemeKey), Is.EqualTo(expected),
                "Принесённый файл цел после закрытия, а без него правка доезжает до файла");
            Assert.That(guard.GetStringValue(ThemeKey), Is.EqualTo("light"), "Правка действует в памяти");
        });
    }

    private void WriteImportedFile(string theme)
    {
        var imported = new SettingsStore(_path + ".import");
        imported.SetValue(ThemeKey, theme);
        imported.Close();
        File.Move(_path + ".import", _path, overwrite: true);
    }
}
