using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Update;

namespace PoproshaykaBot.Core.Tests.Settings.Stores;

[TestFixture]
public sealed class JsonStoreTests
{
    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("json-store");
        _filePath = Path.Combine(_directory.FullName, "update.json");
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Delete(recursive: true);
    }

    private DirectoryInfo _directory = null!;
    private string _filePath = null!;

    [Test]
    public void Save_WhenTheWriteFails_KeepsTheCacheOnTheLastVersionThatReachedDisk()
    {
        var store = CreateStore();
        store.Save(new() { CheckIntervalHours = 3 });

        BlockTheNextWrite();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => store.Save(new() { CheckIntervalHours = 9 }), Throws.InstanceOf<IOException>());

            Assert.That(store.Load().CheckIntervalHours, Is.EqualTo(3),
                "Кэш обязан отставать от несостоявшейся записи, иначе следующее удачное сохранение затрёт целый файл неподтверждёнными данными.");

            Assert.That(File.ReadAllText(_filePath), Does.Contain("\"checkIntervalHours\": 3"));
        }
    }

    [Test]
    public void Mutate_WhenTheWriteFails_KeepsTheCacheOnTheLastVersionThatReachedDisk()
    {
        var store = CreateStore();
        store.Save(new() { CheckIntervalHours = 3 });

        BlockTheNextWrite();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => store.Mutate(settings => { settings.CheckIntervalHours = 9; }),
                Throws.InstanceOf<IOException>());

            Assert.That(store.Load().CheckIntervalHours, Is.EqualTo(3));
            Assert.That(File.ReadAllText(_filePath), Does.Contain("\"checkIntervalHours\": 3"));
        }
    }

    [Test]
    public void Mutate_WhenTheMutatorThrows_LeavesTheCacheAndTheFileUntouched()
    {
        var store = CreateStore();
        store.Save(new() { CheckIntervalHours = 3 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => store.Mutate<bool>(settings =>
                {
                    settings.CheckIntervalHours = 9;
                    throw new InvalidOperationException("отказ валидации внутри мутатора");
                }),
                Throws.InstanceOf<InvalidOperationException>());

            Assert.That(store.Load().CheckIntervalHours, Is.EqualTo(3));
            Assert.That(File.ReadAllText(_filePath), Does.Contain("\"checkIntervalHours\": 3"));
        }
    }

    [Test]
    public void Save_WhenTheWriteFails_RaisesWriteFailedBeforeRethrowing()
    {
        var store = CreateStore();
        store.Save(new() { CheckIntervalHours = 3 });

        BlockTheNextWrite();

        JsonStoreWriteFailedEventArgs? reported = null;
        store.WriteFailed += (_, args) => reported = args;

        Assert.That(() => store.Save(new() { CheckIntervalHours = 9 }), Throws.InstanceOf<IOException>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported?.FilePath, Is.EqualTo(_filePath));
            Assert.That(reported?.Exception, Is.InstanceOf<IOException>());
        }
    }

    [Test]
    public void Save_WhenTheWriteSucceeds_RaisesNothing()
    {
        var store = CreateStore();
        var raised = 0;
        store.WriteFailed += (_, _) => raised++;

        store.Save(new() { CheckIntervalHours = 9 });

        Assert.That(raised, Is.Zero);
    }

    [Test]
    public void Save_WhenASubscriberThrows_StillReportsTheOriginalWriteFailure()
    {
        var store = CreateStore();
        store.Save(new() { CheckIntervalHours = 3 });

        BlockTheNextWrite();
        store.WriteFailed += (_, _) => throw new InvalidOperationException("подписчик упал");

        Assert.That(() => store.Save(new() { CheckIntervalHours = 9 }), Throws.InstanceOf<IOException>());
    }

    [TestCase("{ не json }")]
    [TestCase("")]
    public void Ctor_CorruptFile_BacksItUpAndFallsBackToDefaults(string content)
    {
        File.WriteAllText(_filePath, content);

        var store = CreateStore();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.Load().CheckIntervalHours, Is.EqualTo(new UpdateSettings().CheckIntervalHours));

            Assert.That(Directory.GetFiles(_directory.FullName, "update.invalid-*.json"), Has.Length.EqualTo(1),
                "Битый файл обязан уехать в бэкап до того, как его затрут дефолты.");
        }
    }

    [Test]
    public void Ctor_CorruptFile_PassesTheRawTextThroughTheRedactor()
    {
        File.WriteAllText(_filePath, "{ \"skippedVersion\": секрет }");

        _ = new JsonStore<UpdateSettings>(_filePath,
            NullLogger<JsonStoreTests>.Instance,
            raw => raw.Replace("секрет", "***", StringComparison.Ordinal));

        var backup = Directory.GetFiles(_directory.FullName, "update.invalid-*.json").Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.ReadAllText(backup), Does.Contain("***"));
            Assert.That(File.ReadAllText(backup), Does.Not.Contain("секрет"));
        }
    }

    [Test]
    public void MutateIf_WhenTheMutatorDeclines_TouchesNeitherDiskNorCache()
    {
        var store = CreateStore();
        store.Save(new() { CheckIntervalHours = 3 });

        BlockTheNextWrite();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.MutateIf(settings =>
                {
                    settings.CheckIntervalHours = 9;
                    return false;
                }),
                Is.False);

            Assert.That(store.Load().CheckIntervalHours, Is.EqualTo(3),
                "Отклонённая мутация не должна доезжать ни до файла, ни до кэша: запись здесь заблокирована, и отсутствие исключения это и доказывает.");
        }
    }

    [Test]
    public void MutateIf_WhenTheMutatorAccepts_WritesLikeAnOrdinaryMutation()
    {
        var store = CreateStore();
        store.Save(new() { CheckIntervalHours = 3 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.MutateIf(settings =>
                {
                    settings.CheckIntervalHours = 9;
                    return true;
                }),
                Is.True);

            Assert.That(store.Load().CheckIntervalHours, Is.EqualTo(9));
            Assert.That(File.ReadAllText(_filePath), Does.Contain("\"checkIntervalHours\": 9"));
        }
    }

    private JsonStore<UpdateSettings> CreateStore()
    {
        return new(_filePath, NullLogger<JsonStoreTests>.Instance);
    }

    private void BlockTheNextWrite()
    {
        Directory.CreateDirectory(_filePath + ".old");
    }
}
