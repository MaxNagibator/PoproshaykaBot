using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Chat.Commands;

namespace PoproshaykaBot.Core.Tests.Chat.Commands;

[TestFixture]
public class GameCommandTests
{
    [SetUp]
    public void SetUp()
    {
        _applier = Substitute.For<IChannelInformationApplier>();
        _resolver = Substitute.For<IGameCategoryResolver>();

        _command = new(_applier, _resolver, NullLogger<GameCommand>.Instance);
    }

    private IChannelInformationApplier _applier = null!;
    private IGameCategoryResolver _resolver = null!;
    private GameCommand _command = null!;

    [Test]
    public void CanExecute_OnlyForBroadcasterOrMod()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.CanExecute(new()
            {
                IsBroadcaster = true,
            }), Is.True);

            Assert.That(_command.CanExecute(new()), Is.False);
        }
    }

    [Test]
    public async Task Execute_NoArgs_ReturnsUsage()
    {
        var response = await _command.ExecuteAsync(new()
        {
            IsBroadcaster = true,
            Arguments = [],
        }, CancellationToken.None);

        Assert.That(response!.Text, Does.Contain("!game"));
    }

    [Test]
    public async Task Execute_GameFound_AppliesPatchAndReturnsConfirmation()
    {
        _resolver.ResolveAsync("dota", Arg.Any<CancellationToken>())
            .Returns(new GameCategoryResolution(new("1", "Dota 2", ""), [new("1", "Dota 2", "")]));

        var response = await _command.ExecuteAsync(new()
        {
            IsBroadcaster = true,
            MessageId = "m1",
            Arguments = ["dota"],
        }, CancellationToken.None);

        await _applier.Received(1)
            .ApplyPatchAsync(null,
                "1",
                "Dota 2",
                Arg.Any<CancellationToken>());

        Assert.That(response!.Text, Does.Contain("Dota 2"));
    }

    [Test]
    public async Task Execute_GameNotFound_DoesNotCallApplier()
    {
        _resolver.ResolveAsync("unknown", Arg.Any<CancellationToken>())
            .Returns(new GameCategoryResolution(null, []));

        var response = await _command.ExecuteAsync(new()
        {
            IsBroadcaster = true,
            MessageId = "m1",
            Arguments = ["unknown"],
        }, CancellationToken.None);

        await _applier.DidNotReceiveWithAnyArgs().ApplyPatchAsync(null, null, null, CancellationToken.None);
        Assert.That(response!.Text, Does.Contain("unknown"));
    }

    [Test]
    public async Task Execute_OnlyUnrelatedCandidates_DoesNotCallApplierAndNamesThem()
    {
        _resolver.ResolveAsync("tarkov", Arg.Any<CancellationToken>())
            .Returns(new GameCategoryResolution(null, [new("1", "Tarot", ""), new("2", "Tarkan", "")]));

        var response = await _command.ExecuteAsync(new()
        {
            IsBroadcaster = true,
            MessageId = "m1",
            Arguments = ["tarkov"],
        }, CancellationToken.None);

        await _applier.DidNotReceiveWithAnyArgs().ApplyPatchAsync(null, null, null, CancellationToken.None);
        Assert.That(response!.Text, Does.Contain("tarkov").And.Contain("Tarot, Tarkan"));
    }
}
