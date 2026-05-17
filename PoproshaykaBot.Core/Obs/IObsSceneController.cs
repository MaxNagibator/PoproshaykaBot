namespace PoproshaykaBot.Core.Obs;

public interface IObsSceneController
{
    bool IsConnected { get; }

    Task<string?> GetCurrentSceneAsync(CancellationToken cancellationToken);

    Task SetCurrentSceneAsync(string sceneName, CancellationToken cancellationToken);
}
