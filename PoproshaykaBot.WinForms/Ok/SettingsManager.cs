using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PoproshaykaBot.WinForms.Settings;

public class SettingsManager
{
    private readonly ILogger<SettingsManager> _logger;
    private readonly string _settingsDirectory;
    private readonly string _settingsFilePath;

    private readonly object _syncLock = new();
    private readonly SynchronizationContext? _syncContext;

    private AppSettings? _currentSettings;

    public SettingsManager(ILogger<SettingsManager> logger)
    {
        _logger = logger;
        _settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PoproshaykaBot");

        _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");

        _syncContext = SynchronizationContext.Current;
    }

    public event Action<ObsChatSettings>? ChatSettingsChanged;

    public AppSettings Current
    {
        get
        {
            lock (_syncLock)
            {
                return _currentSettings ??= LoadSettingsInternal();
            }
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        _logger.LogDebug("Начало сохранения настроек в {SettingsFilePath}", _settingsFilePath);

        lock (_syncLock)
        {
            try
            {
                Directory.CreateDirectory(_settingsDirectory);

                var json = JsonSerializer.Serialize(settings, GetJsonOptions());
                var tempFilePath = _settingsFilePath + ".tmp";
                File.WriteAllText(tempFilePath, json, Encoding.UTF8);

                _logger.LogDebug("Настройки сериализованы во временный файл {TempFilePath}", tempFilePath);

                var backupCreated = false;
                var backupFilePath = _settingsFilePath + ".bak";

                if (File.Exists(_settingsFilePath))
                {
                    if (!File.Exists(backupFilePath))
                    {
                        File.Copy(_settingsFilePath, backupFilePath, true);
                        backupCreated = true;
                    }

                    var oldFilePath = _settingsFilePath + ".old";
                    File.Replace(tempFilePath, _settingsFilePath, oldFilePath);

                    if (File.Exists(oldFilePath))
                    {
                        try
                        {
                            File.Delete(oldFilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Не удалось удалить старый файл {OldFilePath}", oldFilePath);
                        }
                    }
                }
                else
                {
                    File.Move(tempFilePath, _settingsFilePath);
                }

                _currentSettings = settings;
                _logger.LogInformation("Настройки приложения успешно сохранены");

                if (backupCreated)
                {
                    _logger.LogInformation("Создан бэкап предыдущих настроек: {BackupFilePath}", backupFilePath);
                }

                InvokeChatSettingsChanged(settings.Twitch.ObsChat);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Критическая ошибка при сохранении настроек в {SettingsFilePath}", _settingsFilePath);

                var backupFilePath = _settingsFilePath + ".bak";
                if (File.Exists(backupFilePath))
                {
                    try
                    {
                        File.Copy(backupFilePath, _settingsFilePath, true);
                        _logger.LogInformation("Настройки успешно восстановлены из бэкапа {BackupFilePath}", backupFilePath);
                    }
                    catch (Exception backupException)
                    {
                        _logger.LogError(backupException, "Не удалось восстановить настройки из бэкапа {BackupFilePath}", backupFilePath);
                    }
                }

                throw new InvalidOperationException($"Ошибка сохранения настроек: {exception.Message}", exception);
            }
        }
    }

    private static AppSettings CreateDefaultSettings()
    {
        return new();
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
    }

    private void InvokeChatSettingsChanged(ObsChatSettings obsChatSettings)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => ChatSettingsChanged?.Invoke(obsChatSettings), null);
        }
        else
        {
            ChatSettingsChanged?.Invoke(obsChatSettings);
        }
    }

    private AppSettings LoadSettingsInternal()
    {
        _logger.LogDebug("Начало загрузки настроек из {SettingsFilePath}", _settingsFilePath);

        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                _logger.LogInformation("Файл настроек {SettingsFilePath} не найден. Применяются настройки по умолчанию", _settingsFilePath);
                return CreateDefaultSettings();
            }

            var json = File.ReadAllText(_settingsFilePath, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, GetJsonOptions());

            if (settings == null)
            {
                throw new InvalidOperationException("Не удалось десериализовать настройки (null)");
            }

            _logger.LogInformation("Настройки приложения успешно загружены");
            return settings;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка загрузки или десериализации настроек из файла {SettingsFilePath}", _settingsFilePath);

            CreateBackupFile(_settingsFilePath, "invalid");

            _logger.LogWarning("Из-за ошибки загрузки применяются настройки по умолчанию");
            return CreateDefaultSettings();
        }
    }

    private void CreateBackupFile(string originalPath, string suffix)
    {
        if (!File.Exists(originalPath))
        {
            return;
        }

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var fileName = Path.GetFileNameWithoutExtension(originalPath);
            var extension = Path.GetExtension(originalPath);
            var backupFileName = $"{fileName}.{suffix}-{timestamp}{extension}";
            var backupPath = Path.Combine(Path.GetDirectoryName(originalPath)!, backupFileName);

            File.Copy(originalPath, backupPath, true);
            _logger.LogInformation("Создан бэкап поврежденного файла настроек: {BackupPath}", backupPath);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка при создании бэкапа для файла {OriginalPath}", originalPath);
        }
    }
}
