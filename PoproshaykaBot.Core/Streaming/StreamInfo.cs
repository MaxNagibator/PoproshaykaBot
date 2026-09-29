namespace PoproshaykaBot.Core.Streaming;

/// <summary>
/// Описание текущего стрима канала (заголовок, игра, зрители и т.п.).
/// </summary>
public class StreamInfo
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int ViewerCount { get; set; }
    public DateTime StartedAt { get; set; }
    public string ThumbnailUrl { get; set; } = string.Empty;
    public IReadOnlyList<string> Tags { get; set; } = [];
    public bool IsMature { get; set; }

    // TODO: снимок без названия, категории и зрителей считается ещё не загруженным (из stream.online до ответа Helix); появится признак источника – сверять по нему
    public bool IsBare => string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(GameName) && ViewerCount == 0;
}
