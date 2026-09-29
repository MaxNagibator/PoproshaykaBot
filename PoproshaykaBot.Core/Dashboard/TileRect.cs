namespace PoproshaykaBot.Core.Dashboard;

public readonly record struct TileRect(string TypeId, int Row, int Column, int RowSpan, int ColumnSpan)
{
    public int RowEnd => Row + RowSpan;

    public int ColumnEnd => Column + ColumnSpan;
}
