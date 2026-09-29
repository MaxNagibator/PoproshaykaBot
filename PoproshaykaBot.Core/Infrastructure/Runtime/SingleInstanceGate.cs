using System.Security.Cryptography;
using System.Text;

namespace PoproshaykaBot.Core.Infrastructure.Runtime;

public static class SingleInstanceGate
{
    public static Mutex? TryAcquire(bool allowExistingInstance)
    {
        return TryAcquire(allowExistingInstance, BuildMutexName());
    }

    internal static Mutex? TryAcquire(bool allowExistingInstance, string mutexName)
    {
        var mutex = new Mutex(true, mutexName, out var createdNew);

        if (createdNew || allowExistingInstance)
        {
            return mutex;
        }

        mutex.Dispose();
        return null;
    }

    public static string BuildMutexName()
    {
        var key = AppPaths.BaseDirectory.ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return $"Local\\PoproshaykaBot-{hash[..16]}";
    }
}
