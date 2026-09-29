using CommunityToolkit.Mvvm.ComponentModel;
using KeepShell.Diagnostics;
using PoproshaykaBot.Core.Diagnostics;
using PoproshaykaBot.Wpf.Infrastructure.Diagnostics;
using System.Windows.Input;

namespace PoproshaykaBot.Wpf.ViewModels.Diagnostics;

public abstract partial class DiagnosticsCardViewModel : ObservableObject, IDiagnosticsCard
{
    private readonly DiagnosticsSnapshotPublisher _publisher;

    private DiagnosticsSnapshot? _snapshot;

    [ObservableProperty]
    private DiagnosticsCardState _state = DiagnosticsCardState.Unknown;

    [ObservableProperty]
    private IReadOnlyList<DiagnosticsCardRow> _rows = [];

    protected DiagnosticsCardViewModel(DiagnosticsSnapshotPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);

        _publisher = publisher;
    }

    public abstract string Title { get; }

    public virtual ICommand? Command => null;

    public virtual string? CommandCaption => null;

    public void SetActive(bool active)
    {
        if (active)
        {
            _publisher.Subscribe(OnSnapshot);

            return;
        }

        _publisher.Unsubscribe(OnSnapshot);
    }

    protected abstract DiagnosticsCardContent Build(DiagnosticsSnapshot snapshot);

    protected void Rebuild()
    {
        if (_snapshot is { } snapshot)
        {
            Apply(snapshot);
        }
    }

    private void OnSnapshot(DiagnosticsSnapshot snapshot)
    {
        _snapshot = snapshot;

        Apply(snapshot);
    }

    private void Apply(DiagnosticsSnapshot snapshot)
    {
        var content = Build(snapshot);

        State = content.State;

        if (!SameRows(Rows, content.Rows))
        {
            Rows = content.Rows;
        }
    }

    private static bool SameRows(IReadOnlyList<DiagnosticsCardRow> current, IReadOnlyList<DiagnosticsCardRow> next)
    {
        if (current.Count != next.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Count; index++)
        {
            var left = current[index];
            var right = next[index];

            if (left != right with { Columns = left.Columns } || !left.Columns.SequenceEqual(right.Columns))
            {
                return false;
            }
        }

        return true;
    }
}
