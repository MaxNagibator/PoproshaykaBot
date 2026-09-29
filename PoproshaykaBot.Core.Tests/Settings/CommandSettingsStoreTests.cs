using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Settings.Stores;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Settings;

[TestFixture]
public sealed class CommandSettingsStoreTests
{
    [Flags]
    private enum TargetBeforeCaller
    {
        None = 0,
        Chat = 1,
        Overlay = 2,
    }

    [Flags]
    private enum TargetBeforeWhisper
    {
        None = 0,
        Chat = 1,
        Overlay = 2,
        Caller = 4,
    }

    private sealed class LegacySettings<TTarget> where TTarget : struct, Enum
    {
        public TTarget DefaultResponseTarget { get; set; }

        public Dictionary<string, LegacyOverride<TTarget>> Commands { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class LegacyOverride<TTarget> where TTarget : struct, Enum
    {
        public bool Enabled { get; set; } = true;

        public TTarget? ResponseTarget { get; set; }
    }

    private LegacySettings<TTarget> ReadAsLegacy<TTarget>() where TTarget : struct, Enum
    {
        return JsonSerializer.Deserialize<LegacySettings<TTarget>>(File.ReadAllText(_path), JsonStoreOptions.Default)!;
    }

    private static CommandResponseTarget SeenByLegacy<TTarget>(TTarget? target) where TTarget : struct, Enum
    {
        var known = Enum.GetValues<TTarget>().Aggregate(0, static (mask, value) => mask | Convert.ToInt32(value));

        return (CommandResponseTarget)(Convert.ToInt32(target ?? default) & known);
    }

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "poproshayka-command-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _path = Path.Combine(_root, "commands.json");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private string _root = null!;
    private string _path = null!;

    [Test]
    public void Свежий_профиль_отвечает_чат_и_оверлей_для_любой_команды()
    {
        var settings = new CommandSettingsStore(null, _path).Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.IsEnabled("ранг"), Is.True);
            Assert.That(settings.ResolveResponseTarget("ранг"),
                Is.EqualTo(CommandResponseTarget.Chat | CommandResponseTarget.Overlay));
        });
    }

    [Test]
    public void Переопределение_переживает_перезапуск_и_читается_без_учёта_регистра()
    {
        var store = new CommandSettingsStore(null, _path);

        store.Mutate(settings =>
        {
            settings.DefaultResponseTarget = CommandResponseTarget.Chat;

            settings.Commands["ранг"] = new()
            {
                Enabled = false,
                ResponseTarget = CommandResponseTarget.Overlay,
            };
        });

        var restored = new CommandSettingsStore(null, _path).Load();

        Assert.Multiple(() =>
        {
            Assert.That(restored.DefaultResponseTarget, Is.EqualTo(CommandResponseTarget.Chat));
            Assert.That(restored.IsEnabled("РАНГ"), Is.False);
            Assert.That(restored.ResolveResponseTarget("Ранг"), Is.EqualTo(CommandResponseTarget.Overlay));
            Assert.That(restored.ResolveResponseTarget("донат"), Is.EqualTo(CommandResponseTarget.Chat));
        });
    }

    [Test]
    public void Неизвестный_бит_цели_из_правленого_руками_файла_отбрасывается()
    {
        File.WriteAllText(_path,
            """
            {
              "defaultResponseTarget": 48,
              "commands": {
                "ранг": { "enabled": true, "responseTarget": 18 }
              }
            }
            """);

        var settings = new CommandSettingsStore(null, _path).Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.ResolveResponseTarget("донат"), Is.EqualTo(CommandResponseTarget.None));
            Assert.That(settings.ResolveResponseTarget("ранг"), Is.EqualTo(CommandResponseTarget.Overlay));
        });
    }

    [Test]
    public void Пустая_запись_команды_не_роняет_разбор_сообщения()
    {
        File.WriteAllText(_path,
            """
            {
              "commands": {
                "ранг": null
              }
            }
            """);

        var settings = new CommandSettingsStore(null, _path).Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.IsEnabled("ранг"), Is.True);
            Assert.That(settings.ResolveResponseTarget("ранг"),
                Is.EqualTo(CommandResponseTarget.Chat | CommandResponseTarget.Overlay));
        });
    }

    [Test]
    public void Права_команды_переживают_перезапуск_и_снимаются_обратно()
    {
        var store = new CommandSettingsStore(null, _path);

        store.Mutate(settings => settings.Commands["ранг"] = new()
        {
            Access = CommandAccessLevel.Moderators,
        });

        var afterRestart = new CommandSettingsStore(null, _path);

        var saved = afterRestart.Load().ReadAccess("РАНГ");

        afterRestart.Mutate(settings => settings.Commands["ранг"].Access = null);

        var cleared = new CommandSettingsStore(null, _path).Load();

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.EqualTo(CommandAccessLevel.Moderators), "Право переживает перезапуск");
            Assert.That(cleared.ReadAccess("ранг"), Is.Null, "Снятое право не возвращается само");
            Assert.That(cleared.ResolveAccessLevel("ранг"), Is.EqualTo(CommandAccessLevel.Everyone));
        });
    }

    [TestCase(-3, CommandAccessLevel.Everyone)]
    [TestCase(1, CommandAccessLevel.Moderators)]
    [TestCase(99, CommandAccessLevel.Broadcaster)]
    public void Неизвестный_уровень_прав_из_файла_зажимается_до_известного(int stored, CommandAccessLevel expected)
    {
        File.WriteAllText(_path,
            $$"""
              {
                "commands": {
                  "ранг": { "enabled": true, "access": {{stored}} }
                }
              }
              """);

        var settings = new CommandSettingsStore(null, _path).Load();

        Assert.That(settings.ResolveAccessLevel("ранг"), Is.EqualTo(expected));
    }

    [Test]
    public void Файл_прошлой_сборки_читается_как_прежде()
    {
        File.WriteAllText(_path,
            """
            {
              "defaultResponseTarget": 3,
              "commands": {
                "ранг": { "enabled": false, "responseTarget": 1 }
              }
            }
            """);

        var settings = new CommandSettingsStore(null, _path).Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.IsEnabled("ранг"), Is.False);
            Assert.That(settings.ResolveResponseTarget("ранг"), Is.EqualTo(CommandResponseTarget.Chat));
            Assert.That(settings.ReadAccess("ранг"), Is.Null, "Поля прав в старом файле нет – это «как в коде»");
            Assert.That(settings.ResolveResponseTarget("донат"), Is.EqualTo(CommandSettings.ChatAndOverlay));
        });
    }

    [Test]
    public void Новый_файл_читается_прошлой_сборкой_без_потери_включения_и_цели()
    {
        var store = new CommandSettingsStore(null, _path);

        store.Mutate(settings =>
        {
            settings.DefaultResponseTarget = CommandSettings.ChatAndOverlay;

            settings.Commands["ранг"] = new()
            {
                Enabled = false,
                ResponseTarget = CommandResponseTarget.Overlay,
                Access = CommandAccessLevel.Broadcaster,
            };

            settings.Commands["донат"] = new()
            {
                ResponseTarget = CommandSettings.CallerOnly,
            };
        });

        var legacy = ReadAsLegacy<TargetBeforeCaller>();

        var rank = legacy.Commands["ранг"];
        var donate = legacy.Commands["донат"];

        Assert.Multiple(() =>
        {
            Assert.That(rank.Enabled, Is.False, "Выключение команды старая сборка читает как раньше");
            Assert.That(SeenByLegacy(rank.ResponseTarget), Is.EqualTo(CommandResponseTarget.Overlay));
            Assert.That(SeenByLegacy(donate.ResponseTarget),
                Is.EqualTo(CommandResponseTarget.Chat),
                "Цель «ответом на сообщение» записана как чат плюс новый бит, поэтому старая сборка отвечает в чат, а не молчит");

            Assert.That(SeenByLegacy<TargetBeforeCaller>(legacy.DefaultResponseTarget), Is.EqualTo(CommandSettings.ChatAndOverlay));
        });
    }

    [Test]
    public void Цель_шёпотом_прошлые_сборки_читают_как_ответ_в_чат()
    {
        new CommandSettingsStore(null, _path).Mutate(settings => settings.Commands["донат"] = new()
        {
            ResponseTarget = CommandSettings.WhisperToCaller,
        });

        Assert.Multiple(() =>
        {
            Assert.That(SeenByLegacy(ReadAsLegacy<TargetBeforeWhisper>().Commands["донат"].ResponseTarget),
                Is.EqualTo(CommandSettings.CallerOnly),
                "Шёпот записан поверх «ответом на сообщение», поэтому прошлая сборка отвечает реплаем");

            Assert.That(SeenByLegacy(ReadAsLegacy<TargetBeforeCaller>().Commands["донат"].ResponseTarget),
                Is.EqualTo(CommandResponseTarget.Chat),
                "Сборка до цели «ответом на сообщение» отвечает в чат, но не молчит");
        });
    }

    [TestCase(CommandResponseTarget.Whisper)]
    [TestCase(CommandResponseTarget.Caller)]
    public void Модификатор_без_чата_пишется_так_чтобы_прошлая_сборка_не_замолчала(CommandResponseTarget target)
    {
        new CommandSettingsStore(null, _path).Mutate(settings => settings.Commands["донат"] = new()
        {
            ResponseTarget = target,
        });

        Assert.That(SeenByLegacy(ReadAsLegacy<TargetBeforeCaller>().Commands["донат"].ResponseTarget),
            Is.EqualTo(CommandResponseTarget.Chat));
    }

    [TestCase("13", CommandSettings.WhisperToCaller)]
    [TestCase("8", CommandSettings.WhisperToCaller)]
    [TestCase("\"Chat, Caller\"", CommandSettings.CallerOnly)]
    [TestCase("\"Overlay\"", CommandResponseTarget.Overlay)]
    [TestCase("\"Chat, Pigeon\"", CommandResponseTarget.Chat)]
    public void Цель_из_файла_читается_и_числом_и_строкой_прошлой_сборки(string stored, CommandResponseTarget expected)
    {
        File.WriteAllText(_path,
            $$"""
              {
                "defaultResponseTarget": {{stored}},
                "commands": {
                  "донат": { "enabled": true, "responseTarget": {{stored}} }
                }
              }
              """);

        var settings = new CommandSettingsStore(null, _path).Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.ResolveResponseTarget("донат"), Is.EqualTo(expected));
            Assert.That(settings.ResolveResponseTarget("ранг"), Is.EqualTo(expected), "Общая цель читается тем же правилом");
        });
    }

    [Test]
    public void Цель_шёпотом_уходит_лично_а_не_в_общий_чат()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CommandSettings.KnownTargets.HasFlag(CommandResponseTarget.Whisper), Is.True);
            Assert.That(CommandSettings.WhisperToCaller.WhispersToCaller(), Is.True);
            Assert.That(CommandSettings.WhisperToCaller.GoesToChat(), Is.False, "Чат для шёпота – только откат");
            Assert.That(CommandSettings.WhisperToCaller.GoesToOverlay(), Is.False);
        });
    }

    [Test]
    public void Повреждённый_файл_оставляет_копию_и_даёт_дефолты()
    {
        File.WriteAllText(_path, "{ это не json");

        var settings = new CommandSettingsStore(null, _path).Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.IsEnabled("ранг"), Is.True);
            Assert.That(Directory.GetFiles(_root, "*invalid*"), Is.Not.Empty);
        });
    }

    [TestCase("""{"commands":{"profile":{"enabled":false}}}""", false, false)]
    [TestCase("""{"commands":{"PROFILE":{"enabled":false}}}""", false, false)]
    [TestCase("""{"commands":{"profile":{"enabled":false},"пресет":{"enabled":true}}}""", true, true)]
    [TestCase("""{"commands":{"ранг":{"enabled":false}}}""", true, false)]
    public void Настройки_прежнего_имени_переезжают_к_пресету_только_когда_своих_у_него_нет(
        string json,
        bool presetEnabled,
        bool profileKept)
    {
        File.WriteAllText(_path, json);

        var store = new CommandSettingsStore(null, _path);
        var settings = store.Load();

        store.Mutate(_ => { });

        using var written = JsonDocument.Parse(File.ReadAllText(_path));
        var keys = written.RootElement.GetProperty("commands").EnumerateObject().Select(property => property.Name).ToList();
        var restored = new CommandSettingsStore(null, _path).Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.IsEnabled("пресет"), Is.EqualTo(presetEnabled));
            Assert.That(settings.IsEnabled("мойпрофиль"), Is.True, "Прежний ключ не достаётся команде, забравшей токен");
            Assert.That(settings.Commands.ContainsKey("profile"), Is.EqualTo(profileKept));
            Assert.That(keys.Contains("profile", StringComparer.OrdinalIgnoreCase), Is.EqualTo(profileKept), "В файл уходит перенесённый ключ");
            Assert.That(restored.IsEnabled("пресет"), Is.EqualTo(presetEnabled));
            Assert.That(restored.IsEnabled("ранг"), Is.EqualTo(!json.Contains("ранг")));
        });
    }
}
