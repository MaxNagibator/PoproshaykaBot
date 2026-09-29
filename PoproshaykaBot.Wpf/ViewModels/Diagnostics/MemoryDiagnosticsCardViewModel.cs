using KeepShell.Diagnostics;
using PoproshaykaBot.Core.Diagnostics;
using PoproshaykaBot.Wpf.Infrastructure.Diagnostics;
using ShellChildProcessUsage = KeepShell.Diagnostics.ChildProcessUsage;

namespace PoproshaykaBot.Wpf.ViewModels.Diagnostics;

public sealed class MemoryDiagnosticsCardViewModel : DiagnosticsCardViewModel
{
    private const double WarningShare = 0.8;

    private const int ChildRows = 3;

    private const string WebViewProcessName = "msedgewebview2";

    private readonly PerformanceMonitor _monitor;

    public MemoryDiagnosticsCardViewModel(DiagnosticsSnapshotPublisher publisher, PerformanceMonitor monitor)
        : base(publisher)
    {
        ArgumentNullException.ThrowIfNull(monitor);

        _monitor = monitor;
    }

    public override string Title => "Память";

    protected override DiagnosticsCardContent Build(DiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Memory is not { } memory)
        {
            _monitor.ReportChildProcesses(null);

            return new(DiagnosticsCardState.Unknown,
            [
                new("Замеры", "нет данных")
                {
                    Hint = "Сторож памяти не запущен – под ключом --ui-smoke он не создаётся вовсе, и мерить память некому",
                },
            ]);
        }

        _monitor.ReportChildProcesses(ToShellUsage(memory));

        var selfShare = DiagnosticsFormat.Share(memory.SelfBytes, memory.SelfThresholdBytes);
        var totalShare = DiagnosticsFormat.Share(memory.TotalBytes, memory.TotalThresholdBytes);

        var rows = new List<DiagnosticsCardRow>
        {
            new("Приложение", $"{DiagnosticsFormat.Size(memory.SelfBytes)} из {DiagnosticsFormat.Size(memory.SelfThresholdBytes)}")
            {
                Hint = Threshold(selfShare, memory.SelfThresholdBytes),
                Fill = selfShare,
            },
        };

        AppendChildren(rows, memory);

        rows.Add(new("С дочерними процессами",
            $"{DiagnosticsFormat.Size(memory.TotalBytes)} из {DiagnosticsFormat.Size(memory.TotalThresholdBytes)}")
        {
            Hint = Threshold(totalShare, memory.TotalThresholdBytes),
            Fill = totalShare,
        });

        rows.Add(new("Замер", DiagnosticsFormat.Ago(memory.MeasuredAt, snapshot.CapturedAt))
        {
            Hint = DiagnosticsFormat.Moment(memory.MeasuredAt),
        });

        return new(ResolveState(memory, selfShare, totalShare), rows);
    }

    private static string Threshold(double? share, long limit)
    {
        var percent = share is { } value ? DiagnosticsFormat.Percent(value) : DiagnosticsFormat.Unset;

        return $"{percent} порога {DiagnosticsFormat.Size(limit)}; за порогом сторож завершает процесс";
    }

    private static DiagnosticsCardState ResolveState(MemoryUsage memory, double? selfShare, double? totalShare)
    {
        if (memory.SelfBytes > memory.SelfThresholdBytes || memory.TotalBytes > memory.TotalThresholdBytes)
        {
            return DiagnosticsCardState.Error;
        }

        return selfShare >= WarningShare || totalShare >= WarningShare
            ? DiagnosticsCardState.Warning
            : DiagnosticsCardState.Ok;
    }

    private static void AppendChildren(List<DiagnosticsCardRow> rows, MemoryUsage memory)
    {
        if (memory.Children.Count == 0)
        {
            rows.Add(new("Дочерние процессы", "нет"));

            return;
        }

        var ordered = memory.Children.OrderByDescending(child => child.Bytes).ToList();

        rows.Add(new("Дочерние процессы", string.Empty)
        {
            Columns = ["память", "штук"],
            IsHeader = true,
            Hint = "Процессы одного имени сложены в одну строку: сколько они заняли и сколько их",
        });

        foreach (var child in ordered.Take(ChildRows))
        {
            var share = DiagnosticsFormat.Share(child.Bytes, memory.TotalThresholdBytes);

            rows.Add(new(DisplayName(child.Name), string.Empty)
            {
                Columns = Usage(child.Bytes, child.Count),
                Hint = Explain(child.Bytes, child.Count, share, memory.TotalThresholdBytes),
                Fill = share,
            });
        }

        var rest = ordered.Skip(ChildRows).ToList();

        if (rest.Count == 0)
        {
            return;
        }

        var restBytes = rest.Sum(child => child.Bytes);
        var restCount = rest.Sum(child => child.Count);

        rows.Add(new("Прочие процессы", string.Empty)
        {
            Columns = Usage(restBytes, restCount),
            Hint = Explain(restBytes, restCount, null, memory.TotalThresholdBytes),
        });
    }

    private static IReadOnlyList<string> Usage(long bytes, int count)
    {
        return [DiagnosticsFormat.Size(bytes), DiagnosticsFormat.Number(count)];
    }

    private static string Explain(long bytes, int count, double? share, long limit)
    {
        var usage = $"{DiagnosticsFormat.Size(bytes)}, {DiagnosticsFormat.Count(count, "процесс", "процесса", "процессов")}";

        return share is { } value
            ? $"{usage}; {DiagnosticsFormat.Percent(value)} общего порога {DiagnosticsFormat.Size(limit)}"
            : usage;
    }

    private static string DisplayName(string name)
    {
        return name.Contains(WebViewProcessName, StringComparison.OrdinalIgnoreCase) ? "WebView2" : name;
    }

    private static ShellChildProcessUsage ToShellUsage(MemoryUsage memory)
    {
        return new(memory.MeasuredAt.UtcDateTime,
            memory.ChildCount,
            memory.ChildBytes,
            [.. memory.Children.Select(child => new ChildProcessEntry(child.Name, child.Count, child.Bytes))]);
    }
}
