using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Runtime;
using System.Security.Cryptography;
using System.Text;

namespace PoproshaykaBot.Core.Tests.Infrastructure.Runtime;

[TestFixture]
public class SingleInstanceGateTests
{
    [Test]
    public void BuildMutexName_MatchesLegacyHostFormat()
    {
        var key = AppPaths.BaseDirectory.ToLowerInvariant();
        var expected = $"Local\\PoproshaykaBot-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16]}";

        var name = SingleInstanceGate.BuildMutexName();

        Assert.Multiple(() =>
        {
            Assert.That(name, Is.EqualTo(expected));
            Assert.That(name, Does.Match(@"^Local\\PoproshaykaBot-[0-9A-F]{16}$"));
        });
    }

    [Test]
    public void TryAcquire_SecondCall_ReturnsNull()
    {
        var mutexName = CreateMutexName();
        using var first = SingleInstanceGate.TryAcquire(false, mutexName);

        var second = SingleInstanceGate.TryAcquire(false, mutexName);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Null);
        });
    }

    [Test]
    public void TryAcquire_AllowingExistingInstance_ReturnsHandleAnyway()
    {
        var mutexName = CreateMutexName();
        using var first = SingleInstanceGate.TryAcquire(false, mutexName);

        using var second = SingleInstanceGate.TryAcquire(true, mutexName);

        Assert.That(second, Is.Not.Null);
    }

    private static string CreateMutexName()
    {
        return $"Local\\PoproshaykaBot-test-{Guid.NewGuid():N}";
    }
}
