using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Wpf.ViewModels.Controls;
using PoproshaykaBot.Wpf.Bootstrap;
using System.Collections.ObjectModel;

namespace PoproshaykaBot.Wpf.ViewModels.Dialogs;

public sealed partial class BroadcastProfileEditDialogViewModel : ObservableObject, IDialogViewModel, IAcceptableDialog, IDisposable
{
    private readonly ObsIntegrationService _obsIntegration;
    private readonly ObsIntegrationStore _obsIntegrationStore;
    private readonly ILogger<BroadcastProfileEditDialogViewModel> _logger;

    private bool _nameRequired = true;
    private bool _isCurrentSettingsMode;
    private bool _sceneBindingEnabled = true;
    private string _originalLanguage = "ru";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    [NotifyPropertyChangedFor(nameof(ConfirmButtonText))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    private string _profileTitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    private int _currentNumber = 1;

    [ObservableProperty]
    private string _tags = string.Empty;

    [ObservableProperty]
    private string _language = "ru";

    [ObservableProperty]
    private string _obsSceneName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _obsScenes = [];

    public BroadcastProfileEditDialogViewModel(
        GameAutocompleteViewModel game,
        ObsIntegrationService obsIntegration,
        ObsIntegrationStore obsIntegrationStore,
        ILogger<BroadcastProfileEditDialogViewModel> logger)
    {
        Game = game;
        _obsIntegration = obsIntegration;
        _obsIntegrationStore = obsIntegrationStore;
        _logger = logger;
    }

    public event EventHandler<bool>? RequestClose;

    public string Title { get; private set; } = "Редактировать профиль";
    public string ConfirmButtonText => _isCurrentSettingsMode
        ? NewProfileName.Length > 0 ? "Сохранить и применить" : "Применить"
        : "OK";
    public string NameLabel { get; private set; } = "Имя:";
    public string? NamePlaceholder { get; private set; }
    public bool IsObsSceneVisible { get; private set; } = true;

    public GameAutocompleteViewModel Game { get; }

    public IReadOnlyList<string> LanguageOptions { get; } = ["ru", "en"];

    public string? Preview
    {
        get
        {
            var template = MessageTemplate.For(ProfileTitle);
            if (!template.Contains("n"))
                return null;
            return $"Текущая серия в эфире: {template.With("n", CurrentNumber.ToString()).Render()}";
        }
    }

    public bool HasPreview => Preview is not null;

    public string NewProfileName => Name.Trim();

    public void LoadFrom(BroadcastProfile profile)
    {
        Name = profile.Name;
        ProfileTitle = profile.Title;
        CurrentNumber = Math.Clamp(profile.CurrentNumber, 1, 1_000_000);
        Game.SetSelected(profile.GameId ?? string.Empty, profile.GameName ?? string.Empty);
        Tags = string.Join(", ", profile.Tags);
        ObsSceneName = profile.ObsSceneName;
        _originalLanguage = profile.BroadcasterLanguage ?? "ru";
        Language = _originalLanguage;
    }

    public void ConfigureCurrentSettingsMode()
    {
        Title = "Текущие настройки эфира";
        _nameRequired = false;
        Name = string.Empty;
        NameLabel = "Сохранить как:";
        NamePlaceholder = "имя нового профиля (необязательно)";
        _isCurrentSettingsMode = true;
        IsObsSceneVisible = false;
        _sceneBindingEnabled = false;
    }

    public void WriteTo(BroadcastProfile profile)
    {
        if (_nameRequired)
            profile.Name = Name.Trim();

        profile.Title = ProfileTitle.Trim();
        profile.CurrentNumber = CurrentNumber;

        if (Game.Selected is { } selected)
        {
            profile.GameId = selected.Id;
            profile.GameName = selected.Name;
        }

        profile.Tags = Tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        profile.BroadcasterLanguage = string.IsNullOrEmpty(Language) ? _originalLanguage : Language;

        if (_sceneBindingEnabled)
            profile.ObsSceneName = ObsSceneName.Trim();
    }

    public async Task LoadObsScenesAsync()
    {
        if (!_sceneBindingEnabled)
            return;

        var settings = _obsIntegrationStore.Load();
        if (!settings.Enabled)
            return;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var scenes = await _obsIntegration.ListScenesAsync(settings, cts.Token);

            if (scenes.Count == 0)
                return;

            var current = ObsSceneName;
            ObsScenes = [.. scenes];
            ObsSceneName = current;
        }
        catch (Exception ex)
        {
            _logger.ObsScenesLoadFailed(ex);
        }
    }

    public bool TryAccept()
    {
        if (!CanConfirm())
            return false;

        RequestClose?.Invoke(this, true);
        return true;
    }

    public void Dispose()
    {
        Game.Dispose();
    }

    private bool CanConfirm() => !_nameRequired || Name.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        RequestClose?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }

    [RelayCommand]
    private void IncreaseNumber()
    {
        if (CurrentNumber < 1_000_000)
            CurrentNumber++;
    }

    [RelayCommand]
    private void DecreaseNumber()
    {
        if (CurrentNumber > 1)
            CurrentNumber--;
    }
}
