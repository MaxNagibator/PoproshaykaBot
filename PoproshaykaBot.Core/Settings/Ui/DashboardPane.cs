using System.Text.Json.Serialization;

namespace PoproshaykaBot.Core.Settings.Ui;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TilePane), "tile")]
[JsonDerivedType(typeof(SplitPane), "split")]
public abstract record DashboardPane;

public sealed record TilePane(string TypeId) : DashboardPane;

public sealed record SplitPane(SplitOrientation Orientation, IReadOnlyList<PaneSlot> Children) : DashboardPane;

public sealed record PaneSlot(DashboardPane Pane, double? Weight);
