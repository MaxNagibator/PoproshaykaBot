using PoproshaykaBot.Core.Chat.Display;

namespace PoproshaykaBot.Core.Tests.Chat.Display;

[TestFixture]
public sealed class ChatDisplayStoreTests
{
    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("chat-display-store");
        _blockersPath = Path.Combine(_directory.FullName, "chat-blockers.txt");
        _zoomPath = Path.Combine(_directory.FullName, "chat-zoom.txt");
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Delete(recursive: true);
    }

    private const string Selectors = ".banner\n# свой комментарий\n.promo";

    private DirectoryInfo _directory = null!;
    private string _blockersPath = null!;
    private string _zoomPath = null!;

    [TestCase(false, TestName = "Сорвавшееся_чтение_правил_запрещает_перезапись_до_удачного_чтения")]
    [TestCase(true, TestName = "Сорвавшееся_чтение_правил_прежним_методом_тоже_запрещает_перезапись")]
    public void Сорвавшееся_чтение_правил_запрещает_перезапись(bool throughLoadBlockersText)
    {
        File.WriteAllText(_blockersPath, Selectors);
        var store = Store();

        using (Lock(_blockersPath))
        {
            if (throughLoadBlockersText)
            {
                Assert.That(store.LoadBlockersText(), Is.Empty);
            }
            else
            {
                Assert.That(store.TryLoadBlockersText(out _), Is.False, "Отказ чтения обязан отличаться от пустого файла.");
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.TrySaveBlockersText(string.Empty), Is.False,
                "Редактор открылся пустым, потому что файл не прочитался, – сохранение стёрло бы все прежние правила.");

            Assert.That(File.ReadAllText(_blockersPath), Is.EqualTo(Selectors));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.TryLoadBlockersText(out var text), Is.True);
            Assert.That(text, Is.EqualTo(Selectors));
            Assert.That(store.TrySaveBlockersText(".new"), Is.True, "Удачное чтение возвращает право записи.");
            Assert.That(File.ReadAllText(_blockersPath), Is.EqualTo(".new"));
        }
    }

    [Test]
    public void Отсутствующий_файл_правил_читается_пустым_и_запись_не_запрещает()
    {
        var store = Store();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.TryLoadBlockersText(out var text), Is.True, "Файла нет – законная пустота свежего профиля, а не отказ.");
            Assert.That(text, Is.Empty);
            Assert.That(store.TrySaveBlockersText(".banner"), Is.True);
            Assert.That(File.ReadAllText(_blockersPath), Is.EqualTo(".banner"));
        }
    }

    [Test]
    public void Чтение_для_скрипта_скрытия_не_возвращает_право_записи()
    {
        File.WriteAllText(_blockersPath, Selectors);
        var store = Store();

        using (Lock(_blockersPath))
        {
            store.LoadBlockersText();
        }

        var script = store.BuildHideClutterScript();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(script, Does.Contain(".promo"));

            Assert.That(store.TrySaveBlockersText(string.Empty), Is.False,
                "Текст в редакторе вырос из сорвавшегося чтения, а скрипт его не видел – право записи возвращает только чтение для редактора.");

            Assert.That(File.ReadAllText(_blockersPath), Is.EqualTo(Selectors));
        }
    }

    [TestCase(0.0)]
    [TestCase(1e-9)]
    [TestCase(-1e-9)]
    public void Сорвавшееся_чтение_масштаба_запрещает_перезапись_до_удачного_чтения(double drift)
    {
        File.WriteAllText(_zoomPath, "1.250");
        var store = Store();

        double zoom;

        using (Lock(_zoomPath))
        {
            zoom = store.LoadZoom();
        }

        store.SaveZoom(ChatDisplayStore.DefaultZoom + drift);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(zoom, Is.EqualTo(ChatDisplayStore.DefaultZoom));

            Assert.That(File.ReadAllText(_zoomPath), Is.EqualTo("1.250"),
                "Плитка применяет масштаб по умолчанию и пишет его обратно – без запрета сохранённый масштаб теряется на старте.");
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.LoadZoom(), Is.EqualTo(1.25));

            store.SaveZoom(1.5);
            Assert.That(File.ReadAllText(_zoomPath), Is.EqualTo("1.500"), "Удачное чтение возвращает право записи.");
        }
    }

    [Test]
    public void Масштаб_выбранный_после_сорвавшегося_чтения_сохраняется()
    {
        File.WriteAllText(_zoomPath, "1.250");
        var store = Store();

        using (Lock(_zoomPath))
        {
            store.LoadZoom();
        }

        store.SaveZoom(1.1);

        Assert.That(File.ReadAllText(_zoomPath), Is.EqualTo("1.100"),
            "Масштаб, отличный от подставленного по умолчанию, выбрал пользователь – запрет на него не распространяется.");
    }

    private static FileStream Lock(string path)
    {
        return new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private ChatDisplayStore Store()
    {
        return new(NullLogger<ChatDisplayStore>.Instance, _directory.FullName);
    }
}
