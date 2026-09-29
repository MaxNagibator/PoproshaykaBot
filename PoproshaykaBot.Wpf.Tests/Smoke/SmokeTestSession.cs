using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using NUnit.Framework.Interfaces;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FlaUIApplication = FlaUI.Core.Application;

namespace PoproshaykaBot.Wpf.Tests.Smoke;

internal sealed class SmokeTestSession : IDisposable
{
    public const string AppExeName = "PoproshaykaBot.Wpf.exe";
    public const string AppExePathEnvVar = "POPROSHAYKA_APP_EXE";
    public const string BaseDirectoryEnvVar = "POPROSHAYKA_BASE_DIR";

    private const string AppProjectName = "PoproshaykaBot.Wpf";
    private const string TestProjectName = "PoproshaykaBot.Wpf.Tests";
    private const string LogsDirectoryName = "logs";
    private const string MainWindowCaptureFileName = "main-window.png";
    private const uint PrintWindowFullContent = 2;
    private const uint OwnerWindowRelation = 4;
    private const int CaptureRenderDelayMilliseconds = 300;

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
            Assert.Fail($"Исполняемый файл приложения не найден: {appPath}. Соберите PoproshaykaBot.Wpf перед запуском UI-тестов.");
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

            var diagnosticsDirectory = TryCreateDiagnosticsDirectory();

            if (diagnosticsDirectory is not null)
            {
                TryCopyLogs(baseDirectory, diagnosticsDirectory);
            }

