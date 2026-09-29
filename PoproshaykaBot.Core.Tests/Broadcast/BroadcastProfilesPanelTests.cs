using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.WinForms.Forms.Broadcast;
using PoproshaykaBot.WinForms.Infrastructure.Di;

namespace PoproshaykaBot.Core.Tests.Broadcast;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class BroadcastProfilesPanelTests
{
    [Test]
    public void Upsert_AfterPanelSetup_ShouldPersistThroughManager()
    {
        var manager = CreateManager();
        var forms = Substitute.For<IFormFactory>();
        var bus = Substitute.For<IEventBus>();
        var streamStatus = Substitute.For<IStreamStatus>();
        var profilesStore = Substitute.For<BroadcastProfilesStore>(NullLogger<BroadcastProfilesStore>.Instance, null, null);
        profilesStore.Load().Returns(new BroadcastProfilesSettings());

        using var panel = new BroadcastProfilesPanel
        {
            Manager = manager,
            Forms = forms,
            Bus = bus,
            Profiles = profilesStore,
            Stream = streamStatus,
        };

        manager.Upsert(new() { Name = "Профиль 1" });

        Assert.That(manager.GetAll().Select(p => p.Name), Is.EquivalentTo(["Профиль 1"]));
    }

    [Test]
    public void TitleMatches_NoPlaceholder_LiteralEqualityWins()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BroadcastProfileStreamComparer.TitleMatches("Просто стрим", "Просто стрим"), Is.True);
            Assert.That(BroadcastProfileStreamComparer.TitleMatches("Просто стрим", "Другое"), Is.False);
        }
    }

    [Test]
    public void TitleMatches_WithPlaceholder_AnyDigitsAccepted()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BroadcastProfileStreamComparer.TitleMatches("Серия #{n}", "Серия #14"), Is.True);
            Assert.That(BroadcastProfileStreamComparer.TitleMatches("Серия #{n}", "Серия #1"), Is.True);
            Assert.That(BroadcastProfileStreamComparer.TitleMatches("Серия #{n}", "Серия #999"), Is.True);
        }
    }

    [Test]
    public void TitleMatches_WithPlaceholder_NonDigitsRejected()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BroadcastProfileStreamComparer.TitleMatches("Серия #{n}", "Серия #abc"), Is.False);
            Assert.That(BroadcastProfileStreamComparer.TitleMatches("Серия #{n}", "Другая #14"), Is.False);
        }
    }

    [Test]
    public void TitleMatches_RegexSpecialCharsInProfileTitle_TreatedAsLiterals()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BroadcastProfileStreamComparer.TitleMatches("a.b+c?d", "a.b+c?d"), Is.True);
            Assert.That(BroadcastProfileStreamComparer.TitleMatches("a.b+c?d", "axbXcYd"), Is.False);
        }
    }

    private static BroadcastProfilesManager CreateManager()
    {
        var logger = Substitute.For<ILogger<BroadcastProfilesManager>>();
        var busLogger = Substitute.For<ILogger<InMemoryEventBus>>();
        var bus = new InMemoryEventBus(busLogger);
        var tempPath = Path.Combine(Path.GetTempPath(), $"broadcast-profiles-panel-{Guid.NewGuid():N}.json");
        var store = new BroadcastProfilesStore(filePath: tempPath);
        var applier = Substitute.For<IChannelInformationApplier>();
        return new(store, applier, bus, TimeProvider.System, logger);
    }
}
