using KeepShell.Diagnostics;
using PoproshaykaBot.Core.Diagnostics;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Wpf.Infrastructure.Diagnostics;

namespace PoproshaykaBot.Wpf.ViewModels.Diagnostics;

public sealed class EventSubDiagnosticsCardViewModel(DiagnosticsSnapshotPublisher publisher)
    : DiagnosticsCardViewModel(publisher)
{
    public override string Title => "EventSub";

    protected override DiagnosticsCardContent Build(DiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.EventSub.Count == 0)
        {
            return new(DiagnosticsCardState.Unknown,
            [
                new("Сессии", "нет данных")
                {
                    Hint = "Ни одна сессия EventSub не отдала состояние",
                },
            ]);
        }

        var rows = new List<DiagnosticsCardRow>();
        var state = DiagnosticsCardState.Ok;

        foreach (var session in snapshot.EventSub)
        {
            var name = Describe(session.Role);

            rows.Add(new($"Сессия {name}", DiagnosticsFormat.Describe(session.State))
            {
                Hint = Attempts(session) ?? Explain(session.State),
            });

            rows.Add(new($"События {name}", DiagnosticsFormat.Ago(session.LastMessageAt, snapshot.CapturedAt))
            {
                Hint = DiagnosticsFormat.Moment(session.LastMessageAt),
            });

            state = DiagnosticsFormat.Worst(state, DiagnosticsFormat.ToCardState(session.State));
        }

        return new(state, rows);
    }

    private static string? Explain(ConnectionState state)
    {
        return state switch
        {
            ConnectionState.Unknown => "Сессия ни разу не отдавала состояние в этом запуске",
            ConnectionState.Disconnected => "Бот не подключён",
            _ => null,
        };
    }

    private static string? Attempts(EventSubStatus session)
    {
        if (session.ReconnectAttempt <= 0)
        {
            return null;
        }

        return session.ReconnectLimit > 0
            ? $"попытка {session.ReconnectAttempt} из {session.ReconnectLimit}"
            : $"попытка {session.ReconnectAttempt}";
    }

    private static string Describe(TwitchOAuthRole role)
    {
        return role switch
        {
            TwitchOAuthRole.Broadcaster => "канала",
            _ => "бота",
        };
    }
}
