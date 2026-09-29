namespace PoproshaykaBot.Wpf.ViewModels.Tiles;

public sealed record PollChoiceRowViewModel(string Title, int Votes, int Percent, bool IsLeader)
{
    public string Label => $"{Percent}% ({Votes})";
}
