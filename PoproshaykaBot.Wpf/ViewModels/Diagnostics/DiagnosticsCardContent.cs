using KeepShell.Diagnostics;

namespace PoproshaykaBot.Wpf.ViewModels.Diagnostics;

public readonly record struct DiagnosticsCardContent(DiagnosticsCardState State, IReadOnlyList<DiagnosticsCardRow> Rows);
