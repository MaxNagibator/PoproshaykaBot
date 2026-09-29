namespace PoproshaykaBot.Core.Debugging;

public interface ITargetChannelProvider
{
    TargetChannelState Current { get; }

    void BeginSession();

    void EndSession();
}
