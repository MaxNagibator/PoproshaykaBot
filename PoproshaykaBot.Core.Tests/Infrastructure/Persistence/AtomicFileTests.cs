using PoproshaykaBot.Core.Infrastructure.Persistence;
using PoproshaykaBot.Core.Tests.Server;

namespace PoproshaykaBot.Core.Tests.Infrastructure.Persistence;

[TestFixture]
public sealed class AtomicFileTests
{
    [SetUp]
    public void SetUp()
    {
        _workDir = Path.Combine(Path.GetTempPath(), "AtomicFileTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
        _targetPath = Path.Combine(_workDir, "data.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (!Directory.Exists(_workDir))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(_workDir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(_workDir, true);
    }

    private string _workDir = null!;
    private string _targetPath = null!;

    [Test]
    public void Save_NewFile_CreatesFileWithContent()
    {
        AtomicFile.Save(_targetPath, "{\"hello\":\"world\"}");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(_targetPath), Is.True);
            Assert.That(File.ReadAllText(_targetPath), Is.EqualTo("{\"hello\":\"world\"}"));
        }
    }

    [Test]
    public void Save_NewFile_DoesNotCreateBackup()
    {
        AtomicFile.Save(_targetPath, "first");

        Assert.That(File.Exists(_targetPath + ".bak"), Is.False,
            "First write must not create a .bak – there is no previous content to back up.");
    }

