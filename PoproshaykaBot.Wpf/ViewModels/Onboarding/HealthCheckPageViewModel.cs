using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Chat;
using PoproshaykaBot.Core.Users;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.Bootstrap;
using System.Diagnostics;

namespace PoproshaykaBot.Wpf.ViewModels.Onboarding;

public sealed partial class HealthCheckPageViewModel : OnboardingPageViewModelBase, IDisposable
{
    private readonly IChatMessenger _chatMessenger;
    private readonly ILogger<HealthCheckPageViewModel> _logger;
    private readonly IShellLauncher _shellLauncher;
    private readonly List<IDisposable> _subs = [];

    private bool _botStatusDetected;

    [ObservableProperty]
    private string _overlayUrl = string.Empty;

    [ObservableProperty]
    private string _botStatusText = "Бот в чате как: ожидание сообщения...";

    [ObservableProperty]
    private bool _botStatusKnown;

    [ObservableProperty]
    private string _chatTestMessage = string.Empty;

    [ObservableProperty]
    private string _chatTestStatusText = "Не выполнено";

    [ObservableProperty]
    private bool _chatTestSuccess;

    [ObservableProperty]
    private bool _chatTestError;

    public HealthCheckPageViewModel(
        IChatMessenger chatMessenger,
        IEventBus eventBus,
        ILogger<HealthCheckPageViewModel> logger,
        IShellLauncher shellLauncher)
    {
        _chatMessenger = chatMessenger;
        _logger = logger;
        _shellLauncher = shellLauncher;

        _subs.Add(eventBus.SubscribeOnUi<ChatMessageReceived>(OnChatMessageReceived));
    }

    public override string PageTitle => "Диагностика (опционально)";

    public string OverlayHint =>
        "Совет: в свойствах источника снимите «Shutdown source when not visible» и "
        + "«Refresh browser when scene becomes active», иначе оверлей перезагружается при смене сцены.";

    public override void OnEnter(OnboardingContext context)
    {
        OverlayUrl = $"http://localhost:{context.Settings.Twitch.HttpServerPort}/chat";
        ResetChatTestStatus();
        CanAdvance = true;
    }

    [RelayCommand]
    private void SendChatTest()
    {
        var message = ChatTestMessage.Trim();

        if (string.IsNullOrEmpty(message))
        {
            ChatTestStatusText = "Введите текст сообщения.";
            ChatTestSuccess = false;
            ChatTestError = false;
            return;
        }

        try
        {
            _chatMessenger.Send(message);
            ChatTestStatusText = "✓ Сообщение поставлено в очередь отправки.";
            ChatTestSuccess = true;
            ChatTestError = false;
        }
        catch (Exception exception)
        {
            _logger.OnboardingChatTestEnqueueFailed(exception);
            ChatTestStatusText = "✗ Не удалось поставить сообщение в очередь.";
            ChatTestSuccess = false;
            ChatTestError = true;
        }
    }

    [RelayCommand]
    private void OpenOverlayInBrowser()
    {
        _shellLauncher.Open(OverlayUrl);
    }

    public void Dispose()
    {
        foreach (var sub in _subs)
        {
            sub.Dispose();
        }

        _subs.Clear();
    }

    private void OnChatMessageReceived(ChatMessageReceived @event)
    {
        if (_botStatusDetected || !@event.IsBot)
        {
            return;
        }

        var status = DescribeBotStatus(@event.Status);
        _botStatusDetected = true;
        BotStatusText = $"Бот в чате как: {status}";
        BotStatusKnown = true;
    }

    private void ResetChatTestStatus()
    {
        ChatTestStatusText = "Не выполнено";
        ChatTestSuccess = false;
        ChatTestError = false;
    }

    private static string DescribeBotStatus(UserStatus status)
    {
        if (status.HasFlag(UserStatus.Broadcaster))
        {
            return "владелец канала";
        }

        if (status.HasFlag(UserStatus.Moderator))
        {
            return "модератор";
        }

        return "обычный пользователь";
    }
}
