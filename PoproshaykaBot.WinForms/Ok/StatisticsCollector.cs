using Microsoft.Extensions.Logging;
using PoproshaykaBot.WinForms.Models;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PoproshaykaBot.WinForms;

public sealed class StatisticsCollector : IAsyncDisposable
{
    private static readonly TimeSpan DefaultAutoSaveInterval = TimeSpan.FromMinutes(1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _statisticsDirectory;
    private readonly string _userStatisticsFilePath;
    private readonly string _botStatisticsFilePath;
    private readonly TimeSpan _autoSaveInterval = DefaultAutoSaveInterval;
    private readonly ILogger<StatisticsCollector> _logger;

    private readonly Dictionary<string, UserStatistics> _userStatistics;

    private readonly object _userStatisticsLock = new();
    private readonly object _botStatisticsLock = new();
    private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
    private BotStatistics _botStatistics;
    private bool _hasChanges;

    private PeriodicTimer? _periodicTimer;
    private CancellationTokenSource? _cts;
    private Task? _autoSaveTask;
    private bool _disposed;

    public StatisticsCollector(ILogger<StatisticsCollector> logger)
    {
        _logger = logger;

        _statisticsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PoproshaykaBot");
        _userStatisticsFilePath = Path.Combine(_statisticsDirectory, "users_statistics.json");
        _botStatisticsFilePath = Path.Combine(_statisticsDirectory, "bot_statistics.json");

        _userStatistics = new();
        _botStatistics = BotStatistics.Create();
        _hasChanges = false;

        _logger.LogDebug("Инициализация сборщика статистики. Директория: {StatisticsDirectory}", _statisticsDirectory);
    }

    public async Task StartAsync()
    {
        if (_autoSaveTask != null)
        {
            _logger.LogWarning("Попытка повторного запуска сборщика статистики проигнорирована");
            return;
        }

        _logger.LogDebug("Запуск сборщика статистики...");

        try
        {
            await LoadStatisticsAsync();

            _cts = new();
            _periodicTimer = new(_autoSaveInterval);
            _autoSaveTask = RunAutoSaveLoopAsync(_cts.Token);

            _logger.LogInformation("Сборщик статистики успешно запущен (интервал автосохранения: {Interval})", _autoSaveInterval);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Критическая ошибка при запуске сборщика статистики");
            throw new InvalidOperationException($"Ошибка запуска сборщика статистики: {exception.Message}", exception);
        }
    }

    public async Task StopAsync()
    {
        _logger.LogDebug("Инициирована остановка сборщика статистики");

        if (_cts != null)
        {
            await _cts.CancelAsync();
        }

        if (_autoSaveTask != null)
        {
            try
            {
                await _autoSaveTask;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Задача автосохранения успешно отменена");
            }
        }

        _periodicTimer?.Dispose();
        _periodicTimer = null;
        _autoSaveTask?.Dispose();
        _autoSaveTask = null;

        try
        {
            await SaveStatisticsAsync(force: true);
            _logger.LogInformation("Сборщик статистики корректно остановлен");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка при остановке сборщика статистики и финальном сохранении");
            throw new InvalidOperationException($"Ошибка остановки сборщика статистики: {exception.Message}", exception);
        }
    }

    public void TrackMessage(string userId, string username)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("ИД пользователя не может быть null или пустым.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Имя пользователя не может быть null или пустым.", nameof(username));
        }

        _logger.LogDebug("Учет нового сообщения от пользователя {UserId}", userId);

        lock (_userStatisticsLock)
        {
            if (_userStatistics.TryGetValue(userId, out var statistic))
            {
                statistic.UpdateName(username);
            }
            else
            {
                _userStatistics[userId] = UserStatistics.Create(userId, username);
                _logger.LogInformation("Зарегистрирован новый пользователь в статистике: {UserId}", userId);
            }

            _userStatistics[userId].IncrementMessageCount();
        }

        lock (_botStatisticsLock)
        {
            _botStatistics.IncrementMessagesProcessed();
        }

