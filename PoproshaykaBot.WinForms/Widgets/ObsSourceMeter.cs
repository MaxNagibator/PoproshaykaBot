namespace PoproshaykaBot.WinForms.Widgets;

public sealed partial class ObsSourceMeter : UserControl
{
    public ObsSourceMeter()
    {
        InitializeComponent();
    }

    public string SourceName { get; set; } = string.Empty;

    public bool Muted { get; set; }

    public bool Found { get; set; }

    public double CurrentLevel { get; private set; }

    public void ShowActive(string displayName, double? volumeDecibels)
    {
        SetState(displayName, Color.SeaGreen, Color.White);
        _detailLabel.Text = volumeDecibels.HasValue
            ? $"включён · {volumeDecibels.Value:0.#} дБ"
            : "включён";

        _detailLabel.ForeColor = Color.Green;
    }

    public void ShowMuted(string displayName, double? volumeDecibels)
    {
        SetState(displayName, Color.Firebrick, Color.White);
        _detailLabel.Text = volumeDecibels.HasValue
            ? $"выключен · {volumeDecibels.Value:0.#} дБ"
            : "выключен";

        _detailLabel.ForeColor = Color.Red;
        ApplyVolumeMeterLevel(0);
    }

    public void ShowMissing(string displayName)
    {
        SetState(displayName, Color.DarkOrange, Color.Black);
        _detailLabel.Text = "источник не найден";
        _detailLabel.ForeColor = Color.Orange;
        ApplyVolumeMeterLevel(0);
    }

    public void ApplyVolumeMeterLevel(double level)
    {
        var normalized = Math.Clamp(level, 0D, 1D);
        CurrentLevel = normalized;
        var width = (int)Math.Round(_volumeMeterPanel.ClientSize.Width * normalized);

        _volumeMeterFillPanel.Width = Math.Max(0, width);
        _volumeMeterFillPanel.BackColor = ResolveVolumeMeterColor(normalized);
    }

    private void OnVolumeMeterPanelResize(object? sender, EventArgs e)
    {
        ApplyVolumeMeterLevel(CurrentLevel);
    }

    private static Color ResolveVolumeMeterColor(double normalized)
    {
        if (normalized >= 0.9D)
        {
            return Color.Firebrick;
        }

        if (normalized >= 0.68D)
        {
            return Color.DarkOrange;
        }

        return Color.SeaGreen;
    }

    private void SetState(string displayName, Color backColor, Color foreColor)
    {
        _stateLabel.Text = string.IsNullOrWhiteSpace(displayName) ? "–" : displayName.Trim();
        _stateLabel.BackColor = backColor;
        _stateLabel.ForeColor = foreColor;
    }
}
