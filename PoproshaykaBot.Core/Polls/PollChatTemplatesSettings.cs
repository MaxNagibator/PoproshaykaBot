namespace PoproshaykaBot.Core.Polls;

public sealed class PollChatTemplatesSettings
{
    public bool StartEnabled { get; set; } = true;
    public string StartTemplate { get; set; } = "📊 Голосование: «{title}». Варианты: {choices}. Длительность: {durationLeft}. Голосуйте в Twitch!";

    public bool ProgressEnabled { get; set; } = false;
    public string ProgressTemplate { get; set; } = "📊 Пока что лидирует «{leader}» ({leaderVotes}/{totalVotes}).";

    public bool EndEnabled { get; set; } = true;
    public string EndTemplate { get; set; } = "🏁 Голосование завершено: победил «{winner}» – {winnerVotes} из {totalVotes}.";

    public bool TerminatedEnabled { get; set; } = true;
    public string TerminatedTemplate { get; set; } = "⛔ Голосование «{title}» завершено досрочно.";

    public bool ArchivedEnabled { get; set; } = false;
    public string ArchivedTemplate { get; set; } = "🗄️ Голосование «{title}» заархивировано.";

    public int ProgressAnnounceIntervalSeconds { get; set; } = 60;
}
