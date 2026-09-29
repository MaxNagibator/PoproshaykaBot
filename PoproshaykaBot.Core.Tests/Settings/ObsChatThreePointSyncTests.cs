using PoproshaykaBot.Core.Server.Obs;
using PoproshaykaBot.Core.Settings.Obs;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PoproshaykaBot.Core.Tests.Settings;

[TestFixture]
public sealed class ObsChatThreePointSyncTests
{
    [TestCaseSource(nameof(GetMutableSourceProperties))]
    public void MutatingObsChatSettingsField_ChangesObsChatCssSettingsOutput(PropertyInfo sourceProp)
    {
        var defaults = new ObsChatSettings();
        var mutated = new ObsChatSettings();

        var changed = TryMutate(sourceProp, mutated);
        Assert.That(changed, Is.True,
            $"Тест не смог мутировать ObsChatSettings.{sourceProp.Name}. "
            + "Добавьте поддержку типа в TryMutate.");

        var defaultCss = ObsChatCssSettings.FromObsChatSettings(defaults);
        var mutatedCss = ObsChatCssSettings.FromObsChatSettings(mutated);

        Assert.That(AreEqual(defaultCss, mutatedCss), Is.False,
            $"Изменение ObsChatSettings.{sourceProp.Name} не повлияло на ObsChatCssSettings – "
            + "пропустили шаг (1) three-point sync в FromObsChatSettings.");
    }

    [TestCaseSource(nameof(GetCssTargetProperties))]
    public void EveryObsChatCssSettingsField_IsConsumedInObsJs(PropertyInfo cssProp)
    {
        var jsPath = LocateOverlayJs();
        var jsSource = File.ReadAllText(jsPath);

        var camelCase = ToCamelCase(cssProp.Name);

        Assert.That(jsSource, Does.Contain(camelCase),
            $"Поле ObsChatCssSettings.{cssProp.Name} (JSON: {camelCase}) не используется в obs.js – "
            + "пропустили шаг (2) three-point sync.");
    }

    [Test]
    public void ValidEntryAnimationsInObsJs_MatchMessageAnimationTypeEntryValues()
    {
        var jsSource = File.ReadAllText(LocateOverlayJs());
        var jsValues = ExtractStringArrayLiteral(jsSource, "validEntryAnimations");

        Assert.That(jsValues, Is.EquivalentTo(MessageAnimationType.EntryValues),
            "Массив validEntryAnimations в obs.js рассинхронизирован с MessageAnimationType.EntryValues. "
            + "Шаг (3) three-point sync пропущен.");
    }

    [Test]
    public void ValidExitAnimationsInObsJs_MatchMessageAnimationTypeExitValuesWithoutNone()
    {
        var jsSource = File.ReadAllText(LocateOverlayJs());
        var jsValues = ExtractStringArrayLiteral(jsSource, "validExitAnimations");

        var expected = MessageAnimationType.ExitValues
            .Where(value => value != MessageAnimationType.None)
            .ToArray();

        Assert.That(jsValues, Is.EquivalentTo(expected),
            "Массив validExitAnimations в obs.js рассинхронизирован с MessageAnimationType.ExitValues "
            + "(без \"no-animation\", который обрабатывается отдельной веткой). Шаг (3) three-point sync пропущен.");
    }

    private static string[] ExtractStringArrayLiteral(string jsSource, string variableName)
    {
        var pattern = $@"const\s+{Regex.Escape(variableName)}\s*=\s*(?:new\s+Set\s*\()?\s*\[(?<body>[^\]]*)\]";
        var match = Regex.Match(jsSource, pattern);

        if (!match.Success)
        {
            throw new InvalidOperationException($"Не нашли массив '{variableName}' в obs.js. Возможно, его переименовали – "
                                                + "обновите тест или верните прежнее имя.");
        }

        return Regex.Matches(match.Groups["body"].Value, "'(?<value>[^']*)'")
            .Select(m => m.Groups["value"].Value)
            .ToArray();
    }

    private static readonly string[] ServerOnlyProperties =
    [
        nameof(ObsChatSettings.MessageImageRoles),
        nameof(ObsChatSettings.MessageImageAllowedHosts),
    ];

    [Test]
    public void ServerOnlyObsChatSettingsFields_StayOutOfObsChatCssSettings()
    {
        var cssNames = GetCssTargetProperties().Select(p => p.Name).ToArray();

        foreach (var name in ServerOnlyProperties)
        {
            Assert.That(cssNames, Does.Not.Contain(name),
                $"ObsChatSettings.{name} – политика картинок, её решает Core. "
                + "В браузер она не отдаётся, иначе белый список и роли уезжают на страницу оверлея.");
        }
    }

    private static IEnumerable<PropertyInfo> GetMutableSourceProperties()
    {
        return typeof(ObsChatSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p is { CanRead: true, CanWrite: true })
            .Where(p => !ServerOnlyProperties.Contains(p.Name, StringComparer.Ordinal));
    }

    private static IEnumerable<PropertyInfo> GetCssTargetProperties()
    {
        return typeof(ObsChatCssSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p is { CanRead: true, CanWrite: true });
    }

    private static bool TryMutate(PropertyInfo prop, ObsChatSettings target)
    {
        var current = prop.GetValue(target);

        switch (current)
        {
            case bool b:
                prop.SetValue(target, !b);
                return true;

            case int i:
                prop.SetValue(target, i + 1);
                return true;

            case string s when prop.Name.Contains("Animation"):
                prop.SetValue(target, MessageAnimationType.None);
                return !string.Equals((string?)prop.GetValue(target), s, StringComparison.Ordinal);

            case string s:
                prop.SetValue(target, s + "_mut");
                return true;

            case Color:
                prop.SetValue(target, Color.FromArgb(1, 2, 3, 4));
                return true;
        }

        return false;
    }

    private static bool AreEqual(ObsChatCssSettings a, ObsChatCssSettings b)
    {
        foreach (var p in typeof(ObsChatCssSettings).GetProperties())
        {
            if (!Equals(p.GetValue(a), p.GetValue(b)))
            {
                return false;
            }
        }

        return true;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string LocateOverlayJs()
    {
        var current = AppContext.BaseDirectory;

        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(current, "PoproshaykaBot.Core", "Assets", "obs.js");
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

        throw new FileNotFoundException("Не найден obs.js (нужен для проверки three-point sync).");
    }
}
