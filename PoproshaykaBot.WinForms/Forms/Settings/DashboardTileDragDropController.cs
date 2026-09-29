using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.WinForms.Tiles;

namespace PoproshaykaBot.WinForms.Forms.Settings;

internal sealed record CellPosition(int Row, int Column);

internal sealed record TileDragPayload(DashboardTileType Type, bool FromGrid);

internal sealed class DashboardTileDragDropController(
    IReadOnlyDictionary<DashboardTileType, PlacedTile> placedTiles,
    Action<DashboardTileType, int, int> onPlaceOrMove,
    Action<DashboardTileType, Control, Point> onShowContextMenu)
{
    public static void HandlePaletteMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (sender is not Button { Enabled: true } button)
        {
            return;
        }

        if (button.Tag is not DashboardTileType type)
        {
            return;
        }

        button.DoDragDrop(new TileDragPayload(type, false), DragDropEffects.Copy);
    }

    public static void HandleCellDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(TileDragPayload)) is TileDragPayload payload)
        {
            e.Effect = payload.FromGrid ? DragDropEffects.Move : DragDropEffects.Copy;
        }
    }

    public void HandleTileMouseDown(object? sender, MouseEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        var owner = control.Tag is DashboardTileType ? control : control.Parent;

        if (owner?.Tag is not DashboardTileType type || owner == null)
        {
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            onShowContextMenu(type, control, e.Location);
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        owner.DoDragDrop(new TileDragPayload(type, true), DragDropEffects.Move);
    }

    public void HandleCellDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(TileDragPayload)) is not TileDragPayload payload)
        {
            return;
        }

        if (sender is not Control control)
        {
            return;
        }

        switch (control.Tag)
        {
            case CellPosition pos:
                onPlaceOrMove(payload.Type, pos.Row, pos.Column);
                break;

            case DashboardTileType targetType when placedTiles.TryGetValue(targetType, out var target):
                if (targetType == payload.Type)
                {
                    return;
                }

                onPlaceOrMove(payload.Type, target.Row, target.Column);
                break;
        }
    }
}
