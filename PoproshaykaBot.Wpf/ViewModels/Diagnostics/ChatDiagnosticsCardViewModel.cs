using KeepShell.Diagnostics;
using PoproshaykaBot.Core.Diagnostics;
using PoproshaykaBot.Wpf.Infrastructure.Diagnostics;

namespace PoproshaykaBot.Wpf.ViewModels.Diagnostics;

public sealed class ChatDiagnosticsCardViewModel(DiagnosticsSnapshotPublisher publisher)
    : DiagnosticsCardViewModel(publisher)
{
    public override string Title => "Чат";

    protected override DiagnosticsCardContent Build(DiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Chat is not { } chat)
        {
            return new(DiagnosticsCardState.Unknown,
            [
                new("Состояние", "нет данных")
                {
                    Hint = "Источник состояния чата не ответил на опрос",
                },
            ]);
        }

        return new(DiagnosticsFormat.ToCardState(chat.State),
        [
            new("Состояние", DiagnosticsFormat.Describe(chat.State))
            {
                Hint = chat.State == ConnectionState.Disconnected ? "Бот к чату не подключён" : null,
            },
            new("Канал", chat.Channel ?? DiagnosticsFormat.Unset),
            new("В канале с", DiagnosticsFormat.Ago(chat.JoinedAt, snapshot.CapturedAt))
            {
                Hint = DiagnosticsFormat.Moment(chat.JoinedAt),
            },
            new("Последнее сообщение", DiagnosticsFormat.Ago(chat.LastMessageAt, snapshot.CapturedAt))
            {
                Hint = DiagnosticsFormat.Moment(chat.LastMessageAt),
            },
        ]);
    }
}
