using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using PoproshaykaBot.Wpf.Views;
using Serilog;
using System.Windows;

namespace PoproshaykaBot.Wpf.Bootstrap;

public sealed class GalleryHost : IGalleryHost
{
    public const string ArgumentName = "--gallery";

    private const string PreviewCommandWithParameters = "донат";
    private const string PreviewCommandWithoutParameters = "помощь";

    private readonly IServiceProvider _services;
    private readonly ShellViewModel _shell;
    private readonly Window _window;

    private GalleryHost(IServiceProvider services, IReadOnlyList<string> pages)
    {
        _services = services;
        _shell = services.GetRequiredService<ShellViewModel>();
        _window = services.GetRequiredService<MainWindow>();

        Cases = [.. pages.Select(page => new GalleryCase(GalleryDialogs.IsDialogKey(page) ? GalleryDialogs.Kind : GalleryCase.PageKind, page))];
    }

    public string AppName => AppInfo.Name;

    public string AppVersion => AppInfo.Version;

    public Window Window => _window;

    public IReadOnlyList<GalleryCase> Cases { get; }

    private static Serilog.ILogger HostLog => Log.ForContext<GalleryHost>();

    public static GalleryArguments Parse(IEnumerable<string> args, string defaultDirectory)
    {
        var defaults = new GalleryDefaults
        {
            Directory = defaultDirectory,
            Themes = [AppThemes.LightKey, AppThemes.DarkKey],
            Width = AppDefaults.GalleryWidthDefault,
            Height = AppDefaults.GalleryHeightDefault,
        };

        return GalleryArguments.Parse(args, defaults, ResolvePages);
    }

    public static GalleryHost Create(IServiceProvider services, GalleryArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return new(services, arguments.Pages);
    }

    public async Task ArrangeAsync()
    {
        _shell.OnboardingBanner.Suppress();

        await StatisticsBootstrap.LoadUserStatisticsAsync(_services).ConfigureAwait(true);

        try
        {
            var bot = await _services.GetRequiredService<StatisticsFileStore>().LoadBotAsync().ConfigureAwait(true);

            if (bot.Value is { } statistics)
            {
                _services.GetRequiredService<IBotStatisticsRepository>().Replace(statistics);
            }

            _services.GetRequiredService<UserStatisticsPageViewModel>().RefreshCommand.Execute(null);
        }
        catch (Exception exception)
        {
            HostLog.Warning(exception, "Статистика бота не прочитана – плитки со счётчиками бота останутся пустыми");
        }

        try
        {
            var usage = await _services.GetRequiredService<StatisticsFileStore>().LoadCommandUsageAsync().ConfigureAwait(true);

            if (usage.Value is { } records)
            {
                _services.GetRequiredService<CommandUsageRepository>().Replace(records);
            }
        }
        catch (Exception exception)
        {
            HostLog.Warning(exception, "Статистика команд не прочитана – страница «Команды» останется без счётчиков");
        }

        await _services.GetRequiredService<StreamHistoryPageViewModel>().BoxArtLoading.ConfigureAwait(true);
    }

    public async Task<GalleryShot> CaptureAsync(GalleryCase item, GalleryContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        if (GalleryDialogs.Find(item.Name) is { } dialog)
        {
            return await CaptureDialogAsync(dialog, context).ConfigureAwait(true);
        }

        if (SectionKeys.IsSelectedCase(item.Name) || SectionKeys.IsParamsCase(item.Name))
        {
            return await CaptureSelectedAsync(item.Name, context).ConfigureAwait(true);
        }

        if (SectionKeys.IsCardsCase(item.Name))
        {
            return await CaptureCardsAsync(item.Name, context).ConfigureAwait(true);
        }

        if (SectionKeys.IsHiddenCase(item.Name))
        {
            return await CaptureHiddenAsync(item.Name, context).ConfigureAwait(true);
        }

        if (GalleryObs.Find(item.Name) is { } obs)
        {
            return await CaptureObsAsync(item.Name, obs, context).ConfigureAwait(true);
        }

        if (SectionKeys.IsEditCase(item.Name))
        {
            return await CaptureEditAsync(item.Name, context).ConfigureAwait(true);
        }

        Navigate(item.Name);

        await context.SettleAsync().ConfigureAwait(true);

        return context.Save(ViewCapture.Slug(item.Name));
    }

