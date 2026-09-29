namespace PoproshaykaBot.Core.Dashboard;

public sealed class PlacedTile
{
    public required string TypeId { get; init; }

    public int Row { get; set; }

    public int Column { get; set; }

    public int ColumnSpan { get; set; } = 1;

    public int RowSpan { get; set; } = 1;

    public int? MaxHeight { get; set; }

    public int? MaxWidth { get; set; }
}
