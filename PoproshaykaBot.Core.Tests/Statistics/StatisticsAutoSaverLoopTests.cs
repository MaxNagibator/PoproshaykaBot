using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Core.Tests.Statistics;

[TestFixture]
public sealed class StatisticsAutoSaverLoopTests
{
    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "poproshayka-autosave-loop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);

        _usersFilePath = Path.Combine(_directory, "users_statistics.json");
        _writeBlockerPath = _usersFilePath + ".tmp";

        var fileStore = new StatisticsFileStore(NullLogger<StatisticsFileStore>.Instance, _directory);

        _userRepository = Substitute.For<IUserStatisticsRepository>();
        _userRepository.HasChanges.Returns(true);
        _userRepository.CreateSnapshotAndMarkSaved().Returns(_ => new List<UserStatistics>());

        _autoSaver = new(_userRepository,
            Substitute.For<IBotStatisticsRepository>(),
            new(TimeProvider.System),
            new(_userRepository, fileStore, NullLogger<UserStatisticsLoader>.Instance),
            fileStore,
            NullLogger<StatisticsAutoSaver>.Instance,
            TimeSpan.FromMilliseconds(50));
    }

    [TearDown]
    public async Task TearDown()
    {
        if (Directory.Exists(_writeBlockerPath))
        {
            Directory.Delete(_writeBlockerPath);
        }

        try
        {
            await _autoSaver.DisposeAsync();
        }
        catch (InvalidOperationException)
        {
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static readonly IProgress<string> NullProgress = new Progress<string>(_ => { });
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    private string _directory = null!;
    private string _usersFilePath = null!;
    private string _writeBlockerPath = null!;
    private IUserStatisticsRepository _userRepository = null!;
    private StatisticsAutoSaver _autoSaver = null!;

    [Test]
    public async Task Отказ_записи_не_убивает_цикл_и_не_оставляет_обещание_следующего_запуска()
    {
        Directory.CreateDirectory(_writeBlockerPath);

        await _autoSaver.StartAsync(NullProgress, CancellationToken.None);

        await WaitForAsync(() => _autoSaver.LastError is not null, "цикл не отметил отказ записи в LastError");

        var frozenNextRunAt = _autoSaver.NextRunAt;

        Directory.Delete(_writeBlockerPath);

        await WaitForAsync(() => _autoSaver.LastError is null && File.Exists(_usersFilePath),
            "цикл не сохранил статистику после отказа – значит, он умер на первом же сбое");

        Assert.Multiple(() =>
        {
            Assert.That(_autoSaver.NextRunAt, Is.Not.Null);
            Assert.That(_autoSaver.NextRunAt, Is.GreaterThan(frozenNextRunAt));
            Assert.That(_autoSaver.LastRunAt, Is.Not.Null);
        });
    }

    private static async Task WaitForAsync(Func<bool> condition, string message)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail(message);
    }
}
