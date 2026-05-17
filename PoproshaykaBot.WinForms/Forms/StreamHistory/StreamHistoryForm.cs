using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using System.Collections;
using System.Globalization;

namespace PoproshaykaBot.WinForms.Forms.StreamHistory;

public sealed partial class StreamHistoryForm : Form
{
    private const string MissingValuePlaceholder = "—";
    private const int ColumnStarted = 0;

    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    private readonly StreamSessionHistoryStore _historyStore;

    private List<StreamSessionRecord> _sessions = [];
    private bool _initialized;

    public StreamHistoryForm(StreamSessionHistoryStore historyStore)
    {
        _historyStore = historyStore;

        InitializeComponent();
    }

    private StreamHistoryForm()
    {
        InitializeComponent();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_initialized)
        {
            return;
        }

        if (this.IsInDesignMode())
        {
            return;
        }

        _initialized = true;

        listViewSessions.Tag = ColumnStarted;
        listViewSessions.ListViewItemSorter = new ListViewItemComparer(ColumnStarted, SortOrder.Descending);

        UpdateDetails(null);
        LoadData();
    }

    private void buttonRefresh_Click(object sender, EventArgs e)
    {
        LoadData();
    }

    private void listViewSessions_SelectedIndexChanged(object sender, EventArgs e)
    {
        var session = listViewSessions.SelectedItems.Count > 0
            ? listViewSessions.SelectedItems[0].Tag as StreamSessionRecord
            : null;

        UpdateDetails(session);
    }

    private void listViewSessions_ColumnClick(object sender, ColumnClickEventArgs e)
    {
        var nextOrder = SortOrder.Ascending;

        if (listViewSessions.Tag is int currentSortColumn
            && currentSortColumn == e.Column
            && listViewSessions.ListViewItemSorter is ListViewItemComparer { Order: SortOrder.Ascending })
        {
            nextOrder = SortOrder.Descending;
        }

        listViewSessions.Tag = e.Column;
        listViewSessions.ListViewItemSorter = new ListViewItemComparer(e.Column, nextOrder);
    }

    private static string NotEmptyOrPlaceholder(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? MissingValuePlaceholder : value;
    }

    private static string FormatNumber(long value)
    {
        return value.ToString("N0", RussianCulture);
    }

    private static string FormatDateTime(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", RussianCulture);
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span.TotalDays >= 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalDays}д {span.Hours}ч {span.Minutes}м");
        }

        if (span.TotalHours >= 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{span.Hours}ч {span.Minutes}м");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}м {span.Seconds}с");
    }

    private void LoadData()
    {
        _sessions = _historyStore.Load().Sessions;

        var selectedId = listViewSessions.SelectedItems.Count > 0
            ? (listViewSessions.SelectedItems[0].Tag as StreamSessionRecord)?.Id
            : null;

        listViewSessions.BeginUpdate();
        listViewSessions.Items.Clear();

        foreach (var session in _sessions)
        {
            var item = new ListViewItem(FormatDateTime(session.StartedAt))
            {
                Tag = session,
            };

            item.SubItems.Add(FormatDuration(session.Duration));
            item.SubItems.Add(string.IsNullOrEmpty(session.Game) ? MissingValuePlaceholder : session.Game);
            item.SubItems.Add(session.MessageCount.ToString(CultureInfo.CurrentCulture));
            item.SubItems.Add(session.ChatterCount.ToString(CultureInfo.CurrentCulture));
            item.SubItems.Add(session.PeakViewers.ToString(CultureInfo.CurrentCulture));
            item.SubItems.Add(session.AverageViewers.ToString(CultureInfo.CurrentCulture));

            listViewSessions.Items.Add(item);

            if (selectedId != null && session.Id == selectedId)
            {
                item.Selected = true;
            }
        }

        listViewSessions.EndUpdate();

        UpdateSummary();

        if (listViewSessions.SelectedItems.Count > 0)
        {
            listViewSessions.SelectedItems[0].EnsureVisible();
        }
        else
        {
            UpdateDetails(null);
        }
    }

    private void UpdateSummary()
    {
        var totalDuration = _sessions.Aggregate(TimeSpan.Zero, (sum, session) => sum + session.Duration);

        labelSummary.Text = string.Create(CultureInfo.InvariantCulture, $"🎬 Стримов: {_sessions.Count:N0} · Общее время: {FormatDuration(totalDuration)}");
    }

    private void UpdateDetails(StreamSessionRecord? session)
    {
        labelTitle.Text = $"📝 Заголовок: {NotEmptyOrPlaceholder(session?.Title)}";
        labelGame.Text = $"🎮 Игра: {NotEmptyOrPlaceholder(session?.Game)}";
        labelStarted.Text = $"▶ Начало: {(session != null ? FormatDateTime(session.StartedAt) : MissingValuePlaceholder)}";
        labelEnded.Text = $"⏹ Окончание: {(session != null ? FormatDateTime(session.EndedAt) : MissingValuePlaceholder)}";
        labelDuration.Text = $"⏱️ Длительность: {(session != null ? FormatDuration(session.Duration) : MissingValuePlaceholder)}";
        labelMessages.Text = $"💬 Сообщений: {(session != null ? FormatNumber(session.MessageCount) : MissingValuePlaceholder)}";
        labelChatters.Text = $"👥 Чаттеров: {(session != null ? FormatNumber(session.ChatterCount) : MissingValuePlaceholder)}";
        labelViewers.Text = session != null
            ? $"👁 Зрители: пик {FormatNumber(session.PeakViewers)} / средн. {FormatNumber(session.AverageViewers)}"
            : "👁 Зрители: пик — / средн. —";

        listViewChatters.BeginUpdate();
        listViewChatters.Items.Clear();

        if (session != null)
        {
            foreach (var chatter in session.Chatters)
            {
                var item = new ListViewItem(chatter.DisplayName)
                {
                    Tag = chatter,
                };

                item.SubItems.Add(chatter.MessageCount.ToString(CultureInfo.CurrentCulture));
                listViewChatters.Items.Add(item);
            }
        }

        listViewChatters.EndUpdate();
    }

    private sealed class ListViewItemComparer(int column, SortOrder order) : IComparer
    {
        public SortOrder Order { get; } = order;

        public int Compare(object? x, object? y)
        {
            var itemX = (ListViewItem)x!;
            var itemY = (ListViewItem)y!;

            int result;

            if (itemX.Tag is StreamSessionRecord sessionX && itemY.Tag is StreamSessionRecord sessionY)
            {
                result = column switch
                {
                    1 => sessionX.Duration.CompareTo(sessionY.Duration),
                    2 => string.Compare(sessionX.Game, sessionY.Game, StringComparison.OrdinalIgnoreCase),
                    3 => sessionX.MessageCount.CompareTo(sessionY.MessageCount),
                    4 => sessionX.ChatterCount.CompareTo(sessionY.ChatterCount),
                    5 => sessionX.PeakViewers.CompareTo(sessionY.PeakViewers),
                    6 => sessionX.AverageViewers.CompareTo(sessionY.AverageViewers),
                    _ => sessionX.StartedAt.CompareTo(sessionY.StartedAt),
                };
            }
            else
            {
                result = string.CompareOrdinal(itemX.SubItems[column].Text, itemY.SubItems[column].Text);
            }

            return Order == SortOrder.Descending ? -result : result;
        }
    }
}
