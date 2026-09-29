using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Settings.Debugging;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Settings.Update;
using PoproshaykaBot.Core.Twitch.Auth;

namespace PoproshaykaBot.Core.Settings;

public static class SettingsDescriber
{
    private const string Unset = "–";

    public static string Describe(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var twitch = settings.Twitch;
        var messages = twitch.Messages;
        var broadcast = twitch.AutoBroadcast;
        var lifecycle = twitch.BotLifecycleAutomation;

        return $"канал {Value(twitch.Channel)}, clientId {Present(twitch.ClientId)}, clientSecret {Present(twitch.ClientSecret)}, "
            + $"redirectUri {Value(twitch.RedirectUri)}, порт {twitch.HttpServerPort}, аккаунт чата {twitch.ChatDisplayAccount}, "
            + $"лимит {twitch.MessagesAllowedInPeriod} сообщений за {twitch.ThrottlingPeriodSeconds} с, "
            + $"приветствие {OnOff(messages.WelcomeEnabled)}, прощание {OnOff(messages.FarewellEnabled)}, "
            + $"подключение {OnOff(messages.ConnectionEnabled)}, отключение {OnOff(messages.DisconnectionEnabled)}, "
            + $"наказание {OnOff(messages.PunishmentEnabled)}, поощрение {OnOff(messages.RewardEnabled)}, "
            + $"авторассылка {OnOff(broadcast.AutoBroadcastEnabled)} каждые {broadcast.BroadcastIntervalMinutes} мин, "
            + $"уведомления о стриме {OnOff(broadcast.StreamStatusNotificationsEnabled)}, "
            + $"автоподключение на онлайн {OnOff(lifecycle.AutoConnectOnStreamOnline)}, "
            + $"автоотключение на офлайн {OnOff(lifecycle.AutoDisconnectOnStreamOffline)}, "
            + $"спец-команды: пользователей {settings.SpecialCommands.AllowedUsers.Count}, рангов {settings.Ranks.Ranks.Count}";
    }

    public static string Describe(TwitchAccountSettings account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var expiresAt = account.AccessTokenExpiresAt is { } expiry
            ? expiry.UtcDateTime.ToString("u")
            : Unset;

        return $"логин {Value(account.Login)}, userId {Value(account.UserId)}, "
            + $"access-токен {Present(account.AccessToken)}, refresh-токен {Present(account.RefreshToken)}, "
            + $"действует до {expiresAt}, скоупов {account.Scopes.Length} (сохранено {account.StoredScopes.Length})";
    }

    public static string Describe(ObsIntegrationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return $"интеграция {OnOff(settings.Enabled)}, автоподключение {OnOff(settings.AutoConnect)}, "
            + $"{Value(settings.Host)}:{settings.Port}, пароль {Present(settings.Password)}, "
            + $"сцена {Value(settings.SceneName)}, источник {Value(settings.SourceName)} {settings.Width}x{settings.Height}, "
            + $"автосоздание источника {OnOff(settings.AutoProvisionBrowserSource)}, "
            + $"сцена по профилю {OnOff(settings.ApplySceneOnProfile)}, профиль по сцене {OnOff(settings.ApplyProfileOnScene)}, "
            + $"источников дашборда {settings.DashboardSourceNames.Count}, микрофон {Present(settings.DashboardMicrophoneName)}, "
            + $"задержка индикатора {settings.DashboardVolumeMeterDelayMs} мс, "
            + $"источников чата для обновления {settings.ChatRefreshSources.Count}, "
            + $"обновление на старте стрима {OnOff(settings.RefreshChatSourcesOnStreamStart)}";
    }

    public static string Describe(ObsChatSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return $"сообщений на экране {settings.MaxMessages}, время {OnOff(settings.ShowTimestamp)}, "
            + $"аватары {OnOff(settings.ShowUserAvatars)}, анимации {OnOff(settings.EnableAnimations)}, "
            + $"затухание {OnOff(settings.EnableMessageFadeOut)} через {settings.MessageLifetimeSeconds} с, "
            + $"кегль {settings.FontSize}, автопрокрутка {OnOff(settings.AutoScrollEnabled)}, "
            + $"картинки из сообщений {OnOff(settings.ShowMessageImages)} (роли {settings.MessageImageRoles}, "
            + $"хостов {settings.MessageImageAllowedHosts?.Count ?? 0}, высота {settings.MessageImageMaxHeightPixels} px)";
    }

    public static string Describe(UpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var lastCheck = settings.LastCheckUtc is { } checkedAt
            ? checkedAt.UtcDateTime.ToString("u")
            : Unset;

        return $"автопроверка {OnOff(settings.AutoCheckEnabled)} каждые {settings.CheckIntervalHours} ч, "
            + $"режим {settings.ApplyMode}, обновление framework-dependent {OnOff(settings.AllowFrameworkDependentUpdate)}, "
            + $"репозиторий {Value(settings.RepositoryOverride)}, пропущенная версия {Value(settings.SkippedVersion)}, "
            + $"последняя проверка {lastCheck}";
    }

    public static string Describe(DebugChannelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return $"отладочный канал {OnOff(settings.IsEnabled)}, канал {Value(settings.Channel)}, "
            + $"отправка сообщений {OnOff(settings.AllowSending)}, недавних каналов {settings.RecentChannels?.Count ?? 0}";
    }

    public static string Describe(BroadcastProfilesSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var lastApplied = settings.LastAppliedProfileId is { } id ? id.ToString() : Unset;

        return $"профилей {settings.Profiles.Count}, последний применённый {lastApplied}";
    }

    public static string Describe(CommandSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var overrides = settings.Commands.Values.Where(x => x is not null).ToList();
        var disabled = overrides.Count(x => !x.Enabled);
        var retargeted = overrides.Count(x => x.ResponseTarget is not null);
        var restricted = overrides.Count(x => x.Access is not null);

        return $"цель ответа по умолчанию {settings.DefaultResponseTarget}, переопределений {overrides.Count} "
            + $"(выключено {disabled}, со своей целью {retargeted}, со своими правами {restricted})";
    }

    public static string Describe(PollsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var templates = settings.ChatTemplates;
        var killSwitch = settings.AutoTriggerKillSwitchDateUtc is { } date ? date.ToString("u") : Unset;

        return $"профилей опросов {settings.Profiles.Count}, объявление старта {OnOff(templates.StartEnabled)}, "
            + $"прогресс {OnOff(templates.ProgressEnabled)} каждые {templates.ProgressAnnounceIntervalSeconds} с, "
            + $"завершение {OnOff(templates.EndEnabled)}, прерывание {OnOff(templates.TerminatedEnabled)}, "
            + $"архив {OnOff(templates.ArchivedEnabled)}, стоп-дата автозапуска {killSwitch}, "
            + $"история до {settings.HistoryMaxItems}";
    }

    private static string OnOff(bool value)
    {
        return value ? "вкл" : "выкл";
    }

    private static string Present(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "нет" : "есть";
    }

    private static string Value(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Unset : value;
    }
}
