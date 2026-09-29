using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Persistence;
using PoproshaykaBot.Core.Settings.Stores;
using System.Text;
using System.Text.Json;

namespace PoproshaykaBot.Core.Statistics;

public sealed class StatisticsFileStore
{
    private const string UserStatisticsFileName = "users_statistics.json";
    private const string BotStatisticsFileName = "bot_statistics.json";
    private const string CommandUsageFileName = "command-usage.json";

    private readonly ILogger<StatisticsFileStore> _logger;
    private readonly string _userStatisticsFilePath;
    private readonly string _botStatisticsFilePath;
    private readonly string _commandUsageFilePath;

    public StatisticsFileStore(ILogger<StatisticsFileStore> logger)
        : this(logger, AppPaths.BaseDirectory)
    {
    }

    internal StatisticsFileStore(ILogger<StatisticsFileStore> logger, string baseDirectory)
    {
        _logger = logger;
        _userStatisticsFilePath = Path.Combine(baseDirectory, UserStatisticsFileName);
        _botStatisticsFilePath = Path.Combine(baseDirectory, BotStatisticsFileName);
        _commandUsageFilePath = Path.Combine(baseDirectory, CommandUsageFileName);
    }

    public Task<StatisticsReadResult<List<UserStatistics>>> LoadUsersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LoadFromFile<List<UserStatistics>>(_userStatisticsFilePath, "пользователей"));
    }

    public Task<StatisticsReadResult<BotStatistics>> LoadBotAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LoadFromFile<BotStatistics>(_botStatisticsFilePath, "бота"));
    }

    public Task<StatisticsReadResult<CommandUsageData>> LoadCommandUsageAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LoadFromFile<CommandUsageData>(_commandUsageFilePath, "команд"));
    }

    public Task SaveCommandUsageAsync(CommandUsageData snapshot, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => SaveToFile(snapshot, _commandUsageFilePath, "команд"), cancellationToken);
    }

    public Task SaveUsersAsync(IReadOnlyList<UserStatistics> snapshot, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => SaveToFile(snapshot, _userStatisticsFilePath, "пользователей"), cancellationToken);
    }

    public Task SaveBotAsync(BotStatistics snapshot, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => SaveToFile(snapshot, _botStatisticsFilePath, "бота"), cancellationToken);
    }

    private StatisticsReadResult<T> LoadFromFile<T>(string filePath, string entityName)
        where T : class
    {
        _logger.LogDebug("Загрузка статистики {EntityName} из файла {FilePath}", entityName, filePath);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Файл статистики {EntityName} не найден. Будут использованы значения по умолчанию", entityName);
            return StatisticsReadResult<T>.NoFile;
        }

        try
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            var data = JsonSerializer.Deserialize<T>(json, JsonStoreOptions.Default);

            if (data == null)
            {
                _logger.LogError("Десериализация статистики {EntityName} вернула null – файл {FilePath} повреждён", entityName, filePath);
                JsonStoreBackup.CreateBackup(filePath, "invalid", _logger);
                return StatisticsReadResult<T>.Failure;
            }

            return StatisticsReadResult<T>.FromFile(data);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка чтения или десериализации файла статистики {EntityName}", entityName);
            JsonStoreBackup.CreateBackup(filePath, "invalid", _logger);
            return StatisticsReadResult<T>.Failure;
        }
    }

    private void SaveToFile<T>(T data, string targetFilePath, string entityName)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, JsonStoreOptions.Default);
            AtomicFile.Save(targetFilePath, json, _logger);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка при записи статистики {EntityName} в файл {Path}", entityName, targetFilePath);
            throw new InvalidOperationException($"Не удалось сохранить статистику ({entityName}) в {targetFilePath}", exception);
        }
    }
}
