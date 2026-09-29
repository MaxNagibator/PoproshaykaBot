using PoproshaykaBot.Core.Server.Images;
using System.Net;

namespace PoproshaykaBot.Core.Tests.Server.Images;

[TestFixture]
public sealed class PublicAddressConnectorTests
{
    [TestCase("127.0.0.1")]
    [TestCase("127.9.9.9")]
    [TestCase("0.0.0.0")]
    [TestCase("10.0.0.7")]
    [TestCase("172.16.0.1")]
    [TestCase("172.31.255.254")]
    [TestCase("192.168.1.1")]
    [TestCase("169.254.169.254")]
    [TestCase("100.64.0.1")]
    [TestCase("198.18.0.1")]
    [TestCase("224.0.0.1")]
    [TestCase("255.255.255.255")]
    [TestCase("::1")]
    [TestCase("fe80::1")]
    [TestCase("fd00::1")]
    [TestCase("::ffff:192.168.0.1")]
    [TestCase("::ffff:127.0.0.1")]
    public void IsRoutable_LocalAddress_IsRefused(string address)
    {
        Assert.That(PublicAddressConnector.IsRoutable(IPAddress.Parse(address)), Is.False);
    }

    [TestCase("1.1.1.1")]
    [TestCase("8.8.8.8")]
    [TestCase("151.101.1.1")]
    [TestCase("172.32.0.1")]
    [TestCase("192.169.0.1")]
    [TestCase("100.128.0.1")]
    [TestCase("2606:4700::1111")]
    [TestCase("::ffff:8.8.8.8")]
    public void IsRoutable_PublicAddress_IsAllowed(string address)
    {
        Assert.That(PublicAddressConnector.IsRoutable(IPAddress.Parse(address)), Is.True);
    }
}
