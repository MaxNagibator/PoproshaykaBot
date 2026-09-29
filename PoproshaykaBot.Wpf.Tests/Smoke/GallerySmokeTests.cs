using KeepShell.Automation;
using PoproshaykaBot.Wpf.Bootstrap;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace PoproshaykaBot.Wpf.Tests.Smoke;

[TestFixture]
[NonParallelizable]
public class GallerySmokeTests
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(3);

    private string _baseDirectory = string.Empty;
    private string _outputDirectory = string.Empty;

    [SetUp]
    public void Setup()
    {
        _baseDirectory = GalleryTestProfile.Create();
        _outputDirectory = Path.Combine(_baseDirectory, "gallery-out");
    }

    [TearDown]
    public void TearDown()
    {
        GalleryTestProfile.Delete(_baseDirectory);
    }

    [Test]
    public void Gallery_ShouldCaptureOverviewAndDialogs()
    {
        var pages = string.Join(',', GalleryDialogs.BroadcastProfile, SectionKeys.Overview, GalleryDialogs.ChatBlockers);
        var (exitCode, output) = RunGallery(pages, AppThemes.LightKey);

        Assert.That(exitCode, Is.Zero, $"Прогон галереи должен завершиться кодом 0. Вывод процесса:{Environment.NewLine}{output}");

        var indexPath = Path.Combine(_outputDirectory, "index.json");
        Assert.That(File.Exists(indexPath), Is.True, $"Индекс прогона не найден: {indexPath}");

        AssertNoBindingErrors();

        using var index = JsonDocument.Parse(File.ReadAllText(indexPath));

        var unknown = index.RootElement.GetProperty("unknown")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        var frames = index.RootElement.GetProperty("frames")
            .EnumerateArray()
            .Select(frame => (
                Kind: frame.GetProperty("kind").GetString(),
                Name: frame.GetProperty("name").GetString(),
                File: frame.GetProperty("file").GetString()))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(unknown, Is.Empty, "Все запрошенные ключи должны быть распознаны");
            Assert.That(frames.Select(frame => frame.Name),
                Is.EqualTo(new[] { GalleryDialogs.BroadcastProfile, SectionKeys.Overview, GalleryDialogs.ChatBlockers }),
                "Индекс должен перечислять три запрошенных кадра");
            Assert.That(frames.Select(frame => frame.Kind),
                Is.EqualTo(new[] { GalleryDialogs.Kind, "page", GalleryDialogs.Kind }),
                "Кейс диалога должен попадать в индекс со своим видом");
            Assert.That(frames.Select(frame => frame.File),
                Is.EqualTo(new[] { "dialog-broadcast-profile-light.png", "overview-light.png", "dialog-chat-blockers-light.png" }),
                "Двоеточие в ключе диалога должно превращаться в дефис имени файла");
        });

        var broadcastProfile = ReadFrame(frames[0].File);
        var overview = ReadFrame(frames[1].File);
        var chatBlockers = ReadFrame(frames[2].File);

        Assert.Multiple(() =>
        {
            Assert.That(overview, Is.Not.Empty, "Кадр страницы обзора пуст");
            Assert.That(broadcastProfile, Is.Not.Empty, "Кадр диалога профиля вещания пуст");
            Assert.That(chatBlockers, Is.Not.Empty, "Кадр диалога блокировки баннеров пуст");
            Assert.That(overview, Is.Not.EqualTo(broadcastProfile), "Модалка осталась открытой – кадр страницы совпал с кадром диалога над ней");
            Assert.That(chatBlockers, Is.Not.EqualTo(overview), "Диалог блокировки баннеров не виден в кадре – он совпал со страницей под ним");
            Assert.That(chatBlockers, Is.Not.EqualTo(broadcastProfile), "Диалоги сняты одинаково – предыдущая модалка осталась в кадре");
        });
    }

    [Test]
    public void Gallery_ShouldCaptureUsersWithAndWithoutSelection()
    {
        var pages = string.Join(',', SectionKeys.UsersSelected, SectionKeys.Users);
        var (exitCode, output) = RunGallery(pages, AppThemes.LightKey);

        Assert.That(exitCode, Is.Zero, $"Прогон галереи должен завершиться кодом 0. Вывод процесса:{Environment.NewLine}{output}");

        AssertNoBindingErrors();

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(_outputDirectory, "index.json")));

        var frames = index.RootElement.GetProperty("frames")
            .EnumerateArray()
            .Select(frame => (Name: frame.GetProperty("name").GetString(), File: frame.GetProperty("file").GetString()))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(frames.Select(frame => frame.Name),
                Is.EqualTo(new[] { SectionKeys.UsersSelected, SectionKeys.Users }),
                "Кейс с выбранной строкой должен попасть в индекс своим именем");
            Assert.That(frames.Select(frame => frame.File),
                Is.EqualTo(new[] { "users-selected-light.png", "users-light.png" }),
                "Двоеточие в имени кейса должно превращаться в дефис имени файла");
        });

        var selected = ReadFrame(frames[0].File);
        var plain = ReadFrame(frames[1].File);

        Assert.That(selected, Is.Not.EqualTo(plain),
            "Кадры совпали: либо рейтинг пуст без подключения бота, либо выбор строки не поставился или не снялся после кадра");
    }

    [Test]
    public void Gallery_ShouldSkipUnknownKeysAndFinishSuccessfully()
    {
        var pages = string.Join(',', SectionKeys.Overview, "dialog:no-such-dialog", "no-such-page");
        var (exitCode, output) = RunGallery(pages, AppThemes.LightKey);

        Assert.That(exitCode, Is.Zero, $"Неизвестный ключ не должен ронять прогон. Вывод процесса:{Environment.NewLine}{output}");

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(_outputDirectory, "index.json")));

        var unknown = index.RootElement.GetProperty("unknown")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        var frames = index.RootElement.GetProperty("frames")
            .EnumerateArray()
            .Select(frame => frame.GetProperty("name").GetString())
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(unknown, Is.EqualTo(new[] { "dialog:no-such-dialog", "no-such-page" }), "Нераспознанные ключи должны уехать в unknown");
            Assert.That(frames, Is.EqualTo(new[] { SectionKeys.Overview }), "Сняться должен только распознанный кейс");
        });
    }

    private void AssertNoBindingErrors()
    {
        var reportPath = Path.Combine(_outputDirectory, GalleryRunner.BindingErrorsFileName);
        Assert.That(File.Exists(reportPath), Is.True, $"Отчёт об ошибках привязок не найден: {reportPath}");

        using var report = JsonDocument.Parse(File.ReadAllText(reportPath));

        var errors = report.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Select(error => $"{error.GetProperty("case").GetString()}/{error.GetProperty("theme").GetString()}: {error.GetProperty("message").GetString()}")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(errors, Is.Empty, () => string.Join(Environment.NewLine, errors));
            Assert.That(report.RootElement.GetProperty("count").GetInt32(), Is.Zero, "Счётчик ошибок привязок не совпал с пустым перечнем");
        });
    }

    private byte[] ReadFrame(string? fileName)
    {
        Assert.That(fileName, Is.Not.Null.And.Not.Empty, "Индекс не назвал файл кадра");

        var path = Path.Combine(_outputDirectory, fileName!);
        Assert.That(File.Exists(path), Is.True, $"Кадр не найден: {path}");

        return File.ReadAllBytes(path);
    }

    private (int ExitCode, string Output) RunGallery(string pages, string themes)
    {
        var appPath = SmokeTestSession.ResolveAppExePath();

        if (!File.Exists(appPath))
        {
            Assert.Fail($"Исполняемый файл приложения не найден: {appPath}. Соберите PoproshaykaBot.Wpf перед запуском UI-тестов.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = appPath,
            WorkingDirectory = Path.GetDirectoryName(appPath)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add(GalleryHost.ArgumentName);
        startInfo.ArgumentList.Add(_outputDirectory);
        startInfo.ArgumentList.Add(GalleryArguments.PagesKey);
        startInfo.ArgumentList.Add(pages);
        startInfo.ArgumentList.Add(GalleryArguments.ThemesKey);
        startInfo.ArgumentList.Add(themes);

        startInfo.EnvironmentVariables[SmokeTestSession.BaseDirectoryEnvVar] = _baseDirectory;

        var output = new StringBuilder();

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException($"Не удалось запустить процесс галереи: {appPath}");

        process.OutputDataReceived += (_, args) => Append(output, args.Data);
        process.ErrorDataReceived += (_, args) => Append(output, args.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit((int)RunTimeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"Прогон галереи не завершился за {RunTimeout}. Вывод процесса:{Environment.NewLine}{output}");
        }

        process.WaitForExit();

        return (process.ExitCode, output.ToString());
    }

    private static void Append(StringBuilder output, string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        lock (output)
        {
            output.AppendLine(line);
        }
    }
}
