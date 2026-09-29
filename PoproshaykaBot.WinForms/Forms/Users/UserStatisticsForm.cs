using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Users;
using PoproshaykaBot.WinForms.Infrastructure;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using System.Collections;

namespace PoproshaykaBot.WinForms.Forms.Users;

public sealed partial class UserStatisticsForm : Form
{
    private const int ColumnMessages = 1;
    private const int ColumnPoints = 2;

    private readonly IUserStatisticsRepository _userStatistics;
    private readonly StatisticsAutoSaver _statisticsAutoSaver;
    private readonly UserRankService _userRankService;
    private readonly UserPointsManagementService _userMessagesManagementService;
    private readonly IChannelProvider _channelProvider;
    private readonly SettingsManager _settingsManager;

    private List<UserStatistics> _allUsers = [];
    private bool _initialized;

    public UserStatisticsForm(
        IUserStatisticsRepository userStatistics,
        StatisticsAutoSaver statisticsAutoSaver,
        UserRankService userRankService,
        UserPointsManagementService userMessagesManagementService,
        IChannelProvider channelProvider,
        SettingsManager settingsManager)
    {
        _userStatistics = userStatistics;
        _statisticsAutoSaver = statisticsAutoSaver;
        _userRankService = userRankService;
        _userMessagesManagementService = userMessagesManagementService;
        _channelProvider = channelProvider;
        _settingsManager = settingsManager;

        InitializeComponent();
    }

    private UserStatisticsForm()
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

        listViewUsers.Tag = ColumnPoints;
        listViewUsers.ListViewItemSorter = new ListViewItemComparer(ColumnPoints, SortOrder.Descending);

