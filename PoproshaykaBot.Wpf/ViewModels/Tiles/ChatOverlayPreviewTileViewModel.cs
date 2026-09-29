using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.IconPacks;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Wpf.ViewModels.Tiles;

public sealed partial class ChatOverlayPreviewTileViewModel : DashboardTileViewModel
{
    private readonly SettingsManager _settings;
    private readonly ToolbarItemViewModel _previewModeAction;
    private readonly ToolbarItemViewModel _viewerModeAction;

    [ObservableProperty]
    private bool _isPreviewMode = true;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorHeading = string.Empty;

    [ObservableProperty]
    private string _errorDescription = string.Empty;

    public ChatOverlayPreviewTileViewModel(SettingsManager settings, ILogger<ChatOverlayPreviewTileViewModel> logger)
        : base("chat-overlay-preview", "Превью оверлея", minWidth: 320, minHeight: 234)
    {
        _settings = settings;
        Logger = logger;
        UserDataFolder = WebView2UserDataFolders.ResolveOverlayPreview();

        _previewModeAction = new(
            PackIconLucideKind.Eye,
            ToggleModeCommand,
            "Режим превью – сообщения не затухают. Нажми, чтобы переключиться на фактическое отображение.");

        _viewerModeAction = new(
            PackIconLucideKind.Monitor,
            ToggleModeCommand,
            "Фактическое отображение – с затуханиями по настройкам OBS. Нажми, чтобы вернуться к превью.");

        _viewerModeAction.IsVisible = false;

        HeaderActions.Add(_previewModeAction);
        HeaderActions.Add(_viewerModeAction);
    }

    public override PackIconLucideKind Icon => PackIconLucideKind.Eye;

    public override bool FillsAvailableSpace => true;

    public override bool ContentFillsTile => true;

    public ILogger Logger { get; }

    public string UserDataFolder { get; }

    public string CurrentUrl
    {
        get
        {
            var port = _settings.Current.Twitch.HttpServerPort;
            return IsPreviewMode
                ? $"http://localhost:{port}/chat?preview=true"
                : $"http://localhost:{port}/chat";
        }
    }

    public void ShowRuntimeMissingError()
    {
        HasError = true;
        ErrorHeading = "Требуется Microsoft Edge WebView2 Runtime";
        ErrorDescription = "Установите его, чтобы открыть превью оверлея.";
        _previewModeAction.IsEnabled = false;
        _viewerModeAction.IsEnabled = false;
    }

    public void ShowGenericError()
    {
        HasError = true;
        ErrorHeading = "Не удалось открыть превью оверлея";
        ErrorDescription = "Подробности – в логах.";
        _previewModeAction.IsEnabled = false;
        _viewerModeAction.IsEnabled = false;
    }

    partial void OnIsPreviewModeChanged(bool value)
    {
        _previewModeAction.IsVisible = value;
        _viewerModeAction.IsVisible = !value;
        OnPropertyChanged(nameof(CurrentUrl));
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsPreviewMode = !IsPreviewMode;
    }
}
