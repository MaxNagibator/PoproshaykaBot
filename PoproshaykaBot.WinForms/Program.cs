using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.WinForms.Broadcast;
using PoproshaykaBot.WinForms.Chat;
using PoproshaykaBot.WinForms.Chat.Commands;
using PoproshaykaBot.WinForms.Services.Http;
using PoproshaykaBot.WinForms.Services.Http.Handlers;
using PoproshaykaBot.WinForms.Settings;
using Serilog;
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
        const string OutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .WriteTo.Debug(outputTemplate: OutputTemplate)
            .WriteTo.File("logs/bot_log_.txt", rollingInterval: RollingInterval.Day, outputTemplate: OutputTemplate)
            .CreateLogger();

        try
        {
            Log.Information("Запуск приложения...");
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

                Log.Warning("Обнаружено аномальное потребление памяти: {MemoryBytes} байт", memoryBytes);
                memoryCheckTimer.Stop();

                Task.Run(async () =>
                {
                    await Task.Delay(Interval);
                    Log.Fatal("Принудительное завершение процесса из-за утечки памяти.");
                    await Log.CloseAndFlushAsync();
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
                    var portValidationPassed = ValidateAndResolvePortConflict(settingsManager);

                    if (portValidationPassed)
                    {
                        try
                        {
                            var httpServer = serviceProvider.GetRequiredService<UnifiedHttpServer>();
                            httpServer.StartAsync().GetAwaiter().GetResult();
                            Log.Information("HTTP сервер успешно запущен.");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Ошибка запуска HTTP сервера");
                            MessageBox.Show($"Ошибка запуска HTTP сервера: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        Log.Warning("Не удалось разрешить конфликт портов. HTTP сервер не запущен.");
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
        catch (Exception ex)
        {
            Log.Fatal(ex, "Приложение завершило работу из-за непредвиденной ошибки.");
        }
        finally
        {
            Log.Information("Завершение работы приложения.");
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
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
        services.AddSingleton<BroadcastScheduler>();

        services.AddSingleton<AudienceTracker>();

        services.AddSingleton<IChatCommand, HelloCommand>();
        services.AddSingleton<IChatCommand, DonateCommand>();
        services.AddSingleton<IChatCommand, HowManyMessagesCommand>();
        services.AddSingleton<IChatCommand, BotStatsCommand>();
        services.AddSingleton<IChatCommand, TopUsersCommand>();
        services.AddSingleton<IChatCommand, MyProfileCommand>();
        services.AddSingleton<IChatCommand, ActiveUsersCommand>();
        services.AddSingleton<IChatCommand, ByeCommand>();
        services.AddSingleton<IChatCommand, StreamInfoCommand>();
        services.AddSingleton<IChatCommand, TrumpCommand>();
        services.AddSingleton<IChatCommand, RanksCommand>();
        services.AddSingleton<IChatCommand, RankCommand>();

        services.AddSingleton<ChatCommandProcessor>(sp =>
        {
            var commands = sp.GetServices<IChatCommand>().ToList();
            var processor = new ChatCommandProcessor(commands);
            processor.Register(new HelpCommand(processor.GetAllCommands));
            return processor;
        });

        services.AddSingleton<TwitchChatHandler>();
        services.AddSingleton<IChannelProvider>(sp => sp.GetRequiredService<TwitchChatHandler>());

        services.AddSingleton<BotConnectionManager>();
        services.AddTransient<MainForm>();
    }

    private static bool ValidateAndResolvePortConflict(SettingsManager settingsManager)
    {
        var settings = settingsManager.Current;
        var redirectUri = settings.Twitch.RedirectUri;
        var serverPort = settings.Twitch.HttpServerPort;

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
        {
            Log.Error("Некорректный RedirectUri: {RedirectUri}", redirectUri);
            MessageBox.Show($"Некорректный RedirectUri: {redirectUri}\n\nПожалуйста, исправьте URI в настройках OAuth.",
                "Ошибка конфигурации",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return false;
        }

        int redirectPort;
        if (uri.Port == -1)
        {
            redirectPort = uri.Scheme == "https" ? 443 : 80;
        }
        else
        {
            redirectPort = uri.Port;
        }

        if (redirectPort == serverPort)
        {
            return true;
        }

        Log.Information("Конфликт портов. Обновление порта с {OldPort} на {NewPort}", serverPort, redirectPort);
        settings.Twitch.HttpServerPort = redirectPort;
        settingsManager.SaveSettings(settings);

        var message = $"""
                       Обнаружен конфликт портов:

                       • RedirectUri использует порт: {redirectPort}
                       • HTTP сервер был настроен на порт: {serverPort}

                       Для корректной работы OAuth порт HTTP сервера был автоматически обновлен до {redirectPort}.

                       Если вы хотите использовать другой порт, пожалуйста, измените его вручную в настройках HTTP сервера и RedirectUri.
                       """;

        MessageBox.Show(message,
            "Порт обновлен",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        return true;
    }
}
