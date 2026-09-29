using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Twitch.Chat;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed partial class CommandsPageViewModel : ObservableObject, IPageHeader, IUnsavedChangesPage
{
    private const string WhisperReauthorizationNotice = "Шёпот необязателен. Пока у бота нет разрешения на личные сообщения, он отвечает на такие команды в общем чате – ответом на сообщение зрителя. Чтобы включить шёпот, откройте в настройках раздел «Авторизация». Добавьте user:manage:whispers в права бота и авторизуйте бота заново.";

    private const string NotWrittenNotice = "Параметры применены и работают до перезапуска. В файл они не записаны: туда только что перенесены данные предыдущей версии. Перезапустите приложение и сохраните параметры ещё раз.";

    private static readonly CommandContext ViewerProbe = new()
    {
        Username = "viewer",
        DisplayName = "viewer",
    };

    private static readonly CommandContext ModeratorProbe = new()
    {
        Username = "moderator",
        DisplayName = "moderator",
        IsModerator = true,
    };

    private static readonly CommandContext BroadcasterProbe = new()
    {
        Username = "broadcaster",
        DisplayName = "broadcaster",
        IsBroadcaster = true,
    };

    private readonly CommandSettingsStore _settingsStore;
    private readonly SettingsManager _settingsManager;
    private readonly CommandUsageRepository _usageRepository;
    private readonly IUnsavedChangesPrompt _unsavedChangesPrompt;
    private readonly AccountsStore _accountsStore;
    private readonly ILogger<CommandsPageViewModel> _logger;
    private readonly List<CommandRowViewModel> _allRows = [];
    private readonly ObservableCollection<CommandRowViewModel> _rows = [];
    private readonly Dictionary<string, IChatCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    private bool _applying;
    private bool _restoringRow;
    private bool _rebuildingView;
    private CommandRowViewModel? _parametersRow;

    [ObservableProperty]
    private CommandRowViewModel? _selectedRow;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyActionCommand))]
    private bool _isFilterActive;

    [ObservableProperty]
    private CommandResponseTargetOption _defaultTargetOption = CommandResponseTargetOption.Both;

    [ObservableProperty]
    private string _summaryLine = string.Empty;

    [ObservableProperty]
    private bool _hasUsage;

    [ObservableProperty]
    private string? _notice;

    [ObservableProperty]
    private string? _whisperNotice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasParameters))]
    private CommandParametersViewModel? _parameters;

    [ObservableProperty]
    private string? _parametersNotice;

    [ObservableProperty]
    private CommandNoticeTone _parametersNoticeTone = CommandNoticeTone.Info;

    public CommandsPageViewModel(
        ChatCommandProcessor processor,
        CommandSettingsStore settingsStore,
        SettingsManager settingsManager,
        CommandUsageRepository usageRepository,
        IUnsavedChangesPrompt unsavedChangesPrompt,
        AccountsStore accountsStore,
        ILogger<CommandsPageViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(processor);

        _accountsStore = accountsStore;
        _settingsStore = settingsStore;
        _settingsManager = settingsManager;
        _usageRepository = usageRepository;
        _unsavedChangesPrompt = unsavedChangesPrompt;
        _logger = logger;

        var byName = StringComparer.Create(UiCulture.Russian, ignoreCase: true);

        foreach (var command in processor.GetAllCommands().OrderBy(command => command.Canonical, byName))
        {
            var row = new CommandRowViewModel(command, processor.Prefix, ProbeCodeLevel(command));
            row.EnabledChanged += OnRowEnabledChanged;
            row.TargetChanged += OnRowTargetChanged;
            row.AccessChanged += OnRowAccessChanged;

            _allRows.Add(row);
            _commands[command.Canonical] = command;
        }

        Reload();
    }

    public string PageTitle => "Команды";

    public string? PageDescription => "Права, цель ответа, параметры и статистика вызовов";

    public ObservableCollection<CommandRowViewModel> Rows => _rows;

    public IReadOnlyList<CommandResponseTargetOption> CommandTargetOptions => CommandResponseTargetOption.ForCommand;

    public IReadOnlyList<CommandResponseTargetOption> DefaultTargetOptions => CommandResponseTargetOption.ForDefault;

    public IReadOnlyList<CommandAccessOption> AccessOptions => CommandAccessOption.All;

    public bool HasCommands => _rows.Count > 0;

    public bool HasSelectedRow => SelectedRow is not null;

    public bool HasParameters => Parameters is not null;

    public string EmptyHeading => IsFilterActive ? "Ничего не найдено" : "Команд нет";

    public string EmptyDescription => IsFilterActive
        ? "Ни одна команда не подходит под поиск. Очистите его, чтобы увидеть весь список."
        : "Ни одной команды чата не зарегистрировано.";

    public string? EmptyActionText => "Сбросить поиск";

    public IRelayCommand? EmptyActionCommand => IsFilterActive ? ClearFilterCommand : null;

    public string UsageHint => HasUsage
        ? string.Empty
        : "Счётчики вызовов читаются при подключении бота – до него они пустые";

    public bool HasUnsavedChanges => Parameters is { IsDirty: true };

    public string UnsavedChangesSubject => _parametersRow is { } row
        ? $"Параметры команды {row.Invocation} изменены, но не сохранены."
        : "Параметры команды изменены, но не сохранены.";

    public void OnEnter()
    {
        if (HasUnsavedChanges)
        {
            ReloadRows();
            return;
        }

        Reload();
    }

    public Task<bool> TrySaveUnsavedChangesAsync()
    {
        return Task.FromResult(TrySaveParameters());
    }

    public void DiscardUnsavedChanges()
    {
        ReloadParameters();
    }

    public void RefreshUsage()
    {
        var usage = _usageRepository
            .GetSnapshot()
            .ToDictionary(record => record.Canonical, StringComparer.OrdinalIgnoreCase);

        foreach (var row in _allRows)
        {
            row.ApplyUsage(usage.GetValueOrDefault(row.Canonical));
            row.RefreshLastUsedText();
        }

        HasUsage = usage.Count > 0;
        OnPropertyChanged(nameof(UsageHint));

        UpdateSummaries();
    }

    public bool TrySelect(string canonical)
    {
        var row = _rows.FirstOrDefault(item => string.Equals(item.Canonical, canonical, StringComparison.OrdinalIgnoreCase));

        if (row is null)
        {
            return false;
        }

        SelectedRow = row;

        return true;
    }

    private static CommandAccessLevel? ProbeCodeLevel(IChatCommand command)
    {
        if (command.CanExecute(ViewerProbe))
        {
            return CommandAccessLevel.Everyone;
        }

        if (command.CanExecute(ModeratorProbe))
        {
            return CommandAccessLevel.Moderators;
        }

        return command.CanExecute(BroadcasterProbe) ? CommandAccessLevel.Broadcaster : null;
    }

    private static CommandOverride GetOrAddOverride(CommandSettings settings, string canonical)
    {
        if (settings.Commands.TryGetValue(canonical, out var existing) && existing is not null)
        {
            return existing;
        }

        var created = new CommandOverride();
        settings.Commands[canonical] = created;

        return created;
    }

    private static void DropRedundantOverride(CommandSettings settings, string canonical)
    {
        if (settings.Commands.TryGetValue(canonical, out var existing)
            && existing is { Enabled: true, ResponseTarget: null, Access: null })
        {
            settings.Commands.Remove(canonical);
        }
    }

    private void Reload()
    {
        ReloadRows();
        ReloadParameters();
    }

    private void ReloadRows()
    {
        var settings = _settingsStore.Load();

        _applying = true;

        try
        {
            DefaultTargetOption = CommandResponseTargetOption.ForDefaultTarget(settings.DefaultResponseTarget);

            foreach (var row in _allRows)
            {
                row.ApplySettings(settings);
            }
        }
        finally
        {
            _applying = false;
        }

        RefreshUsage();
        RefreshWhisperNotice();

        RebuildView();
    }

    partial void OnFilterTextChanged(string value)
    {
        RebuildView();
    }

    partial void OnSelectedRowChanged(CommandRowViewModel? value)
    {
        if (_restoringRow || _rebuildingView)
        {
            return;
        }

        OnPropertyChanged(nameof(HasSelectedRow));

        if (!ConfirmParametersDraft("переключением на другую команду"))
        {
            RestoreParametersRow();
            return;
        }

        ReloadParameters();
    }

    partial void OnParametersChanging(CommandParametersViewModel? value)
    {
        if (Parameters is not null)
        {
            Parameters.PropertyChanged -= OnParametersPropertyChanged;
        }
    }

    partial void OnParametersChanged(CommandParametersViewModel? value)
    {
        if (value is not null)
        {
            value.PropertyChanged += OnParametersPropertyChanged;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        SaveParametersCommand.NotifyCanExecuteChanged();
    }

    partial void OnDefaultTargetOptionChanged(CommandResponseTargetOption value)
    {
        if (_applying || value.Target is not { } target)
        {
            return;
        }

        if (!TryMutate(settings => settings.DefaultResponseTarget = target))
        {
            return;
        }

        _logger.CommandDefaultTargetChanged(value.Title);

        RefreshEffectiveTargets();
    }

    private void OnParametersPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (string.Equals(args.PropertyName, nameof(CommandParametersViewModel.IsDirty), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveParametersCommand.NotifyCanExecuteChanged();
        }
    }

    private void ReloadParameters()
    {
        ParametersNotice = null;
        ParametersNoticeTone = CommandNoticeTone.Info;
        _parametersRow = SelectedRow;

        if (SelectedRow is null || !_commands.TryGetValue(SelectedRow.Canonical, out var command))
        {
            Parameters = null;
            return;
        }

        var parameters = CommandParametersViewModel.TryCreate(command);

        parameters?.Load(_settingsManager.Current);

        Parameters = parameters;
    }

    private bool ConfirmParametersDraft(string action)
    {
        if (!HasUnsavedChanges)
        {
            return true;
        }

        return _unsavedChangesPrompt.Ask(UnsavedChangesSubject, action) switch
        {
            UnsavedChangesDecision.Save => TrySaveParameters(),
            UnsavedChangesDecision.Discard => true,
            _ => false,
        };
    }

    private void RestoreParametersRow()
    {
        _restoringRow = true;

        try
        {
            SelectedRow = _parametersRow;
        }
        finally
        {
            _restoringRow = false;
        }

        OnPropertyChanged(nameof(HasSelectedRow));
    }

    private void OnRowEnabledChanged(object? sender, EventArgs args)
    {
        if (sender is not CommandRowViewModel row)
        {
            return;
        }

        var enabled = row.IsEnabled;

        if (!TryMutate(settings =>
            {
                GetOrAddOverride(settings, row.Canonical).Enabled = enabled;
                DropRedundantOverride(settings, row.Canonical);
            }))
        {
            return;
        }

        _logger.CommandEnabledChanged(row.Invocation, enabled ? "включена" : "выключена");

        UpdateSummaries();
    }

    private void OnRowTargetChanged(object? sender, EventArgs args)
    {
        if (sender is not CommandRowViewModel row)
        {
            return;
        }

        var target = row.TargetOption.Target;

        if (!TryMutate(settings =>
            {
                GetOrAddOverride(settings, row.Canonical).ResponseTarget = target;
                DropRedundantOverride(settings, row.Canonical);
            }))
        {
            return;
        }

        _logger.CommandTargetChanged(row.Invocation, row.TargetOption.Title);

        RefreshEffectiveTargets();
    }

    private void OnRowAccessChanged(object? sender, EventArgs args)
    {
        if (sender is not CommandRowViewModel row)
        {
            return;
        }

        var access = row.AccessOption.Level;

        if (!TryMutate(settings =>
            {
                GetOrAddOverride(settings, row.Canonical).Access = access;
                DropRedundantOverride(settings, row.Canonical);
            }))
        {
            return;
        }

        _logger.CommandAccessChanged(row.Invocation, row.AccessOption.Title);

        UpdateSummaries();
    }

    private bool TryMutate(Action<CommandSettings> mutator)
    {
        try
        {
            _settingsStore.Mutate(mutator);
            Notice = null;

            return true;
        }
        catch (Exception exception)
        {
            _logger.CommandSettingsSaveFailed(exception);
            Notice = "Настройки команд не сохранились – правка отменена. Подробности в журнале.";
            ReloadRows();

            return false;
        }
    }

    private void RefreshEffectiveTargets()
    {
        var settings = _settingsStore.Load();

        _applying = true;

        try
        {
            foreach (var row in _allRows)
            {
                row.ApplySettings(settings);
            }
        }
        finally
        {
            _applying = false;
        }

        RefreshWhisperNotice();
    }

    private void RefreshWhisperNotice()
    {
        var whispers = _allRows.Any(row => row.EffectiveTarget.WhispersToCaller());

        WhisperNotice = whispers && !TwitchScopes.IsGranted(_accountsStore.LoadBot(), TwitchScopes.UserManageWhispers)
            ? WhisperReauthorizationNotice
            : null;
    }

    private void RebuildView()
    {
        var selected = SelectedRow?.Canonical;
        var query = FilterText.Trim();
        var hasFilter = query.Length > 0;

        _rebuildingView = true;

        try
        {
            _rows.Clear();

            foreach (var row in _allRows)
            {
                if (hasFilter && !row.Matches(query) && !IsEditedRow(row))
                {
                    continue;
                }

                row.RefreshLastUsedText();
                _rows.Add(row);
            }

            SelectedRow = _rows.FirstOrDefault(row => string.Equals(row.Canonical, selected, StringComparison.OrdinalIgnoreCase))
                          ?? _rows.FirstOrDefault();
        }
        finally
        {
            _rebuildingView = false;
        }

        OnPropertyChanged(nameof(HasSelectedRow));

        if (!ReferenceEquals(SelectedRow, _parametersRow))
        {
            ReloadParameters();
        }

        IsFilterActive = hasFilter;

        UpdateSummaries();

        OnPropertyChanged(nameof(HasCommands));
        OnPropertyChanged(nameof(EmptyHeading));
        OnPropertyChanged(nameof(EmptyDescription));
    }

    private bool IsEditedRow(CommandRowViewModel row)
    {
        return HasUnsavedChanges && ReferenceEquals(row, _parametersRow);
    }

    private void UpdateSummaries()
    {
        var enabled = _allRows.Count(row => row.IsEnabled);
        var total = _allRows.Sum(row => row.TotalCount).ToString("N0", UiCulture.Russian);
        var stream = _allRows.Sum(row => row.StreamCount).ToString("N0", UiCulture.Russian);
        var visible = IsFilterActive ? $" · показано {_rows.Count}" : string.Empty;

        SummaryLine = $"Включено {enabled} из {_allRows.Count} · вызовов {total}, за текущий стрим {stream}{visible}";
    }

    private bool CanSaveParameters()
    {
        return Parameters is { IsDirty: true };
    }

    [RelayCommand(CanExecute = nameof(CanSaveParameters))]
    private void SaveParameters()
    {
        TrySaveParameters();
    }

    private bool TrySaveParameters()
    {
        if (Parameters is not { } parameters || _parametersRow is not { } row)
        {
            return true;
        }

        if (!parameters.Validate())
        {
            ShowParametersNotice("Проверьте выделенные поля – параметры не сохранены.", CommandNoticeTone.Error);

            return false;
        }

        bool written;

        try
        {
            written = _settingsManager.Mutate(parameters.Apply);
        }
        catch (Exception exception)
        {
            _logger.CommandParametersSaveFailed(exception, row.Invocation);
            ShowParametersNotice("Параметры не сохранились. Подробности в журнале.", CommandNoticeTone.Error);

            return false;
        }

        parameters.Load(_settingsManager.Current);

        if (written)
        {
            _logger.CommandParametersSaved(row.Invocation);
            ShowParametersNotice("Параметры сохранены", CommandNoticeTone.Info);
        }
        else
        {
            _logger.CommandParametersNotWritten(row.Invocation);
            ShowParametersNotice(NotWrittenNotice, CommandNoticeTone.Warning);
        }

        SaveParametersCommand.NotifyCanExecuteChanged();

        return true;
    }

    private void ShowParametersNotice(string text, CommandNoticeTone tone)
    {
        ParametersNoticeTone = tone;
        ParametersNotice = text;
    }

    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
    }

    [RelayCommand]
    private void Refresh()
    {
        if (!ConfirmParametersDraft("перечитыванием"))
        {
            return;
        }

        Reload();
    }
}
