using PoproshaykaBot.WinForms.Models;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Timers;
using Timer = System.Timers.Timer;

namespace PoproshaykaBot.WinForms;

public class StatisticsCollector : IAsyncDisposable
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
    private readonly Timer _autoSaveTimer;
    private readonly Dictionary<string, UserStatistics> _userStatistics;
    private readonly object _userStatisticsLock = new();
    private BotStatistics _botStatistics;
    private bool _hasChanges;
    private bool _disposed;

    public StatisticsCollector(TimeSpan? autoSaveInterval = null)
    {
        var interval = autoSaveInterval ?? DefaultAutoSaveInterval;

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentException("Интервал автосохранения должен быть больше нуля.", nameof(autoSaveInterval));
        }

        _statisticsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PoproshaykaBot");

        _userStatisticsFilePath = Path.Combine(_statisticsDirectory, "users_statistics.json");
        _botStatisticsFilePath = Path.Combine(_statisticsDirectory, "bot_statistics.json");

        _userStatistics = new();
        _botStatistics = BotStatistics.Create();
        _hasChanges = false;

        _autoSaveTimer = new(interval.TotalMilliseconds);
        _autoSaveTimer.Elapsed += OnAutoSaveTimerElapsed;
        _autoSaveTimer.AutoReset = true;
    }

    public async Task StartAsync()
    {
        try
        {
            await LoadStatisticsAsync();
            _autoSaveTimer.Start();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Ошибка запуска сборщика статистики: {exception.Message}", exception);
        }
    }

    public async Task StopAsync()
    {
        _autoSaveTimer.Stop();

        try
        {
            await SaveStatisticsAsync(true);
        }
        catch (Exception exception)
        {
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

        lock (_userStatisticsLock)
        {
            if (_userStatistics.TryGetValue(userId, out var statistic))
            {
                statistic.UpdateName(username);
            }
            else
            {
                _userStatistics[userId] = UserStatistics.Create(userId, username);
            }

            _userStatistics[userId].IncrementMessageCount();
        }

        _botStatistics.IncrementMessagesProcessed();
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
        _botStatistics.UpdateUptime();
        return _botStatistics;
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
        return UpdateUserMessages(userId, delta, (stats, d) =>
        {
            stats.BonusMessageCount += d;
        });
    }

    public bool DecrementUserMessages(string userId, ulong delta)
    {
        return UpdateUserMessages(userId, delta, (stats, d) =>
        {
            stats.ShtrafMessageCount += d;
        });
    }

    public Task SaveNowAsync()
    {
        return SaveStatisticsAsync(true);
    }

    public void ResetBotStartTime()
    {
        _botStatistics.ResetStartTime();
        MarkAsChanged();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    public async Task LoadStatisticsAsync()
    {
        try
        {
            var userStats = await LoadUserStatisticsAsync();
            var botStats = await LoadBotStatisticsAsync();

            lock (_userStatisticsLock)
            {
                _userStatistics.Clear();

                foreach (var (id, userStatistic) in userStats)
                {
                    _userStatistics[id] = userStatistic;
                }
            }

            _botStatistics = botStats;
            _hasChanges = false;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Ошибка загрузки статистики: {exception.Message}", exception);
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync();

        _autoSaveTimer.Stop();
        _autoSaveTimer.Dispose();
        _disposed = true;
    }

    private async void OnAutoSaveTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            await SaveStatisticsAsync();
        }
        catch (Exception)
        {
            // TODO: Добавить логирование ошибок автосохранения
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
        if (!force && !_hasChanges)
        {
            return;
        }

        List<UserStatistics> snapshot;
        lock (_userStatisticsLock)
        {
            snapshot = _userStatistics.Values.ToList();
        }

        try
        {
            await SaveUserStatisticsAsync(snapshot);
            await SaveBotStatisticsAsync(_botStatistics);
            _hasChanges = false;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Ошибка сохранения статистики: {exception.Message}", exception);
        }
    }

    private async Task<Dictionary<string, UserStatistics>> LoadUserStatisticsAsync()
    {
        try
        {
            if (!File.Exists(_userStatisticsFilePath))
            {
                return new();
            }

            var json = await File.ReadAllTextAsync(_userStatisticsFilePath, Encoding.UTF8);
            var userStatisticsList = JsonSerializer.Deserialize<List<UserStatistics>>(json, JsonOptions);

            if (userStatisticsList != null)
            {
                return userStatisticsList.ToDictionary(x => x.UserId, x => x);
            }

            return new();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Ошибка загрузки статистики пользователей: {exception.Message}");
            CreateBackupFile(_userStatisticsFilePath, "invalid");
            return new();
        }
    }

    private async Task SaveUserStatisticsAsync(List<UserStatistics> userStatistics)
    {
        try
        {
            Directory.CreateDirectory(_statisticsDirectory);

            await Task.Run(() =>
            {
                var userStatisticsList = userStatistics;
                var json = JsonSerializer.Serialize(userStatisticsList, JsonOptions);

                var tempFilePath = _userStatisticsFilePath + ".tmp";
                File.WriteAllText(tempFilePath, json, Encoding.UTF8);

                var backupCreated = false;

                if (File.Exists(_userStatisticsFilePath) && !File.Exists(_userStatisticsFilePath + ".bak"))
                {
                    File.Copy(_userStatisticsFilePath, _userStatisticsFilePath + ".bak", true);
                    backupCreated = true;
                }

                File.Replace(tempFilePath, _userStatisticsFilePath, _userStatisticsFilePath + ".old");

                var oldFilePath = _userStatisticsFilePath + ".old";

                if (File.Exists(oldFilePath))
                {
                    try
                    {
                        File.Delete(oldFilePath);
                    }
                    catch
                    {
                    }
                }

                if (backupCreated)
                {
                    Console.WriteLine($"Создан бэкап статистики пользователей: {_userStatisticsFilePath}.bak");
                }
            });
        }
        catch (Exception exception)
        {
            if (File.Exists(_userStatisticsFilePath + ".bak"))
            {
                try
                {
                    File.Copy(_userStatisticsFilePath + ".bak", _userStatisticsFilePath, true);
                    Console.WriteLine("Восстановлена статистика пользователей из бэкапа");
                }
                catch (Exception backupException)
                {
                    Console.WriteLine($"Ошибка восстановления статистики пользователей из бэкапа: {backupException.Message}");
                }
            }

            throw new InvalidOperationException($"Ошибка сохранения статистики пользователей: {exception.Message}", exception);
        }
    }

    private async Task<BotStatistics> LoadBotStatisticsAsync()
    {
        try
        {
            if (!File.Exists(_botStatisticsFilePath))
            {
                return BotStatistics.Create();
            }

            var json = await File.ReadAllTextAsync(_botStatisticsFilePath, Encoding.UTF8);
            var botStatistics = JsonSerializer.Deserialize<BotStatistics>(json, JsonOptions);

            return botStatistics ?? throw new InvalidOperationException("Не удалось десериализовать статистику");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Ошибка загрузки статистики бота: {exception.Message}");
            CreateBackupFile(_botStatisticsFilePath, "invalid");
            return BotStatistics.Create();
        }
    }

    private async Task SaveBotStatisticsAsync(BotStatistics botStatistics)
    {
        try
        {
            Directory.CreateDirectory(_statisticsDirectory);

            await Task.Run(() =>
            {
                var json = JsonSerializer.Serialize(botStatistics, JsonOptions);

                var tempFilePath = _botStatisticsFilePath + ".tmp";
                File.WriteAllText(tempFilePath, json, Encoding.UTF8);

                var backupCreated = false;

                if (File.Exists(_botStatisticsFilePath) && !File.Exists(_botStatisticsFilePath + ".bak"))
                {
                    File.Copy(_botStatisticsFilePath, _botStatisticsFilePath + ".bak", true);
                    backupCreated = true;
                }

                File.Replace(tempFilePath, _botStatisticsFilePath, _botStatisticsFilePath + ".old");

                var oldFilePath = _botStatisticsFilePath + ".old";

                if (File.Exists(oldFilePath))
                {
                    try
                    {
                        File.Delete(oldFilePath);
                    }
                    catch
                    {
                    }
                }

                if (backupCreated)
                {
                    Console.WriteLine($"Создан бэкап статистики бота: {_botStatisticsFilePath}.bak");
                }
            });
        }
        catch (Exception exception)
        {
            if (!File.Exists(_botStatisticsFilePath + ".bak"))
            {
                throw new InvalidOperationException($"Ошибка сохранения статистики бота: {exception.Message}", exception);
            }

            try
            {
                File.Copy(_botStatisticsFilePath + ".bak", _botStatisticsFilePath, true);
                Console.WriteLine("Восстановлена статистика бота из бэкапа");
            }
            catch (Exception backupException)
            {
                Console.WriteLine($"Ошибка восстановления статистики бота из бэкапа: {backupException.Message}");
            }

            throw new InvalidOperationException($"Ошибка сохранения статистики бота: {exception.Message}", exception);
        }
    }

    private void MarkAsChanged()
    {
        _hasChanges = true;
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
            Console.WriteLine($"Создан бэкап поврежденного файла: {backupPath}");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Ошибка создания бэкапа файла: {exception.Message}");
        }
    }
}
