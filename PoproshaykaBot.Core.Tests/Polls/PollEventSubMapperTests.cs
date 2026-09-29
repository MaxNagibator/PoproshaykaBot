using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Twitch.Helix;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Polls;

[TestFixture]
public class PollEventSubMapperTests
{
    private static JsonElement Parse(string json)
    {
        return JsonDocument.Parse(json).RootElement;
    }

    [Test]
    public void FromEventSub_Begin_MapsActiveSnapshotWithoutVotes()
    {
        var json = Parse("""
                         {
                           "id": "poll-1",
                           "broadcaster_user_id": "1",
                           "broadcaster_user_login": "x",
                           "broadcaster_user_name": "X",
                           "title": "Вопрос?",
                           "choices": [
                             { "id": "c1", "title": "A" },
                             { "id": "c2", "title": "B" }
                           ],
                           "channel_points_voting": { "is_enabled": false, "amount_per_vote": 0 },
                           "started_at": "2026-04-24T10:00:00Z",
                           "ends_at": "2026-04-24T10:01:00Z"
                         }
                         """);

        var snapshot = PollEventSubMapper.FromEventSubBegin(json);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.PollId, Is.EqualTo("poll-1"));
            Assert.That(snapshot.Status, Is.EqualTo(PollSnapshotStatus.Active));
            Assert.That(snapshot.Choices, Has.Count.EqualTo(2));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Choices[0].Votes, Is.EqualTo(0));
            Assert.That(snapshot.Choices[0].ChoiceId, Is.EqualTo("c1"));
            Assert.That(snapshot.Choices[0].Title, Is.EqualTo("A"));
            Assert.That(snapshot.StartedAtUtc, Is.EqualTo(DateTime.Parse("2026-04-24T10:00:00Z").ToUniversalTime()));
            Assert.That(snapshot.EndsAtUtc, Is.EqualTo(DateTime.Parse("2026-04-24T10:01:00Z").ToUniversalTime()));
            Assert.That(snapshot.EndedAtUtc, Is.Null);
            Assert.That(snapshot.ChannelPointsVotingEnabled, Is.False);
        }
    }

    [Test]
    public void FromEventSub_Progress_MapsActiveSnapshotWithVotes()
    {
        var json = Parse("""
                         {
                           "id": "poll-1",
                           "broadcaster_user_id": "1",
                           "broadcaster_user_login": "x",
                           "broadcaster_user_name": "X",
                           "title": "Вопрос?",
                           "choices": [
                             { "id": "c1", "title": "A", "votes": 5, "channel_points_votes": 2, "bits_votes": 0 },
                             { "id": "c2", "title": "B", "votes": 3, "channel_points_votes": 0, "bits_votes": 0 }
                           ],
                           "channel_points_voting": { "is_enabled": true, "amount_per_vote": 100 },
                           "started_at": "2026-04-24T10:00:00Z",
                           "ends_at": "2026-04-24T10:01:00Z"
                         }
                         """);

        var snapshot = PollEventSubMapper.FromEventSubProgress(json);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Status, Is.EqualTo(PollSnapshotStatus.Active));
            Assert.That(snapshot.Choices[0].Votes, Is.EqualTo(5));
            Assert.That(snapshot.Choices[0].ChannelPointsVotes, Is.EqualTo(2));
            Assert.That(snapshot.ChannelPointsVotingEnabled, Is.True);
            Assert.That(snapshot.ChannelPointsPerVote, Is.EqualTo(100));
        }
    }

    [TestCase("completed", PollSnapshotStatus.Completed)]
    [TestCase("terminated", PollSnapshotStatus.Terminated)]
    [TestCase("archived", PollSnapshotStatus.Archived)]
    [TestCase("moderated", PollSnapshotStatus.Moderated)]
    [TestCase("invalid", PollSnapshotStatus.Invalid)]
    public void FromEventSub_End_MapsStatus(string jsonStatus, PollSnapshotStatus expected)
    {
        var json = Parse($$"""
                           {
                             "id": "poll-1",
                             "broadcaster_user_id": "1",
                             "title": "Вопрос?",
                             "choices": [
                               { "id": "c1", "title": "A", "votes": 5, "channel_points_votes": 0, "bits_votes": 0 },
                               { "id": "c2", "title": "B", "votes": 3, "channel_points_votes": 0, "bits_votes": 0 }
                             ],
                             "channel_points_voting": { "is_enabled": false, "amount_per_vote": 0 },
                             "status": "{{jsonStatus}}",
                             "started_at": "2026-04-24T10:00:00Z",
                             "ended_at": "2026-04-24T10:01:00Z"
                           }
                           """);

        var snapshot = PollEventSubMapper.FromEventSubEnd(json);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Status, Is.EqualTo(expected));
            Assert.That(snapshot.EndedAtUtc, Is.EqualTo(DateTime.Parse("2026-04-24T10:01:00Z").ToUniversalTime()));
        }
    }

    [Test]
    public void DetectWinner_SingleMax_ReturnsThatChoice()
    {
        var snapshot = MakeSnapshot(("A", 5), ("B", 3));
        var (winner, isTie) = PollEventSubMapper.DetectWinner(snapshot);

        Assert.That(winner, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(winner!.Title, Is.EqualTo("A"));
            Assert.That(isTie, Is.False);
        }
    }

    [Test]
    public void DetectWinner_Tie_ReturnsNullAndTrue()
    {
        var snapshot = MakeSnapshot(("A", 5), ("B", 5));
        var (winner, isTie) = PollEventSubMapper.DetectWinner(snapshot);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winner, Is.Null);
            Assert.That(isTie, Is.True);
        }
    }

    [Test]
    public void DetectWinner_AllZero_ReturnsNullAndFalse()
    {
        var snapshot = MakeSnapshot(("A", 0), ("B", 0));
        var (winner, isTie) = PollEventSubMapper.DetectWinner(snapshot);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(winner, Is.Null);
            Assert.That(isTie, Is.False);
        }
    }

    [Test]
    public void FromHelix_MapsStatus()
    {
        var helix = new HelixPollInfo("poll-1",
            "1",
            "Вопрос?",
            [new("c1", "A", 5, 0, 0), new("c2", "B", 3, 0, 0)],
            false,
            0,
            "ACTIVE",
            60,
            DateTime.Parse("2026-04-24T10:00:00Z").ToUniversalTime(),
            null);

        var snapshot = PollEventSubMapper.FromHelix(helix, null);

        Assert.That(snapshot, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Status, Is.EqualTo(PollSnapshotStatus.Active));
            Assert.That(snapshot.Choices, Has.Count.EqualTo(2));
            Assert.That(snapshot.EndsAtUtc, Is.EqualTo(snapshot.StartedAtUtc.AddSeconds(60)));
        }
    }

    [TestCase("ACTIVE", PollSnapshotStatus.Active)]
    [TestCase("TERMINATED", PollSnapshotStatus.Terminated)]
    [TestCase("SOMETHING_NEW", PollSnapshotStatus.Active)]
    public void FromCreatedPoll_UnknownStatusCountsAsActive(string status, PollSnapshotStatus expected)
    {
        var helix = new HelixPollInfo("poll-1",
            "1",
            "Вопрос?",
            [new("c1", "A", 0, 0, 0)],
            false,
            0,
            status,
            60,
            DateTime.Parse("2026-04-24T10:00:00Z").ToUniversalTime(),
            null);

        var snapshot = PollEventSubMapper.FromCreatedPoll(helix, Guid.NewGuid());

        Assert.That(snapshot.Status, Is.EqualTo(expected),
            "Только что созданный опрос активен по определению – незнакомый статус ответа не превращает запуск в отказ");
    }

    [TestCase("SOMETHING_NEW")]
    [TestCase("")]
    [TestCase(null)]
    public void FromHelix_UnknownStatus_ReturnsNull(string? status)
    {
        var helix = new HelixPollInfo("poll-1",
            "1",
            "Вопрос?",
            [new("c1", "A", 5, 0, 0)],
            false,
            0,
            status!,
            60,
            DateTime.Parse("2026-04-24T10:00:00Z").ToUniversalTime(),
            DateTime.Parse("2026-04-24T10:01:00Z").ToUniversalTime());

        Assert.That(PollEventSubMapper.FromHelix(helix, null), Is.Null);
    }

    [Test]
    public void FromHelix_CompletedWithEndedAt_SetsEndedAt()
    {
        var started = DateTime.Parse("2026-04-24T10:00:00Z").ToUniversalTime();
        var ended = DateTime.Parse("2026-04-24T10:01:00Z").ToUniversalTime();

        var helix = new HelixPollInfo("poll-1",
            "1",
            "Вопрос?",
            [new("c1", "A", 5, 0, 0)],
            false,
            0,
            "COMPLETED",
            60,
            started,
            ended);

        var snapshot = PollEventSubMapper.FromHelix(helix, null);

        Assert.That(snapshot, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Status, Is.EqualTo(PollSnapshotStatus.Completed));
            Assert.That(snapshot.EndedAtUtc, Is.EqualTo(ended));
        }
    }

    private static PollSnapshot MakeSnapshot(params (string title, int votes)[] choices)
    {
        var choiceList = choices
            .Select((c, i) => new PollChoiceSnapshot($"c{i}", c.title, c.votes, 0, 0))
            .ToArray();

        return new("poll-1",
            null,
            "Вопрос?",
            choiceList,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            false,
            0,
            PollSnapshotStatus.Completed,
            DateTime.UtcNow);
    }
}
