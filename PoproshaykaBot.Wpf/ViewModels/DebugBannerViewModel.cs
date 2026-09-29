using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Core.Debugging;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed partial class DebugBannerViewModel : ObservableObject
{
    private readonly ITargetChannelProvider _targetChannelProvider;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _shortText = string.Empty;

    [ObservableProperty]
    private bool _isSendingAllowed;

    public DebugBannerViewModel(ITargetChannelProvider targetChannelProvider)
    {
        ArgumentNullException.ThrowIfNull(targetChannelProvider);

        _targetChannelProvider = targetChannelProvider;

        Refresh();
    }

    public void Refresh()
    {
        var target = _targetChannelProvider.Current;

        if (!target.IsDebugSession)
        {
            IsVisible = false;
            return;
        }

        IsSendingAllowed = target.IsSendingAllowed;

        Text = target.IsSendingAllowed
            ? $"Отладка: бот подключается к каналу {target.Login} и пишет в его чат."
            : $"Отладка: бот читает чат канала {target.Login}. Сообщения не отправляются – они видны в журнале.";

        ShortText = target.IsSendingAllowed
            ? $"Отладка: {target.Login}, отправка включена"
            : $"Отладка: {target.Login}, только чтение";

        IsVisible = true;
    }
}
