using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Persistence;
using PoproshaykaBot.Core.Settings.Migrations;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Users;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PoproshaykaBot.Core.Settings;

public class SettingsManager
{
    private readonly ILogger<SettingsManager> _logger;
    private readonly string _settingsFilePath;
    private readonly JsonStore<AppSettings> _store;
    private readonly object _syncLock = new();

    private AppSettings? _currentSettings;

    public SettingsManager(ILogger<SettingsManager> logger, string? settingsFilePath = null, SettingsWriteGate? gate = null)
    {
        _logger = logger;
        _settingsFilePath = settingsFilePath ?? AppPaths.SettingsFile("settings.json");
        _store = new(_settingsFilePath, logger, parser: ParseFile, describe: SettingsDescriber.Describe, gate: gate);
    }

    public virtual AppSettings Current
    {
        get
        {
            lock (_syncLock)
            {
                return _currentSettings ??= _store.Load();
            }
        }
    }

    public virtual bool SaveSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _logger.LogDebug("Начало сохранения user-настроек в {SettingsFilePath}", _settingsFilePath);

        lock (_syncLock)
        {
            try
            {
                var written = _store.Save(settings);
                _currentSettings = _store.Load();
                LogApplied(written);

                return written;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Критическая ошибка при сохранении настроек в {SettingsFilePath}", _settingsFilePath);
                throw new InvalidOperationException($"Ошибка сохранения настроек: {exception.Message}", exception);
            }
        }
    }

    public virtual bool Mutate(Action<AppSettings> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);

        lock (_syncLock)
        {
            try
            {
                var written = _store.Mutate(mutator);
                _currentSettings = _store.Load();
                LogApplied(written);

                return written;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Критическая ошибка при сохранении настроек в {SettingsFilePath}", _settingsFilePath);
                throw new InvalidOperationException($"Ошибка сохранения настроек: {exception.Message}", exception);
            }
        }
    }

    public virtual void UpdateCurrent(Action<AppSettings> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);

        lock (_syncLock)
        {
            mutator(_currentSettings ??= _store.Load());
            _logger.LogDebug("Настройки изменены только в памяти, файл {SettingsFilePath} не тронут", _settingsFilePath);
        }
    }

    private void LogApplied(bool written)
    {
        if (written)
        {
            _logger.LogInformation("User-настройки приложения сохранены");
            return;
        }

        _logger.LogInformation("User-настройки приложения приняты в памяти и действуют до перезапуска: файл {SettingsFilePath} переписан снаружи, и класть поверх него прочитанное раньше нельзя",
            _settingsFilePath);
    }

    private void SanitizeRanks(AppSettings settings)
    {
        var ranks = settings.Ranks.Ranks;

        if (ranks.Count == 0)
        {
            settings.Ranks.Ranks = new RanksSettings().Ranks;
            _logger.LogWarning("Раздел ranks был пустой – восстановлены значения по умолчанию");
            return;
        }

        var hasInvalid = ranks.Any(rank =>
            string.IsNullOrEmpty(rank.Emoji) || string.IsNullOrEmpty(rank.Name));

        if (!hasInvalid)
        {
            return;
        }

        settings.Ranks.Ranks = new RanksSettings().Ranks;
        _logger.LogWarning("В разделе ranks обнаружены повреждённые записи (null emoji/name) – список рангов восстановлен из дефолтов");
    }

    private AppSettings ParseFile(string json)
    {
        var node = JsonNode.Parse(json);

        if (node is not JsonObject root)
        {
            throw new InvalidOperationException("Корневой элемент settings.json не является JSON-объектом");
        }

        var baseDirectory = Path.GetDirectoryName(_settingsFilePath)!;

        if (SettingsMigrator.Migrate(root, _logger, baseDirectory).Changed)
        {
            JsonStoreBackup.CreateBackup(_settingsFilePath, "pre-migration", _logger, AccountsTokenRedactor.Redact);
            var migratedJson = root.ToJsonString(JsonStoreOptions.Default);
            AtomicFile.Save(_settingsFilePath, migratedJson, _logger, keepBackup: false);
            _logger.LogInformation("Настройки мигрированы в актуальный формат и сохранены");
        }

        var settings = root.Deserialize<AppSettings>(JsonStoreOptions.Default);

        if (settings == null)
        {
            throw new InvalidOperationException("Не удалось десериализовать настройки (null)");
        }

        SanitizeRanks(settings);

        _logger.LogInformation("Настройки приложения загружены");
        return settings;
    }
}
