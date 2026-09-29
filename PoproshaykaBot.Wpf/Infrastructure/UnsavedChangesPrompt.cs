using System.Windows;

namespace PoproshaykaBot.Wpf.Infrastructure;

public sealed class UnsavedChangesPrompt : IUnsavedChangesPrompt
{
    public UnsavedChangesDecision Ask(string subject, string action)
    {
        if (App.IsHeadless)
        {
            return UnsavedChangesDecision.Discard;
        }

        var answer = StyledMessageBox.Show(
            $"{subject} Сохранить их перед {action}?",
            "Несохранённые изменения",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes);

        return answer switch
        {
            MessageBoxResult.Yes => UnsavedChangesDecision.Save,
            MessageBoxResult.No => UnsavedChangesDecision.Discard,
            _ => UnsavedChangesDecision.Stay,
        };
    }
}
