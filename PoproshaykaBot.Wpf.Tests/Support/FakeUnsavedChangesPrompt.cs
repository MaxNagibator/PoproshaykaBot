using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.Tests.Support;

public sealed class FakeUnsavedChangesPrompt : IUnsavedChangesPrompt
{
    public UnsavedChangesDecision Answer { get; set; } = UnsavedChangesDecision.Discard;

    public int AskCount { get; private set; }

    public string? LastSubject { get; private set; }

    public string? LastAction { get; private set; }

    public UnsavedChangesDecision Ask(string subject, string action)
    {
        AskCount++;
        LastSubject = subject;
        LastAction = action;

        return Answer;
    }
}
