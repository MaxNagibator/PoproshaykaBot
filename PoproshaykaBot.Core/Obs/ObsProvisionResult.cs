namespace PoproshaykaBot.Core.Obs;

public sealed record ObsProvisionResult(
    bool Created,
    string SceneName,
    string SourceName,
    string Url);
