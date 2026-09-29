using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed partial class CommandRowViewModel : ObservableObject
{
    private bool _applying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private bool _isEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    [NotifyPropertyChangedFor(nameof(IsTargetInherited))]
    [NotifyPropertyChangedFor(nameof(TargetTooltip))]
    [NotifyPropertyChangedFor(nameof(TargetSummary))]
    private CommandResponseTargetOption _targetOption = CommandResponseTargetOption.Inherit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    [NotifyPropertyChangedFor(nameof(AccessText))]
    [NotifyPropertyChangedFor(nameof(AccessHint))]
    [NotifyPropertyChangedFor(nameof(EffectiveAccessLevel))]
    [NotifyPropertyChangedFor(nameof(HasAccessConflict))]
    [NotifyPropertyChangedFor(nameof(AccessConflictNote))]
    private CommandAccessOption _accessOption = CommandAccessOption.Inherit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveTargetText))]
    [NotifyPropertyChangedFor(nameof(TargetTooltip))]
    [NotifyPropertyChangedFor(nameof(TargetSummary))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private CommandResponseTarget _effectiveTarget = CommandSettings.ChatAndOverlay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCountText))]
    private long _totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StreamCountText))]
    private long _streamCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastUse))]
    [NotifyPropertyChangedFor(nameof(LastUsedAtText))]
    [NotifyPropertyChangedFor(nameof(LastUsedTooltip))]
    [NotifyPropertyChangedFor(nameof(LastUsedSummary))]
    private DateTimeOffset? _lastUsedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastUsedByText))]
    [NotifyPropertyChangedFor(nameof(LastUsedTooltip))]
    [NotifyPropertyChangedFor(nameof(LastUsedSummary))]
    private string _lastUsedBy = string.Empty;

    public CommandRowViewModel(IChatCommand command, string prefix, CommandAccessLevel? codeLevel)
    {
        ArgumentNullException.ThrowIfNull(command);

        Canonical = command.Canonical;
        Description = command.Description;
        CodeLevel = codeLevel;
        IsRestrictedToAllowedUsers = command.IsRestrictedToAllowedUsers;

        Invocation = prefix + command.Canonical;

        var aliases = command.Aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => prefix + alias)
            .ToList();

        Aliases = aliases;
        AliasesText = aliases.Count > 0 ? string.Join(" · ", aliases) : string.Empty;
        HasAliases = aliases.Count > 0;
    }

    public event EventHandler? EnabledChanged;

    public event EventHandler? TargetChanged;

    public event EventHandler? AccessChanged;

    public string Canonical { get; }

    public string Invocation { get; }

    public IReadOnlyList<string> Aliases { get; }

    public string AliasesText { get; }

    public bool HasAliases { get; }

    public string Description { get; }

    public CommandAccessLevel? CodeLevel { get; }

    public bool IsRestrictedToAllowedUsers { get; }

    public bool IsClosedByCode => CodeLevel is null;

    public string CodeAccessText => CommandAccessOption.DescribeSource(CodeLevel);

    public CommandAccessLevel EffectiveAccessLevel => CodeLevel is { } code
        ? CommandAccessLevels.Stricter(code, AccessOption.Level ?? CommandAccessLevel.Everyone)
        : CommandAccessLevel.Broadcaster;

    public bool HasAccessConflict => CodeLevel is { } code
        && AccessOption.Level is { } chosen
        && chosen < code;

    public string? AccessConflictNote => HasAccessConflict
        ? $"Ограничение в коде команды сильнее выбранного: {CodeAccessText.ToLower(UiCulture.Russian)}. Настройка вправе только сузить доступ."
        : null;

    public string AccessText
    {
        get
        {
            if (IsClosedByCode)
            {
                return "Никто";
            }

            var level = CommandAccessOption.Describe(EffectiveAccessLevel);

            if (!IsRestrictedToAllowedUsers)
            {
                return level;
            }

            return EffectiveAccessLevel == CommandAccessLevel.Everyone
                ? "Особый список"
                : $"{level} · особый список";
        }
    }

    public string AccessHint
    {
        get
        {
            var basic = IsClosedByCode
                ? "Команда не отвечает ни зрителю, ни стримеру"
                : EffectiveAccessLevel switch
                {
                    CommandAccessLevel.Broadcaster => "Команду вызывает только владелец канала",
                    CommandAccessLevel.Moderators => "Команда отвечает только стримеру и модераторам канала",
                    _ => "Команду может вызвать любой зритель",
                };

            return IsRestrictedToAllowedUsers
                ? basic + "; по существу она отвечает только тем, кто указан в особом списке – остальным приходит отказ"
                : basic;
        }
    }

    public bool IsTargetInherited => TargetOption.Target is null;

    public string EffectiveTargetText => CommandResponseTargetOption.Describe(EffectiveTarget);

    public string TargetTooltip => IsTargetInherited
        ? $"Как в настройках: {EffectiveTargetText.ToLower(UiCulture.Russian)}"
        : TargetOption.Hint;

    public string TargetSummary => IsTargetInherited
        ? $"Ответ: {EffectiveTargetText.ToLower(UiCulture.Russian)} – как в настройках страницы"
        : $"Ответ: {EffectiveTargetText.ToLower(UiCulture.Russian)}";

    public string TotalCountText => TotalCount.ToString("N0", UiCulture.Russian);

    public string StreamCountText => StreamCount.ToString("N0", UiCulture.Russian);

    public bool HasLastUse => LastUsedAt is not null;

    public string LastUsedAtText => LastUsedAt is { } moment
        ? RelativeTime.DescribeMoment(moment, DateTimeOffset.UtcNow)
        : "–";

    public string LastUsedByText => LastUsedBy.Length > 0 ? LastUsedBy : string.Empty;

    public string LastUsedSummary => HasLastUse
        ? LastUsedBy.Length > 0 ? $"последний вызов, {LastUsedBy}" : "последний вызов"
        : "вызовов не было";

    public string LastUsedTooltip => LastUsedAt is { } moment
        ? $"{RelativeTime.FormatMoment(moment)}{(LastUsedBy.Length > 0 ? $", вызвал {LastUsedBy}" : string.Empty)}"
        : "Команду ещё ни разу не вызывали";

    public string Summary
    {
        get
        {
            var state = IsEnabled ? "включена" : "выключена";

            return $"{Invocation}, {state}, права {AccessText.ToLower(UiCulture.Russian)}, "
                + $"ответ {EffectiveTargetText.ToLower(UiCulture.Russian)}, вызовов {TotalCountText}";
        }
    }

    public string EnabledAutomationName => $"Включить команду {Invocation}";

    public string TargetAutomationName => $"Цель ответа команды {Invocation}";

    public string AccessAutomationName => $"Права на команду {Invocation}";

    public void ApplySettings(CommandSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _applying = true;

        try
        {
            IsEnabled = settings.IsEnabled(Canonical);
            TargetOption = CommandResponseTargetOption.ForCommandTarget(ReadOverride(settings));
            AccessOption = CommandAccessOption.For(settings.ReadAccess(Canonical));
            EffectiveTarget = settings.ResolveResponseTarget(Canonical);
        }
        finally
        {
            _applying = false;
        }
    }

    public void ApplyUsage(CommandUsageRecord? record)
    {
        TotalCount = record?.TotalCount ?? 0;
        StreamCount = record?.StreamCount ?? 0;
        LastUsedAt = record?.LastUsedAt;
        LastUsedBy = record?.LastUsedBy ?? string.Empty;
    }

    public bool Matches(string query)
    {
        if (Invocation.Contains(query, StringComparison.InvariantCultureIgnoreCase)
            || Description.Contains(query, StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        return Aliases.Any(alias => alias.Contains(query, StringComparison.InvariantCultureIgnoreCase));
    }

    public void RefreshLastUsedText()
    {
        OnPropertyChanged(nameof(LastUsedAtText));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (_applying)
        {
            return;
        }

        EnabledChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnTargetOptionChanged(CommandResponseTargetOption value)
    {
        if (_applying)
        {
            return;
        }

        TargetChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnAccessOptionChanged(CommandAccessOption value)
    {
        if (_applying)
        {
            return;
        }

        AccessChanged?.Invoke(this, EventArgs.Empty);
    }

    private CommandResponseTarget? ReadOverride(CommandSettings settings)
    {
        return settings.Commands.TryGetValue(Canonical, out var commandOverride)
            ? commandOverride?.ResponseTarget
            : null;
    }
}
