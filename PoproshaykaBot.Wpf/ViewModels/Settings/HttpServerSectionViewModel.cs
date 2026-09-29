using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class HttpServerSectionViewModel : ObservableObject, IDisposable
{
    private readonly KestrelHttpServer _server;
    private readonly IDialogService _dialogService;
    private readonly IClipboardService _clipboard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ObsUrl))]
    private int _port;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ServerStatusText))]
    [NotifyPropertyChangedFor(nameof(ServerStatusSeverity))]
    private bool _isRunning;

    public HttpServerSectionViewModel(KestrelHttpServer server, IDialogService dialogService, IClipboardService clipboard)
    {
        _server = server;
        _dialogService = dialogService;
        _clipboard = clipboard;
    }

    public string ObsUrl => $"http://localhost:{Port}/chat";

    public string ServerStatusText => IsRunning ? "Запущен" : "Готов к запуску";

    public StatusSeverity ServerStatusSeverity => IsRunning ? StatusSeverity.Success : StatusSeverity.Warning;

    public void LoadSettings(TwitchSettings settings)
    {
        Port = settings.HttpServerPort;
        IsRunning = _server.IsRunning;
    }

    [RelayCommand]
    private void CopyUrl()
    {
        if (_clipboard.TrySetText(ObsUrl))
        {
            _dialogService.Info("Информация", "URL скопирован в буфер обмена!");
            return;
        }

        _dialogService.Error("Ошибка", "Не удалось скопировать URL в буфер обмена.");
    }

    [RelayCommand]
    private async Task RestartServerAsync()
    {
        try
        {
            if (_server.IsRunning)
            {
                await _server.StopAsync();
            }

            await _server.StartAsync();
            IsRunning = _server.IsRunning;

            _dialogService.Info("Информация", "HTTP сервер перезапущен.");
        }
        catch (Exception ex)
        {
            _dialogService.Error("Ошибка", $"Ошибка перезапуска сервера: {ex.Message}");
        }
    }

    public void Dispose()
    {
    }
}
