using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.WinForms.Broadcast;
using PoproshaykaBot.WinForms.Chat;
using PoproshaykaBot.WinForms.Services;
using PoproshaykaBot.WinForms.Services.Http;
using PoproshaykaBot.WinForms.Services.Http.Handlers;
using PoproshaykaBot.WinForms.Settings;
using System.Diagnostics;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.EventSub.Websockets;
using Timer = System.Windows.Forms.Timer;

namespace PoproshaykaBot.WinForms;

public static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var memoryCheckTimer = new Timer();

        const long ThresholdMb = 1024;
        const long MemoryThreshold = 1024 * 1024 * ThresholdMb;
        const int Interval = 1000;

        memoryCheckTimer.Interval = Interval;
        memoryCheckTimer.Tick += (_, _) =>
        {
            var currentProcess = Process.GetCurrentProcess();
            var memoryBytes = currentProcess.PrivateMemorySize64;

            if (memoryBytes <= MemoryThreshold)
            {
                return;
            }

            memoryCheckTimer.Stop();

            Task.Run(async () =>
            {
                await Task.Delay(Interval);
                currentProcess.Kill();
            });

            var memoryMb = memoryBytes / (1024.0 * 1024.0);
            var message =
                $"""
                 Внимание! Обнаружено аномальное потребление ресурсов.

                 Текущее использование: {memoryMb:F2} MB
                 Установленный лимит: {ThresholdMb:F2} MB

                 У вас есть {Interval / 1000} секунд, чтобы прочитать это сообщение, затем процесс будет убит принудительно.
                 """;

            MessageBox.Show(message, "Критическая нагрузка на память", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Environment.FailFast("Пользователь подтвердил закрытие при утечке памяти.");
        };

        memoryCheckTimer.Start();

        var services = new ServiceCollection();
        ConfigureServices(services);

        var serviceProvider = services.BuildServiceProvider();
        try
        {
            var settingsManager = serviceProvider.GetRequiredService<SettingsManager>();
            var statistics = serviceProvider.GetRequiredService<StatisticsCollector>();

            var twitchSettings = settingsManager.Current.Twitch;
            var httpServerEnabled = twitchSettings.HttpServerEnabled;

            if (httpServerEnabled)
            {
                var portValidator = serviceProvider.GetRequiredService<PortValidator>();
                var portValidationPassed = portValidator.ValidateAndResolvePortConflict();

                if (portValidationPassed)
                {
                    try
                    {
                        var httpServer = serviceProvider.GetRequiredService<UnifiedHttpServer>();
                        httpServer.StartAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка запуска HTTP сервера: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Не удалось разрешить конфликт портов. HTTP сервер не запущен.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            statistics.LoadStatisticsAsync().GetAwaiter().GetResult();

            var mainForm = serviceProvider.GetRequiredService<MainForm>();
            Application.Run(mainForm);
        }
        finally
        {
            serviceProvider.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
        });

        services.AddHttpClient();

        services.AddSingleton<SettingsManager>();

        services.AddSingleton<TwitchAPI>(sp =>
        {
            var settings = sp.GetRequiredService<SettingsManager>().Current.Twitch;
            var api = new TwitchAPI();
            api.Settings.ClientId = settings.ClientId;
            return api;
        });

        services.AddSingleton<TwitchClient>(sp =>
        {
            var settings = sp.GetRequiredService<SettingsManager>().Current.Twitch;
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = settings.MessagesAllowedInPeriod,
                ThrottlingPeriod = TimeSpan.FromSeconds(settings.ThrottlingPeriodSeconds),
                DisconnectWait = 0,
            };

            var wsClient = new WebSocketClient(clientOptions);
            return new(wsClient);
        });

        services.AddSingleton<StatisticsCollector>();
        services.AddSingleton<TwitchOAuthService>();
        services.AddSingleton<ChatHistoryManager>();
        services.AddTransient<PortValidator>();
        services.AddSingleton<SseService>();
        services.AddSingleton<OAuthHandler>();
        services.AddSingleton<OverlayHandler>();
        services.AddSingleton<SseHandler>();
        services.AddSingleton<ApiHistoryHandler>();
        services.AddSingleton<ApiChatSettingsHandler>();

        services.AddSingleton<Router>(sp =>
        {
            var oauth = sp.GetRequiredService<OAuthHandler>();
            var overlay = sp.GetRequiredService<OverlayHandler>();
            var sse = sp.GetRequiredService<SseHandler>();
            var apiHistory = sp.GetRequiredService<ApiHistoryHandler>();
            var apiSettings = sp.GetRequiredService<ApiChatSettingsHandler>();

            var router = new Router();
            router.Register("/", oauth);
            router.Register("/chat", overlay);
            router.Register("/events", sse);
            router.Register("/api/history", apiHistory);
            router.Register("/api/chat-settings", apiSettings);

            router.Register("/assets/obs.css", new StaticContentHandler("PoproshaykaBot.WinForms.Assets.obs.css", "text/css; charset=utf-8"));
            router.Register("/assets/obs.js", new StaticContentHandler("PoproshaykaBot.WinForms.Assets.obs.js", "application/javascript; charset=utf-8"));
            router.Register("/favicon.ico", new StaticContentHandler("PoproshaykaBot.WinForms.icon.ico", "image/x-icon"));

            return router;
        });

        services.AddSingleton<UnifiedHttpServer>(sp =>
        {
            var settings = sp.GetRequiredService<SettingsManager>();
            var history = sp.GetRequiredService<ChatHistoryManager>();
            var router = sp.GetRequiredService<Router>();
            var sseService = sp.GetRequiredService<SseService>();

            return new(history, router, sseService, settings.Current.Twitch.HttpServerPort);
        });

        services.AddSingleton<EventSubWebsocketClient>();
        services.AddSingleton<StreamStatusManager>();
        services.AddSingleton<ChatDecorationsProvider>();
        services.AddSingleton<UserRankService>();
        services.AddSingleton<UserMessagesManagementService>();

        services.AddSingleton<TwitchChatMessenger>();
        services.AddSingleton<BroadcastScheduler>(sp =>
        {
            var messenger = sp.GetRequiredService<TwitchChatMessenger>();
            var settingsManager = sp.GetRequiredService<SettingsManager>();
            var streamStatusManager = sp.GetRequiredService<StreamStatusManager>();

            var messageProvider = new Func<int, string>(counter =>
            {
                var template = settingsManager.Current.Twitch.AutoBroadcast.BroadcastMessageTemplate;
                var info = streamStatusManager.CurrentStream;

                return template
                    .Replace("{counter}", counter.ToString())
                    .Replace("{title}", info?.Title ?? string.Empty)
                    .Replace("{game}", info?.GameName ?? string.Empty)
                    .Replace("{viewers}", info?.ViewerCount.ToString() ?? string.Empty);
            });

            return new(messenger, settingsManager, messageProvider);
        });

        services.AddSingleton<BotFactory>();

        services.AddTransient<Func<Bot>>(sp =>
        {
            var factory = sp.GetRequiredService<BotFactory>();
            return factory.Create;
        });

        services.AddTransient<BotConnectionManager>();
        services.AddTransient<MainForm>();
    }
}