        MarkAsChanged();
    }

    public UserStatistics? GetUserStatistics(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        lock (_userStatisticsLock)
        {
            _userStatistics.TryGetValue(userId, out var userStats);
            return userStats;
        }
    }

    public UserStatistics? GetUserStatisticsByName(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var target = username.TrimStart('@');

        lock (_userStatisticsLock)
        {
            return _userStatistics.Values
                .FirstOrDefault(x => string.Equals(x.Name, target, StringComparison.OrdinalIgnoreCase));
        }
    }

    public BotStatistics GetBotStatistics()
    {
        lock (_botStatisticsLock)
        {
            _botStatistics.UpdateUptime();
            return _botStatistics.Clone();
        }
    }

    public List<UserStatistics> GetTopUsers(int count = 10)
    {
        lock (_userStatisticsLock)
        {
            return _userStatistics.Values
                .OrderByDescending(x => x.TotalMessageCount)
                .Take(count)
                .ToList();
        }
    }

    public List<UserStatistics> GetAllUsers()
    {
        lock (_userStatisticsLock)
        {
            return _userStatistics.Values.ToList();
        }
    }

    public bool IncrementUserMessages(string userId, ulong delta)
    {
        var result = UpdateUserMessages(userId, delta, (stats, d) => stats.BonusMessageCount += d);
        if (result)
        {
            _logger.LogInformation("Пользователю {UserId} начислено {Delta} бонусных сообщений", userId, delta);
        }

        return result;
    }

    public bool DecrementUserMessages(string userId, ulong delta)
    {
        var result = UpdateUserMessages(userId, delta, (stats, d) => stats.ShtrafMessageCount += d);
        if (result)
        {
            _logger.LogInformation("Пользователю {UserId} начислено {Delta} штрафных сообщений", userId, delta);
        }

        return result;
    }

    public Task SaveNowAsync()
    {
        _logger.LogDebug("Принудительный вызов сохранения статистики");
        return SaveStatisticsAsync(force: true);
    }

    public void ResetBotStartTime()
    {
        lock (_botStatisticsLock)
        {
            _botStatistics.ResetStartTime();
        }

        MarkAsChanged();
        _logger.LogInformation("Время старта бота сброшено");
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Освобождение ресурсов StatisticsCollector (Dispose={Disposed})", _disposed);

        if (_disposed)
        {
            return;
        }

        await StopAsync();
        _cts?.Dispose();
        _saveSemaphore.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    public async Task LoadStatisticsAsync()
    {
        _logger.LogDebug("Начало загрузки статистики");
        try
        {
            var userStatsList = await LoadDataFromFileAsync<List<UserStatistics>>(_userStatisticsFilePath, "пользователей") ?? new();
            var botStats = await LoadDataFromFileAsync<BotStatistics>(_botStatisticsFilePath, "бота") ?? BotStatistics.Create();

            lock (_userStatisticsLock)
            {
                _userStatistics.Clear();
                foreach (var userStatistic in userStatsList)
                {
                    _userStatistics[userStatistic.UserId] = userStatistic;
                }
            }

            lock (_botStatisticsLock)
            {
                _botStatistics = botStats;
            }

            _hasChanges = false;
            _logger.LogInformation("Статистика успешно загружена. Загружено пользователей: {UserCount}", _userStatistics.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Сбой при комплексной загрузке статистики");
            throw new InvalidOperationException($"Ошибка загрузки статистики: {exception.Message}", exception);
        }
    }

    private async Task RunAutoSaveLoopAsync(CancellationToken ct)
    {
        _logger.LogDebug("Цикл автосохранения запущен");
        try
        {
            while (await _periodicTimer!.WaitForNextTickAsync(ct))
            {
                await SaveStatisticsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Цикл автосохранения прерван (OperationCanceledException)");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Непредвиденная ошибка в фоновом цикле автосохранения статистики");
        }
    }

    private bool UpdateUserMessages(string userId, ulong delta, Action<UserStatistics, ulong> updateAction)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("ID пользователя не может быть null или пустым.", nameof(userId));
        }

        if (delta == 0)
        {
            return false;
        }

        lock (_userStatisticsLock)
        {
            if (!_userStatistics.TryGetValue(userId, out var stats))
            {
                _logger.LogWarning("Попытка обновить сообщения для неизвестного пользователя {UserId}. Операция пропущена", userId);
                return false;
            }

            updateAction(stats, delta);
            stats.LastSeen = DateTime.UtcNow;
        }

        MarkAsChanged();
        return true;
    }

    private async Task SaveStatisticsAsync(bool force = false)
    {
        await _saveSemaphore.WaitAsync();
        try
        {
            List<UserStatistics> userSnapshot;
            BotStatistics botSnapshot;

            lock (_userStatisticsLock)
            {
                var localHasChanges = _hasChanges;
                if (!force && !localHasChanges)
                {
                    return;
                }

                userSnapshot = _userStatistics.Values.Select(u => u.Clone()).ToList();
                _hasChanges = false;
            }

            lock (_botStatisticsLock)
            {
                botSnapshot = _botStatistics.Clone();
            }

            try
            {
                await SaveDataToFileAsync(userSnapshot, _userStatisticsFilePath, "пользователей");
                await SaveDataToFileAsync(botSnapshot, _botStatisticsFilePath, "бота");

                _logger.LogDebug("Сохранение статистики успешно завершено");
            }
            catch
            {
                lock (_userStatisticsLock)
                {
                    _hasChanges = true;
                }

                throw;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Сбой при попытке сохранения статистики");
            throw new InvalidOperationException($"Ошибка сохранения статистики: {exception.Message}", exception);
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }

    private async Task<T?> LoadDataFromFileAsync<T>(string filePath, string entityName)
    {
        _logger.LogDebug("Загрузка статистики {EntityName} из файла {FilePath}", entityName, filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Файл статистики {EntityName} не найден. Будут использованы значения по умолчанию", entityName);
                return default;
            }

            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            var data = JsonSerializer.Deserialize<T>(json, JsonOptions);

            if (data == null)
            {
                _logger.LogWarning("Десериализация статистики {EntityName} вернула null", entityName);
            }

            return data;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка чтения или десериализации файла статистики {EntityName}", entityName);
            CreateBackupFile(filePath, "invalid");
            return default;
        }
    }

    private async Task SaveDataToFileAsync<T>(T data, string targetFilePath, string entityName)
    {
        try
        {
            Directory.CreateDirectory(_statisticsDirectory);

            await Task.Run(() =>
            {
                var json = JsonSerializer.Serialize(data, JsonOptions);
                var tempFilePath = targetFilePath + ".tmp";

                File.WriteAllText(tempFilePath, json, Encoding.UTF8);
                var backupCreated = false;

                if (File.Exists(targetFilePath))
                {
                    var backupFilePath = targetFilePath + ".bak";
                    if (!File.Exists(backupFilePath))
                    {
                        File.Copy(targetFilePath, backupFilePath, true);
                        backupCreated = true;
                    }

                    var oldFilePath = targetFilePath + ".old";
                    File.Replace(tempFilePath, targetFilePath, oldFilePath);

                    if (File.Exists(oldFilePath))
                    {
                        try
                        {
                            File.Delete(oldFilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Не удалось удалить старый файл {FilePath} при сохранении статистики {EntityName}", oldFilePath, entityName);
                        }
                    }
                }
                else
                {
                    File.Move(tempFilePath, targetFilePath);
                }

                if (backupCreated)
                {
                    _logger.LogInformation("Создан бэкап статистики {EntityName}: {BackupPath}", entityName, targetFilePath + ".bak");
                }
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка при записи статистики {EntityName} в файл", entityName);

            var backupFilePath = targetFilePath + ".bak";
            if (File.Exists(backupFilePath))
            {
                try
                {
                    File.Copy(backupFilePath, targetFilePath, true);
                    _logger.LogWarning("Статистика {EntityName} успешно восстановлена из бэкапа", entityName);
                }
                catch (Exception backupException)
                {
                    _logger.LogCritical(backupException, "Критический сбой: не удалось восстановить статистику {EntityName} из бэкапа", entityName);
                }
            }

            throw;
        }
    }

    private void MarkAsChanged()
    {
        lock (_userStatisticsLock)
        {
            _hasChanges = true;
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
            _logger.LogInformation("Создан аварийный бэкап поврежденного файла: {BackupPath}", backupPath);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Сбой при создании аварийного бэкапа файла {OriginalPath}", originalPath);
        }
    }
}
