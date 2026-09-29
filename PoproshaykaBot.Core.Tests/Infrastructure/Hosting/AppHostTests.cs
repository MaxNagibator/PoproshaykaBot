using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Tests.Server;

namespace PoproshaykaBot.Core.Tests.Infrastructure.Hosting;

[TestFixture]
public sealed class AppHostTests
{
    [Test]
    public void StartAsync_NoComponents_DoesNothing()
    {
        var host = new AppHost([], NullLogger<AppHost>.Instance);

        Assert.DoesNotThrowAsync(() => host.StartAsync(CancellationToken.None));
    }

    [Test]
    public async Task StartAsync_RunsComponentsInStartOrder()
    {
        var trace = new List<string>();
        var components = new IHostedComponent[]
        {
            new RecordingComponent("third", 300, trace),
            new RecordingComponent("first", 100, trace),
            new RecordingComponent("second", 200, trace),
        };

        var host = new AppHost(components, NullLogger<AppHost>.Instance);
        await host.StartAsync(CancellationToken.None);

        Assert.That(trace, Is.EqualTo(["start:first", "start:second", "start:third"]));
    }

    [Test]
    public async Task StopAsync_RunsComponentsInReverseOrder()
    {
        var trace = new List<string>();
        var components = new IHostedComponent[]
        {
            new RecordingComponent("a", 100, trace),
            new RecordingComponent("b", 200, trace),
            new RecordingComponent("c", 300, trace),
        };

        var host = new AppHost(components, NullLogger<AppHost>.Instance);
        await host.StartAsync(CancellationToken.None);
        trace.Clear();

        await host.StopAsync(CancellationToken.None);

        Assert.That(trace, Is.EqualTo(["stop:c", "stop:b", "stop:a"]));
    }

    [Test]
    public async Task StopAsync_OnlyStopsStartedComponents()
    {
        var trace = new List<string>();
        var failing = new RecordingComponent("failing", 200, trace) { ThrowOnStart = true };
        var components = new IHostedComponent[]
        {
            new RecordingComponent("a", 100, trace),
            failing,
            new RecordingComponent("c", 300, trace),
        };

        var host = new AppHost(components, NullLogger<AppHost>.Instance);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await host.StartAsync(CancellationToken.None));

        trace.Clear();
        await host.StopAsync(CancellationToken.None);

        Assert.That(trace, Is.EqualTo(["stop:a"]),
            "Only successfully-started components should be stopped; failed and never-started components must be skipped.");
    }

    [Test]
    public async Task StopAsync_ContinuesAfterComponentException()
    {
        var trace = new List<string>();
        var components = new IHostedComponent[]
        {
            new RecordingComponent("a", 100, trace),
            new RecordingComponent("b", 200, trace) { ThrowOnStop = true },
            new RecordingComponent("c", 300, trace),
        };

        var host = new AppHost(components, NullLogger<AppHost>.Instance);
        await host.StartAsync(CancellationToken.None);
        trace.Clear();

        await host.StopAsync(CancellationToken.None);

        Assert.That(trace, Does.Contain("stop:c"));
        Assert.That(trace, Does.Contain("stop:a"));
    }

    [Test]
    public async Task StartAsync_PassesProgressToComponents()
    {
        var reports = new List<string>();
        var progress = new SynchronousProgress<string>(reports.Add);

        var trace = new List<string>();
        var components = new IHostedComponent[]
        {
            new RecordingComponent("only", 100, trace),
        };

        var host = new AppHost(components, NullLogger<AppHost>.Instance);
        await host.StartAsync(progress, CancellationToken.None);

        Assert.That(reports, Does.Contain("only"));
    }

    [Test]
    public void StartAsync_RespectsCancellationToken()
    {
        var trace = new List<string>();
        var components = new IHostedComponent[]
        {
            new RecordingComponent("a", 100, trace),
            new RecordingComponent("b", 200, trace),
        };

        var host = new AppHost(components, NullLogger<AppHost>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(async () => await host.StartAsync(cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task StartAsync_AfterStop_RestartsComponents()
    {
        var trace = new List<string>();
        var components = new IHostedComponent[]
        {
            new RecordingComponent("a", 100, trace),
        };

        var host = new AppHost(components, NullLogger<AppHost>.Instance);
        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);
        trace.Clear();

        await host.StartAsync(CancellationToken.None);

        Assert.That(trace, Is.EqualTo(["start:a"]));
    }

    [Test]
    public void StartAsync_ComponentThrows_LogsComponentNameAndRethrows()
    {
        var trace = new List<string>();
        var logger = new RecordingLogger<AppHost>();
        var components = new IHostedComponent[]
        {
            new RecordingComponent("a", 100, trace),
            new RecordingComponent("падающий", 200, trace) { ThrowOnStart = true },
        };

        var host = new AppHost(components, logger);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await host.StartAsync(CancellationToken.None));

        var error = logger.Entries.SingleOrDefault(entry => entry.Level == LogLevel.Error);

        Assert.That(error, Is.Not.Null, "Падение компонента должно попадать в лог с уровнем Error");
        Assert.Multiple(() =>
        {
            Assert.That(error.Message, Does.Contain("падающий"));
            Assert.That(error.Exception, Is.InstanceOf<InvalidOperationException>());
        });
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value)
        {
            handler(value);
        }
    }

    private sealed class RecordingComponent(string name, int order, List<string> trace) : IHostedComponent
    {
        public string Name { get; } = name;

        public int StartOrder { get; } = order;

        public bool ThrowOnStart { get; init; }

        public bool ThrowOnStop { get; init; }

        public Task StartAsync(IProgress<string> progress, CancellationToken cancellationToken)
        {
            trace.Add($"start:{Name}");

            if (ThrowOnStart)
            {
                throw new InvalidOperationException($"Component {Name} failed to start");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(IProgress<string> progress, CancellationToken cancellationToken)
        {
            trace.Add($"stop:{Name}");

            if (ThrowOnStop)
            {
                throw new InvalidOperationException($"Component {Name} failed to stop");
            }

            return Task.CompletedTask;
        }
    }
}
