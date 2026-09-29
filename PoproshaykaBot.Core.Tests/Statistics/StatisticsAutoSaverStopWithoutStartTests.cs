using PoproshaykaBot.Core.Statistics;

namespace PoproshaykaBot.Core.Tests.Statistics;

[TestFixture]
public sealed class StatisticsAutoSaverStopWithoutStartTests
{
    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PoproshaykaBot-AutoSaverTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    private string _tempDir = string.Empty;

    [Test]
    public async Task StopAsync_WithoutStartAsync_DoesNotOverwriteExistingFile()
    {
        var statsFile = Path.Combine(_tempDir, "users_statistics.json");
        const string PreservedContent = """
                                        [
                                          {
                                            "userId": "u1",
                                            "name": "Alice",
                                            "messageCount": 100,
                                            "bonusPoints": 5,
                                            "penaltyPoints": 2,
                                            "firstSeen": "2024-01-01T00:00:00Z",
                                            "lastSeen": "2024-01-02T00:00:00Z"
                                          }
                                        ]
                                        """;

        await File.WriteAllTextAsync(statsFile, PreservedContent);

        var userRepo = new UserStatisticsRepository(NullLogger<UserStatisticsRepository>.Instance);
        var botRepo = new BotStatisticsRepository();
        var fileStore = new StatisticsFileStore(NullLogger<StatisticsFileStore>.Instance, _tempDir);
        var loader = new UserStatisticsLoader(userRepo, fileStore, NullLogger<UserStatisticsLoader>.Instance);
        var saver = new StatisticsAutoSaver(userRepo, botRepo, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        await saver.StopAsync(new Progress<string>(), CancellationToken.None);

        var afterStop = await File.ReadAllTextAsync(statsFile);

        Assert.That(afterStop, Is.EqualTo(PreservedContent),
            "StopAsync without preceding StartAsync must not overwrite the on-disk file with empty repository state");
    }

    [Test]
    public async Task DisposeAsync_WithoutStartAsync_DoesNotOverwriteExistingFile()
    {
        var statsFile = Path.Combine(_tempDir, "users_statistics.json");
        const string PreservedContent = """[{"userId":"u1","name":"Bob","messageCount":42,"firstSeen":"2024-01-01T00:00:00Z","lastSeen":"2024-01-02T00:00:00Z"}]""";
        await File.WriteAllTextAsync(statsFile, PreservedContent);

        var userRepo = new UserStatisticsRepository(NullLogger<UserStatisticsRepository>.Instance);
        var botRepo = new BotStatisticsRepository();
        var fileStore = new StatisticsFileStore(NullLogger<StatisticsFileStore>.Instance, _tempDir);
        var loader = new UserStatisticsLoader(userRepo, fileStore, NullLogger<UserStatisticsLoader>.Instance);
        var saver = new StatisticsAutoSaver(userRepo, botRepo, new(TimeProvider.System), loader, fileStore, NullLogger<StatisticsAutoSaver>.Instance);

        await saver.DisposeAsync();

        var afterDispose = await File.ReadAllTextAsync(statsFile);

        Assert.That(afterDispose, Is.EqualTo(PreservedContent),
            "DisposeAsync without preceding StartAsync must not overwrite the on-disk file");
    }
}
