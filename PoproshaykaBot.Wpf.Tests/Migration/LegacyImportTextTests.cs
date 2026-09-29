using PoproshaykaBot.Core.Settings.Migrations.LegacyImport;
using PoproshaykaBot.Wpf.ViewModels.Migration;

namespace PoproshaykaBot.Wpf.Tests.Migration;

[TestFixture]
public class LegacyImportTextTests
{
    [Test]
    public void A_file_left_behind_keeps_the_headline_from_claiming_a_transfer()
    {
        var result = Build(inPlace: true, unmigrated: ["accounts.json"]);

        Assert.Multiple(() =>
        {
            Assert.That(LegacyImportText.BuildHeadline(result), Is.EqualTo("Часть данных осталась на прежнем месте."),
                "Ничего не переехало, поэтому «перенесено» было бы неправдой.");
            Assert.That(LegacyImportText.DescribeUnmigrated(result), Does.Contain("данные входа в Twitch"),
                "Файл называется по назначению, а не путём.");
            Assert.That(LegacyImportText.DescribeUnmigrated(result), Does.Contain("Ничего не потеряно"),
                "Человеку нужно знать, что делать ничего не надо.");
        });
    }

    [Test]
    public void Nothing_left_behind_leaves_the_result_screen_without_that_line()
    {
        Assert.That(LegacyImportText.DescribeUnmigrated(Build(copied: ["settings/settings.json"])), Is.Null);
    }

    [TestCase(new string[0], new string[0], false, "Переносить было нечего: все файлы уже на месте.")]
    [TestCase(new[] { "settings/settings.json" }, new string[0], false, "Данные перенесены.")]
    [TestCase(new string[0], new string[0], true, "Данные остались на месте и подготовлены к работе.")]
    [TestCase(new[] { "settings/settings.json" }, new[] { "accounts.json" }, false, "Данные перенесены не полностью.")]
    [TestCase(new string[0], new[] { "accounts.json" }, false, "Перенести данные не удалось.")]
    public void The_headline_names_what_actually_happened(string[] copied, string[] failed, bool inPlace, string expected)
    {
        var result = Build(copied, inPlace: inPlace, failures: [.. failed.Select(name => new LegacyImportFailure(name, "io"))]);

        Assert.That(LegacyImportText.BuildHeadline(result), Is.EqualTo(expected));
    }

    [TestCase(new[] { "users_statistics.json" }, "Перенесённые данные вступят в силу после перезапуска приложения. До перезапуска статистика не сохраняется – чтобы не затереть перенесённое.")]
    [TestCase(new[] { "settings/settings.json", "bot_statistics.json" }, "Перенесённые данные вступят в силу после перезапуска приложения. До перезапуска настройки и статистика не сохраняются – чтобы не затереть перенесённое.")]
    [TestCase(new[] { "stream_sessions.json" }, "Перенесённые данные вступят в силу после перезапуска приложения. До перезапуска история стримов не сохраняется – чтобы не затереть перенесённое.")]
    [TestCase(new[] { "users_statistics.json", "stream_sessions.json" }, "Перенесённые данные вступят в силу после перезапуска приложения. До перезапуска статистика и история стримов не сохраняются – чтобы не затереть перенесённое.")]
    [TestCase(new[] { "polls-history.json" }, "Перенесённые данные вступят в силу после перезапуска приложения. До перезапуска история голосований не сохраняется – чтобы не затереть перенесённое.")]
    [TestCase(new[] { "users_statistics.json", "stream_sessions.json", "polls-history.json" }, "Перенесённые данные вступят в силу после перезапуска приложения. До перезапуска статистика, история стримов и история голосований не сохраняются – чтобы не затереть перенесённое.")]
    [TestCase(new[] { "settings/polls.json" }, "Перенесённые данные вступят в силу после перезапуска приложения. До перезапуска настройки не сохраняются – чтобы не затереть перенесённое.")]
    [TestCase(new[] { "settings/ui-preferences.toml" }, "Перенесённые данные вступят в силу после перезапуска приложения.")]
    public void The_restart_line_promises_a_restart_and_warns_about_every_file_whose_writing_stopped(string[] copied, string expected)
    {
        Assert.That(LegacyImportText.BuildRestartNotice(Build(copied), isSettingsEntry: true), Is.EqualTo(expected));
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public void The_restart_line_belongs_to_the_settings_entry_with_something_copied(bool isSettingsEntry, bool copiedNothing)
    {
        var result = Build(copiedNothing ? [] : ["settings/settings.json"]);

        Assert.That(LegacyImportText.BuildRestartNotice(result, isSettingsEntry), Is.Null,
            "На старте перезапуск и так впереди, а без переноса сообщать не о чем.");
    }

    private static LegacyImportResult Build(
        IReadOnlyList<string>? copied = null,
        bool inPlace = false,
        IReadOnlyList<LegacyImportFailure>? failures = null,
        IReadOnlyList<string>? unmigrated = null)
    {
        return new()
        {
            SourcePath = @"C:\old",
            TargetPath = @"C:\new",
            CopiedFiles = copied ?? [],
            SkippedFiles = [],
            Failures = failures ?? [],
            RequiresReauthorization = false,
            IsInPlaceMigration = inPlace,
            UnmigratedLegacyFiles = unmigrated ?? [],
        };
    }
}