        UpdateDetails(null);
        UpdateActionState();
        LoadData();
    }

    private void textBoxFilter_TextChanged(object sender, EventArgs e)
    {
        ApplyFilter();
    }

    private void listViewUsers_SelectedIndexChanged(object sender, EventArgs e)
    {
        var user = listViewUsers.SelectedItems.Count > 0
            ? listViewUsers.SelectedItems[0].Tag as UserStatistics
            : null;

        UpdateDetails(user);
        UpdateActionState();
    }

    private async void buttonAction_Click(object sender, EventArgs e)
    {
        if (listViewUsers.SelectedItems.Count == 0 || listViewUsers.SelectedItems[0].Tag is not UserStatistics user)
        {
            MessageBox.Show("⚠️ Не выбран пользователь.", "⚠️ Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var delta = (long)numericIncrement.Value;

        if (delta == 0)
        {
            UpdateActionState();
            return;
        }

        bool updated;

        try
        {
            updated = await ApplyAdjustmentAsync(user, delta);
        }
        catch (Exception exception)
        {
            var verb = delta < 0 ? "наказать" : "поощрить";
            MessageBox.Show($"Не удалось {verb} пользователя: {exception.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!updated)
        {
            MessageBox.Show("🔍 Пользователь не найден в статистике.", "🔍 Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        UpdateDetails(user);
        RefreshUserList();

        try
        {
            await _statisticsAutoSaver.SaveNowAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"💾 Не удалось сохранить статистику: {ex.Message}", "💾 Ошибка сохранения", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ButtonClearFilterOnClick(object? sender, EventArgs e)
    {
        textBoxFilter.Text = string.Empty;
    }

    private void ButtonPointTermOnClick(object? sender, EventArgs e)
    {
        using var dialog = new PointTermDialog();
        OffScreenForm.PrepareIfHeadless(dialog);
        dialog.LoadFrom(_settingsManager.Current.Ranks.PointTerm);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _settingsManager.Mutate(settings => settings.Ranks.PointTerm = dialog.BuildResult());
        }
        catch (Exception exception)
        {
            MessageBox.Show($"💾 Не удалось сохранить названия: {exception.Message}", "💾 Ошибка сохранения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var selected = listViewUsers.SelectedItems.Count > 0
            ? listViewUsers.SelectedItems[0].Tag as UserStatistics
            : null;

        UpdateDetails(selected);
        UpdateActionState();
    }

    private void listViewUsers_ColumnClick(object sender, ColumnClickEventArgs e)
    {
        var nextOrder = SortOrder.Ascending;

        if (listViewUsers.Tag is int currentSortCol
            && currentSortCol == e.Column
            && listViewUsers.ListViewItemSorter is ListViewItemComparer comparer
            && comparer.Order == SortOrder.Ascending)
        {
            nextOrder = SortOrder.Descending;
        }

        listViewUsers.Tag = e.Column;
        listViewUsers.ListViewItemSorter = new ListViewItemComparer(e.Column, nextOrder);
    }

    private void listViewUsers_MouseDoubleClick(object sender, MouseEventArgs e)
    {
        if (listViewUsers.SelectedItems.Count == 0)
        {
            return;
        }

        numericIncrement.Focus();
        numericIncrement.Select(0, numericIncrement.Text.Length);
    }

    private void textBoxFilter_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter || listViewUsers.Items.Count == 0)
        {
            return;
        }

        listViewUsers.Items[0].Selected = true;
        listViewUsers.Focus();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void numericIncrement_ValueChanged(object sender, EventArgs e)
    {
        UpdateActionState();
    }

    private void numericIncrement_KeyUp(object sender, KeyEventArgs e)
    {
        UpdateActionState();
    }

    private void numericIncrement_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter || !buttonAction.Enabled)
        {
            return;
        }

        buttonAction.PerformClick();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private Task<bool> ApplyAdjustmentAsync(UserStatistics user, long delta)
    {
        if (delta < 0)
        {
            var amount = (ulong)-delta;
            var notification = _userMessagesManagementService.GetPunishmentNotification(user.Name, amount);
            MessageBox.Show(notification, "\U0001F3F4\u200D\u2620\uFE0F Наказание от СЕРЁГИ ПИРАТА! ⚔️", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return _userMessagesManagementService.PunishUserAsync(user.UserId, user.Name, amount, _channelProvider.Channel);
        }
        else
        {
            var amount = (ulong)delta;
            var notification = _userMessagesManagementService.GetRewardNotification(user.Name, amount);
            MessageBox.Show(notification, "🎉 Поощрение от СЕРЁГИ ПИРАТА! 🏆", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return _userMessagesManagementService.RewardUserAsync(user.UserId, user.Name, amount, _channelProvider.Channel);
        }
    }

    private void LoadData()
    {
        _allUsers = _userStatistics.GetAll().ToList();
        UpdateGlobalStats();
        ApplyFilter();
    }

    private void RefreshUserList()
    {
        _allUsers = _userStatistics.GetAll().ToList();
        UpdateGlobalStats();
        ApplyFilter();

        if (listViewUsers.SelectedItems.Count > 0)
        {
            listViewUsers.SelectedItems[0].EnsureVisible();
        }
    }

    private void ApplyFilter()
    {
        var term = textBoxFilter.Text?.Trim() ?? string.Empty;

        var filtered = string.IsNullOrWhiteSpace(term)
            ? _allUsers
            : _allUsers.Where(x =>
                x.Name.Contains(term, StringComparison.InvariantCultureIgnoreCase)
                || x.UserId.Contains(term, StringComparison.InvariantCultureIgnoreCase));

        var selectedUserId = listViewUsers.SelectedItems.Count > 0
            ? (listViewUsers.SelectedItems[0].Tag as UserStatistics)?.UserId
            : null;

        listViewUsers.BeginUpdate();
        listViewUsers.Items.Clear();

        foreach (var user in filtered)
        {
            var rank = _userRankService.GetRank(user.Points);
            var item = new ListViewItem(user.Name)
            {
                Tag = user,
            };

            item.SubItems.Add(user.MessageCount.ToString());
            item.SubItems.Add(user.Points.ToString());
            item.SubItems.Add($"{rank.Emoji} {rank.DisplayName}");

            listViewUsers.Items.Add(item);

            if (selectedUserId != null && user.UserId == selectedUserId)
            {
                item.Selected = true;
            }
        }

        listViewUsers.EndUpdate();

        UpdateActionState();
    }

    private void UpdateGlobalStats()
    {
        var totalUsers = _allUsers.Count;
        var totalMessages = (long)_allUsers.Sum(u => (decimal)u.MessageCount);
        var totalPoints = (long)_allUsers.Sum(u => (decimal)u.Points);
        var totalBonus = (long)_allUsers.Sum(u => (decimal)u.BonusPoints);
        var totalPenalty = (long)_allUsers.Sum(u => (decimal)u.PenaltyPoints);

        labelGlobalUsers.Text = $"👥 Пользователей: {totalUsers:N0}";
        labelGlobalMessages.Text = $"💬 Сообщений: {totalMessages:N0}";
        labelGlobalPoints.Text = $"🏆 Баллов: {totalPoints:N0}";
        labelGlobalBonus.Text = $"🎁 Бонус: {totalBonus:N0} / 🚫 Штраф: {totalPenalty:N0}";
    }

    private void UpdateDetails(UserStatistics? user)
    {
        labelUserId.Text = $"🆔 ID: {user?.UserId ?? "–"}";
        labelUserName.Text = $"👤 Имя: {user?.Name ?? "–"}";
        labelMessages.Text = $"💬 Сообщения: {user?.MessageCount:N0}";
        labelPoints.Text = $"🏆 Баллы: {user?.Points:N0}";
        labelBonus.Text = $"    🎁 Бонус: {user?.BonusPoints:N0}";
        labelPenalty.Text = $"    🚫 Штраф: {user?.PenaltyPoints:N0}";

        var rank = _userRankService.GetRank(user?.Points ?? 0);
        labelChessPiece.Text = $"{rank.Emoji} {rank.DisplayName}";
    }

    private void UpdateActionState()
    {
        var hasSelection = listViewUsers.SelectedItems.Count > 0;
        var hasValue = numericIncrement.Value != 0;
        buttonAction.Enabled = hasSelection && hasValue;

        var term = _userRankService.PointTerm;
        var count = (long)numericIncrement.Value;

        buttonAction.Text = count switch
        {
            > 0 => $"➕ Добавить {count} {term.ForCount(count)}",
            < 0 => $"➖ Убрать {-count} {term.ForCount(count)}",
            _ => "➕ Изменить баланс",
        };
    }

    private sealed class ListViewItemComparer(int column, SortOrder order) : IComparer
    {
        public SortOrder Order { get; } = order;

        public int Compare(object? x, object? y)
        {
            var itemX = (ListViewItem)x!;
            var itemY = (ListViewItem)y!;
            var textX = itemX.SubItems[column].Text;
            var textY = itemY.SubItems[column].Text;

            int returnVal;

            if (column is ColumnMessages or ColumnPoints
                && long.TryParse(textX, out var valX)
                && long.TryParse(textY, out var valY))
            {
                returnVal = valX.CompareTo(valY);
            }
            else
            {
                returnVal = string.Compare(textX, textY, StringComparison.Ordinal);
            }

            return Order == SortOrder.Descending ? -returnVal : returnVal;
        }
    }
}
