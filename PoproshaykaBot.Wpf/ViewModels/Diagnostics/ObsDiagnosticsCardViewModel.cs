using KeepShell.Diagnostics;
using PoproshaykaBot.Core.Diagnostics;
using PoproshaykaBot.Wpf.Infrastructure.Diagnostics;

namespace PoproshaykaBot.Wpf.ViewModels.Diagnostics;

public sealed class ObsDiagnosticsCardViewModel(DiagnosticsSnapshotPublisher publisher)
    : DiagnosticsCardViewModel(publisher)
{
    public override string Title => "OBS";

    protected override DiagnosticsCardContent Build(DiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Obs is not { } obs)
        {
            return new(DiagnosticsCardState.Unknown,
            [
                new("Состояние", "нет данных")
                {
                    Hint = "Источник состояния OBS не ответил на опрос",
                },
            ]);
        }

        return new(DiagnosticsFormat.ToCardState(obs.State),
        [
            new("Состояние", DiagnosticsFormat.Describe(obs.State))
            {
                Hint = obs.State switch
                {
                    ConnectionState.Disconnected => "Интеграция с OBS выключена или подключение ещё не состоялось",
                    ConnectionState.Unknown => "OBS ни разу не отвечал в этом запуске",
                    _ => null,
                },
            },
            new("Последний обмен", DiagnosticsFormat.Ago(obs.LastEventAt, snapshot.CapturedAt))
            {
                Hint = $"{DiagnosticsFormat.Moment(obs.LastEventAt)}; считается ответ OBS на запрос, а не его события",
            },
        ]);
    }
}
