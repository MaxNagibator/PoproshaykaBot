namespace PoproshaykaBot.Core.Polls;

public sealed class PollsSettings
{
    public List<PollProfile> Profiles { get; set; } = [];

    public PollChatTemplatesSettings ChatTemplates { get; set; } = new();

    /// <summary>
    /// Если задано – авто-триггеры голосований отключены на указанный день (UTC).
    /// Сбрасывается автоматически при смене календарной даты.
    /// </summary>
    public DateTime? AutoTriggerKillSwitchDateUtc { get; set; }

    public int HistoryMaxItems { get; set; } = 500;
}
