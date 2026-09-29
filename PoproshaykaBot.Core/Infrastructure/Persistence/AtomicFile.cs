using Microsoft.Extensions.Logging;
using System.Text;

namespace PoproshaykaBot.Core.Infrastructure.Persistence;

public static class AtomicFile
{
    public static void Save(string targetPath, string content, ILogger? logger = null, bool keepBackup = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);
        ArgumentNullException.ThrowIfNull(content);

        Save(targetPath, tempPath => File.WriteAllText(tempPath, content, Encoding.UTF8), logger, keepBackup);
    }

    public static void Save(string targetPath, byte[] content, ILogger? logger = null, bool keepBackup = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);
        ArgumentNullException.ThrowIfNull(content);

        Save(targetPath, tempPath => File.WriteAllBytes(tempPath, content), logger, keepBackup);
    }

    public static void Save(string targetPath, Action<string> writeTemporary, ILogger? logger = null, bool keepBackup = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);
        ArgumentNullException.ThrowIfNull(writeTemporary);

        var directory = Path.GetDirectoryName(targetPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        WriteAtomic(targetPath, writeTemporary, logger, keepBackup);
    }

    private static void WriteAtomic(string targetPath, Action<string> writeTemporary, ILogger? logger, bool keepBackup)
    {
        var tempPath = targetPath + ".tmp";
        var backupPath = targetPath + ".bak";

        writeTemporary(tempPath);

        if (File.Exists(targetPath))
        {
            File.Copy(targetPath, backupPath, true);

            var oldPath = targetPath + ".old";
            ReplaceTarget(tempPath, targetPath, oldPath, logger);
            TryDelete(oldPath, logger);
        }
        else
        {
            File.Move(tempPath, targetPath);
        }

        if (!keepBackup)
        {
            TryDelete(backupPath, logger);
        }
    }

    private static void ReplaceTarget(string tempPath, string targetPath, string oldPath, ILogger? logger)
    {
        try
        {
            File.Replace(tempPath, targetPath, oldPath);
        }
        catch
        {
            TryRestoreFromBackup(targetPath, logger);
            throw;
        }
    }

    private static void TryRestoreFromBackup(string targetPath, ILogger? logger)
    {
        var backupPath = targetPath + ".bak";

        if (!File.Exists(backupPath))
        {
            return;
        }

        try
        {
            File.Copy(backupPath, targetPath, true);
            logger?.LogInformation("Содержимое {TargetPath} восстановлено из бэкапа {BackupPath}", targetPath, backupPath);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Не удалось восстановить {TargetPath} из бэкапа {BackupPath}", targetPath, backupPath);
        }
    }

    private static void TryDelete(string path, ILogger? logger)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Не удалось удалить временный файл {Path}", path);
        }
    }
}
