using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Diagnostics;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PoproshaykaBot.Core.Infrastructure.Runtime;

internal static class ProcessTree
{
    private const string UnknownProcessName = "неизвестный процесс";

    public static long SelfMemoryBytes()
    {
        using var process = Process.GetCurrentProcess();
        return process.PrivateMemorySize64;
    }

    public static IReadOnlyList<ChildProcessUsage> ChildProcesses(ILogger logger)
    {
        var parentByPid = SnapshotParentMap();

        if (parentByPid.Count == 0)
        {
            return [];
        }

        var childrenByParent = new Dictionary<int, List<int>>();
        foreach (var (pid, parentPid) in parentByPid)
        {
            if (!childrenByParent.TryGetValue(parentPid, out var siblings))
            {
                siblings = [];
                childrenByParent[parentPid] = siblings;
            }

            siblings.Add(pid);
        }

        var byName = new Dictionary<string, (int Count, long Bytes)>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<int>();
        var visited = new HashSet<int> { Environment.ProcessId };
        pending.Enqueue(Environment.ProcessId);

        while (pending.Count > 0)
        {
            if (!childrenByParent.TryGetValue(pending.Dequeue(), out var children))
            {
                continue;
            }

            foreach (var childPid in children)
            {
                if (!visited.Add(childPid))
                {
                    continue;
                }

                pending.Enqueue(childPid);

                try
                {
                    using var child = Process.GetProcessById(childPid);
                    var name = string.IsNullOrWhiteSpace(child.ProcessName) ? UnknownProcessName : child.ProcessName;
                    var previous = byName.GetValueOrDefault(name);
                    byName[name] = (previous.Count + 1, previous.Bytes + child.PrivateMemorySize64);
                }
                catch (Exception exception)
                {
                    logger.LogDebug(exception, "Watchdog: процесс {Pid} недоступен для замера памяти", childPid);
                }
            }
        }

        return byName
            .Select(pair => new ChildProcessUsage(pair.Key, pair.Value.Count, pair.Value.Bytes))
            .OrderByDescending(usage => usage.Bytes)
            .ToArray();
    }

    private static Dictionary<int, int> SnapshotParentMap()
    {
        const uint Th32csSnapprocess = 0x00000002;
        var map = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(Th32csSnapprocess, 0);

        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
        {
            return map;
        }

        try
        {
            var entry = new ProcessEntry32 { dwSize = (uint)Marshal.SizeOf<ProcessEntry32>() };

            if (!Process32First(snapshot, ref entry))
            {
                return map;
            }

            do
            {
                map[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
            } while (Process32Next(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return map;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }
}
