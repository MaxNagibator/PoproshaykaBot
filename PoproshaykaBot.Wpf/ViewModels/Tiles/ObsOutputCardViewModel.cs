using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Wpf.Bootstrap;

namespace PoproshaykaBot.Wpf.ViewModels.Tiles;

public enum ObsOutputCardKind
{
    None = 0,
    Stream = 1,
    Record = 2,
}

public enum ObsOutputCardState
{
    None = 0,
    Unknown = 1,
    Idle = 2,
    Active = 3,
    Paused = 4,
    Error = 5,
}

public sealed partial class ObsOutputCardViewModel : ObservableObject
{
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(8);

    private readonly ObsIntegrationService _obsIntegration;
    private readonly ILogger _logger;

    private bool _actionInFlight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(ShowsSecondaryButton))]
    [NotifyPropertyChangedFor(nameof(PrimaryButtonText))]
    [NotifyPropertyChangedFor(nameof(SecondaryButtonText))]
    private ObsOutputCardState _state = ObsOutputCardState.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTimecode))]
    private string? _timecode;

    [ObservableProperty]
    private string _meta = string.Empty;

    [ObservableProperty]
    private bool _metaIsError;

    [ObservableProperty]
    private string? _health;

    [ObservableProperty]
    private string? _healthSeverity;

    public ObsOutputCardViewModel(ObsOutputCardKind kind, ObsIntegrationService obsIntegration, ILogger logger)
    {
        Kind = kind;
        _obsIntegration = obsIntegration;
        _logger = logger;
    }

    // TODO: добавить пульсацию точки/ореол и анимированную обводку карточки (Storyboard) для Active/Paused – пока статичная подсветка токенами State.*.
    public ObsOutputCardKind Kind { get; }

    public bool IsRecordMode => Kind == ObsOutputCardKind.Record;

    public bool HasTimecode => !string.IsNullOrWhiteSpace(Timecode);

    public bool IsRunning => State is ObsOutputCardState.Active or ObsOutputCardState.Paused;

    public bool ShowsSecondaryButton => IsRecordMode && IsRunning;

    public string Title => (Kind, State) switch
    {
        (ObsOutputCardKind.Stream, ObsOutputCardState.Active) => "В эфире",
        (ObsOutputCardKind.Stream, ObsOutputCardState.Idle) => "Эфир не идёт",
        (ObsOutputCardKind.Stream, ObsOutputCardState.Error) => "Эфир: ошибка",
        (ObsOutputCardKind.Stream, _) => "Эфир",
        (ObsOutputCardKind.Record, ObsOutputCardState.Active) => "Идёт запись",
        (ObsOutputCardKind.Record, ObsOutputCardState.Paused) => "Запись на паузе",
        (ObsOutputCardKind.Record, ObsOutputCardState.Idle) => "Запись не идёт",
        (ObsOutputCardKind.Record, ObsOutputCardState.Error) => "Запись: ошибка",
        (ObsOutputCardKind.Record, _) => "Запись",
        _ => "–",
    };

    public string PrimaryButtonText => (Kind, State) switch
    {
        (ObsOutputCardKind.Stream, ObsOutputCardState.Idle) => "▶ Старт",
        (ObsOutputCardKind.Stream, ObsOutputCardState.Active) => "■ Стоп",
        (ObsOutputCardKind.Record, ObsOutputCardState.Idle) => "● Старт",
        (ObsOutputCardKind.Record, ObsOutputCardState.Active) => "■ Стоп",
        (ObsOutputCardKind.Record, ObsOutputCardState.Paused) => "■ Стоп",
        _ => "–",
    };

    public string SecondaryButtonText => State switch
    {
        ObsOutputCardState.Active => "⏸ Пауза",
        ObsOutputCardState.Paused => "▶ Дальше",
        _ => "⏸ Пауза",
    };

    public void ApplySnapshot(
        bool? active,
        bool? paused,
        string? timecode,
        string? meta,
        string? health = null,
        string? healthSeverity = null)
    {
        State = ResolveState(active, paused);
        Timecode = string.IsNullOrWhiteSpace(timecode) ? null : timecode;
        MetaIsError = false;
        Meta = meta ?? string.Empty;
        Health = health;
        HealthSeverity = healthSeverity;
    }

    public void ApplyUnknown()
    {
        State = ObsOutputCardState.Unknown;
        Timecode = null;
        MetaIsError = false;
        Meta = string.Empty;
        Health = null;
        HealthSeverity = null;
    }

    private static ObsOutputCardState ResolveState(bool? active, bool? paused)
    {
        return (active, paused) switch
        {
            (true, true) => ObsOutputCardState.Paused,
            (true, _) => ObsOutputCardState.Active,
            (false, _) => ObsOutputCardState.Idle,
            _ => ObsOutputCardState.Unknown,
        };
    }

    private Func<CancellationToken, Task>? ResolvePrimaryAction()
    {
        return (Kind, State) switch
        {
            (ObsOutputCardKind.Stream, ObsOutputCardState.Idle) => _obsIntegration.StartStreamAsync,
            (ObsOutputCardKind.Stream, ObsOutputCardState.Active) => _obsIntegration.StopStreamAsync,
            (ObsOutputCardKind.Record, ObsOutputCardState.Idle) => _obsIntegration.StartRecordAsync,
            (ObsOutputCardKind.Record, ObsOutputCardState.Active) => _obsIntegration.StopRecordAsync,
            (ObsOutputCardKind.Record, ObsOutputCardState.Paused) => _obsIntegration.StopRecordAsync,
            _ => null,
        };
    }

    [RelayCommand(CanExecute = nameof(CanInvokePrimary))]
    private Task InvokePrimaryAsync()
    {
        var action = ResolvePrimaryAction();
        return action is null ? Task.CompletedTask : InvokeObsActionAsync(action);
    }

    private bool CanInvokePrimary()
    {
        return !_actionInFlight && ResolvePrimaryAction() is not null;
    }

    [RelayCommand(CanExecute = nameof(CanInvokeSecondary))]
    private Task InvokeSecondaryAsync()
    {
        return InvokeObsActionAsync(token => _obsIntegration.ToggleRecordPauseAsync(token));
    }

    private bool CanInvokeSecondary()
    {
        return !_actionInFlight
               && IsRecordMode
               && State is ObsOutputCardState.Active or ObsOutputCardState.Paused;
    }

    private async Task InvokeObsActionAsync(Func<CancellationToken, Task> action)
    {
        _actionInFlight = true;
        RaiseCommandStates();

        try
        {
            using var cts = new CancellationTokenSource(ActionTimeout);
            await action(cts.Token);
        }
        catch (OperationCanceledException)
        {
            ShowMetaError("превышено время ожидания");
        }
        catch (Exception exception)
        {
            _logger.ObsOutputActionFailed(exception, Kind);
            ShowMetaError("не удалось выполнить");
        }
        finally
        {
            _actionInFlight = false;
            RaiseCommandStates();
        }
    }

    private void ShowMetaError(string message)
    {
        MetaIsError = true;
        Meta = $"⚠ {message}";
    }

    private void RaiseCommandStates()
    {
        InvokePrimaryCommand.NotifyCanExecuteChanged();
        InvokeSecondaryCommand.NotifyCanExecuteChanged();
    }

    partial void OnStateChanged(ObsOutputCardState value)
    {
        RaiseCommandStates();
    }
}
