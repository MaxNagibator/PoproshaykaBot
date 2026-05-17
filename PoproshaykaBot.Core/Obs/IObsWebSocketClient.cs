using System.Text.Json;

namespace PoproshaykaBot.Core.Obs;

public interface IObsWebSocketClient : IAsyncDisposable
{
    event EventHandler<ObsWebSocketEventArgs>? EventReceived;

    bool IsConnected { get; }

    Task<ObsConnectionSnapshot> ConnectAsync(ObsConnectionOptions options, CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);

    Task<JsonElement?> SendRequestAsync(string requestType, object? requestData, CancellationToken cancellationToken);
}
