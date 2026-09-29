using Microsoft.Extensions.Logging;

namespace PoproshaykaBot.Core.Settings.Stores;

internal static class JsonStoreBackup
{
    public static void CreateBackup(string filePath, string suffix, ILogger? logger = null)
    {
        CreateBackup(filePath, suffix, logger, null);
    }

    public static void CreateBackup(string filePath, string suffix, ILogger? logger, Func<string, string>? redactor)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var directory = Path.GetDirectoryName(filePath)!;
            var name = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var backupPath = Path.Combine(directory, $"{name}.{suffix}-{timestamp}{extension}");

            if (redactor == null)
            {
                File.Copy(filePath, backupPath, true);
                logger?.LogInformation("Создан бэкап файла настроек: {BackupPath}", backupPath);
                return;
            }

            string raw;
            try
            {
                raw = File.ReadAllText(filePath);
            }
            catch (Exception readException)
            {
                logger?.LogError(readException,
                    "Не удалось прочитать {FilePath} для редактируемого бэкапа – бэкап не создан, чтобы не утечь сырое содержимое",
                    filePath);

                return;
            }

            string redacted;
            try
            {
                redacted = redactor(raw);
            }
            catch (Exception redactException)
            {
                logger?.LogError(redactException,
                    "Сбой редактора при подготовке бэкапа {FilePath} – бэкап не создан",
                    filePath);

                return;
            }

            File.WriteAllText(backupPath, redacted);
            logger?.LogInformation("Создан редактированный бэкап файла настроек: {BackupPath}", backupPath);
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Ошибка при создании бэкапа для файла {FilePath}", filePath);
        }
    }
}
