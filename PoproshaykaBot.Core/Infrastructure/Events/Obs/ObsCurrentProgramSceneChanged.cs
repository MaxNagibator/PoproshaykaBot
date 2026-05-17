namespace PoproshaykaBot.Core.Infrastructure.Events.Obs;

public sealed record ObsCurrentProgramSceneChanged(string SceneName) : EventBase;
