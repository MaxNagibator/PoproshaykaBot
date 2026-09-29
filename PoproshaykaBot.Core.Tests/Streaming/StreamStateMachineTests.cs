using PoproshaykaBot.Core.Streaming;

namespace PoproshaykaBot.Core.Tests.Streaming;

[TestFixture]
public sealed class StreamStateMachineTests
{
    private static StreamInfo SampleStream(string title = "Заголовок")
    {
        return new()
        {
            Id = "stream-1",
            UserId = "12345",
            UserLogin = "bobito217",
            UserName = "Bobito217",
            GameId = "509658",
            GameName = "Just Chatting",
            Title = title,
            Language = "ru",
            ViewerCount = 42,
            StartedAt = new(2026, 4, 30, 12, 0, 0, DateTimeKind.Utc),
            ThumbnailUrl = "https://example.com/{width}x{height}.jpg",
            Tags = ["Russian"],
            IsMature = false,
        };
    }

    [Test]
    public void NewMachine_StatusIsUnknown_StreamIsNull()
    {
        var machine = new StreamStateMachine();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(machine.CurrentStatus, Is.EqualTo(StreamStatus.Unknown));
            Assert.That(machine.CurrentStream, Is.Null);
        }
    }

    [Test]
    public void ApplyOnlineSnapshot_FromUnknown_TransitionsToOnline()
    {
        var machine = new StreamStateMachine();

        var transition = machine.ApplyOnlineSnapshot(SampleStream());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(transition.Transitioned, Is.True);
            Assert.That(transition.Previous, Is.EqualTo(StreamStatus.Unknown));
            Assert.That(machine.CurrentStatus, Is.EqualTo(StreamStatus.Online));
            Assert.That(machine.CurrentStream, Is.Not.Null);
        }
    }

    [Test]
    public void ApplyOnlineSnapshot_AlreadyOnline_DoesNotMarkTransition()
    {
        var machine = new StreamStateMachine();

        machine.ApplyOnlineSnapshot(SampleStream());
        var second = machine.ApplyOnlineSnapshot(SampleStream("Новый"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Transitioned, Is.False);
            Assert.That(second.Previous, Is.EqualTo(StreamStatus.Online));
            Assert.That(machine.CurrentStream!.Title, Is.EqualTo("Новый"));
        }
    }

    [Test]
    public void ApplyOnlineSnapshot_SameContent_DoesNotReportSnapshotChange()
    {
        var machine = new StreamStateMachine();

        machine.ApplyOnlineSnapshot(SampleStream());
        var second = machine.ApplyOnlineSnapshot(SampleStream());

        Assert.That(second.SnapshotChanged, Is.False,
            "повторный одинаковый снимок не должен превращаться в событие шины каждые 30 секунд");
    }

    [Test]
    public void ApplyOnlineSnapshot_MetadataFilledIn_ReportsSnapshotChange()
    {
        var machine = new StreamStateMachine();

        machine.MarkOnline("stream-1", new(2026, 4, 30, 12, 0, 0, DateTimeKind.Utc));
        var second = machine.ApplyOnlineSnapshot(SampleStream());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Transitioned, Is.False);
            Assert.That(second.SnapshotChanged, Is.True);
            Assert.That(machine.CurrentStream!.ThumbnailUrl, Is.EqualTo("https://example.com/{width}x{height}.jpg"));
        }
    }

    [Test]
    public void ApplyOnlineSnapshot_OnlyViewerCountChanged_DoesNotReportSnapshotChange()
    {
        var machine = new StreamStateMachine();

        machine.ApplyOnlineSnapshot(SampleStream());

        var next = SampleStream();
        next.ViewerCount += 17;

        var second = machine.ApplyOnlineSnapshot(next);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.SnapshotChanged, Is.False);
            Assert.That(machine.CurrentStream!.ViewerCount, Is.EqualTo(59),
                "счётчик зрителей обновляется в состоянии, но событием метаданных не считается");
        }
    }

    [Test]
    public void ApplyOffline_FromOnline_ReportsSnapshotChange()
    {
        var machine = new StreamStateMachine();

        machine.ApplyOnlineSnapshot(SampleStream());
        var offline = machine.ApplyOffline();
        var second = machine.ApplyOffline();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(offline.SnapshotChanged, Is.True);
            Assert.That(second.SnapshotChanged, Is.False);
        }
    }

    [Test]
    public void MarkOnline_WithoutEventPayload_LeavesStreamUntouched()
    {
        var machine = new StreamStateMachine();

        var transition = machine.MarkOnline(null, default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(transition.Transitioned, Is.True);
            Assert.That(machine.CurrentStatus, Is.EqualTo(StreamStatus.Online));
            Assert.That(machine.CurrentStream, Is.Null,
                "без id и started_at в нотификации stream подтянется retry-циклом");
        }
    }

    [Test]
    public void MarkOnline_WithEventPayload_PublishesCanonicalStart()
    {
        var machine = new StreamStateMachine();
        var startedAt = new DateTime(2026, 4, 30, 12, 0, 0, DateTimeKind.Utc);

        machine.MarkOnline("stream-7", startedAt);

        Assert.That(machine.CurrentStream, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(machine.CurrentStream!.Id, Is.EqualTo("stream-7"));
            Assert.That(machine.CurrentStream.StartedAt, Is.EqualTo(startedAt));
            Assert.That(machine.CurrentStream.StartedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }

    [Test]
    public void MarkOnline_ForStreamAlreadyKnownFromHelix_KeepsMetadata()
    {
        var machine = new StreamStateMachine();
        machine.ApplyOnlineSnapshot(SampleStream());

        machine.MarkOnline("stream-1", new(2026, 4, 30, 12, 0, 0, DateTimeKind.Utc));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(machine.CurrentStream!.Title, Is.EqualTo("Заголовок"));
            Assert.That(machine.CurrentStream.ViewerCount, Is.EqualTo(42));
        }
    }

    [Test]
    public void MarkOnline_ForDifferentStream_ReplacesStaleSnapshot()
    {
        var machine = new StreamStateMachine();
        machine.ApplyOnlineSnapshot(SampleStream());

        var newStart = new DateTime(2026, 5, 1, 18, 30, 0, DateTimeKind.Utc);
        machine.MarkOnline("stream-2", newStart);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(machine.CurrentStream!.Id, Is.EqualTo("stream-2"));
            Assert.That(machine.CurrentStream.StartedAt, Is.EqualTo(newStart));
            Assert.That(machine.CurrentStream.Title, Is.Empty,
                "метаданные прошлого стрима не должны переезжать на новый");
        }
    }

    [Test]
    public void ApplyOffline_FromOnline_ClearsStream()
    {
        var machine = new StreamStateMachine();

        machine.ApplyOnlineSnapshot(SampleStream());
        var transition = machine.ApplyOffline();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(transition.Transitioned, Is.True);
            Assert.That(transition.Previous, Is.EqualTo(StreamStatus.Online));
            Assert.That(machine.CurrentStatus, Is.EqualTo(StreamStatus.Offline));
            Assert.That(machine.CurrentStream, Is.Null);
        }
    }

    [Test]
    public void ProbeOfflineDivergence_FirstCallWhileOnline_IsPending()
    {
        var machine = new StreamStateMachine();
        machine.ApplyOnlineSnapshot(SampleStream());

        var nowUtc = new DateTime(2026, 4, 30, 12, 0, 0, DateTimeKind.Utc);
        var outcome = machine.ProbeOfflineDivergence(nowUtc, TimeSpan.FromMinutes(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome.Action, Is.EqualTo(OfflineProbeAction.Pending));
            Assert.That(machine.CurrentStatus, Is.EqualTo(StreamStatus.Online),
                "первое API-сообщение об офлайне не должно мгновенно сбрасывать локальный Online");
        }
    }

    [Test]
    public void ProbeOfflineDivergence_AfterThreshold_ForcesOffline()
    {
        var machine = new StreamStateMachine();
        machine.ApplyOnlineSnapshot(SampleStream());

        var startUtc = new DateTime(2026, 4, 30, 12, 0, 0, DateTimeKind.Utc);
        var threshold = TimeSpan.FromMinutes(2);

        var first = machine.ProbeOfflineDivergence(startUtc, threshold);
        var afterThreshold = machine.ProbeOfflineDivergence(startUtc.Add(threshold).AddSeconds(1), threshold);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Action, Is.EqualTo(OfflineProbeAction.Pending));
            Assert.That(afterThreshold.Action, Is.EqualTo(OfflineProbeAction.ForcedOffline));
            Assert.That(afterThreshold.Previous, Is.EqualTo(StreamStatus.Online));
            Assert.That(machine.CurrentStatus, Is.EqualTo(StreamStatus.Offline));
            Assert.That(machine.CurrentStream, Is.Null);
        }
    }

    [Test]
    public void ProbeOfflineDivergence_ResetByOnlineSnapshot_StartsFreshClock()
    {
        var machine = new StreamStateMachine();
        machine.ApplyOnlineSnapshot(SampleStream());

        var startUtc = new DateTime(2026, 4, 30, 12, 0, 0, DateTimeKind.Utc);
        var threshold = TimeSpan.FromMinutes(2);

        machine.ProbeOfflineDivergence(startUtc, threshold);
        machine.ApplyOnlineSnapshot(SampleStream());
        var afterReset = machine.ProbeOfflineDivergence(startUtc.AddMinutes(3), threshold);

        Assert.That(afterReset.Action, Is.EqualTo(OfflineProbeAction.Pending),
            "после успешного online-snapshot offline-probe должен начаться заново – иначе ложное forced-offline");
    }

    [Test]
    public void ProbeOfflineDivergence_StatusNotOnline_ReportsNotOnline()
    {
        var machine = new StreamStateMachine();

        var outcome = machine.ProbeOfflineDivergence(DateTime.UtcNow, TimeSpan.FromMinutes(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome.Action, Is.EqualTo(OfflineProbeAction.NotOnline));
            Assert.That(machine.CurrentStream, Is.Null);
        }
    }

    [Test]
    public void ApplyChannelUpdate_WithoutCurrentStream_StoresButReturnsFalse()
    {
        var machine = new StreamStateMachine();

        var applied = machine.ApplyChannelUpdate(new("Новый", "ru", "999", "Программирование", []));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Is.False);
            Assert.That(machine.CurrentStream, Is.Null);
        }
    }

    [Test]
    public void ApplyChannelUpdate_WithCurrentStream_OverlaysFields()
    {
        var machine = new StreamStateMachine();
        machine.ApplyOnlineSnapshot(SampleStream());

        var applied = machine.ApplyChannelUpdate(new("Новый", "en", "999", "Программирование", []));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Is.True);
            Assert.That(machine.CurrentStream!.Title, Is.EqualTo("Новый"));
            Assert.That(machine.CurrentStream.Language, Is.EqualTo("en"));
            Assert.That(machine.CurrentStream.GameId, Is.EqualTo("999"));
            Assert.That(machine.CurrentStream.GameName, Is.EqualTo("Программирование"));
            Assert.That(machine.CurrentStream.ViewerCount, Is.EqualTo(42),
                "ChannelUpdated не должен трогать поля, которыми не владеет (ViewerCount, Tags и т.п.)");
        }
    }

    [Test]
    public void StoredChannelUpdate_AppliedToFutureOnlineSnapshot()
    {
        var machine = new StreamStateMachine();
        machine.ApplyChannelUpdate(new("Свежий", "ru", "999", "Программирование", []));

        machine.ApplyOnlineSnapshot(SampleStream("Старый из Helix"));

        Assert.That(machine.CurrentStream!.Title, Is.EqualTo("Свежий"),
            "сохранённый channel.update должен перетирать лагающий Helix snapshot");
    }

    [Test]
    public void UpdateStreamSnapshot_AppliesStoredOverlay()
    {
        var machine = new StreamStateMachine();
        machine.MarkOnline(null, default);
        machine.ApplyChannelUpdate(new("Свежий", "ru", "999", "Программирование", []));

        machine.UpdateStreamSnapshot(SampleStream("Старый из Helix"));

        Assert.That(machine.CurrentStream!.Title, Is.EqualTo("Свежий"));
    }

    [Test]
    public void ResetToUnknown_ClearsEverythingIncludingChannelUpdate()
    {
        var machine = new StreamStateMachine();
        machine.ApplyOnlineSnapshot(SampleStream());
        machine.ApplyChannelUpdate(new("Свежий", "ru", "999", "Программирование", []));

        machine.ResetToUnknown();

        var nextSnapshot = SampleStream("Новый из Helix");
        machine.ApplyOnlineSnapshot(nextSnapshot);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(machine.CurrentStream!.Title, Is.EqualTo("Новый из Helix"),
                "после ResetToUnknown сохранённый ChannelUpdated не должен влиять на новые snapshot'ы");
        }
    }

    [Test]
    public void ForgetChannelUpdate_PreservesStatusButClearsOverlay()
    {
        var machine = new StreamStateMachine();
        machine.ApplyChannelUpdate(new("Старый", "ru", "999", "Игра", []));

        machine.ForgetChannelUpdate();
        machine.ApplyOnlineSnapshot(SampleStream("Из Helix"));

        Assert.That(machine.CurrentStream!.Title, Is.EqualTo("Из Helix"),
            "ForgetChannelUpdate должен забывать предыдущий update, чтобы новый session welcome начал с чистого листа");
    }
}
