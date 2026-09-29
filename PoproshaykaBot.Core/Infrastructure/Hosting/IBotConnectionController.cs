using PoproshaykaBot.Core.Infrastructure.Events.Lifecycle;

namespace PoproshaykaBot.Core.Infrastructure.Hosting;

public interface IBotConnectionController
{
    bool IsBusy { get; }

    BotLifecyclePhase CurrentPhase { get; }

    void StartConnection();

    void CancelConnection();

    Task WaitForConnectionAsync();

    Task StopAsync(BotStopMode mode);
}
