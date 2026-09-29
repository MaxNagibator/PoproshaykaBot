namespace PoproshaykaBot.Wpf.ViewModels;

public interface IUnsavedChangesPage
{
    bool HasUnsavedChanges { get; }

    string UnsavedChangesSubject { get; }

    Task<bool> TrySaveUnsavedChangesAsync();

    void DiscardUnsavedChanges();
}
