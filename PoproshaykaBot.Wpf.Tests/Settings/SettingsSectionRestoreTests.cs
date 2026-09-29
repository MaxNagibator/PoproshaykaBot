using KeepShell.ViewModels;
using MahApps.Metro.IconPacks;

namespace PoproshaykaBot.Wpf.Tests.Settings;

[TestFixture]
public class SettingsSectionRestoreTests
{
    [TestCase("update", "update")]
    [TestCase("несуществующий", "basic")]
    [TestCase("", "basic")]
    public void Запомненный_раздел_восстанавливается_а_неизвестный_ключ_оставляет_первый(string stored, string expected)
    {
        var sections = List();

        sections.Restore(stored);

        Assert.That(sections.Selected, Is.SameAs(sections[expected]));
    }

    private static SettingsSectionList List()
    {
        return new(
            new SettingsSection("basic", "Основные", PackIconLucideKind.Settings2, "канал лимиты сообщения"),
            new SettingsSection("obs", "OBS", PackIconLucideKind.MonitorPlay, "websocket подключение оверлей порт"),
            new SettingsSection("update", "Обновления", PackIconLucideKind.Download, "github релизы версия"));
    }
}
