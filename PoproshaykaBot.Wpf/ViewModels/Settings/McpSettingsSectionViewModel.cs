using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepShell.Mcp;
using System.ComponentModel;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class McpSettingsSectionViewModel : ObservableObject, IDisposable
{
    private readonly McpServerOptions _options;
    private readonly IDialogService _dialogService;
    private readonly IClipboardService _clipboard;

    public McpSettingsSectionViewModel(
        McpPreferences preferences,
        McpServerHost server,
        McpServerOptions options,
        IDialogService dialogService,
        IClipboardService clipboard)
    {
        Preferences = preferences;
        Server = server;
        _options = options;
        _dialogService = dialogService;
        _clipboard = clipboard;

        Preferences.PropertyChanged += OnPreferencesPropertyChanged;
        Server.PropertyChanged += OnServerPropertyChanged;
    }

    public McpPreferences Preferences { get; }

    public McpServerHost Server { get; }

    public string Url => $"http://127.0.0.1:{Preferences.Port}{_options.EndpointPath}";

    public string ConnectSnippet => McpConnectSnippets.For(0, Url, Preferences.Token, _options.ServerName);

    public string StatusText => (Server.Endpoint, Preferences.Enabled) switch
    {
        ({ } endpoint, _) => $"Сервер слушает {endpoint}",
        (null, true) => "Сервер включён, но не поднялся",
        _ => "Сервер выключен – агент подключиться не сможет",
    };

    public void Dispose()
    {
        Preferences.PropertyChanged -= OnPreferencesPropertyChanged;
        Server.PropertyChanged -= OnServerPropertyChanged;
    }

    [RelayCommand]
    private void CopyToken()
    {
        if (_clipboard.TrySetText(Preferences.Token))
        {
            _dialogService.Info("Информация", "Токен скопирован в буфер обмена!");
            return;
        }

        _dialogService.Error("Ошибка", "Не удалось скопировать токен в буфер обмена.");
    }

    [RelayCommand]
    private void CopyConnectSnippet()
    {
        if (_clipboard.TrySetText(ConnectSnippet))
        {
            _dialogService.Info("Информация", "Строка подключения скопирована – её можно вставить в настройки агента!");
            return;
        }

        _dialogService.Error("Ошибка", "Не удалось скопировать строку подключения в буфер обмена.");
    }

    private void OnPreferencesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(McpPreferences.Port) or nameof(McpPreferences.Token))
        {
            OnPropertyChanged(nameof(Url));
            OnPropertyChanged(nameof(ConnectSnippet));
        }

        if (e.PropertyName is nameof(McpPreferences.Enabled))
        {
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void OnServerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(McpServerHost.Endpoint))
        {
            OnPropertyChanged(nameof(StatusText));
        }
    }
}
