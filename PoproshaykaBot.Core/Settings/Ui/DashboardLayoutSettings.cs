namespace PoproshaykaBot.Core.Settings.Ui;

public class DashboardLayoutSettings
{
    public int ColumnCount { get; set; } = 4;
    public int RowCount { get; set; } = 3;
    public List<DashboardTileSettings> Tiles { get; set; } = [];
    public DashboardPane? Root { get; set; }
}
