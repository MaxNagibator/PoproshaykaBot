namespace PoproshaykaBot.Wpf.Infrastructure;

public interface IUnsavedChangesPrompt
{
    UnsavedChangesDecision Ask(string subject, string action);
}
