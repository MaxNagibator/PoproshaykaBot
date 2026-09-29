using PoproshaykaBot.Core.Settings.Obs;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PoproshaykaBot.Core.Tests.Settings;

[TestFixture]
public sealed class AnimationsDemoUiSyncTests
{
    [TestCaseSource(nameof(GetMutableSourceProperties))]
    public void EveryObsChatSettingsProperty_HasMatchingCfgControl(PropertyInfo prop)
    {
        var camelCase = ToCamelCase(prop.Name);

        if (prop.PropertyType == typeof(Color))
        {
            var configJs = File.ReadAllText(LocateAsset("animations-demo-config.js"));
            var pattern = $"""key\s*:\s*['"]{Regex.Escape(camelCase)}['"]""";
            Assert.That(configJs, Does.Match(pattern));

            return;
        }

        var html = File.ReadAllText(LocateAsset("animations-demo.html"));
        var expectedId = "cfg-" + camelCase;
        var idPattern = $"""\bid\s*=\s*["']{Regex.Escape(expectedId)}["']""";
        Assert.That(html, Does.Match(idPattern));
    }

    [TestCaseSource(nameof(GetMutableSourceProperties))]
    public void EveryObsChatSettingsProperty_IsBoundInConfigJs(PropertyInfo prop)
    {
        if (prop.PropertyType == typeof(Color))
        {
            return;
        }

        var camelCase = ToCamelCase(prop.Name);
        var configJs = File.ReadAllText(LocateAsset("animations-demo-config.js"));

        var bindPattern = $"""bindControl\(\s*['"]cfg-{Regex.Escape(camelCase)}['"]\s*,\s*['"]{Regex.Escape(camelCase)}['"]""";
        Assert.That(configJs, Does.Match(bindPattern));
    }

    [TestCaseSource(nameof(GetIntegerSourceProperties))]
    public void EveryIntegerObsChatSettingsProperty_HasEntryInRangesSchema(PropertyInfo prop)
    {
        var camelCase = ToCamelCase(prop.Name);

        Assert.That(ObsChatRangesSchema.All, Does.ContainKey(camelCase));
    }

    [Test]
    public void EveryRangesSchemaKey_MapsToIntegerObsChatSettingsProperty()
    {
        var integerProperties = typeof(ObsChatSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(int))
            .Select(p => ToCamelCase(p.Name))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var key in ObsChatRangesSchema.All.Keys)
        {
            Assert.That(integerProperties, Does.Contain(key));
        }
    }

    [Test]
    public void RangeFieldsInConfigJs_MatchObsChatRangesSchemaKeys()
    {
        var configJs = File.ReadAllText(LocateAsset("animations-demo-config.js"));
        var jsKeys = ExtractRangeFieldKeys(configJs);

        Assert.That(jsKeys, Is.EquivalentTo(ObsChatRangesSchema.All.Keys));
    }

    private static IReadOnlyList<string> ExtractRangeFieldKeys(string jsSource)
    {
        var arrayMatch = Regex.Match(jsSource, @"const\s+rangeFields\s*=\s*\[(?<body>[\s\S]*?)\];");
        if (!arrayMatch.Success)
        {
            throw new InvalidOperationException("Не нашли массив 'rangeFields' в animations-demo-config.js. "
                                                + "Возможно, его переименовали – обновите тест или верните прежнее имя.");
        }

        return Regex.Matches(arrayMatch.Groups["body"].Value,
                """\[\s*['"][^'"]+['"]\s*,\s*['"](?<key>[^'"]+)['"]\s*\]""")
            .Select(m => m.Groups["key"].Value)
            .ToArray();
    }

    private static IEnumerable<PropertyInfo> GetMutableSourceProperties()
    {
        return typeof(ObsChatSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p is { CanRead: true, CanWrite: true });
    }

    private static IEnumerable<PropertyInfo> GetIntegerSourceProperties()
    {
        return GetMutableSourceProperties().Where(p => p.PropertyType == typeof(int));
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string LocateAsset(string fileName)
    {
        var current = AppContext.BaseDirectory;

        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(current, "PoproshaykaBot.Core", "Assets", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(current);
            if (parent == null)
            {
                break;
            }

            current = parent.FullName;
        }

        throw new FileNotFoundException($"Не найден ассет {fileName} (нужен для проверки UI-полноты демки).");
    }
}
