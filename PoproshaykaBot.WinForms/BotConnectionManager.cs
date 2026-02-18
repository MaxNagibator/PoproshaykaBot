namespace PoproshaykaBot.WinForms;

public sealed class BotConnectionManager(Func<string, Bot> botFactory, TwitchOAuthService tokenService) : IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _connectionTask;
    private bool _disposed;

    public event EventHandler<BotConnectionResult>? ConnectionCompleted;

    public event EventHandler<string>? ProgressChanged;

    public bool IsBusy => _connectionTask is { IsCompleted: false };

    public void StartConnection()
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Connection is already in progress");
        }

        _cts = new();
        _connectionTask = ConnectAsync(_cts.Token);
    }

    public void CancelConnection()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CancelConnection();
        _cts?.Dispose();
        _disposed = true;
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        try
        {
            ReportProgress("Получение токена доступа...");

            var accessToken = await tokenService.GetAccessTokenAsync(ct);

            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Не удалось получить токен доступа. Проверьте настройки OAuth.");
            }

            ReportProgress("Создание экземпляра бота...");
            var bot = botFactory(accessToken);

            ReportProgress("Подключение к серверу Twitch...");
            await bot.ConnectAsync(ct);

            ReportProgress("Подключение установлено успешно");
            ConnectionCompleted?.Invoke(this, BotConnectionResult.Success(bot));
        }
        catch (OperationCanceledException)
        {
            ConnectionCompleted?.Invoke(this, BotConnectionResult.Cancelled());
        }
        catch (Exception exception)
        {
            ConnectionCompleted?.Invoke(this, BotConnectionResult.Failed(exception));
        }
    }

    private void ReportProgress(string message)
    {
        ProgressChanged?.Invoke(this, message);
    }
}

public class BotConnectionResult
{
    private BotConnectionResult(BotConnectionStatus status, Bot? bot = null, Exception? exception = null)
    {
        Status = status;
        Bot = bot;
        Exception = exception;
    }

    public Bot? Bot { get; }

    public Exception? Exception { get; }

    public bool IsSuccess => Status == BotConnectionStatus.Success;

    public bool IsCancelled => Status == BotConnectionStatus.Cancelled;

    public bool IsFailed => Status == BotConnectionStatus.Failed;

    private BotConnectionStatus Status { get; }

    public static BotConnectionResult Success(Bot bot)
    {
        return new(BotConnectionStatus.Success, bot);
    }

    public static BotConnectionResult Cancelled()
    {
        return new(BotConnectionStatus.Cancelled);
    }

    public static BotConnectionResult Failed(Exception exception)
    {
        return new(BotConnectionStatus.Failed, exception: exception);
    }
}

public enum BotConnectionStatus
{
    Success,
    Cancelled,
    Failed,
}
