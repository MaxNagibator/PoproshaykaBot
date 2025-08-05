namespace PoproshaykaBot.WinForms.Models;

public class ChatMessageData
{
    public DateTime Timestamp { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public ChatMessageType MessageType { get; init; }
    public UserStatus Status { get; init; } = UserStatus.None;

    public List<EmoteInfo> Emotes { get; init; } = [];
    public List<KeyValuePair<string, string>> Badges { get; init; } = [];
    public Dictionary<string, string> BadgeUrls { get; init; } = new(); // [badgeType/version] = imageUrl
}

public class EmoteInfo
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public int StartIndex { get; init; }
    public int EndIndex { get; init; }
}
