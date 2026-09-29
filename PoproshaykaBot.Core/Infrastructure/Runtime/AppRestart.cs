using PoproshaykaBot.Core.Update;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace PoproshaykaBot.Core.Infrastructure.Runtime;

public static class AppRestart
{
    public const string RestartAfterArgument = "--restart-after";

    public static readonly TimeSpan HandoffTimeout = TimeSpan.FromSeconds(15);

    public static IReadOnlyList<string> BuildArguments(IReadOnlyList<string> original, int previousProcessId)
    {
        ArgumentNullException.ThrowIfNull(original);

        var arguments = new List<string>(original.Count + 2);
        var skipProcessId = false;

        foreach (var argument in original)
        {
            if (skipProcessId)
            {
                skipProcessId = false;

                if (TryParseProcessId(argument, out _))
                {
                    continue;
                }
            }

            if (Is(argument, RestartAfterArgument))
            {
                skipProcessId = true;
                continue;
            }

            if (Is(argument, UpdateApplier.FinalizeArgument))
            {
                continue;
            }

            arguments.Add(argument);
        }

        arguments.Add(RestartAfterArgument);
        arguments.Add(previousProcessId.ToString(CultureInfo.InvariantCulture));
        return arguments;
    }

    public static int? FindPreviousProcessId(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        for (var index = arguments.Count - 2; index >= 0; index--)
        {
            if (Is(arguments[index], RestartAfterArgument))
            {
                return TryParseProcessId(arguments[index + 1], out var processId) ? processId : null;
            }
        }

        return null;
    }

    public static bool ShouldLaunch(bool restartRequested, bool updateApplied, bool fatalShutdown)
    {
        return restartRequested && !updateApplied && !fatalShutdown;
    }

    public static bool WaitForExit(int processId, TimeSpan timeout)
    {
        Process process;

        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return true;
        }

        using (process)
        {
            try
            {
                return process.WaitForExit(timeout);
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }
    }

    public static bool TryLaunch(string executablePath, IEnumerable<string> arguments, bool useShellExecute, out Exception? failure)
    {
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = useShellExecute,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            failure = null;
            return true;
        }
        catch (Exception exception)
        {
            failure = exception;
            return false;
        }
    }

    private static bool Is(string argument, string name)
    {
        return string.Equals(argument, name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseProcessId(string value, out int processId)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out processId) && processId > 0;
    }
}
