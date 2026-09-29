namespace PoproshaykaBot.Core.Server;

public sealed record PortReconcileResult(bool IsResolved, PortReconcileNotice? Notice);

public sealed record PortReconcileNotice(string Title, string Message, PortReconcileSeverity Severity);

public enum PortReconcileSeverity
{
    None = 0,
    Information = 1,
    Error = 2,
    Warning = 3,
}
