using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Settings.Stores;

namespace PoproshaykaBot.Core.Tests.Broadcast.Profiles;

[TestFixture]
public class GameCategoryResolverTests
{
    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "recent-categories-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _searchApi = Substitute.For<ITwitchSearchApi>();
        _store = new(filePath: Path.Combine(_tempDir, "recent-categories.json"));
        _resolver = new(_searchApi, _store,
            NullLogger<GameCategoryResolver>.Instance);
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

    private string _tempDir = null!;
    private ITwitchSearchApi _searchApi = null!;
    private RecentCategoriesStore _store = null!;
    private GameCategoryResolver _resolver = null!;

    [Test]
    public async Task SearchAsync_ReturnsApiResults()
    {
        _searchApi.SearchCategoriesAsync("dota", 10, Arg.Any<CancellationToken>())
            .Returns([
                new("1", "Dota 2", ""),
                new("2", "DOTA", ""),
            ]);

        var results = await _resolver.SearchAsync("dota", CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Id, Is.EqualTo("1"));
    }

    [TestCase("tarkov", "2")]
    [TestCase("TARKOV", "2")]
    [TestCase("escape tarkov", "2")]
    [TestCase("tarot", "1")]
    [TestCase("tarkov!", "2")]
    [TestCase("tar", "1")]
    [TestCase("Escape from Tarkov: Arena", "3")]
    public async Task ResolveAsync_PicksMatchingName_NotFirstResult(string query, string expectedId)
    {
        _searchApi.SearchCategoriesAsync(query, 10, Arg.Any<CancellationToken>())
            .Returns([
                new("1", "Tarot", ""),
                new("2", "Escape from Tarkov", ""),
                new("3", "Escape from Tarkov: Arena", ""),
            ]);

        var result = await _resolver.ResolveAsync(query, CancellationToken.None);

        Assert.That(result.Match?.Id, Is.EqualTo(expectedId));
        Assert.That(_store.Load().Select(x => x.Id), Is.EqualTo(new[] { expectedId }));
    }

    [TestCase("tarkov", "Tarot")]
    [TestCase("tar", "StarCraft")]
    public async Task ResolveAsync_NoNameMatches_ReturnsCandidatesWithoutMatchAndRemembersNothing(string query, string candidate)
    {
        _searchApi.SearchCategoriesAsync(query, 10, Arg.Any<CancellationToken>())
            .Returns([new("1", candidate, "")]);

        var result = await _resolver.ResolveAsync(query, CancellationToken.None);

        Assert.That(result.Match, Is.Null);
        Assert.That(result.Candidates.Select(x => x.Name), Is.EqualTo(new[] { candidate }));
        Assert.That(_store.Load(), Is.Empty);
    }

    [Test]
    public async Task ResolveAsync_NoResults_ReturnsNoMatch()
    {
        _searchApi.SearchCategoriesAsync("xxx", 10, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _resolver.ResolveAsync("xxx", CancellationToken.None);

        Assert.That(result.Match, Is.Null);
        Assert.That(result.Candidates, Is.Empty);
    }

    [Test]
    public async Task RememberAsync_AddsToCache_AndLimitsTo20()
    {
        for (var i = 0; i < 25; i++)
        {
            await _resolver.RememberAsync(new(i.ToString(), $"Game {i}", ""));
        }

        var recents = _store.Load();
        Assert.That(recents, Has.Count.EqualTo(20));
        Assert.That(recents[0].Id, Is.EqualTo("24"));
    }

    [Test]
    public async Task RememberAsync_ExistingId_MovesToTop()
    {
        await _resolver.RememberAsync(new("A", "Game A", ""));
        await _resolver.RememberAsync(new("B", "Game B", ""));
        await _resolver.RememberAsync(new("A", "Game A", ""));

        var recents = _store.Load();
        Assert.That(recents, Has.Count.EqualTo(2));
        Assert.That(recents[0].Id, Is.EqualTo("A"));
    }
}
