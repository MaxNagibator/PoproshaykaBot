using PoproshaykaBot.WinForms.Models;
using System.Collections;

namespace PoproshaykaBot.WinForms;

public sealed partial class UserStatisticsForm : Form
{
    private readonly StatisticsCollector? _statisticsCollector;
    private readonly UserRankService _userRankService;
    private readonly UserMessagesManagementService _userMessagesManagementService;
    private Bot? _bot;
    private List<UserStatistics> _allUsers = [];
    private List<UserStatistics> _filteredUsers = [];

    public UserStatisticsForm(
        StatisticsCollector statisticsCollector,
        UserRankService userRankService,
        UserMessagesManagementService userMessagesManagementService,
        Bot? bot = null)
    {
        _statisticsCollector = statisticsCollector;
        _userRankService = userRankService;
        _userMessagesManagementService = userMessagesManagementService;
        _bot = bot;

        InitializeComponent();
        InitializeRuntime();
        LoadData();
    }

    private UserStatisticsForm()
    {
        InitializeComponent();
    }

    public void UpdateBotReference(Bot? bot)
    {
        _bot = bot;
    }

    private void textBoxFilter_TextChanged(object sender, EventArgs e)
    {
        ApplyFilter();
    }

    private void listBoxUsers_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (listViewUsers.SelectedItems.Count == 0)
        {
            UpdateDetails(null);
            UpdateActionState();
            return;
        }

        var user = listViewUsers.SelectedItems[0].Tag as UserStatistics;
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

