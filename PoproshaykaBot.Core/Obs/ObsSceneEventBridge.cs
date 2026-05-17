using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Obs;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using System.Text.Json;

namespace PoproshaykaBot.Core.Obs;

/// <summary>
/// Транслирует сырое obs-websocket событие <c>CurrentProgramSceneChanged</c> в типизированное
/// <see cref="ObsCurrentProgramSceneChanged" /> на <see cref="IEventBus" />. Подписка живёт весь
/// процесс (как и сам клиент): обработчик вызывается из receive-loop OBS, поэтому публикация —
/// fire-and-forget, чтобы не блокировать приём остальных событий (volume meters, запись, сцены).
/// </summary>
internal sealed class ObsSceneEventBridge(
    IObsWebSocketClient client,
    IEventBus eventBus,
    ILogger<ObsSceneEventBridge> logger)
    : IAppLifetimeComponent
{
    private const string CurrentProgramSceneChangedEvent = "CurrentProgramSceneChanged";

    public string Name => "OBS мост событий сцен";

    public int StartOrder => 11;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        client.EventReceived += OnObsEventReceived;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        client.EventReceived -= OnObsEventReceived;
        return Task.CompletedTask;
    }

    private void OnObsEventReceived(object? sender, ObsWebSocketEventArgs e)
    {
        if (!string.Equals(e.EventType, CurrentProgramSceneChangedEvent, StringComparison.Ordinal))
        {
            return;
        }

        if (e.EventData is not { } data
            || !data.TryGetProperty("sceneName", out var sceneNameElement)
            || sceneNameElement.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var sceneName = sceneNameElement.GetString();
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        _ = PublishSafeAsync(sceneName);
    }

    private async Task PublishSafeAsync(string sceneName)
    {
        try
        {
            await eventBus.PublishAsync(new ObsCurrentProgramSceneChanged(sceneName)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "OBS мост: не удалось опубликовать смену сцены {SceneName}", sceneName);
        }
    }
}
