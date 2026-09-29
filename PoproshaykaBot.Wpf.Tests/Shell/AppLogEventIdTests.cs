using System.Reflection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Wpf.Bootstrap;

namespace PoproshaykaBot.Wpf.Tests.Shell;

[TestFixture]
public class AppLogEventIdTests
{
    [Test]
    public void All_LoggerMessage_EventIds_are_unique()
    {
        var appLogType = typeof(AppThemes).Assembly.GetType("PoproshaykaBot.Wpf.Bootstrap.AppLog", throwOnError: true)!;

        var ids = appLogType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(m => m.GetCustomAttribute<LoggerMessageAttribute>()?.EventId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        Assume.That(ids, Is.Not.Empty, "Reflection found no [LoggerMessage] attributes – feasibility caveat applies");

        var duplicates = ids
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.That(duplicates, Is.Empty,
            $"Duplicate EventIds in AppLog: {string.Join(", ", duplicates)}");
    }
}
