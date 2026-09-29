using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Diagnostics;
using System.Text;
using FlaUIApplication = FlaUI.Core.Application;

namespace PoproshaykaBot.WinForms.Tests;

internal sealed class SmokeTestSession : IDisposable
{
    public const string AppExeName = "PoproshaykaBot.WinForms.exe";
    public const string AppExePathEnvVar = "POPROSHAYKA_APP_EXE";
    private const string AppProjectName = "PoproshaykaBot.WinForms";
    private const string TestProjectName = "PoproshaykaBot.WinForms.Tests";
    public const string BaseDirectoryEnvVar = "POPROSHAYKA_BASE_DIR";

    private static readonly TimeSpan DefaultWindowAppearTimeout = TimeSpan.FromSeconds(30);

    private readonly UiSmokeLock _uiLock;

    private SmokeTestSession(string baseDirectory, FlaUIApplication app, UIA3Automation automation, Window mainWindow, UiSmokeLock uiLock)
    {
        BaseDirectory = baseDirectory;
        App = app;
        Automation = automation;
        MainWindow = mainWindow;
        _uiLock = uiLock;
    }

    public FlaUIApplication App { get; }

    public UIA3Automation Automation { get; }

    public Window MainWindow { get; }

    public string BaseDirectory { get; }

    public static SmokeTestSession Launch(SmokeTestSessionOptions? options = null)
    {
        options ??= new();

        var appPath = ResolveAppExePath();

        if (!File.Exists(appPath))
        {
            Assert.Fail($"Исполняемый файл приложения не найден: {appPath}. Соберите PoproshaykaBot.WinForms перед запуском UI-тестов.");
        }

        var uiLock = UiSmokeLock.Acquire();
        var baseDirectory = string.Empty;

        UIA3Automation? automation = null;
        FlaUIApplication? app = null;

        try
        {
            baseDirectory = CreateTempBaseDirectory();

            if (options.SeedConfiguredSettings)
            {
                SeedConfiguredSettings(baseDirectory);
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = appPath,
                Arguments = "--ui-smoke",
                WorkingDirectory = Path.GetDirectoryName(appPath)!,
                UseShellExecute = false,
            };

            processStartInfo.EnvironmentVariables[BaseDirectoryEnvVar] = baseDirectory;

            automation = new();
            app = FlaUIApplication.Launch(processStartInfo);

            var timeout = options.WindowAppearTimeout ?? DefaultWindowAppearTimeout;
            var mainWindow = WaitForFirstTopLevelWindow(app, automation, timeout)
                             ?? throw new InvalidOperationException($"Не удалось обнаружить ни одного top-level окна процесса в течение {timeout}");

            return new(baseDirectory, app, automation, mainWindow, uiLock);
        }
        catch
        {
            try
            {
                app?.Kill();
            }
            catch
            {
                // best-effort cleanup
            }

            app?.Dispose();
            automation?.Dispose();
            CleanupBaseDirectory(baseDirectory);
            uiLock.Dispose();
            throw;
        }
    }

    public Window? FindTopLevelWindow(Func<Window, bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var match = TryFindMatchingTopLevel(predicate);
            if (match is not null)
            {
                return match;
            }

            Thread.Sleep(100);
        }

        return null;
    }

    public void Dispose()
    {
        try
        {
            CloseProcessWindowsGracefully(TimeSpan.FromSeconds(3));
        }
        catch
        {
            // best-effort, force-kill below регардлесс
        }

        try
        {
            App.Kill();
        }
        catch
        {
            // best-effort cleanup
        }

        try
        {
            App.Dispose();
        }
        catch
        {
            // best-effort cleanup
        }

        try
        {
            Automation.Dispose();
        }
        catch
        {
            // best-effort cleanup
        }

        CleanupBaseDirectory(BaseDirectory);
        _uiLock.Dispose();
    }

    private static Window? WaitForFirstTopLevelWindow(FlaUIApplication app, UIA3Automation automation, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var first = EnumerateTopLevelWindows(app, automation).FirstOrDefault();
            if (first is not null)
            {
                return first;
            }

            Thread.Sleep(100);
        }

        return null;
    }

    private static IReadOnlyList<Window?> EnumerateTopLevelWindows(FlaUIApplication app, UIA3Automation automation)
    {
        Window[]? topLevels = null;

        try
        {
            topLevels = app.GetAllTopLevelWindows(automation);
        }
        catch
        {
            // fallback to desktop enumeration below
        }

        if (topLevels is { Length: > 0 })
        {
            return topLevels;
        }

        try
        {
            var children = automation.GetDesktop()
                .FindAllChildren(cf =>
                    cf.ByProcessId(app.ProcessId).And(cf.ByControlType(ControlType.Window)));

            return children.Select(c => c.AsWindow()).ToList();
        }
        catch
        {
            return Array.Empty<Window?>();
        }
    }

    private static string ResolveAppExePath()
    {
        var envOverride = Environment.GetEnvironmentVariable(AppExePathEnvVar);

        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return Path.GetFullPath(envOverride);
        }

        var testAssemblyDir = Path.GetDirectoryName(typeof(SmokeTestSession).Assembly.Location)
                              ?? throw new InvalidOperationException("Не удалось определить каталог тестовой сборки");

        var testProjectDirectory = Path.DirectorySeparatorChar + TestProjectName + Path.DirectorySeparatorChar;
        var separatorIndex = testAssemblyDir.LastIndexOf(testProjectDirectory, StringComparison.OrdinalIgnoreCase);

        if (separatorIndex < 0)
        {
            throw new InvalidOperationException(
                $"Каталог тестовой сборки не содержит сегмента {TestProjectName}: {testAssemblyDir}");
        }

        var appAssemblyDir = string.Concat(
            testAssemblyDir.AsSpan(0, separatorIndex + 1),
            AppProjectName,
            testAssemblyDir.AsSpan(separatorIndex + testProjectDirectory.Length - 1));

        return Path.GetFullPath(Path.Combine(appAssemblyDir, AppExeName));
    }

    private static string CreateTempBaseDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PoproshaykaBot-uismoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void SeedConfiguredSettings(string baseDirectory)
    {
        var settingsDir = Path.Combine(baseDirectory, "settings");
        Directory.CreateDirectory(settingsDir);

        const string Json = """
                            {
                              "twitch": {
                                "channel": "smoke-test",
                                "clientId": "smoke-test-client-id",
                                "clientSecret": "smoke-test-client-secret"
                              }
                            }
                            """;

        var path = Path.Combine(settingsDir, "settings.json");
        File.WriteAllText(path, Json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void CleanupBaseDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private void CloseProcessWindowsGracefully(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (TryGetHasExited())
            {
                return;
            }

            var windows = EnumerateTopLevelWindows(App, Automation);

            if (windows.Count == 0)
            {
                Thread.Sleep(100);
                continue;
            }

            foreach (var window in windows)
            {
                if (window is null)
                {
                    continue;
                }

                try
                {
                    window.Close();
                }
                catch
                {
                    // best-effort: process might be exiting, window handle invalid, etc.
                }
            }

            Thread.Sleep(150);
        }
    }

    private bool TryGetHasExited()
    {
        try
        {
            return App.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private Window? TryFindMatchingTopLevel(Func<Window, bool> predicate)
    {
        return EnumerateTopLevelWindows(App, Automation)
            .FirstOrDefault(window => window is not null && predicate(window));
    }
}

internal sealed class SmokeTestSessionOptions
{
    public bool SeedConfiguredSettings { get; init; } = true;

    public TimeSpan? WindowAppearTimeout { get; init; }
}
