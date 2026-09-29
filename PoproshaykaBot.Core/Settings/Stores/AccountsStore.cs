using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Core.Twitch.Chat;

namespace PoproshaykaBot.Core.Settings.Stores;

public sealed class AccountsStore
{
    private readonly ILogger<AccountsStore>? _logger;
    private readonly string _filePath;
    private readonly JsonStore<AccountsFileDto> _store;

    public AccountsStore(ILogger<AccountsStore>? logger = null, string? filePath = null, SettingsWriteGate? gate = null)
    {
        _logger = logger;
        _filePath = filePath ?? AppPaths.SettingsFile("accounts.json");
        _store = new(_filePath, logger, AccountsTokenRedactor.Redact, describe: DescribeAccounts, gate: gate);

        if (logger?.IsEnabled(LogLevel.Debug) == true)
        {
            var state = _store.Load();

            logger.LogDebug("AccountsStore инициализирован из {FilePath} (bot.login={BotLogin}, broadcaster.login={BroadcasterLogin})",
                _filePath,
                string.IsNullOrEmpty(state.BotAccount?.Login) ? "–" : state.BotAccount.Login,
                string.IsNullOrEmpty(state.BroadcasterAccount?.Login) ? "–" : state.BroadcasterAccount.Login);
        }
    }

    public TwitchAccountSettings LoadBot()
    {
        return Load(TwitchOAuthRole.Bot);
    }

    public TwitchAccountSettings LoadBroadcaster()
    {
        return Load(TwitchOAuthRole.Broadcaster);
    }

    public TwitchAccountSettings Load(TwitchOAuthRole role)
    {
        return TakeAccount(_store.Load(), role);
    }

    public void Mutate(TwitchOAuthRole role, Action<TwitchAccountSettings> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);

        var written = _store.Mutate(state => mutator(TakeAccount(state, role)));

        _logger?.LogDebug("AccountsStore: применена мутация для роли {Role}, {Outcome}",
            role,
            written ? $"состояние сохранено в {_filePath}" : "состояние принято только в памяти до перезапуска");
    }

    public bool TryClearAccessToken(TwitchOAuthRole role, string expectedToken)
    {
        ArgumentNullException.ThrowIfNull(expectedToken);

        var cleared = _store.MutateIf(state =>
        {
            var account = TakeAccount(state, role);

            if (!string.Equals(account.AccessToken, expectedToken, StringComparison.Ordinal))
            {
                return false;
            }

            account.AccessToken = string.Empty;
            account.AccessTokenExpiresAt = null;
            return true;
        });

        if (cleared)
        {
            _logger?.LogInformation("AccountsStore: access-токен роли {Role} очищён по запросу 401-обработчика", role);
        }
        else
        {
            _logger?.LogDebug("AccountsStore.TryClearAccessToken: токен роли {Role} уже изменился – очистка пропущена", role);
        }

        return cleared;
    }

    public bool SaveAll(TwitchAccountSettings bot, TwitchAccountSettings broadcaster)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(broadcaster);

        var written = _store.Save(new()
        {
            BotAccount = bot,
            BroadcasterAccount = broadcaster,
        });

        if (written)
        {
            _logger?.LogInformation("AccountsStore: оба аккаунта заменены целиком и сохранены в {FilePath}", _filePath);
            return true;
        }

        _logger?.LogInformation("AccountsStore: оба аккаунта заменены в памяти до перезапуска, файл {FilePath} переписан снаружи", _filePath);
        return false;
    }

    private static string DescribeAccounts(AccountsFileDto state)
    {
        return $"бот – {SettingsDescriber.Describe(state.BotAccount ?? new())}; "
            + $"вещатель – {SettingsDescriber.Describe(state.BroadcasterAccount ?? new())}";
    }

    private static TwitchAccountSettings TakeAccount(AccountsFileDto state, TwitchOAuthRole role)
    {
        if (role is not (TwitchOAuthRole.Bot or TwitchOAuthRole.Broadcaster))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, null);
        }

        var bot = state.BotAccount ??= new();
        var broadcaster = state.BroadcasterAccount ??= new();

        ApplyScopeDefaults(bot, TwitchScopes.BotRequired);
        ApplyScopeDefaults(broadcaster, TwitchScopes.BroadcasterRequired);

        return role == TwitchOAuthRole.Bot ? bot : broadcaster;
    }

    private static void ApplyScopeDefaults(TwitchAccountSettings account, IReadOnlyList<string> defaultScopes)
    {
        if (account.Scopes.Length == 0)
        {
            account.Scopes = [..defaultScopes];
        }
    }

    private sealed class AccountsFileDto
    {
        public TwitchAccountSettings? BotAccount { get; set; }
        public TwitchAccountSettings? BroadcasterAccount { get; set; }
    }
}
