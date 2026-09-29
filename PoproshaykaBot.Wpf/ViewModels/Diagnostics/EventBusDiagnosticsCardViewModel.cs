using CommunityToolkit.Mvvm.Input;
using KeepShell.Diagnostics;
using PoproshaykaBot.Core.Diagnostics;
using PoproshaykaBot.Wpf.Infrastructure.Diagnostics;
using System.Windows.Input;

namespace PoproshaykaBot.Wpf.ViewModels.Diagnostics;

public sealed partial class EventBusDiagnosticsCardViewModel(DiagnosticsSnapshotPublisher publisher)
    : DiagnosticsCardViewModel(publisher)
{
    private const int TypeRows = 10;

    private const int CollapsedTypeRows = 4;

    private bool _expanded;

    private int _typeCount;

    private int _totalTypeCount;

    public override string Title => "Шина событий";

    public override ICommand? Command => _typeCount > CollapsedTypeRows ? ToggleTypesCommand : null;

    public override string? CommandCaption => _expanded
        ? "Показать меньше"
        : _totalTypeCount > _typeCount
            ? $"Показать {_typeCount} самых частых из {_totalTypeCount}"
            : $"Показать все типы ({_typeCount})";

    protected override DiagnosticsCardContent Build(DiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Bus is not { } bus)
        {
            SetTypeCount(0, 0);

            return new(DiagnosticsCardState.Unknown,
            [
                new("Публикаций", "нет данных")
                {
                    Hint = "Счётчики шины не ответили на опрос",
                },
            ]);
        }

        var rows = new List<DiagnosticsCardRow>
        {
            new("Публикаций", DiagnosticsFormat.Number(bus.PublishedTotal))
            {
                Hint = "Считаются и события без единого подписчика",
            },
            new("Сбоев обработчиков", DiagnosticsFormat.Number(bus.HandlerFailures))
            {
                Hint = "Обработчик бросил исключение; соседние обработчики того же события при этом отработали",
            },
            new("Продолжения", $"запущено {DiagnosticsFormat.Number(bus.ContinuationsStarted)}, упало {DiagnosticsFormat.Number(bus.ContinuationFailures)}")
            {
                Hint = "Работа, поставленная через ContinueAfterPublish: она идёт после всех обработчиков публикации",
            },
        };

        AppendTypes(rows, bus, snapshot.CapturedAt);

        var state = bus.HandlerFailures > 0 || bus.ContinuationFailures > 0
            ? DiagnosticsCardState.Warning
            : DiagnosticsCardState.Ok;

        return new(state, rows);
    }

    private void AppendTypes(List<DiagnosticsCardRow> rows, EventBusStatistics bus, DateTimeOffset capturedAt)
    {
        var available = Math.Min(bus.ByType.Count, TypeRows);

        SetTypeCount(available, bus.ByType.Count);

        if (available == 0)
        {
            return;
        }

        var shown = _expanded ? available : Math.Min(available, CollapsedTypeRows);

        rows.Add(new("Тип события", string.Empty)
        {
            Columns = ["всего", "сбои", "макс."],
            IsHeader = true,
            Hint = Explain(bus.ByType.Count, shown),
        });

        foreach (var type in bus.ByType.Take(shown))
        {
            rows.Add(new(type.EventType, string.Empty)
            {
                Columns =
                [
                    DiagnosticsFormat.Number(type.PublishCount),
                    DiagnosticsFormat.Number(type.HandlerFailureCount),
                    DiagnosticsFormat.Milliseconds(type.MaxDuration),
                ],
                Hint = $"публикаций {DiagnosticsFormat.Number(type.PublishCount)}, "
                    + $"сбоев обработчиков {DiagnosticsFormat.Number(type.HandlerFailureCount)}, "
                    + $"дольше всего {DiagnosticsFormat.Milliseconds(type.MaxDuration)}; "
                    + $"последняя публикация {DiagnosticsFormat.Ago(type.LastPublishedAt, capturedAt)}, "
                    + $"суммарно {DiagnosticsFormat.Milliseconds(type.TotalDuration)}",
            });
        }
    }

    private static string Explain(int total, int shown)
    {
        var columns = "Публикаций всего, сбоев обработчиков и самая долгая публикация";

        return total > shown
            ? $"{columns}. Показаны {shown} самых частых типов из {total}"
            : columns;
    }

    private void SetTypeCount(int count, int total)
    {
        if (_typeCount == count && _totalTypeCount == total)
        {
            return;
        }

        _typeCount = count;
        _totalTypeCount = total;

        OnPropertyChanged(nameof(Command));
        OnPropertyChanged(nameof(CommandCaption));
    }

    [RelayCommand]
    private void ToggleTypes()
    {
        _expanded = !_expanded;

        OnPropertyChanged(nameof(CommandCaption));

        Rebuild();
    }
}
