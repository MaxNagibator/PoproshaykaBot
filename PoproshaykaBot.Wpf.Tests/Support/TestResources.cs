using KeepShell.Testing;
using System.Windows;

namespace PoproshaykaBot.Wpf.Tests.Support;

internal static class TestResources
{
    public static void Ensure(IEnumerable<string> sources)
    {
        PackScheme.Ensure();

        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var merged = application.Resources.MergedDictionaries;

        foreach (var source in sources)
        {
            if (merged.All(dictionary => dictionary.Source?.OriginalString != source))
            {
                merged.Add(new ResourceDictionary { Source = new(source) });
            }
        }
    }
}
