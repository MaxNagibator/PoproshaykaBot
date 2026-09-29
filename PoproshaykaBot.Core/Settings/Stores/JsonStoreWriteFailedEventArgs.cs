namespace PoproshaykaBot.Core.Settings.Stores;

internal sealed class JsonStoreWriteFailedEventArgs(string filePath, Exception exception) : EventArgs
{
    public string FilePath { get; } = filePath;

    public Exception Exception { get; } = exception;
}
