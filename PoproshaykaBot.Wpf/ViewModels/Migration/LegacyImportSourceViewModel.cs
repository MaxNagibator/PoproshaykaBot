using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Core.Settings.Migrations.LegacyImport;

namespace PoproshaykaBot.Wpf.ViewModels.Migration;

public sealed partial class LegacyImportSourceViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public LegacyImportSourceViewModel(LegacyDataSummary summary)
    {
        Summary = summary;
        Title = LegacyImportText.DescribeKind(summary.Kind);
        Path = summary.SourcePath;
        Modified = LegacyImportText.DescribeModified(summary);
        Content = LegacyImportText.DescribeContent(summary);

        Note = summary.Kind == LegacyDataSourceKind.CurrentBaseDirectory
            ? "Эти данные уже лежат там, где нужно – их достаточно преобразовать, копировать ничего не придётся."
            : null;
    }

    public LegacyDataSummary Summary { get; }

    public string Title { get; }

    public string Path { get; }

    public string Modified { get; }

    public string Content { get; }

    public string? Note { get; }
}
