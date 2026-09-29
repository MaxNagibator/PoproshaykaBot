using KeepShell.Diagnostics;
using PoproshaykaBot.Core.Diagnostics;
using PoproshaykaBot.Wpf.Infrastructure.Diagnostics;

namespace PoproshaykaBot.Wpf.ViewModels.Diagnostics;

public sealed class QueueDiagnosticsCardViewModel(DiagnosticsSnapshotPublisher publisher)
    : DiagnosticsCardViewModel(publisher)
{
    private const double WarningShare = 0.8;

    private const int ErrorTextMax = 160;

    public override string Title => "Очередь и таймеры";

    protected override DiagnosticsCardContent Build(DiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.ChatQueue is null && snapshot.Jobs.Count == 0)
        {
            return new(DiagnosticsCardState.Unknown,
            [
                new("Очередь чата", "нет данных")
                {
                    Hint = "Ни очередь отправки, ни таймеры не ответили на опрос",
                },
            ]);
        }

        var rows = new List<DiagnosticsCardRow>();
        var state = AppendQueue(rows, snapshot);

        foreach (var job in snapshot.Jobs)
        {
            AppendJob(rows, job, snapshot.CapturedAt);

            if (job.LastError is not null)
            {
                state = DiagnosticsCardState.Error;
            }
        }

        return new(state, rows);
    }

    private static DiagnosticsCardState AppendQueue(List<DiagnosticsCardRow> rows, DiagnosticsSnapshot snapshot)
    {
        if (snapshot.ChatQueue is not { } queue)
        {
            rows.Add(new("Очередь чата", "нет данных"));

            return DiagnosticsCardState.Unknown;
        }

        var share = DiagnosticsFormat.Share(queue.Length, queue.Capacity);

        rows.Add(new("Очередь чата", $"{DiagnosticsFormat.Number(queue.Length)} из {DiagnosticsFormat.Number(queue.Capacity)}")
        {
            Hint = share is { } value
                ? $"{DiagnosticsFormat.Percent(value)} ёмкости; на полной очереди отправка ждёт места"
                : "Ёмкость очереди неизвестна",
            Fill = share,
        });

        rows.Add(new("Отправлено", DiagnosticsFormat.Number(queue.SentCount)));

        rows.Add(new("Сбоев отправки", DiagnosticsFormat.Number(queue.FailedCount))
        {
            Hint = "Сообщения, которые Twitch не принял; подробности – в журнале",
        });

        rows.Add(new("Последняя отправка", DiagnosticsFormat.Ago(queue.LastSentAt, snapshot.CapturedAt))
        {
            Hint = DiagnosticsFormat.Moment(queue.LastSentAt),
        });

        return share >= WarningShare || queue.FailedCount > 0
            ? DiagnosticsCardState.Warning
            : DiagnosticsCardState.Ok;
    }

    private static void AppendJob(List<DiagnosticsCardRow> rows, ScheduledJobStatus job, DateTimeOffset now)
    {
        var name = Describe(job.Job);

        rows.Add(new(name, DiagnosticsFormat.Until(job.NextRunAt, now))
        {
            Hint = Schedule(job, now),
        });

        if (job.LastError is not { } error)
        {
            return;
        }

        rows.Add(new($"{name}: ошибка", Shorten(error))
        {
            Hint = error,
        });
    }

    private static string Schedule(ScheduledJobStatus job, DateTimeOffset now)
    {
        var parts = new List<string>();

        if (job.NextRunAt is not null)
        {
            parts.Add($"в {DiagnosticsFormat.Moment(job.NextRunAt)}");
        }

        if (job.Interval is { } interval)
        {
            parts.Add($"интервал {DiagnosticsFormat.Duration(interval)}");
        }

        parts.Add($"последний запуск {DiagnosticsFormat.Ago(job.LastRunAt, now)}");

        return string.Join(", ", parts);
    }

    private static string Shorten(string error)
    {
        var text = error.ReplaceLineEndings(" ").Trim();

        return text.Length <= ErrorTextMax ? text : text[..ErrorTextMax] + "…";
    }

    private static string Describe(ScheduledJob job)
    {
        return job switch
        {
            ScheduledJob.Broadcast => "Следующая рассылка",
            ScheduledJob.StatisticsAutoSave => "Следующее автосохранение",
            _ => "Работа по расписанию",
        };
    }
}
