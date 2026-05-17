namespace PoproshaykaBot.Core.Broadcast.Profiles;

public sealed class BroadcastProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string BroadcasterLanguage { get; set; } = "ru";
    public List<string> Tags { get; set; } = [];
    public string ObsSceneName { get; set; } = string.Empty;
    public int CurrentNumber { get; set; } = 1;
    public DateTimeOffset? LastApplyAt { get; set; }
    public DateTimeOffset? LastAutoAdvanceAt { get; set; }
}
