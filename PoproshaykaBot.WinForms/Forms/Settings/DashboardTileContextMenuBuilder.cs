using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.WinForms.Infrastructure;
using PoproshaykaBot.WinForms.Tiles;

namespace PoproshaykaBot.WinForms.Forms.Settings;

internal interface IDashboardTileCommands
{
    void SetColumnSpan(DashboardTileType type, int span);

    void SetRowSpan(DashboardTileType type, int span);

    void SetMaxWidth(DashboardTileType type, int? value);

    void SetMaxHeight(DashboardTileType type, int? value);

    void RemoveTile(DashboardTileType type);
}

internal static class DashboardTileContextMenuBuilder
{
    private static readonly int[] MaxHeightPresets = [100, 150, 200, 250, 300, 400, 500];
    private static readonly int[] MaxWidthPresets = [200, 300, 400, 500, 600, 800];

    public static void Populate(
        ContextMenuStrip menu,
        DashboardTileType type,
        PlacedTile placed,
        int columnCount,
        int rowCount,
        IDashboardTileCommands commands,
        IWin32Window dialogOwner)
    {
        ClearAndDisposeItems(menu);

        var maxColumnSpan = Math.Max(1, columnCount - placed.Column);
        var maxRowSpan = Math.Max(1, rowCount - placed.Row);

        menu.Items.Add(BuildSpanMenu("Ширина", placed.ColumnSpan, maxColumnSpan,
            span => commands.SetColumnSpan(type, span)));

        menu.Items.Add(BuildSpanMenu("Высота", placed.RowSpan, maxRowSpan,
            span => commands.SetRowSpan(type, span)));

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(BuildMaxSizeMenu("Макс. ширина",
            MaxWidthPresets,
            placed.MaxWidth,
            type.MaxWidth,
            value => commands.SetMaxWidth(type, value),
            dialogOwner));

        menu.Items.Add(BuildMaxSizeMenu("Макс. высота",
            MaxHeightPresets,
            placed.MaxHeight,
            type.MaxHeight,
            value => commands.SetMaxHeight(type, value),
            dialogOwner));

        menu.Items.Add(new ToolStripSeparator());

        var removeItem = new ToolStripMenuItem("Удалить плитку");
        removeItem.Click += (_, _) => commands.RemoveTile(type);
        menu.Items.Add(removeItem);
    }

    private static void ClearAndDisposeItems(ContextMenuStrip menu)
    {
        var oldItems = menu.Items.Cast<ToolStripItem>().ToList();
        menu.Items.Clear();

        foreach (var oldItem in oldItems)
        {
            oldItem.Dispose();
        }
    }

    private static ToolStripMenuItem BuildSpanMenu(string label, int currentSpan, int maxSpan, Action<int> setSpan)
    {
        var menu = new ToolStripMenuItem($"{label}: {currentSpan}");

        for (var i = 1; i <= maxSpan; i++)
        {
            var span = i;
            var item = new ToolStripMenuItem(span.ToString())
            {
                Checked = span == currentSpan,
            };

            item.Click += (_, _) => setSpan(span);
            menu.DropDownItems.Add(item);
        }

        return menu;
    }

    private static ToolStripMenuItem BuildMaxSizeMenu(
        string label,
        IReadOnlyList<int> presets,
        int? overrideValue,
        int? typeDefault,
        Action<int?> setValue,
        IWin32Window dialogOwner)
    {
        var isExplicitAuto = overrideValue is <= 0;
        var effective = isExplicitAuto ? null : overrideValue ?? typeDefault;

        string headerText;

        if (isExplicitAuto)
        {
            headerText = $"{label}: авто";
        }
        else if (effective.HasValue)
        {
            headerText = overrideValue.HasValue
                ? $"{label}: {effective.Value}px"
                : $"{label}: {effective.Value}px (по умолч.)";
        }
        else
        {
            headerText = $"{label}: авто (по умолч.)";
        }

        var menu = new ToolStripMenuItem(headerText);

        var defaultLabel = typeDefault.HasValue
            ? $"По умолчанию ({typeDefault.Value}px)"
            : "По умолчанию (авто)";

        var defaultItem = new ToolStripMenuItem(defaultLabel)
        {
            Checked = !overrideValue.HasValue,
        };

        defaultItem.Click += (_, _) => setValue(null);
        menu.DropDownItems.Add(defaultItem);

        if (typeDefault.HasValue)
        {
            var autoItem = new ToolStripMenuItem("Авто")
            {
                Checked = isExplicitAuto,
            };

            autoItem.Click += (_, _) => setValue(0);
            menu.DropDownItems.Add(autoItem);
        }

        menu.DropDownItems.Add(new ToolStripSeparator());

        var isCustomValue = overrideValue is > 0 && !presets.Contains(overrideValue.Value);

        foreach (var preset in presets)
        {
            var value = preset;
            var item = new ToolStripMenuItem($"{value}px")
            {
                Checked = overrideValue == value,
            };

            item.Click += (_, _) => setValue(value);
            menu.DropDownItems.Add(item);
        }

        var customLabel = isCustomValue
            ? $"Указать... ({overrideValue!.Value}px)"
            : "Указать...";

        var customItem = new ToolStripMenuItem(customLabel)
        {
            Checked = isCustomValue,
        };

        customItem.Click += (_, _) =>
        {
            var initial = overrideValue is > 0 ? overrideValue.Value : typeDefault ?? presets[0];
            var custom = CustomSizePrompt.Show(dialogOwner, label, initial);

            if (custom.HasValue)
            {
                setValue(custom.Value);
            }
        };

        menu.DropDownItems.Add(customItem);

        return menu;
    }
}

internal static class CustomSizePrompt
{
    private const int MinValue = 50;
    private const int MaxValue = 5000;

    public static int? Show(IWin32Window owner, string label, int initial)
    {
        var clamped = Math.Clamp(initial, MinValue, MaxValue);

        using var form = new Form
        {
            Text = label,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };

        OffScreenForm.PrepareIfHeadless(form);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new(12),
        };

        var promptLabel = new Label
        {
            Text = "Значение, px:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new(0, 6, 8, 0),
        };

        var numeric = new NumericUpDown
        {
            Minimum = MinValue,
            Maximum = MaxValue,
            Increment = 10,
            Value = clamped,
            Width = 100,
            Margin = new(0, 4, 0, 0),
        };

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new(0, 8, 0, 0),
        };

        var cancelButton = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
        };

        var okButton = new Button
        {
            Text = "ОК",
            DialogResult = DialogResult.OK,
            AutoSize = true,
        };

        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);

        layout.Controls.Add(promptLabel, 0, 0);
        layout.Controls.Add(numeric, 1, 0);
        layout.Controls.Add(buttons, 0, 1);
        layout.SetColumnSpan(buttons, 2);

        form.Controls.Add(layout);
        form.AcceptButton = okButton;
        form.CancelButton = cancelButton;

        return form.ShowDialog(owner) == DialogResult.OK
            ? (int)numeric.Value
            : null;
    }
}