            CleanupBaseDirectory(baseDirectory);
            uiLock.Dispose();
            throw;
        }
    }

    public Window? FindAppWindow(Func<Window, bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var match = EnumerateAppWindows().FirstOrDefault(window => window is not null && predicate(window));
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
        var diagnosticsDirectory = TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed
            ? TryCreateDiagnosticsDirectory()
            : null;

        if (diagnosticsDirectory is not null)
        {
            TryCaptureMainWindow(diagnosticsDirectory);
        }

        try
        {
            CloseProcessWindowsGracefully(TimeSpan.FromSeconds(3));
        }
        catch
        {
            // best-effort, force-kill below regardless
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

        if (diagnosticsDirectory is not null)
        {
            TryCopyLogs(BaseDirectory, diagnosticsDirectory);
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

    private static List<Window?> EnumerateTopLevelWindows(FlaUIApplication app, UIA3Automation automation)
    {
        var processId = (uint)app.ProcessId;
        var handles = new List<IntPtr>();

        EnumWindows((handle, _) =>
        {
            if (IsWindowVisible(handle)
                && !IsWindowVisible(GetWindow(handle, OwnerWindowRelation))
                && GetWindowThreadProcessId(handle, out var owner) != 0
                && owner == processId)
            {
                handles.Add(handle);
            }

            return true;
        }, IntPtr.Zero);

        var windows = new List<Window?>(handles.Count);

        foreach (var handle in handles)
        {
            try
            {
                var element = automation.FromHandle(handle);

                if (element.ControlType == ControlType.Window)
                {
                    windows.Add(element.AsWindow());
                }
            }
            catch
            {
                // best-effort: window handle may be transitioning
            }
        }

        return windows;
    }

    internal static string ResolveAppExePath()
    {
        var envOverride = Environment.GetEnvironmentVariable(AppExePathEnvVar);

        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return Path.GetFullPath(envOverride);
        }

        var (testAssemblyDir, separatorIndex) = LocateTestProjectSegment();
        var testProjectDirectory = Path.DirectorySeparatorChar + TestProjectName + Path.DirectorySeparatorChar;

        var appAssemblyDir = string.Concat(
            testAssemblyDir.AsSpan(0, separatorIndex + 1),
            AppProjectName,
            testAssemblyDir.AsSpan(separatorIndex + testProjectDirectory.Length - 1));

        return Path.GetFullPath(Path.Combine(appAssemblyDir, AppExeName));
    }

    [DllImport("user32.dll")]
    private static extern int PrintWindow(IntPtr hwnd, IntPtr deviceContext, uint flags);

    private delegate bool EnumWindowsCallback(IntPtr hwnd, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint relation);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    private static (string TestAssemblyDirectory, int SeparatorIndex) LocateTestProjectSegment()
    {
        var testAssemblyDir = Path.GetDirectoryName(typeof(SmokeTestSession).Assembly.Location)
                              ?? throw new InvalidOperationException("Не удалось определить каталог тестовой сборки");

        var testProjectDirectory = Path.DirectorySeparatorChar + TestProjectName + Path.DirectorySeparatorChar;
        var separatorIndex = testAssemblyDir.LastIndexOf(testProjectDirectory, StringComparison.OrdinalIgnoreCase);

        if (separatorIndex < 0)
        {
            throw new InvalidOperationException(
                $"Каталог тестовой сборки не содержит сегмента {TestProjectName}: {testAssemblyDir}");
        }

        return (testAssemblyDir, separatorIndex);
    }

    private static string? TryCreateDiagnosticsDirectory()
    {
        try
        {
            var (testAssemblyDir, separatorIndex) = LocateTestProjectSegment();
            var root = Path.Combine(testAssemblyDir[..separatorIndex], "artifacts", "ui-smoke");
            var name = SanitizeDirectoryName(TestContext.CurrentContext.Test.Name);

            var candidate = Path.Combine(root, name);
            var suffix = 1;

            while (Directory.Exists(candidate))
            {
                candidate = Path.Combine(root, $"{name}-{suffix}");
                suffix++;
            }

            Directory.CreateDirectory(candidate);
            return candidate;
        }
        catch
        {
            // best-effort diagnostics
            return null;
        }
    }

    private static string SanitizeDirectoryName(string testName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(testName.Length);

        foreach (var symbol in testName)
        {
            sanitized.Append(Array.IndexOf(invalid, symbol) >= 0 ? '_' : symbol);
        }

        var result = sanitized.ToString().Trim().Trim('.');
        return string.IsNullOrEmpty(result) ? "ui-smoke" : result;
    }

    private static void TryCopyLogs(string baseDirectory, string diagnosticsDirectory)
    {
        try
        {
            if (string.IsNullOrEmpty(baseDirectory))
            {
                return;
            }

            var logsDirectory = Path.Combine(baseDirectory, LogsDirectoryName);

            if (!Directory.Exists(logsDirectory))
            {
                return;
            }

            var target = Path.Combine(diagnosticsDirectory, LogsDirectoryName);
            Directory.CreateDirectory(target);

            foreach (var file in Directory.EnumerateFiles(logsDirectory))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }
        }
        catch
        {
            // best-effort diagnostics
        }
    }

    private void TryCaptureMainWindow(string diagnosticsDirectory)
    {
        try
        {
            if (!MainWindow.IsAvailable)
            {
                return;
            }

            MainWindow.Move(0, 0);
            Thread.Sleep(CaptureRenderDelayMilliseconds);
            var handle = MainWindow.Properties.NativeWindowHandle.ValueOrDefault;
            var bounds = MainWindow.BoundingRectangle;

            if (handle == IntPtr.Zero || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using var bitmap = new Bitmap(bounds.Width, bounds.Height);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                var deviceContext = graphics.GetHdc();

                try
                {
                    if (PrintWindow(handle, deviceContext, PrintWindowFullContent) == 0)
                    {
                        return;
                    }
                }
                finally
                {
                    graphics.ReleaseHdc(deviceContext);
                }
            }

            bitmap.Save(Path.Combine(diagnosticsDirectory, MainWindowCaptureFileName), ImageFormat.Png);
        }
        catch
        {
            // best-effort diagnostics
        }
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
        var startedAt = DateTime.UtcNow;
        var deadline = startedAt + timeout;
        var ownedOnlyDeadline = startedAt + TimeSpan.FromTicks(timeout.Ticks / 2);

        while (DateTime.UtcNow < deadline)
        {
            if (TryGetHasExited())
            {
                return;
            }

            var owned = DistinctByHandle(EnumerateOwnedWindows());

            var windows = owned.Count > 0 && DateTime.UtcNow < ownedOnlyDeadline
                ? owned
                : DistinctByHandle(owned.Concat(EnumerateTopLevelWindows(App, Automation)));

            if (windows.Count == 0)
            {
                Thread.Sleep(100);
                continue;
            }

            foreach (var window in windows)
            {
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

    private static List<Window> DistinctByHandle(IEnumerable<Window?> windows)
    {
        var handles = new HashSet<IntPtr>();
        var result = new List<Window>();

        foreach (var window in windows)
        {
            if (window is null)
            {
                continue;
            }

            IntPtr handle;

            try
            {
                handle = window.Properties.NativeWindowHandle.ValueOrDefault;
            }
            catch
            {
                // best-effort: window handle may be transitioning
                handle = IntPtr.Zero;
            }

            if (handle == IntPtr.Zero || handles.Add(handle))
            {
                result.Add(window);
            }
        }

        return result;
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

    private IEnumerable<Window?> EnumerateAppWindows()
    {
        foreach (var window in EnumerateTopLevelWindows(App, Automation))
        {
            yield return window;
        }

        foreach (var window in EnumerateOwnedWindows())
        {
            yield return window;
        }
    }

    private IEnumerable<Window?> EnumerateOwnedWindows()
    {
        Window[]? modals = null;

        try
        {
            modals = MainWindow.ModalWindows;
        }
        catch
        {
            // best-effort: window handle may be transitioning
        }

        if (modals is not null)
        {
            foreach (var modal in modals)
            {
                yield return modal;
            }
        }

        AutomationElement[]? nested = null;

        try
        {
            nested = MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Window));
        }
        catch
        {
            // best-effort: owned modal dialogs are nested under the owner in the WPF UIA tree
        }

        if (nested is not null)
        {
            foreach (var element in nested)
            {
                yield return element.AsWindow();
            }
        }
    }
}

internal sealed class SmokeTestSessionOptions
{
    public bool SeedConfiguredSettings { get; init; } = true;

    public TimeSpan? WindowAppearTimeout { get; init; }
}
