namespace PoproshaykaBot.Core.Update;

public interface IUpdateInstaller
{
    Task PrepareAsync(UpdateCandidate candidate, IProgress<int>? progress, CancellationToken cancellationToken);

    PendingUpdate? ReadPending();

    bool DiscardPending(string version);
}
