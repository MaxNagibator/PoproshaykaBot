namespace PoproshaykaBot.Wpf.Bootstrap;

public static class SettingsKeys
{
    public const string ShowPageHeader = "ui.shell.show_page_header";
    public const string FontScale = "ui.shell.font_scale";
    public const string EnableToastNotifications = "ui.notifications.toast";
    public const string NavCollapsed = "ui.shell.nav_collapsed";
    public const string LastPage = "ui.shell.last_page";
    public const string SettingsSection = "ui.settings.section";
    public const string LegacyImportDismissed = "ui.migration.legacy_import_dismissed";

    public const string StreamTrendMetric = "ui.streams.trend_metric";
    public const string StreamTrendLength = "ui.streams.trend_length";
    public const string StreamTrendVisible = "ui.streams.trend_visible";
    public const string StreamSummaryExpanded = "ui.streams.summary_expanded";
    public const string StreamListView = "ui.streams.list_view";
    public const string StreamSortKey = "ui.streams.sort_key";
    public const string StreamSortDescending = "ui.streams.sort_descending";

    public const string UserBalanceExpanded = "ui.users.balance_expanded";

    public const string WindowLeft = "ui.window.left";
    public const string WindowTop = "ui.window.top";
    public const string WindowWidth = "ui.window.width";
    public const string WindowHeight = "ui.window.height";
    public const string WindowMaximized = "ui.window.maximized";

    public static string Theme => ThemeManager.SettingsKeyName;
}
