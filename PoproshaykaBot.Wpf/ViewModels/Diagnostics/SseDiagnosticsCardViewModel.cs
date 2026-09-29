using KeepShell.Diagnostics;
using PoproshaykaBot.Core.Diagnostics;
using PoproshaykaBot.Wpf.Infrastructure.Diagnostics;

namespace PoproshaykaBot.Wpf.ViewModels.Diagnostics;

public sealed class SseDiagnosticsCardViewModel(DiagnosticsSnapshotPublisher publisher)
    : DiagnosticsCardViewModel(publisher)
{
    public override string Title => "Веб-сервер";

    protected override DiagnosticsCardContent Build(DiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Sse is not { } sse)
        {
            return new(DiagnosticsCardState.Unknown,
            [
                new("Состояние", "нет данных")
                {
                    Hint = "Источник состояния веб-сервера не ответил на опрос",
                },
            ]);
        }

        return new(ResolveState(sse),
        [
            new("Состояние", sse.Running ? "работает" : "остановлен")
            {
                Hint = sse.Running ? null : "Встроенный HTTP-сервер выключен в настройках или ещё не запущен",
            },
            new("Клиенты оверлея", DiagnosticsFormat.Number(sse.ClientCount))
            {
                Hint = "Открытые подключения к потоку событий: браузерный источник OBS, превью оверлея",
            },
            new("Отброшено сообщений", DiagnosticsFormat.Number(sse.DroppedMessageCount))
            {
                Hint = "Клиент не успевал читать поток, и сообщение до него не доехало",
            },
        ]);
    }

    private static DiagnosticsCardState ResolveState(SseStatus sse)
    {
        if (!sse.Running)
        {
            return DiagnosticsCardState.Unknown;
        }

        return sse.DroppedMessageCount > 0 ? DiagnosticsCardState.Warning : DiagnosticsCardState.Ok;
    }
}
