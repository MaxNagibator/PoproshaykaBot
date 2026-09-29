using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class RecentDebugChannelRowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    [NotifyPropertyChangedFor(nameof(Severity))]
    [NotifyPropertyChangedFor(nameof(Details))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private ChannelLiveStatus _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Details))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private string? _unknownReason;

    public RecentDebugChannelRowViewModel(string login, DateTimeOffset lastUsedAt, DateTimeOffset now)
    {
        Login = login;
        LastUsedAt = lastUsedAt;
        UsedText = $"использован {RelativeTime.DescribeMoment(lastUsedAt, now)}";
        _status = new(login, ChannelLiveState.Unknown);
    }

    public string Login { get; }

    public DateTimeOffset LastUsedAt { get; }

    public string UsedText { get; }

    public string StateText => Status.State switch
    {
        ChannelLiveState.Live => string.Create(UiCulture.Russian, $"в эфире · зрителей: {Status.ViewerCount:N0}"),
        ChannelLiveState.Offline => "не в эфире",
        _ => "неизвестно",
    };

    public StatusSeverity Severity => Status.State switch
    {
        ChannelLiveState.Live => StatusSeverity.Success,
        ChannelLiveState.Offline => StatusSeverity.None,
        _ => StatusSeverity.Warning,
    };

    public string Details
    {
        get
        {
            switch (Status.State)
            {
                case ChannelLiveState.Live:
                    var lines = new List<string> { Status.Title ?? "Стрим без названия" };

                    if (Status.GameName is { } game)
                    {
                        lines.Add($"Категория: {game}");
                    }

                    if (Status.StartedAt is { } startedAt)
                    {
                        lines.Add($"В эфире с {startedAt.ToLocalTime().ToString("HH:mm", UiCulture.Russian)}");
                    }

                    return string.Join(Environment.NewLine, lines);

                case ChannelLiveState.Offline:
                    return "Сейчас не в эфире.";

                default:
                    return UnknownReason ?? "Состояние ещё не проверено. Нажмите «Обновить».";
            }
        }
    }

    public string Summary => $"{Login}, {StateText}, {UsedText}";

    public string UsedTooltip => $"Последнее подключение: {RelativeTime.FormatMoment(LastUsedAt)}";

    public void Apply(ChannelLiveStatus? status, string? unknownReason)
    {
        Status = status ?? new(Login, ChannelLiveState.Unknown);
        UnknownReason = Status.State == ChannelLiveState.Unknown ? unknownReason : null;
    }
}