    private static IReadOnlyList<string> ResolvePages(IReadOnlyList<string> requested, ICollection<string> unknown)
    {
        if (requested.Count == 0)
        {
            return SectionKeys.All;
        }

        var pages = new List<string>();

        foreach (var page in requested)
        {
            var match = Canonical(page);

            if (match is null)
            {
                unknown.Add(page);
                continue;
            }

            if (!pages.Contains(match, StringComparer.Ordinal))
            {
                pages.Add(match);
            }
        }

        return pages.Count > 0 ? pages : SectionKeys.All;
    }

    private static string? Canonical(string requested)
    {
        if (GalleryDialogs.IsDialogKey(requested))
        {
            return GalleryDialogs.Find(requested)?.Key;
        }

        return SectionKeys.All
            .Concat(SectionKeys.Selected)
            .Concat(SectionKeys.Cards)
            .Concat(SectionKeys.Hidden)
            .Concat(SectionKeys.Params)
            .Concat(SectionKeys.Obs)
            .Concat(SectionKeys.Edit)
            .FirstOrDefault(known => string.Equals(known, requested, StringComparison.OrdinalIgnoreCase));
    }

    private static int DetailedSessionIndex(IReadOnlyList<StreamSessionRowViewModel> sessions)
    {
        var detailed = IndexOf(sessions, session => session.Segments.Count > 1 && session.Chatters.Count > 0);

        return detailed >= 0 ? detailed : Math.Max(IndexOf(sessions, session => session.Chatters.Count > 0), 0);
    }

    private static int IndexOf(IReadOnlyList<StreamSessionRowViewModel> sessions, Func<StreamSessionRecord, bool> predicate)
    {
        for (var index = 0; index < sessions.Count; index++)
        {
            if (predicate(sessions[index].Source))
            {
                return index;
            }
        }

        return -1;
    }

    private static void WarnWhenNothingSelected(string name, bool selected)
    {
        if (!selected)
        {
            HostLog.Warning("Кейс «{Case}» снят без выбранной строки – в профиле нет подходящих записей", name);
        }
    }

    private async Task<GalleryShot> CaptureSelectedAsync(string name, GalleryContext context)
    {
        Navigate(SectionKeys.PageOf(name));

        await context.SettleAsync().ConfigureAwait(true);

        var clearSelection = SelectPreviewRow(name);

        try
        {
            await context.SettleAsync().ConfigureAwait(true);

            return context.Save(ViewCapture.Slug(name));
        }
        finally
        {
            clearSelection();

            await context.SettleAsync().ConfigureAwait(true);
        }
    }

    private async Task<GalleryShot> CaptureCardsAsync(string name, GalleryContext context)
    {
        Navigate(SectionKeys.PageOf(name));

        var streams = _services.GetRequiredService<StreamHistoryPageViewModel>();
        var wasTable = streams.IsTableView;

        streams.IsCardsView = true;

        await context.SettleAsync().ConfigureAwait(true);

        WarnWhenNothingSelected(name, streams.TrySelectAt(DetailedSessionIndex(streams.Sessions)));

        try
        {
            await context.SettleAsync().ConfigureAwait(true);

            return context.Save(ViewCapture.Slug(name));
        }
        finally
        {
            streams.SelectedRow = null;
            streams.IsTableView = wasTable;

            await context.SettleAsync().ConfigureAwait(true);
        }
    }

    private async Task<GalleryShot> CaptureHiddenAsync(string name, GalleryContext context)
    {
        Navigate(SectionKeys.PageOf(name));

        var streams = _services.GetRequiredService<StreamHistoryPageViewModel>();

        if (streams.HiddenCount == 0)
        {
            HostLog.Warning("Кейс «{Case}» снят без убранных стримов – в профиле нет таких записей", name);
        }

        streams.ShowHidden = true;

        try
        {
            await context.SettleAsync().ConfigureAwait(true);

            return context.Save(ViewCapture.Slug(name));
        }
        finally
        {
            streams.ShowHidden = false;

            await context.SettleAsync().ConfigureAwait(true);
        }
    }