        if (_statisticsCollector == null)
        {
            MessageBox.Show("❌ Статистика недоступна.", "❌ Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var delta = (long)numericIncrement.Value;

        if (delta == 0)
        {
            UpdateActionState();
            return;
        }

        bool updated;

        if (delta < 0)
        {
            var notificationMessage = _userMessagesManagementService.GetPunishmentNotification(user.Name, (ulong)-delta);
            MessageBox.Show(notificationMessage, "🏴‍☠️ Наказание от СЕРЁГИ ПИРАТА! ⚔️", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            try
            {
                updated = _userMessagesManagementService.PunishUser(user.UserId, user.Name, (ulong)-delta, _bot?.Channel);
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Не удалось наказать пользователя: {exception.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        else
        {
            var notificationMessage = _userMessagesManagementService.GetRewardNotification(user.Name, (ulong)delta);
            MessageBox.Show(notificationMessage, "🎉 Поощрение от СЕРЁГИ ПИРАТА! 🏆", MessageBoxButtons.OK, MessageBoxIcon.Information);

            try
            {
                updated = _userMessagesManagementService.RewardUser(user.UserId, user.Name, (ulong)delta, _bot?.Channel);
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Не удалось поощрить пользователя: {exception.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
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
            await _statisticsCollector.SaveNowAsync();
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

    private void InitializeRuntime()
    {
        listViewUsers.ColumnClick += (s, e) =>
        {
            if (s is not ListView lvw)
            {
                return;
            }

            if (lvw.Tag is not int currentSortCol || currentSortCol != e.Column)
            {
                lvw.Tag = e.Column;
                lvw.ListViewItemSorter = new ListViewItemComparer(e.Column, SortOrder.Ascending);
            }
            else
            {
                var comparer = lvw.ListViewItemSorter as ListViewItemComparer;
                var currentOrder = comparer?.Order ?? SortOrder.Ascending;
                lvw.ListViewItemSorter = new ListViewItemComparer(e.Column,
                    currentOrder == SortOrder.Ascending
                        ? SortOrder.Descending
                        : SortOrder.Ascending);
            }
        };

        listViewUsers.MouseDoubleClick += (s, e) =>
        {
            if (listViewUsers.SelectedItems.Count <= 0)
            {
                return;
            }

            numericIncrement.Focus();
            numericIncrement.Select(0, numericIncrement.Text.Length);
        };

        textBoxFilter.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || listViewUsers.Items.Count <= 0)
            {
                return;
            }

            listViewUsers.Items[0].Selected = true;
            listViewUsers.Focus();
            e.Handled = true;
            e.SuppressKeyPress = true;
        };

        numericIncrement.ValueChanged += (_, _) => UpdateActionState();
        numericIncrement.KeyUp += (_, _) => UpdateActionState();
        numericIncrement.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || !buttonAction.Enabled)
            {
                return;
            }

            buttonAction_Click(buttonAction, e);
            e.Handled = true;
            e.SuppressKeyPress = true;
        };

        UpdateDetails(null);
        UpdateGlobalStats();
        UpdateActionState();
    }

    private void LoadData()
    {
        if (_statisticsCollector == null)
        {
            return;
        }

        _allUsers = _statisticsCollector.GetAllUsers();
        UpdateGlobalStats();
        ApplyFilter();
    }

    private void RefreshUserList()
    {
        if (_statisticsCollector == null)
        {
            return;
        }

        var currentFilter = textBoxFilter.Text;
        var selectedUserId = listViewUsers.SelectedItems.Count > 0 ? (listViewUsers.SelectedItems[0].Tag as UserStatistics)?.UserId : null;

        _allUsers = _statisticsCollector.GetAllUsers();
        UpdateGlobalStats();

        textBoxFilter.Text = currentFilter;
        ApplyFilter();

        if (selectedUserId == null)
        {
            return;
        }

        foreach (ListViewItem item in listViewUsers.Items)
        {
            if (item.Tag is not UserStatistics user || user.UserId != selectedUserId)
            {
                continue;
            }

            item.Selected = true;
            item.EnsureVisible();
            break;
        }
    }

    private void ApplyFilter()
    {
        var term = textBoxFilter.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(term))
        {
            _filteredUsers = _allUsers.ToList();
        }
        else
        {
            _filteredUsers = _allUsers
                .Where(x => x.Name.Contains(term, StringComparison.InvariantCultureIgnoreCase)
                            || x.UserId.Contains(term, StringComparison.InvariantCultureIgnoreCase))
                .ToList();
        }

        var selectedUserId = listViewUsers.SelectedItems.Count > 0 ? (listViewUsers.SelectedItems[0].Tag as UserStatistics)?.UserId : null;

        listViewUsers.BeginUpdate();
        listViewUsers.Items.Clear();

        foreach (var user in _filteredUsers)
        {
            var rank = _userRankService.GetRank(user.TotalMessageCount);
            var item = new ListViewItem(user.Name)
            {
                Tag = user,
            };

            item.SubItems.Add(user.TotalMessageCount.ToString());
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
        if (_allUsers == null || _allUsers.Count == 0)
        {
            labelGlobalUsers.Text = "👥 Пользователей: 0";
            labelGlobalTotal.Text = "💬 Всего сообщ: 0";
            labelGlobalWritten.Text = "✍️ Написано: 0";
            labelGlobalBonus.Text = "🎁 Бонус/Штраф: 0";
            return;
        }

        var totalUsers = _allUsers.Count;
        var totalMessages = (long)_allUsers.Sum(u => (decimal)u.TotalMessageCount);
        var totalWritten = (long)_allUsers.Sum(u => (decimal)u.MessageCount);
        var totalBonus = (long)_allUsers.Sum(u => (decimal)u.BonusMessageCount);
        var totalPenalty = (long)_allUsers.Sum(u => (decimal)u.ShtrafMessageCount);

        labelGlobalUsers.Text = $"👥 Пользователей: {totalUsers:N0}";
        labelGlobalTotal.Text = $"💬 Всего сообщ: {totalMessages:N0}";
        labelGlobalWritten.Text = $"✍️ Написано: {totalWritten:N0}";
        labelGlobalBonus.Text = $"🎁 Бонус: {totalBonus:N0} / 🚫 Штраф: {totalPenalty:N0}";
    }

    private void UpdateDetails(UserStatistics? user)
    {
        if (user == null)
        {
            labelUserId.Text = "🆔 ID: —";
            labelUserName.Text = "👤 Имя: —";
            labelMessageTotal.Text = "💬 Всего: 0";
            labelMessageWritten.Text = "    ✍️ Написано: 0";
            labelMessageBonus.Text = "    🎁 Бонус: 0";
            labelMessagePenalty.Text = "    🚫 Штраф: 0";
            labelChessPiece.Text = "♟ ПЕШКА";
            return;
        }

        labelUserId.Text = $"🆔 ID: {user.UserId}";
        labelUserName.Text = $"👤 Имя: {user.Name}";
        labelMessageTotal.Text = $"💬 Всего: {user.TotalMessageCount:N0}";
        labelMessageWritten.Text = $"    ✍️ Написано: {user.MessageCount:N0}";
        labelMessageBonus.Text = $"    🎁 Бонус: {user.BonusMessageCount:N0}";
        labelMessagePenalty.Text = $"    🚫 Штраф: {user.ShtrafMessageCount:N0}";

        var rank = _userRankService.GetRank(user.TotalMessageCount);
        labelChessPiece.Text = $"{rank.Emoji} {rank.DisplayName}";
    }

    private void UpdateActionState()
    {
        var hasSelection = listViewUsers.SelectedItems.Count > 0;
        var hasValue = numericIncrement.Value != 0;
        buttonAction.Enabled = hasSelection && hasValue;

        if (numericIncrement.Value > 0)
        {
            buttonAction.Text = $"➕ Добавить {numericIncrement.Value} сообщений";
        }
        else if (numericIncrement.Value < 0)
        {
            buttonAction.Text = $"➖ Убрать {-numericIncrement.Value} сообщений";
        }
        else
        {
            buttonAction.Text = "➕ Добавить сообщения";
        }
    }

    private sealed class ListViewItemComparer(int column, SortOrder order) : IComparer
    {
        public SortOrder Order { get; } = order;

        public int Compare(object? x, object? y)
        {
            int returnVal;
            var itemX = (ListViewItem)x!;
            var itemY = (ListViewItem)y!;

            if (column == 1)
            {
                if (long.TryParse(itemX.SubItems[column].Text, out var valX) && long.TryParse(itemY.SubItems[column].Text, out var valY))
                {
                    returnVal = valX.CompareTo(valY);
                }
                else
                {
                    returnVal = string.Compare(itemX.SubItems[column].Text, itemY.SubItems[column].Text, StringComparison.Ordinal);
                }
            }
            else
            {
                returnVal = string.Compare(itemX.SubItems[column].Text, itemY.SubItems[column].Text, StringComparison.Ordinal);
            }

            return Order == SortOrder.Descending ? -returnVal : returnVal;
        }
    }
}
