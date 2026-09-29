namespace PoproshaykaBot.Core.Obs;

public interface IObsSceneController
{
    bool IsConnected { get; }

    ObsConnectionSnapshot CurrentStatus { get; }

    Task<string?> GetCurrentSceneAsync(CancellationToken cancellationToken);

    Task SetCurrentSceneAsync(string sceneName, CancellationToken cancellationToken);
}