    private async Task<GalleryShot> CaptureEditAsync(string name, GalleryContext context)
    {
        Navigate(SectionKeys.PageOf(name));

        await context.SettleAsync().ConfigureAwait(true);

        var dashboard = _services.GetRequiredService<DashboardViewModel>();

        if (!dashboard.CanEdit)
        {
            HostLog.Warning("Кейс «{Case}» снят без режима правки – раскладка без дерева или ушла в стопку", name);
        }
        else if (!dashboard.IsEditing)
        {
            dashboard.ToggleEditCommand.Execute(null);
        }

        try
        {
            await context.SettleAsync().ConfigureAwait(true);

            return context.Save(ViewCapture.Slug(name));
        }
        finally
        {
            dashboard.StopEditing();

            await context.SettleAsync().ConfigureAwait(true);
        }
    }

    private async Task<GalleryShot> CaptureObsAsync(string name, ObsDashboardSnapshot snapshot, GalleryContext context)
    {
        Navigate(SectionKeys.PageOf(name));

        var tile = _services.GetServices<DashboardTileViewModel>().OfType<ObsInfoTileViewModel>().Single();

        tile.BeginPreview(snapshot);

        try
        {
            await context.SettleAsync().ConfigureAwait(true);

            return context.Save(ViewCapture.Slug(name));
        }
        finally
        {
            tile.EndPreview();

            await context.SettleAsync().ConfigureAwait(true);
        }
    }

    private Action SelectPreviewRow(string name)
    {
        if (string.Equals(name, SectionKeys.UsersSelected, StringComparison.OrdinalIgnoreCase))
        {
            var users = _services.GetRequiredService<UserStatisticsPageViewModel>();

            WarnWhenNothingSelected(name, users.TrySelectAt(0));

            return () => users.SelectedRow = null;
        }

        if (string.Equals(name, SectionKeys.StreamsSelected, StringComparison.OrdinalIgnoreCase))
        {
            var streams = _services.GetRequiredService<StreamHistoryPageViewModel>();

            WarnWhenNothingSelected(name, streams.TrySelectAt(DetailedSessionIndex(streams.Sessions)));

            return () => streams.SelectedRow = null;
        }

        if (string.Equals(name, SectionKeys.CommandsSelected, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, SectionKeys.CommandsParams, StringComparison.OrdinalIgnoreCase))
        {
            var commands = _services.GetRequiredService<CommandsPageViewModel>();
            var canonical = string.Equals(name, SectionKeys.CommandsParams, StringComparison.OrdinalIgnoreCase)
                ? PreviewCommandWithParameters
                : PreviewCommandWithoutParameters;

            WarnWhenNothingSelected(name, commands.TrySelect(canonical));

            return () => commands.SelectedRow = null;
        }

        throw new InvalidOperationException($"Кейс «{name}» не умеет выбирать строку.");
    }

    private async Task<GalleryShot> CaptureDialogAsync(GalleryDialogCase item, GalleryContext context)
    {
        Navigate(item.Page);

        if (item.SettingsSection is { } section)
        {
            _services.GetRequiredService<SettingsPageViewModel>().Sections.Restore(section);
        }

        using var scope = _services.CreateScope();
        var dialog = item.Create(scope.ServiceProvider);

        try
        {
            _ = _shell.Modal.ShowAsync(dialog);

            await context.SettleAsync().ConfigureAwait(true);

            return context.Save(ViewCapture.Slug(item.Key));
        }
        finally
        {
            _shell.Modal.RequestCancel();

            await context.SettleAsync().ConfigureAwait(true);
        }
    }

    private void Navigate(string page)
    {
        if (string.Equals(page, SectionKeys.Settings, StringComparison.OrdinalIgnoreCase))
        {
            _shell.OpenSettingsCommand.Execute(null);

            return;
        }

        if (_shell.FindSectionByKey(page) is not { } section)
        {
            throw new InvalidOperationException($"Страница «{page}» не открылась – её нет в навигации.");
        }

        _shell.Selected = section;
    }
}
