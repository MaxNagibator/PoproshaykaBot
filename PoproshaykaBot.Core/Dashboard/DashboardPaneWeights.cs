using PoproshaykaBot.Core.Settings.Ui;

namespace PoproshaykaBot.Core.Dashboard;

internal static class DashboardPaneWeights
{
    private const int MaxDepth = 32;

    public static bool IsExplicit(double? weight)
    {
        return weight is { } value && value > 0 && double.IsFinite(value);
    }

    public static bool IsValid(double? weight)
    {
        return weight is null || IsExplicit(weight);
    }

    public static DashboardPane Sanitize(DashboardPane root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return Rewrite(root, 0);
    }

    private static DashboardPane Rewrite(DashboardPane pane, int depth)
    {
        if (depth > MaxDepth || pane is not SplitPane { Children: not null } split)
        {
            return pane;
        }

        PaneSlot[]? children = null;

        for (var index = 0; index < split.Children.Count; index++)
        {
            if (split.Children[index] is not { Pane: not null } slot)
            {
                continue;
            }

            var rewritten = Rewrite(slot.Pane, depth + 1);
            var valid = IsValid(slot.Weight);

            if (valid && ReferenceEquals(rewritten, slot.Pane))
            {
                continue;
            }

            children ??= [.. split.Children];
            children[index] = new(rewritten, valid ? slot.Weight : null);
        }

        return children is null ? pane : new SplitPane(split.Orientation, children);
    }
}
