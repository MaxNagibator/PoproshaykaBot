using PoproshaykaBot.Core.Settings;

namespace PoproshaykaBot.Core.Tests.Settings;

[TestFixture]
public sealed class SettingsManagerCacheOwnershipTests
{
    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("settings-manager-cache");
        _filePath = Path.Combine(_directory.FullName, "settings.json");
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Delete(recursive: true);
    }

    private DirectoryInfo _directory = null!;
    private string _filePath = null!;

    [Test]
    public void SaveSettings_CallerKeepsEditingItsObject_LeavesTheCacheOnTheSavedVersion()
    {
        var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, _filePath);

        var saved = new AppSettings();
        saved.Twitch.Channel = "первый";
        manager.SaveSettings(saved);

        saved.Twitch.Channel = "правка мимо сохранения";

        Assert.That(manager.Current.Twitch.Channel, Is.EqualTo("первый"),
            "Объект вызывающей стороны не должен становиться кэшем – иначе правка после сохранения молча уезжает в чтения всего приложения.");
    }

    [Test]
    public void Mutate_WritesTheFileAndUpdatesTheCache()
    {
        var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, _filePath);

        manager.Mutate(settings => settings.Twitch.HttpServerPort = 3000);

        var reloaded = new SettingsManager(NullLogger<SettingsManager>.Instance, _filePath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(manager.Current.Twitch.HttpServerPort, Is.EqualTo(3000));
            Assert.That(reloaded.Current.Twitch.HttpServerPort, Is.EqualTo(3000));
        }
    }

    [Test]
    public void UpdateCurrent_ChangesTheCacheWithoutTouchingTheFile()
    {
        var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, _filePath);

        manager.UpdateCurrent(settings => settings.Twitch.Channel = "только в памяти");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(manager.Current.Twitch.Channel, Is.EqualTo("только в памяти"));
            Assert.That(File.Exists(_filePath), Is.False,
                "Правка мастера онбординга живёт до кнопки сохранения и не должна попадать на диск сама.");
        }
    }

    [Test]
    public void SaveSettings_WhenTheWriteFails_LeavesTheCacheMatchingDisk()
    {
        var blocked = Path.Combine(_directory.FullName, "blocked.json");
        Directory.CreateDirectory(blocked);

        var manager = new SettingsManager(NullLogger<SettingsManager>.Instance, blocked);

        var settings = new AppSettings();
        settings.Twitch.Channel = "не доехало до диска";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => manager.SaveSettings(settings), Throws.InstanceOf<InvalidOperationException>());
            Assert.That(manager.Current.Twitch.Channel, Is.EqualTo(new AppSettings().Twitch.Channel));
        }
    }
}
