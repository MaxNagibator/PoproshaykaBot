using KeepShell.ViewModels;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.Views.Settings;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PoproshaykaBot.Wpf.Views;

public partial class SettingsPageView : UserControl, IView<SettingsPageViewModel>
{
    private const double BlockTopPadding = 12;

    private readonly List<(string Key, FrameworkElement Element)> _blocks = [];

    private SettingsSectionList? _sections;

    public SettingsPageView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static void Collect(DependencyObject root, List<(string Key, FrameworkElement Element)> blocks)
    {
        if (root is FrameworkElement element && SettingsBlock.GetKey(element) is { Length: > 0 } key)
        {
            blocks.Add((key, element));
        }

        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var index = 0; index < count; index++)
        {
            Collect(VisualTreeHelper.GetChild(root, index), blocks);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_sections is not null || DataContext is not SettingsPageViewModel viewModel)
        {
            return;
        }

        _sections = viewModel.Sections;
        _sections.ChildActivated += OnChildActivated;
        _sections.PropertyChanged += OnSectionsPropertyChanged;

        var restored = _sections.SelectedChild;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => ScrollToBlock(restored));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_sections is null)
        {
            return;
        }

        _sections.ChildActivated -= OnChildActivated;
        _sections.PropertyChanged -= OnSectionsPropertyChanged;
        _sections = null;
        _blocks.Clear();
    }

    private void OnSectionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SettingsSectionList.Selected), StringComparison.Ordinal))
        {
            SectionScroll.ScrollToVerticalOffset(0);
        }
    }

    private void OnChildActivated(object? sender, SettingsSubsection child)
    {
        ScrollToBlock(child);
    }

    private void ScrollToBlock(SettingsSubsection? child)
    {
        if (child is null || _sections?.Selected is not { } section)
        {
            return;
        }

        SectionScroll.UpdateLayout();

        if (FindBlock($"{section.Key}/{child.Key}") is not { } block)
        {
            return;
        }

        var top = block.TransformToAncestor(SectionScroll).Transform(default).Y + SectionScroll.VerticalOffset;

        SectionScroll.ScrollToVerticalOffset(Math.Max(0, top - BlockTopPadding));
    }

    private FrameworkElement? FindBlock(string key)
    {
        foreach (var (blockKey, element) in Blocks())
        {
            if (element.IsVisible && string.Equals(blockKey, key, StringComparison.Ordinal))
            {
                return element;
            }
        }

        return null;
    }

    private IReadOnlyList<(string Key, FrameworkElement Element)> Blocks()
    {
        if (_blocks.Count == 0)
        {
            Collect(SectionScroll, _blocks);
        }

        return _blocks;
    }

    private void SectionScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_sections is not { Selected: { Children.Count: > 0 } section })
        {
            return;
        }

        var offset = SectionScroll.VerticalOffset;
        var prefix = $"{section.Key}/";
        var tops = new List<(string Key, double Top)>();

        foreach (var (key, element) in Blocks())
        {
            if (element.IsVisible && key.StartsWith(prefix, StringComparison.Ordinal))
            {
                tops.Add((key[prefix.Length..], element.TransformToAncestor(SectionScroll).Transform(default).Y + offset));
            }
        }

        var atBottom = SectionScroll.ScrollableHeight > 0 && offset >= SectionScroll.ScrollableHeight - 1;
        var visible = SettingsBlock.VisibleAt(tops, offset, atBottom);

        if (section.FindChild(visible) is { } child && !ReferenceEquals(_sections.SelectedChild, child))
        {
            _sections.SelectedChild = child;
        }
    }

    private void SuppressAutoScroll(object sender, RequestBringIntoViewEventArgs e)
    {
        if (e.OriginalSource is not TextBox)
        {
            e.Handled = true;
        }
    }
}
