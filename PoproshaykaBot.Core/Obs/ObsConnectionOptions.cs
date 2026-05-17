using System.Globalization;

namespace PoproshaykaBot.Core.Obs;

public sealed record ObsConnectionOptions(string Host, int Port, string Password)
{
    public Uri Uri => new(string.Create(CultureInfo.InvariantCulture, $"ws://{Host}:{Port}"));
}
