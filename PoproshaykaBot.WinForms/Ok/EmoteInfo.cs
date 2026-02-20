namespace PoproshaykaBot.WinForms.Models;

public class EmoteInfo
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public int StartIndex { get; init; }
    public int EndIndex { get; init; }
}