using System.Net;
using System.Net.Sockets;

namespace PoproshaykaBot.Core.Server.Images;

internal static class PublicAddressConnector
{
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var routable = Array.FindAll(addresses, IsRoutable);

        if (routable.Length == 0)
        {
            throw new HttpRequestException($"адрес хоста {context.DnsEndPoint.Host} ведёт не в интернет");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            await socket.ConnectAsync(routable, context.DnsEndPoint.Port, cancellationToken);

            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();

            throw;
        }
    }

    public static bool IsRoutable(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var value = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        if (IPAddress.IsLoopback(value)
            || value.IsIPv6LinkLocal
            || value.IsIPv6SiteLocal
            || value.IsIPv6UniqueLocal
            || value.IsIPv6Multicast)
        {
            return false;
        }

        if (value.AddressFamily is not AddressFamily.InterNetwork)
        {
            return !value.Equals(IPAddress.IPv6Any) && !value.Equals(IPAddress.IPv6None);
        }

        var octets = value.GetAddressBytes();

        return octets[0] switch
        {
            0 or 10 or 127 => false,
            100 => octets[1] is < 64 or > 127,
            169 => octets[1] is not 254,
            172 => octets[1] is < 16 or > 31,
            192 => octets[1] is not 168 && (octets[1] is not 0 || octets[2] is not 0),
            198 => octets[1] is not (18 or 19),
            >= 224 => false,
            _ => true,
        };
    }
}
