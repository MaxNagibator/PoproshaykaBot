using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Update;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Settings.Update;
using PoproshaykaBot.Core.Update;
using PoproshaykaBot.Wpf.Infrastructure;
using System.Globalization;
using System.Windows;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class UpdateSettingsSectionViewModel : ObservableObject, IDisposable
{
    private readonly IUpdateCoordinator _coordinator;
    private readonly IDialogService _dialogService;
    private readonly UpdateStore _store;
    private readonly List<IDisposable> _subscriptions = [];

    [ObservableProperty]
    private bool _autoCheckEnabled;

    [ObservableProperty]
    private double _checkIntervalHours = 6;

    [ObservableProperty]
    private bool _autoInstall;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _currentVersionText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private StatusSeverity _statusSeverity;

    [ObservableProperty]
    private bool _isFrameworkDependentVisible;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    private bool _allowFrameworkDependent;

    [ObservableProperty]
    private string _repositoryOverride = string.Empty;

    [ObservableProperty]
    private string _repositoryHintText = string.Empty;

    [ObservableProperty]
    private StatusSeverity _repositoryHintSeverity;

    public UpdateSettingsSectionViewModel(IUpdateCoordinator coordinator, UpdateStore store, IEventBus bus, IDialogService dialogService)
    {
        _coordinator = coordinator;
        _store = store;
        _dialogService = dialogService;
        IsFrameworkDependentVisible = coordinator.Kind == UpdateKind.FrameworkDependent;
        CurrentVersionText = $"Текущая версия: {coordinator.CurrentVersion} (.NET {Environment.Version.Major})";
        _subscriptions.Add(bus.SubscribeOnUi<UpdateAvailable>(_ => RefreshStatus()));
        _subscriptions.Add(bus.SubscribeOnUi<UpdatePrepared>(_ => RefreshStatus()));
        _subscriptions.Add(bus.SubscribeOnUi<UpdateDiscarded>(_ => RefreshStatus()));
        RefreshStatus();
    }

    public string DefaultRepositorySlug => _coordinator.DefaultRepositorySlug;

    public void LoadSettings(UpdateSettings settings)
    {
        AutoCheckEnabled = settings.AutoCheckEnabled;
        CheckIntervalHours = Math.Clamp(settings.CheckIntervalHours, 1, 168);
        AutoInstall = settings.ApplyMode == UpdateApplyMode.SilentOnExit;
        AllowFrameworkDependent = settings.AllowFrameworkDependentUpdate;
        RepositoryOverride = settings.RepositoryOverride ?? string.Empty;
        RefreshRepositoryHint();
        RefreshStatus();
    }

    public void SaveSettings(UpdateSettings settings)
    {
        settings.AutoCheckEnabled = AutoCheckEnabled;
        settings.CheckIntervalHours = (int)CheckIntervalHours;
        settings.ApplyMode = AutoInstall ? UpdateApplyMode.SilentOnExit : UpdateApplyMode.NotifyAndConfirm;
        settings.AllowFrameworkDependentUpdate = AllowFrameworkDependent;
        var slug = RepositoryOverride.Trim();
        settings.RepositoryOverride = slug.Length == 0 ? null : slug;
    }

    public Task OnSavedAsync(UpdateApplyMode previousMode)
    {
        return previousMode == UpdateApplyMode.SilentOnExit && !AutoInstall
            ? _coordinator.DiscardPreparedUpdateAsync(CancellationToken.None)
            : Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }

        _subscriptions.Clear();
    }

    [RelayCommand]
    private void IncreaseInterval() => CheckIntervalHours = Math.Min(168, CheckIntervalHours + 1);

    [RelayCommand]
    private void DecreaseInterval() => CheckIntervalHours = Math.Max(1, CheckIntervalHours - 1);

    [RelayCommand(CanExecute = nameof(CanActNow))]
    private async Task CheckNowAsync()
    {
        IsBusy = true;
        (StatusText, StatusSeverity) = ("Проверка обновлений…", StatusSeverity.Info);
        DownloadProgress = 0;

        try
        {
            var candidate = await _coordinator.CheckNowAsync(CancellationToken.None);

            if (candidate is null)
            {
                (StatusText, StatusSeverity) = ("У вас установлена актуальная версия.", StatusSeverity.None);
            }
            else
            {
                RefreshStatus();
            }
        }
        catch (Exception exception)
        {
            (StatusText, StatusSeverity) = ($"Ошибка проверки: {exception.Message}", StatusSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallUpdateAsync()
    {
        var candidate = _coordinator.LatestCandidate;

        if (candidate is null)
        {
            return;
        }

        var question = string.Equals(_coordinator.PreparedVersion, candidate.Version.ToString(), StringComparison.Ordinal)
            ? $"Установить загруженную версию {candidate.Version} сейчас?"
            : $"Загрузить и установить версию {candidate.Version}?";

        if (!_dialogService.Confirm("Установка обновления", question + "\n\nПриложение перезапустится автоматически."))
        {
            return;
        }

        IsBusy = true;
        DownloadProgress = 0;

        var progress = new Progress<int>(pct =>
        {
            (StatusText, StatusSeverity) = ($"Загрузка обновления… {pct}%", StatusSeverity.Info);
            DownloadProgress = pct;
        });

        try
        {
            await _coordinator.PrepareAsync(candidate, progress, CancellationToken.None);

            _dialogService.Info("Установка обновления", "Обновление загружено. Приложение закроется и установит новую версию.");

            Application.Current.MainWindow?.Close();
        }
        catch (Exception exception)
        {
            (StatusText, StatusSeverity) = ($"Ошибка загрузки: {exception.Message}", StatusSeverity.Error);

            _dialogService.Error("Ошибка обновления", $"Не удалось установить обновление: {exception.Message}");

            IsBusy = false;
        }
    }

    private bool CanActNow() => !IsBusy && IsUpdatableNow();

    private bool CanInstall() => !IsBusy && IsUpdatableNow() && _coordinator.LatestCandidate is not null;

    partial void OnAllowFrameworkDependentChanged(bool value) => RefreshStatus();

    partial void OnRepositoryOverrideChanged(string value) => RefreshRepositoryHint();

    private bool IsUpdatableNow() => _coordinator.Kind switch
    {
        UpdateKind.Portable => true,
        UpdateKind.FrameworkDependent => AllowFrameworkDependent,
        _ => false,
    };

    private void RefreshRepositoryHint()
    {
        var text = RepositoryOverride.Trim();

        if (text.Length == 0)
        {
            RepositoryHintText = $"Пусто – используется {_coordinator.DefaultRepositorySlug} (из сборки).";
            RepositoryHintSeverity = StatusSeverity.None;
        }
        else if (UpdateRepository.IsValidSlug(text))
        {
            RepositoryHintText = $"✓ {text}";
            RepositoryHintSeverity = StatusSeverity.Success;
        }
        else
        {
            RepositoryHintText = "Неверный формат. Ожидается owner/repo.";
            RepositoryHintSeverity = StatusSeverity.Error;
        }
    }

    private void RefreshStatus()
    {
        if (_coordinator.Kind == UpdateKind.Unsupported)
        {
            (StatusText, StatusSeverity) = ("Автообновление недоступно для этой сборки (запуск из среды разработки).", StatusSeverity.None);
            return;
        }

        if (!IsUpdatableNow())
        {
            (StatusText, StatusSeverity) = ("Автообновление выключено для сборки, требующей .NET. Включите опцию ниже.", StatusSeverity.None);
            return;
        }

        var candidate = _coordinator.LatestCandidate;

        if (candidate is not null
            && string.Equals(_coordinator.PreparedVersion, candidate.Version.ToString(), StringComparison.Ordinal))
        {
            (StatusText, StatusSeverity) = ($"Обновление {candidate.Version} загружено и установится при выходе из приложения.", StatusSeverity.Success);
            InstallUpdateCommand.NotifyCanExecuteChanged();
        }
        else if (candidate is not null)
        {
            (StatusText, StatusSeverity) = ($"Доступно обновление: {candidate.Version}", StatusSeverity.Success);
            InstallUpdateCommand.NotifyCanExecuteChanged();
        }
        else
        {
            (StatusText, StatusSeverity) = (FormatLastCheck(_store.Load()), StatusSeverity.None);
        }
    }

    private static string FormatLastCheck(UpdateSettings settings)
    {
        if (settings.LastCheckUtc is { } lastCheck)
        {
            var local = lastCheck.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            return $"Последняя проверка: {local}. Обновлений не найдено.";
        }

        return "Проверка обновлений ещё не выполнялась.";
    }
}
