namespace PoproshaykaBot.Core.Server;

public interface IHttpServerStatus
{
    bool IsRunning { get; }

    int? Port { get; }
}