    [Test]
    public void Save_OverExistingFile_KeepsBackupOfPrevious()
    {
        File.WriteAllText(_targetPath, "previous");

        AtomicFile.Save(_targetPath, "current");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(_targetPath), Is.EqualTo("current"));
            Assert.That(File.Exists(_targetPath + ".bak"), Is.True);
            Assert.That(File.ReadAllText(_targetPath + ".bak"), Is.EqualTo("previous"));
        }
    }

    [Test]
    public void Save_OverExistingFileTwice_KeepsLastGoodVersionInBackup()
    {
        File.WriteAllText(_targetPath, "v1");

        AtomicFile.Save(_targetPath, "v2");
        AtomicFile.Save(_targetPath, "v3");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(_targetPath), Is.EqualTo("v3"));
            Assert.That(File.ReadAllText(_targetPath + ".bak"), Is.EqualTo("v2"),
                ".bak must preserve the *last good* version so a crash rolls back to v{N-1}, not to the original baseline.");
        }
    }

    [Test]
    public void Save_WhenTheSwapFails_LeavesTheLastGoodContentReadable()
    {
        AtomicFile.Save(_targetPath, "v1");
        AtomicFile.Save(_targetPath, "v2");

        Directory.CreateDirectory(_targetPath + ".old");

        var logger = new RecordingLogger<AtomicFileTests>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => AtomicFile.Save(_targetPath, "v3", logger), Throws.InstanceOf<IOException>());

            Assert.That(File.ReadAllText(_targetPath), Is.EqualTo("v2"),
                "Сорвавшаяся запись обязана вернуть файл из .bak: читателю достаётся последняя хорошая версия, а не обрывок.");

            Assert.That(File.ReadAllText(_targetPath + ".bak"), Is.EqualTo("v2"));

            Assert.That(logger.Entries.Any(entry => entry.Message.Contains("восстановлено из бэкапа")), Is.True,
                "Подмена – единственная фаза, где целевой файл уже мог измениться, и откат из .bak обязан остаться именно здесь.");
        }
    }

    [TestCaseSource(nameof(PreSwapFailures))]
    public void Бросок_до_подмены_не_кладёт_резервную_копию_поверх_целевого_файла(Action<string> arrangeFailure)
    {
        AtomicFile.Save(_targetPath, "v1");
        AtomicFile.Save(_targetPath, "v2");

        arrangeFailure(_targetPath);

        var logger = new RecordingLogger<AtomicFileTests>();

        Assert.Catch(() => AtomicFile.Save(_targetPath, "v3", logger),
            "Сорвавшаяся запись обязана дойти до вызывающего, а не быть проглоченной откатом.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(_targetPath), Is.EqualTo("v2"),
                "До File.Replace целевой файл не тронут, и откат из .bak откатил бы данные на поколение назад.");

            Assert.That(File.ReadAllText(_targetPath + ".bak"), Is.EqualTo("v1"),
                ".bak прошлой удачной записи остаётся как был – он старше целевого файла.");

            Assert.That(logger.Entries, Is.Empty,
                "Восстанавливать нечего, поэтому о восстановлении не сообщается и оно не пробуется.");
        }
    }

    [Test]
    public void Бросок_без_целевого_файла_не_создаёт_его_из_чужого_бэкапа()
    {
        var backupPath = _targetPath + ".bak";
        File.WriteAllText(backupPath, "чужое");
        Directory.CreateDirectory(_targetPath);

        var logger = new RecordingLogger<AtomicFileTests>();

        Assert.Catch(() => AtomicFile.Save(_targetPath, "v1", logger),
            "Сорвавшийся File.Move обязан дойти до вызывающего.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(_targetPath), Is.False,
                "Целевого файла не было – восстанавливать нечего по определению.");

            Assert.That(File.ReadAllText(backupPath), Is.EqualTo("чужое"));

            Assert.That(logger.Entries, Is.Empty,
                "Попытка отката в этой ветке значит, что .bak чужой жизни файла лёг бы на его место.");
        }
    }

    private static IEnumerable<TestCaseData> PreSwapFailures()
    {
        yield return new TestCaseData((Action<string>)(target => Directory.CreateDirectory(target + ".tmp")))
            .SetName("Бросок_на_записи_временного_файла");

        yield return new TestCaseData((Action<string>)(target => File.SetAttributes(target + ".bak", FileAttributes.ReadOnly)))
            .SetName("Бросок_на_копировании_резервной_копии");
    }

    [TestCase(true, TestName = "Запись_без_откатной_копии_убирает_её_после_удавшейся_подмены")]
    [TestCase(false, TestName = "Запись_без_откатной_копии_убирает_bak_прежней_жизни_файла")]
    public void Запись_без_откатной_копии_не_оставляет_bak(bool targetExists)
    {
        if (targetExists)
        {
            AtomicFile.Save(_targetPath, "v1");
            AtomicFile.Save(_targetPath, "v2");
        }
        else
        {
            File.WriteAllText(_targetPath + ".bak", "чужое");
        }

        AtomicFile.Save(_targetPath, "v3", keepBackup: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(_targetPath), Is.EqualTo("v3"));

            Assert.That(File.Exists(_targetPath + ".bak"), Is.False,
                "У файла с секретами копия прежнего содержимого нужна только на время подмены.");

            Assert.That(File.Exists(_targetPath + ".old"), Is.False);
        }
    }

    [Test]
    public void Сорвавшаяся_подмена_без_откатной_копии_всё_равно_откатывается_из_неё()
    {
        AtomicFile.Save(_targetPath, "v1");
        Directory.CreateDirectory(_targetPath + ".old");

        Assert.That(() => AtomicFile.Save(_targetPath, "v2", keepBackup: false), Throws.InstanceOf<IOException>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(_targetPath), Is.EqualTo("v1"),
                "Копия убирается только после удачи – откат на сорвавшемся File.Replace тот же, что у остальных файлов.");

            Assert.That(File.ReadAllText(_targetPath + ".bak"), Is.EqualTo("v1"),
                "На сорвавшейся подмене копия остаётся: если откат сам не удался, она единственная целая версия.");
        }
    }

    [Test]
    public void Save_DoesNotLeaveTempFile()
    {
        AtomicFile.Save(_targetPath, "content");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(_targetPath + ".tmp"), Is.False);
            Assert.That(File.Exists(_targetPath + ".old"), Is.False);
        }
    }

    [Test]
    public void Save_CreatesMissingDirectories()
    {
        var nestedPath = Path.Combine(_workDir, "sub", "deeper", "file.json");

        AtomicFile.Save(nestedPath, "content");

        Assert.That(File.Exists(nestedPath), Is.True);
    }

    [Test]
    public void Save_NullArguments_Throws()
    {
        Assert.Throws<ArgumentException>(() => AtomicFile.Save("", "content"));
        Assert.Throws<ArgumentNullException>(() => AtomicFile.Save(null!, "content"));
        Assert.Throws<ArgumentNullException>(() => AtomicFile.Save(_targetPath, (string)null!));
        Assert.Throws<ArgumentNullException>(() => AtomicFile.Save(_targetPath, (byte[])null!));
        Assert.Throws<ArgumentNullException>(() => AtomicFile.Save(_targetPath, (Action<string>)null!));
        Assert.Throws<ArgumentException>(() => AtomicFile.Save("", _ => { }));
    }

    [Test]
    public void Save_Bytes_WritesWholeFile_AndReplacesPreviousContent()
    {
        AtomicFile.Save(_targetPath, "старое"u8.ToArray());
        AtomicFile.Save(_targetPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllBytes(_targetPath), Is.EqualTo(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
            Assert.That(File.Exists(_targetPath + ".tmp"), Is.False);
        }
    }

    [Test]
    public void Save_PassesLoggerThrough_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => AtomicFile.Save(_targetPath, "content", NullLogger.Instance));
    }
}
