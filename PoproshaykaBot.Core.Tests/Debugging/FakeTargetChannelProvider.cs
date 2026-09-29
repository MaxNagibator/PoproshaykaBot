using PoproshaykaBot.Core.Debugging;

namespace PoproshaykaBot.Core.Tests.Debugging;

internal sealed class FakeTargetChannelProvider(string login = "test-channel") : ITargetChannelProvider
{
    public string Login { get; set; } = login;

    public string OwnChannel { get; set; } = login;

    public bool IsDebugSession { get; set; }

    public bool IsSendingAllowed { get; set; } = true;

    public bool IsProfileIsolated { get; set; }

    public TargetChannelState Current => new(Login, OwnChannel, IsDebugSession, IsSendingAllowed, IsProfileIsolated);

    public void BeginSession()
    {
    }

    public void EndSession()
    {
    }

    public static FakeTargetChannelProvider Foreign(string login, string ownChannel, bool isSendingAllowed = false)
    {
        return new(login)
        {
            OwnChannel = ownChannel,
            IsDebugSession = true,
            IsSendingAllowed = isSendingAllowed,
        };
    }
}
