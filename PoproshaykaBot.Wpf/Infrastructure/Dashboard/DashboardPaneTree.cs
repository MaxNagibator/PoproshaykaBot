using PoproshaykaBot.Wpf.ViewModels;

namespace PoproshaykaBot.Wpf.Infrastructure.Dashboard;

public static class DashboardPaneTree
{
    public static PaneLayout? Find(PaneLayout root, int[] path)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(path);

        if (root.Path.AsSpan().SequenceEqual(path))
        {
            return root;
        }

        if (root is not SplitPaneLayout split)
        {
            return null;
        }

        foreach (var child in split.Children)
        {
            if (Find(child.Pane, path) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    public static int IndexOfChild(SplitPaneLayout node, int[] childPath)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(childPath);

        for (var index = 0; index < node.Children.Count; index++)
        {
            if (node.Children[index].Pane.Path.AsSpan().SequenceEqual(childPath))
            {
                return index;
            }
        }

        return -1;
    }

    public static int Neighbour(SplitPaneLayout node, int index)
    {
        ArgumentNullException.ThrowIfNull(node);

        for (var candidate = index + 1; candidate < node.Children.Count; candidate++)
        {
            if (!node.Children[candidate].SizesToContent)
            {
                return candidate;
            }
        }

        for (var candidate = index - 1; candidate >= 0; candidate--)
        {
            if (!node.Children[candidate].SizesToContent)
            {
                return candidate;
            }
        }

        return -1;
    }

    public static int[]? FirstLeafPath(PaneLayout pane)
    {
        ArgumentNullException.ThrowIfNull(pane);

        if (pane is not SplitPaneLayout split)
        {
            return pane.Path;
        }

        foreach (var child in split.Children)
        {
            if (FirstLeafPath(child.Pane) is { } path)
            {
                return path;
            }
        }

        return null;
    }
}
