using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed partial class StreamMonitoringViewModel : ObservableObject, IDisposable
{
    private readonly IDisposable _subscription;

    [ObservableProperty]
    private bool _isVisible;

    public StreamMonitoringViewModel(IEventBus eventBus)
    {
        _subscription = eventBus.SubscribeOnUi<StreamMonitoringStatusChanged>(Apply);
    }

    public StreamMonitoringIndicatorViewModel Bot { get; } = new("Бот");

    public StreamMonitoringIndicatorViewModel Broadcaster { get; } = new("Стример");

    public void Dispose()
    {
        _subscription.Dispose();
    }

    private void Apply(StreamMonitoringStatusChanged statusEvent)
    {
        var indicator = statusEvent.Role switch
        {
            TwitchOAuthRole.Broadcaster => Broadcaster,
            _ => Bot,
        };

        indicator.Apply(statusEvent.Status, statusEvent.Detail);

        IsVisible = Bot.IsReported || Broadcaster.IsReported;
    }
}

public sealed partial class StreamMonitoringIndicatorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private StatusSeverity _severity;

    [ObservableProperty]
    private string _tooltip = string.Empty;

    [ObservableProperty]
    private bool _isReported;

    public StreamMonitoringIndicatorViewModel(string role)
    {
        Role = role;
        _text = role;
    }

    public string Role { get; }

    public void Apply(StreamMonitoringStatus status, string? detail)
    {
        IsReported = true;

        var (label, severity) = status switch
        {
            StreamMonitoringStatus.Connecting => ("подключение", StatusSeverity.Info),
            StreamMonitoringStatus.Connected => ("работает", StatusSeverity.Success),
            StreamMonitoringStatus.Reconnecting => ("переподключение", StatusSeverity.Warning),
            StreamMonitoringStatus.Failed => ("ошибка", StatusSeverity.Error),
            _ => ("остановлен", StatusSeverity.None),
        };

        Text = $"{Role}: {label}";
        Severity = severity;
        Tooltip = string.IsNullOrWhiteSpace(detail)
            ? $"Мониторинг стрима, {Role.ToLowerInvariant()}: {label}"
            : $"Мониторинг стрима, {Role.ToLowerInvariant()}: {label} ({detail})";
    }
}
